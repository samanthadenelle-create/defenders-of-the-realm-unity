// =============================================================================
// EndStateView — the ONE shared Obsidian end-state screen (WO-B, UI conformance
// audit 2026-07-02 §3.2). Victory / defeat / hero-death / wave-results all render
// through THIS view from an EndStateVM. Replaces the divergent implementations:
// BattleArenaHud.ShowVictorySummary + ShowLossPanel (retired in that file) and
// WaveCelebrationManager's IMGUI toast / prefab text (retired there).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Canon (docs/UI_BLINK_TEMPLATE_CANON.md + owner addenda 2026-07-02):
//   • Master factory only: ElarionUiKit.BuildObsidianModal / BuildObsidianPanel
//     with frameName = RpgUiCatalog.FrameCore; content DROPS into the returned
//     drop-zones (header / body / footer). No per-screen chrome.
//   • ONE way out (owner button law): a single primary kit Button in the footer.
//     The factory's shared Close chip is HIDDEN here — an end-state must not
//     offer a second, redundant exit. (Kit change reported: a `withClose:false`
//     parameter on BuildObsidianPanel would make this first-class.)
//   • Sized to content: the panel rect is computed from what the VM carries —
//     no cavernous empty space (the owner's F8 "THis looks bad" Victory modal).
//   • SMOOTH (owner directive): fade+scale in ~250ms ease-out (unscaled time),
//     spoils rows stagger-reveal ~50ms apart, the primary button lands last.
//     No pre-existing shared UI tween helper exists in the codebase (searched:
//     only ad-hoc coroutines — BattleArenaHud.PopCrown, VillageHudController.
//     FadeInHud), so the tween lives here. KIT-PROMOTION CANDIDATE: lift
//     RevealRoutine into ElarionUiKit once a second screen needs it.
//   • MVVM strict: this view binds the EndStateVM and reads NO game state.
//   • Never pauses time — the hero-death variant narrates HeroHealth's respawn
//     coroutine, which runs on scaled time.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.UI
{
    /// <summary>The shared end-state screen. Build one via <see cref="Show"/>.</summary>
    public sealed class EndStateView : MonoBehaviour
    {
        private static EndStateView _open;

        private EndStateVM _vm;
        private bool _fired;                      // primary-action latch (fires exactly once)
        private readonly List<Reveal> _reveals = new List<Reveal>();

        // HUD-2 (GAP_AUDIT P2 #14): the FULL end-state modal now joins the single-modal arbiter so
        // opening it closes any lingering panel (a shop left open at death) and the back button can
        // dismiss it. RegisterBattleAllowed — an end-state is the decision node shown AT/after a battle,
        // so the WO-437 battle-lock must never reject it (like the Battle HUD / Pause). Compact banners
        // (no scrim, non-blocking, auto-dismiss) deliberately stay OUT of the arbiter -> handle is null.
        private DeNelle.Core.UI.PanelHandle _panelHandle;

        private struct Reveal
        {
            public CanvasGroup Group;
            public RectTransform Rect;
            public float Delay;
            public float FromScale;
        }

        // ── entry point ───────────────────────────────────────────────────────

        /// <summary>Show the end-state screen for <paramref name="vm"/> (replaces any open one).</summary>
        public static EndStateView Show(EndStateVM vm,
            // F8-15 death forensic window: name WHO opened each end-state (GameOverScreen /
            // HeroDeathEndState / BattleArenaHud / WaveCelebrationManager all funnel HERE). The
            // FULL modal now routes through PanelManager (HUD-2 fixed, below); only the COMPACT
            // banner stays out of the arbiter (non-blocking).
            [System.Runtime.CompilerServices.CallerMemberName] string openerMember = null,
            [System.Runtime.CompilerServices.CallerFilePath]  string openerFile   = null)
        {
            if (vm == null) return null;
            if (DeathTrace.Active)
            {
                string opener = DeathTrace.Describe(openerMember, openerFile);
                DeathTrace.ScreenOpened("EndState '" + vm.Title + "'", opener);
                // HUD-2 FIXED: the FULL end-state modal now routes through PanelManager
                // (RegisterBattleAllowed + NotifyOpened below), so it swaps out any prior panel and
                // the arbiter can dismiss it. Only the COMPACT banner stays out of the arbiter on
                // purpose (no scrim, non-blocking, auto-dismiss) — flag just that case in the window.
                if (vm.Compact)
                    DeathTrace.ScreenBypassedArbiter("EndState '" + vm.Title + "'", opener);
                if (_open != null)
                    DeathTrace.ScreenClosed("EndState '" + (_open._vm != null ? _open._vm.Title : "?") + "'",
                        "EndStateView.Show (replaced by '" + vm.Title + "')");
            }
            if (_open != null) { Destroy(_open.gameObject); _open = null; }

            // REAL EventSystem buttons (audit §2e: GameOverScreen's manual Input hit-test
            // existed because builds lacked an EventSystem — ensure one, don't hand-roll).
            EnsureEventSystem();

            GameObject canvas;
            ElarionUiKit.PanelChrome chrome;

            if (vm.Compact)
            {
                // Wave-results banner: small top-of-screen panel, NO scrim/backdrop, non-blocking.
                canvas = ElarionUiKit.BuildModalCanvas("EndState", 31000);
                var c = canvas.GetComponent<Canvas>();
                if (c != null) c.overrideSorting = true;
                // Grown DOWN (top edge held at 0.86) to 0.30 of screen height: this is the
                // row-less SPLASH size (F8-45: a spoils damage report further extends the
                // banner downward in Bind), so it must carry enough height for the header band
                // (Bind) plus the emblem+subtitle below it — otherwise the tall title band
                // would crush them. (Was 0.64–0.86 = 0.22h, too short to seat the headline.)
                chrome = ElarionUiKit.BuildObsidianPanel(canvas.transform, vm.Title,
                    new Vector2(0.15f, 0.56f), new Vector2(0.85f, 0.86f),
                    onClose: null, withBackdrop: false, frameName: RpgUiCatalog.FrameCore,
                    medallionIcon: "crest");   // explicit: the socket seats the crest family, never blank
            }
            else
            {
                // Full end-state modal, sized to the VM's content (no cavernous empty space).
                float half = PanelHalfHeight(vm);
                var modal = ElarionUiKit.BuildObsidianModal("EndState", vm.Title,
                    new Vector2(0.22f, 0.53f - half), new Vector2(0.78f, 0.53f + half),   // WO-433: narrower victory panel (was 0.08/0.92)
                    onClose: null,   // scrim stays a pure raycast-blocker — no second way out
                    frameName: RpgUiCatalog.FrameCore,
                    medallionIcon: "crest");   // explicit: the socket seats the crest family, never blank
                canvas = modal.canvas;
                chrome = modal.chrome;
            }

            // Owner button law: an end-state has exactly ONE way out (the primary button).
            // Hide the factory's shared Close chip. KIT CHANGE REPORTED: BuildObsidianPanel
            // withClose:false would make this first-class instead of hide-after-build.
            if (chrome.close != null) chrome.close.gameObject.SetActive(false);

            var view = canvas.AddComponent<EndStateView>();
            view.Bind(vm, chrome);
            _open = view;

            // HUD-2: the FULL modal joins the single-modal arbiter (battle-allowed - it shows at/after
            // a battle, so the WO-437 lock must not reject it). NotifyOpened closes any lingering panel
            // (e.g. a shop open at death) and records this as THE open modal. Compact banners are
            // non-blocking (no scrim) and intentionally NOT registered.
            if (!vm.Compact)
            {
                view._panelHandle = PanelManager.RegisterBattleAllowed("EndState",
                    view.CloseFromArbiter, () => view != null);
                PanelManager.NotifyOpened(view._panelHandle);
            }

            // P23 (HUD_OBSIDIAN A4.6): the end-state is the DECISION NODE — while it is
            // up the posture is hostile(postbattle) and the HUD kit stands down.
            DeNelle.Core.HudModel.PostureSignals.SetEndState(true);
            return view;
        }

        /// <summary>Content-sized panel half-height (fraction of screen) from the VM.</summary>
        private static float PanelHalfHeight(EndStateVM vm)
        {
            float units = 0.6f;                            // header/footer breathing room
            if (vm.Emblem != null) units += 2.4f;
            // Owner F8 flag_04 ("death panel elements overlap"): the subtitle was budgeted
            // ONE line's worth (1.1) regardless of length, so the 3-line death message
            // overflowed its band — shield icon over line 1, Try Again over lines 2-3.
            // Budget the band per WRAPPED LINE so the panel grows to fit the message.
            if (!string.IsNullOrEmpty(vm.Subtitle)) units += 1.1f * SubtitleLines(vm.Subtitle);
            if (vm.Stars >= 0) units += 1.0f;
            if (vm.TimeSeconds >= 0f) units += 0.8f;
            units += vm.Spoils.Count * 1.0f;
            return Mathf.Clamp(0.055f + units * 0.021f, 0.12f, 0.36f);   // WO-433: raise height clamp (was 0.33)
        }

        /// <summary>Estimated WRAPPED line count for the subtitle at FontBody inside the
        /// body well (~36 chars/line at the modal's width). Explicit '\n' segments each
        /// wrap independently. Drives the band weight so a multi-line death message gets
        /// a band tall enough to hold it (F8 flag_04: one-line band = text spilled over
        /// the emblem above and the CTA below). Clamped 1..4.</summary>
        private static int SubtitleLines(string subtitle)
        {
            if (string.IsNullOrEmpty(subtitle)) return 0;
            int lines = 0;
            foreach (var seg in subtitle.Split('\n'))
                lines += Mathf.Max(1, Mathf.CeilToInt(seg.Length / 36f));
            return Mathf.Clamp(lines, 1, 4);
        }

        // ── binding ───────────────────────────────────────────────────────────

        private void Bind(EndStateVM vm, ElarionUiKit.PanelChrome chrome)
        {
            _vm = vm;

            // ── SPLASH TITLE HEADER BAND (F8 2026-07-08: "Wave 1 Cleared!" title rendered
            //    0 visible glyphs) ────────────────────────────────────────────────────────
            // FrameCore's stock header band is only ~0.072 of the panel — the captured title
            // rect was 906x16px, far too SHORT to seat even the kit's 20px FontHardFloor, so
            // UiKitTextFitGuard culled the whole title (0 glyphs). This is the same too-short-
            // band class as the DialogueView header fix, but the EndState panel is ALSO
            // FrameCore-based and the earlier fix didn't reach here. An end-state is a
            // victory / defeat / wave-clear SPLASH — so grow the header into a TALL top band
            // and let a BIG headline (up to FontTitle=88) render, then pull the body top below
            // the band so title and content can never overlap. These are THIS panel's OWN
            // per-instance zones (Zone() mints a fresh RectTransform for each panel), so no
            // other FrameCore screen is affected. Anchors are fractions of panel height, so the
            // splash scales with the panel; the title authors up to 88 and FitSingleLine already
            // bounds it — we only give it ROOM, never shrink the font.
            if (chrome.layout != null && chrome.layout.header != null)
            {
                var hdr = chrome.layout.header;
                hdr.anchorMin = new Vector2(hdr.anchorMin.x, 0.760f);   // was ~0.900
                hdr.anchorMax = new Vector2(hdr.anchorMax.x, 0.985f);   // was ~0.972
                if (chrome.layout.body != null && chrome.layout.body.anchorMax.y > 0.745f)
                    chrome.layout.body.anchorMax =                       // body top clears the band
                        new Vector2(chrome.layout.body.anchorMax.x, 0.745f);
            }

            // Drop-zones (sprite-first contract: layout is null on the procedural
            // fallback panel — mirror the default zone fractions on the content).
            RectTransform well   = chrome.layout != null ? chrome.layout.body
                                 : MakeZone(chrome.content.transform, "Zone_Body",   0.06f, 0.10f, 0.94f, 0.875f);

            // The Continue button owns its OWN footer band (R4: it was overlapping the last reward
            // row, "Iron +8"). FrameCore carries NO footer drop-zone (ElarionUiKit.ZonesFor leaves
            // hasFooter=false for FrameCore, ElarionUiKit.cs:365-373), and the old raw-fraction
            // fallback footer (panel y .030–.095) OVERLAPPED the body well's base (y .075) — that
            // overlap is what pushed the button onto the last row. So when there is no real footer
            // drop-zone, carve the button's band out of the BOTTOM of the body well and hand
            // BuildBody only the reward well ABOVE it, leaving a guaranteed gap between the two.
            bool hasFooterZone = chrome.layout != null && chrome.layout.footer != null;
            // F8-43: compact banners carry NO primary CTA (VM sets PrimaryLabel null/empty)
            // — they auto-dismiss in seconds, so a Continue button is a redundant control
            // (owner one-action law). No CTA => no footer band; the reward well owns the
            // whole body and the exit is auto-dismiss + tap-anywhere (wired below).
            bool hasCta = !string.IsNullOrEmpty(vm.PrimaryLabel);
            // WO-672 Slice E: the ONE case the compact banner's CTA seat returns — a
            // VM-supplied banner CTA ("Repair All - N crystals" on the wave damage
            // report). It is BUTTON-ONLY and distinct from Primary on purpose:
            // tap-anywhere + auto-dismiss keep funnelling FirePrimary (dismiss), so
            // neither can ever silently fire the crystal spend.
            bool hasBannerCta = !hasCta && vm.Compact && !string.IsNullOrEmpty(vm.CtaLabel);
            bool anyCta = hasCta || hasBannerCta;
            RectTransform footer     = !anyCta ? null
                                     : hasFooterZone ? chrome.layout.footer
                                     : MakeZone(well, "Zone_Footer",     0.10f, 0f,    0.90f, 0.16f);
            // VICTORY SWEEP (fresh 1280x720 capture, 2026-07-06: "Wood +15" / "Iron +8" ran
            // BEHIND Continue): on the REAL-footer path (FrameCore relocates its default
            // footer band above the Close) the reward well was the WHOLE body well while the
            // law-pinned canonical CTA — 120 units tall vs the ~50-unit footer band it is
            // centred in — spills UP into the body over the last reward rows. Same zone-flow
            // discipline as the death-panel fix (#22 below): the reward well is ALWAYS its
            // own zone, and on the real-footer path its floor is raised above the pinned
            // CTA's measured top edge so rewards and Continue can never share pixels.
            RectTransform rewardWell = MakeZone(well, "Zone_RewardWell",
                0f, (hasFooterZone || !anyCta) ? 0f : 0.22f, 1f, 1f);

            // ONE primary action (Continue / Rise again / ...) — built FIRST so the reward
            // well can be sized around the law-pinned CTA; it still lands LAST in the reveal.
            // F8-43: skipped entirely when the VM carries no PrimaryLabel (compact banners)
            // — no button, no footer carving; the banner's exit is auto-dismiss + tap-anywhere.
            Button btn = null;
            if (anyCta)
            {
                // WO-672: the banner CTA fires FireCta (the VM action + dismiss); the
                // primary CTA keeps firing FirePrimary. Same seat, same canonical size.
                btn = ElarionUiKit.Button(footer, hasCta ? vm.PrimaryLabel : vm.CtaLabel,
                    ElarionUiKit.ButtonKind.Gold,
                    new Vector2(0.24f, 0.06f), new Vector2(0.76f, 0.94f),
                    hasCta ? (Action)FirePrimary : FireCta);
                // Unaffordable Repair-All renders DISABLED but still shows the cost
                // (informative, not dead — owner law; state carried by the disabled
                // interaction + greyed kit visuals, never color alone).
                if (hasBannerCta) btn.interactable = vm.CtaEnabled;
                // OWNER F8 x3: the Continue/primary action is the SAME pixel size on every
                // screen (matches the shared Close). The anchors above only centre it in the
                // footer band; the canonical size is stamped here.
                ElarionUiKit.PinCanonicalCtaSize(btn);
                Canvas.ForceUpdateCanvases();
                var bRt = (RectTransform)btn.transform;
                // Robust CTA height: measured rect, else the pinned sizeDelta, floored at the
                // canonical constant — a 0 here silently disabled the band growth entirely.
                float need = Mathf.Max(bRt.rect.height,
                    Mathf.Max(bRt.sizeDelta.y, ElarionUiKit.CanonCtaHeight));
                // #22 (capture 9403, "YOU HAVE FALLEN" strip): on SHORT panels the law-pinned
                // canonical CTA is TALLER than the carved footer band, so the centred button
                // spilled UP over the body copy ("Try Again" on top of the death message). The
                // CTA size is law — so the BAND must grow to contain it: when the pinned button
                // exceeds the footer band, raise the band's top and lift the reward well above
                // it (gap preserved).
                if (!hasFooterZone)
                {
                    float wellH = well.rect.height;
                    if (wellH > 1f && need > footer.rect.height - 4f)
                    {
                        float frac = Mathf.Clamp01((need + 12f) / wellH);
                        footer.anchorMax = new Vector2(footer.anchorMax.x, frac);
                        rewardWell.anchorMin = new Vector2(rewardWell.anchorMin.x, Mathf.Min(0.95f, frac + 0.04f));
                        FlowTrace.Step("EndState",
                            $"footer band grown to contain the canonical CTA (need={need:0}px, well={wellH:0}px, band->{frac:0.###})");
                    }
                }
                else
                {
                    // Real footer drop-zone (FrameCore): the footer band cannot grow (it is the
                    // frame's zone), so instead lift the reward well's FLOOR above the CTA's top
                    // edge. footer/well anchors are both fractions of the panel content.
                    var contentRt = chrome.content != null ? chrome.content.GetComponent<RectTransform>() : null;
                    float panelH = contentRt != null ? contentRt.rect.height : 0f;
                    float footerCentre = (footer.anchorMin.y + footer.anchorMax.y) * 0.5f;
                    float ctaTop = panelH > 1f ? footerCentre + (need * 0.5f) / panelH
                                               : footerCentre + 0.12f;   // conservative unmeasured fallback
                    float wellMin = well.anchorMin.y, wellMax = well.anchorMax.y;
                    float floor = Mathf.Clamp01((ctaTop + 0.02f - wellMin)
                                                / Mathf.Max(0.05f, wellMax - wellMin));
                    if (floor > 0f)
                    {
                        rewardWell.anchorMin = new Vector2(rewardWell.anchorMin.x, Mathf.Min(0.5f, floor));
                        FlowTrace.Step("EndState",
                            $"reward well floor raised above the canonical CTA (ctaTop={ctaTop:0.###}, well {wellMin:0.###}-{wellMax:0.###}, floor->{Mathf.Min(0.5f, floor):0.###})");
                    }
                }
            }
            if (vm.Compact && !hasCta)
            {
                // F8-43: no primary CTA on the compact banner — tap-anywhere on the PANEL
                // becomes the manual dismiss (compact panels have no scrim/backdrop, so the
                // world stays interactive around the banner). AutoDismissAfter (below)
                // remains the softlock guard; both funnel FirePrimary, which latches on
                // _fired so the route still fires exactly once. WO-672: when the banner
                // carries a Repair-All CTA the overlay slots BEHIND the panel content
                // (first sibling) so the CTA button stays on top and gets the click —
                // dismiss is then the CTA / auto-dismiss / a tap the chrome lets through.
                var tap = new GameObject("TapDismiss", typeof(Image), typeof(Button));
                tap.transform.SetParent(chrome.root.transform, false);
                var tapRt = (RectTransform)tap.transform;
                tapRt.anchorMin = Vector2.zero; tapRt.anchorMax = Vector2.one;
                tapRt.offsetMin = Vector2.zero; tapRt.offsetMax = Vector2.zero;
                var tapImg = tap.GetComponent<Image>();
                tapImg.color = Color.clear;      // invisible; raycastTarget still catches taps
                tapImg.raycastTarget = true;
                tap.GetComponent<Button>().transition = Selectable.Transition.None;
                tap.GetComponent<Button>().onClick.AddListener(FirePrimary);
                if (hasBannerCta) tap.transform.SetAsFirstSibling();
                FlowTrace.Step("EndState", hasBannerCta
                    ? "compact banner: tap-dismiss behind panel (Repair-All CTA on top)"
                    : "compact banner: primary CTA suppressed (auto-dismiss/tap)");
            }

            // F8-35 ("characters still overlap, extend panel"): the reward well is laid out
            // in PIXELS now (each row owns a fixed row height — see BuildBody), so the well
            // must be at least RequiredBodyPx tall. The old fraction-weight layout simply
            // divided whatever space was left after the close-band reservation + the CTA
            // floor-raise, squeezing 11+ units of content into ~150px — every label/value
            // overprinted the next row. Measure the well and EXTEND the panel (grow the
            // frame root's Y anchors; every zone is fraction-anchored so the whole chrome
            // scales, and the fixed-px CTA/close bands become MORE generous, never less).
            if (chrome.root != null)
            {
                Canvas.ForceUpdateCanvases();
                float wellPx = rewardWell.rect.height;
                float needPx = RequiredBodyPx(vm);
                if (wellPx > 1f && needPx > wellPx + 1f)
                {
                    var rootRt = (RectTransform)chrome.root.transform;
                    if (vm.Compact)
                    {
                        // F8-45 (wave damage report): the compact banner's fixed 0.30h frame
                        // was sized for the row-less wave-clear splash; a spoils report inside
                        // it would only uniform-compress (BuildBody's scale<1 fallback) into
                        // unreadable ~13px rows — the F8-35 class. Grow DOWNWARD (top edge
                        // held at its splash anchor) just enough for the content; the world
                        // stays non-blocked (no scrim) and a row-less banner is unchanged.
                        float y0 = rootRt.anchorMin.y, y1 = rootRt.anchorMax.y;
                        float hNow = y1 - y0;
                        float grownH = Mathf.Min(y1 - 0.08f, hNow * (needPx / wellPx));
                        if (grownH > hNow + 0.001f)
                        {
                            rootRt.anchorMin = new Vector2(rootRt.anchorMin.x, y1 - grownH);
                            Canvas.ForceUpdateCanvases();
                            FlowTrace.Step("EndState",
                                $"compact banner extended down for damage report: need={needPx:0}px well {wellPx:0}->{rewardWell.rect.height:0}px h {hNow:0.###}->{grownH:0.###}");
                        }
                    }
                    else
                    {
                        float y0 = rootRt.anchorMin.y, y1 = rootRt.anchorMax.y;
                        float halfNow = (y1 - y0) * 0.5f;
                        float grownHalf = Mathf.Min(0.47f, halfNow * (needPx / wellPx));
                        if (grownHalf > halfNow + 0.001f)
                        {
                            float cy = Mathf.Clamp((y0 + y1) * 0.5f, 0.03f + grownHalf, 0.97f - grownHalf);
                            rootRt.anchorMin = new Vector2(rootRt.anchorMin.x, cy - grownHalf);
                            rootRt.anchorMax = new Vector2(rootRt.anchorMax.x, cy + grownHalf);
                            Canvas.ForceUpdateCanvases();
                            FlowTrace.Step("EndState",
                                $"panel extended for content: need={needPx:0}px well {wellPx:0}->{rewardWell.rect.height:0}px half {halfNow:0.###}->{grownHalf:0.###}");
                        }
                    }
                }
            }

            BuildBody(vm, rewardWell);
            if (btn != null)   // F8-43: compact banners build no CTA
                Track(btn.gameObject, 0.25f + vm.Spoils.Count * 0.05f + 0.08f, 0.92f);

            // Smooth in: whole panel fades+scales, then the staggered content.
            var rootGroup = chrome.root.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = chrome.root.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            StartCoroutine(RevealRoutine(rootGroup, (RectTransform)chrome.root.transform, 0f, 0.25f, 0.94f));
            foreach (var r in _reveals)
                StartCoroutine(RevealRoutine(r.Group, r.Rect, r.Delay, 0.20f, r.FromScale));

            if (vm.AutoDismissSeconds > 0f)
                StartCoroutine(AutoDismissAfter(vm.AutoDismissSeconds));

            SceneManager.sceneLoaded += OnSceneLoaded;

            FlowTrace.Step("EndState",
                $"{vm.Kind} shown: spoils={vm.Spoils.Count} action={vm.PrimaryRoute}");
        }

        // ── F8-35 pixel row heights (post-scale canvas units — same space as the kit's
        // canonical 360x120 CTA and the ElarionUi font constants: FontBody=50 etc.).
        // Each band OWNS this height; bands never share pixels. If the well is still
        // shorter than the total after the panel extension hit its clamp, all bands
        // compress by one uniform factor and the labels' FitSingleLine shrink-to-fit
        // keeps the text inside its band (never overprinting a sibling).
        private const float EmblemPx  = 80f;
        private const float SubLinePx = 54f;   // per wrapped subtitle line (FontBody 50)
        private const float StarsPx   = 34f;
        private const float TimePx    = 44f;   // FontLabel 40 bold — its OWN row, clear of the subtitle/stars
        private const float RowPx     = 56f;   // one spoils row (FontBody 50 + plate inset)
        private const float BandGapPx = 8f;

        /// <summary>Total body-well pixels the VM's bands demand (drives the panel extension).</summary>
        private static float RequiredBodyPx(EndStateVM vm)
        {
            float px = 0f; int n = 0;
            if (vm.Emblem != null) { px += EmblemPx; n++; }
            if (!string.IsNullOrEmpty(vm.Subtitle)) { px += SubLinePx * SubtitleLines(vm.Subtitle); n++; }
            if (vm.Stars >= 0) { px += StarsPx; n++; }
            if (vm.TimeSeconds >= 0f) { px += TimePx; n++; }
            px += vm.Spoils.Count * RowPx; n += vm.Spoils.Count;
            if (n > 1) px += BandGapPx * (n - 1);
            return px;
        }

        /// <summary>Stack the VM's content top-down inside the body zone. F8-35: bands are
        /// PIXEL-sized (each row owns a real row height) instead of fraction-weighted — the
        /// old weights divided whatever space survived the close-band reservation + CTA
        /// floor-raise, so a 5-reward victory squeezed every row to ~13px and all the
        /// labels/values overprinted (owner capture flag_20260708-085151_03.png).</summary>
        private void BuildBody(EndStateVM vm, RectTransform body)
        {
            // (pixel height, builder) bands, top to bottom.
            var bands = new List<(float px, Action<RectTransform> build)>();

            if (vm.Emblem != null)
                bands.Add((EmblemPx, host =>
                {
                    var go = new GameObject("Emblem", typeof(Image));
                    go.transform.SetParent(host, false);
                    var img = go.GetComponent<Image>();
                    img.sprite = vm.Emblem;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    var rt = img.rectTransform;
                    rt.anchorMin = new Vector2(0.38f, 0.04f);
                    rt.anchorMax = new Vector2(0.62f, 0.96f);
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    Track(go, 0.10f, 0.7f);   // emblem pops from smaller — the hero beat
                }));

            // F8 flag_04 ("death panel elements overlap"): this band was a FIXED 1.1 weight
            // (one line) while the death message wraps to ~3 lines — TMP renders overflow
            // OUTSIDE its rect, so the text climbed under the emblem band above (shield on
            // top of line 1) and sank into the carved footer band below ("Try Again" over
            // lines 2-3). Weight the band per wrapped line (matches PanelHalfHeight, which
            // grows the panel by the same estimate) and auto-shrink as the last-resort
            // guard so the copy can NEVER escape its rect (§1.14: text never overlaps
            // siblings) if the estimate is ever short.
            if (!string.IsNullOrEmpty(vm.Subtitle))
                bands.Add((SubLinePx * SubtitleLines(vm.Subtitle), host =>
                {
                    var l = ElarionUiKit.Label(host, vm.Subtitle, 0f, 1f, ElarionUi.Parchment,
                        ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
                    l.enableAutoSizing = true;                       // shrink-to-fit guard only —
                    l.fontSizeMax = ElarionUi.FontBody;              // never grows past the kit size
                    l.fontSizeMin = ElarionUi.FontBody * 0.66f;
                    l.raycastTarget = false;
                    Track(l.gameObject, 0.14f, 1f);
                }));

            if (vm.Stars >= 0)
                bands.Add((StarsPx, host => BuildStarRow(host, vm.Stars)));

            if (vm.TimeSeconds >= 0f)
                bands.Add((TimePx, host =>
                {
                    var l = ElarionUiKit.Label(host, "Time  " + FormatTime(vm.TimeSeconds), 0f, 1f,
                        ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                        0.06f, 0.94f, bold: true);
                    ElarionUiKit.FitSingleLine(l);   // §1.14 — the time line never spills its own row
                    l.raycastTarget = false;
                    Track(l.gameObject, 0.20f, 1f);
                }));

            for (int i = 0; i < vm.Spoils.Count; i++)
            {
                int idx = i;
                bands.Add((RowPx, host =>
                    Guard.Try("EndState", "spoils row " + idx,
                        () => BuildSpoilRow(host, vm.Spoils[idx], 0.25f + idx * 0.05f))));
            }

            // Lay the bands out top-down at their OWN pixel heights. Only when the well is
            // still shorter than the total (panel extension clamped at 94% screen height)
            // do all bands compress by one uniform factor — logged, never silent.
            if (bands.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            float wellH = body.rect.height;
            float totalPx = BandGapPx * (bands.Count - 1);
            foreach (var b in bands) totalPx += b.px;
            float scale = wellH > 1f && totalPx > wellH ? wellH / totalPx : 1f;
            if (scale < 1f)
                FlowTrace.Warn("EndState",
                    $"body rows compressed to fit: need={totalPx:0}px well={wellH:0}px scale={scale:0.###} (panel extension clamped)");
            float y = 0f;
            foreach (var (px, build) in bands)
            {
                var host = MakeZonePx(body, "Band", y, px * scale);
                y += (px + BandGapPx) * scale;
                build(host);
            }
        }

        /// <summary>One spoils row: kit slot plate + icon (null-safe) + label + amount.</summary>
        private void BuildSpoilRow(RectTransform host, SpoilRowVM row, float revealDelay)
        {
            if (row == null) return;
            // WO-714 W2 (pack row-list grammar): the row plate is the REAL Blink Stat_Element
            // (element/element_stat, 9-sliced — the same plate CurrencyChip and the HUD stat rows
            // sit on), resolved sprite-first ALWAYS (P9 — never gated on ff.blinkchrome). On the
            // real plate the pack's embossed steel carries the depth, so no procedural accent:
            // gold stays reserved for content (the amount / gilt values), never chrome.
            // Null-art fallback = the previous (#23) procedural obsidian tile + thin gold left
            // bar, byte-for-byte — an art-absent run never blanks a reward row.
            GameObject plate;
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
            if (plateSprite != null)
            {
                plate = ElarionUiKit.AddImage(host, "SpoilRow",
                    new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.96f), Color.white, rounded: false);
                var pImg = plate.GetComponent<Image>();
                pImg.sprite = plateSprite;
                pImg.type = Image.Type.Sliced;
                pImg.raycastTarget = false;
            }
            else
            {
                plate = ElarionUiKit.AddImage(host, "SpoilRow",
                    new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.96f), ElarionUiKit.ObsidianFill);
                plate.GetComponent<Image>().raycastTarget = false;
                var accent = ElarionUiKit.AddImage(plate.transform, "GoldAccent",
                    new Vector2(0f, 0.12f), new Vector2(0.02f, 0.88f), ElarionUiKit.ObsidianTrim, rounded: false);
                accent.GetComponent<Image>().raycastTarget = false;
            }
            // Icon: sprite-first from the VM; when the VM had no sheet art for the item (R4: "Wood"
            // — ItemIconCatalog.ForConsumable("mat_wood") resolves null today, EndStateVM.cs:120-122)
            // fall back to a generic resource/loot icon so a reward row NEVER blanks its slot. Same
            // RpgUiCatalog.Get path the other rows resolve through on the model side.
            var iconSprite = row.Icon;
            // SWEEP 9413 R2 (#7): the generic IconInventory fallback painted a plain yellow
            // square beside every art-less reward line. Resolve the reward CONCEPT icon first
            // (concept-icons.json maps gold/wood/iron → the currency sprites) from the row label;
            // only then the generic kit fallback — never a bare placeholder square.
            if (iconSprite == null)
                iconSprite = ConceptIconResolver.ResolveAny((row.Label ?? "").Trim().ToLowerInvariant());
            if (iconSprite == null)
                iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconInventory);
            if (iconSprite != null)
            {
                var go = new GameObject("Icon", typeof(Image));
                go.transform.SetParent(plate.transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = iconSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                // Fresh-capture sweep 2026-07-06: fraction-sized icons collapsed to ~12px
                // when the band stack squeezed the rows. Fixed 40x40 reference-unit square
                // (>= 24 screen px at the 720p landscape scale), anchored middle-left —
                // the icon never shrinks with its band.
                rt.anchorMin = new Vector2(0.035f, 0.5f);
                rt.anchorMax = new Vector2(0.035f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(40f, 40f);
            }
            // F8-35: label left / value right, each FIT to ONE line in its own column —
            // "Equipped" wrapped to "Equipp/d" and long gear names spilled into the value
            // column at the fixed FontBody size. FitSingleLine (§1.14) shrinks-to-fit with
            // ellipsis so neither side can ever wrap or cross the column split again.
            var label = ElarionUiKit.Label(plate.transform, row.Label ?? "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft,
                iconSprite != null ? 0.17f : 0.06f, 0.62f);
            ElarionUiKit.FitSingleLine(label);
            label.raycastTarget = false;
            var amount = ElarionUiKit.Label(plate.transform, row.Amount ?? "", 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineRight,
                0.64f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(amount);
            amount.raycastTarget = false;
            Track(plate, revealDelay, 0.96f);
        }

        /// <summary>Rating row: three procedural gold diamonds (filled/dim). Deliberately
        /// sprite-free — the TMP star glyphs tofu'd on the build font and the crown art
        /// carries a white fringe (owner F8), so the rating can never blank or fringe.</summary>
        private void BuildStarRow(RectTransform host, int stars)
        {
            var rowGo = new GameObject("Stars", typeof(RectTransform));
            rowGo.transform.SetParent(host, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = Vector2.zero; rowRt.anchorMax = Vector2.one;
            rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Star" + i, typeof(Image));
                go.transform.SetParent(rowRt, false);
                var img = go.GetComponent<Image>();
                img.color = i < stars ? ElarionUiKit.ObsidianTrim : new Color(1f, 1f, 1f, 0.14f);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                float cx = 0.5f + (i - 1) * 0.13f;
                rt.anchorMin = new Vector2(cx, 0.5f);
                rt.anchorMax = new Vector2(cx, 0.5f);
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // diamond
            }
            Track(rowGo, 0.18f, 1f);
        }

        // ── actions / lifecycle ───────────────────────────────────────────────

        /// <summary>Fire the VM's primary action exactly once, then tear down.</summary>
        private void FirePrimary()
        {
            if (_fired) return;
            _fired = true;
            FlowTrace.Step("EndState", $"{_vm.Kind} primary fired: action={_vm.PrimaryRoute}");
            // F8-15: the continue/respawn path OUT of the death screen — name the route the
            // player chose so the death window shows the full open->close lifecycle.
            DeathTrace.Note($"END-STATE CONTINUE: '{_vm.Title}' primary fired -> action={_vm.PrimaryRoute} (screen tearing down)");
            var act = _vm.Primary;
            _vm.Primary = null;
            act?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>WO-672 Slice E: fire the banner CTA (Repair All) exactly once, then
        /// dismiss the banner via the normal primary route. The repaired-summary lands
        /// through WallRepairController.FeedbackShown (the existing HUD toast surface),
        /// so the banner does not need to re-render its rows (dismiss > refresh: the
        /// simpler honest option — the toast states exactly what was repaired/spent).</summary>
        private bool _ctaFired;
        private void FireCta()
        {
            if (_ctaFired || _fired) return;
            _ctaFired = true;
            FlowTrace.Step("EndState", $"{_vm.Kind} banner CTA fired: action={_vm.CtaRoute}");
            var act = _vm.Cta;
            _vm.Cta = null;
            Guard.Try("EndState", "banner CTA action", () => act?.Invoke());
            FirePrimary();   // dismiss after the action; latched, fires exactly once
        }

        private IEnumerator AutoDismissAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, seconds));
            FirePrimary();
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // The world moved on underneath us (e.g. raid-death evac loaded the hub):
            // tear down silently WITHOUT firing the primary route.
            _fired = true;
            Destroy(gameObject);
        }

        /// <summary>HUD-2: the single-modal arbiter swapped us out (another modal opened over the
        /// end-state). Tear down WITHOUT firing the primary route — a displaced end-state must not
        /// silently trigger continue/respawn (mirrors <see cref="OnSceneLoaded"/>).</summary>
        private void CloseFromArbiter()
        {
            _fired = true;
            if (this != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // HUD-2: release the arbiter slot (no-op for compact banners - handle is null - and a
            // no-op if we were already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            if (_open == this)
            {
                // F8-15: close step-out for the death window — pairs with the ScreenOpened above so
                // the chain shows each end-state's full open->close lifecycle (which popup outlived which).
                if (DeathTrace.Active)
                    DeathTrace.ScreenClosed("EndState '" + (_vm != null ? _vm.Title : "?") + "'",
                        "EndStateView.OnDestroy" + (_fired ? " (primary fired)" : " (torn down without firing)"));
                _open = null;
                // P23 (A4.6): the decision node closed — the posture arc moves on.
                DeNelle.Core.HudModel.PostureSignals.SetEndState(false);
            }
        }

        // ── smooth-in tween (KIT-PROMOTION CANDIDATE) ─────────────────────────

        /// <summary>Register a GameObject for the staggered reveal (alpha 0 until its turn).</summary>
        private void Track(GameObject go, float delay, float fromScale)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _reveals.Add(new Reveal
            {
                Group = cg,
                Rect = go.transform as RectTransform,
                Delay = delay,
                FromScale = fromScale,
            });
        }

        /// <summary>Ease-out cubic fade+scale on UNSCALED time (plays through slow-mo /
        /// any pause). Mirrors the proven BattleArenaHud.PopCrown pattern, generalized.</summary>
        private static IEnumerator RevealRoutine(CanvasGroup cg, RectTransform rt,
                                                 float delay, float duration, float fromScale)
        {
            if (cg == null) yield break;
            if (rt != null && fromScale < 1f) rt.localScale = Vector3.one * fromScale;

            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 3f);   // ease-out cubic
                if (cg == null) yield break;                 // torn down mid-tween
                cg.alpha = eased;
                if (rt != null && fromScale < 1f)
                    rt.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, eased);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;
        }

        // ── tiny helpers ──────────────────────────────────────────────────────

        /// <summary>Kit buttons need an EventSystem; builds don't always have one
        /// (the reason GameOverScreen hand-rolled hit-testing). Same proven pattern
        /// as BattleArenaHud.EnsureEventSystem.</summary>
        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        /// <summary>A full-width zone at a FIXED pixel height, stacked from the TOP of the
        /// parent (F8-35: bands own their row height instead of splitting the well by
        /// fraction — a squeezed well can no longer overprint every row into ~13px).</summary>
        private static RectTransform MakeZonePx(RectTransform parent, string name,
                                                float topPx, float heightPx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(topPx + heightPx));
            rt.offsetMax = new Vector2(0f, -topPx);
            return rt;
        }

        private static RectTransform MakeZone(Transform parent, string name,
                                              float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
