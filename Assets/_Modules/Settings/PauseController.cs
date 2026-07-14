// =============================================================================
// PauseController — the pause overlay + Time.timeScale handling (audit P0-10).
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #47b): the UXML overlay
// (PauseOverlay.uxml — canon §8: UXML does not render in builds) is RETIRED.
// The overlay is now a code-built kit modal (FrameOptions, narrow portrait):
// Resume / Settings / Quit to Title; the chrome's shared Close = Resume.
//
// WHAT IT DOES (unchanged):
//   * Pause via the HUD PAUSE/BACK button through Core PauseGate
//     (PauseToggleRequested when no modal is open) + public TogglePause().
//   * On pause: Time.timeScale = 0 (freezes wave timers, the ATB tick, enemy
//     movement) + show. On resume: restore the captured pre-pause timeScale.
//   * Settings opens SettingsController over the top; the pause panel hides
//     and re-shows on SettingsClosed. Quit restores timeScale FIRST, then
//     SceneRouter.GoTitle().
//   * OnApplicationPause(true) auto-pauses (platform compliance §2.3);
//     never auto-resumes.
//
// MODULE ISOLATION unchanged: DeNelle.Settings references DeNelle.Core only.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using DeNelle.Core;
using DeNelle.Core.UI;

namespace DeNelle.Settings
{
    /// <summary>
    /// Drives the pause overlay: PauseGate-driven toggle, <see cref="Time.timeScale"/>
    /// freeze, and the Resume / Settings / Quit menu (code-built kit modal).
    /// </summary>
    public sealed class PauseController : MonoBehaviour
    {
        [Header("Settings screen")]
        [Tooltip("The settings screen the Settings button opens. Optional — if unassigned the " +
                 "Settings button is hidden. SettingsController lives in this same module.")]
        [SerializeField] private SettingsController _settings;

        [Header("Input")]
        [Tooltip("Auto-pause when the app is backgrounded (incoming call / task switch). " +
                 "Platform-compliance behaviour — recommended on for mobile builds.")]
        [SerializeField] private bool _pauseOnApplicationPause = true;

        [Header("Events")]
        [Tooltip("Raised whenever the pause state changes — argument is the new IsPaused value.")]
        public UnityEvent<bool> PauseStateChanged = new UnityEvent<bool>();

        private ElarionUiKit.ObsidianModal _modal;
        private bool _paused;

        // The timeScale captured at the moment of pausing — restored on resume.
        // Captured (rather than assumed 1) so pausing during a slow-motion or
        // fast-forward effect restores that, not a hard 1.0.
        private float _timeScaleBeforePause = 1f;

        /// <summary>True while the game is paused and the overlay is showing.</summary>
        public bool IsPaused => _paused;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void OnEnable()
        {
            // Mobile-first: the HUD's on-screen PAUSE/BACK button drives the back/pause
            // decision through the Core PauseGate — when no modal is open the gate raises
            // PauseToggleRequested (the modal-close branch lives in PauseGate/PanelManager).
            PauseGate.PauseToggleRequested -= OnPauseToggleRequested;
            PauseGate.PauseToggleRequested += OnPauseToggleRequested;

            if (_settings != null)
            {
                _settings.SettingsClosed.RemoveListener(OnSettingsClosed);
                _settings.SettingsClosed.AddListener(OnSettingsClosed);
            }
        }

        private void OnDisable()
        {
            PauseGate.PauseToggleRequested -= OnPauseToggleRequested;
            if (_settings != null) _settings.SettingsClosed.RemoveListener(OnSettingsClosed);
        }

        private void OnPauseToggleRequested() => TogglePause();

        /// <summary>
        /// Runtime wiring seam (WO-714 W8): the serialized <c>_settings</c> reference only
        /// works for scene-placed instances, but Settings/Pause are installed by
        /// <see cref="PauseHudBootstrap"/> via AddComponent — Awake/OnEnable run with the
        /// field still null. This attaches the settings screen after construction and
        /// (re)wires the SettingsClosed listener so the pause panel re-shows on Back.
        /// Call before the first Pause(); the Settings button builds only if attached.
        /// </summary>
        public void AttachSettings(SettingsController settings)
        {
            if (_settings == settings) return;
            if (_settings != null) _settings.SettingsClosed.RemoveListener(OnSettingsClosed);
            _settings = settings;
            if (_settings != null)
            {
                _settings.SettingsClosed.RemoveListener(OnSettingsClosed);
                _settings.SettingsClosed.AddListener(OnSettingsClosed);
            }
        }

        private void OnDestroy()
        {
            // Safety net: never leave the engine frozen if this object dies
            // (scene unload, domain reload) while paused.
            if (_paused) Time.timeScale = _timeScaleBeforePause;
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        /// <summary>Auto-pauses when the OS backgrounds the app — platform-compliance
        /// behaviour (audit §2.3). Only ever pauses; never auto-resumes.</summary>
        private void OnApplicationPause(bool isBackgrounded)
        {
            if (_pauseOnApplicationPause && isBackgrounded && !_paused)
                Pause();
        }

        // =====================================================================
        //  Build (kit modal, lazy on first pause)
        // =====================================================================

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            // Chrome Close = Resume. Sits above gameplay panels (31000), below
            // Settings (32000) so the settings screen opens over the top.
            _modal = ElarionUiKit.BuildObsidianModal("PauseUI", "Paused",
                new Vector2(0.34f, 0.24f), new Vector2(0.66f, 0.76f), Resume,
                sortingOrder: 31500,
                frameName: RpgUiCatalog.FrameOptions, medallionIcon: "settings");

            var layout = _modal.chrome.layout;
            var body = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            ElarionUiKit.BuildObsidianButton(body, "Resume",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.10f, 0.74f), new Vector2(0.90f, 0.92f), Resume);
            // Settings button only when a settings screen is wired — never a dead control.
            if (_settings != null)
            {
                ElarionUiKit.BuildObsidianButton(body, "Settings",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.68f), OnSettingsClicked);
            }
            ElarionUiKit.BuildObsidianButton(body, "Quit to Title",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                new Vector2(0.10f, 0.26f), new Vector2(0.90f, 0.44f), OnQuitClicked);

            _modal.canvas.SetActive(false);   // built hidden; Pause shows it
        }

        // =====================================================================
        //  Public API — pause control (HUD pause button calls these)
        // =====================================================================

        /// <summary>Pauses if running, resumes if paused.</summary>
        public void TogglePause()
        {
            if (_paused) Resume();
            else Pause();
        }

        /// <summary>Pauses the game: captures + zeroes <see cref="Time.timeScale"/>
        /// and shows the overlay. Idempotent.</summary>
        public void Pause()
        {
            if (_paused) return;
            EnsureBuilt();

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _paused = true;

            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(true);
            PauseStateChanged?.Invoke(true);
        }

        /// <summary>Resumes: restores the pre-pause <see cref="Time.timeScale"/> and
        /// hides the overlay (and the settings screen, if open). Idempotent.</summary>
        public void Resume()
        {
            if (!_paused) return;

            Time.timeScale = _timeScaleBeforePause;
            _paused = false;

            if (_settings != null && _settings.IsOpen)
                _settings.Close();

            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);
            PauseStateChanged?.Invoke(false);
        }

        // =====================================================================
        //  Button handlers
        // =====================================================================

        /// <summary>Opens settings over the pause panel; the pause panel hides while
        /// settings is up (the game stays frozen) and re-shows on SettingsClosed.</summary>
        private void OnSettingsClicked()
        {
            if (_settings == null) return;
            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);
            _settings.Open();
        }

        /// <summary>Re-shows the pause panel when the settings screen closes (still paused).</summary>
        private void OnSettingsClosed()
        {
            if (_paused && _modal != null && _modal.canvas != null)
                _modal.canvas.SetActive(true);
        }

        /// <summary>Quits to Title. Restores <see cref="Time.timeScale"/> FIRST — the
        /// next scene must never load frozen — then routes via SceneRouter.</summary>
        private void OnQuitClicked()
        {
            Time.timeScale = _timeScaleBeforePause <= 0f ? 1f : _timeScaleBeforePause;
            _paused = false;

            if (_settings != null && _settings.IsOpen)
                _settings.Close();
            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);

            SceneRouter.GoTitle();
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — the pause overlay is fully code-built now.
//   1. Add PauseController to any GameObject (no UIDocument needed). The kit
//      modal builds lazily on first Pause() at sortingOrder 31500 (below the
//      Settings screen's 32000 — settings opens on top).
//   2. Settings: assign a SettingsController; if left empty the Settings button
//      never builds (no dead control).
//   3. HUD pause button: raises PauseGate.RequestBack() — when no modal is open
//      the gate raises PauseToggleRequested, handled here. REQUIRED for mobile.
//   4. Time.timeScale = 0 freezes everything reading Time.deltaTime. Systems
//      that must run while paused use Time.unscaledDeltaTime; uGUI input works
//      frozen (EventSystem is unscaled).
// =============================================================================
