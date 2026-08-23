using System;
using System.Collections;
using DeNelle.Core;
using DeNelle.Core.Ads;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeNelle.Village.Monetization
{
    /// <summary>Once-per-UTC-day retention reward shown only after onboarding.</summary>
    public sealed class DailyChestController : MonoBehaviour
    {
        public const string PlacementId = "place.daily.chest";
        private const int BaseGold = 500;

        private static DailyChestController s_instance;
        private static bool s_tutorialJustFinished;

        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _handle;
        private TMP_Text _status;
        private Button _doubleButton;
        private Transform _well;
        private AdFace _adFace = AdFace.Unknown;
        private bool _offeredThisSession;
        private bool _claiming;

        // -- canon-strings keys (WO-1051 section 4). No player-facing sentence is typed inline. --
        private const string KeyTitle          = "chestTitle";
        private const string KeyHeadline       = "chestHeadline";
        private const string KeyBody           = "chestBody";
        private const string KeyStatusFree     = "chestStatusFree";
        private const string KeyClaimFree      = "chestClaimFree";
        private const string KeyClaimDouble    = "chestClaimDouble";
        private const string KeyAdNotReady     = "chestAdNotReady";
        private const string KeyAdOpening      = "chestAdOpening";
        private const string KeyAdUnavailable  = "chestAdUnavailable";
        private const string KeyAdNoReward     = "chestAdNoReward";
        private const string KeyLedgerLoading  = "chestLedgerLoading";

        /// <summary>The ad CTA's three player-readable states. The WORD carries the state
        /// (owner is red/green colourblind - hue may never be the only signal, CLAUDE.md);
        /// the face colour is a second, redundant channel.</summary>
        private enum AdFace { Unknown, Ready, NotReady, Opening }

        // -- Layout, in fractions of the frame's BODY ZONE (never of the whole panel) --
        // WO-1051: the CTAs used to be authored on chrome.content at panel y 0.10-0.28,
        // which intersects the shared Close (DefaultCloseZone y 0.050-0.125 as a rect, and
        // in truth a FIXED CanonCtaHeight box that grows UP from 0.050 - on this panel it
        // tops out near panel y 0.22). Parenting to layout.body instead inherits the
        // factory's close-band reservation (ElarionUiKit.BuildObsidianPanel, WO-714 P6),
        // so nothing here can geometrically reach the Close, at any panel size.
        //
        // THE CTA BAND IS SIZED FROM THE TOUCH FLOOR, NOT BY EYE. Panel height is 0.84 of the
        // canvas; the kit's reservation then leaves a body well of ~0.5845 panel height, so the
        // 0.025-0.280 band resolves to ~135 reference px at 16:9 (530 px well) and ~117 px at
        // 20:9 (460 px well). Both clear MinTouchPx (112), which means ClampMinTouch is a NO-OP
        // here - and a no-op clamp is the point: an inflating clamp is exactly what pushed the
        // FrameRaid Deploy row down into the shared Close (ElarionUiKit ~line 445). The panel
        // was made TALLER rather than the buttons made shorter, per that same note.
        private static readonly Vector2 ClaimMin  = new Vector2(0.015f, 0.025f);
        private static readonly Vector2 ClaimMax  = new Vector2(0.485f, 0.280f);
        private static readonly Vector2 AdMin     = new Vector2(0.515f, 0.025f);
        private static readonly Vector2 AdMax     = new Vector2(0.985f, 0.280f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("DailyChestController");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DailyChestController>();
        }

        public static void NotifyTutorialFinished()
        {
            s_tutorialJustFinished = true;
            if (s_instance != null) s_instance.StartCoroutine(s_instance.OfferAfterDelay());
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(OfferAfterDelay());
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _offeredThisSession = false;
            StartCoroutine(OfferAfterDelay());
        }

        private IEnumerator OfferAfterDelay()
        {
            yield return new WaitForSecondsRealtime(s_tutorialJustFinished ? 0.75f : 1.25f);
            s_tutorialJustFinished = false;
            TryOffer();
        }

        private void Update()
        {
            if (_modal == null || _well == null) return;
            if (_claiming) { SetAdFace(AdFace.Opening); return; }
            bool ready = FeatureFlags.RewardedAdSkip && AdServices.Current.IsRewardedReadyFor(PlacementId);
            SetAdFace(ready ? AdFace.Ready : AdFace.NotReady);
            if (!ready) AdServices.Current.PreloadRewarded(PlacementId);
        }

        /// <summary>Repaint AND RELABEL the optional-ad CTA for its current state (WO-1051 defect 5).
        /// The old build made it once as a Gray face and only ever flipped .interactable, so a READY
        /// ad button looked exactly like a dead one. Three states, each carrying its own WORD.</summary>
        private void SetAdFace(AdFace face)
        {
            // Compare on the STATE only, never on "is the button object still there": if the kit
            // ever handed back null, a "rebuild while null" guard would rebuild every single frame.
            if (face == _adFace) return;
            _adFace = face;
            if (_well == null) return;

            if (_doubleButton != null) { Destroy(_doubleButton.gameObject); _doubleButton = null; }

            string label;
            ElarionUiKit.ObsidianButtonColor color;
            switch (face)
            {
                case AdFace.Ready:
                    label = VillageStrings.Canon(KeyClaimDouble);
                    color = ElarionUiKit.ObsidianButtonColor.Green;
                    break;
                case AdFace.Opening:
                    label = VillageStrings.Canon(KeyAdOpening);
                    color = ElarionUiKit.ObsidianButtonColor.Gray;
                    break;
                default:
                    label = VillageStrings.Canon(KeyAdNotReady);
                    color = ElarionUiKit.ObsidianButtonColor.Gray;
                    break;
            }

            _doubleButton = ElarionUiKit.BuildObsidianButton(_well, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, color, AdMin, AdMax, WatchForDouble);
            if (_doubleButton != null) _doubleButton.interactable = face == AdFace.Ready;
            FlowTrace.Step("DailyChest", "ad CTA face=" + face + " label='" + label + "'");
        }

        private void TryOffer()
        {
            if (_offeredThisSession || _modal != null || !HubScenes.IsHub(SceneManager.GetActiveScene().name)) return;
            var state = GameStateService.Instance?.State;
            if (state == null || !state.Onboarded || string.Equals(state.DailyChestDayKey, TodayKey(), StringComparison.Ordinal)) return;

            _offeredThisSession = true;
            Build();
            AdServices.Current.PreloadRewarded(PlacementId);
        }

        private void Build()
        {
            _handle = PanelManager.Register("Daily Chest", Close, IsShowing);
            // medallionIcon dropped (WO-1051 defect 4): there is no icon_chest sprite anywhere under
            // Assets/Resources, and the default (frameless) zone set declares hasMedallion = false -
            // the id resolved to nothing and had nowhere to render even if it had.
            _modal = ElarionUiKit.BuildObsidianModal("DailyChestUI", VillageStrings.Canon(KeyTitle),
                new Vector2(0.155f, 0.08f), new Vector2(0.845f, 0.92f), Close, 31010);

            _well = ResolveWell();
            if (_well == null) { Close(); return; }

            // Type sizes are named off the shared ladder, never typed as per-screen literals
            // (ElarionUi.cs:105-121), and every block is fit-guarded so a longer sentence
            // reflows down to the mobile floor instead of spilling onto the ornate border.
            // x is 0.02-0.98 of the BODY WELL, so nothing takes ElarionUiKit.Label's 0.03/0.97
            // panel-wide defaults, which is how both old labels overhung the well by 3% a side.
            var headline = ElarionUiKit.Label(_well, VillageStrings.Canon(KeyHeadline),
                0.855f, 0.995f, ElarionUi.Parchment, ElarionUi.FontHead,
                TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            ElarionUiKit.FitBlock(headline);

            var body = ElarionUiKit.Label(_well, VillageStrings.Canon(KeyBody),
                0.465f, 0.825f, ElarionUi.Parchment, ElarionUi.FontBody,
                TextAlignmentOptions.Top, 0.02f, 0.98f);
            ElarionUiKit.FitBlock(body);

            _status = ElarionUiKit.Label(_well, VillageStrings.Canon(KeyStatusFree),
                0.325f, 0.425f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.02f, 0.98f);
            ElarionUiKit.FitBlock(_status);

            // PRIMARY: the free path. Gold face, left seat - it must never read as the lesser option.
            ElarionUiKit.BuildObsidianButton(_well, VillageStrings.Canon(KeyClaimFree),
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                ClaimMin, ClaimMax, () => Claim(BaseGold, "free"));

            // SECONDARY: the optional ad. Built through SetAdFace so it can never be born wearing
            // a state it is not in.
            _adFace = AdFace.Unknown;
            SetAdFace(FeatureFlags.RewardedAdSkip && AdServices.Current.IsRewardedReadyFor(PlacementId)
                ? AdFace.Ready : AdFace.NotReady);

            if (!PanelManager.NotifyOpened(_handle)) Close();
        }

        /// <summary>The content parent: the frame's BODY ZONE, which the kit factory has already
        /// raised clear of the shared Close band. Every other panel takes this (CosmeticShopPanel,
        /// PauseController, SettingsController, DialogueView, ClanChatPanel, ...); this screen was
        /// the one that grabbed chrome.content directly and then authored raw panel fractions on
        /// top of the Close. If the layout is somehow absent we do NOT fall back to the raw content
        /// rect - that IS the defect - we build an equivalent reserved well instead.</summary>
        private Transform ResolveWell()
        {
            var chrome = _modal != null ? _modal.chrome : null;
            if (chrome == null || chrome.content == null)
            {
                FlowTrace.Fail("DailyChest", "modal chrome/content missing - the chest cannot be built.");
                return null;
            }

            var layout = chrome.layout;
            if (layout != null && layout.body != null)
            {
                FlowTrace.Step("DailyChest", "content parented to layout.body (close band reserved by the kit)");
                return layout.body;
            }

            FlowTrace.Warn("DailyChest", "chrome.layout.body absent - building a local reserved well " +
                                         "so the CTAs still cannot reach the shared Close.");
            var go = new GameObject("Zone_BodyFallback", typeof(RectTransform));
            go.transform.SetParent(chrome.content.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.06f, 0.35f);   // floor clears the CanonCtaHeight close box
            rt.anchorMax = new Vector2(0.94f, 0.875f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();                     // never occlude the earlier-built Close
            return rt;
        }

        private void WatchForDouble()
        {
            if (_claiming) return;
            if (!FeatureFlags.RewardedAdSkip || !AdServices.Current.IsRewardedReadyFor(PlacementId))
            {
                SetStatus(VillageStrings.Canon(KeyAdUnavailable));
                AdServices.Current.PreloadRewarded(PlacementId);
                return;
            }

            _claiming = true;
            SetStatus(VillageStrings.Canon(KeyAdOpening));
            AdServices.Current.ShowRewarded(PlacementId, result =>
            {
                _claiming = false;
                if (result.Rewarded) Claim(BaseGold * 2, "rewarded_double");
                else SetStatus(VillageStrings.Canon(KeyAdNoReward));
            });
        }

        private void Claim(int gold, string path)
        {
            if (_claiming) return;
            var service = GameStateService.Instance;
            var state = service?.State;
            if (state == null || string.Equals(state.DailyChestDayKey, TodayKey(), StringComparison.Ordinal))
            {
                Close();
                return;
            }

            if (EconomyService.Instance == null)
            {
                SetStatus(VillageStrings.Canon(KeyLedgerLoading));
                return;
            }

            _claiming = true;
            state.DailyChestDayKey = TodayKey();
            EconomyService.Instance.AddCoins(gold);
            service.Save();
            FlowTrace.Step("DailyChest", $"claimed +{gold} Gold path={path} day={state.DailyChestDayKey}");
            Close();
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        private bool IsShowing() => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy;

        private void Close()
        {
            PanelManager.NotifyClosed(_handle);
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
            _modal = null;
            _status = null;
            _doubleButton = null;
            _well = null;
            _adFace = AdFace.Unknown;
        }

        // WO-1134 — REPOINTED at the one Core definition (DeNelle.Core.UtcDay). This used to
        // be its own `DateTime.UtcNow.ToString("yyyy-MM-dd")`, one of five identical private
        // copies scattered across the monetization + raid paths. Kept as a local wrapper so
        // the five call sites below read unchanged; the TRUTH now lives in exactly one file.
        private static string TodayKey() => UtcDay.Key();
    }
}
