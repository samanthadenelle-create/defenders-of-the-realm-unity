// =============================================================================
// HeroCinemachineRig — Cinemachine-3 over-the-shoulder rig for Blaise.
// -----------------------------------------------------------------------------
// Replaces the hand-rolled VillageCamera with Unity's Cinemachine 3 stack so
// the camera auto-pulls-in when a building / wall obstructs the hero (owner
// 2026-05-20: walls + buildings keep blocking the view).
//
// Layout:
//   • Main Camera             — gets a CinemachineBrain (added at runtime if
//                               missing) so Cinemachine drives it.
//   • This GameObject's child — CinemachineCamera + ThirdPersonFollow +
//                               Deoccluder. Follow + LookAt point at the hero.
//
// Spawned by VillageSceneBuilder.BuildHero alongside HeroLocomotion. The old
// VillageCamera component on Main Camera is disabled so the two rigs don't
// fight.
// =============================================================================

using Unity.Cinemachine;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroCinemachineRig : MonoBehaviour
    {
        [Tooltip("Shoulder offset — X right of hero, Y above pivot, Z forward. " +
                 "Cinemachine 3 ThirdPersonFollow units.")]
        [SerializeField] private Vector3 _shoulderOffset = new Vector3(0.4f, 1.6f, 0f);

        [Tooltip("How far behind the hero the camera sits before deoccluder pulls. " +
                 "Owner direction 2026-05-20: pulled to 7 m so the hero doesn't fill the frame.")]
        [SerializeField] private float _cameraDistance = 7.0f;

        [Tooltip("Position damping (seconds) — higher = smoother + laggier.")]
        [SerializeField] private float _damping = 0.25f;

        [Tooltip("Vertical arm length — pivot height above the shoulder offset.")]
        [SerializeField] private float _verticalArmLength = 0.5f;

        [Tooltip("Enable Cinemachine Deoccluder — auto-pulls camera in when " +
                 "geometry blocks the hero. Currently OFF: the deoccluder was " +
                 "treating the hero's own collider as an obstacle (2026-05-20). " +
                 "Re-enable after we tag/layer the hero so the deoccluder ignores it.")]
        [SerializeField] private bool _enableDeoccluder = false;

        private void Start()
        {
            EnsureBrainOnMainCamera();
            BuildCinecam();
        }

        private static void EnsureBrainOnMainCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<CinemachineBrain>() == null)
                cam.gameObject.AddComponent<CinemachineBrain>();
        }

        private void BuildCinecam()
        {
            // Child GameObject hosts the Cinemachine stack so its transform is
            // independent of the hero (Cinemachine reads its own transform as
            // the cam's home position).
            var cineGo = new GameObject("HeroCinecam");
            cineGo.transform.SetParent(transform, false);

            var vcam = cineGo.AddComponent<CinemachineCamera>();
            vcam.Follow = transform;
            vcam.LookAt = transform;
            vcam.Priority = 100;

            // Body: 3rd-person OTS follow with damped pull behind the hero.
            var body = cineGo.AddComponent<CinemachineThirdPersonFollow>();
            body.ShoulderOffset = _shoulderOffset;
            body.VerticalArmLength = _verticalArmLength;
            body.CameraDistance = _cameraDistance;
            body.Damping = new Vector3(_damping, _damping, _damping);

            if (_enableDeoccluder)
            {
                var deoccluder = cineGo.AddComponent<CinemachineDeoccluder>();
                deoccluder.AvoidObstacles.Enabled = true;
                deoccluder.AvoidObstacles.DistanceLimit = 8f;
                deoccluder.AvoidObstacles.CameraRadius = 0.3f;
                deoccluder.AvoidObstacles.SmoothingTime = 0.15f;
                deoccluder.AvoidObstacles.Damping = 0.4f;
                deoccluder.AvoidObstacles.DampingWhenOccluded = 0.05f;
            }

            // Disable any legacy VillageCamera on Main Camera so the two
            // rigs don't fight. (Cinemachine drives the transform now.)
            var cam = Camera.main;
            if (cam != null)
            {
                var legacy = cam.GetComponent<VillageCamera>();
                if (legacy != null) legacy.enabled = false;
            }
        }
    }
}
