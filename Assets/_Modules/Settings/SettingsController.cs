// =============================================================================
// SettingsController — drives the options menu (audit P0-8 §2.1).
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #47): the UXML screen
// (SettingsScreen.uxml — canon-flagged "UXML does not render in builds", §8)
// is RETIRED. The screen is now code-built uGUI on the Obsidian master frame
// (BuildObsidianModal: FrameSettings + the ONE shared Close + scrim).
//
// WHAT IT OFFERS (unchanged):
//   * Audio    — Master / Music / SFX volume sliders + a global mute toggle.
//   * Gameplay — Easy / Normal / Hard difficulty selector + blurb.
//   * Graphics — Seeker_Low / Seeker_High / Desktop quality selector.
//   * Comfort  — screen-shake on/off toggle (accessibility — audit §2.7).
//   * Help     — Game Guide button (WO-588); Reset to defaults.
//   * Back     = the chrome's shared Close (raises SettingsClosed for the
//     pause overlay, exactly as before).
//
// PERSISTENCE unchanged: every control writes straight through SettingsModel
// (persists immediately + applies live). Public API unchanged: Open() /
// Close() / IsOpen / SettingsClosed — PauseController's wiring still holds.
// =============================================================================

using DeNelle.Core.Diagnostics;
using System;
using DeNelle.Core.Platform;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Audio;

namespace DeNelle.Settings
{
    /// <summary>
    /// Drives the options menu: audio sliders, quality/difficulty selectors and
    /// the screen-shake toggle. Modal — <see cref="Open"/> / <see cref="Close"/>
    /// show and hide it; every control persists + applies through
    /// <see cref="SettingsModel"/>.
    /// </summary>
    public sealed class SettingsController : MonoBehaviour
    {
        [Header("Audio mixer (optional)")]
        [Tooltip("The project AudioMixer. Optional — if left empty, AudioMixerBridge resolves it " +
                 "from Resources/Audio/GameAudioMixer, and no-ops safely until the mixer asset exists.")]
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Events")]
        [Tooltip("Raised when the player taps Back / closes the screen. The opener " +
                 "(e.g. the pause overlay) listens to restore its own focus.")]
        public UnityEvent SettingsClosed = new UnityEvent();

        private ElarionUiKit.ObsidianModal _modal;
        private Slider _masterSlider, _musicSlider, _sfxSlider;
        private TextMeshProUGUI _masterValue, _musicValue, _sfxValue;
        private Toggle _muteToggle, _shakeToggle, _musicToggle;
        private Transform _qualityRow, _difficultyRow;
        private TextMeshProUGUI _difficultyBlurb, _audioSeam;
        private Button _walletConnectButton, _walletDisconnectButton;

        private static readonly QualityTier[] Tiers =
        {
            QualityTier.SeekerLow, QualityTier.SeekerHigh, QualityTier.Desktop,
        };
        private static readonly Difficulty[] Difficulties =
        {
            Difficulty.Easy, Difficulty.Normal, Difficulty.Hard,
        };

        private bool _open;
        private bool _suppressCallbacks;

        // AUDIT FIX (2026-07-30): px layout ladder. Sum of every rung + gap below:
        //   top pad 14 + 5 captions x 54 + 3 slider rows x 68 + 3 toggle rows x 76
        //   + seam 60 + difficulty row 132 + blurb 54 + quality row 132
        //   + wallet caption/row 174 + help/reset row 120 + bottom pad 24 = 1370.
        // Keep this constant in sync with the EnsureBuilt ladder if rungs change.
        private const float RequiredLadderPx = 1370f;

        /// <summary>Resolved height (canvas-local px) of the scroll content every band is a
        /// fraction of: max(body px, <see cref="RequiredLadderPx"/>). Set in EnsureBuilt.</summary>
        private float _ladderPx = RequiredLadderPx;

        /// <summary>Convert a canvas-local px extent to a fraction of the scroll content, so
        /// every band resolves to a KNOWN px size (button rungs stay >= the 112 px kit touch
        /// floor and ClampMinTouch never inflates them over neighbouring label bands).</summary>
        private float Frac(float px) => px / Mathf.Max(1f, _ladderPx);

        // Single-modal arbiter handle. Settings is a SYSTEM modal reachable from the pause
        // menu during an active battle, so it registers battle-allowed (never rejected by the
        // battle-lock). Close delegate = Close (raises SettingsClosed); isOpen = _open.
        private PanelHandle _panelHandle;

        /// <summary>True while the settings screen is open and visible.</summary>
        public bool IsOpen => _open;

        private void Awake()
        {
            // Hand a directly-assigned mixer to the bridge — priority over the
            // Resources lookup. Null is fine: the bridge resolves lazily.
            if (_audioMixer != null)
                AudioMixerBridge.SetMixer(_audioMixer);
            CurrencySkinResolver.WalletConnectionChanged += OnWalletConnectionChanged;
        }

        private void OnDestroy()
        {
            CurrencySkinResolver.WalletConnectionChanged -= OnWalletConnectionChanged;
            // Don't leak the arbiter slot if destroyed while open (scene unload).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        // =====================================================================
        //  Public API — Open / Close (the pause overlay drives these)
        // =====================================================================

        /// <summary>Opens the settings screen; re-reads persisted values first.</summary>
        public void Open()
        {
            EnsureBuilt();
            if (_modal == null || _modal.canvas == null) return;
            RefreshFromModel();
            _modal.canvas.SetActive(true);
            _open = true;
            // Announce the settings modal opened so the arbiter arms the back button.
            if (_panelHandle != null) PanelManager.NotifyOpened(_panelHandle);
        }

        /// <summary>Closes the settings screen and raises <see cref="SettingsClosed"/>.</summary>
        public void Close()
        {
            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);
            _open = false;
            // Release the arbiter slot as settings closes (no-op if already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            SettingsClosed?.Invoke();
        }

        // =====================================================================
        //  Build (kit modal, lazy on first open)
        // =====================================================================

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            // Chrome Close = Back (raises SettingsClosed via Close()).
            // WO-714 W8 (P7 font floor): widened 0.26-0.74 -> 0.08-0.92. The old ~518
            // reference-px panel could not seat FontFloor(30) text in its fractional
            // zones ("100%" needs ~60px, the value zone had ~52); at ~907px every zone
            // seats floor-size text. Portrait near-full-width matches the other
            // Obsidian panels (upgrade/inventory).
            _modal = ElarionUiKit.BuildObsidianModal("SettingsUI", "Settings",
                new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f), Close,
                sortingOrder: 32000,   // settings sits above every other modal
                frameName: RpgUiCatalog.FrameSettings, medallionIcon: "settings");

            // Register with the single-modal arbiter (battle-allowed — a system settings screen
            // must be openable from pause mid-combat and never rejected). Arbiter close = Close.
            if (_panelHandle == null)
                _panelHandle = PanelManager.RegisterBattleAllowed("Settings", Close, () => _open);

            var layout = _modal.chrome.layout;

            // FRESH-CAPTURE FIX (2026-07-06): the FrameSettings header band (y 0.905–0.995,
            // the art's top-centre TAB) renders ABOVE the visible slab at this panel stretch —
            // the "Settings" title read as a plate floating outside the panel. Re-seat the
            // chrome's header zone INSIDE the panel top and back it with an obsidian plate so
            // the title always reads on chrome (the title label is a child of the zone).
            if (layout != null && layout.header != null)
            {
                layout.header.anchorMin = new Vector2(0.28f, 0.885f);
                layout.header.anchorMax = new Vector2(0.72f, 0.945f);
                var backing = new GameObject("TitleBacking", typeof(Image));
                backing.transform.SetParent(layout.header, false);
                var bkRt = (RectTransform)backing.transform;
                bkRt.anchorMin = Vector2.zero; bkRt.anchorMax = Vector2.one;
                bkRt.offsetMin = Vector2.zero; bkRt.offsetMax = Vector2.zero;
                var bkImg = backing.GetComponent<Image>();
                bkImg.color = ElarionUiKit.ObsidianFill;
                bkImg.raycastTarget = false;
                backing.transform.SetAsFirstSibling();
            }

            var bodyZone = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // AUDIT FIX (2026-07-30, 16-panel headed audit): the old layout banded rows as
            // fractions of the body, so a 0.055 button band resolved to ~74 px (portrait) /
            // ~40 px (landscape) and the kit touch floor (ClampMinTouch, MinTouchPx=112)
            // grew every selector/help button symmetrically PAST its band - over the section
            // header above (headers read as "Ga"/"Gr"/"H" slivers behind the button grid)
            // and the difficulty caption below (it rendered across the Easy/Normal/Hard
            // chips). Re-banded on a PX LADDER: every band is a fixed canvas-local px rung
            // (button rows 120 px >= the 112 floor, so the floor can never inflate them),
            // stacked inside a vertical scroller (RumorBoard WO-795 pattern) whose content
            // height = max(body px, ladder px). Portrait bodies fit with no scroll; short
            // (landscape) bodies scroll instead of overlapping. Fonts untouched (owner law:
            // fix layout, never shrink fonts). Zero behavior change - same controls, same
            // wiring, same persistence.
            float bodyPx = BodyLocalHeight(_modal.canvas, bodyZone);
            _ladderPx = Mathf.Max(bodyPx, RequiredLadderPx);
            var body = BuildScrollHost(bodyZone, _ladderPx);

            float y = 1f - Frac(14f);
            // ── Audio ────────────────────────────────────────────────────────
            y = Caption(body, "Audio", y);
            (_masterSlider, _masterValue) = SliderRow(body, "Master", ref y, OnMasterChanged);
            (_musicSlider,  _musicValue)  = SliderRow(body, "Music",  ref y, OnMusicChanged);
            (_sfxSlider,    _sfxValue)    = SliderRow(body, "SFX",    ref y, OnSfxChanged);
            // Music On/Off — the affordance the retired HUD overlay used to carry
            // (owner bug 2026-07-12: the on-screen button overlapped mobile controls).
            // Same seam: writes SettingsModel.MusicVolume / Muted + drives the live
            // AudioService via AudioServiceBridge, so it is audible immediately.
            _musicToggle = ToggleRow(body, "Music", ref y, OnMusicOnOffChanged);
            _muteToggle = ToggleRow(body, "Mute all audio", ref y, OnMuteChanged);
            // WO-714 W8 (P7): 11 -> 30 (FontFloor) + FitBlock; row height grew to seat it.
            _audioSeam = MakeText(body, "Audio mixer not wired yet - volumes persist and apply when it lands.",
                30, ElarionUi.ParchmentDim, FontStyles.Italic, TextAlignmentOptions.Left,
                new Vector2(0.06f, y - Frac(52f)), new Vector2(0.94f, y), multiline: true);
            y -= Frac(60f);

            // ── Gameplay ─────────────────────────────────────────────────────
            y = Caption(body, "Gameplay", y);
            // 120 px rung: >= the 112 px kit touch floor, so ClampMinTouch never grows
            // these chips out of their band (the audit's header/caption overlap).
            _difficultyRow = ZoneRect(body, "DifficultyRow", new Vector2(0.06f, y - Frac(120f)), new Vector2(0.94f, y));
            y -= Frac(132f);
            // AUDIT FIX (2026-07-30): the difficulty caption gets its own clear band BELOW
            // the Easy/Normal/Hard chips - single line, no wrap, TMP Ellipsis (FitSingleLine
            // via multiline:false), so it can never render across the buttons again.
            _difficultyBlurb = MakeText(body, "", 30, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.Left, new Vector2(0.06f, y - Frac(46f)), new Vector2(0.94f, y));
            y -= Frac(54f);

            // ── Graphics ─────────────────────────────────────────────────────
            y = Caption(body, "Graphics", y);
            _qualityRow = ZoneRect(body, "QualityRow", new Vector2(0.06f, y - Frac(120f)), new Vector2(0.94f, y));
            y -= Frac(132f);

            // ── Comfort ──────────────────────────────────────────────────────
            y = Caption(body, "Comfort", y);
            _shakeToggle = ToggleRow(body, "Screen shake", ref y, OnShakeChanged);

            // WO-1171 section 4: the real player-facing home for BOTH halves of wallet
            // ownership. The Wallet assembly stays behind CurrencySkinResolver's Core seam.
            // Two explicit controls make the available action legible without relying on colour;
            // the inapplicable one remains visible but disabled so players can discover both.
            y = Caption(body, "Wallet", y);
            _walletConnectButton = ElarionUiKit.BuildObsidianButton(body, "Connect Wallet",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y),
                CurrencySkinResolver.RequestWalletConnect);
            _walletDisconnectButton = ElarionUiKit.BuildObsidianButton(body, "Disconnect Wallet",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                new Vector2(0.52f, y - Frac(120f)), new Vector2(0.94f, y),
                CurrencySkinResolver.RequestWalletDisconnect);
            y -= Frac(132f);

            // ── Help + Reset (WO-588) ────────────────────────────────────────
            y = Caption(body, "Help", y);
            ElarionUiKit.BuildObsidianButton(body, "Game Guide",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y), OnGameGuideClicked);
            ElarionUiKit.BuildObsidianButton(body, "Reset Defaults",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                new Vector2(0.52f, y - Frac(120f)), new Vector2(0.94f, y), OnResetClicked);
            y -= Frac(120f);   // step PAST the Help row - Caption only advances 54, a button row is 120

            // -- Defence reports (WO-1026) -----------------------------------
            // WHY SETTINGS AND NOT THE ACTION BAR: CLAUDE.md §7 caps the calm(town) bar at SIX
            // visible faces and spends paragraphs on why; a seventh face would silently undo that
            // ruling. This is the SAME answer WO-588 reached for the Game Guide - a secondary
            // screen that must be reachable without eating a bar face lives behind Settings.
            // The row is only BUILT when the player has reports (the panel is registered
            // scene-independently either way), so a fresh save shows no dead button, and the
            // unread count rides in the LABEL TEXT - never a coloured dot, which the owner
            // cannot see.
            // ⚠ REACHABLE, NOT YET DISCOVERABLE. Settings is two taps from town and that clears
            // the acceptance bar, but a player who never opens Settings will not learn the report
            // exists. A discoverable surface (an unread badge in town) is an owner felt-call, and
            // is flagged in the WO rather than minted here.
            int unread = DeNelle.Core.Defense.DefenseReportLedger.UnreadCount();
            int reportCount = DeNelle.Core.Defense.DefenseReportLedger.All().Count;
            if (reportCount > 0)
            {
                y = Caption(body, "Your Town", y);
                string label = unread > 0
                    ? "Defence Reports (" + unread + " new)"
                    : "Defence Reports";
                ElarionUiKit.BuildObsidianButton(body, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y), OnDefenseReportsClicked);
                y -= Frac(120f);
            }

            // -- Legal (store-readiness 2026-08-19) --------------------------
            // Most app stores require the privacy policy to be reachable FROM INSIDE the app, not
            // only from the listing. publishing/config.yaml already declares both URLs (:52 license,
            // :65 privacy) and both pages return 200, but nothing in the client linked to either -
            // `git grep echoes-of-elarion.vercel.app -- Assets/*` returned zero hits.
            // ASCII-only labels (non-ASCII renders as tofu in TMP), and the two sit side by side on
            // the same 120 px rung every other row uses, so touch targets stay at the mobile floor.
            y = Caption(body, "Legal", y);
            ElarionUiKit.BuildObsidianButton(body, "Privacy Policy",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y), OnPrivacyClicked);
            ElarionUiKit.BuildObsidianButton(body, "Terms of Service",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.52f, y - Frac(120f)), new Vector2(0.94f, y), OnTermsClicked);
            y -= Frac(120f);

            // -- Ad privacy (2026-08-20) --------------------------------------
            // CONSENT THAT CANNOT BE WITHDRAWN IS NOT CONSENT, so the prompt is reachable here for
            // ever, not just once at first run. Two separate controls because they are two separate
            // rights: "Ad Privacy" re-asks the personalised-ads question, and the CCPA opt-out is a
            // standing instruction that is NOT cleared by re-answering the first one.
            // The label carries the state, not a colour (the owner is red/green colourblind).
            y = Caption(body, "Ad Privacy", y);
            ElarionUiKit.BuildObsidianButton(body, "Ad Privacy Choices",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y), OnAdPrivacyClicked);
            _doNotSellButton = ElarionUiKit.BuildObsidianButton(body, DoNotSellLabel(),
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.52f, y - Frac(120f)), new Vector2(0.94f, y), OnDoNotSellClicked);
            y -= Frac(120f);

            // -- Offline (PROD-010, 2026-08-19) -------------------------------
            // The opt-in door for offline mode. It lives in Settings rather than firing at boot
            // because this is an ~88 MB download: a prompt that ambushes a new player during the
            // opening minutes is the wrong trade, and the owner's spec is "opt IN", not "opt out".
            // The button's LABEL carries the state, not a colour - the owner is red/green
            // colourblind, so "Downloaded" vs "Download for Offline" must read in greyscale.
            // WebGL already runs from the web-hosted Addressables catalog and browser-managed
            // cache. Offering an app-style offline pull there is misleading and can strand the
            // player on a prompt asking an online web instance to get online before downloading.
#if !UNITY_WEBGL
            y = Caption(body, "Offline", y);
            ElarionUiKit.BuildObsidianButton(body,
                DeNelle.Core.OfflineContentService.PulledForThisBuild ? "Offline Ready" : "Play Offline",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y), OnOfflineClicked);
            y -= Frac(120f);
#endif

            // ── Developer (owner ruling 2026-08-08) ──────────────────────────
            // "remove the dev flag on the left side, and let's hide the dev panel ... let's stick it
            // under settings." The on-screen DEV chips are gone (ff.devresourcetool defaults OFF
            // everywhere now) and this is the replacement door, so access survives without anything
            // sitting in shot during a capture.
            //
            // GATED ON IsRegistered, NOT on a build symbol. DeNelle.DevTools is compiled out of
            // release builds, so in a store APK nothing registers PanelId.DevPanel and this section
            // never renders - no dead button, and no #if in a settings screen. Settings still
            // references Core only; it never learns that DevTools exists.
            if (PanelRouter.IsRegistered(PanelId.DevPanel))
            {
                y = Caption(body, "Developer", y);
                ElarionUiKit.BuildObsidianButton(body, "Dev Panel",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.06f, y - Frac(120f)), new Vector2(0.48f, y), OnDevPanelClicked);
                y -= Frac(120f);
            }

            BuildSelectorButtons();
            _modal.canvas.SetActive(false);   // built hidden; Open shows it
        }

        // Rebuilt whole so the active selection re-colors (Yellow = active).
        private void BuildSelectorButtons()
        {
            for (int i = _qualityRow.childCount - 1; i >= 0; i--)
                Destroy(_qualityRow.GetChild(i).gameObject);
            for (int i = _difficultyRow.childCount - 1; i >= 0; i--)
                Destroy(_difficultyRow.GetChild(i).gameObject);

            for (int i = 0; i < Tiers.Length; i++)
            {
                QualityTier captured = Tiers[i];
                bool selected = SettingsModel.Quality == captured;
                float x0 = 0.005f + i / 3f, x1 = x0 + 1f / 3f - 0.01f;
                // FRESH-CAPTURE FIX (2026-07-06): "Desktop (60 FPS)" truncated to
                // "(Desktop (60 FP…" on the chip — shorten the copy (drop the parens:
                // "Desktop 60 FPS") and fit the label to one line so it can never clip.
                string tierText = SettingsModel.TierLabel(captured)
                    .Replace("(", "").Replace(")", "").Replace("  ", " ").Trim();
                var b = ElarionUiKit.BuildObsidianButton(_qualityRow, tierText,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f),
                    () => OnQualityTierClicked(captured));
                FitChipLabel(b, tierText);
                if (selected) InkButtonLabel(b);
            }
            for (int i = 0; i < Difficulties.Length; i++)
            {
                Difficulty captured = Difficulties[i];
                bool selected = SettingsModel.Difficulty == captured;
                float x0 = 0.005f + i / 3f, x1 = x0 + 1f / 3f - 0.01f;
                var b = ElarionUiKit.BuildObsidianButton(_difficultyRow, DifficultyTuning.Label(captured),
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f),
                    () => OnDifficultyClicked(captured));
                FitChipLabel(b, null);
                if (selected) InkButtonLabel(b);
            }
        }

        // SWEEP 9413 R2 (#2): the selected (gold) chip rendered GOLD text on the GOLD face —
        // near invisible (luminance law). The kit's constructed mode inks Yellow labels, but the
        // PREFAB mode keeps the prefab's gold label color, so force dark Ink on the selected
        // chip's label wherever the build mode put it (children, else the prefab root).
        // FRESH-CAPTURE FIX (2026-07-06): selector-chip labels clipped ("(Desktop (60 FP…").
        // Resolve the chip's label wherever the build mode put it (constructed child / prefab
        // root), optionally stamp the shortened copy over the prefab's own, then bound it to
        // one auto-sized line (kit FitSingleLine) so chip text can never clip again.
        private static void FitChipLabel(Button b, string text)
        {
            if (b == null) return;
            var lbl = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl == null && b.transform.parent != null)
                lbl = b.transform.parent.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl == null) return;
            if (!string.IsNullOrEmpty(text)) lbl.text = text;
            ElarionUiKit.FitSingleLine(lbl);
        }

        private static void InkButtonLabel(Button b)
        {
            if (b == null) return;
            var labels = b.GetComponentsInChildren<TMPro.TMP_Text>(true);
            if ((labels == null || labels.Length == 0) && b.transform.parent != null)
                labels = b.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>(true);
            if (labels == null) return;
            foreach (var t in labels) t.color = ElarionUi.Ink;
        }

        // =====================================================================
        //  Refresh — pull every persisted value into the controls
        // =====================================================================

        private void RefreshFromModel()
        {
            _suppressCallbacks = true;
            try
            {
                if (_masterSlider != null) _masterSlider.value = SettingsModel.MasterVolume;
                if (_musicSlider != null) _musicSlider.value = SettingsModel.MusicVolume;
                if (_sfxSlider != null) _sfxSlider.value = SettingsModel.SfxVolume;
                if (_muteToggle != null) _muteToggle.isOn = SettingsModel.Muted;
                if (_musicToggle != null) _musicToggle.isOn = MusicOn;
                if (_shakeToggle != null) _shakeToggle.isOn = SettingsModel.ScreenShake;

                UpdateVolumeLabels();
                BuildSelectorButtons();
                if (_difficultyBlurb != null)
                    _difficultyBlurb.text = DifficultyTuning.Blurb(SettingsModel.Difficulty);
                if (_audioSeam != null)
                    _audioSeam.gameObject.SetActive(!AudioMixerBridge.HasMixer);
                RefreshWalletControls();
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }

        private void OnWalletConnectionChanged(bool connected, string shortAddress)
        {
            RefreshWalletControls();
        }

        private void RefreshWalletControls()
        {
            bool connected = CurrencySkinResolver.IsWalletConnected;
            if (_walletConnectButton != null) _walletConnectButton.interactable = !connected;
            if (_walletDisconnectButton != null)
            {
                _walletDisconnectButton.interactable = connected;
                var label = _walletDisconnectButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    string address = CurrencySkinResolver.ConnectedWalletShortAddress;
                    label.text = connected && !string.IsNullOrEmpty(address)
                        ? "Disconnect " + address
                        : "Disconnect Wallet";
                }
            }
        }

        private void UpdateVolumeLabels()
        {
            if (_masterValue != null && _masterSlider != null) _masterValue.text = FormatPercent(_masterSlider.value);
            if (_musicValue != null && _musicSlider != null) _musicValue.text = FormatPercent(_musicSlider.value);
            if (_sfxValue != null && _sfxSlider != null) _sfxValue.text = FormatPercent(_sfxSlider.value);
        }

        // =====================================================================
        //  Control callbacks — each persists + applies through SettingsModel
        // =====================================================================

        private void OnMasterChanged(float v)
        {
            if (_suppressCallbacks) return;
            SettingsModel.MasterVolume = v;
            SettingsModel.ApplyAudio();
            UpdateVolumeLabels();
        }

        private void OnMusicChanged(float v)
        {
            if (_suppressCallbacks) return;
            SettingsModel.MusicVolume = v;
            SettingsModel.ApplyAudio();
            UpdateVolumeLabels();
        }

        private void OnSfxChanged(float v)
        {
            if (_suppressCallbacks) return;
            SettingsModel.SfxVolume = v;
            SettingsModel.ApplyAudio();
            UpdateVolumeLabels();
        }

        private void OnMuteChanged(bool on)
        {
            if (_suppressCallbacks) return;
            SettingsModel.Muted = on;
            SettingsModel.ApplyAudio();
            // Keep the Music On/Off toggle honest — muting everything makes music off.
            if (_musicToggle != null) { _suppressCallbacks = true; _musicToggle.isOn = MusicOn; _suppressCallbacks = false; }
        }

        /// <summary>Music is audible only when not master-muted AND its volume is up
        /// (mirrors the retired HUD overlay's MusicOn definition).</summary>
        private static bool MusicOn => !SettingsModel.Muted && SettingsModel.MusicVolume > 0.01f;

        /// <summary>
        /// Music On/Off — the affordance moved here from the HUD overlay. On restores
        /// audible music (clears master mute + raises the music volume to its default
        /// if it was zeroed); Off zeroes only the music volume (SFX untouched). Drives
        /// the live AudioService through AudioServiceBridge so it is heard immediately,
        /// exactly as the old HUD button did, and persists via SettingsModel.
        /// </summary>
        private void OnMusicOnOffChanged(bool on)
        {
            if (_suppressCallbacks) return;
            if (on)
            {
                SettingsModel.Muted = false;
                if (SettingsModel.MusicVolume < 0.01f)
                    SettingsModel.MusicVolume = SettingsModel.DefaultMusicVolume;
                SettingsModel.ApplyAll();
                AudioServiceBridge.SetMuted(false);
                AudioServiceBridge.SetMusicVolume(SettingsModel.MusicVolume);
            }
            else
            {
                SettingsModel.MusicVolume = 0f;
                SettingsModel.ApplyAll();
                AudioServiceBridge.SetMusicVolume(0f);
            }
            // Reflect the change onto the music slider + mute toggle without re-firing.
            RefreshFromModel();
        }

        private void OnShakeChanged(bool on)
        {
            if (_suppressCallbacks) return;
            SettingsModel.ScreenShake = on;
            SettingsModel.ApplyScreenShake();
        }

        private void OnQualityTierClicked(QualityTier tier)
        {
            SettingsModel.Quality = tier;
            SettingsModel.ApplyQuality();
            BuildSelectorButtons();
        }

        /// <summary>Persists the difficulty (the WaveManager reads it at the next
        /// countdown — no extra apply step needed) and re-highlights the row.</summary>
        private void OnDifficultyClicked(Difficulty difficulty)
        {
            SettingsModel.Difficulty = difficulty;
            BuildSelectorButtons();
            if (_difficultyBlurb != null)
                _difficultyBlurb.text = DifficultyTuning.Blurb(difficulty);
        }

        private void OnResetClicked()
        {
            SettingsModel.ResetToDefaults();
            RefreshFromModel();
        }

        /// <summary>WO-588: closes Settings first so the modal arbiter swaps cleanly.</summary>
        private void OnGameGuideClicked()
        {
            Close();
            PanelRouter.Open(PanelId.GameGuide);
        }

        /// <summary>WO-1026: the town door onto the Defence Report. Closes Settings first so the
        /// modal arbiter swaps cleanly (the Game Guide route's rule, same reason).
        /// If nothing registered the panel the route returns false and we say so rather than
        /// leaving the player tapping a dead button in silence.</summary>
        private void OnDefenseReportsClicked()
        {
            Close();
            if (!PanelRouter.Open(PanelId.DefenseReport))
                FlowTrace.Warn("Siege",
                    "Settings: PanelRouter.Open(PanelId.DefenseReport) returned FALSE - " +
                    "DefenseReportPanelBootstrap did not register the opener.");
        }

        // Store listing declares these two (publishing/config.yaml:52 license_url, :65
        // privacy_policy_url) and both return 200. They live here as consts so the app and the
        // listing cannot drift apart silently - if one moves, this is the single other place to change.
        // NOTE the domain differs from the backend on purpose: the API lives on
        // defenders-of-the-realm-v2.vercel.app, the public pages on echoes-of-elarion.vercel.app.
        private const string PrivacyUrl = "https://echoes-of-elarion.vercel.app/privacy";
        private const string TermsUrl   = "https://echoes-of-elarion.vercel.app/terms";

        /// <summary>Opens the privacy policy in the device browser. Settings stays OPEN, unlike the
        /// Game Guide route: this leaves the app entirely, and the player should land back on the
        /// screen they left rather than on the town.</summary>
        private void OnPrivacyClicked() => OpenExternal(PrivacyUrl, "privacy");

        /// <summary>Re-ask the personalised-ads question. Clears only the GDPR answer — the CCPA
        /// opt-out is a standing instruction and survives (AdConsentService.ResetGdprForReprompt).</summary>
        private void OnAdPrivacyClicked()
        {
            DeNelle.Core.Monetization.AdConsentService.ResetGdprForReprompt();
            DeNelle.Core.UI.AdConsentPanel.Show();
        }

        /// <summary>The button that carries the CCPA opt-out state in its LABEL (never by colour —
        /// the owner is red/green colourblind, so the words have to do the work).</summary>
        private Button _doNotSellButton;

        private static string DoNotSellLabel() =>
            DeNelle.Core.Monetization.AdConsentService.CcpaOptOut ? "Do Not Sell: ON" : "Do Not Sell: OFF";

        /// <summary>Toggle the CCPA "do not sell or share" opt-out. Takes effect on the next SDK
        /// init; the value is persisted immediately so it cannot be lost by a crash before then.
        /// The label is updated IN PLACE rather than rebuilding the screen — a settings panel that
        /// reflows under the player's thumb is how a mis-tap happens.</summary>
        private void OnDoNotSellClicked()
        {
            bool next = !DeNelle.Core.Monetization.AdConsentService.CcpaOptOut;
            DeNelle.Core.Monetization.AdConsentService.SetCcpaOptOut(next);

            if (_doNotSellButton != null)
            {
                var label = _doNotSellButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = DoNotSellLabel();
            }
        }

        /// <summary>PROD-010: open the offline opt-in prompt. Show() is a no-op when the
        /// content is already pulled for this build, so a second tap cannot re-download.</summary>
        private void OnOfflineClicked() => DeNelle.Core.UI.OfflineOptInPanel.Show();

        /// <summary>Opens the terms of service in the device browser.</summary>
        private void OnTermsClicked() => OpenExternal(TermsUrl, "terms");

        private void OpenExternal(string url, string what)
        {
            // Guarded: on a platform where OpenURL throws or is a no-op the player gets nothing
            // visible, so the failure must at least be a logged line and never a silent dead button.
            Guard.Try("Settings", $"open {what} url", () =>
            {
                FlowTrace.Step("Settings", $"opening {what} policy -> {url}");
                Application.OpenURL(url);
            });
        }

        /// <summary>Opens the developer console. Mirrors the Game Guide route exactly - close
        /// Settings first so the single-modal arbiter is not asked to hold two panels open.</summary>
        private void OnDevPanelClicked()
        {
            Close();
            PanelRouter.Open(PanelId.DevPanel);
        }

        // =====================================================================
        //  Composed uGUI controls (Blink-skinned)
        // =====================================================================

        /// <summary>
        /// Post-scale canvas-local px height of the body zone. Replicates the kit's
        /// CanvasScaler math (the F8-5 lesson: a canvas's LIVE rect on its creation frame
        /// reads raw screen px - the scaler has not applied yet), then multiplies the
        /// y-anchor spans from the body zone up to the canvas root (kit chrome is
        /// fraction-anchored with zero offsets, so anchor spans ARE the geometry).
        /// </summary>
        private static float BodyLocalHeight(GameObject canvasRoot, Transform body)
        {
            const float fallbackH = 1920f;   // kit portrait reference height
            float canvasH = fallbackH;
            var scaler = canvasRoot != null ? canvasRoot.GetComponent<CanvasScaler>() : null;
            float sw = Screen.width, sh = Screen.height;
            if (scaler != null && sw > 1f && sh > 1f
                && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
            {
                float refW = Mathf.Max(1f, scaler.referenceResolution.x);
                float refH = Mathf.Max(1f, scaler.referenceResolution.y);
                float scale = Mathf.Pow(2f, Mathf.Lerp(
                    Mathf.Log(sw / refW, 2f), Mathf.Log(sh / refH, 2f),
                    scaler.matchWidthOrHeight));
                if (scale > 0.01f) canvasH = sh / scale;
            }
            float frac = 1f;
            var rt = body as RectTransform;
            while (rt != null && canvasRoot != null && rt.gameObject != canvasRoot)
            {
                frac *= Mathf.Clamp(rt.anchorMax.y - rt.anchorMin.y, 0.0001f, 1f);
                rt = rt.parent as RectTransform;
            }
            return canvasH * frac;
        }

        /// <summary>
        /// Vertical scroller over the body zone (RumorBoard WO-795 pattern): masked viewport
        /// with a transparent drag-catcher, top-anchored fixed-px content the px-ladder rows
        /// band against. Content taller than the viewport scrolls (short landscape bodies);
        /// content equal to the viewport (portrait - the ladder fits) cannot move (Clamped).
        /// </summary>
        private static Transform BuildScrollHost(Transform bodyZone, float contentPx)
        {
            var viewportGo = new GameObject("SettingsScroll",
                typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(bodyZone, false);
            var vpr = (RectTransform)viewportGo.transform;
            vpr.anchorMin = Vector2.zero; vpr.anchorMax = Vector2.one;
            vpr.offsetMin = Vector2.zero; vpr.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var cr = (RectTransform)contentGo.transform;
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, contentPx);

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            return cr;
        }

        /// <summary>Section caption; returns the next row's top y.</summary>
        private float Caption(Transform body, string text, float y)
        {
            // WO-714 W8 (P7 font floor): 15 -> 34 (above FontFloor 30; section headers
            // lead the ladder). MakeText auto-fits, so long captions ellipsize, never clip.
            // AUDIT FIX (2026-07-30): 46 px rung - the header owns its own full-width slim
            // band; button rows are floor-proof px rungs, so nothing inflates over it.
            MakeText(body, text, 34, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.05f, y - Frac(46f)), new Vector2(0.95f, y));
            return y - Frac(54f);
        }

        /// <summary>Label + Blink-skinned slider + % value, one row. Advances y.</summary>
        private (Slider, TextMeshProUGUI) SliderRow(Transform body, string label, ref float y,
            Action<float> onChanged)
        {
            float top = y, bottom = y - Frac(58f);   // AUDIT FIX 2026-07-30: px rung
            // WO-714 W8 (P7): 13 -> 30 (FontFloor).
            MakeText(body, label, 30, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.06f, bottom), new Vector2(0.24f, top));

            var host = ZoneRect(body, "Slider_" + label, new Vector2(0.26f, bottom + Frac(12f)), new Vector2(0.82f, top - Frac(12f)));
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(host, false);
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            // Track (Blink bar sprite when mirrored; dark bar fallback).
            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(sliderGo.transform, false);
            var trt = trackGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.3f); trt.anchorMax = new Vector2(1f, 0.7f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var trackImg = trackGo.GetComponent<Image>();
            var barSprite = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, "panel_bar");
            if (barSprite != null) { trackImg.sprite = barSprite; trackImg.type = Image.Type.Sliced; }
            else trackImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Fill.
            var fillAreaGo = new GameObject("FillArea", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fart = fillAreaGo.GetComponent<RectTransform>();
            fart.anchorMin = new Vector2(0f, 0.32f); fart.anchorMax = new Vector2(1f, 0.68f);
            fart.offsetMin = Vector2.zero; fart.offsetMax = Vector2.zero;
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            // Pin the fill rect to its area. The Slider drives only the fill's ANCHORS on value
            // change; it never resets offsets/sizeDelta, so an uninitialised RectTransform (default
            // sizeDelta) inflated the gold fill into a giant block over the whole audio section
            // (#20). Zeroing offsets + sizeDelta keeps the fill hugging its anchor rect.
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            fillRt.sizeDelta = Vector2.zero; fillRt.pivot = new Vector2(0.5f, 0.5f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = ElarionUi.Gold;

            // Handle.
            var handleAreaGo = new GameObject("HandleArea", typeof(RectTransform));
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var hart = handleAreaGo.GetComponent<RectTransform>();
            hart.anchorMin = new Vector2(0f, 0f); hart.anchorMax = new Vector2(1f, 1f);
            hart.offsetMin = Vector2.zero; hart.offsetMax = Vector2.zero;
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var hrt = handleGo.GetComponent<RectTransform>();
            // Full-height thumb that the Slider slides horizontally (anchors x are value-driven);
            // pin the y-anchors + width so it can't inflate like the fill did (#20).
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(22f, 0f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = ElarionUi.Gilt;

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.handleRect = hrt;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = SettingsModel.MaxVolume;
            slider.onValueChanged.AddListener(v => onChanged(v));

            // WO-714 W8 (P7): 12 -> 30 (FontFloor); the widened modal seats "100%" at floor size.
            var valueLabel = MakeText(body, "100%", 30, ElarionUi.ParchmentDim, FontStyles.Normal,
                TextAlignmentOptions.Right, new Vector2(0.84f, bottom), new Vector2(0.94f, top));

            y = bottom - Frac(10f);
            return (slider, valueLabel);
        }

        /// <summary>Label + uGUI Toggle (gold check), one row. Advances y.</summary>
        private Toggle ToggleRow(Transform body, string label, ref float y, Action<bool> onChanged)
        {
            // SWEEP 9413 R2 (#2): row raised 0.045 → 0.055 and the toggle box is now a FIXED
            // pixel square (below) — the fraction-stretched box collapsed to a sliver on the
            // capture aspect once the plate/outline landed.
            float top = y, bottom = y - Frac(64f);   // AUDIT FIX 2026-07-30: px rung
            // WO-714 W8 (P7): 13 -> 30 (FontFloor).
            MakeText(body, label, 30, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.06f, bottom), new Vector2(0.70f, top));

            // FRESH-CAPTURE FIX (2026-07-06, colorblind law): the toggle's state was carried
            // by the gold check ALONE (color/shape only) and the box read as an anonymous
            // square far from its label. An explicit "On"/"Off" state TEXT sits beside the
            // box — never color-alone — and updates with every value change.
            var stateLbl = MakeText(body, "Off", 30, ElarionUi.Parchment, FontStyles.Bold,   // P7: 12 -> 30
                TextAlignmentOptions.Right, new Vector2(0.71f, bottom), new Vector2(0.84f, top));

            var host = ZoneRect(body, "Toggle_" + label, new Vector2(0.86f, bottom + Frac(5f)), new Vector2(0.94f, top - Frac(5f)));
            var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            toggleGo.transform.SetParent(host, false);
            var trt = toggleGo.GetComponent<RectTransform>();
            // Fixed 44x44 square centered in the host zone — never height-collapses with the row.
            trt.anchorMin = new Vector2(0.5f, 0.5f); trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(44f, 44f);

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(toggleGo.transform, false);
            var brt = boxGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var boxImg = boxGo.GetComponent<Image>();
            // EYES-SWEEP 2026-07-06 (#2): the box was pure black-on-black — an OFF toggle (the
            // check graphic only renders when isOn) had NO visible control at all ("Mute all audio"
            // read as label-only while the ON "Screen shake" showed its gold check). The empty box
            // must always render: kit bar plate when the mirrored art is present, and a gilt outline
            // in every case so null art never blanks the control.
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, "panel_bar");
            if (plate != null) { boxImg.sprite = plate; boxImg.type = Image.Type.Sliced; boxImg.color = Color.white; }
            else boxImg.color = new Color(0.10f, 0.09f, 0.07f, 0.9f);
            var boxEdge = boxGo.AddComponent<Outline>();
            boxEdge.effectColor = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.8f);
            boxEdge.effectDistance = new Vector2(1.5f, -1.5f);

            var checkGo = new GameObject("Check", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(boxGo.transform, false);
            var crt = checkGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.18f, 0.18f); crt.anchorMax = new Vector2(0.82f, 0.82f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var checkImg = checkGo.GetComponent<Image>();
            checkImg.color = ElarionUi.Gold;

            var toggle = toggleGo.GetComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            // State text updates OUTSIDE the suppress guard (RefreshFromModel's isOn writes
            // must still repaint "On"/"Off" even while callbacks are suppressed).
            toggle.onValueChanged.AddListener(v =>
            {
                if (stateLbl != null) stateLbl.text = v ? "On" : "Off";
                onChanged(v);
            });

            y = bottom - Frac(12f);
            return toggle;
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp(value, 0f, SettingsModel.MaxVolume) * 100f)}%";
        }

        private static Transform ZoneRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max,
            bool multiline = false)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            ElarionUiKit.EnsureFont(t);
            // WO-714 W8 (P7 mobile font floor): every label routes through the kit fit
            // helpers — bounded auto-size down to the factory floor, then ellipsis
            // (single-line) / truncate (block). Never sub-legible, never clipping.
            if (multiline) ElarionUiKit.FitBlock(t);
            else ElarionUiKit.FitSingleLine(t);
            return t;
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — the settings screen is fully code-built now.
//   1. Add SettingsController to any GameObject (no UIDocument needed). The
//      kit modal builds lazily on first Open() at sortingOrder 32000.
//   2. AudioMixer: assign the field (or place the asset at Resources/Audio/
//      GameAudioMixer). Until then the sliders persist and the seam notice shows.
//   3. PauseController holds a serialized reference and calls Open(); this
//      controller raises SettingsClosed on Back/Close so pause can re-show.
//   4. SettingsModel is static — no state is lost across scene loads.
// =============================================================================
