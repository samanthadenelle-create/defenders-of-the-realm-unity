// =============================================================================
// AuraController — drives a persistent aura ParticleSystem scaled with pet level.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Placed in Village/Pets/ (not Pets/) because it references VFXManager and
// VFXType, which live in DeNelle.Village. DeNelle.Pets cannot reference Village.
//
// SETUP:
//   1. Attach to any pet prefab alongside Animator and PetBrain.
//   2. Drag the looping aura ParticleSystem into auraPrefab (or place it as a
//      child of the prefab — Awake() will find it via GetComponentInChildren).
//   3. Optionally drag an orbiting sparks PS into orbitSparksPrefab.
//   4. Call SetLevel(int) when pet XP level changes. Call PlayLevelUpBurst()
//      for the celebration pop.
//
// COLOUR THEMES (wire in Inspector via particle system Color over Lifetime):
//   Fire  (Flame Pup)   — orange/red   — auraPrefab uses warm orange gradient
//   Ice   (Aether Sprite) — blue/cyan  — auraPrefab uses cool blue gradient
//   Storm (future)      — purple/yellow
//   Colour assignment is done in the ParticleSystem asset, not in this script.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives a persistent aura ParticleSystem whose intensity scales with pet level.
    /// Attach to any pet prefab alongside its Animator and PetBrain.
    /// </summary>
    public class AuraController : MonoBehaviour
    {
        [Header("Aura Prefabs (assign per pet type)")]
        [Tooltip("Looping aura ParticleSystem — already a child of this prefab, or drag one in.")]
        public ParticleSystem auraPrefab;

        [Header("Level Scaling")]
        [Tooltip("Emission rate multiplier per level tier.")]
        public float level1EmissionRate = 4f;
        public float level3EmissionRate = 14f;
        public float level5EmissionRate = 28f;

        [Tooltip("Particle start-size scale at level 1 (baseline = 1.0).")]
        public float level1SizeScale  = 0.7f;
        [Tooltip("Particle start-size scale at level 3.")]
        public float level3SizeScale  = 1.0f;
        [Tooltip("Particle start-size scale at level 5+.")]
        public float level5SizeScale  = 1.4f;

        [Header("Orbiting Sparks (Level 5+)")]
        [Tooltip("Secondary orbiting sparks ParticleSystem (optional).")]
        public ParticleSystem orbitSparksPrefab;

        [Header("Level-Up Burst")]
        public float burstIntensityMultiplier = 2.5f;
        public float burstDuration            = 2f;

        // ── State ─────────────────────────────────────────────────────────────

        private int _currentLevel = 1;
        private ParticleSystem _auraInstance;
        private ParticleSystem _orbitInstance;
        private Coroutine _burstRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // If auraPrefab is assigned, instantiate it as a child; otherwise
            // fall back to any ParticleSystem already on/under this GameObject.
            if (auraPrefab != null)
                _auraInstance = Instantiate(auraPrefab, transform.position,
                                            Quaternion.identity, transform);
            else
                _auraInstance = GetComponentInChildren<ParticleSystem>();

            if (orbitSparksPrefab != null)
                _orbitInstance = Instantiate(orbitSparksPrefab, transform.position,
                                             Quaternion.identity, transform);
        }

        private void OnEnable()
        {
            _auraInstance?.Play();
            ApplyLevel(_currentLevel);
        }

        private void OnDisable()
        {
            _auraInstance?.Stop();
            _orbitInstance?.Stop();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Set the pet's current level and update aura intensity immediately.
        /// Call from PetProgression.ApplyBonuses() or equivalent level-up path.
        /// </summary>
        public void SetLevel(int level)
        {
            _currentLevel = Mathf.Max(1, level);
            ApplyLevel(_currentLevel);
        }

        /// <summary>
        /// One-shot celebration burst — scales emission briefly then returns to
        /// normal. Safe to call multiple times (cancels previous burst).
        /// Also fires the LevelUp_Celebration VFX at this pet's world position.
        /// </summary>
        public void PlayLevelUpBurst()
        {
            // Fire the shared level-up VFX via VFXManager (null-safe).
            VFXManager.Instance?.PlayImpact(VFXType.LevelUp_Celebration, transform.position);

            if (_auraInstance == null) return;

            if (_burstRoutine != null) StopCoroutine(_burstRoutine);
            _burstRoutine = StartCoroutine(BurstRoutine());
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void ApplyLevel(int level)
        {
            if (_auraInstance == null) return;

            // Emission rate
            float targetRate = level >= 5 ? level5EmissionRate
                             : level >= 3 ? level3EmissionRate
                             :              level1EmissionRate;
            var em = _auraInstance.emission;
            em.rateOverTime = targetRate;

            // Particle start size
            float targetSize = level >= 5 ? level5SizeScale
                             : level >= 3 ? level3SizeScale
                             :              level1SizeScale;
            var main = _auraInstance.main;
            // Scale the constant; if the original uses a curve this sets the
            // constant override (designer can re-add curve in the prefab).
            main.startSize = new ParticleSystem.MinMaxCurve(
                targetSize * 0.8f, targetSize * 1.2f);

            // Orbit sparks only at level 5+
            if (_orbitInstance != null)
            {
                bool wantOrbit = level >= 5;
                if (wantOrbit && !_orbitInstance.isPlaying) _orbitInstance.Play();
                else if (!wantOrbit && _orbitInstance.isPlaying) _orbitInstance.Stop();
            }
        }

        private IEnumerator BurstRoutine()
        {
            if (_auraInstance == null) yield break;

            var em = _auraInstance.emission;
            float normalRate = em.rateOverTime.constant;
            em.rateOverTime  = normalRate * burstIntensityMultiplier;

            yield return new WaitForSeconds(burstDuration);

            // Restore — re-query in case SetLevel was called during the burst.
            float targetRate = _currentLevel >= 5 ? level5EmissionRate
                             : _currentLevel >= 3 ? level3EmissionRate
                             :                      level1EmissionRate;
            em.rateOverTime = targetRate;
            _burstRoutine   = null;
        }
    }
}
