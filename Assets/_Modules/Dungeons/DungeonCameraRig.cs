// =============================================================================
// DungeonCameraRig - the dungeon follow rig (Week 5, re-framed 2026-07-17).
// -----------------------------------------------------------------------------
// OWNER RE-FRAME (2026-07-17): the ORIGINAL rig was a fixed top-down isometric
// chase (pitch ~52 deg, seated ~9u up). In a room with a ~4u ceiling that camera
// floats near/above the roofline and reads as "looking over the top" - the owner
// stood in a room and could not see it. The DEFAULT is now an OVER-THE-SHOULDER
// third-person camera: seated just behind + slightly above the Keeper's shoulder
// at eyeline height, looking FORWARD down the corridor. FIRST-PERSON is an easy
// A/B (ff.dungeonfpv). The legacy top-down iso is one PlayerPref away (ff.dungeoniso).
//
// -- Three modes (chosen once at Bind, off FeatureFlags - NOT a serialized field,
//    so the choice applies to the already-baked dungeon scenes with no re-bake) --
//   - OverShoulder (DEFAULT)  - CinemachineThirdPersonFollow behind+above a
//                               heading-corrected pivot; rotates to stay behind
//                               the Keeper as they turn, looks down the corridor.
//   - FirstPerson (ff.dungeonfpv) - the SAME ThirdPersonFollow with ~0 distance +
//                               eyeline offset (an FPV STUB - camera placement
//                               only; see "What a full FPV still needs" below).
//   - Iso (ff.dungeoniso)     - the legacy CinemachineFollow top-down chase, kept
//                               verbatim for an A/B against the old look.
//
// -- Why a heading-corrected PIVOT (the load-bearing gotcha) --
// DungeonHero.FaceHeading sets transform.rotation = LookRotation(heading) *
// Euler(0,-90,0) - the Tripo FBX carries a 90 deg model-yaw offset, so the hero
// TRANSFORM's forward is 90 deg off the VISUAL forward. ThirdPersonFollow frames
// behind Follow.rotation, so pointing it straight at the hero transform would seat
// the camera at the Keeper's SIDE, not behind. We therefore Follow a child pivot
// parented to the hero with a constant local yaw (_headingYawOffset, default +90)
// that UNDOES the FBX offset - pivot.forward == the Keeper's visual heading. If a
// dungeon ever ships a hero rig with NO such offset, zero _headingYawOffset.
//
// -- What a full FPV still needs (this is a STUB) --
//   - Independent look: mouse-delta / touch-drag yaw+pitch decoupled from the
//     movement heading (today FPV looks wherever the Keeper is walking).
//   - Hide the hero body (or just the head) so the camera is not inside the mesh.
//   - Clamp pitch + optional head-bob. None of that is wired here - this seam only
//     places the camera at the eyeline so the owner can felt-judge FPV vs OTS.
//
// -- Occlusion / ceiling --
// OTS seats the camera ~1.9u up and ~3u back - well under the ~4u ceiling, so no
// roof clip. The one real risk is a wall directly behind the Keeper in a tight
// room; ThirdPersonFollow's built-in AvoidObstacles (physics module) pulls the
// camera in when that happens. It ignores the "Player"-tagged hero so the Keeper's
// own capsule never yanks the camera.
//
// DungeonController owns scene orchestration and calls Bind() once the hero is
// placed; this component owns ONLY the camera maths so the rig can be tuned and
// unit-reasoned in isolation.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using Unity.Cinemachine;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Configures and owns the dungeon's follow rig. DEFAULT is an over-the-shoulder
    /// third-person camera looking forward down the corridor; first-person
    /// (ff.dungeonfpv) and the legacy top-down iso (ff.dungeoniso) are flag A/Bs.
    /// Sits on the same GameObject as the <see cref="CinemachineCamera"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class DungeonCameraRig : MonoBehaviour
    {
        private enum CamMode { OverShoulder, FirstPerson, Iso }

        // -- Tuning - over-the-shoulder (DEFAULT) -----------------------------

        [Header("Over-the-shoulder (default)")]
        [Tooltip("Shoulder pivot offset from the Keeper origin, target-local " +
                 "(X = right of the hero for the shoulder, Y = up to the eyeline, " +
                 "Z = forward). ~1.55u Y keeps the camera at head height - well " +
                 "under the ~4u room ceiling, no roof clip.")]
        [SerializeField] private Vector3 _otsShoulderOffset = new Vector3(0.5f, 1.55f, 0f);

        [Tooltip("Extra vertical lift of the camera 'hand' above the shoulder " +
                 "pivot - a small value tips the view slightly down so the floor " +
                 "ahead reads. Keep modest to stay under the ceiling.")]
        [SerializeField] private float _otsVerticalArmLength = 0.35f;

        [Tooltip("How far BEHIND the Keeper the camera sits (world units). ~3u " +
                 "frames the hero in the lower third and shows the corridor ahead " +
                 "without floating far back into the wall behind.")]
        [SerializeField] private float _otsCameraDistance = 3.0f;

        [Tooltip("Which shoulder the camera favours: 0 = left, 0.5 = centred, " +
                 "1 = right. ~0.6 gives a gentle right-shoulder over-the-shoulder bias.")]
        [Range(0f, 1f)]
        [SerializeField] private float _otsCameraSide = 0.6f;

        [Tooltip("Position damping per camera-local axis (seconds of catch-up). " +
                 "Higher = smoother/laggier. ~0.18 is a calm, non-nauseating follow.")]
        [SerializeField] private Vector3 _otsDamping = new Vector3(0.18f, 0.18f, 0.18f);

        [Tooltip("Local yaw (deg) applied to the follow PIVOT so it points at the " +
                 "Keeper's VISUAL forward, undoing DungeonHero.FaceHeading's -90 deg " +
                 "Tripo FBX model offset. Default +90. Zero this only for a dungeon " +
                 "hero rig that has NO model-yaw offset.")]
        [SerializeField] private float _headingYawOffset = 90f;

        [Tooltip("Enable ThirdPersonFollow's built-in obstacle avoidance so a wall " +
                 "directly behind the Keeper pulls the camera in instead of clipping. " +
                 "Ignores the 'Player'-tagged hero so its own capsule never pulls.")]
        [SerializeField] private bool _otsAvoidObstacles = true;

        [Tooltip("Layers the over-the-shoulder camera treats as view-blocking " +
                 "geometry (dungeon walls). KayKit walls sit on the Default layer.")]
        [SerializeField] private LayerMask _otsObstacleMask = 1 << 0; // Default

        // -- Tuning - first-person (ff.dungeonfpv, STUB) ----------------------

        [Header("First-person (ff.dungeonfpv)")]
        [Tooltip("Eyeline pivot offset for FPV, target-local. Y ~1.62 is head " +
                 "height; a small +Z pushes the camera to the face plane so it is " +
                 "not buried in the chest mesh. STUB: no independent look yet.")]
        [SerializeField] private Vector3 _fpvShoulderOffset = new Vector3(0f, 1.62f, 0.28f);

        [Tooltip("Camera distance behind the eyeline for FPV - ~0 seats it AT the " +
                 "eyes looking forward. A hair above 0 avoids a degenerate arm.")]
        [SerializeField] private float _fpvCameraDistance = 0.05f;

        // -- Tuning - legacy top-down iso (ff.dungeoniso) ---------------------

        [Header("Legacy top-down iso (ff.dungeoniso)")]
        [Tooltip("World-space offset of the iso camera from the Keeper (high, " +
                 "pulled back). Height-capped at bind time by MaxHeightAboveHero.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 9f, -6.25f);

        [Tooltip("Hard cap on how far ABOVE the Keeper the iso camera may sit " +
                 "(world units). The whole offset scales down to preserve the angle.")]
        [SerializeField] private float _maxHeightAboveHero = 9f;

        [Tooltip("Iso camera pitch in degrees - the top-down down-tilt.")]
        [SerializeField] private float _pitch = 52f;

        [Tooltip("Iso camera yaw in degrees. 0 keeps north screen-up.")]
        [SerializeField] private float _yaw = 0f;

        [Tooltip("Iso chase damping per axis (seconds of lag).")]
        [SerializeField] private Vector3 _positionDamping = new Vector3(1.4f, 1.4f, 1.4f);

        // -- Lens -------------------------------------------------------------

        [Header("Lens")]
        [Tooltip("Vertical field of view for the dungeon camera.")]
        [SerializeField] private float _fieldOfView = 40f;

        [Tooltip("Orthographic lens (unused - LensSettings.Orthographic is read-only " +
                 "in Cinemachine 3.x; kept for inspector parity).")]
        [SerializeField] private bool _orthographic = false;

        [Tooltip("Orthographic half-height (unused, see above).")]
        [SerializeField] private float _orthographicSize = 8f;

        // -- Runtime ----------------------------------------------------------

        private CinemachineCamera _camera;
        private CinemachineFollow _follow;
        private CinemachineThirdPersonFollow _tpf;
        private Transform _pivot;      // heading-corrected follow target for OTS/FPV
        private Transform _boundHero;
        private CamMode _mode = CamMode.OverShoulder;

        /// <summary>The CinemachineCamera this rig drives.</summary>
        public CinemachineCamera Camera => _camera;

        /// <summary>The current world-space iso camera-from-hero offset (iso mode only).</summary>
        public Vector3 FollowOffset => _followOffset;

        // -- Lifecycle --------------------------------------------------------

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            // The iso path needs a CinemachineFollow body; the OTS/FPV path swaps
            // it out at Bind. Grab the scene-authored one if present.
            _follow = GetComponent<CinemachineFollow>();
        }

        // Auto-bind fallback for dungeon scenes with no DungeonController to call
        // Bind() explicitly (e.g. Folk's Granary). One Start() lookup is cheap.
        private void Start()
        {
            if (_camera != null && _camera.Follow != null && _boundHero != null) return;
            var hero = FindAnyObjectByType<DungeonHero>();
            if (hero != null) Bind(hero.transform);
        }

        // -- Public API -------------------------------------------------------

        /// <summary>
        /// Binds the rig to the Keeper and applies the framing for the active mode
        /// (over-the-shoulder by default; first-person / iso via FeatureFlags).
        /// Called by <see cref="DungeonController"/> once the hero is at spawn.
        /// </summary>
        public void Bind(Transform hero)
        {
            if (_camera == null) _camera = GetComponent<CinemachineCamera>();
            if (_camera == null || hero == null) return;

            _boundHero = hero;
            _mode = ResolveMode();

            ApplyLens();

            if (_mode == CamMode.Iso)
                ApplyIso(hero);
            else
                ApplyThirdPerson(hero, firstPerson: _mode == CamMode.FirstPerson);
        }

        /// <summary>Re-applies the framing for the active mode to an already-bound rig.</summary>
        public void RefreshFraming()
        {
            if (_boundHero != null) Bind(_boundHero);
        }

        /// <summary>Overrides the legacy iso camera-from-hero offset and re-frames (iso mode only).</summary>
        public void SetFollowOffset(Vector3 offset)
        {
            _followOffset = offset;
            RefreshFraming();
        }

        // -- Mode selection ---------------------------------------------------

        private static CamMode ResolveMode()
        {
            // FPV wins the A/B if both preview flags are set; otherwise the
            // over-the-shoulder default unless the legacy iso is explicitly asked for.
            if (FeatureFlags.DungeonFpv) return CamMode.FirstPerson;
            if (FeatureFlags.DungeonCameraIso) return CamMode.Iso;
            return CamMode.OverShoulder;
        }

        // -- Over-the-shoulder / first-person ---------------------------------

        /// <summary>
        /// Seats a <see cref="CinemachineThirdPersonFollow"/> behind a
        /// heading-corrected pivot so the camera stays behind the Keeper's VISUAL
        /// forward and looks down the corridor. First-person uses the same body with
        /// an eyeline offset and ~0 distance.
        /// </summary>
        private void ApplyThirdPerson(Transform hero, bool firstPerson)
        {
            // The iso body must not fight the OTS body - a vcam runs one Body stage.
            // Disable BEFORE Destroy (deferred to frame end) so the pipeline skips
            // the pending-destroy body this frame instead of posing iso for one frame.
            if (_follow != null)
            {
                _follow.enabled = false;
                Destroy(_follow);
                _follow = null;
            }

            EnsurePivot(hero);

            // Follow the heading-corrected pivot (not the hero directly): the pivot
            // undoes the Tripo -90 FBX yaw so "behind" is the Keeper's visual back.
            _camera.Follow = _pivot;
            _camera.LookAt = null;

            if (_tpf == null) _tpf = GetComponent<CinemachineThirdPersonFollow>();
            if (_tpf == null) _tpf = gameObject.AddComponent<CinemachineThirdPersonFollow>();
            _tpf.enabled = true;

            if (firstPerson)
            {
                _tpf.ShoulderOffset = _fpvShoulderOffset;
                _tpf.VerticalArmLength = 0f;
                _tpf.CameraDistance = Mathf.Max(0.01f, _fpvCameraDistance);
                _tpf.CameraSide = 0.5f;
            }
            else
            {
                _tpf.ShoulderOffset = _otsShoulderOffset;
                _tpf.VerticalArmLength = _otsVerticalArmLength;
                _tpf.CameraDistance = _otsCameraDistance;
                _tpf.CameraSide = _otsCameraSide;
            }
            _tpf.Damping = _otsDamping;

#if CINEMACHINE_PHYSICS
            // Built-in obstacle avoidance: a wall behind the Keeper pulls the camera
            // in rather than clipping. Ignore the hero's own capsule ("Player" is a
            // Unity builtin tag, so CompareTag never throws even if unused here).
            var avoid = _tpf.AvoidObstacles;
            avoid.Enabled = _otsAvoidObstacles && !firstPerson;
            avoid.CollisionFilter = _otsObstacleMask;
            avoid.IgnoreTag = "Player";
            avoid.CameraRadius = 0.2f;
            _tpf.AvoidObstacles = avoid;
#endif

            // Seat the camera immediately so the first frame is already framed.
            SeatThirdPersonImmediate(firstPerson);

            FlowTrace.Step("DungeonCam",
                $"ApplyThirdPerson: mode={(firstPerson ? "FPV" : "OverShoulder")} target='{hero.name}' " +
                $"shoulder={_tpf.ShoulderOffset} arm={_tpf.VerticalArmLength} dist={_tpf.CameraDistance} " +
                $"side={_tpf.CameraSide} headingYaw={_headingYawOffset} heroPos={hero.position} " +
                $"camPos={transform.position}.");
        }

        /// <summary>Ensures the heading-corrected follow pivot exists, parented to the hero.</summary>
        private void EnsurePivot(Transform hero)
        {
            if (_pivot == null)
            {
                var go = new GameObject("DungeonOTSPivot");
                _pivot = go.transform;
            }
            _pivot.SetParent(hero, worldPositionStays: false);
            _pivot.localPosition = Vector3.zero;
            // Constant local yaw undoes the FBX model offset so pivot.forward is the
            // Keeper's visual heading; position tracks the hero automatically.
            _pivot.localRotation = Quaternion.Euler(0f, _headingYawOffset, 0f);
        }

        /// <summary>
        /// Places the rig transform at the ThirdPersonFollow rest pose on bind so
        /// there is no visible snap-in on the first frame (Cinemachine then damps).
        /// </summary>
        private void SeatThirdPersonImmediate(bool firstPerson)
        {
            if (_pivot == null) return;

            Vector3 fwd = _pivot.forward;
            Vector3 right = _pivot.right;
            Vector3 up = Vector3.up;

            Vector3 offset = firstPerson ? _fpvShoulderOffset : _otsShoulderOffset;
            float arm = firstPerson ? 0f : _otsVerticalArmLength;
            float dist = firstPerson ? Mathf.Max(0.01f, _fpvCameraDistance) : _otsCameraDistance;

            Vector3 hand = _pivot.position
                           + right * offset.x
                           + up * (offset.y + arm)
                           + fwd * offset.z;
            Vector3 camPos = hand - fwd * dist;

            transform.position = camPos;
            Vector3 lookDir = (hand + fwd * 4f) - camPos;
            if (lookDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDir, up);
        }

        // -- Legacy top-down iso ----------------------------------------------

        /// <summary>Seats the legacy fixed-angle top-down iso chase (ff.dungeoniso).</summary>
        private void ApplyIso(Transform hero)
        {
            // The iso path needs a CinemachineFollow body and NO ThirdPersonFollow.
            if (_tpf != null) _tpf.enabled = false;
            if (_follow == null) _follow = GetComponent<CinemachineFollow>();
            if (_follow == null) _follow = gameObject.AddComponent<CinemachineFollow>();
            _follow.enabled = true;

            _camera.Follow = hero;
            _camera.LookAt = null;

            // Fixed down-tilt: with no Aim stage Cinemachine preserves this.
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 off = EffectiveIsoOffset();
            transform.position = hero.position + off;

            _follow.FollowOffset = off;
            var settings = _follow.TrackerSettings;
            settings.PositionDamping = _positionDamping;
            _follow.TrackerSettings = settings;

            FlowTrace.Step("DungeonCam",
                $"ApplyIso (legacy): target='{hero.name}' authored={_followOffset} effective={off} " +
                $"(cap={_maxHeightAboveHero}) pitch={_pitch} yaw={_yaw} camPos={transform.position}.");
        }

        /// <summary>The authored iso offset with its height capped, angle preserved.</summary>
        private Vector3 EffectiveIsoOffset()
        {
            Vector3 o = _followOffset;
            if (_maxHeightAboveHero > 0.01f && o.y > _maxHeightAboveHero)
            {
                float scale = _maxHeightAboveHero / o.y;
                o = new Vector3(o.x * scale, _maxHeightAboveHero, o.z * scale);
            }
            return o;
        }

        // -- Lens -------------------------------------------------------------

        /// <summary>Pushes the FOV into the camera lens.</summary>
        private void ApplyLens()
        {
            if (_camera == null) return;
            var lens = _camera.Lens;
            lens.FieldOfView = _fieldOfView;
            _camera.Lens = lens;
        }

        // -- Editor -----------------------------------------------------------

        private void OnValidate()
        {
            // Keep the scene-view iso rig oriented while a designer tunes it. The
            // OTS/FPV modes are driven at runtime by ThirdPersonFollow, so this only
            // matters for the legacy iso preview.
            if (!Application.isPlaying && FeatureFlags.DungeonCameraIso)
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
