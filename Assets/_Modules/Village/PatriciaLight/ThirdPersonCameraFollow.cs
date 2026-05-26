// =============================================================================
// ThirdPersonCameraFollow — over-the-shoulder follow camera for Patricia Light.
// -----------------------------------------------------------------------------
// WO-47 Phase 2 ("Defend the Tower"). A dead-simple smoothed follow camera that
// rides behind + above the hero on the tower balcony and looks slightly above
// the hero so the streaming enemies fill the lower screen. It is the dedicated-
// scene analog of the in-village VillageCamera, but with no Cinemachine / input
// dependency — the hero barely moves here (it fires from the balcony), so a
// fixed-offset SmoothDamp is all the framing this mode needs.
//
// HARD CONSTRAINT (WO-47): lives in DeNelle.Village (no new asmdef). The scene
// builder adds it to the Main Camera by reflection (the editor asmdef cannot
// reference DeNelle.Village); PatriciaLightController hands it the hero
// transform at runtime once the hero rig is spawned.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A smoothed third-person follow camera. Set <see cref="Target"/> at runtime
    /// (PatriciaLightController does this after spawning the hero); the camera
    /// eases to <c>target.position + offset</c> rotated into the target's facing
    /// and looks at the target's head. No-op until a target is assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCameraFollow : MonoBehaviour
    {
        [Tooltip("Local-space offset from the followed target (behind + above).")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 8f, -12f);

        [Tooltip("Height above the target the camera aims at (so enemies fill the lower screen).")]
        [SerializeField] private float _lookAtHeight = 2.5f;

        [Tooltip("Position smoothing time (seconds) — bigger = lazier follow.")]
        [SerializeField] private float _positionSmoothTime = 0.18f;

        [Tooltip("When true the offset is rotated into the target's facing; off = world-space offset.")]
        [SerializeField] private bool _followFacing = false;

        private Transform _target;
        private Vector3 _velocity;

        // ── Camera shake (decaying random positional offset) ──────────────────
        private float _shakeAmplitude;   // current peak offset (metres)
        private float _shakeTimeLeft;    // seconds remaining
        private float _shakeDuration;    // full duration for the linear decay

        /// <summary>The transform the camera follows. Assigned at runtime.</summary>
        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                if (_target != null) SnapToTarget();
            }
        }

        /// <summary>Sets the follow offset (behind + above the target).</summary>
        public void SetOffset(Vector3 offset) => _offset = offset;

        /// <summary>
        /// Kicks a decaying camera shake — a random positional jitter layered on
        /// top of the follow pose in <see cref="LateUpdate"/> that fades to zero
        /// over <paramref name="duration"/>. Re-issuing keeps the stronger shake
        /// (so a tower hit during a boss flourish doesn't cut the big shake short).
        /// </summary>
        /// <param name="intensity">Peak offset in metres (e.g. 0.25 small, 0.7 big).</param>
        /// <param name="duration">Seconds the shake takes to decay to zero.</param>
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            // Keep whichever shake is stronger / longer so overlaps don't truncate.
            if (intensity >= _shakeAmplitude)
            {
                _shakeAmplitude = intensity;
                _shakeDuration = duration;
                _shakeTimeLeft = duration;
            }
            else
            {
                _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration);
            }
        }

        /// <summary>Immediately places the camera at its resting pose behind the target.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            transform.position = DesiredPosition();
            AimAtTarget();
            _velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = Vector3.SmoothDamp(
                transform.position, DesiredPosition(), ref _velocity, _positionSmoothTime);
            AimAtTarget();
            ApplyShake();
        }

        /// <summary>Layers the decaying random shake offset on top of the framed pose.</summary>
        private void ApplyShake()
        {
            if (_shakeTimeLeft <= 0f) return;
            _shakeTimeLeft -= Time.deltaTime;
            if (_shakeTimeLeft <= 0f) { _shakeAmplitude = 0f; return; }

            float k = _shakeDuration > 0f ? _shakeTimeLeft / _shakeDuration : 0f; // 1 → 0 linear decay
            float mag = _shakeAmplitude * k;
            Vector3 jitter = new Vector3(
                (Random.value * 2f - 1f),
                (Random.value * 2f - 1f),
                (Random.value * 2f - 1f)) * mag;
            transform.position += jitter;
        }

        private Vector3 DesiredPosition()
        {
            Vector3 worldOffset = _followFacing ? _target.TransformVector(_offset) : _offset;
            return _target.position + worldOffset;
        }

        private void AimAtTarget()
        {
            Vector3 lookAt = _target.position + Vector3.up * _lookAtHeight;
            Vector3 dir = lookAt - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
