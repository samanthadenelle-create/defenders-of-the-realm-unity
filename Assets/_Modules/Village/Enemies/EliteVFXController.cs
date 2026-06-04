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

            VFXType spawnVfx = isBoss ? VFXType.Boss_Spawn : VFXType.Elite_Spawn;
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

        /// <summary>
        /// Call from <see cref="Enemy"/> Die() instead of the normal death VFX.
        /// Enemy.cs checks for this component before falling back to the default
        /// VfxPool.SpawnDeathBurst path.
        /// </summary>
        public void OnEliteDeath()
        {
            VFXType deathVfx = isBoss ? VFXType.Boss_Death : VFXType.Elite_Death;
            VFXManager.Play(deathVfx, transform.position);

            if (isBoss)
            {
                // Heavy shake on boss death: intensity 0.7, duration 0.7 s.
                CameraShakeBridge.Shake(0.7f, 0.7f);
                // AudioService.Instance?.PlaySfx(SfxId.BossDeath);
            }
            else
            {
                // Medium shake on elite death: intensity 0.3, duration 0.3 s.
                CameraShakeBridge.Shake(0.3f, 0.3f);
            }
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
