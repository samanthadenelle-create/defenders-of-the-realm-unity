// =============================================================================
// PauseController — the pause overlay + Time.timeScale handling (audit P0-10).
// -----------------------------------------------------------------------------
// The missing-components audit §2.3: "No pause menu, no Time.timeScale
// handling, no pause input action. A tower-defense game with wave timers and
// an ATB battle needs pause for both UX and platform compliance." This is that
// system — the MonoBehaviour behind PauseOverlay.uxml.
//
// WHAT IT DOES:
//   * Toggles pause on the Esc key (new Input System) and via a public
//     TogglePause() the HUD's pause button can call.
//   * On pause: sets Time.timeScale = 0 (freezes wave timers, the ATB tick,
//     enemy movement) and shows the overlay. On resume: restores the previous
//     timeScale and hides it.
//   * The overlay has Resume / Settings / Quit-to-Title.
//       - Resume   -> Resume().
//       - Settings -> opens the SettingsController screen over the top; the
//                     pause panel hides while settings are up and re-shows
//                     when SettingsController raises SettingsClosed.
//       - Quit     -> restores timeScale (never leave the next scene frozen)
//                     and SceneRouter.GoTitle().
//
// PLATFORM COMPLIANCE: OnApplicationPause(true) — Android backgrounding / an
// incoming call — auto-pauses, the behaviour the audit explicitly calls for.
//
// MODULE ISOLATION: DeNelle.Settings references DeNelle.Core only. Pausing is
// done with the engine-global Time.timeScale, so this needs no reference to any
// gameplay module — wave managers, the ATB engine and hero movement all read
// Time.timeScale / Time.deltaTime and freeze for free. Quit-to-Title uses the
// Core SceneRouter. The HUD pause button is wired by the integrator (the HUD
// module cannot see DeNelle.Settings) — see the integrator notes below.
//
// Lives in DeNelle.Settings; references DeNelle.Core only.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using DeNelle.Core;
using DeNelle.Core.UI;

namespace DeNelle.Settings
{
    /// <summary>
    /// Drives the pause overlay: Esc-to-pause, <see cref="Time.timeScale"/>
    /// freeze, and the Resume / Settings / Quit menu. Lives on the pause overlay
    /// <see cref="UIDocument"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument hosting PauseOverlay.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Settings screen")]
        [Tooltip("The settings screen the Settings button opens. Optional — if unassigned the " +
                 "Settings button is hidden. SettingsController lives in this same module.")]
        [SerializeField] private SettingsController _settings;

        [Header("Input")]
        // Mobile-first (keyboard-removal sweep): the Esc-to-pause field + Update() poll were
        // removed — Escape is gone on touch hardware. Pause/back is driven by the HUD's
        // on-screen PAUSE/BACK button via PauseGate.PauseToggleRequested (see OnEnable).
        [Tooltip("Auto-pause when the app is backgrounded (incoming call / task switch). " +
                 "Platform-compliance behaviour — recommended on for mobile builds.")]
        [SerializeField] private bool _pauseOnApplicationPause = true;

        [Header("Events")]
        [Tooltip("Raised whenever the pause state changes — argument is the new IsPaused value. " +
                 "Gameplay can listen to e.g. silence the HUD or stop input polling.")]
        public UnityEvent<bool> PauseStateChanged = new UnityEvent<bool>();

        // ── UXML element names — the binding contract with PauseOverlay.uxml ──
        private const string RootName = "pause-root";
        private const string ResumeButtonName = "pause-resume-button";
        private const string SettingsButtonName = "pause-settings-button";
        private const string QuitButtonName = "pause-quit-button";

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private Button _resumeButton;
        private Button _settingsButton;
        private Button _quitButton;

        private bool _bound;
        private bool _paused;

        // The timeScale captured at the moment of pausing — restored on resume.
        // Captured (rather than assumed 1) so pausing while a slow-motion or
        // fast-forward effect is active restores that, not a hard 1.0.
        private float _timeScaleBeforePause = 1f;

        /// <summary>True while the game is paused and the overlay is showing.</summary>
        public bool IsPaused => _paused;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindElements();
            SetOverlayVisible(false); // start un-paused.

            // Mobile-first (keyboard-removal sweep): Escape is gone. The HUD's on-screen
            // PAUSE/BACK button now drives the back/pause decision through the Core
            // PauseGate — when no modal is open the gate raises PauseToggleRequested, which
            // we handle here exactly as the old Escape "else → TogglePause()" branch did.
            // (The modal-close branch is handled inside PauseGate via PanelManager.)
            PauseGate.PauseToggleRequested -= OnPauseToggleRequested;
            PauseGate.PauseToggleRequested += OnPauseToggleRequested;
        }

        private void OnDisable()
        {
            if (_resumeButton != null) _resumeButton.clicked -= OnResumeClicked;
            if (_settingsButton != null) _settingsButton.clicked -= OnSettingsClicked;
            if (_quitButton != null) _quitButton.clicked -= OnQuitClicked;
            if (_settings != null) _settings.SettingsClosed.RemoveListener(OnSettingsClosed);
            PauseGate.PauseToggleRequested -= OnPauseToggleRequested;
            _bound = false;
        }

        /// <summary>
        /// Handles the Core <see cref="PauseGate.PauseToggleRequested"/> seam — raised by
        /// the HUD PAUSE/BACK button when no modal is open. Toggles pause, the touch-path
        /// equivalent of the removed Escape "else → TogglePause()" branch.
        /// </summary>
        private void OnPauseToggleRequested()
        {
            TogglePause();
        }

        private void OnDestroy()
        {
            // Safety net: never leave the engine frozen if this object dies
            // (scene unload, domain reload) while paused.
            if (_paused) Time.timeScale = _timeScaleBeforePause;
        }

        // Mobile-first (keyboard-removal sweep): the Esc-key Update() poll was REMOVED.
        // The single owner of the back/pause decision (close the open modal, else toggle
        // pause) now lives in Core's PauseGate.RequestBack(), invoked by the HUD's on-screen
        // PAUSE/BACK button. When no modal is open the gate raises PauseToggleRequested,
        // which this controller handles in OnPauseToggleRequested (→ TogglePause). The
        // modal-close half is handled by PauseGate via PanelManager. Behaviour-identical to
        // the retired Escape handler; reachable by TAP on keyboard-less hardware.

        /// <summary>
        /// Auto-pauses when the OS backgrounds the app (incoming call, task
        /// switch) — platform-compliance behaviour the audit §2.3 calls for.
        /// Only ever pauses; it never auto-resumes, so the player chooses when
        /// to return.
        /// </summary>
        private void OnApplicationPause(bool isBackgrounded)
        {
            if (_pauseOnApplicationPause && isBackgrounded && !_paused)
                Pause();
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[PauseController] No UIDocument root — pause overlay will not display.");
                return;
            }

            _resumeButton = _root.Q<Button>(ResumeButtonName);
            _settingsButton = _root.Q<Button>(SettingsButtonName);
            _quitButton = _root.Q<Button>(QuitButtonName);

            if (_resumeButton != null)
            {
                _resumeButton.clicked -= OnResumeClicked;
                _resumeButton.clicked += OnResumeClicked;
            }
            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsClicked;
                _settingsButton.clicked += OnSettingsClicked;
                // No settings screen wired — hide the button rather than offer a dead control.
                _settingsButton.style.display = _settings != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_quitButton != null)
            {
                _quitButton.clicked -= OnQuitClicked;
                _quitButton.clicked += OnQuitClicked;
            }

            if (_settings != null)
            {
                _settings.SettingsClosed.RemoveListener(OnSettingsClosed);
                _settings.SettingsClosed.AddListener(OnSettingsClosed);
            }

            _bound = true;
        }

        // =====================================================================
        //  Public API — pause control (HUD pause button calls these)
        // =====================================================================

        /// <summary>Pauses if running, resumes if paused. The Esc key and a HUD pause button call this.</summary>
        public void TogglePause()
        {
            if (_paused) Resume();
            else Pause();
        }

        /// <summary>
        /// Pauses the game: captures and zeroes <see cref="Time.timeScale"/>
        /// (freezing wave timers, the ATB tick and movement) and shows the
        /// overlay. Idempotent — a second call while paused does nothing.
        /// </summary>
        public void Pause()
        {
            if (_paused) return;
            if (!_bound) BindElements();

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _paused = true;

            SetOverlayVisible(true);
            PauseStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Resumes the game: restores the pre-pause <see cref="Time.timeScale"/>
        /// and hides the overlay (and the settings screen, if it was left open).
        /// Idempotent — a call while running does nothing.
        /// </summary>
        public void Resume()
        {
            if (!_paused) return;

            Time.timeScale = _timeScaleBeforePause;
            _paused = false;

            if (_settings != null && _settings.IsOpen)
                _settings.Close();

            SetOverlayVisible(false);
            PauseStateChanged?.Invoke(false);
        }

        // =====================================================================
        //  Button handlers
        // =====================================================================

        private void OnResumeClicked()
        {
            Resume();
        }

        /// <summary>
        /// Opens the settings screen over the pause panel. The pause panel hides
        /// itself while settings are up (the game stays frozen — settings is a
        /// child of the paused state) and re-shows on <see cref="OnSettingsClosed"/>.
        /// </summary>
        private void OnSettingsClicked()
        {
            if (_settings == null) return;
            SetOverlayVisible(false); // tuck the pause panel behind the settings screen.
            _settings.Open();
        }

        /// <summary>Re-shows the pause panel when the settings screen closes (still paused).</summary>
        private void OnSettingsClosed()
        {
            if (_paused) SetOverlayVisible(true);
        }

        /// <summary>
        /// Quits to the Title scene. Restores <see cref="Time.timeScale"/> FIRST —
        /// the next scene must never load frozen — then routes via the Core
        /// <see cref="SceneRouter"/>.
        /// </summary>
        private void OnQuitClicked()
        {
            // Always un-freeze before leaving, regardless of paused flag state.
            Time.timeScale = _timeScaleBeforePause <= 0f ? 1f : _timeScaleBeforePause;
            _paused = false;

            if (_settings != null && _settings.IsOpen)
                _settings.Close();
            SetOverlayVisible(false);

            SceneRouter.GoTitle();
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>Shows / hides the pause overlay scrim + panel.</summary>
        private void SetOverlayVisible(bool visible)
        {
            var root = _root ?? (_document != null ? _document.rootVisualElement : null);
            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the pause overlay.
// -----------------------------------------------------------------------------
//   1. Create a GameObject with a UIDocument; assign PauseOverlay.uxml as its
//      Source Asset. Add this PauseController component beside it. Sort it
//      ABOVE the HUD but BELOW the settings overlay (settings opens on top).
//      One per gameplay scene (Village, Dungeon, ATBBattle), or a shared
//      DontDestroyOnLoad object — either works.
//
//   2. Settings: assign a SettingsController (PauseOverlay's "Settings" button
//      opens it). If left empty the Settings button hides itself.
//
//   3. HUD pause button: the DeNelle.HUD module cannot see DeNelle.Settings, so
//      the integrator wires the HUD's pause button to PauseController. Either:
//        - add a pause Button to VillageHud.uxml and, in the village scene
//          builder, hook it: pauseButton.clicked += pauseController.TogglePause;
//        - or call pauseController.TogglePause() from wherever the HUD raises a
//          pause request. (Esc already works with no wiring on desktop.)
//
//   4. Esc-to-pause uses the new Input System (UnityEngine.InputSystem). On
//      keyboard-less hardware Keyboard.current is null and the HUD pause button
//      is the pause control — so step 3 is REQUIRED for mobile.
//
//   5. Time.timeScale = 0 freezes everything that reads Time.deltaTime — wave
//      timers, the ATB tick, enemy/hero movement. Any system that must keep
//      running while paused (UI animations, this overlay) should use
//      Time.unscaledDeltaTime. UI Toolkit transitions are unscaled already.
// =============================================================================
