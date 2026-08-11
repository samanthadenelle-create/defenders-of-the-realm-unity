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
using UnityEngine.UI;
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

        // Single-modal arbiter handle. The pause menu is a SYSTEM modal that may open during
        // an active battle, so it registers battle-allowed (a plain gameplay panel would be
        // rejected + force-closed by the battle-lock). Close delegate = Resume; isOpen = _paused.
        private PanelHandle _panelHandle;

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
            if (_paused) Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            // Don't leak the arbiter slot if destroyed while paused (scene unload).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
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
            //
            // STACKING FIX (owner 2026-07-19 "pause button has options stacked"): the option
            // buttons are laid out by the common VerticalLayoutGroup column (below), which cannot
            // self-overlap. The overlap was the shared frame CLOSE button (== Resume): FrameOptions
            // seats it at the bottom footer band and SeatSharedCloseInside grows it UPWARD ~132px
            // INTO the body. On the previous short modal (frac height 0.64) the Close button's top
            // reached ~content-frac 0.247 while the button column's body floor sat at 0.150 -- so the
            // Close control climbed ~0.10 of the body (~50px) into the column's lower region and
            // collided with the bottom option ("Quit to Title"). Fix, all inside this one file:
            //   (a) a TALLER modal (frac height 0.78) for comfortable vertical room, and
            //   (b) a bigger column bottomInset (0.18) so the stack floor clears the Close band --
            // guaranteeing disjoint, gapped, >=MinTouchPx bands that always fit inside the frame at
            // any screen size (verified headless: Builds/ui-capture/PauseMenu_<res>.png).
            _modal = ElarionUiKit.BuildObsidianModal("PauseUI", "Paused",
                new Vector2(0.33f, 0.11f), new Vector2(0.67f, 0.89f), Resume,
                sortingOrder: 31500,
                frameName: RpgUiCatalog.FrameOptions, medallionIcon: "settings");

            var layout = _modal.chrome.layout;
            var body = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // Common spaced button column (ElarionUiKit) -- one shared VerticalLayoutGroup so buttons
            // never overlap under the touch floor, at any screen size (owner 2026-07-16 "fix in
            // common"). bottomInset 0.18 keeps the whole stack ABOVE the frame's shared Close button.
            var stack = ElarionUiKit.BuildButtonColumn(body,
                gapPx: 18f, sideInset: 0.08f, topInset: 0.05f, bottomInset: 0.18f);
            ElarionUiKit.AddColumnButton(stack, "Resume",
                ElarionUiKit.ObsidianButtonColor.Green, Resume);
            // Settings button only when a settings screen is wired — never a dead control.
            if (_settings != null)
                ElarionUiKit.AddColumnButton(stack, "Settings",
                    ElarionUiKit.ObsidianButtonColor.Gray, OnSettingsClicked);
            ElarionUiKit.AddColumnButton(stack, "Quit to Title",
                ElarionUiKit.ObsidianButtonColor.Red, OnQuitClicked);

            // Register with the single-modal arbiter (battle-allowed — a pause menu must be
            // openable mid-combat and never rejected). The back button / arbiter close = Resume.
            if (_panelHandle == null)
                _panelHandle = PanelManager.RegisterBattleAllowed("Pause", Resume, () => _paused);

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

            // WO-1016/P0 (owner F8 seq 2319, 2026-08-10 — "No locomotioonj in town"): capture the
            // pre-pause scale, but NEVER capture a FROZEN one. Two independent systems freeze the
            // world (this controller, and BreakCaptureHarness.FlagHere's F8 note freeze at
            // BreakCaptureHarness.cs:474). If the OS backgrounds the app while the F8 note box is up,
            // Time.timeScale is ALREADY 0 here, so the old line captured 0 and Resume() below restored
            // 0 — a PERMANENT, INVISIBLE freeze: the pause modal closes, input is still read, the
            // camera still orbits (unscaled), build mode still works (its own input path), and the
            // hero cannot move because Time.deltaTime is 0. That is exactly the captured signature
            // (live input in [Flow:HeroDrift] with vel=(0.000,0.000) and a frozen animator baseNt).
            // A capture of <=0 is never meaningful to restore, so it degrades to 1.
            float observed = Time.timeScale;
            _timeScaleBeforePause = observed > 0f ? observed : 1f;
            Time.timeScale = 0f;
            _paused = true;
            DeNelle.Core.Diagnostics.FlowTrace.Step("Pause",
                $"PAUSE -> timeScale 0 (captured {observed:F2}" +
                (observed > 0f ? "" : " <= 0, ALREADY FROZEN by another owner — restoring to 1 instead") +
                $"). Resume will restore {_timeScaleBeforePause:F2}.");

            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(true);
            // Announce the pause modal opened so the arbiter arms the back button + closes any prior panel.
            if (_panelHandle != null) PanelManager.NotifyOpened(_panelHandle);
            PauseStateChanged?.Invoke(true);
        }

        /// <summary>Resumes: restores the pre-pause <see cref="Time.timeScale"/> and
        /// hides the overlay (and the settings screen, if open). Idempotent.</summary>
        public void Resume()
        {
            if (!_paused) return;

            // Belt-and-braces with the capture guard above: a restore is the LAST place a frozen
            // world may be re-armed, so it can never write a non-positive scale (the same guard
            // OnQuitClicked has always had — it was simply missing on the path players use).
            Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            _paused = false;
            DeNelle.Core.Diagnostics.FlowTrace.Step("Pause",
                $"RESUME -> timeScale {Time.timeScale:F2} (captured {_timeScaleBeforePause:F2}).");

            if (_settings != null && _settings.IsOpen)
                _settings.Close();

            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);
            // Release the arbiter slot as the pause menu closes (no-op if already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
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
            // Relinquish the arbiter slot BEFORE opening Settings so the settings modal does not
            // swap-close the pause menu (whose close delegate is Resume — that would unfreeze the
            // game). The pause panel stays paused + hidden and re-shows on SettingsClosed.
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
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
            // Release the arbiter slot as we leave for the title (no-op if already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);

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
