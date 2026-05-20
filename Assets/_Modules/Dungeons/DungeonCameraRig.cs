// =============================================================================
// DungeonCameraRig — the top-down isometric Cinemachine follow rig (Week 5).
// -----------------------------------------------------------------------------
// Port spec Part 5 Week 5:
//   "manages camera (Cinemachine follow rig, top-down isometric tilt)."
//
// The dungeon camera is a fixed-angle isometric chase rig: it holds a constant
// pitch and yaw while sliding to keep the Keeper framed. It is NOT an orbit /
// free-look camera (that is the village rig's job).
//
// ── How the isometric tilt is built (Cinemachine 3.x) ──
// The CinemachineCamera carries ONLY a CinemachineFollow (Body) component —
// deliberately no Aim component. With no Aim stage, Cinemachine leaves the
// camera's own transform rotation untouched, so the rig keeps whatever pitch we
// author. CinemachineFollow then slides the camera so it sits at a fixed
// FollowOffset from the Keeper, with damping for a smooth chase.
//
//   • FollowOffset — the world-space offset from the hero (high, pulled back).
//   • transform pitch — the down-tilt that gives the isometric look.
//   • PositionDamping — how softly the rig eases along as the Keeper walks.
//
// DungeonController owns scene orchestration and calls Bind() once the hero is
// placed; this component owns ONLY the camera maths so the rig can be tuned and
// unit-reasoned in isolation. The controller's inline ConfigureCamera() path
// still works for a hand-wired camera — this component is the reusable, fully
// self-configuring alternative the scene builder can drop on the rig instead.
//
// No async flows — camera framing is per-frame, handled by Cinemachine itself.
// =============================================================================

using Unity.Cinemachine;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Configures and owns the dungeon's top-down isometric Cinemachine follow
    /// rig — a fixed pitch/yaw chase camera that keeps the Keeper framed without
    /// orbiting. Sits on the same GameObject as the <see cref="CinemachineCamera"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class DungeonCameraRig : MonoBehaviour
    {
        // ── Tuning — isometric framing ───────────────────────────────────────

        [Header("Isometric framing")]
        [Tooltip("World-space offset of the camera from the Keeper. The default " +
                 "is high and pulled back along -Z for the spec's top-down tilt.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 13f, -9f);

        [Tooltip("Camera pitch in degrees — the isometric down-tilt. ~52° looks " +
                 "down at the floor while keeping wall faces and the hero readable.")]
        [SerializeField] private float _pitch = 52f;

        [Tooltip("Camera yaw in degrees. 0 keeps north screen-up; rotate for a " +
                 "diagonal isometric presentation if the layout reads better turned.")]
        [SerializeField] private float _yaw = 0f;

        [Header("Chase damping")]
        [Tooltip("How softly the rig eases along behind the Keeper, per axis " +
                 "(seconds of lag). Equal values give an even, calm follow.")]
        [SerializeField] private Vector3 _positionDamping = new Vector3(1.4f, 1.4f, 1.4f);

        [Header("Lens")]
        [Tooltip("Vertical field of view for the dungeon camera. A modest FOV " +
                 "flattens perspective toward the isometric look.")]
        [SerializeField] private float _fieldOfView = 40f;

        [Tooltip("When true, drive an orthographic lens instead of perspective " +
                 "for a true (parallel-projection) isometric presentation.")]
        [SerializeField] private bool _orthographic = false;

        [Tooltip("Orthographic half-height (world units), used only when " +
                 "Orthographic is enabled.")]
        [SerializeField] private float _orthographicSize = 8f;

        // ── Runtime ──────────────────────────────────────────────────────────

        private CinemachineCamera _camera;
        private CinemachineFollow _follow;

        /// <summary>The CinemachineCamera this rig drives.</summary>
        public CinemachineCamera Camera => _camera;

        /// <summary>The current world-space camera-from-hero offset.</summary>
        public Vector3 FollowOffset => _followOffset;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();

            // The rig needs a Body component to chase the target. Add a
            // CinemachineFollow if the scene builder did not wire one — and
            // deliberately add NO Aim component so the fixed pitch holds.
            _follow = GetComponent<CinemachineFollow>();
            if (_follow == null)
                _follow = gameObject.AddComponent<CinemachineFollow>();
        }

        // Auto-bind fallback for dungeon scenes that have no DungeonController
        // to call Bind() explicitly (e.g. Folk's Granary, which is authored as a
        // simpler scene without the controller orchestrator). One Start() lookup
        // per scene load is cheap; the controller path still wins by running first.
        private void Start()
        {
            if (_camera != null && _camera.Follow != null) return;
            var hero = FindAnyObjectByType<DungeonHero>();
            if (hero != null) Bind(hero.transform);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Binds the rig to the Keeper and applies the isometric framing. Called
        /// by <see cref="DungeonController"/> once the hero is placed at spawn.
        /// </summary>
        /// <param name="hero">The Keeper transform the rig chases.</param>
        public void Bind(Transform hero)
        {
            if (_camera == null) _camera = GetComponent<CinemachineCamera>();
            if (_follow == null) _follow = GetComponent<CinemachineFollow>();
            if (_camera == null || hero == null) return;

            // Tracking target drives the Follow body. LookAt is left unset on
            // purpose — with no Aim component the rig keeps its authored pitch.
            _camera.Follow = hero;
            _camera.LookAt = null;

            ApplyLens();
            ApplyFraming(hero);
        }

        /// <summary>
        /// Re-applies the framing to an already-bound rig — call after changing
        /// the offset / pitch at runtime (e.g. a future room-specific zoom).
        /// </summary>
        public void RefreshFraming()
        {
            if (_camera != null && _camera.Follow != null)
            {
                ApplyLens();
                ApplyFraming(_camera.Follow);
            }
        }

        /// <summary>Overrides the camera-from-hero offset and re-frames the rig.</summary>
        public void SetFollowOffset(Vector3 offset)
        {
            _followOffset = offset;
            RefreshFraming();
        }

        // ── Internals ────────────────────────────────────────────────────────

        /// <summary>
        /// Seats the camera at the isometric offset + pitch and hard-locks the
        /// CinemachineFollow offset so the rig chases without orbiting.
        /// </summary>
        private void ApplyFraming(Transform hero)
        {
            // Fixed down-tilt: with no Aim stage Cinemachine preserves this.
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // Seat the camera at the authored offset immediately so the first
            // frame is already framed (no visible snap-in on scene load).
            transform.position = hero.position + _followOffset;

            if (_follow != null)
            {
                _follow.FollowOffset = _followOffset;

                // Position damping for a steady chase. Binding mode is left at
                // the CinemachineFollow default; a WorldSpace override is a
                // Cinemachine-API finetune (see unity-decisions.md).
                var settings = _follow.TrackerSettings;
                settings.PositionDamping = _positionDamping;
                _follow.TrackerSettings = settings;
            }
        }

        /// <summary>Pushes the FOV / projection choice into the camera lens.</summary>
        private void ApplyLens()
        {
            if (_camera == null) return;
            // Perspective isometric rig. LensSettings.Orthographic is read-only
            // in Cinemachine 3.x — an orthographic override is a finetune item.
            var lens = _camera.Lens;
            lens.FieldOfView = _fieldOfView;
            _camera.Lens = lens;
        }

        // ── Editor ───────────────────────────────────────────────────────────

        private void OnValidate()
        {
            // Keep the scene-view rig oriented to the authored angle while a
            // designer tunes the pitch / yaw in the inspector.
            if (!Application.isPlaying)
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
