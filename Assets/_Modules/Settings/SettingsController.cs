// =============================================================================
// SettingsController — drives the options menu (audit P0-8 §2.1).
// -----------------------------------------------------------------------------
// The MonoBehaviour behind SettingsScreen.uxml — the settings screen the
// missing-components audit (§2.1) says does not exist anywhere in the project.
//
// WHAT IT OFFERS:
//   * Audio    — Master / Music / SFX volume sliders + a global mute toggle.
//   * Graphics — a three-tier quality selector (Seeker_Low / Seeker_High /
//                Desktop), buttons built at runtime from QualityTier.
//   * Comfort  — a screen-shake on/off toggle (accessibility — audit §2.7).
//   * A "Reset to defaults" and a "Back" button.
//
// PERSISTENCE: every control writes straight through SettingsModel, which
// persists immediately (PlayerPrefs + the Core save layer) and applies the
// value live (AudioMixerBridge / SeekerBootstrap / ScreenShakeSetting). There
// is no separate "save" step — changes are durable the moment they are made,
// and SettingsBootstrap re-applies them on the next launch.
//
// MODAL, not a HUD: the screen is shown / hidden by Open() / Close(); while
// open it paints a full-screen scrim and captures input. It does NOT own a
// scene — the pause overlay (or a future title Options button) shows it as an
// overlay UIDocument. SettingsClosed fires when the player taps Back so the
// opener (e.g. PauseController) can return focus to itself.
//
// Lives in DeNelle.Settings; references DeNelle.Core only — module isolation.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEngine.Audio;

namespace DeNelle.Settings
{
    /// <summary>
    /// Drives the options menu: audio sliders, the quality-tier selector and the
    /// screen-shake toggle. Modal — <see cref="Open"/> / <see cref="Close"/>
    /// show and hide it; every control persists + applies through
    /// <see cref="SettingsModel"/>. Lives on the settings overlay
    /// <see cref="UIDocument"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SettingsController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument hosting SettingsScreen.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Audio mixer (optional)")]
        [Tooltip("The project AudioMixer. Optional — if left empty, AudioMixerBridge resolves it " +
                 "from Resources/Audio/GameAudioMixer, and no-ops safely until the Audio-system " +
                 "agent's mixer asset exists. Assign it here once that mixer ships.")]
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Behaviour")]
        [Tooltip("Hide the settings screen on Awake so it starts closed.")]
        [SerializeField] private bool _startHidden = true;

        [Header("Events")]
        [Tooltip("Raised when the player taps Back / closes the screen. The opener " +
                 "(e.g. the pause overlay) listens to restore its own focus.")]
        public UnityEvent SettingsClosed = new UnityEvent();

        // ── UXML element names — the binding contract with SettingsScreen.uxml ─
        private const string RootName = "settings-root";
        private const string MasterSliderName = "settings-master-slider";
        private const string MasterValueName = "settings-master-value";
        private const string MusicSliderName = "settings-music-slider";
        private const string MusicValueName = "settings-music-value";
        private const string SfxSliderName = "settings-sfx-slider";
        private const string SfxValueName = "settings-sfx-value";
        private const string MuteToggleName = "settings-mute-toggle";
        private const string QualityRowName = "settings-quality-row";
        private const string ShakeToggleName = "settings-shake-toggle";
        private const string AudioSeamName = "settings-audio-seam";
        private const string ResetButtonName = "settings-reset-button";
        private const string BackButtonName = "settings-back-button";

        // ── USS class names — styled by SettingsScreen.uss ───────────────────
        private const string TierButtonClass = "quality-tier-button";
        private const string TierButtonActiveClass = "quality-tier-button--active";

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private Slider _masterSlider;
        private Label _masterValue;
        private Slider _musicSlider;
        private Label _musicValue;
        private Slider _sfxSlider;
        private Label _sfxValue;
        private Toggle _muteToggle;
        private VisualElement _qualityRow;
        private Toggle _shakeToggle;
        private Label _audioSeam;
        private Button _resetButton;
        private Button _backButton;

        // One quality-tier button paired with the tier it selects.
        private readonly Button[] _tierButtons = new Button[3];
        private static readonly QualityTier[] Tiers =
        {
            QualityTier.SeekerLow, QualityTier.SeekerHigh, QualityTier.Desktop,
        };

        private bool _bound;
        private bool _open;

        /// <summary>True while the settings screen is open and visible.</summary>
        public bool IsOpen => _open;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();

            // Hand a directly-assigned mixer to the bridge — it takes priority
            // over the Resources lookup. Null is fine: the bridge then resolves
            // lazily and no-ops if the asset is still absent.
            if (_audioMixer != null)
                AudioMixerBridge.SetMixer(_audioMixer);
        }

        private void OnEnable()
        {
            BindElements();
            if (_startHidden) SetVisible(false);
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            _bound = false;
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[SettingsController] No UIDocument root — settings screen will not display.");
                return;
            }

            _masterSlider = _root.Q<Slider>(MasterSliderName);
            _masterValue = _root.Q<Label>(MasterValueName);
            _musicSlider = _root.Q<Slider>(MusicSliderName);
            _musicValue = _root.Q<Label>(MusicValueName);
            _sfxSlider = _root.Q<Slider>(SfxSliderName);
            _sfxValue = _root.Q<Label>(SfxValueName);
            _muteToggle = _root.Q<Toggle>(MuteToggleName);
            _qualityRow = _root.Q<VisualElement>(QualityRowName);
            _shakeToggle = _root.Q<Toggle>(ShakeToggleName);
            _audioSeam = _root.Q<Label>(AudioSeamName);
            _resetButton = _root.Q<Button>(ResetButtonName);
            _backButton = _root.Q<Button>(BackButtonName);

            BuildQualityButtons();
            RegisterCallbacks();
            RefreshFromModel();
            _bound = true;
        }

        /// <summary>Builds the three quality-tier buttons into the quality row once.</summary>
        private void BuildQualityButtons()
        {
            if (_qualityRow == null) return;
            _qualityRow.Clear();

            for (int i = 0; i < Tiers.Length; i++)
            {
                QualityTier tier = Tiers[i];
                var button = new Button { name = $"quality-tier-{i}", text = SettingsModel.TierLabel(tier) };
                button.AddToClassList(TierButtonClass);
                // Capture the tier in a local so the closure binds the right value.
                QualityTier captured = tier;
                button.clicked += () => OnQualityTierClicked(captured);
                _qualityRow.Add(button);
                _tierButtons[i] = button;
            }
        }

        private void RegisterCallbacks()
        {
            if (_masterSlider != null) _masterSlider.RegisterValueChangedCallback(OnMasterChanged);
            if (_musicSlider != null) _musicSlider.RegisterValueChangedCallback(OnMusicChanged);
            if (_sfxSlider != null) _sfxSlider.RegisterValueChangedCallback(OnSfxChanged);
            if (_muteToggle != null) _muteToggle.RegisterValueChangedCallback(OnMuteChanged);
            if (_shakeToggle != null) _shakeToggle.RegisterValueChangedCallback(OnShakeChanged);
            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
            if (_backButton != null) _backButton.clicked += OnBackClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_masterSlider != null) _masterSlider.UnregisterValueChangedCallback(OnMasterChanged);
            if (_musicSlider != null) _musicSlider.UnregisterValueChangedCallback(OnMusicChanged);
            if (_sfxSlider != null) _sfxSlider.UnregisterValueChangedCallback(OnSfxChanged);
            if (_muteToggle != null) _muteToggle.UnregisterValueChangedCallback(OnMuteChanged);
            if (_shakeToggle != null) _shakeToggle.UnregisterValueChangedCallback(OnShakeChanged);
            if (_resetButton != null) _resetButton.clicked -= OnResetClicked;
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
        }

        // =====================================================================
        //  Public API — Open / Close (the pause overlay drives these)
        // =====================================================================

        /// <summary>
        /// Opens the settings screen. Re-reads the persisted values into every
        /// control first so the screen always reflects the current state.
        /// </summary>
        public void Open()
        {
            if (!_bound) BindElements();
            RefreshFromModel();
            SetVisible(true);
            _open = true;
        }

        /// <summary>
        /// Closes the settings screen and raises <see cref="SettingsClosed"/>.
        /// Equivalent to the player tapping Back.
        /// </summary>
        public void Close()
        {
            SetVisible(false);
            _open = false;
            SettingsClosed?.Invoke();
        }

        // =====================================================================
        //  Refresh — pull every persisted value into the controls
        // =====================================================================

        /// <summary>
        /// Loads every control from <see cref="SettingsModel"/>. The slider /
        /// toggle writes here are guarded by <see cref="_suppressCallbacks"/> so
        /// setting them programmatically does not echo back as a "changed" event.
        /// </summary>
        private void RefreshFromModel()
        {
            _suppressCallbacks = true;
            try
            {
                if (_masterSlider != null) _masterSlider.value = SettingsModel.MasterVolume;
                if (_musicSlider != null) _musicSlider.value = SettingsModel.MusicVolume;
                if (_sfxSlider != null) _sfxSlider.value = SettingsModel.SfxVolume;
                if (_muteToggle != null) _muteToggle.value = SettingsModel.Muted;
                if (_shakeToggle != null) _shakeToggle.value = SettingsModel.ScreenShake;

                UpdateVolumeLabels();
                UpdateQualityHighlight(SettingsModel.Quality);
                UpdateAudioSeamNotice();
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }

        private bool _suppressCallbacks;

        /// <summary>Refreshes the three percentage labels next to the volume sliders.</summary>
        private void UpdateVolumeLabels()
        {
            if (_masterValue != null && _masterSlider != null)
                _masterValue.text = FormatPercent(_masterSlider.value);
            if (_musicValue != null && _musicSlider != null)
                _musicValue.text = FormatPercent(_musicSlider.value);
            if (_sfxValue != null && _sfxSlider != null)
                _sfxValue.text = FormatPercent(_sfxSlider.value);
        }

        /// <summary>Marks the active tier button and clears the others.</summary>
        private void UpdateQualityHighlight(QualityTier active)
        {
            for (int i = 0; i < _tierButtons.Length; i++)
            {
                if (_tierButtons[i] == null) continue;
                _tierButtons[i].EnableInClassList(TierButtonActiveClass, Tiers[i] == active);
            }
        }

        /// <summary>
        /// Shows the audio-mixer seam notice only while the mixer asset is
        /// absent — once the Audio-system agent's mixer is wired, it hides itself.
        /// </summary>
        private void UpdateAudioSeamNotice()
        {
            if (_audioSeam == null) return;
            bool show = !AudioMixerBridge.HasMixer;
            _audioSeam.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // =====================================================================
        //  Control callbacks — each persists + applies through SettingsModel
        // =====================================================================

        private void OnMasterChanged(ChangeEvent<float> evt)
        {
            if (_suppressCallbacks) return;
            SettingsModel.MasterVolume = evt.newValue;
            SettingsModel.ApplyAudio();
            UpdateVolumeLabels();
        }

        private void OnMusicChanged(ChangeEvent<float> evt)
        {
            if (_suppressCallbacks) return;
            SettingsModel.MusicVolume = evt.newValue;
            SettingsModel.ApplyAudio();
            UpdateVolumeLabels();
        }

        private void OnSfxChanged(ChangeEvent<float> evt)
        {
            if (_suppressCallbacks) return;
            SettingsModel.SfxVolume = evt.newValue;
            SettingsModel.ApplyAudio();
            UpdateVolumeLabels();
        }

        private void OnMuteChanged(ChangeEvent<bool> evt)
        {
            if (_suppressCallbacks) return;
            SettingsModel.Muted = evt.newValue;
            SettingsModel.ApplyAudio();
        }

        private void OnShakeChanged(ChangeEvent<bool> evt)
        {
            if (_suppressCallbacks) return;
            SettingsModel.ScreenShake = evt.newValue;
            SettingsModel.ApplyScreenShake();
        }

        private void OnQualityTierClicked(QualityTier tier)
        {
            SettingsModel.Quality = tier;
            SettingsModel.ApplyQuality();
            UpdateQualityHighlight(tier);
        }

        private void OnResetClicked()
        {
            SettingsModel.ResetToDefaults();
            RefreshFromModel();
        }

        private void OnBackClicked()
        {
            Close();
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>Formats a 0..1.5 volume value as a rounded percentage string.</summary>
        private static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp(value, 0f, SettingsModel.MaxVolume) * 100f)}%";
        }

        /// <summary>Shows / hides the whole settings overlay.</summary>
        private void SetVisible(bool visible)
        {
            var root = _root ?? (_document != null ? _document.rootVisualElement : null);
            if (root != null)
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the settings screen.
// -----------------------------------------------------------------------------
//   1. Create a GameObject with a UIDocument; assign SettingsScreen.uxml as its
//      Source Asset. Add this SettingsController component beside it. Put it on
//      a Canvas / panel sort-order ABOVE the HUD and the pause overlay so it
//      renders on top.
//
//   2. AudioMixer: once the Audio-system agent ships the mixer, assign it to
//      the "Audio Mixer" field here (or place it at Resources/Audio/
//      GameAudioMixer.mixer). Until then the sliders persist and the screen
//      shows the seam notice. The exposed parameters MUST be named MasterVol /
//      MusicVol / SfxVol — see AudioMixerBridge.
//
//   3. The pause overlay opens this screen: see PauseController, which holds a
//      serialized reference to this component and calls Open(). This controller
//      raises SettingsClosed on Back so the pause overlay can re-show itself.
//
//   4. The settings GameObject can be marked DontDestroyOnLoad and shared
//      across scenes, or instanced per scene that needs options — either works;
//      SettingsModel is static so no state is lost across scene loads.
// =============================================================================
