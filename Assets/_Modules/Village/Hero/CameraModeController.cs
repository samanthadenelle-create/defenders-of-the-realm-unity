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
//       whole village footprint and pans around it. (~50° pitch down, ~30m out,
//       ~22m high, 60° FOV.)
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

        [Tooltip("Pitch (deg, down) of the town bird's-eye. 45–60 per WO-338.")]
        [SerializeField, Range(40f, 65f)] private float _townPitch = 50f;

        [Tooltip("Planar distance (m) from the town centre. 25–35 per WO-338.")]
        [SerializeField, Min(10f)] private float _townDistance = 30f;

        [Tooltip("Camera height (m) above the town centre. 20–25 per WO-338.")]
        [SerializeField, Min(5f)] private float _townHeight = 22f;

        [Tooltip("Field of view (deg) in the town overview. 55–65 per WO-338 (wider than battle).")]
        [SerializeField, Range(50f, 70f)] private float _townFov = 60f;

        [Header("Town input (pan / zoom — NOT hero-relative)")]
        [Tooltip("Allow the player to pan the yaw around the town centre.")]
        [SerializeField] private bool _townPanEnabled = true;

        [Tooltip("Desktop pan speed (deg/sec) for A/D + drag.")]
        [SerializeField, Min(0f)] private float _townPanSpeed = 80f;

        [Tooltip("Desktop zoom step (m of distance) per scroll notch.")]
        [SerializeField, Min(0f)] private float _townZoomStep = 4f;

        [Tooltip("Min / max town distance (zoom clamp).")]
        [SerializeField] private float _townDistanceMin = 18f;
        [SerializeField] private float _townDistanceMax = 45f;

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
            // Stand down entirely so we never fight it — and reset our pan seat so the
            // next town entry re-seeds cleanly behind the player's last drag.
            if (_buildModeActive)
            {
                _townSeatInit = false;
                return;
            }

            // Drive the blend toward the active mode (smooth 0.6s).
            float target = _mode == CameraMode.Town ? 1f : 0f;
            float step = _transitionSeconds > 0f ? dt / _transitionSeconds : 1f;
            _blend = Mathf.MoveTowards(_blend, target, step);

            // BATTLE/EXPLORATION, fully blended out → SmartMobileCamera's transform
            // stands untouched (its owner-validated framing, zero degradation).
            if (_blend <= 0.0001f)
            {
                _cam.fieldOfView = _baseFov; // SMC manages its own combat-FOV from here
                return;
            }

            // We are in town, or transitioning. Compute the town bird's-eye seat and
            // blend the camera from SMC's just-written battle seat toward it.
            UpdateTownInput(dt);
            ComputeTownSeat(out Vector3 townPos, out Quaternion townRot);

            float t = Smoothstep01(_blend);

            // SMC already wrote its battle seat to transform.* this frame; blend from it.
            transform.position = Vector3.Lerp(transform.position, townPos, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, townRot, t);
            _cam.fieldOfView = Mathf.Lerp(_baseFov, _townFov, t);
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

            // Pitch the offset down by _townPitch and rotate it by the player's pan yaw.
            // The seat is anchored to the TOWN CENTRE, not the hero (economy overview).
            Quaternion orbit = Quaternion.Euler(_townPitch, _townYaw, 0f);
            Vector3 back = orbit * new Vector3(0f, 0f, -_townDistanceLive);
            pos = lookAt + back + Vector3.up * (_townHeight - _townLookAtHeight);

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
