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
using DeNelle.Core.Diagnostics;

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

        // DEF-178: self-bootstrap. Previously NOTHING added this manager to any
        // scene (no builder, no editor setup, no bootstrap), so Instance was always
        // null and EVERY HitStopManager.DoImpact(...) call site — TowerCombat shots,
        // HeroHealth death/hit beats, LevelUpVFXController, EnvironmentVFX — was a
        // silent no-op. The tiered impact stack was wired but DEAD. Mirroring
        // CombatFeedbackManager / VfxPool / ProjectilePool, bring it up on load so
        // those existing calls actually fire. AfterSceneLoad so Camera.main exists.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[HitStopManager]").AddComponent<HitStopManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // Guarantee timeScale is restored if this object is torn down mid-stop
            // (scene unload / domain reload) so a freeze can never leak past us.
            if (_hitStopRoutine != null || _frozenScaleApplied >= 0f) Time.timeScale = 1f;
            _frozenScaleApplied = -1f;
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

        /// <summary>
        /// The scale the ACTIVE stop applied, or -1 when no stop is in flight. Read by the
        /// <see cref="LateUpdate"/> deadline watchdog so it only ever un-does OUR freeze and
        /// never stamps over a legitimate slow-mo owned by someone else.
        /// </summary>
        private float _frozenScaleApplied = -1f;

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

        // =====================================================================
        //  DEADLINE WATCHDOG (owner report 2026-08-20: town frozen after a fight)
        // =====================================================================

        /// <summary>
        /// Restore <c>Time.timeScale</c> if our stop outlived its deadline.
        ///
        /// <para>CAPTURED DEFECT. After an arena fight in the hub the owner was left unable to move.
        /// The trace named it exactly: <c>[Flow:HeroOwner] ... inputSuppressed=False timeScale=0.04
        /// dt=0.0013</c> — input was never blocked, the world was running at 4% speed, and it stayed
        /// there for 182 consecutive samples over three minutes. 0.04 is this class's own value
        /// (<c>HitTier.Medium</c>, and <c>Enemy.cs</c>'s death stop), applied at the instant the
        /// battle resolved and the victory modal opened.</para>
        ///
        /// <para>WHY A COROUTINE CANNOT BE THE ONLY RESTORE PATH. <see cref="HitStopRoutine"/>
        /// writes a GLOBAL and hands the only restore to a coroutine — and a coroutine stops
        /// silently whenever its GameObject is deactivated, taking the restore with it and leaving
        /// the global stuck. <c>OnDestroy</c> does not fire for a deactivation, so the existing
        /// guard there cannot cover it. A frozen world is not a survivable outcome for a mechanism
        /// this cosmetic, so the restore is now owned by a deadline that runs every frame on the
        /// UNSCALED clock — it cannot be starved by the very slow-down it exists to undo.</para>
        ///
        /// <para>It only reverts a scale THIS class applied (see <see cref="_frozenScaleApplied"/>),
        /// so an ArenaDeathCam or WaveCelebration slow-mo passes through untouched.</para>
        ///
        /// <para>And it FAILS LOUDLY rather than healing quietly: the exact leak this fixes went
        /// unexplained precisely because nothing announced it. If this fires, the next capture names
        /// the cause instead of starting from zero (CLAUDE.md sec. 12).</para>
        /// </summary>
        private void LateUpdate()
        {
            if (_frozenScaleApplied < 0f) return;
            if (Time.unscaledTime <= _hitStopEndTime + DeadlineGraceSeconds) return;

            float leaked = Time.timeScale;
            bool stillOurs = Mathf.Abs(leaked - _frozenScaleApplied) < 0.001f;

            if (_hitStopRoutine != null) { StopCoroutine(_hitStopRoutine); _hitStopRoutine = null; }
            _frozenScaleApplied = -1f;

            if (!stillOurs)
            {
                // Someone else owns the clock now. Our stop is simply over; say nothing and
                // above all do NOT stamp 1f over their slow-mo.
                return;
            }

            Time.timeScale = 1f;
            FlowTrace.Fail("HitStop",
                $"HIT-STOP LEAK RECOVERED: timeScale was still {leaked:F2} " +
                $"{(Time.unscaledTime - _hitStopEndTime):F2}s past its deadline - the restore " +
                "coroutine never completed (its GameObject was almost certainly deactivated, which " +
                "stops coroutines without firing OnDestroy). Restored to 1. The world was running " +
                $"at {leaked * 100f:F0}% speed, which reads to the player as frozen controls.");
        }

        /// <summary>How far past the deadline to wait before calling it a leak. Generous enough that
        /// a frame hitch or a one-frame ordering race is never mistaken for one.</summary>
        private const float DeadlineGraceSeconds = 0.25f;

        private void OnDisable()
        {
            // A coroutine dies on deactivation and OnDestroy does NOT fire for it, so without this
            // a mid-stop SetActive(false) leaves the global pinned. This is the cheap half of the
            // same fix as the watchdog above; both exist because either alone can be out-raced.
            if (_frozenScaleApplied >= 0f && Mathf.Abs(Time.timeScale - _frozenScaleApplied) < 0.001f)
            {
                Time.timeScale = 1f;
                FlowTrace.Warn("HitStop",
                    "hit-stop host DISABLED mid-stop - timeScale restored to 1 here, because the " +
                    "restore coroutine has just been killed by the deactivation.");
            }
            _frozenScaleApplied = -1f;
            _hitStopRoutine = null;
        }

        private IEnumerator HitStopRoutine(float frozenScale, float duration)
        {
            Time.timeScale = frozenScale;
            _frozenScaleApplied = frozenScale;
            yield return new WaitForSecondsRealtime(duration);
            // DEF-178: restore to the game's normal 1f, NOT a captured `prev`. The
            // project has no slow-mo system — normal time is always 1 — and capturing
            // `prev` risked pinning a frozen value if CombatFeedbackManager's own
            // hit-stop (the only other Time.timeScale owner) overlapped this one on
            // the same frame. Restoring to 1 makes the two managers safe to coexist.
            Time.timeScale = 1f;
            _hitStopRoutine = null;
            _frozenScaleApplied = -1f;
        }

        /// <summary>
        /// Shake the camera. Delegates to ThirdPersonCameraFollow.Shake().
        /// If no camera component is found, the call is silently ignored.
        /// <para>
        /// HONOURS THE PLAYER PREFERENCE (2026-08-16). This path holds a TYPED, cached camera
        /// reference rather than going through CameraShakeBridge's reflection resolve, so it is
        /// deliberately left as a direct call — but it gates on CameraShakeBridge.Enabled, the
        /// same "camerashake" preference the bridge reads, so the accessibility toggle covers
        /// this seam too. Before this guard, every HitStopManager tier and EnvironmentVFX's
        /// environmental shake ignored the setting entirely.
        /// </para>
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (!CameraShakeBridge.Enabled) return;
            EnsureCamera();
            _camera?.Shake(intensity, duration);
        }

        /// <summary>
        /// Apply a brief roll + pitch kick to the camera, then spring back smoothly.
        /// This adds an angular jolt that feels more dramatic than positional shake alone.
        /// Gated on the same "camerashake" preference (2026-08-16): it is angular rather than
        /// positional, but to the player it is the same involuntary camera motion the comfort
        /// toggle exists to suppress, so opting out of shake must opt out of this too.
        /// </summary>
        public void CameraKick(float rollDeg, float pitchDeg, float duration)
        {
            if (!CameraShakeBridge.Enabled) return;
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
