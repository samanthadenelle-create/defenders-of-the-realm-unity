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
