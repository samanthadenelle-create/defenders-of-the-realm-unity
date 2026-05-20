// =============================================================================
// VillageCamera — over-the-shoulder follow rig for Hero (Blaise).
// -----------------------------------------------------------------------------
// Tracks the hero's transform with a smooth-damped position lag and a
// LookAt aimed slightly above the hero's center, so the hero's back/shoulders
// frame the lower third of the screen. The follow offset is in the hero's
// LOCAL space, so the camera stays "behind" the hero even as the hero rotates.
//
// Wiring: VillageSceneBuilder.CreateCamera + BuildHero combine to attach this
// component onto the Main Camera and assign _target to the hero transform.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Third-person follow camera. LateUpdate-driven smooth-damp follow with a
    /// hero-local offset so the camera trails the hero through turns.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class VillageCamera : MonoBehaviour
    {
        [Tooltip("The hero transform to follow. Wired by VillageSceneBuilder.")]
        [SerializeField] private Transform _target;

        [Tooltip("Position offset in the hero's local space. " +
                 "Default: right shoulder, head-height, ~1.5 m back so adjacent " +
                 "buildings don't fill the frame (owner: 2026-05-19).")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0.5f, 2.0f, -1.5f);

        [Tooltip("Local pitch of the camera (degrees). Slightly down-tilted so " +
                 "the hero's back fills the lower-third and the world reads ahead.")]
        [SerializeField] private float _localPitchDegrees = 8f;

        [Tooltip("Smoothing time for the position chase (seconds). 0 = instant.")]
        [SerializeField] private float _positionSmoothTime = 0.08f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (_target == null) return;

            // Place the camera in the hero's local frame so it trails through
            // turns instead of staying anchored to world axes.
            Vector3 desired = _target.TransformPoint(_followOffset);
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _positionSmoothTime);

            // Track the hero's yaw but apply the configured local pitch — so the
            // camera always sees ahead-and-up regardless of hero direction.
            float heroYaw = _target.eulerAngles.y;
            transform.rotation = Quaternion.Euler(_localPitchDegrees, heroYaw, 0f);
        }

        /// <summary>Editor-side hook so VillageSceneBuilder can wire the target.</summary>
        public void SetTarget(Transform hero)
        {
            _target = hero;
        }
    }
}
