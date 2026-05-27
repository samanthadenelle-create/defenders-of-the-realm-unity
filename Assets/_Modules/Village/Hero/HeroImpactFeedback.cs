// =============================================================================
// HeroImpactFeedback — DEF-70 (3): bow-shot recoil trigger + gamepad haptics.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village (the only hero assembly on this branch).
//
// SPEC INTENT: PlayRecoil() fires a short upper-body "BowRecoil" recoil state on
// the Animator; PlayHaptic(intensity, duration) rumbles the active gamepad and
// falls back gracefully when no gamepad is present. Called from the Ranger's
// attack resolution (future HeroCombat — stubbed with a TODO here).
//
// RECONCILIATION TO THIS BRANCH:
//   • The hero controller (Assets/Editor/HeroAnimatorSetup.cs) has ONLY Speed(float)
//     + Cast(trigger) on a single layer (index 0). It has NO "BowRecoil" trigger
//     and NO upper-body layer. SetTrigger on an undefined parameter logs a Unity
//     warning but does NOT throw, so PlayRecoil() is safe today; it becomes a real
//     recoil once the controller adds the parameter + state (documented below).
//   • The Animator is on the swapped-in "HeroBody" child, not this GameObject —
//     resolved the same way HeroLocomotion / HeroAbilities resolve it, with a
//     transform.Find("HeroBody") self-heal because HeroBodySwapper re-parents the
//     body at runtime.
//   • Unity.InputSystem IS referenced by DeNelle.Village.asmdef, so
//     UnityEngine.InputSystem.Gamepad is available for haptics.
//
// CONTROLLER ADDITIONS REQUIRED for a real recoil (document only — author these in
// HeroAnimatorSetup.cs; do NOT hand-edit the controller asset):
//   • Add a Trigger parameter named exactly "BowRecoil".
//   • Add a short (~0.2s) recoil state on the UPPER-BODY layer (index 1, the same
//     masked layer HeroAimIK documents) so it only kicks the bow arm, then transition
//     back to the aim/idle state after exit. The hash below is the binding contract.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Village
{
    /// <summary>
    /// Fires the bow-recoil animation trigger and gamepad rumble on a hero attack.
    /// Both entry points are public so a future HeroCombat can call them from the
    /// Ranger's attack resolution.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroImpactFeedback : MonoBehaviour
    {
        /// <summary>
        /// Animator <c>BowRecoil</c> trigger hash — cached so we never do a per-call
        /// string lookup (acceptance criterion). The controller must define a Trigger
        /// parameter with this exact name (see file header).
        /// </summary>
        private static readonly int BowRecoilHash = Animator.StringToHash("BowRecoil");

        private Animator _animator;
        private Coroutine _hapticRoutine;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// Plays the upper-body bow-recoil animation.
        /// TODO: wire from HeroCombat on the Ranger's attack resolution once it lands.
        /// </summary>
        public void PlayRecoil()
        {
            ResolveAnimatorIfNeeded();
            // SetTrigger on a parameter the controller has not defined yet is a
            // no-op + warning in Unity, not an exception — safe until the trigger
            // is authored (see header). Hash is cached, never a string lookup.
            if (_animator != null) _animator.SetTrigger(BowRecoilHash);
        }

        /// <summary>
        /// Rumbles the active gamepad at <paramref name="intensity"/> (0..1) for
        /// <paramref name="duration"/> seconds, then stops. No-ops gracefully when
        /// no gamepad is connected. Uses unscaled real time so the stop lands even
        /// if the game is paused / time-scaled.
        /// </summary>
        public void PlayHaptic(float intensity, float duration)
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return; // graceful fallback — keyboard/touch players feel nothing

            float clamped = Mathf.Clamp01(intensity);
            gamepad.SetMotorSpeeds(clamped, clamped);

            if (_hapticRoutine != null) StopCoroutine(_hapticRoutine);
            _hapticRoutine = StartCoroutine(StopHapticAfter(duration, gamepad));
        }

        private IEnumerator StopHapticAfter(float duration, Gamepad gamepad)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            // The device may have been disconnected during the wait — wrap the stop
            // so a removed-device access can never throw (matches WaveFeedbackDirector).
            try { if (gamepad != null) gamepad.SetMotorSpeeds(0f, 0f); } catch { }
            _hapticRoutine = null;
        }

        private void OnDisable()
        {
            // Don't leave a motor spinning if this hero is torn down mid-rumble.
            if (_hapticRoutine != null)
            {
                StopCoroutine(_hapticRoutine);
                _hapticRoutine = null;
            }
            try { Gamepad.current?.SetMotorSpeeds(0f, 0f); } catch { }
        }

        /// <summary>
        /// Self-heal the Animator reference: Awake() runs before HeroBodySwapper
        /// swaps in the real FBX body, so the cached Animator can be stale/null.
        /// Only does work while unwired (matches HeroLocomotion / HeroAbilities).
        /// </summary>
        private void ResolveAnimatorIfNeeded()
        {
            if (_animator != null) return;
            var bodyT = transform.Find("HeroBody");
            if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }
    }
}
