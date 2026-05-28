// =============================================================================
// HitStopManager — tiered hit stop, screen shake, and camera kick. DEF-VFX-02.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   • Hit Stop  — briefly slows Time.timeScale to 0 or near-0 on heavy impacts,
//                 then snaps back. Creates the "weight" feeling on big hits.
//   • Screen Shake — delegates to ThirdPersonCameraFollow.Shake(). Tiered so
//                    small hits barely shake and boss slams rock the screen.
//   • Camera Kick — a brief angular kick (roll + pitch) then spring-back.
//
// USAGE (call from anywhere — static helpers require no Instance null-check):
//
//   HitStopManager.DoImpact(HitTier.Light);       // arrow hit, glancing blow
//   HitStopManager.DoImpact(HitTier.Heavy);       // knight slam, brute punch
//   HitStopManager.DoImpact(HitTier.Boss);        // boss special, wave boss hit
//   HitStopManager.DoImpact(HitTier.Lethal);      // killing blow on a big enemy
//
//   // Or call each system individually:
//   HitStopManager.Instance.HitStop(0.05f, 0.08f);         // duration, timeScale
//   HitStopManager.Instance.Shake(0.12f, 0.3f);            // intensity, duration
//   HitStopManager.Instance.CameraKick(2f, 1.5f, 0.18f);   // roll, pitch, duration
//
// PERFORMANCE: Time.timeScale manipulation is a game-wide operation. It is safe
// to call on mobile; the freeze lasts < 100ms. Never overlaps — a new hit stop
// while one is active extends or replaces it (keeps whichever is longer).
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    // ── HitTier ───────────────────────────────────────────────────────────────

    /// <summary>Preset tiers for impact feedback. Pass to HitStopManager.DoImpact().</summary>
    public enum HitTier
    {
        /// <summary>Arrow hit, glancing blow, enemy poke — very subtle shake only.</summary>
        Light,
        /// <summary>Knight melee slam, tower bolt, standard enemy hit — brief stop + shake.</summary>
        Medium,
        /// <summary>Hero W/R abilities, large melee — longer stop, visible shake.</summary>
        Heavy,
        /// <summary>Boss special attack, wave-boss impact — strong stop + full camera kick.</summary>
        Boss,
        /// <summary>Killing blow on a mid-boss or brute — maximum impact, freeze + shake + kick.</summary>
        Lethal,
    }

    // ── HitStopManager ────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton manager for hit stop (timeScale pause), screen shake, and
    /// camera kick. Integrates with ThirdPersonCameraFollow.Shake().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitStopManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static HitStopManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Hit Stop Settings")]
        [Tooltip("Minimum TimeScale during a stop (0 = full freeze, 0.05 = near-stop).")]
        [SerializeField, Range(0f, 0.3f)] private float _hitStopMinScale = 0.02f;

        [Header("Camera Shake")]
        [Tooltip("A cached ref to the in-scene ThirdPersonCameraFollow. Auto-resolved if null.")]
        [SerializeField] private ThirdPersonCameraFollow _camera;

        [Header("Quality Gate")]
        [Tooltip("Minimum VFXQuality required for hit stop to fire (Heavy impacts on Low = skip stop, keep shake).")]
        [SerializeField] private VFXQuality _hitStopMinQuality = VFXQuality.Medium;

        // ── Runtime state ─────────────────────────────────────────────────────

        private Coroutine _hitStopRoutine;
        private float     _hitStopEndTime;

        // ── Static convenience ────────────────────────────────────────────────

        /// <summary>
        /// Fire all three feedback systems (hit stop + shake + kick) based on a
        /// preset HitTier. The most common call site.
        /// </summary>
        public static void DoImpact(HitTier tier)
            => Instance?.TriggerTier(tier);

        /// <summary>Screen shake only — useful for environmental rumbles.</summary>
        public static void DoShake(float intensity, float duration)
            => Instance?.Shake(intensity, duration);

        // ── Public instance methods ───────────────────────────────────────────

        /// <summary>
        /// Trigger a preset tier — hit stop + shake + camera kick all at once.
        /// Individual tuning is inside the switch below.
        /// </summary>
        public void TriggerTier(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light:
                    // Subtle — shake only, no stop.
                    Shake(0.04f, 0.15f);
                    break;

                case HitTier.Medium:
                    HitStop(0.06f, 0.04f);
                    Shake(0.08f, 0.25f);
                    break;

                case HitTier.Heavy:
                    HitStop(0.09f, 0.02f);
                    Shake(0.14f, 0.35f);
                    CameraKick(1.5f, 1.0f, 0.15f);
                    break;

                case HitTier.Boss:
                    HitStop(0.12f, 0.0f);
                    Shake(0.22f, 0.50f);
                    CameraKick(3.0f, 2.0f, 0.22f);
                    break;

                case HitTier.Lethal:
                    HitStop(0.18f, 0.0f);
                    Shake(0.30f, 0.60f);
                    CameraKick(4.0f, 3.0f, 0.28f);
                    break;
            }
        }

        /// <summary>
        /// Freeze (or near-freeze) Time.timeScale for <paramref name="duration"/> seconds,
        /// then snap back to 1. Safe to call while another stop is active — takes the
        /// longer/stronger of the two.
        /// </summary>
        /// <param name="duration">Seconds to hold the frozen timeScale.</param>
        /// <param name="frozenScale">TimeScale during the stop (0 = full freeze).</param>
        public void HitStop(float duration, float frozenScale = 0f)
        {
            // Quality gate — skip on Low quality.
            if (VFXManager.Instance != null &&
                (int)VFXManager.Instance.CurrentQuality < (int)_hitStopMinQuality)
                return;

            float endTime = Time.unscaledTime + duration;

            // If an existing stop would end later, leave it running.
            if (_hitStopRoutine != null && endTime <= _hitStopEndTime) return;

            _hitStopEndTime = endTime;
            if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = StartCoroutine(HitStopRoutine(
                Mathf.Max(_hitStopMinScale, frozenScale), duration));
        }

        private IEnumerator HitStopRoutine(float frozenScale, float duration)
        {
            float prev = Time.timeScale;
            Time.timeScale = frozenScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prev;
            _hitStopRoutine = null;
        }

        /// <summary>
        /// Shake the camera. Delegates to ThirdPersonCameraFollow.Shake().
        /// If no camera component is found, the call is silently ignored.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            EnsureCamera();
            _camera?.Shake(intensity, duration);
        }

        /// <summary>
        /// Apply a brief roll + pitch kick to the camera, then spring back smoothly.
        /// This adds an angular jolt that feels more dramatic than positional shake alone.
        /// </summary>
        public void CameraKick(float rollDeg, float pitchDeg, float duration)
        {
            EnsureCamera();
            if (_camera == null) return;
            StartCoroutine(CameraKickRoutine(rollDeg, pitchDeg, duration));
        }

        private IEnumerator CameraKickRoutine(float roll, float pitch, float duration)
        {
            if (_camera == null) yield break;
            var camTransform = _camera.transform;
            Quaternion original = camTransform.localRotation;

            // Apply kick (local Euler offset).
            camTransform.localRotation = original *
                Quaternion.Euler(-pitch, 0f, roll);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                // Smooth spring back using SmoothStep.
                camTransform.localRotation = Quaternion.Slerp(
                    camTransform.localRotation, original, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            camTransform.localRotation = original;
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void EnsureCamera()
        {
            if (_camera != null) return;
            _camera = FindAnyObjectByType<ThirdPersonCameraFollow>();
        }
    }
}
