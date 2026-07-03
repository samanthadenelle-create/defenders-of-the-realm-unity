// =============================================================================
// SmartMobileCamera (DEF-53) — adaptive third-person follow with auto-framing.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   A drop-in companion/replacement for VillageCamera with three layers of
//   adaptive behaviour tuned for mobile touch play:
//
//   1. MOVEMENT LEAD — the look-at point scoots forward in the hero's movement
//      direction, creating a sense of purpose and giving the player more runway
//      to react to incoming threats.
//
//   2. COMBAT ZOOM — when enemies are in range the camera subtly widens FOV and
//      increases the chase distance so the player sees more of the battle. The
//      zoom smoothly reverts to the idle offset when the area is clear.
//
//   3. AUTO-FRAMING (optional) — with framing enabled the camera interpolates
//      the look-at point toward the centroid of the hero + the nearest visible
//      threat, keeping both on screen. Can be toggled at runtime.
//
// ARCHITECTURE:
//   * Add this component alongside (or instead of) VillageCamera. Call
//     SetTarget(heroTransform) from VillageController / VillageSceneBuilder.
//   * Uses Physics.OverlapSphereNonAlloc for enemy scans — one per
//     _enemyScanInterval seconds, not per-frame.
//   * All camera motion is in LateUpdate (same as VillageCamera) so it
//     composes cleanly with Animator root-motion.
//   * Uses Time.unscaledDeltaTime so the camera doesn't freeze during hit-stop.
//
// TUNING CHEATSHEET (Inspector):
//   _followOffset      — idle world-space offset behind/above the hero
//   _combatZoomOut     — extra distance added to offset.z during combat
//   _combatFovBoost    — extra degrees added to the camera's FOV in combat
//   _leadDistance      — how far ahead of movement to bias the look-at
//   _smoothTime        — SmoothDamp follow smoothness
//   _combatScanRadius  — enemy detection radius for combat zoom / framing
//   _enemyScanInterval — seconds between enemy proximity scans
//   _framingEnabled    — enable auto-framing toward the nearest enemy
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>
    /// Adaptive mobile follow camera with movement-lead, combat zoom, and
    /// auto-framing. Drop-in companion / replacement for <see cref="VillageCamera"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SmartMobileCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The hero root to follow. Set by VillageController.SetTarget.")]
        [SerializeField] private Transform _target;

        [Header("Follow offset (idle)")]
        [Tooltip("World-space offset from the hero in idle/explore state. Owner 2026-06-02: " +
                 "this is a 3D game — committed to a CLOSE cinematic third-person (was 0,18,-22 " +
                 "= flat top-down board-game seat that killed the 3D feel). Awake() auto-migrates " +
                 "the legacy high value baked into Village.unity to this default, so it applies on " +
                 "the next Play with NO rebake and NO scene edit. Tune Y=height, Z=distance live to " +
                 "taste; any value below the legacy threshold is honored.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 2.6f, -4.5f);

        // Close cinematic 3D third-person, tilted DOWN for mobile PORTRAIT (DEF-227).
        // Portrait viewports have a tall vertical FOV, so a near-horizontal seat (the old
        // 0,3.5,-6 = ~9.5 deg of downtilt) filled the top half with sky/rooftops and shoved
        // the hero large + low into a corner. This seat sits higher and a touch further back
        // (~28 deg downtilt over the 2.5m look-at) so the ground frames the hero and the sky
        // band shrinks. Awake() snaps the retired top-down seat to this.
        // Owner 2026-06-08 (desktop/landscape): the 0,6.5,-7.5 portrait seat reads "twice
        // as high"; dropped to a CLOSE over-the-shoulder action seat (low + near) so the
        // hero's combat animations read almost face-to-face. _forceCameraFix snaps the
        // baked scene value to this every Play (no rebake). Tune Y=height / Z=distance live.
        private static readonly Vector3 DefaultFollowOffset = new Vector3(0f, 2.6f, -4.5f);
        private const float LegacyHighOffsetY = 14f;   // old TD seat sat at y=18; >=14 => retire it

        [Tooltip("Look-at height above hero feet (metres).")]
        [SerializeField] private float _lookAtHeight = 2.5f;

        [Header("Movement lead")]
        [Tooltip("How far ahead of the hero's movement direction the look-at point is biased. " +
                 "0 = no lead; 3–5 is a comfortable mobile feel.")]
        [SerializeField, Min(0f)] private float _leadDistance = 3.5f;

        [Tooltip("Seconds for the lead point to catch up when the hero stops.")]
        [SerializeField, Min(0.05f)] private float _leadSmoothTime = 0.3f;

        [Header("Smoothing")]
        [Tooltip("Position SmoothDamp time (seconds). Lower = snappier.")]
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.10f;

        [Header("Combat zoom")]
        [Tooltip("Radius within which enemies trigger the combat-zoom state.")]
        [SerializeField, Min(1f)] private float _combatScanRadius = 12f;

        [Tooltip("Seconds between enemy proximity scans (use 0.2–0.4 for mobile).")]
        [SerializeField, Range(0.05f, 1f)] private float _enemyScanInterval = 0.25f;

        [Tooltip("Extra backward distance added to the follow offset during combat.")]
        [SerializeField, Min(0f)] private float _combatZoomOut = 2.5f;

        [Tooltip("Extra FOV degrees added during combat (0 = no FOV change).")]
        [SerializeField, Range(0f, 15f)] private float _combatFovBoost = 4f;

        [Tooltip("Speed at which combat zoom transitions (higher = snappier).")]
        [SerializeField, Min(0.1f)] private float _combatZoomSpeed = 2.5f;

        [Tooltip("Layer mask for enemy detection sweeps. Set to the Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Header("Auto-framing")]
        [Tooltip("When enabled the look-at point interpolates toward the centroid of " +
                 "hero + nearest enemy so both stay in frame during combat.")]
        [SerializeField] private bool _framingEnabled = true;

        [Tooltip("Fraction of the offset from hero to nearest enemy applied to the " +
                 "look-at centroid. 0.5 = halfway between hero and enemy. Kept low " +
                 "so the hero always stays well inside the frame.")]
        [SerializeField, Range(0f, 0.7f)] private float _framingBias = 0.2f;

        [Tooltip("How quickly the look-at recentres on the hero when no enemies are near.")]
        [SerializeField, Min(0.5f)] private float _framingReturnSpeed = 4f;

        // ── WO-512 slice 2: lock-on framing ──────────────────────────────────────
        // When a lock target is bound (BattleArena.SetLockTarget) AND FeatureFlags.LockOn is
        // on, the auto-framing source is OVERRIDDEN to the LOCKED enemy instead of the
        // auto-nearest scan, framing engages immediately (combat blend forced toward 1), and a
        // SEPARATE, capped bias keeps the Knight well in-frame. It REUSES the exact same
        // _leadPoint SmoothDamp as the existing framing path — never a transform set, never a
        // snap; a switch eases because it only moves the damp goal. When the lock target is null
        // OR the flag is off, this whole block is skipped and the camera path is byte-identical
        // to today (zero free-look regression). Yaw-assist (auto-centering) is deliberately NOT
        // implemented this slice (it is the mobile-nausea hotspot — deferred to slice 4).
        [Tooltip("WO-512: fraction of the offset from hero to the LOCKED enemy applied to the " +
                 "look-at centroid while lock-on framing is active. CAPPED at 0.45 so the Knight " +
                 "always stays well inside the frame (kept distinct from _framingBias).")]
        [SerializeField, Range(0f, 0.45f)] private float _lockFramingBias = 0.32f;

        // The enemy currently locked-on (set by BattleArena.SetLockTarget, cleared on
        // ClearLockTarget / Resolve / disable). Null = no lock -> today's exact framing path.
        private Transform _lockTarget;

        [Header("Orbit-behind (third-person)")]
        [Tooltip("Owner 2026-06-02 (\"will the camera pivot behind me when I walk around the " +
                 "castle?\"): when ON, the follow offset rotates with the hero's TRAVEL direction " +
                 "so the camera swings to your back as you round a building — true third-person. " +
                 "OFF = the legacy fixed compass angle (always behind world -Z).")]
        // 2026-06-02: DEFAULT OFF — the first cut (auto-orbit chasing travel heading) fed
        // back into camera-relative movement and made the hero curve/"always turn left"
        // while walking + firing. Reverted to the fixed offset the owner called "much
        // better"; a proper orbit needs a manual/aim-driven yaw, not movement-driven, and
        // must be decoupled from the locomotion input frame. Left here, off, for that pass.
        [SerializeField] private bool _orbitBehind = false;

        // DEF-202/204 (owner: camera was the "worst feature" — world-locked yaw). The fix
        // ships behind _orbitBehind, but Village.unity BAKES _orbitBehind=0, so a BUILD ships
        // world-locked — the exact reason this kept reopening (a C# default flip is overridden
        // by the baked scene value). Force it ON at runtime, no rebake, mirroring the
        // legacy-offset migration in Awake. Set false in code to A/B the legacy camera.
        [SerializeField] private bool _forceCameraFix = true;

        [Tooltip("Degrees/sec the camera yaw chases the hero's travel heading. Lower = lazier, " +
                 "more cinematic swing; higher = snaps behind faster.")]
        [SerializeField, Min(15f)] private float _orbitYawSpeed = 150f;

        [Tooltip("Min planar speed (m/s) before the orbit yaw updates — below this the camera " +
                 "holds its current angle (so standing still / tiny nudges don't spin the view).")]
        [SerializeField, Min(0.01f)] private float _orbitMoveThreshold = 0.4f;

        // DEF-202/204 player-authoritative orbit (CAMERA_INPUT_OVERHAUL.md §2). The yaw is
        // driven ONLY by player pan input (CameraPanInput → AddYaw) plus an optional damped
        // pull toward the hero's FACING (never velocity). This structurally excludes the old
        // velocity-chasing curl/spiral: {yaw←velocity} is absent, only {move←yaw} remains.
        [Tooltip("Min vertical pitch (deg) the player can tilt the orbit camera to (negative = look up).")]
        [SerializeField] private float _panPitchMin = -10f;

        [Tooltip("Max vertical pitch (deg) the player can tilt the orbit camera to.")]
        [SerializeField] private float _panPitchMax = 35f;

        [Tooltip("DEF-202/204: gentle auto-recenter that swings the camera toward the hero's FACING " +
                 "(never velocity) after an idle delay. OFF by default — the camera holds the player's " +
                 "last yaw until they drag again. Loop gain < 1 (damped + idle-gated + suspended during " +
                 "drag) so it converges, never spirals.")]
        [SerializeField] private bool _facingRecenterEnabled = false;

        [Tooltip("Seconds of no-drag before the facing-recenter resumes (only if enabled). " +
                 "WO-385: kept SHORT so the seat trails the hero's facing in enclosed hubs instead of " +
                 "staying world-locked behind a wall — but non-zero so a quick manual pan isn't instantly " +
                 "yanked back. Suspended entirely while the player is actively dragging.")]
        [SerializeField, Min(0f)] private float _facingRecenterDelay = 0.4f;

        [Tooltip("Max degrees/sec the facing-recenter swings the camera toward the hero's facing. The " +
                 "actual step is PROPORTIONAL to the remaining angle (damped) and capped at this, so big " +
                 "corner turns swing promptly while the swing eases to a stop as it lines up — no overshoot, " +
                 "no spiral (loop gain < 1).")]
        [SerializeField, Min(0f)] private float _facingRecenterSpeed = 220f;

        [Tooltip("WO-385: continuous-recenter stiffness (1/sec). The per-frame swing toward the hero's " +
                 "facing is angleError * this, clamped to _facingRecenterSpeed. Higher = the seat hugs the " +
                 "hero's back more tightly; lower = a lazier cinematic trail. ~3–5 reads as a smooth " +
                 "auto-trailing third-person seat that keeps you facing your open side indoors.")]
        [SerializeField, Min(0.1f)] private float _facingRecenterStiffness = 4f;

        [Header("Wall collision (DEF-151)")]
        [Tooltip("When ON, the camera spherecasts from the hero pivot toward its desired position " +
                 "each frame and pulls IN to just in front of any wall/world geometry in the way, " +
                 "so it never embeds in a wall mesh and loses the hero. OFF = legacy fixed offset " +
                 "(the DEF-151 clipping bug). Leave ON.")]
        [SerializeField] private bool _collisionEnabled = true;

        [Tooltip("World geometry the camera collides against (walls, buildings, towers, ground). " +
                 "EXCLUDES Enemy/Water/Ignore Raycast/UI so mobs and triggers never shove the view. " +
                 "Default = Default + Building + Tower (the layers village walls/structures live on).")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        // Default collision mask, resolved in Awake from the project's named layers so it
        // stays correct even if layer indices shift. Default(0) | Building(6) | Tower(3).
        // Enemy(8), Water(4), Ignore Raycast(2), UI(5), TransparentFX(1) are deliberately OUT.
        [Tooltip("Radius of the occlusion spherecast - the camera keeps at least this much clearance " +
                 "from a wall so the near clip plane never punches through the surface.")]
        [SerializeField, Min(0.05f)] private float _collisionRadius = 0.35f;

        [Tooltip("Extra gap (metres) kept between the wall hit point and the camera, on top of the " +
                 "spherecast radius. Stops the wall's inside face from filling the screen.")]
        [SerializeField, Min(0f)] private float _collisionSkin = 0.2f;

        [Tooltip("Closest the camera is ever allowed to pull in toward the hero pivot (metres). " +
                 "Prevents the camera snapping onto the hero's head in a tight corner.")]
        [SerializeField, Min(0.5f)] private float _minCollisionDistance = 1.2f;

        [Tooltip("How fast the camera pulls IN when a wall appears (higher = snappier, avoids a " +
                 "clip frame). Pull-in is near-instant; pull-out is eased by _collisionReturnSpeed.")]
        [SerializeField, Min(1f)] private float _collisionApproachSpeed = 40f;

        [Tooltip("How fast the camera eases back OUT to the full offset once the wall is clear " +
                 "(lower = smoother, no jitter as you walk along a wall).")]
        [SerializeField, Min(1f)] private float _collisionReturnSpeed = 8f;

        [Tooltip("WO-385: distance (metres) below which the camera still PULLS IN to the occluder " +
                 "as a last-resort safety backstop (so the camera body never embeds point-blank in a " +
                 "mesh). Above this, occluders are FADED (hidden) instead so the camera keeps its " +
                 "proper seat/angle and you simply see the hero through the wall. Keep small.")]
        [SerializeField, Min(0.1f)] private float _occluderPullInDistance = 0.6f;

        private bool _collisionMaskInit;
        // Smoothed 0..1 fraction of the desired distance currently allowed (1 = no wall, full offset).
        private float _distanceFrac = 1f;

        // ── Occluder fade (WO-385) ─────────────────────────────────────────────
        // Instead of pulling the camera IN to a wall (which jammed it to a close "lost" angle at
        // every corner), we keep the camera at its proper seat and FADE the occluding renderer(s)
        // to ShadowsOnly so you see the hero through the wall. Renderers are restored the instant
        // they stop occluding. _faded stores each currently-hidden renderer with its ORIGINAL
        // shadow casting mode so restore is exact; _fadedThisFrame marks the ones still occluding.
        private readonly Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> _faded
            = new Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();
        private readonly HashSet<Renderer> _fadedThisFrame = new HashSet<Renderer>();
        private readonly List<Renderer> _restoreScratch = new List<Renderer>();
        // Reused buffer for SphereCastAll (NonAlloc) so the per-frame path stays allocation-light.
        private readonly RaycastHit[] _occluderHits = new RaycastHit[16];

        // ── Runtime state ──────────────────────────────────────────────────────

        private Camera  _cam;
        private float   _baseFov;
        private Vector3 _posVelocity;
        private Vector3 _leadPoint;
        private Vector3 _leadVelocity;
        private float   _scanTimer;
        private float   _combatBlend;       // 0 = idle, 1 = full combat zoom
        private Vector3 _nearestEnemyPos;
        private bool    _enemyInRange;
        private float   _orbitYaw;          // legacy: smoothed camera yaw (deg) — no longer drives rotation
        private bool    _orbitYawInit;      // seeded from the hero's facing on first frame

        // ── Player-authoritative camera yaw (DEF-202/204, CAMERA_INPUT_OVERHAUL.md §2) ──
        // Written ONLY by AddYaw/AddPitch (pan input) plus an optional damped pull toward
        // hero FACING. NEVER a function of hero velocity/position/MoveIntent. This is the
        // single yaw authority for both the camera seat AND HeroLocomotion's movement basis
        // (read via CameraYaw), so a constant stick yields a straight line — no spiral.
        private float _panYaw;
        private float _panPitch;          // clamped to [_panPitchMin, _panPitchMax]
        private float _timeSinceLastDrag; // drives the idle-gated facing-recenter

        private readonly Collider[] _scanBuffer = new Collider[32];

        // WO-383: the HeroLocomotion we're currently subscribed to for OnTeleported. On a
        // scene-seam warp the hero jumps far in one frame; without this the SmoothDamp follow
        // would chase the jump through the intermediate bad positions. We snap the seat
        // instead. Tracked so we can re-subscribe when the target changes and cleanly detach
        // in OnDisable / OnDestroy.
        private HeroLocomotion _teleportLoco;

        // ── Sole-camera guard (same pattern as VillageCamera) ─────────────────
        private float _soleCheckTimer;

        // ── Screen shake (DEF-67) ──────────────────────────────────────────────
        private Vector3 _shakeOffset;
        private Coroutine _shakeRoutine;

        // ── Singleton (DEF-67: audio controllers + VFX need to reach the camera) ──
        /// <summary>
        /// The active SmartMobileCamera in the scene (null between scenes).
        /// Audio / VFX systems use this to call <see cref="Shake"/>.
        /// </summary>
        public static SmartMobileCamera Instance { get; private set; }

        // ── Player-authoritative camera yaw API (DEF-202/204) ──────────────────
        /// <summary>
        /// The camera's current yaw (deg) used as the basis for camera-relative movement
        /// (HeroLocomotion reads this). Returns 0 when orbit-behind is OFF, so movement
        /// stays world-relative and byte-identical to the legacy shipped build (A/B parity).
        /// This is a pure player-input value — NEVER derived from hero velocity — so holding
        /// a constant stick produces a straight line with no spiral.
        /// </summary>
        public float CameraYaw => _orbitBehind ? _panYaw : 0f;

        /// <summary>Player drag / right-stick yaw delta (deg). The ONLY way external input rotates the view.</summary>
        public void AddYaw(float deg)
        {
            _panYaw += deg;
            _timeSinceLastDrag = 0f;
        }

        /// <summary>Optional pitch from vertical drag; clamped to the safe [_panPitchMin, _panPitchMax] band.</summary>
        public void AddPitch(float deg)
        {
            _panPitch = Mathf.Clamp(_panPitch + deg, _panPitchMin, _panPitchMax);
            _timeSinceLastDrag = 0f;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _cam    = GetComponent<Camera>();
            _baseFov = _cam.fieldOfView;

            // SINGLE-AUTHORITY GUARD: the scene builder attaches BOTH VillageCamera
            // and SmartMobileCamera to the Main Camera. Two follow rigs writing the
            // same transform every LateUpdate fight each other — the hero drifts
            // off-screen and the camera can swing to the ground/wall. SmartMobileCamera
            // is the intended DEF-53 driver (combat zoom + Shake singleton), so we
            // disable the legacy VillageCamera here and own the transform alone.
            var legacy = GetComponent<VillageCamera>();
            if (legacy != null) legacy.enabled = false;

            // One-time migration (owner 2026-06-02): retire the legacy high top-down
            // seat (0,18,-22) baked into Village.unity. Anything this high is the old
            // board-game framing that flattened the 3D feel — snap it to the close 3D
            // third-person default. Applies on Play with no rebake/scene edit; any
            // genuinely-tuned lower offset is left untouched.
            if (_followOffset.y >= LegacyHighOffsetY)
            {
                Debug.Log($"[SmartMobileCamera] retiring legacy top-down offset {_followOffset} " +
                          $"-> {DefaultFollowOffset} (close 3D third-person).");
                _followOffset = DefaultFollowOffset;
            }

            // DEF-202/204: override the baked _orbitBehind=0 / _facingRecenterEnabled=0 so the
            // BUILD gets the camera fix (camera-relative movement + slide-to-pan + lazy
            // swing-behind). No rebake — same one-time-migration pattern as the offset above.
            if (_forceCameraFix)
            {
                _orbitBehind = true;
                _facingRecenterEnabled = true;

                // DEF-227 — Village.unity BAKES the old near-horizontal seat (0,3.5,-6),
                // which reads fine in landscape but in mobile PORTRAIT fills the top with
                // sky/rooftops and pushes the hero large + low into a corner. The legacy
                // migration above only retires the y>=14 top-down seat, so force the new
                // tilted-down framing here (same no-rebake override pattern as orbit/offset)
                // and tame the look-at lead so the hero recentres instead of trailing into
                // a corner. A genuinely-tuned high seat (y>=14) is left to the migration.
                if (_followOffset.y < LegacyHighOffsetY)
                    _followOffset = DefaultFollowOffset;
                if (_leadDistance > 1.5f)
                    _leadDistance = 1.5f;
            }

            ResolveCollisionMask();
        }

        // DEF-151: build the camera-occlusion mask from the project's NAMED layers so it
        // tracks the real layer indices (walls/buildings/towers = world geometry the camera
        // must not enter) and deliberately omits Enemy/Water/UI/triggers (which must never
        // push the camera). If the inspector value was left at the "~0" sentinel we replace
        // it; an explicitly-narrowed mask the owner set is honored.
        private void ResolveCollisionMask()
        {
            if (_collisionMaskInit) return;
            _collisionMaskInit = true;

            // Treat the default "everything" value as "unset" and compute a sane world mask.
            if (_collisionMask.value == ~0)
            {
                int mask = 1 << 0; // Default (walls/ground/most structures live here)
                int building = LayerMask.NameToLayer("Building");
                int tower    = LayerMask.NameToLayer("Tower");
                if (building >= 0) mask |= 1 << building;
                if (tower    >= 0) mask |= 1 << tower;
                _collisionMask = mask;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeTeleport();   // WO-383: detach the hero teleport handler
            RestoreAllFaded();   // WO-385: never leave a faded wall invisible on teardown
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoadedForCamera;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedForCamera;
            UnsubscribeTeleport();   // WO-383: detach the hero teleport handler (re-attaches on next acquire)
            RestoreAllFaded();   // WO-385: restore any faded occluders so nothing is left invisible
            _lockTarget = null;  // WO-512: drop the lock-on framing target so a re-enable starts clean
        }

        private void OnSceneLoadedForCamera(Scene scene, LoadSceneMode mode)
        {
            // Re-enforce sole camera and snap on any scene load (including additive OuterWorld)
            // so the follow isn't lost after additive loads or scene transitions in Village2.
            EnforceSoleCamera();
            if (IsTargetValid())
            {
                ForceFollowImmediate();
            }
            else
            {
                TryFindHero();
                if (IsTargetValid())
                {
                    ForceFollowImmediate();
                }
            }
        }

        private void Start()
        {
            // Fallback: if the scene builder didn't wire a target (or the hero
            // spawned after this camera), find the hero by canonical tag/name/loco so the
            // camera is never left staring at the origin/ground (tree) on load.
            EnsureTargetAndSnap();
            EnforceSoleCamera();
        }

        private void EnsureTargetAndSnap()
        {
            if (!IsTargetValid())
            {
                TryFindHero();
            }
            if (IsTargetValid())
            {
                transform.position = _target.position + _followOffset;
                _leadPoint         = _target.position + Vector3.up * _lookAtHeight;
                AimAt(_leadPoint);
                ForceFollowImmediate();  // ensure snap (idempotent)
            }
        }

        private void TryFindHero()
        {
            var heroGo = GameObject.FindWithTag("Player");
            // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
            if (heroGo == null) heroGo = SafeFindWithTag("HeroTarget");
            // Tag-independent fallback: the baked Village2 hero is NOT tagged Player/
            // HeroTarget, which left this camera with no target ("fixed, doesn't follow
            // the hero"). The hero definitively carries HeroLocomotion, so lock onto that.
            if (heroGo == null)
            {
                var loco = FindAnyObjectByType<HeroLocomotion>();
                if (loco != null) heroGo = loco.gameObject;
            }
            // Additional fallback for baked hero names (e.g. "Hero (Blaise)", "Hero (Knight)" etc.)
            // used by HeroControlEnsurer and scene builder. Helps when tags are missing or
            // hero appears late on web load / editor scene load.
            if (heroGo == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                {
                    if (t != null && t.name.StartsWith("Hero ("))
                    {
                        heroGo = t.gameObject;
                        break;
                    }
                }
            }
            if (heroGo != null)
            {
                _target = heroGo.transform;
                Debug.Log($"[SmartMobileCamera] acquired hero target: {heroGo.name}");
            }
        }

        private bool IsTargetValid() => _target != null && _target.gameObject != null;

        // WO-383: (re)subscribe to the current target's HeroLocomotion.OnTeleported so a
        // scene-seam warp snaps the camera instead of smooth-chasing the jump. Idempotent and
        // null-safe — detaches any previous subscription first, then attaches the new one.
        private void SyncTeleportSubscription()
        {
            HeroLocomotion loco = _target != null ? _target.GetComponent<HeroLocomotion>() : null;
            if (loco == _teleportLoco) return;
            if (_teleportLoco != null) _teleportLoco.OnTeleported -= OnHeroTeleported;
            _teleportLoco = loco;
            if (_teleportLoco != null) _teleportLoco.OnTeleported += OnHeroTeleported;
        }

        // WO-383: detach the teleport subscription (OnDisable / OnDestroy / target change).
        private void UnsubscribeTeleport()
        {
            if (_teleportLoco != null) _teleportLoco.OnTeleported -= OnHeroTeleported;
            _teleportLoco = null;
        }

        // WO-383: the hero just warped (scene seam). Snap the camera to its seat so it never
        // smooth-chases through the intermediate teleport positions. Does NOT touch movement
        // or yaw — purely a follow-position snap (WO-368 world-absolute basis preserved).
        private void OnHeroTeleported()
        {
            if (IsTargetValid()) ForceFollowImmediate();
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private void LateUpdate()
        {
            // Enforce sole camera every frame so no additive scene camera or Cinemachine vcam can steal the view.
            EnforceSoleCamera();

            // Keep searching for the hero each frame until we have a valid target so we never
            // sit framing the origin/tree. ONLY snap (ForceFollowImmediate) on the frame we first
            // acquire the target — NOT every frame. The old per-frame ">0.5m → snap" fought the
            // SmoothDamp follow below (which aims at a DIFFERENT orbit/collision-adjusted seat),
            // making the camera oscillate between the two points nonstop ("screen shakes back and
            // forth"). Ongoing follow is owned solely by the SmoothDamp pass below.
            if (!IsTargetValid())
            {
                RestoreAllFaded();   // WO-385: target lost — never leave a faded wall invisible
                TryFindHero();
                if (!IsTargetValid()) return;
                ForceFollowImmediate();   // one-shot snap off the tree on first acquisition
            }

            float dt = Time.unscaledDeltaTime;

            // ── 1. Enemy scan (throttled) ──────────────────────────────────────
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _enemyScanInterval;
                ScanForEnemies();
            }

            // ── 2. Combat blend ───────────────────────────────────────────────
            float combatTarget = _enemyInRange ? 1f : 0f;
            _combatBlend = Mathf.MoveTowards(_combatBlend, combatTarget, _combatZoomSpeed * dt);

            // ── 3. Follow offset with combat zoom (+ orbit-behind) ────────────
            Vector3 zoomOffset = _followOffset + new Vector3(0f, 0f, -_combatZoomOut * _combatBlend);

            // Orbit-behind (DEF-202/204, CAMERA_INPUT_OVERHAUL.md §2): rotate the offset by
            // the PLAYER-authoritative _panYaw (set via AddYaw from pan input) — NOT the hero's
            // velocity. The old velocity-chasing yaw fed back into camera-relative movement and
            // produced the "always turn left" curl; that {yaw←velocity} edge is now structurally
            // absent. _panYaw is a pure accumulator of player input plus an optional damped pull
            // toward the hero's FACING, so holding a constant stick yields a straight line.
            if (_orbitBehind)
            {
                if (!_orbitYawInit) { _panYaw = _target.eulerAngles.y; _orbitYawInit = true; }

                // Facing-recenter — WO-385: the cure for the "world-locked seat" in enclosed hubs.
                // The seat continuously TRAILS the hero's FACING (never velocity → no curl/spiral)
                // after a short post-drag grace, so walking back into the castle and rounding corners
                // keeps the camera on the hero's open side instead of leaving it pinned behind a wall.
                // The step is PROPORTIONAL to the remaining angle (angleErr * stiffness), capped at
                // _facingRecenterSpeed and never overshooting the target — loop gain < 1, converges,
                // and is fully suspended while the player is actively dragging (AddYaw zeroes the timer).
                _timeSinceLastDrag += dt;
                if (_facingRecenterEnabled && _timeSinceLastDrag > _facingRecenterDelay)
                {
                    float angleErr = Mathf.DeltaAngle(_panYaw, _target.eulerAngles.y);
                    float maxStep  = _facingRecenterSpeed * dt;
                    // Damped step: shrink with the remaining error so the swing eases to a stop.
                    float step = Mathf.Clamp(angleErr * _facingRecenterStiffness * dt, -maxStep, maxStep);
                    // Never step past the target (kills any chance of overshoot/oscillation).
                    if (Mathf.Abs(step) > Mathf.Abs(angleErr)) step = angleErr;
                    _panYaw += step;
                }

                zoomOffset = Quaternion.Euler(_panPitch, _panYaw, 0f) * zoomOffset;
            }

            Vector3 desired = _target.position + zoomOffset;

            // ── 3b. Wall collision / occlusion (DEF-151) ──────────────────────
            // ROOT CAUSE of the bug this fixes: the camera was placed at a fixed
            // offset behind the hero with NO awareness of geometry in between, so a
            // wall sitting between the pivot and the desired seat let the camera slide
            // straight through the mesh — the screen filled with the wall's inside face
            // and the hero shrank to a speck. Fix: spherecast from the hero pivot toward
            // the desired position; if it hits world geometry, pull the seat IN to just
            // in front of the hit so the camera body (+ near clip) never enters the wall.
            desired = ApplyCollision(desired, dt);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _posVelocity, _smoothTime, float.MaxValue, dt);
            // DEF-67: apply shake offset on top of the smoothed position.
            if (_shakeOffset.sqrMagnitude > 0.0001f)
                transform.position += _shakeOffset;

            // ── 4. FOV ────────────────────────────────────────────────────────
            _cam.fieldOfView = Mathf.Lerp(_baseFov, _baseFov + _combatFovBoost, _combatBlend);

            // ── 5. Movement lead ──────────────────────────────────────────────
            // Hero velocity is used ONLY for the look-at lead bias here — it MUST NOT feed
            // the camera yaw (_panYaw), or the old curl/spiral returns. This is the single
            // GetHeroVelocity() call, deliberately below the yaw block (CAMERA_INPUT_OVERHAUL.md §2.3).
            Vector3 heroVelFlat = GetHeroVelocity();
            heroVelFlat.y = 0f;
            Vector3 heroBase = _target.position + Vector3.up * _lookAtHeight;
            Vector3 leadTarget = heroBase;
            if (heroVelFlat.sqrMagnitude > 0.01f)
                leadTarget += heroVelFlat.normalized * _leadDistance;

            // ── 6. Auto-framing ───────────────────────────────────────────────
            // WO-512 slice 2: LOCK-ON framing override. When a lock target is bound AND the flag is
            // on, frame the LOCKED enemy (not the auto-nearest scan): engage framing immediately
            // (force the combat blend toward 1 so we don't wait for the proximity scan) and bias the
            // look-at toward the locked enemy with the SEPARATE capped _lockFramingBias. Reuses the
            // SAME _leadPoint SmoothDamp below — only the damp GOAL changes, so a switch eases and
            // there is never a snap. GUARDED so flag-off / no-lock is byte-identical to today.
            bool lockFraming = _lockTarget != null && DeNelle.Core.FeatureFlags.LockOn;
            if (lockFraming)
            {
                // Force framing to engage now (don't gate on _enemyInRange / the proximity scan).
                _combatBlend = Mathf.MoveTowards(_combatBlend, 1f, _combatZoomSpeed * dt);

                Vector3 lockPos = _lockTarget.position + Vector3.up;   // chest-ish, matches scan anchor
                Vector3 midpoint = Vector3.Lerp(heroBase, lockPos, _lockFramingBias);
                leadTarget = Vector3.Lerp(leadTarget, midpoint, _combatBlend);
            }
            else if (_framingEnabled && _enemyInRange && _combatBlend > 0.1f)
            {
                Vector3 midpoint = Vector3.Lerp(heroBase, _nearestEnemyPos, _framingBias);
                leadTarget = Vector3.Lerp(leadTarget, midpoint, _combatBlend);
            }

            _leadPoint = Vector3.SmoothDamp(_leadPoint, leadTarget, ref _leadVelocity,
                _leadSmoothTime, float.MaxValue, dt);

            AimAt(_leadPoint);

            // ── Sole-camera check ─────────────────────────────────────────────
            _soleCheckTimer -= dt;
            if (_soleCheckTimer <= 0f)
            {
                _soleCheckTimer = 1f;
                EnforceSoleCamera();
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Wires the hero target (called by HeroControlEnsurer and scene bootstraps).</summary>
        public void SetTarget(Transform hero)
        {
            _target = hero;
            SyncTeleportSubscription();   // WO-383: track the new target's OnTeleported
            if (hero != null)
            {
                Debug.Log($"[SmartMobileCamera] SetTarget wired to: {hero.name}");
            }
        }

        /// <summary>Forces an immediate snap to the current target (bypasses smooth for initial acquire on load).
        /// Call after SetTarget from external wirers.</summary>
        public void ForceFollowImmediate()
        {
            if (IsTargetValid())
            {
                SyncTeleportSubscription();   // WO-383: ensure we track this target's OnTeleported
                transform.position = _target.position + _followOffset;
                _leadPoint = _target.position + Vector3.up * _lookAtHeight;
                AimAt(_leadPoint);
                _posVelocity = Vector3.zero;
                EnforceSoleCamera();
                Debug.Log("[SmartMobileCamera] ForceFollowImmediate snap executed");
            }
        }

        /// <summary>Toggles the auto-framing behaviour at runtime.</summary>
        public bool FramingEnabled
        {
            get => _framingEnabled;
            set => _framingEnabled = value;
        }

        // ── WO-512 slice 2: lock-on framing API ─────────────────────────────────
        /// <summary>
        /// Bind the LOCKED enemy the camera should keep framed (called by BattleArena when the
        /// soft lock engages / switches). While bound AND FeatureFlags.LockOn is on, LateUpdate
        /// frames this transform via the existing _leadPoint damp (no snap). Passing a null /
        /// destroyed transform is treated as a clear. A switch is just another SetLockTarget — the
        /// shared damp eases the look-at to the new foe smoothly. No-op when the flag is off.
        /// </summary>
        public void SetLockTarget(Transform t)
        {
            if (!DeNelle.Core.FeatureFlags.LockOn) return;   // flag off -> today's exact camera
            if (t == null) { ClearLockTarget(); return; }
            _lockTarget = t;
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena",
                "LOCKON camera framing target bound '" + t.name.Replace("(Clone)", "").Trim() + "'.");
        }

        /// <summary>Release the lock-on framing (back to today's auto-framing / free-look). The
        /// _leadPoint damp eases back; never a snap. Safe to call any time.</summary>
        public void ClearLockTarget()
        {
            if (_lockTarget == null) return;
            _lockTarget = null;
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena", "LOCKON camera framing target cleared.");
        }

        /// <summary>
        /// Plays a screen-shake impulse. Safe to call from any MonoBehaviour.
        /// Cancels any in-progress shake and starts a fresh one.
        /// <para>DEF-67: called by TowerAudioController on heavy creak/debris events
        /// and by WaveMusicController on boss-wave transitions.</para>
        /// </summary>
        /// <param name="intensity">Peak displacement in world units (0.1 = subtle, 0.5 = heavy).</param>
        /// <param name="duration">Total duration of the shake in seconds.</param>
        public void Shake(float intensity, float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private System.Collections.IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Envelope: full intensity at start, fades to zero by duration.
                float envelope = 1f - (elapsed / duration);
                _shakeOffset   = Random.insideUnitSphere * (intensity * envelope);
                _shakeOffset.z = 0f; // keep depth axis stable; shake only X/Y
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _shakeOffset  = Vector3.zero;
            _shakeRoutine = null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ScanForEnemies()
        {
            if (!IsTargetValid()) { _enemyInRange = false; return; }

            int count = Physics.OverlapSphereNonAlloc(
                _target.position, _combatScanRadius, _scanBuffer, _enemyMask, QueryTriggerInteraction.Collide);

            float closestSqr = float.MaxValue;
            Vector3 closestPos = Vector3.zero;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var col = _scanBuffer[i];
                if (col == null) continue;
                // Only count live hostile IDamageable targets (avoids counting the hero).
                var dmg = col.GetComponentInParent<DeNelle.Core.Combat.IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != DeNelle.Core.Combat.CombatFaction.Hostile) continue;

                float sqr = (col.transform.position - _target.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closestPos = dmg.WorldPosition + Vector3.up;
                    found = true;
                }
            }

            _enemyInRange    = found;
            _nearestEnemyPos = found ? closestPos : _target.position + Vector3.up * _lookAtHeight;
        }

        // WO-385: camera-occlusion pass (replaces the DEF-151 hard pull-in). The old behaviour
        // spherecast pivot→seat and pulled the camera IN to _minCollisionDistance whenever ANY
        // world geometry was between hero and camera — so at EVERY corner the camera jammed to a
        // close "lost" angle and the slow ease-out couldn't recover while the wall persisted.
        // New behaviour: KEEP the camera at its proper seat and FADE the occluding renderer(s) to
        // ShadowsOnly (mesh hidden, shadows kept) so you simply see the hero through the wall.
        // Renderers restore the instant they stop occluding. The only remaining pull-in is a rare
        // safety backstop: if an occluder is point-blank close (< _occluderPullInDistance) we still
        // pull in to it so the camera body never literally embeds in a mesh. Walking past normal
        // corner walls now fades them — it never jams the view.
        private Vector3 ApplyCollision(Vector3 desired, float dt)
        {
            if (!_collisionEnabled || !IsTargetValid())
            {
                _distanceFrac = 1f;
                RestoreAllFaded();   // never leave a wall invisible when collision is off / target lost
                return desired;
            }

            // Pivot = where the camera looks (hero chest/head), well above the feet so the
            // cast doesn't immediately bury itself in the ground collider at the hero's base.
            Vector3 pivot = _target.position + Vector3.up * _lookAtHeight;
            Vector3 toCam = desired - pivot;
            float fullDist = toCam.magnitude;
            if (fullDist <= 0.0001f)
            {
                _distanceFrac = 1f;
                RestoreFadedNotHitThisFrame();
                return desired;
            }

            Vector3 dir = toCam / fullDist;

            _fadedThisFrame.Clear();
            float nearestOccluderDist = float.MaxValue;

            // SphereCastAll so the camera body — not an infinitely-thin ray — clears the wall,
            // and so we catch EVERY occluder between hero and seat (not just the first), fading
            // them all. QueryTriggerInteraction.Ignore so combat-scan / pickup triggers never block.
            int count = Physics.SphereCastNonAlloc(pivot, _collisionRadius, dir, _occluderHits,
                fullDist, _collisionMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = _occluderHits[i].collider;
                if (col == null) continue;
                // Never fade or collide against the hero's own body.
                if (IsTargetCollider(col)) continue;

                float hitDist = _occluderHits[i].distance;
                if (hitDist < nearestOccluderDist) nearestOccluderDist = hitDist;

                // Hide the visible mesh of this occluder (keep its shadows) so the hero shows through.
                FadeOccluder(col);
            }

            // Restore any renderer we faded on a previous frame that is NOT occluding now.
            RestoreFadedNotHitThisFrame();

            // Target fraction of the full distance we're allowed this frame (1 = full seat).
            // Default: hold the full seat (we faded the wall rather than pulling in).
            float targetFrac = 1f;

            // SAFETY BACKSTOP ONLY: if an occluder is point-blank close, still pull in to it so
            // the camera body / near clip never embeds in the mesh. This is the rare last resort
            // — normal corner walls (well beyond _occluderPullInDistance) are faded, not pulled in.
            if (nearestOccluderDist < _occluderPullInDistance)
            {
                float allowed = nearestOccluderDist - _collisionSkin;
                if (allowed < _minCollisionDistance) allowed = _minCollisionDistance;
                if (allowed > fullDist)               allowed = fullDist;
                targetFrac = allowed / fullDist;
            }

            // Pull IN fast (avoid a clip frame), ease OUT slowly (no jitter along a wall).
            float speed = targetFrac < _distanceFrac ? _collisionApproachSpeed : _collisionReturnSpeed;
            _distanceFrac = Mathf.MoveTowards(_distanceFrac, targetFrac, speed * dt);

            return pivot + dir * (fullDist * _distanceFrac);
        }

        // Hide an occluder's visible mesh (set ShadowsOnly) so the hero shows through, keeping its
        // shadows. Stores the ORIGINAL shadow casting mode the first time we touch each renderer so
        // restore is exact. Marks every renderer touched this frame in _fadedThisFrame.
        private void FadeOccluder(Collider col)
        {
            if (col == null) return;

            // Renderer on the collider, or the nearest one up/under its hierarchy (compound colliders).
            var rend = col.GetComponent<Renderer>();
            if (rend == null) rend = col.GetComponentInParent<Renderer>();
            if (rend == null) rend = col.GetComponentInChildren<Renderer>();
            if (rend == null) return;

            if (!_faded.ContainsKey(rend))
                _faded[rend] = rend.shadowCastingMode;

            _fadedThisFrame.Add(rend);
            if (rend.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        // Restore (un-hide) every faded renderer that was NOT an occluder this frame, so walls
        // reappear the instant they stop blocking the view. Null-safe (renderers can be destroyed).
        private void RestoreFadedNotHitThisFrame()
        {
            if (_faded.Count == 0) return;

            _restoreScratch.Clear();
            foreach (var kv in _faded)
            {
                if (kv.Key == null || !_fadedThisFrame.Contains(kv.Key))
                    _restoreScratch.Add(kv.Key);
            }

            for (int i = 0; i < _restoreScratch.Count; i++)
            {
                var rend = _restoreScratch[i];
                if (rend != null)
                    rend.shadowCastingMode = _faded[rend];
                _faded.Remove(rend);
            }
        }

        // Restore ALL faded renderers to their original shadow casting mode and clear the sets, so
        // nothing is ever left invisible (collision disabled, target lost, scene change, teardown).
        private void RestoreAllFaded()
        {
            if (_faded.Count == 0) { _fadedThisFrame.Clear(); return; }

            foreach (var kv in _faded)
            {
                if (kv.Key != null)
                    kv.Key.shadowCastingMode = kv.Value;
            }
            _faded.Clear();
            _fadedThisFrame.Clear();
        }

        // True if the hit collider belongs to the hero we're following (its own body must
        // never count as an occluder, or the camera would jam onto the hero's back).
        private bool IsTargetCollider(Collider col)
        {
            if (col == null || _target == null) return false;
            return col.transform == _target || col.transform.IsChildOf(_target);
        }

        private Vector3 GetHeroVelocity()
        {
            // Try HeroLocomotion first (has velocity), fall back to transform delta.
            var loco = _target.GetComponent<HeroLocomotion>();
            return loco != null ? loco.Velocity : Vector3.zero;
        }

        private void AimAt(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        public void EnforceSoleCamera()
        {
            if (_cam == null) return;
            _cam.depth = 100f;
            if (!_cam.enabled) _cam.enabled = true;

            // The builder (Village2Playable / VillageSceneBuilder) adds BOTH the legacy
            // VillageCamera AND this SmartMobileCamera to the Main Camera, on the documented
            // assumption that "SMC's EnforceSoleCamera disables VillageCamera and takes over".
            // The loop below only disables Camera COMPONENTS on OTHER GameObjects — it can NOT
            // disable a sibling follow-SCRIPT sharing this GameObject's single Camera. So the
            // two follow scripts ran every frame, both writing transform.position/rotation and
            // fighting over the seat (SMC wants height 2.6/dist 4.5; VillageCamera's Awake forces
            // height 5.5/dist 9). Realize the intended sole-camera contract: disable the sibling
            // legacy follow rig so ONLY SmartMobileCamera drives the view.
            var legacy = GetComponent<VillageCamera>();
            if (legacy != null && legacy.enabled)
            {
                legacy.enabled = false;
                Debug.Log("[SmartMobileCamera] disabled sibling VillageCamera (sole-camera contract).");
            }

            foreach (var c in Camera.allCameras)
            {
                if (c == null || c == _cam) continue;
                if (c.targetTexture != null) continue;
                if (!c.enabled) continue;
                c.enabled = false;
                Debug.Log($"[SmartMobileCamera] disabled rogue screen camera '{c.name}'.");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_target == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(_target.position, _combatScanRadius);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_target.position + Vector3.up * _lookAtHeight, _leadPoint);
                Gizmos.DrawWireSphere(_leadPoint, 0.2f);
            }
        }
#endif
    }
}
