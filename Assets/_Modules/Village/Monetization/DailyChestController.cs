using System;
using System.Collections;
using System.Globalization;
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
        private Coroutine _offerRoutine;

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
        private const string KeyClaimedToast   = "chestClaimedToast";

        // -- WO-1213 Slice A: the oracle seam. ElarionUiKit.ShowToast is a hard no-op outside
        // play (Application.isPlaying), so a suite cannot observe the card; it observes the
        // DECISION to raise one. Mirrors BankOverflowToastPresenter.ToastCount /
        // LastToastMessage exactly, so the two acknowledgement paths are asserted the same way.
        /// <summary>Player-facing claim toasts raised since the last <see cref="ResetDiagnostics"/>.</summary>
        public static int ToastCount { get; private set; }

        /// <summary>The exact text of the most recent claim toast ("" if none).</summary>
        public static string LastToastMessage { get; private set; } = string.Empty;

        /// <summary>Test/teardown seam: clears the counters so one case cannot colour the next.</summary>
        public static void ResetDiagnostics()
        {
            ToastCount = 0;
            LastToastMessage = string.Empty;
        }

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
        //
        // WO-1213 Slice B - HORIZONTAL RHYTHM ONLY. The old row was 0.015 outer margin against a
        // 0.030 gutter, so each button sat HALF as far from the panel wall as from its neighbour
        // and the pair read as jammed into the corners. The row now runs on ONE spacing unit
        // (0.050) used three times - wall, gutter, wall - so the rhythm is even end to end.
        // THE VERTICAL BAND IS UNTOUCHED at 0.025-0.280 for the reason stated above: it is what
        // makes ClampMinTouch a no-op, and buying width by shortening buttons is the exact move
        // that pushed the FrameRaid Deploy row into the shared Close. Width is the only axis that
        // paid: 0.470 -> 0.425 of the body well, still hundreds of reference px, nowhere near the
        // 112 px floor - so nothing here can trip an inflating clamp either.
        // The spacing is written ONCE and the four rects are derived from it, so wall and gutter
        // cannot drift apart again the way 0.015/0.030 did.
        private const float CtaSpacing = 0.050f;    // the ONE unit: outer margin AND gutter
        private const float CtaTop     = 0.280f;    // load-bearing band, see the note above
        private const float CtaBottom  = 0.025f;

        private static readonly Vector2 ClaimMin  = new Vector2(CtaSpacing, CtaBottom);
        private static readonly Vector2 ClaimMax  = new Vector2(0.5f - CtaSpacing * 0.5f, CtaTop);
        private static readonly Vector2 AdMin     = new Vector2(0.5f + CtaSpacing * 0.5f, CtaBottom);
        private static readonly Vector2 AdMax     = new Vector2(1f - CtaSpacing, CtaTop);

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
            if (s_instance != null) s_instance.QueueOffer();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            QueueOffer();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_offerRoutine != null) StopCoroutine(_offerRoutine);
            _offerRoutine = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _offeredThisSession = false;
            QueueOffer();
        }

        private void QueueOffer()
        {
            if (_offerRoutine != null) StopCoroutine(_offerRoutine);
            _offerRoutine = StartCoroutine(OfferWhenUiClear());
        }

        private IEnumerator OfferWhenUiClear()
        {
            yield return new WaitForSecondsRealtime(s_tutorialJustFinished ? 0.75f : 1.25f);
            s_tutorialJustFinished = false;
            int clearFrames = 0;
            while (clearFrames < 2)
            {
                clearFrames = PanelManager.AnyOpen ? 0 : clearFrames + 1;
                yield return null;
            }
            TryOffer();
            _offerRoutine = null;
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
                    FlowTrace.Step("DailyChest", "ad CTA hidden until rewarded placement is ready");
                    return;
            }

            _doubleButton = ElarionUiKit.BuildObsidianButton(_well, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, color, AdMin, AdMax, WatchForDouble);
            if (_doubleButton != null)
            {
                MedievalUiSkin.ApplyButton(_doubleButton, primary: false);
                _doubleButton.interactable = face == AdFace.Ready;
            }
            FlowTrace.Step("DailyChest", "ad CTA face=" + face + " label='" + label + "'");
        }

        private void TryOffer()
        {
            if (_offeredThisSession || _modal != null || !HubScenes.IsHub(SceneManager.GetActiveScene().name)) return;
            var state = GameStateService.Instance?.State;
            if (state == null || !state.Onboarded || string.Equals(state.DailyChestDayKey, TodayKey(), StringComparison.Ordinal)) return;

            Build();
            if (_modal != null)
            {
                _offeredThisSession = true;
                AdServices.Current.PreloadRewarded(PlacementId);
            }
        }

        private void Build()
        {
            _handle = PanelManager.Register("Daily Chest", Close, IsShowing);
            // medallionIcon dropped (WO-1051 defect 4): there is no icon_chest sprite anywhere under
            // Assets/Resources, and the default (frameless) zone set declares hasMedallion = false -
            // the id resolved to nothing and had nowhere to render even if it had.
            _modal = ElarionUiKit.BuildObsidianModal("DailyChestUI", VillageStrings.Canon(KeyTitle),
                new Vector2(0.155f, 0.08f), new Vector2(0.845f, 0.92f), Close, 31010);
            MedievalUiSkin.ApplyShell(_modal.chrome, compact: false);

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
            var free = ElarionUiKit.BuildObsidianButton(_well, VillageStrings.Canon(KeyClaimFree),
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                ClaimMin, ClaimMax, () => Claim(BaseGold, "free"));
            MedievalUiSkin.ApplyButton(free, primary: true);

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
            AcknowledgeClaim(gold, path);
            Close();
        }

        /// <summary>WO-1213 Slice A. The grant was silent: AddCoins landed and the modal simply
        /// vanished, so from the player's seat a successful rewarded double and a CANCELLED ad
        /// looked identical - which is exactly how it was reported ("cancelling to the home
        /// screen"). The FlowTrace above proves the grant to US and says nothing to THEM.
        ///
        /// Fires for BOTH paths (free and rewarded_double) - a player who takes the base reward
        /// is owed the same acknowledgement.
        ///
        /// It is raised BEFORE Close() but it does NOT depend on that ordering: the kit toast
        /// builds its own root GameObject with its own ScreenSpaceOverlay canvas
        /// (ElarionUiKitConformance.ShowToast) and is never parented to this modal, so
        /// Close()'s Destroy(_modal.canvas) cannot take it with it. Life is set past the
        /// panel teardown so the sentence survives the close animation.</summary>
        private void AcknowledgeClaim(int gold, string path)
        {
            string amount = gold.ToString("N0", CultureInfo.InvariantCulture);
            string raw = VillageStrings.Canon(KeyClaimedToast);
            string msg;
            try { msg = string.Format(CultureInfo.InvariantCulture, raw, amount); }
            catch (FormatException ex)
            {
                // No silent failures (CLAUDE.md section 12): a bad placeholder degrades to the
                // raw sentence rather than throwing out of the claim path.
                FlowTrace.Fail("DailyChest", "canon-strings key '" + KeyClaimedToast +
                                             "' has a bad format placeholder: " + ex.Message);
                msg = raw;
            }

            // ── WO-1347 (second owner tag) - THE COLLECT FLOURISH ────────────────────────
            // HER TAG, VERBATIM (Assets/Editor/VfxManualPicks.json):
            //     DailyChestCollect_Aura -> Lana Studio/Casual RPG VFX/Prefabs/
            //                               Backlight_resources/backlight_coin.prefab
            //     isLoop false, scale 1.0        her words: "daily chest collect"
            // The key is mapped VERBATIM. Nothing here picks, substitutes or rescales a
            // prefab (memory vfx-map-owner-tags-no-creative-pick) and backlight_coin.prefab
            // is NOT modified on disk - it is a shared pack asset.
            //
            // WHY IT IS RAISED IN WORLD SPACE AND NOT IN THIS MODAL: this chest is a
            // ScreenSpaceOverlay UI panel, and her tagged effect is a world-space particle
            // composite. Parented into an overlay Canvas it would render at the wrong scale
            // or depth or not at all - which looks exactly like the tag failing. CollectBurstVfx
            // seats it unparented in world space in front of the camera and time-bounds it, so
            // the Close() on the next statement cannot take it with it. See that file's header.
            //
            // ADDITIVE, exactly like the WO-1225 raise below it: the grant, the canon sentence,
            // the toast and their traces are untouched. This is a flourish, never a receipt -
            // if it fails to resolve the player still gets the toast and the counting gold chip.
            CollectBurstVfx.Raise("DailyChestCollect_Aura", "daily chest claimed path=" + path);

            ToastCount++;
            LastToastMessage = msg;
            // Tone is DECORATION only (owner is red/green colourblind, CLAUDE.md section 7) -
            // the sentence carries the whole message. Default 480x76 card holds ~2 lines.
            ElarionUiKit.ShowToast(msg, ElarionUiKit.ToastTone.Info, 3.2f, 720);
            FlowTrace.Step("DailyChest", "claim toast path=" + path + " -> '" + msg + "'");

            // WO-1225 -- ADDITIVE. Everything above is WO-1213 and is untouched: the grant, the
            // canon sentence, the toast and its trace all still run exactly as committed. The
            // toast simply is not ENOUGH: on 2026-08-26 it fired correctly and EchoUnlockDialogue
            // opened over it 3 ms later (sortingOrder 31020 + a full-screen scrim, vs the toast's
            // 720), so a provably-correct grant read to the owner as nothing happening.
            //
            // This raise adds a SECOND, un-occludable acknowledgement anchored to the persistent
            // gold chip (owner ruling: "can it show streamers and +1000 showing to gold? counting
            // up animation?"). It renders NOTHING here and passes no number to the screen: `gold`
            // travels only as the shortfall oracle. HudKitController waits for the wallet's own
            // push and animates to the MEASURED balance -- so if this grant were ever clamped or
            // refused, the player would see the true, smaller number and the log would carry a
            // SHORTFALL warn.
            RewardCelebration.Raise("Gold", gold, "daily.chest." + path);
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
