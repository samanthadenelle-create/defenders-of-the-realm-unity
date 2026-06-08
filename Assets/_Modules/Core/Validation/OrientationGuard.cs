// =============================================================================
// OrientationGuard — WO-363 runtime orientation gate (opt-in MonoBehaviour).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Validation
//
// Drop this on any moving character (hero, companion, pet, enemy). Each frame —
// WHEN ENABLED — it samples the character's facing (this transform's forward)
// and its movement input, and asserts the WO-363 rule via OrientationValidator:
//   if input != zero and angle(facing, input) > tolerance -> GATE-FAILED.
//
// SHIPPING-SAFE / OPT-IN: the guard does nothing unless BOTH:
//   (1) the serialized `_enabled` bool is ticked, AND
//   (2) the ORIENTATION_GATE scripting define is set (Editor always allowed).
// So a player build with the define absent pays zero cost and never spams. A
// failure LOGS Debug.LogError (a clear "GATE-FAILED" line) + sets HasFailed — it
// NEVER throws / hard-crashes a player build.
//
// INPUT SOURCE: by default the guard derives the input direction from the frame's
// world-space displacement (position delta on XZ), which needs no coupling to any
// specific locomotion script — so it works for hero, companions, pets and enemies
// uniformly. A locomotion script that already knows its input may instead push it
// in via SetInput() (overrides the displacement estimate for that frame).
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Validation
{
    /// <summary>
    /// Opt-in per-character runtime guard for the WO-363 facing-matches-input rule.
    /// Generic across hero/companions/pets/enemies. Logs a clear GATE-FAILED error
    /// (never throws) when, while moving, the character's facing diverges from its
    /// movement direction beyond <see cref="OrientationValidator.DefaultToleranceDegrees"/>.
    /// Inert unless <see cref="_enabled"/> is set AND (in a player build) the
    /// <c>ORIENTATION_GATE</c> scripting define is present.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrientationGuard : MonoBehaviour
    {
        [Tooltip("Master opt-in. When false the guard is completely inert (no sampling, " +
                 "no logging). Keep OFF in shipping content; tick ON for QA/validation runs.")]
        [SerializeField] private bool _enabled = false;

        [Tooltip("Allowed angle (deg) between facing and movement input before GATE-FAILED. " +
                 "~30° absorbs in-flight smooth rotation (slerp toward the heading).")]
        [SerializeField] private float _toleranceDegrees = OrientationValidator.DefaultToleranceDegrees;

        [Tooltip("Minimum world-space XZ speed (m/s) before the guard considers the character " +
                 "to be moving. Below this it's treated as idle (facing not asserted).")]
        [SerializeField] private float _minSpeed = 0.25f;

        [Tooltip("Optional label for log lines (defaults to the GameObject name).")]
        [SerializeField] private string _label = "";

        /// <summary>True once this guard has logged at least one GATE-FAILED this session.</summary>
        public bool HasFailed { get; private set; }

        /// <summary>Number of frames this guard flagged a facing/input mismatch.</summary>
        public int FailureCount { get; private set; }

        private Vector3 _lastPos;
        private bool _hasExternalInput;
        private Vector3 _externalInput;

        /// <summary>
        /// Whether the guard is live this run: opt-in bool AND (editor OR the
        /// ORIENTATION_GATE define). A player build without the define is always inert.
        /// </summary>
        public bool IsActive
        {
            get
            {
#if UNITY_EDITOR || ORIENTATION_GATE
                return _enabled;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Push the authoritative movement input for THIS frame (e.g. from a locomotion
        /// script). Overrides the displacement-based estimate until the next LateUpdate
        /// consumes it. Vector is world-space; Y is ignored.
        /// </summary>
        public void SetInput(Vector3 worldInput)
        {
            _externalInput = worldInput;
            _hasExternalInput = true;
        }

        private void OnEnable()
        {
            _lastPos = transform.position;
        }

        private void LateUpdate()
        {
            // Cheapest possible early-out so a shipping build (define absent) and any
            // un-ticked guard cost effectively nothing.
            if (!IsActive)
            {
                _lastPos = transform.position;
                _hasExternalInput = false;
                return;
            }

            // Determine the movement input direction for this frame.
            Vector3 input;
            if (_hasExternalInput)
            {
                input = _externalInput;
            }
            else
            {
                Vector3 delta = transform.position - _lastPos;
                delta.y = 0f;
                float dt = Mathf.Max(Time.deltaTime, 1e-5f);
                // Only count as "moving input" if above the min-speed threshold.
                input = (delta.magnitude / dt) >= _minSpeed ? delta : Vector3.zero;
            }

            _lastPos = transform.position;
            _hasExternalInput = false;

            if (!OrientationValidator.HasInput(input)) return; // idle — preserve facing, no assert

            if (!OrientationValidator.TryValidate(transform.forward, input, out string reason, _toleranceDegrees))
            {
                HasFailed = true;
                FailureCount++;
                string who = string.IsNullOrEmpty(_label) ? name : _label;
                // Clear, grep-able verdict; LogError (not an exception) so a player build
                // surfaces it without crashing.
                Debug.LogError($"GATE-FAILED [orientation] {who}: {reason}", this);
            }
        }

        /// <summary>Reset the failure latch/counters (e.g. between validation scenes).</summary>
        public void ResetState()
        {
            HasFailed = false;
            FailureCount = 0;
            _lastPos = transform.position;
            _hasExternalInput = false;
        }
    }
}
