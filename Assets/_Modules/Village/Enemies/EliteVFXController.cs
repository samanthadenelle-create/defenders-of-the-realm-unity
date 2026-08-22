// =============================================================================
// EliteVFXController — WO-66: boss / elite enemy VFX differentiation.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Add to any enemy prefab alongside EnemyBrain to give it elite/boss visuals.
// Set isElite or isBoss in the Inspector.
//
// INTEGRATION (WO-874 wired it; before that this component was on NO prefab):
//   • Enemy.Configure() AddComponent's this on any enemy whose enemies.json stat block
//     reads boss or elite, then calls ArmForTier(). That is the attach seam - the
//     ruling was "wire it", and a static shortcut is what routed around it once.
//   • Enemy.Die() checks for EliteVFXController and calls OnEliteDeath() instead
//     of the normal death VFX path (see Enemy.cs edit, WO-66).
//   • Enemy.ExecuteContactAttack() calls OnEliteAttack(hitPos) — WO-874.
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

        // WO-874: set by ArmForTier. Start() then stands down, because the CODE path
        // already decided this instance's tier and started its routines - a second
        // start would double the spawn burst and the spawn shake on the same frame.
        private bool _armedByCode;

        // WO-874: true between ArmForTier and OnDisable. The two coroutines below are
        // started from exactly one place each so a pooled body cannot accumulate a
        // second aura pulse on every reuse (that is a per-reuse coroutine leak, and it
        // shows up as an aura that pulses faster the longer the session runs).
        private bool _running;

        /// <summary>
        /// WO-874 - THE ATTACH SEAM, and the reason this component stopped being dead code.
        /// <para>
        /// The owner's 2026-08-04 ruling (RECONFIRMED 2026-08-21) was WIRE IT: attach this
        /// controller on the elite/boss spawn path so its spawn / aura / attack / death
        /// actually fire. Commit <c>4c1da079</c> instead lifted two STATICS out of this file
        /// and called them from <c>Enemy</c>, which delivered the death/spawn tell while
        /// routing around the ruling - so the aura and <see cref="OnEliteAttack"/> had still
        /// never run in the shipped game. This method is what closes that: it is called from
        /// <c>Enemy.Configure</c>, the ONE place every spawn path sets the stat block and
        /// also the pooled-reuse entry point.
        /// </para>
        /// <para>
        /// It must be idempotent and re-armable, because a pooled body's
        /// <see cref="Start"/> runs exactly once for the lifetime of the POOL, not of the
        /// enemy. Re-arming stops whatever the previous life left running and starts fresh.
        /// </para>
        /// </summary>
        public void ArmForTier(bool boss, bool elite)
        {
            isBoss = boss;
            isElite = elite;
            _armedByCode = true;

            CacheAuraLight();
            StopRoutines();

            if (!isBoss && !isElite)
            {
                // Neither flag: nothing tier-specific to drive. Deliberately NOT an error -
                // a caller may arm-then-clear when an enemy is re-Configured to a plain tier
                // on pool reuse, and silently doing nothing is the correct outcome there.
                //
                // ⛔ THE Stop() IS LOAD-BEARING, AND IT USED TO BE A LEAK (fixed 2026-08-22).
                // auraParticles.Play() used to run ABOVE this early return, so a body that had
                // lived as an elite and was reused as a PLAIN mob started its aura and never
                // stopped it: a trash enemy wearing an elite's glow, on a pooled body, which is
                // the hardest class of bug to reproduce because it depends on what the body WAS
                // in a previous life. The component survives Release/Get, so anything it was
                // driving must be explicitly stood down here rather than assumed fresh.
                if (auraParticles != null) auraParticles.Stop();
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EliteVFX", "arm-plain", 5f,
                    $"ArmForTier on '{name}' with neither boss nor elite set - aura/spawn drama " +
                    "stood down for this life (pooled reuse as a plain-tier enemy).");
                return;
            }

            if (auraParticles != null) auraParticles.Play();

            _running = true;
            StartCoroutine(PulseAura());
            StartCoroutine(DramaticSpawnRoutine());

            DeNelle.Core.Diagnostics.FlowTrace.Step("EliteVFX",
                $"armed '{name}' boss={isBoss} elite={isElite} - aura pulse + dramatic spawn running " +
                $"(delay {spawnDramaticDelay:0.##}s); death/attack tells now route through this component.");
        }

        private void CacheAuraLight()
        {
            if (_auraLight != null) return;
            _auraLight = GetComponentInChildren<Light>();
            _baseAuraIntensity = _auraLight != null ? _auraLight.intensity : 1f;
        }

        private void StopRoutines()
        {
            if (!_running) return;
            _running = false;
            StopAllCoroutines();
            // Leave the light where the ladder floor is, not wherever the sine happened to
            // stop: a pooled body reused as a plain enemy would otherwise keep a boss-bright
            // light forever, with nothing left running to bring it down.
            if (_auraLight != null) _auraLight.intensity = _baseAuraIntensity;
        }

        private void OnDisable() => StopRoutines();

        private void Start()
        {
            // WO-874: the HAND-PLACED prefab path. When ArmForTier already ran (the code
            // path, which is now every elite and boss in the game), this is a no-op - the
            // routines are already running and re-starting them here would fire the spawn
            // burst and the camera shake twice on the same frame.
            if (_armedByCode) return;

            CacheAuraLight();

            if (auraParticles != null) auraParticles.Play();

            if (isBoss || isElite)
            {
                _running = true;
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
        // WO-886's ANSWER was to lift the tier rule into the two statics below rather than
        // auto-attach, on the grounds that attaching would ship the aura + spawn drama as
        // unrequested felt changes under a death-VFX work order. That reasoning was sound
        // FOR WO-886's scope and it is now SUPERSEDED, not reversed by accident:
        //
        // ⚠ WO-874 (owner ruling 2026-08-04, RECONFIRMED VERBATIM 2026-08-21 "874 wire
        //   ruling stands") makes the aura + spawn drama the REQUESTED change. The
        //   component is now genuinely AddComponent'd on the elite/boss spawn path
        //   (Enemy.Configure -> EnsureEliteVfx -> ArmForTier), so from here on
        //   GetComponent<EliteVFXController>() returns NON-NULL for every elite and boss
        //   and the OnEliteDeath branch above is live. The statics below are NOT dead:
        //   they remain the ONE home of the tier rule, called by both this component and
        //   the plain-tier path in Enemy that has no component to consult.
        //
        // So: one rule, two call sites, zero drift — and a hand-placed prefab that carries
        // this component still behaves identically (Start() stands down when ArmForTier
        // already ran, so the two entry points cannot double-fire).

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
