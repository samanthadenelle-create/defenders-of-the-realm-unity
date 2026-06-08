// =============================================================================
// CameraModeController (WO-338) — the HUD-aware DYNAMIC camera.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   A context-aware overlay that switches the game camera between TWO modes with
//   a smooth 0.6s blend, sharing the SAME context signals the HUD reads:
//
//   • TOWN / economy  (bird's-eye): trigger = in the village AND no active wave
//       AND not in build mode. A high, wide overview LOCKED to the town centre
//       (the Heart at world origin) — NOT hero-relative — so the player sees the
//       whole village footprint and pans around it. (45° pitch down, ~12m out
//       (WO-367, ~65% closer), height derived from pitch+distance, 60° FOV.)
//   • BATTLE / EXPLORATION (cinematic 3rd-person): trigger = a wave is active OR
//       an ATB battle is live OR the hero is OUT in the open world (beyond the
//       town ring). This mode is the EXISTING, owner-validated SmartMobileCamera
//       (close 3rd-person, hero-relative). We do NOT replace or degrade it —
//       CameraModeController simply STANDS DOWN and lets SmartMobileCamera own the
//       transform exactly as it does today.
//
// ARCHITECTURE — NON-DESTRUCTIVE OVERLAY:
//   SmartMobileCamera (SMC) writes the camera transform every LateUpdate (its
//   validated close 3rd-person seat). CameraModeController runs AFTER SMC (higher
//   DefaultExecutionOrder) and:
//     - BATTLE/EXPLORATION: does nothing — SMC's transform stands (zero degradation).
//     - TOWN: overrides the transform with the bird's-eye town seat.
//     - During a mode change: blends from SMC's just-written seat to the town seat
//       (and back) over _transitionSeconds (0.6s) with a smoothstep ease.
//   Because it post-processes rather than disabling SMC, there is never a frame
//   where two rigs fight, and the handover never jumps: when the blend completes
//   in BATTLE we stop touching the transform and SMC is already where we left it.
//
//   BUILD MODE: BuildModeController already seizes the camera on Enter() (its own
//   top-down overview + pan/zoom) and restores it on Exit(). CameraModeController
//   defers entirely while BuildModeController.IsActive — we treat build mode as a
//   TOWN-class context but let the build controller drive (per the memory
//   "pull up only for base-build"); we never fight its overview.
//
// CONTEXT = the SAME signals the HUD uses (BattleHudVisibilityManager):
//   - wave active   → WaveManager.Phase (Countdown | Active)
//   - ATB battle    → a DeNelle.BattleATB.BattleController exists + enabled
//   - build mode    → BuildModeController.IsActive (+ BuildModeChanged event)
//   - exploration   → hero beyond the town ring (GromOuterWorldReturnJoin's
//                     geometric model: origin = Heart, town ≈ within HomeRadius).
//   We're in DeNelle.Village so WaveManager / BuildModeController / HeroLocomotion
//   are DIRECT references; only the cross-assembly BattleController is reflected
//   (mirrors BattleHudVisibilityManager's HUD→Core asmdef discipline).
//
// TOWN INPUT: pan / zoom around the town centre (NOT hero-relative). Reuses the
//   SmartMobileCamera AddYaw/AddPitch pan input plumbing if a driver is feeding it,
//   plus a desktop WASD/drag/scroll fallback, all clamped to the map footprint.
//
// WEBGL-SAFE: no per-frame allocations; the one cross-assembly lookup is reflected
//   and try/caught; all blends use unscaled time so they run during hit-stop.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Context-aware camera mode switch. Layers a TOWN bird's-eye over the existing
    /// owner-validated <see cref="SmartMobileCamera"/> (kept as the BATTLE/EXPLORATION
    /// mode) and blends between them in 0.6s. See file header for the full design.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)] // run AFTER SmartMobileCamera so we post-process its seat
    public sealed class CameraModeController : MonoBehaviour
    {
        public enum CameraMode { BattleExploration, Town }

        [Header("Town bird's-eye (economy overview)")]
        [Tooltip("World point the town camera orbits/looks at. Default = the Heart of Elarion at the origin.")]
        [SerializeField] private Vector3 _townCentre = Vector3.zero;

        [Tooltip("Look-at height above the town centre (the Heart's mid-height).")]
        [SerializeField] private float _townLookAtHeight = 2.5f;

        [Tooltip("Pitch (deg, down) of the town bird's-eye. 45–60 per WO-338. WO-368: 45° (was 50°).")]
        [SerializeField, Range(40f, 65f)] private float _townPitch = 45f;

        [Tooltip("Straight-line distance (m) to the town look-at. WO-367: 12 (~65% closer than 30) for a more intimate town read. Derives planar+height at _townPitch.")]
        [SerializeField, Min(6f)] private float _townDistance = 12f;

        [Tooltip("Camera height (m) above the town centre. Kept proportional to _townDistance for serialized consistency; the seat DERIVES height from pitch+distance. WO-367: 9 (was 22).")]
        [SerializeField, Min(3f)] private float _townHeight = 9f;

        [Tooltip("Field of view (deg) in the town overview. 55–65 per WO-338 (wider than battle).")]
        [SerializeField, Range(50f, 70f)] private float _townFov = 60f;

        [Header("Town input (pan / zoom — NOT hero-relative)")]
        [Tooltip("Allow the player to pan the yaw around the town centre.")]
        [SerializeField] private bool _townPanEnabled = true;

        [Tooltip("Desktop pan speed (deg/sec) for A/D + drag.")]
        [SerializeField, Min(0f)] private float _townPanSpeed = 80f;

        [Tooltip("Desktop zoom step (m of distance) per scroll notch.")]
        [SerializeField, Min(0f)] private float _townZoomStep = 4f;

        [Tooltip("Min / max town distance (zoom clamp). WO-367: tightened to 8–18 to match the closer base (was 18–45).")]
        [SerializeField] private float _townDistanceMin = 8f;
        [SerializeField] private float _townDistanceMax = 18f;

        [Header("Context — exploration ring (geometric, matches GromOuterWorldReturnJoin)")]
        [Tooltip("Hero within this radius of the town centre counts as 'in town'. Beyond it = exploration.")]
        [SerializeField, Min(5f)] private float _townRadius = 45f;

        [Tooltip("Hysteresis band (m) added to the town radius before flipping to exploration, so the " +
                 "mode doesn't flicker when the hero loiters on the ring.")]
        [SerializeField, Min(0f)] private float _townRadiusHysteresis = 6f;

        [Header("Transition")]
        [Tooltip("Seconds for the smooth blend between the two modes (WO-338 = 0.6s).")]
        [SerializeField, Min(0.05f)] private float _transitionSeconds = 0.6f;

        [Tooltip("Seconds between context re-evaluations (cheap; the build-mode event flips instantly).")]
        [SerializeField, Min(0.05f)] private float _contextPollInterval = 0.25f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private Camera _cam;
        private SmartMobileCamera _battleCam;
        private Transform _hero;
        private float _baseFov;

        private WaveManager _waveManager; // cached; re-resolved if it goes null across scenes

        private CameraMode _mode = CameraMode.BattleExploration;
        // 0 = fully battle/exploration (SMC owns), 1 = fully town bird's-eye.
        private float _blend;
        private float _contextTimer;

        // Town pan state (yaw around the centre; distance = zoom). Hero-INDEPENDENT.
        private float _townYaw;
        private float _townDistanceLive;
        private bool _townSeatInit;
        private bool _dragging;
        private Vector3 _dragLastMouse;

        // Build mode is an explicit "defer to BuildModeController" gate (it owns the cam).
        private bool _buildModeActive;

        // WRITER MODEL: while FULLY in town we DISABLE SmartMobileCamera so only ONE rig
        // writes the transform (no SMC SmoothDamp fighting the locked town seat). During a
        // blend SMC stays enabled so its LIVE seat is the battle endpoint we lerp from/to.
        // Re-enabled the instant we fully blend back to battle/exploration.
        private bool _smcSuspended;

        // Cross-assembly BattleController (ATB) — reflected, matching the HUD's discipline.
        private System.Type _battleControllerType;
        private bool _battleTypeResolved;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _battleCam = GetComponent<SmartMobileCamera>();
            _baseFov = _cam.fieldOfView;
            _townDistanceLive = _townDistance;
        }

        private void OnEnable()
        {
            BuildModeController.BuildModeChanged -= OnBuildModeChanged;
            BuildModeController.BuildModeChanged += OnBuildModeChanged;
            // Seed from the live controller in case build mode is already active.
            _buildModeActive = BuildModeController.Instance != null && BuildModeController.Instance.IsActive;
        }

        private void OnDisable()
        {
            BuildModeController.BuildModeChanged -= OnBuildModeChanged;

            // SAFETY: never leave SMC disabled if we go away — that would freeze the
            // camera. Hand the transform back to its validated rig on the way out.
            //
            // EXCEPTION — build mode: BuildModeController.PullCameraBack disables EVERY
            // "Camera"-named driver on the camera (SMC first, then THIS controller, whose
            // type name also contains "Camera"). It deliberately disables SMC for its
            // overview and re-enables it in RestoreCamera(). So while build mode is active
            // we must NOT re-enable SMC here — that would resurrect the rig BuildMode just
            // suppressed and split/fight the overview. BuildModeController owns SMC's
            // enabled state for the duration; we just drop our suspend bookkeeping.
            if (_buildModeActive)
            {
                _smcSuspended = false; // BuildMode now owns SMC.enabled; don't touch it
                return;
            }
            ResumeSmc();
        }

        private void OnBuildModeChanged(bool active) => _buildModeActive = active;

        private void Start()
        {
            EvaluateContext();
            // Snap to the resolved mode on boot (no blend on the first frame).
            _blend = _mode == CameraMode.Town ? 1f : 0f;
        }

        // Runs AFTER SmartMobileCamera (execution order 100). SMC has already written
        // its validated battle seat into the transform this frame; we post-process.
        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            _contextTimer -= dt;
            if (_contextTimer <= 0f)
            {
                _contextTimer = _contextPollInterval;
                EvaluateContext();
            }

            // BUILD MODE: BuildModeController owns the camera (its own overview + pan).
            // Stand down entirely so we never fight it — hand the transform back to SMC
            // (re-enable it) and reset our pan seat so the next town entry re-seeds cleanly.
            if (_buildModeActive)
            {
                _townSeatInit = false;
                ResumeSmc();
                return;
            }

            // Drive the blend toward the active mode (smooth 0.6s).
            float target = _mode == CameraMode.Town ? 1f : 0f;
            float step = _transitionSeconds > 0f ? dt / _transitionSeconds : 1f;
            _blend = Mathf.MoveTowards(_blend, target, step);

            // BATTLE/EXPLORATION, fully blended out → hand the camera back to
            // SmartMobileCamera entirely (re-enable it). Its owner-validated framing is
            // restored with ZERO degradation; we don't touch the transform at all.
            if (_blend <= 0.0001f)
            {
                ResumeSmc();
                return;
            }

            // We are in town, or transitioning.
            //
            // WRITER MODEL:
            //  • FULLY in town (_blend == 1): the town bird's-eye is the SOLE writer; SMC is
            //    suspended so its SmoothDamp can't fight a hero-relative pull every frame.
            //  • TRANSITIONING (0 < _blend < 1): SMC stays ENABLED so its LIVE validated seat
            //    is the blend's battle endpoint — we read transform.* (SMC just wrote it this
            //    frame) and lerp toward the town seat by the absolute blend t. This makes both
            //    the enter AND exit blends track SMC's real seat (no stale-seat jump), and the
            //    result is never fed back into SMC (we overwrite after it ran).
            bool fullyTown = _blend >= 0.9999f;
            if (fullyTown) SuspendSmc();
            else ResumeSmc(); // keep SMC live during the blend so its seat is current

            UpdateTownInput(dt);
            ComputeTownSeat(out Vector3 townPos, out Quaternion townRot);

            float t = Smoothstep01(_blend);

            // Battle endpoint = SMC's live seat (transform.*, written this frame) while
            // transitioning, or the town seat itself at t==1. Single effective writer:
            // SMC computes, we produce the final composited seat.
            transform.position = Vector3.Lerp(transform.position, townPos, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, townRot, t);
            _cam.fieldOfView = Mathf.Lerp(_baseFov, _townFov, t);
        }

        // ── Single-writer handover with SmartMobileCamera ──────────────────────────

        // Disable SMC's LateUpdate so the town bird's-eye is the sole transform writer.
        private void SuspendSmc()
        {
            if (_smcSuspended || _battleCam == null) return;
            _battleCam.enabled = false;
            _smcSuspended = true;
        }

        // Re-enable SMC so it resumes its validated battle/exploration framing. SMC's
        // SmoothDamp eases from wherever the town seat left the camera, so the handover
        // back is smooth (no jump). Restore the FOV it owns.
        private void ResumeSmc()
        {
            if (!_smcSuspended) { if (_cam != null) _cam.fieldOfView = _baseFov; return; }
            if (_battleCam != null) _battleCam.enabled = true;
            _smcSuspended = false;
        }

        // ── Town seat (LOCKED to the town centre — never hero-relative) ───────────

        private void ComputeTownSeat(out Vector3 pos, out Quaternion rot)
        {
            if (!_townSeatInit)
            {
                _townYaw = 0f;
                _townDistanceLive = Mathf.Clamp(_townDistance, _townDistanceMin, _townDistanceMax);
                _townSeatInit = true;
            }

            Vector3 lookAt = _townCentre + Vector3.up * _townLookAtHeight;

            // Spherical seat around the town centre (NOT hero-relative). _townPitch is the
            // single source of truth for the down-angle; _townDistanceLive is the straight-
            // line range to the look-at. We DERIVE the planar offset + height from those, so
            // the height/angle are internally consistent (the previous seat double-counted
            // _townHeight on top of the already-pitched back vector → camera ~2x too high,
            // staring near-straight-down at nothing — the WO-338 regression).
            //
            //   pitch down by _townPitch:  up = sin(pitch)*dist, planar-back = cos(pitch)*dist
            //   yaw the planar-back by the player's pan yaw around the centre.
            float pitchRad = _townPitch * Mathf.Deg2Rad;
            float up = Mathf.Sin(pitchRad) * _townDistanceLive;
            float planar = Mathf.Cos(pitchRad) * _townDistanceLive;

            Quaternion yaw = Quaternion.Euler(0f, _townYaw, 0f);
            Vector3 planarBack = yaw * new Vector3(0f, 0f, -planar);

            pos = lookAt + planarBack + Vector3.up * up;

            Vector3 dir = lookAt - pos;
            rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : transform.rotation;
        }

        private void UpdateTownInput(float dt)
        {
            if (!_townPanEnabled) return;

            // Yaw pan: A/D (or arrow) keys + horizontal mouse drag. NOT hero-relative.
            float keyPan = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyPan -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyPan += 1f;
            if (keyPan != 0f) _townYaw += keyPan * _townPanSpeed * dt;

            if (Input.GetMouseButtonDown(0)) { _dragging = true; _dragLastMouse = Input.mousePosition; }
            if (Input.GetMouseButtonUp(0)) _dragging = false;
            if (_dragging && Input.GetMouseButton(0))
            {
                Vector3 now = Input.mousePosition;
                float dx = now.x - _dragLastMouse.x;
                _dragLastMouse = now;
                _townYaw += dx * 0.2f; // screen-px → deg
            }

            // Zoom: scroll wheel adjusts the planar distance to the centre.
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
                _townDistanceLive = Mathf.Clamp(
                    _townDistanceLive - scroll * _townZoomStep, _townDistanceMin, _townDistanceMax);

            // Also accept a touch/pan driver feeding SmartMobileCamera's pan API.
            // (No-op if nothing calls AddYaw; keeps a single pan-input contract.)
        }

        // ── Context detection (shares the HUD's signals) ──────────────────────────

        private void EvaluateContext()
        {
            CameraMode resolved = CameraMode.BattleExploration;
            try
            {
                bool battle = IsWaveActive() || IsInBattle() || IsExploring();
                resolved = battle ? CameraMode.BattleExploration : CameraMode.Town;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CameraModeController] EvaluateContext failed: " + e.Message);
                resolved = CameraMode.BattleExploration; // safest fallback = the validated camera
            }
            _mode = resolved;
        }

        private bool IsWaveActive()
        {
            // Cache the live WaveManager; re-resolve if it was destroyed (scene change).
            if (_waveManager == null) _waveManager = FindObjectOfType<WaveManager>();
            if (_waveManager == null) return false;
            var phase = _waveManager.Phase;
            return phase == WavePhase.Countdown || phase == WavePhase.Active;
        }

        private bool IsInBattle()
        {
            if (!_battleTypeResolved)
            {
                _battleTypeResolved = true;
                _battleControllerType = System.Type.GetType(
                    "DeNelle.BattleATB.BattleController, DeNelle.BattleATB");
            }
            if (_battleControllerType == null) return false;
            var bc = FindObjectOfType(_battleControllerType) as Behaviour;
            return bc != null && bc.isActiveAndEnabled;
        }

        // Exploration = the hero is OUT beyond the town ring (geometric, matching
        // GromOuterWorldReturnJoin's origin-distance model). Hysteresis prevents
        // flicker on the boundary: once in town we need to pass radius+hysteresis to
        // flip to exploration, and vice-versa via the bare radius.
        private bool IsExploring()
        {
            if (_hero == null) { _hero = TryFindHero(); if (_hero == null) return false; }

            Vector3 d = _hero.position - _townCentre;
            d.y = 0f;
            float distSqr = d.sqrMagnitude;

            float outer = _townRadius + _townRadiusHysteresis;
            if (_mode == CameraMode.Town)
                return distSqr > outer * outer;       // need to clearly leave the ring
            return distSqr > _townRadius * _townRadius; // already out → stay out until back inside
        }

        private static Transform TryFindHero()
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) go = SafeFindWithTag("HeroTarget");
            return go != null ? go.transform : null;
        }

        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private static float Smoothstep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>The mode the controller currently wants to be in (post-context-eval).</summary>
        public CameraMode Mode => _mode;

        /// <summary>True while the town bird's-eye is fully or partially active.</summary>
        public bool IsTownActive => _blend > 0.0001f;

        /// <summary>Re-point the overview (e.g. if the town centre is not the origin).</summary>
        public void SetTownCentre(Vector3 worldCentre) => _townCentre = worldCentre;
    }
}
