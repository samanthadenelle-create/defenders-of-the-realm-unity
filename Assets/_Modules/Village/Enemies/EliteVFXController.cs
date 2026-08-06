// =============================================================================
// EliteVFXController — WO-66: boss / elite enemy VFX differentiation.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Add to any enemy prefab alongside EnemyBrain to give it elite/boss visuals.
// Set isElite or isBoss in the Inspector.
//
// INTEGRATION:
//   • Enemy.Die() checks for EliteVFXController and calls OnEliteDeath() instead
//     of the normal death VFX path (see Enemy.cs edit, WO-66).
//   • EnemyBrain.TryAttack() (if implemented) calls OnEliteAttack(hitPos).
//   • CameraShakeBridge.Shake() is used — same pattern as the rest of the project.
//     No CameraShakeManager / ShakeTier exists; those references are mapped to
//     CameraShakeBridge intensity floats (Heavy ≈ 0.5, Medium ≈ 0.3).
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Add to any enemy prefab alongside <see cref="EnemyBrain"/> to give it
    /// elite or boss visuals — pulsing aura, dramatic spawn, and a beefed-up
    /// death explosion. Set <see cref="isElite"/> or <see cref="isBoss"/> in
    /// the Inspector.
    /// </summary>
    public class EliteVFXController : MonoBehaviour
    {
        public bool isElite = false;
        public bool isBoss  = false;

        [Header("Aura")]
        public ParticleSystem auraParticles;
        [Range(1f, 3f)] public float auraPulseSpeed = 1.2f;

        [Header("Spawn")]
        public float spawnDramaticDelay = 0.5f;

        // ── Internals ─────────────────────────────────────────────────────────
        private Light _auraLight;
        private float _baseAuraIntensity;

        private void Start()
        {
            _auraLight = GetComponentInChildren<Light>();
            _baseAuraIntensity = _auraLight != null ? _auraLight.intensity : 1f;

            if (auraParticles != null) auraParticles.Play();

            if (isBoss || isElite)
            {
                StartCoroutine(PulseAura());
                StartCoroutine(DramaticSpawnRoutine());
            }
        }

        // ── Aura ─────────────────────────────────────────────────────────────

        private IEnumerator PulseAura()
        {
            while (true)
            {
                float pulse = Mathf.Sin(Time.time * auraPulseSpeed * Mathf.PI) * 0.5f + 0.5f;
                if (_auraLight != null)
                    _auraLight.intensity = Mathf.Lerp(
                        _baseAuraIntensity * 0.6f,
                        _baseAuraIntensity * (isBoss ? 1.8f : 1.3f),
                        pulse);
                yield return null;
            }
        }

        // ── Spawn ─────────────────────────────────────────────────────────────

        private IEnumerator DramaticSpawnRoutine()
        {
            yield return new WaitForSeconds(spawnDramaticDelay);

            // WO-893: delegate to the shared rule rather than re-deciding it here, so a
            // hand-placed prefab carrying this component and a pooled enemy driven by
            // Enemy.FireSpawnTell cannot pick different art for the same tier.
            VFXType spawnVfx = SpawnVfxFor(isBoss, isElite);
            VFXManager.Play(spawnVfx, transform.position);

            if (isBoss)
            {
                // Heavy camera shake: intensity 0.5, duration 0.5 s.
                CameraShakeBridge.Shake(0.5f, 0.5f);
                // AudioService.Instance?.PlaySfx(SfxId.BossSpawn);  // wired when SfxId lands
            }
            else
            {
                // Elite gets a lighter shake.
                CameraShakeBridge.Shake(0.25f, 0.3f);
            }
        }

        // ── Death ─────────────────────────────────────────────────────────────
        //
        // WO-886 — THE DEATH TIER RULE HAS ONE HOME, AND IT IS HERE.
        //
        // This component is authored to be dropped on a prefab, and a grep of every
        // .prefab / .unity / .asset in the tree finds it on NONE of them — so
        // Enemy.Die()'s GetComponent<EliteVFXController>() has always returned null and
        // OnEliteDeath has never run in the shipped game. The 0.7 boss camera shake
        // WO-886 asks for as a felt criterion therefore never fired either; every kill,
        // boss included, got the flat 0.18 regular shake.
        //
        // The fix is NOT to auto-attach this component. Its Start() also drives an aura
        // light pulse and a DramaticSpawnRoutine (Boss_Spawn VFX + a spawn shake) —
        // attaching it to every elite would ship three unrequested felt changes under a
        // death-VFX work order. Instead the tier rule is lifted into the two statics
        // below, Enemy drives them straight off its enemies.json stat block (which is the
        // only species signal the pool/factory spawn path actually sets), and this
        // component delegates to them. One rule, two call sites, zero drift — and a
        // hand-placed prefab that DOES carry this component still behaves identically.

        /// <summary>
        /// The death VFXType for a tier. Boss outranks elite; a plain enemy returns
        /// <see cref="VFXType.None"/> so the caller keeps its own species-derived burst.
        /// </summary>
        public static VFXType DeathVfxFor(bool isBoss, bool isElite)
        {
            if (isBoss)  return VFXType.Boss_Death;
            if (isElite) return VFXType.Elite_Death;
            return VFXType.None;
        }

        /// <summary>
        /// WO-893 — THE SPAWN TIER RULE, in the same one place as the death rule above and
        /// for the same reason: this component is attached to no prefab in the tree (WO-886
        /// grepped every .prefab/.unity/.asset and found none), so
        /// <see cref="DramaticSpawnRoutine"/> has never run in the shipped game and neither
        /// Elite_Spawn nor Boss_Spawn had ever played. Lifting the rule to a static lets
        /// <c>Enemy</c> drive it off its enemies.json stat block - the only species signal
        /// the pool/factory spawn path actually sets - without auto-attaching this component
        /// and silently shipping its aura light pulse as well.
        /// <para>
        /// Unlike <see cref="DeathVfxFor"/>, a plain enemy does NOT return
        /// <see cref="VFXType.None"/>: the STANDARD spawn is exactly the moment WO-893
        /// exists to add ("mobs no longer pop from nothing"), so every tier returns a real
        /// type and the three form a ladder - standard materialise, elite rise, boss set
        /// piece. All three are Family B one-shots; a spawn must never hold a loop slot.
        /// </para>
        /// </summary>
        public static VFXType SpawnVfxFor(bool isBoss, bool isElite)
        {
            if (isBoss)  return VFXType.Boss_Spawn;
            if (isElite) return VFXType.Elite_Spawn;
            return VFXType.Enemy_Spawn;
        }

        /// <summary>
        /// Fires the camera shake that matches a death's tier: boss 0.7, elite 0.3,
        /// everything else the regular 0.18 kill punch. These are the exact values the
        /// instance path has always declared — lifted, not re-invented.
        /// </summary>
        public static void PlayDeathShake(bool isBoss, bool isElite)
        {
            if (isBoss)       CameraShakeBridge.Shake(0.7f,  0.7f);
            else if (isElite) CameraShakeBridge.Shake(0.3f,  0.3f);
            else              CameraShakeBridge.Shake(0.18f, 0.22f);
        }

        /// <summary>
        /// Call from <see cref="Enemy"/> Die() instead of the normal death VFX.
        /// Enemy.cs checks for this component before falling back to the default
        /// VfxPool.SpawnDeathBurst path.
        /// </summary>
        public void OnEliteDeath()
        {
            VFXType deathVfx = DeathVfxFor(isBoss, isElite);
            if (deathVfx == VFXType.None)
            {
                // Neither flag set: this component was added for its aura/spawn drama only.
                // Fall to the shared ladder floor rather than playing nothing.
                VfxPool.SpawnDeathBurst(transform.position);
            }
            else
            {
                VFXManager.Play(deathVfx, transform.position);
            }

            PlayDeathShake(isBoss, isElite);
            // AudioService.Instance?.PlaySfx(SfxId.BossDeath);  // wired when SfxId lands
        }

        // ── Attack ────────────────────────────────────────────────────────────

        /// <summary>
        /// Call from EnemyBrain attack logic for the elite/boss attack VFX.
        /// </summary>
        public void OnEliteAttack(Vector3 hitPos)
        {
            VFXType attackVfx = isBoss
                ? VFXType.Boss_AttackImpact
                : VFXType.Impact_ExplosionAether;
            VFXManager.Play(attackVfx, hitPos);

            if (isBoss)
            {
                // Medium shake on boss attack: intensity 0.25, duration 0.25 s.
                CameraShakeBridge.Shake(0.25f, 0.25f);
            }
        }
    }
}
