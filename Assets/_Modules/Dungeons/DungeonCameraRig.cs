// =============================================================================
// DungeonCameraRig - the dungeon follow rig (Week 5, re-framed 2026-07-17).
// -----------------------------------------------------------------------------
// OWNER RE-FRAME (2026-07-17): the ORIGINAL rig was a fixed top-down isometric
// chase (pitch ~52 deg, seated ~9u up). In a room with a ~4u ceiling that camera
// floats near/above the roofline and reads as "looking over the top" - the owner
// stood in a room and could not see it. That re-frame made OVER-THE-SHOULDER the
// default: seated just behind + slightly above the Keeper's shoulder at eyeline
// height, looking FORWARD down the corridor.
//
// OWNER RULING 2026-08-07 (WO-920) - **THE 2026-07-26 FPV DEFAULT IS REVERSED.**
// ff.dungeonfpv is now defaultOn:FALSE, so ResolveMode() returns OverShoulder and the
// LOCKED over-the-shoulder rig is the shipped explore camera. FPV survives intact as an
// opt-in A/B (ff.dungeonfpv=1) - nothing about it was deleted.
//
// WHY the reversal: FPV-by-default was a WORKAROUND, not a preference. It was chosen
// 2026-07-26 INSTEAD OF raising the ceiling, because the top-down iso rig floated at the
// roofline and the room could not be seen. WO-919 removed that premise - composed rooms
// are now 4 m walls WITH a ceiling slab and are relit dark - so an under-ceiling OTS seat
// works, and the owner asked for a stationary/calm view rather than a drifting free-look.
// (The free-look drift was real and specific: SampleLookDelta below reads the raw mouse
// delta with NO button held, so on desktop an idle mouse nudge rotates the view.)
//
// ⚠ SCOPE - THE THING THAT IS EASIEST TO GET WRONG ABOUT THIS FILE. Verified at source
// 2026-08-07: this rig exists in exactly TWO scenes, Dungeon_HealersCottage and
// Dungeon_FolksGranary (built by DungeonSceneBuilder / FolksGranaryBuilder, which also
// bake the Main Camera + CinemachineBrain). The COMPOSED dungeons
// (Assets/Scenes/DungeonCompose/dg_*.unity, RoomForge/DungeonBaker) and the hand-coded
// KayKitChallengeOutpost bake NO camera and NO rig at all - grep either .unity for the
// Camera class id (!u!20) and you get zero hits; DungeonBaker L230-237 says so outright.
// In those scenes HeroControlEnsurer L283-295 creates "GameplayCamera (ensured)" and the
// dungeon camera is DeNelle.Village.SmartMobileCamera, whose matching locked seat lives in
// its ApplyDungeonProfileIfNeeded. Editing only this file changes only those two scenes.
// The seat numbers for BOTH rigs come from DeNelle.Core.World.DungeonCameraProfile.
//
// -- Three modes (chosen once at Bind, off FeatureFlags - NOT a serialized field,
//    so the choice applies to the already-baked dungeon scenes with no re-bake) --
//   - OverShoulder (THE DEFAULT, WO-920) - CinemachineThirdPersonFollow behind+above a
//                               heading-corrected pivot; rotates to stay behind
//                               the Keeper as they turn, looks down the corridor.
//                               LOCKED: no look layer, AvoidObstacles OFF, and no
//                               combat reframe (see SetCombatFraming).
//   - FirstPerson (ff.dungeonfpv, OPT-IN since WO-920) - the SAME ThirdPersonFollow
//                               with ~0 distance + eyeline offset, now a FULL FPV:
//                               an independent yaw+pitch LOOK layer drives the pivot's
//                               WORLD rotation each LateUpdate (decoupled from
//                               FaceHeading), the hero body renderers are hidden
//                               (ShadowsOnly), and AvoidObstacles + head-bob stay OFF.
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
// -- FPV look layer (implemented 2026-07-26) --
//   - Independent look: RIGHT-half touch-drag / desktop mouse-delta accumulate into
//     _lookYaw/_lookPitch (pitch clamped ~±70), written to the pivot's WORLD rotation
//     in LateUpdate so look is decoupled from the movement heading. The LEFT of the
//     screen is reserved for DungeonHero's movement joystick (never stolen).
//   - The hero body renderers are set ShadowsOnly on FPV bind (restored on mode
//     change / teardown) so the camera is not inside the mesh but the shadow survives.
//   - SetCombatFraming(bool) forces OverShoulder for arena fights and restores FPV.
//
// -- Occlusion / ceiling --
// OTS seats the camera at DungeonCameraProfile.CameraHeight + VerticalArmLength
// (1.9 + 0.35 = 2.25u) and CameraDistance back - well under the 4u ceiling, no roof clip.
// AvoidObstacles is now OFF BY DEFAULT (WO-920 §3 Phase A.3). It was on, and in a tight
// room it is a bounce generator: a wall behind the Keeper yanks the camera in and it
// slides back out again every time you turn a corner, which is a large part of what the
// owner felt. The trade is that the seat can pass through a wall when the Keeper backs
// flat against one; the shorter seat makes that rare, and the WO rules that no yank beats
// no clip-through. Re-enabling it is one serialized bool - document the reason if you do.
//
// DungeonController owns scene orchestration and calls Bind() once the hero is
// placed; this component owns ONLY the camera maths so the rig can be tuned and
// unit-reasoned in isolation.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;   // WO-920: DungeonCameraProfile — the seat shared with SmartMobileCamera
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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

        [Header("Over-the-shoulder (THE DEFAULT - WO-920)")]
        [Tooltip("Shoulder pivot offset from the Keeper origin, target-local " +
                 "(X = right of the hero for the shoulder, Y = up to the eyeline, " +
                 "Z = forward). Y comes from DungeonCameraProfile.CameraHeight and is " +
                 "re-forced at Bind, so the baked scene value cannot fight it.")]
        [SerializeField] private Vector3 _otsShoulderOffset = new Vector3(0.5f, 1.9f, 0f); // WO-920: was 2.2 (2026-07-26 "taller"), now profile-sourced

        [Tooltip("Extra vertical lift of the camera 'hand' above the shoulder " +
                 "pivot - a small value tips the view slightly down so the floor " +
                 "ahead reads. Sourced from DungeonCameraProfile.VerticalArmLength; " +
                 "shoulder Y + this is the real camera height and must clear the ceiling.")]
        [SerializeField] private float _otsVerticalArmLength = 0.35f; // WO-920: was 0.7 (2026-07-26), back to the WO cap

        [Tooltip("How far BEHIND the Keeper the camera sits (world units). Sourced from " +
                 "DungeonCameraProfile.CameraDistance - close enough to stay inside a " +
                 "10u room (RoomForgeCanon.Cell) instead of floating into the wall behind.")]
        [SerializeField] private float _otsCameraDistance = 3.2f; // WO-920: was 3.8 (2026-07-26), now profile-sourced

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

        [Tooltip("ThirdPersonFollow's built-in obstacle avoidance. WO-920 turns this OFF by " +
                 "default: in a tight room it pulls the camera in on the wall behind the Keeper " +
                 "and slides back out on every corner, which is bounce. OFF = the seat never " +
                 "yanks; the cost is an occasional clip-through when the Keeper backs into a " +
                 "wall. Was true until 2026-08-07.")]
        [SerializeField] private bool _otsAvoidObstacles = false;

        [Tooltip("Layers the over-the-shoulder camera treats as view-blocking " +
                 "geometry (dungeon walls). KayKit walls sit on the Default layer.")]
        [SerializeField] private LayerMask _otsObstacleMask = 1 << 0; // Default

        // -- Tuning - first-person (ff.dungeonfpv, OPT-IN A/B since WO-920) ---

        [Header("First-person (ff.dungeonfpv - OPT-IN, default OFF)")]
        [Tooltip("Eyeline pivot offset for FPV, target-local. Y ~1.62 is head " +
                 "height; a small +Z pushes the camera to the face plane so it is " +
                 "not buried in the chest mesh. The independent yaw+pitch look " +
                 "layer IS implemented (see the FPV block in LateUpdate).")]
        [SerializeField] private Vector3 _fpvShoulderOffset = new Vector3(0f, 1.62f, 0.28f);

        [Tooltip("Camera distance behind the eyeline for FPV - ~0 seats it AT the " +
                 "eyes looking forward. A hair above 0 avoids a degenerate arm.")]
        [SerializeField] private float _fpvCameraDistance = 0.05f;

        [Tooltip("FPV look sensitivity in degrees per input pixel (touch-drag delta on " +
                 "the RIGHT half of the screen / desktop mouse delta). Owner felt-tunes.")]
        [SerializeField] private float _fpvLookSensitivity = 0.14f;

        [Tooltip("FPV pitch clamp (degrees up/down) so the free-look never rolls past " +
                 "vertical - held modest (~70) to fight motion sickness.")]
        [Range(10f, 89f)]
        [SerializeField] private float _fpvPitchClamp = 70f;

        [Tooltip("Fraction of the screen WIDTH from the left that is reserved for the " +
                 "movement joystick and IGNORED by the look-drag (so a left-thumb walk " +
                 "never rotates the camera). 0.5 = right half of the screen looks.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _fpvLookScreenLeftReserve = 0.5f;

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

        // -- FPV independent-look accumulator (decoupled from FaceHeading) -----
        // _fpvActive is true while the first-person look layer drives the pivot each
        // LateUpdate; _lookYaw/_lookPitch accumulate the drag/mouse delta (pitch clamped).
        private bool _fpvActive;
        private float _lookYaw;
        private float _lookPitch;

        // -- Combat framing override ------------------------------------------
        // True while an arena fight has forced over-the-shoulder framing (SetCombatFraming);
        // FPV/iso is restored to the resolved mode when it clears.
        private bool _combatFramingActive;

        // -- Hidden hero body (FPV) -------------------------------------------
        // The hero mesh renderers switched to ShadowsOnly on FPV bind (so the camera is
        // not inside the mesh but the shadow survives), with their prior cast mode saved
        // for a clean restore on mode change / teardown.
        private readonly List<Renderer> _hiddenBodyRenderers = new List<Renderer>();
        private readonly List<ShadowCastingMode> _hiddenBodyPriorModes = new List<ShadowCastingMode>();

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

            // WO-920: force the OTS seat from the SHARED profile at RUNTIME, so the two
            // already-baked dungeon scenes get it with NO re-bake (the SerializeField defaults
            // are updated to match for future bakes). This REPLACES the 2026-07-26 "the dungeon
            // needs a taller camera" force-assign, which hardcoded 2.2 / 0.7 / 3.8 here and put
            // the camera at ~2.9u - authored against the OLD 2.8u walls, i.e. ABOVE them, which
            // is exactly why the owner's screenshots show an elevated view over a short maze.
            // Now 1.9 + 0.35 = 2.25u, comfortably under the WO-919 4u ceiling, sharing its
            // numbers with SmartMobileCamera's dungeon profile so the two pipelines cannot drift.
            _otsShoulderOffset = new Vector3(
                _otsShoulderOffset.x, DungeonCameraProfile.CameraHeight, _otsShoulderOffset.z);
            _otsVerticalArmLength = DungeonCameraProfile.VerticalArmLength;
            _otsCameraDistance = DungeonCameraProfile.CameraDistance;

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

        /// <summary>
        /// Picks the framing. Priority is unchanged (WO-920 §3 Phase D): FPV &gt; Iso &gt; OTS —
        /// FPV still wins if both opt-in flags are set. What changed is that BOTH flags now
        /// default OFF, so the fall-through OverShoulder is the shipped default.
        /// <para>§12: the decision is TRACED with its reason, so a log or a headless capture
        /// answers "which camera am I in, and why" without a playtest. Exactly one
        /// <c>[Flow:DungeonCam] mode=</c> line fires per dungeon across both pipelines — this
        /// one, or SmartMobileCamera.ApplyDungeonProfileIfNeeded's — which also tells you which
        /// pipeline owns the view.</para>
        /// </summary>
        private static CamMode ResolveMode()
        {
            bool fpv = FeatureFlags.DungeonFpv;
            bool iso = FeatureFlags.DungeonCameraIso;

            CamMode mode = fpv ? CamMode.FirstPerson
                         : iso ? CamMode.Iso
                               : CamMode.OverShoulder;

            string why = fpv ? "ff.dungeonfpv=1 opted IN (FPV wins over iso)"
                       : iso ? "ff.dungeoniso=1 opted IN (legacy top-down A/B)"
                             : "both opt-in flags OFF -> WO-920 locked over-the-shoulder default";

            FlowTrace.Step("DungeonCam",
                $"mode={mode} (DungeonCameraRig) why={why} [ff.dungeonfpv={fpv} ff.dungeoniso={iso}]");

            return mode;
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

            // Switching INTO OTS (or re-applying) must undo any FPV body-hide from a
            // prior first-person bind; the FPV branch below re-hides when needed.
            RestoreHeroBody();
            _fpvActive = firstPerson;

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

                // Seed the independent look from the Keeper's VISUAL forward so FPV opens
                // looking down the corridor (transform yaw + the FBX heading offset), then
                // the LateUpdate look layer takes over decoupled from FaceHeading.
                _lookYaw = hero.eulerAngles.y + _headingYawOffset;
                _lookPitch = 0f;
                DriveFpvPivot();   // orient the pivot NOW so the immediate seat matches

                // Hide the hero body (ShadowsOnly) so the camera is not buried in the mesh.
                HideHeroBody(hero);
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

        // -- First-person independent look ------------------------------------

        /// <summary>
        /// Drives the FPV free-look each frame: samples the RIGHT-half touch-drag /
        /// desktop mouse delta, accumulates yaw + (pitch-clamped) pitch, and writes the
        /// pivot's WORLD rotation so the view is DECOUPLED from DungeonHero.FaceHeading
        /// (the Keeper can look one way and walk another). No-op unless FPV is the active
        /// framing. AvoidObstacles + head-bob stay OFF here (motion sickness).
        /// </summary>
        private void LateUpdate()
        {
            if (!_fpvActive || _combatFramingActive || _pivot == null) return;

            Vector2 look = SampleLookDelta();
            _lookYaw += look.x * _fpvLookSensitivity;
            _lookPitch -= look.y * _fpvLookSensitivity;   // drag/mouse up => look up
            _lookPitch = Mathf.Clamp(_lookPitch, -_fpvPitchClamp, _fpvPitchClamp);

            DriveFpvPivot();
        }

        /// <summary>Writes the accumulated look onto the pivot's world rotation (FPV).</summary>
        private void DriveFpvPivot()
        {
            if (_pivot == null) return;
            _pivot.rotation = Quaternion.Euler(_lookPitch, _lookYaw, 0f);
        }

        /// <summary>
        /// This frame's look delta in input pixels. Mobile: the FIRST active touch whose
        /// contact STARTED on the right side of the screen (past
        /// <see cref="_fpvLookScreenLeftReserve"/>) — so the left-thumb movement joystick
        /// is never stolen. Desktop: the mouse delta. Zero when nothing is dragging.
        /// </summary>
        private Vector2 SampleLookDelta()
        {
            var ts = Touchscreen.current;
            if (ts != null)
            {
                float minX = Screen.width * _fpvLookScreenLeftReserve;
                bool anyTouch = false;
                foreach (var t in ts.touches)
                {
                    if (t == null) continue;
                    var phase = t.phase.ReadValue();
                    if (phase != UnityEngine.InputSystem.TouchPhase.Began
                        && phase != UnityEngine.InputSystem.TouchPhase.Moved
                        && phase != UnityEngine.InputSystem.TouchPhase.Stationary)
                        continue;
                    anyTouch = true;
                    // Reserve the left of the screen for the movement joystick.
                    if (t.startPosition.ReadValue().x < minX) continue;
                    return t.delta.ReadValue();
                }
                // On a real touch device with a finger down (but only on the left),
                // do NOT fall through to a synthesized mouse delta.
                if (anyTouch || Application.isMobilePlatform) return Vector2.zero;
            }

            var mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        }

        // -- Combat framing override ------------------------------------------

        /// <summary>
        /// Temporarily forces OVER-THE-SHOULDER framing (for an arena fight) and restores
        /// the resolved mode when cleared. DungeonController calls <c>SetCombatFraming(true)</c>
        /// when a real-time BattleArena encounter stages and <c>SetCombatFraming(false)</c>
        /// when it ends. Null-safe: a no-op until the rig is bound.
        /// <para><b>WO-920 §3 Phase B — POLICY B1 ON THE DEFAULT PATH.</b> When the resolved
        /// traversal mode is already OverShoulder (the shipped default), this is a NO-OP: there
        /// is nothing to swap to, and re-running ApplyThirdPerson would rebuild the pivot, re-seat
        /// the rig and re-run SeatThirdPersonImmediate on every fight start AND end — a visible
        /// pop at both edges for zero framing change. That double pop per encounter is the
        /// "combat thrash" half of the owner's bounce report. So: ONE calm seat for explore and
        /// arena alike.</para>
        /// <para>The swap is PRESERVED, not deleted, for the opted-in modes (effectively B3): with
        /// ff.dungeonfpv=1 or ff.dungeoniso=1 a fight still forces OTS and restores the chosen mode
        /// afterwards, because fighting in first-person or top-down genuinely is unreadable. The
        /// DungeonController wiring is untouched either way.</para>
        /// </summary>
        public void SetCombatFraming(bool on)
        {
            if (_boundHero == null) return;
            if (on == _combatFramingActive) return;

            // B1: the default locked-OTS traversal mode IS the combat framing. Do not re-seat.
            if (_mode == CamMode.OverShoulder)
            {
                FlowTrace.Step("DungeonCam",
                    $"SetCombatFraming({on}): NO-OP — traversal mode is already the locked " +
                    "over-the-shoulder seat (WO-920 policy B1: one calm seat for explore + arena, " +
                    "so there is no stage-in/stage-out pop).");
                return;
            }

            if (on)
            {
                _combatFramingActive = true;
                // Force OTS regardless of the resolved mode (restores the body + clears FPV).
                ApplyThirdPerson(_boundHero, firstPerson: false);
                FlowTrace.Step("DungeonCam", "SetCombatFraming(true): forced over-the-shoulder for the fight.");
            }
            else
            {
                _combatFramingActive = false;
                // Restore the resolved traversal mode.
                if (_mode == CamMode.Iso) ApplyIso(_boundHero);
                else ApplyThirdPerson(_boundHero, firstPerson: _mode == CamMode.FirstPerson);
                FlowTrace.Step("DungeonCam",
                    $"SetCombatFraming(false): restored traversal mode={_mode}.");
            }
        }

        // -- Hero body hide (FPV) ---------------------------------------------

        /// <summary>
        /// Switches the Keeper's mesh renderers to ShadowsOnly on FPV bind so the camera
        /// (seated at the eyeline) is not looking at the inside of the body, while the
        /// floor shadow is kept. Prior cast modes are saved for <see cref="RestoreHeroBody"/>.
        /// </summary>
        private void HideHeroBody(Transform hero)
        {
            if (hero == null) return;
            RestoreHeroBody();   // never double-capture

            // SkinnedMeshRenderer is the KayKit rig; also catch any MeshRenderer body parts.
            var smrs = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in smrs) StashAndHide(r);
            var mrs = hero.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in mrs) StashAndHide(r);

            FlowTrace.Step("DungeonCam",
                $"HideHeroBody (FPV): {_hiddenBodyRenderers.Count} renderer(s) set ShadowsOnly on '{hero.name}'.");
        }

        private void StashAndHide(Renderer r)
        {
            if (r == null) return;
            _hiddenBodyRenderers.Add(r);
            _hiddenBodyPriorModes.Add(r.shadowCastingMode);
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        /// <summary>Restores every hero renderer hidden by <see cref="HideHeroBody"/>.</summary>
        private void RestoreHeroBody()
        {
            for (int i = 0; i < _hiddenBodyRenderers.Count; i++)
            {
                var r = _hiddenBodyRenderers[i];
                if (r == null) continue;
                r.shadowCastingMode = i < _hiddenBodyPriorModes.Count
                    ? _hiddenBodyPriorModes[i]
                    : ShadowCastingMode.On;
            }
            _hiddenBodyRenderers.Clear();
            _hiddenBodyPriorModes.Clear();
        }

        private void OnDestroy()
        {
            // Never leak the FPV body-hide onto the shared hero rig (it survives the scene).
            RestoreHeroBody();
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
