// =============================================================================
// CameraPanInput (DEF-202/204) — slide-to-pan touch + right-mouse orbit driver.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The PLAYER's hand on the third-person camera yaw. Feeds drag deltas into the
// SINGLE yaw authority — SmartMobileCamera.AddYaw/AddPitch (_panYaw) — which the
// follow seat AND HeroLocomotion's movement basis both read (CameraYaw). Because
// _panYaw is pure player input (never hero velocity), holding the stick yields a
// straight line; this driver simply lets the player choose the heading.
//
// DESIGN (CAMERA_INPUT_OVERHAUL.md §4):
//   • Lean Touch finger handler (asmdef already refs LeanTouch — no asmdef change).
//   • Start-zone exclusion: reject fingers over GUI, in build mode, or that STARTED
//     inside the joystick zone (VirtualJoystick.IsInZone). Everything else outside
//     those zones pans — "slide-to-pan everywhere outside the stick".
//   • Tap-vs-drag: a finger does nothing until it has moved past _dragThresholdPx
//     (~12px), so a stationary tap still reaches tap-to-attack consumers. Once it
//     crosses the threshold it is "claimed" and feeds deltas to the camera.
//   • Drag-overrides-follow-then-resumes: AddYaw resets the camera's idle timer, so
//     the (optional) facing-recenter is suspended during a drag and resumes after.
//   • Desktop parity: right-mouse drag → AddYaw/AddPitch (left-click stays attack).
//   • Self-bootstrapping DDOL; ABORTS in the PatriciaLight/DTT scene (LeanTouchAimDriver
//     owns Lean there) and only spawns when a HeroLocomotion is present — no scene edit.
//
// Flag-guarded: _panEnabled (default true, but inert unless SmartMobileCamera._orbitBehind).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Lean.Touch;

namespace DeNelle.Village
{
    /// <summary>
    /// Slide-to-pan (Lean Touch) + right-mouse orbit driver for the Village/OuterWorld
    /// third-person camera. Writes the single yaw authority via
    /// <see cref="SmartMobileCamera.AddYaw"/> / <see cref="SmartMobileCamera.AddPitch"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraPanInput : MonoBehaviour
    {
        [Header("Pan enable")]
        [Tooltip("Master switch for slide-to-pan + right-mouse orbit. Default ON, but inert " +
                 "unless SmartMobileCamera._orbitBehind is also ON (CameraYaw returns 0 otherwise).")]
        [SerializeField] private bool _panEnabled = true;

        [Header("Sensitivity")]
        [Tooltip("Yaw degrees applied per horizontal drag pixel.")]
        [SerializeField] private float _dragSensX = 0.15f;

        [Tooltip("Pitch degrees applied per vertical drag pixel (clamped in the camera API).")]
        [SerializeField] private float _dragSensY = 0.10f;

        [Tooltip("Degrees/second the camera orbits from KEYBOARD (Q/E yaw, R/F pitch). Keyboard is " +
                 "the WebGL-reliable camera control — right-mouse drag is eaten by the browser " +
                 "context menu in a web build, so on web this is how the player moves the camera.")]
        [SerializeField] private float _keyOrbitSpeed = 90f;

        [Tooltip("Pixels a finger must move before it is treated as a drag (vs a tap). " +
                 "Below this, the tap passes through to tap-to-attack.")]
        [SerializeField] private float _dragThresholdPx = 12f;

        // Per-finger accumulated travel since press (for the tap-vs-drag threshold).
        private readonly Dictionary<int, float> _fingerTravel = new Dictionary<int, float>();
        // Fingers that have crossed the threshold and are now panning (claimed).
        private readonly HashSet<int> _panning = new HashSet<int>();

        // Scene this driver must NOT run in (Lean is owned by LeanTouchAimDriver there).
        private const string DttSceneName = "PatriciaLightMode";

        public static CameraPanInput Instance { get; private set; }

        // Hero-presence gate: this driver stays inert until a HeroLocomotion exists.
        // The hero may spawn a frame or two AFTER scene-load (same race VirtualJoystick
        // and SmartMobileCamera defend against), so we re-check on sceneLoaded + a
        // throttled timer rather than gating at Bootstrap with no retry.
        private bool _heroPresent;
        private float _heroCheckTimer;   // throttles the hero-presence re-check

        // ── Self-bootstrap (mirror VirtualJoystick; no scene-builder edit needed) ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            // Spawn in any gameplay scene that is not the DTT scene. We deliberately do
            // NOT gate on a hero existing yet — the hero can spawn a frame later, and
            // Bootstrap fires once with no retry. The spawned singleton lazily re-checks
            // for the hero (RefreshHeroPresence) so a late spawn still enables panning.
            if (SceneManager.GetActiveScene().name == DttSceneName) return;
            new GameObject("CameraPanInput").AddComponent<CameraPanInput>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Lean needs a LeanTouch instance in the scene to dispatch gestures.
            if (LeanTouch.Instance == null) gameObject.AddComponent<LeanTouch>();

            // Re-evaluate hero presence on every scene load (covers Village/OuterWorld
            // swaps), then seed the initial check (hero may not exist this frame yet).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshHeroPresence();
        }

        private void OnEnable()
        {
            LeanTouch.OnFingerUpdate += HandleFinger;
            LeanTouch.OnFingerUp     += HandleFingerUp;
        }

        private void OnDisable()
        {
            LeanTouch.OnFingerUpdate -= HandleFinger;
            LeanTouch.OnFingerUp     -= HandleFingerUp;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m) => RefreshHeroPresence();

        // Single source of truth for the hero gate. Cheap FindFirstObjectByType, called
        // only on sceneLoaded + a 0.5s throttle (never per-frame) — mirrors
        // VirtualJoystick.ApplyVisibility's pattern.
        private void RefreshHeroPresence()
        {
            _heroPresent = Object.FindFirstObjectByType<HeroLocomotion>() != null;
            if (!_heroPresent)
            {
                // Drop any in-progress finger claims so we don't resume mid-drag later.
                _panning.Clear();
                _fingerTravel.Clear();
            }
        }

        // True while the DTT scene is active — Lean is owned by LeanTouchAimDriver there,
        // so we must never double-drive. (Bootstrap also prevents spawning in DTT, but a
        // DDOL instance could outlive a scene swap INTO DTT, so guard at runtime too.)
        private static bool InDttScene() => SceneManager.GetActiveScene().name == DttSceneName;

        // Whole-screen suppression: build mode owns its own taps + top-down overview.
        private static bool BuildModeActive()
        {
            return BuildModeController.Instance != null && BuildModeController.Instance.IsActive;
        }

        // ── Lean Touch finger handler (CAMERA_INPUT_OVERHAUL.md §4.2–4.4) ──────
        private void HandleFinger(LeanFinger f)
        {
            if (!_panEnabled || !_heroPresent || f == null) return;
            if (InDttScene()) return;
            if (f.Index < 0) return;            // simulated mouse / hover — desktop fallback owns it

            // Step 1–2: reject GUI + build mode (whole-screen consumers).
            if (f.IsOverGui)      { Release(f.Index); return; }
            if (BuildModeActive()) { Release(f.Index); return; }

            // Step 3: reject fingers that STARTED inside the joystick zone (single source of
            // truth — VirtualJoystick.IsInZone). The joystick claims finger-0 in its zone via
            // legacy Input regardless; we simply never claim a finger that started there.
            if (VirtualJoystick.IsInZone(f.StartScreenPosition)) { Release(f.Index); return; }

            // Step 4: pan candidate. Tap-vs-drag — accumulate travel until past threshold.
            if (!_panning.Contains(f.Index))
            {
                _fingerTravel.TryGetValue(f.Index, out float travelled);
                travelled += f.ScreenDelta.magnitude;
                _fingerTravel[f.Index] = travelled;
                if (travelled < _dragThresholdPx) return;   // still a tap — let attack consume it
                _panning.Add(f.Index);                      // promoted to a drag
            }

            // Claimed: feed the drag delta to the single yaw authority.
            var cam = SmartMobileCamera.Instance;
            if (cam != null && f.ScreenDelta.sqrMagnitude > 0f)
            {
                cam.AddYaw(f.ScreenDelta.x * _dragSensX);
                cam.AddPitch(-f.ScreenDelta.y * _dragSensY);
            }
        }

        private void HandleFingerUp(LeanFinger f)
        {
            if (f == null) return;
            Release(f.Index);
        }

        private void Release(int index)
        {
            _panning.Remove(index);
            _fingerTravel.Remove(index);
        }

        // ── Desktop parity: right-mouse drag = orbit (CAMERA_INPUT_OVERHAUL.md §4.6) ──
        private void Update()
        {
            // Throttled hero re-check (the hero spawns after scene-load, and a scene swap
            // may not have fired OnSceneLoaded yet). NOT per-frame — mirrors VirtualJoystick.
            _heroCheckTimer -= Time.unscaledDeltaTime;
            if (_heroCheckTimer <= 0f) { _heroCheckTimer = 0.5f; RefreshHeroPresence(); }

            if (!_panEnabled || !_heroPresent || InDttScene()) return;
            if (BuildModeActive()) return;   // build mode owns the camera

            // Keyboard orbit — the WebGL-reliable camera control. The right-mouse drag below
            // is swallowed by the browser's context menu in a web build, so on web Q/E (yaw)
            // and R/F (pitch) are how the player moves the camera. Works on standalone too.
            // Feeds the SAME single yaw authority as the mouse, so no movement/gate impact.
            var keys = Keyboard.current;
            var camKeys = SmartMobileCamera.Instance;
            if (keys != null && camKeys != null)
            {
                float kYaw = 0f, kPitch = 0f;
                if (keys.qKey.isPressed) kYaw   -= 1f;
                if (keys.eKey.isPressed) kYaw   += 1f;
                if (keys.rKey.isPressed) kPitch -= 1f;
                if (keys.fKey.isPressed) kPitch += 1f;
                if (kYaw   != 0f) camKeys.AddYaw(kYaw   * _keyOrbitSpeed * Time.unscaledDeltaTime);
                if (kPitch != 0f) camKeys.AddPitch(kPitch * _keyOrbitSpeed * Time.unscaledDeltaTime);
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;

            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= 0f) return;

            var cam = SmartMobileCamera.Instance;
            if (cam == null) return;
            cam.AddYaw(delta.x * _dragSensX);
            cam.AddPitch(-delta.y * _dragSensY);
        }
    }
}
