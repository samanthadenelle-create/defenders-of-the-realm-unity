// =============================================================================
// HeroLocomotionCadence — STRIDE-POLISH runtime tuning knob (2026-07-02).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS: the walk-feel fix (HeroAnimatorFactory.ApplyLocomotionCadence)
// bakes per-child m_TimeScale into the Knight's Locomotion blend tree (walk x2,
// run x3) to cancel HeroBodySwapper's global 0.5x animator playback — netting
// walk 1.0x / run 1.5x cadence. Those timeScales are BAKE-TIME (editor asset
// values); the owner cannot felt-tune them without a controller rebuild. This
// component exposes the one knob that IS cleanly runtime-tunable: the animator's
// global `speed`, applied ONLY while the base layer sits in a locomotion state
// (Locomotion / InjuredLocomotion) and NOT in transition — so the tuned
// cast/attack/hit pacing (WO-217 AttackSpeed, ActorAnimator.ShapeAttackTempo
// hitstop) is never touched.
//
//   PlayerPrefs "anim.runCadence"  (default 1.5 = the baked net run cadence)
//
// Semantics: the value IS the desired net run-clip cadence multiplier vs
// real-time. 1.5 (default) = exactly the baked tuning = ZERO behavior change.
// 1.8 = +20% faster locomotion cadence (less foot-skate at 6 m/s travel);
// walk scales proportionally (walk net = value / 1.5). Applied as
//   anim.speed = baseSpeed * (value / BakedNetRunCadence)   while in locomotion,
//   anim.speed = baseSpeed                                   restored on exit.
//
// Dev-panel buttons (DevPanelController "Animation (feel)") nudge the pref live.
// Guarded: no Animator / no controller / inactive = silent no-op every frame.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime locomotion-cadence enforcer for the hero. Scales the Animator's
    /// global playback speed ONLY while the base layer is in a locomotion state,
    /// so the owner can felt-tune stride cadence via the "anim.runCadence"
    /// PlayerPrefs knob without a controller rebuild and without disturbing the
    /// tuned cast/attack pacing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroLocomotionCadence : MonoBehaviour
    {
        /// <summary>PlayerPrefs key for the net run-cadence multiplier.</summary>
        public const string PrefKey = "anim.runCadence";

        /// <summary>The net run cadence the BAKED tuning already delivers
        /// (blend-child timeScale x3 * HeroBodySwapper 0.5 global = 1.5).</summary>
        public const float BakedNetRunCadence = 1.0f;

        private const float MinCadence = 0.5f;
        private const float MaxCadence = 3.0f;

        private static float? s_cached; // avoid a PlayerPrefs read every frame

        /// <summary>The owner-tunable net run-cadence multiplier (default 1.5 =
        /// baked behavior, zero change). Clamped to a sane band; persisted.</summary>
        public static float RunCadence
        {
            get
            {
                s_cached ??= Mathf.Clamp(
                    PlayerPrefs.GetFloat(PrefKey, BakedNetRunCadence), MinCadence, MaxCadence);
                return s_cached.Value;
            }
            set
            {
                float v = Mathf.Clamp(value, MinCadence, MaxCadence);
                s_cached = v;
                PlayerPrefs.SetFloat(PrefKey, v);
            }
        }

        private Animator _anim;
        private float _baseSpeed = 0.5f; // HeroBodySwapper.HeroAnimSpeed at attach time
        private bool _wasLocomotion;

        /// <summary>
        /// Attach (or re-bind) the enforcer on <paramref name="host"/> for the
        /// given animator. Called by HeroBodySwapper right after it sets the
        /// global anim.speed; idempotent across body swaps.
        /// </summary>
        public static void Attach(GameObject host, Animator anim, float baseSpeed)
        {
            if (host == null || anim == null) return;
            if (!host.TryGetComponent(out HeroLocomotionCadence c))
                c = host.AddComponent<HeroLocomotionCadence>();
            c._anim = anim;
            c._baseSpeed = baseSpeed;
            c._wasLocomotion = false;
        }

        private void LateUpdate()
        {
            if (_anim == null || !_anim.isActiveAndEnabled ||
                _anim.runtimeAnimatorController == null) return;

            // Locomotion = base layer sits in a locomotion state AND is not mid-
            // transition. During a transition into an action state we treat it as
            // NOT locomotion so the base speed is restored before the action plays
            // (and ShapeAttackTempo's hitstop writes are never fought — we only
            // write during locomotion or once on the locomotion->action edge).
            var st = _anim.GetCurrentAnimatorStateInfo(0);
            bool loco = !_anim.IsInTransition(0) &&
                        (st.IsName("Locomotion") || st.IsName("InjuredLocomotion"));

            if (loco)
            {
                float target = _baseSpeed * (RunCadence / BakedNetRunCadence);
                if (!Mathf.Approximately(_anim.speed, target)) _anim.speed = target;
            }
            else if (_wasLocomotion)
            {
                _anim.speed = _baseSpeed; // restore the tuned action pacing once
            }
            _wasLocomotion = loco;
        }
    }
}
