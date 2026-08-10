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
            // Section 12: a NEW Show() replacing an OPEN end-state is the path that stranded the
            // owner twice - the village wave banner landing on top of an arena victory summary and
            // taking its home-return action with it. Previously this was logged only when the
            // DeathTrace forensic window happened to be open, i.e. never in normal play.
            if (_open != null)
            {
                _open.AbandonedPrimaryWarn($"EndStateView.Show - REPLACED by a new end-state '{vm.Title}'");
                Destroy(_open.gameObject);
                _open = null;
            }

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
                // Grown DOWN (top edge held at CompactTopY) to 0.30 of screen height: this is
                // the row-less SPLASH size (F8-45/WO-952: Bind's owned compact solve grows the
                // banner downward to its FINAL content-fitted height), so it must carry enough
                // height for the header band (Bind) plus the emblem+subtitle below it —
                // otherwise the tall title band would crush them. (Was 0.64–0.86 = 0.22h, too
                // short to seat the headline.)
                chrome = ElarionUiKit.BuildObsidianPanel(canvas.transform, vm.Title,
                    new Vector2(CompactX0, CompactSplashBottomY), new Vector2(CompactX1, CompactTopY),
                    onClose: null, withBackdrop: false, frameName: RpgUiCatalog.FrameCore,
                    medallionIcon: "crest");   // explicit: the socket seats the crest family, never blank
            }
            else
            {
                // Full end-state modal, sized to the VM's content in REAL PIXELS.
                //
                // OWNER F8 2026-08-05 ("the text is too compacted"): the panel used to be built at a
                // GUESSED fictional-unit size and then grown ~2x by a post-hoc extension block. Every
                // fraction reservation inside it — the kit's close-band reservation, the header band,
                // the CTA floor-raise — had already been computed against the PRE-growth panel and was
                // never recomputed, so the body zone kept a fraction sized for a panel half as tall and
                // the content got ~17% of the panel. Build the CANVAS FIRST (BuildObsidianModal's own
                // three steps, inlined — canvas + scrim + panel) so the panel height can be SOLVED
                // against ElarionUiKit.PostScaleCanvasHeight BEFORE the frame exists. The panel is then
                // built ONCE at its final size and never resized: nothing can desynchronise.
                canvas = ElarionUiKit.BuildModalCanvas("EndState", 31000);
                var mc = canvas.GetComponent<Canvas>();
                if (mc != null) mc.overrideSorting = true;
                ElarionUiKit.Scrim(canvas.transform, null);   // pure raycast-blocker — no second way out
                float half = PanelHalfHeight(vm, ElarionUiKit.PostScaleCanvasHeight(canvas.transform));
                // ORCHESTRATOR RULING (WO-894): vertical centre 0.53 -> 0.50. The panel is built at
                // centre +- MaxPanelHalf(0.47), so a 0.53 centre put the TOP edge at 0.53 + 0.47 =
                // 1.000 — flush with the screen top, i.e. clipping — while MaxPanelHalf's own comment
                // documents the intent as "0.03..0.97". At 0.50 the clamp lands exactly on the
                // documented 0.03..0.97 at IDENTICAL height. Costs 3% of downward drift toward the
                // bottom HUD band; a screen touching the top edge is the worse defect.
                chrome = ElarionUiKit.BuildObsidianPanel(canvas.transform, vm.Title,
                    new Vector2(0.22f, 0.50f - half), new Vector2(0.78f, 0.50f + half),   // WO-433: narrower victory panel (was 0.08/0.92)
                    onClose: null,   // no second way out
                    frameName: RpgUiCatalog.FrameCore,
                    medallionIcon: "crest");   // explicit: the socket seats the crest family, never blank
            }

            // Owner button law: an end-state has exactly ONE way out (the primary button).
            // Hide the factory's shared Close chip.
            // LOAD-BEARING: Bind's owned geometry pass RECLAIMS the kit's close-band reservation
            // on the strength of this line. Re-enabling the Close here without reverting that
            // pass would put the Close underneath the CTA. (A kit-level withClose:false would make
            // this first-class instead of hide-after-build — reported, deliberately not done here:
            // an ElarionUiKit signature change has game-wide blast radius.)
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

        // ── PANEL GEOMETRY LAW (owner F8 2026-08-05) ──────────────────────────────
        // ONE stack, all fractions of THE SAME (final) panel height, top to bottom:
        //   [0.985 .. 0.820]  header band  — one FontTitle(88) line, ~101px line box
        //   [0.805 .. floor]  BODY WELL    — owns every VM band (this is what must fit)
        //   [floor .. ctaTop] CtaGapY      — the guaranteed gap; bands never share pixels
        //   [ctaTop.. 0.045]  the canonical 360x132 CTA, seated in the RECLAIMED CLOSE BAND
        //                     (this screen HIDES the shared Close — see Show, below).
        // The CTA is a FIXED 132 reference px, so it is subtracted in PIXELS, never as a
        // fraction: that is precisely the unit mix-up that produced the compressed screen.
        private const float HeaderY0  = 0.820f;   // was 0.760 — 0.225 of the panel for ONE title line
        private const float HeaderY1  = 0.985f;
        private const float BodyTopY  = 0.805f;   // body top clears the header band
        private const float CtaBandY0 = 0.045f;   // CTA bottom edge (the freed close band's lower edge)
        private const float CtaGapY   = 0.020f;   // matches the kit's own body/close gap
        /// <summary>Panel fraction available to the body well once the header, the gap and the
        /// CTA BAND ORIGIN are taken; the CTA's own 132 px come off in pixels.</summary>
        private const float BodyFracOfPanel = BodyTopY - CtaBandY0 - CtaGapY;   // 0.740
        /// <summary>Panel may span 0.03..0.97 of the screen (the old grownHalf 0.47 clamp).
        /// TRUE AGAIN as of WO-894: the panel is centred at 0.50, so 0.50 +- 0.47 really is
        /// 0.03..0.97. It was built at centre 0.53 until now, which silently made the real span
        /// 0.06..1.00 — the top edge flush with the screen edge.</summary>
        private const float MaxPanelHalf = 0.47f;
        private const float MinPanelHalf = 0.14f;

        // ── COMPACT BANNER GEOMETRY (the wave-clear / outpost variant) ────────────
        /// <summary>Compact body-well TOP as a fraction of the banner panel — pulled below the
        /// tall splash header band so the headline and the content can never overlap.</summary>
        private const float CompactBodyTopY = 0.745f;
        /// <summary>Compact body-well FLOOR. This is FrameCore's OWN art-measured well floor
        /// (ElarionUiKit ZonesFor, case FrameCore: z.body = (0.055, 0.075, 0.945, 0.835)) — it
        /// clears the frame's ornate bottom border. WO-952: the owned compact solve reclaims
        /// down to this floor on EVERY compact banner — a CTA-carrying banner seats the CTA in
        /// its own band ON this floor instead of keeping the kit's dead close-band reservation
        /// (which is what left a 249px well for 276px of rows, the captured defect).</summary>
        private const float CompactBodyFloorY = 0.075f;

        // ── WO-952 COMPACT FRAME CONSTANTS — single source for Show AND the owned solve
        //    (the capture proved these numbers were living in two places: Show's literal
        //    anchors and the growth block's literal 0.08 clamp; a solve that must invert
        //    the layout law needs them named once). ──────────────────────────────────────
        /// <summary>Banner left/right edges on the canvas (Show's build anchors).</summary>
        private const float CompactX0 = 0.15f;
        private const float CompactX1 = 0.85f;
        /// <summary>Banner panel width as a canvas fraction — the compact analogue of
        /// <see cref="PanelWidthFrac"/>. WO-952: the subtitle/spoils width chain used the
        /// full modal's 0.56 for the banner too, under-measuring the banner's real 0.70
        /// column by 20% and over-counting wrapped lines (need inflated for nothing).</summary>
        private const float CompactPanelWidthFrac = CompactX1 - CompactX0;   // 0.700
        /// <summary>Banner TOP edge (screen fraction) — held while the banner grows down.</summary>
        private const float CompactTopY = 0.86f;
        /// <summary>Row-less SPLASH bottom edge: the 0.30h build-time banner (Show).</summary>
        private const float CompactSplashBottomY = 0.56f;
        /// <summary>The grown banner's bottom-edge floor (the pre-existing growth clamp:
        /// the world stays visible below the banner, so it may span at most
        /// <see cref="CompactTopY"/> minus this of the screen = 0.78h).</summary>
        private const float CompactGrowthFloorY = 0.08f;
        /// <summary>Gap between the body well's floor and the seated banner CTA, ref px
        /// (matches the +12 the legacy footer-grow compensation used).</summary>
        private const float CompactCtaGapPx = 12f;

        /// <summary>Deterministic body-well height in reference px for the OWNED geometry path
        /// (0 = not owned; BuildBody then measures). Set by the geometry pass in Bind.</summary>
        private float _wellPx;

        /// <summary>Compact banner only: the body well's height as a fraction of the PANEL,
        /// captured once so the downward-growth block can re-solve <see cref="_wellPx"/>
        /// against the grown panel instead of re-measuring a live rect.</summary>
        private float _compactBodyFrac;

        /// <summary>WO-952: the compact banner's OWNED CTA band — canonical CTA height,
        /// seated on the frame art's measured well floor, sized against the banner's FINAL
        /// solved height. Non-null only when the owned compact solve ran for a banner that
        /// carries a CTA; the CTA build then seats the button here instead of carving a
        /// footer out of the body well (the carve is what the stale close-band reservation
        /// used to collide with).</summary>
        private RectTransform _compactCtaBand;

        /// <summary>Content-sized panel HALF-height (fraction of screen), solved in REAL PIXELS.
        /// The old body of this method summed fictional "units" (2.4 for an emblem, 1.1 per
        /// subtitle line...) and multiplied by 0.021 — a number with no relationship to the
        /// pixel-sized bands BuildBody actually lays out, which is why the panel was always the
        /// wrong size and needed the post-hoc extension that desynchronised every fraction.
        /// Invert the real layout law instead: wellPx = BodyFracOfPanel * panelPx - CanonCtaHeight,
        /// so panelPx = (RequiredBodyPx + CanonCtaHeight) / BodyFracOfPanel.</summary>
        private static float PanelHalfHeight(EndStateVM vm, float canvasH)
        {
            if (canvasH < 100f) canvasH = 1920f;   // headless / no scaler — the kit's own fallback
            float panelPx = (RequiredBodyPx(vm, canvasH) + ElarionUiKit.CanonCtaHeight) / BodyFracOfPanel;
            return Mathf.Clamp(panelPx / (2f * canvasH), MinPanelHalf, MaxPanelHalf);
        }

        // ── SUBTITLE MEASUREMENT (WO-894, orchestrator ruling) ────────────────────────
        // This used to be `seg.Length / 36f` — a FIXED chars-per-line tuned for the portrait
        // panel. 36 chars at FontBody 50 implies a ~900px text column, which is roughly right
        // for the 2670x1200 landscape well (~985px) and nearly DOUBLE the portrait well
        // (~495px). So the same constant over-reserved on the raid victory (the 4-line clamp,
        // 240px) and under-reserved in portrait. Both the WIDTH and the TEXT are now measured.

        /// <summary>Post-scale canvas WIDTH, in the SAME reference-px space as
        /// <see cref="ElarionUiKit.PostScaleCanvasHeight"/>. The CanvasScaler divides BOTH axes
        /// by one scaleFactor, so the post-scale canvas keeps the screen's aspect and the width
        /// is simply height x aspect. DERIVED rather than measured for exactly the reason the
        /// height is (ElarionUiKit.cs:1014-1018): a live rect read on the creation frame returns
        /// RAW SCREEN pixels.</summary>
        private static float PostScaleCanvasWidth(float canvasH)
        {
            // SurfaceWidth/Height, not Screen.* — identical at runtime (no override); a capture
            // drives them so this build-time width resolves the TARGET aspect, not the editor's.
            float sw = ElarionUiKit.SurfaceWidth, sh = ElarionUiKit.SurfaceHeight;
            if (sw < 1f || sh < 1f) return canvasH * (1080f / 1920f);   // headless: kit portrait reference
            return canvasH * (sw / sh);
        }

        // The deterministic width chain down to the subtitle's own text column. Every link is a
        // constant already in the tree, cited so a future reader can re-verify without measuring:
        //   panel      x 0.22..0.78 of the canvas        (Show, above — WO-433)
        //   body zone  x 0.055..0.945 of the panel       (ElarionUiKit ZonesFor, case FrameCore:
        //                                                 z.body = (0.055, 0.075, 0.945, 0.835))
        //   subtitle   x 0.04..0.96 of its band          (BuildBody, below)
        private const float PanelWidthFrac    = 0.78f - 0.22f;     // 0.560
        private const float BodyZoneWidthFrac = 0.945f - 0.055f;   // 0.890 (FrameCore)
        private const float SubtitleInsetFrac = 0.96f - 0.04f;     // 0.920

        /// <summary>The panel-width fraction the width chain must use for THIS screen.
        /// WO-952: the banner spans 0.70 of the canvas (Show: 0.15..0.85) but the chain
        /// always used the full modal's 0.56 — under-measuring the compact subtitle column
        /// by 20%, over-counting wrapped lines and inflating the banner's need.</summary>
        private static float PanelWidthFracFor(EndStateVM vm)
        {
            return vm != null && vm.Compact ? CompactPanelWidthFrac : PanelWidthFrac;
        }

        /// <summary>Reference px of text column the subtitle actually gets.
        /// <paramref name="panelWidthFrac"/> = this screen's canvas-width fraction
        /// (<see cref="PanelWidthFracFor"/>) — WO-952: never assume the modal's.</summary>
        private static float SubtitleWidthPx(float canvasH, float panelWidthFrac)
        {
            return PostScaleCanvasWidth(canvasH) * panelWidthFrac * BodyZoneWidthFrac * SubtitleInsetFrac;
        }

        /// <summary>WRAPPED line count for the subtitle at FontBody inside the REAL body-well
        /// width. Explicit '\n' segments each wrap independently. Drives the band height so a
        /// multi-line death message gets a band tall enough to hold it (F8 flag_04: a one-line
        /// band let the text spill over the emblem above and the CTA below). Clamped 1..4.</summary>
        private static int SubtitleLines(string subtitle, float canvasH, float panelWidthFrac)
        {
            if (string.IsNullOrEmpty(subtitle)) return 0;
            float columnPx = Mathf.Max(1f, SubtitleWidthPx(canvasH, panelWidthFrac));
            int lines = 0;
            foreach (var seg in subtitle.Split('\n'))
                lines += Mathf.Max(1, Mathf.CeilToInt(MeasureTextPx(seg, ElarionUi.FontBody) / columnPx));
            return Mathf.Clamp(lines, 1, 4);
        }

        private static TMPro.TMP_FontAsset _bodyFont;
        private static bool _bodyFontTried;

        /// <summary>Rendered width of <paramref name="text"/> at <paramref name="fontSize"/> in
        /// reference px, summed from the BODY FONT'S OWN GLYPH ADVANCES — the same numbers TMP
        /// lays the text out with. A MEASUREMENT, not a character estimate: it cannot drift when
        /// the copy changes (which is precisely how the fixed "36 chars/line" went wrong).
        ///
        /// Falls back to a 0.5em average ONLY if the font asset is absent or its character table
        /// is unpopulated (a dynamic atlas before anything has been rendered) — detected by how
        /// much of the string actually resolved, never assumed. Even that fallback is derived
        /// from the real font SIZE and applied against the real column width, so it is still
        /// geometry-aware. Kerning is ignored (sub-1% on Latin copy at this size).</summary>
        private static float MeasureTextPx(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            if (!_bodyFontTried)
            {
                _bodyFontTried = true;
                try
                {
                    _bodyFont = Resources.Load<TMPro.TMP_FontAsset>(
                        RpgUiCatalog.FontRoot + RpgUiCatalog.FontBodyAsset);
                }
                catch (Exception e)
                {
                    FlowTrace.Warn("EndState", "body font load failed for text measure: " + e.Message);
                    _bodyFont = null;
                }
            }

            var fa = _bodyFont;
            if (fa != null && fa.faceInfo.pointSize > 0f)
            {
                float advance = 0f;
                int matched = 0;
                try
                {
                    var table = fa.characterLookupTable;
                    if (table != null)
                    {
                        for (int i = 0; i < text.Length; i++)
                        {
                            if (table.TryGetValue(text[i], out var ch) && ch != null && ch.glyph != null)
                            {
                                advance += ch.glyph.metrics.horizontalAdvance;
                                matched++;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // §12 no silent failure: say the metric path failed, then use the estimate.
                    FlowTrace.Throttle("EndState", "measure-fallback", 10f,
                        "glyph-advance measure failed, using the em estimate: " + e.Message);
                    matched = 0;
                }
                // Only trust the sum when most of the string really resolved — an empty/partial
                // table would otherwise measure a long line as nearly zero and under-reserve.
                if (matched >= Mathf.CeilToInt(text.Length * 0.6f))
                    return advance * (fontSize / fa.faceInfo.pointSize) * fa.faceInfo.scale;
            }

            return text.Length * fontSize * 0.5f;   // ~0.5em average advance for Latin copy
        }

        // ── binding ───────────────────────────────────────────────────────────

        /// <summary>Post-scale canvas height for THIS screen, captured once in <see cref="Bind"/>.
        /// The subtitle's wrapped line count is measured against the real column width, which is
        /// derived from this — so band sizing and the panel solve use the SAME number.</summary>
        private float _canvasH;

        private void Bind(EndStateVM vm, ElarionUiKit.PanelChrome chrome)
        {
            _vm = vm;
            _canvasH = ElarionUiKit.PostScaleCanvasHeight(
                chrome.root != null ? chrome.root.transform : transform);

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
            //
            // OWNED GEOMETRY PASS (full modal only). One place stamps header / CTA band / body
            // well, all against the SAME measured panel height, BEFORE anything is built into
            // them. Replaces the three desynchronised reservations the owner felt as compaction.
            bool ownGeometry = !vm.Compact && chrome.layout != null
                               && chrome.layout.header != null
                               && chrome.layout.body != null
                               && chrome.layout.footer != null
                               && chrome.root != null;
            if (ownGeometry)
            {
                // Panel height the DETERMINISTIC way (ElarionUiKit.cs:1014-1018): reading a live
                // rect on the canvas's creation frame returns RAW SCREEN pixels because the
                // CanvasScaler has not applied yet. PostScaleCanvasHeight x the panel's own anchor
                // span gives the height the fraction anchors will really resolve against.
                var rootRt = (RectTransform)chrome.root.transform;
                float panelFracH = Mathf.Max(0.05f, rootRt.anchorMax.y - rootRt.anchorMin.y);
                float panelPx = _canvasH * panelFracH;
                float ctaBandH = ElarionUiKit.CanonCtaHeight / Mathf.Max(1f, panelPx);
                float bodyFloor = CtaBandY0 + ctaBandH + CtaGapY;

                // Header: 0.760-0.985 was ~0.225 of the panel — far more than ONE FontTitle(88)
                // line needs, and every px of it came out of the body.
                var hdr = chrome.layout.header;
                hdr.anchorMin = new Vector2(hdr.anchorMin.x, HeaderY0);
                hdr.anchorMax = new Vector2(hdr.anchorMax.x, HeaderY1);
                hdr.offsetMin = new Vector2(hdr.offsetMin.x, 0f);
                hdr.offsetMax = new Vector2(hdr.offsetMax.x, 0f);

                // RECLAIM THE DEAD CLOSE BAND (the recipe already merged for
                // FoundingChoiceController.cs:177-188). The kit reserves room at the bottom of
                // EVERY framed panel for the ONE shared Close (BuildObsidianPanel's close-band
                // reservation, ElarionUiKit.cs:582-647): it relocates the footer band ABOVE the
                // Close box and raises the body floor above that. THIS screen HIDES the Close
                // (owner button law — one way out; see Show), so the whole reservation is dead
                // space. Seat the CTA in the freed band and drop the body floor onto it.
                // VALID ONLY BECAUSE THE CLOSE IS HIDDEN — do not re-enable the Close without
                // reverting this pass.
                var ftr = chrome.layout.footer;
                ftr.anchorMin = new Vector2(ftr.anchorMin.x, CtaBandY0);
                ftr.anchorMax = new Vector2(ftr.anchorMax.x, CtaBandY0 + ctaBandH);
                ftr.offsetMin = new Vector2(ftr.offsetMin.x, 0f);
                ftr.offsetMax = new Vector2(ftr.offsetMax.x, 0f);

                var bdy = chrome.layout.body;
                bdy.anchorMin = new Vector2(bdy.anchorMin.x, bodyFloor);
                bdy.anchorMax = new Vector2(bdy.anchorMax.x, BodyTopY);
                bdy.offsetMin = new Vector2(bdy.offsetMin.x, 0f);
                bdy.offsetMax = new Vector2(bdy.offsetMax.x, 0f);

                // The well height is now KNOWN in reference px — hand it to BuildBody instead of
                // letting it re-measure a creation-frame rect.
                _wellPx = (BodyTopY - bodyFloor) * panelPx;
                Canvas.ForceUpdateCanvases();
                FlowTrace.Step("EndState",
                    $"geometry: panel={panelPx:0}px (frac {panelFracH:0.###}) header {HeaderY0:0.###}-{HeaderY1:0.###} " +
                    $"body {bodyFloor:0.###}-{BodyTopY:0.###} = {_wellPx:0}px cta band {CtaBandY0:0.###}-{CtaBandY0 + ctaBandH:0.###} " +
                    $"need={RequiredBodyPx(vm, _canvasH):0}px " +
                    $"(subtitle column {SubtitleWidthPx(_canvasH, PanelWidthFracFor(vm)):0}px -> {SubtitleLines(vm.Subtitle, _canvasH, PanelWidthFracFor(vm))} line(s))");
            }
            else if (chrome.layout != null && chrome.layout.header != null)
            {
                // Compact banner (and any layout without a footer zone): unchanged splash header.
                var hdr = chrome.layout.header;
                hdr.anchorMin = new Vector2(hdr.anchorMin.x, 0.760f);   // was ~0.900
                hdr.anchorMax = new Vector2(hdr.anchorMax.x, 0.985f);   // was ~0.972
                if (chrome.layout.body != null && chrome.layout.body.anchorMax.y > CompactBodyTopY)
                    chrome.layout.body.anchorMax =                       // body top clears the band
                        new Vector2(chrome.layout.body.anchorMax.x, CompactBodyTopY);

                // ── WO-952 OWNED COMPACT GEOMETRY (F8 capture 2026-08-10, twice: "need=276px
                //    well=249px scale=0.9") — the full modal's 2026-08-05 lesson applied to
                //    the banner ─────────────────────────────────────────────────────────────
                // WHAT WENT WRONG: the old pass reclaimed the kit's dead close-band reservation
                // ONLY when the banner carried no CTA at all. A WO-672 Repair-All banner kept
                // the reservation's 0.45 body floor — computed against the 0.30h SPLASH panel —
                // and the later downward growth scaled that stale fraction up with the panel:
                // at the growth clamp on a 16:9 desktop (1080 ref-px canvas, 842px panel),
                // 0.45 x 842 = 379px sat below the body well for a 132px button, leaving a
                // 0.295 x 842 = 249px well for 276px of rows -> uniform 0.9 compression, every
                // band below its own content size. Exactly the captured numbers.
                //
                // THE FIX IS REFLOW, NOT SHRINK: solve the banner's FINAL height up front from
                // the content it must seat (need + the canonical CTA band when one is carried),
                // stamp every band against that ONE height, and seat the CTA in its OWN bottom
                // band on the frame art's measured well floor (ZonesFor FrameCore z.body.y =
                // 0.075). The close-band reclaim therefore now runs on EVERY compact banner —
                // CTA-shaped instead of gated off — and nothing is stamped before the height it
                // is a fraction of is known, so nothing can desynchronise (the exact recipe
                // that fixed the full modal's compaction on 2026-08-05).
                //
                // Clamps: never below the 0.30h splash (a row-less banner is unchanged), never
                // past the growth floor (the world below stays visible). The growth floor is
                // the ONE remaining compression source; BuildBody's Fail net still names it
                // when it bites — the net stays, it caught this.
                bool compactAnyCta = vm.Compact
                                     && (!string.IsNullOrEmpty(vm.PrimaryLabel)
                                         || !string.IsNullOrEmpty(vm.CtaLabel));
                if (vm.Compact && chrome.layout.body != null && chrome.root != null)
                {
                    var bdy = chrome.layout.body;
                    var rootRt0 = (RectTransform)chrome.root.transform;
                    float topY = rootRt0.anchorMax.y;                            // splash top, held
                    float hNow = Mathf.Max(0.05f, topY - rootRt0.anchorMin.y);   // the 0.30h splash
                    float needPx = RequiredBodyPx(vm, _canvasH);
                    float ctaPx = compactAnyCta
                        ? ElarionUiKit.CanonCtaHeight + CompactCtaGapPx : 0f;
                    // Invert the layout law (the PanelHalfHeight recipe): the body well is
                    // (CompactBodyTopY - CompactBodyFloorY) of the panel minus the CTA band's
                    // pixels, so panelPx = (need + ctaBand) / that fraction.
                    float solvedPx = (needPx + ctaPx) / (CompactBodyTopY - CompactBodyFloorY);
                    float panelFrac = Mathf.Clamp(solvedPx / Mathf.Max(1f, _canvasH),
                                                  hNow, topY - CompactGrowthFloorY);
                    rootRt0.anchorMin = new Vector2(rootRt0.anchorMin.x, topY - panelFrac);
                    float panelPx = panelFrac * _canvasH;

                    float bodyFloor = CompactBodyFloorY + ctaPx / Mathf.Max(1f, panelPx);
                    bdy.anchorMin = new Vector2(bdy.anchorMin.x, bodyFloor);
                    bdy.anchorMax = new Vector2(bdy.anchorMax.x, CompactBodyTopY);
                    bdy.offsetMin = new Vector2(bdy.offsetMin.x, 0f);
                    bdy.offsetMax = new Vector2(bdy.offsetMax.x, 0f);

                    if (compactAnyCta)
                    {
                        // The CTA's OWN band: canonical height, seated on the art floor, its
                        // fraction computed from the FINAL panel px so PinCanonicalCtaSize's
                        // fixed 132px box fills it exactly. Parented beside the body zone so
                        // both resolve in the same (panel-fraction) space. The CTA build below
                        // seats the button here instead of carving the body well.
                        _compactCtaBand = MakeZone(bdy.parent, "Zone_CompactCta",
                            0.10f, CompactBodyFloorY,
                            0.90f, CompactBodyFloorY
                                   + ElarionUiKit.CanonCtaHeight / Mathf.Max(1f, panelPx));
                    }

                    _compactBodyFrac = Mathf.Max(0.01f, CompactBodyTopY - bodyFloor);
                    _wellPx = _compactBodyFrac * panelPx;
                    Canvas.ForceUpdateCanvases();
                    FlowTrace.Step("EndState",
                        $"compact banner geometry (WO-952 owned solve): panel={panelPx:0}px " +
                        $"(frac {panelFrac:0.###}, splash was {hNow:0.###}) body {bodyFloor:0.###}-" +
                        $"{CompactBodyTopY:0.###} = {_wellPx:0}px well, need={needPx:0}px, " +
                        $"ctaBand={(compactAnyCta ? ElarionUiKit.CanonCtaHeight : 0f):0}px" +
                        (panelFrac >= topY - CompactGrowthFloorY - 0.0005f
                            ? " (AT THE GROWTH CLAMP)" : string.Empty));
                }
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
            // WO-952: the owned compact solve minted a dedicated CTA band below the body
            // well — use it. FrameCore has no footer zone, so the legacy carve stole 16% of
            // the body well the solve had just sized to EXACTLY the content's need.
            RectTransform footer     = !anyCta ? null
                                     : _compactCtaBand != null ? _compactCtaBand
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
                0f, (hasFooterZone || !anyCta || _compactCtaBand != null) ? 0f : 0.22f, 1f, 1f);

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
                // OWNED GEOMETRY: the CTA band above was sized to EXACTLY CanonCtaHeight and the
                // body floor already sits CtaGapY above its top, so neither compensation can have
                // anything to do — and both of them re-derive fractions from creation-frame rects,
                // which is what made the reservations drift apart in the first place. Skip them.
                if (ownGeometry || _compactCtaBand != null)
                {
                    // WO-952: the compact owned band is sized to EXACTLY CanonCtaHeight against
                    // the final panel, same as the full modal's reclaimed close band — both
                    // compensations below re-derive fractions from creation-frame rects, which
                    // is the desync class this pass exists to end. Skip them.
                    FlowTrace.Step("EndState",
                        $"CTA seated in the {(ownGeometry ? "reclaimed close band" : "owned compact CTA band (WO-952)")} " +
                        $"(need={need:0}px, band=={ElarionUiKit.CanonCtaHeight:0}px) - no floor-raise required");
                }
                else if (!hasFooterZone)
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

            // POST-HOC PANEL EXTENSION — DELETED for the full modal (owner F8 2026-08-05).
            // It grew the frame root ~2x AFTER the kit's close-band reservation, the header band
            // and the CTA floor-raise had all been computed against the pre-growth panel, and it
            // recomputed NONE of them. That desynchronisation IS the compressed screen: the body
            // zone kept a fraction sized for the small panel, so the well resolved to ~17% of a
            // 907px panel and BuildBody scaled every band to 0.363. The full modal is now solved
            // to its final size in Show (PanelHalfHeight) and stamped once by the owned geometry
            // pass above — there is nothing left to extend.
            //
            // The COMPACT banner's downward growth — WO-952: SUPERSEDED by the owned compact
            // solve whenever the frame zones exist (_wellPx > 1): the banner is now BUILT at
            // its final solved height, so there is nothing left to grow — and growing here
            // against fractions stamped for a different height is the exact desync the
            // 2026-08-10 capture measured (the stale 0.45 reservation scaled up with the
            // panel). This block remains ONLY for the art-less procedural fallback panel
            // (chrome.layout == null), where no solve ran and the live rect is all there is.
            if (vm.Compact && chrome.root != null && _wellPx <= 1f)
            {
                Canvas.ForceUpdateCanvases();
                // Prefer the well the reclaim pass SOLVED (reference px). Falls back to the
                // live rect only on the CTA path, where no reclaim ran — unchanged behaviour.
                float wellPx = _wellPx > 1f ? _wellPx : rewardWell.rect.height;
                float needPx = RequiredBodyPx(vm, _canvasH);
                if (wellPx > 1f && needPx > wellPx + 1f)
                {
                    var rootRt = (RectTransform)chrome.root.transform;
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
                        // Keep the SOLVED well in step with the panel it was solved against —
                        // otherwise BuildBody would measure the content against the PRE-growth
                        // height and report a compression that no longer exists.
                        if (_wellPx > 1f && _compactBodyFrac > 0f)
                            _wellPx = _compactBodyFrac * _canvasH * grownH;
                        FlowTrace.Step("EndState",
                            $"compact banner extended down for its rows: need={needPx:0}px well {wellPx:0}->" +
                            $"{(_wellPx > 1f ? _wellPx : rewardWell.rect.height):0}px h {hNow:0.###}->{grownH:0.###}" +
                            (grownH >= y1 - 0.08f - 0.0005f ? " (AT THE GROWTH CLAMP)" : string.Empty));
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
        //
        // OWNER F8 2026-08-05: EVERY constant here except the emblem was SMALLER than the fixed
        // content it has to seat, so even at scale 1.0 the band overflowed onto its neighbour —
        // the stars printed through the Time line and the row icons hung off their plates. Each
        // one is now >= its own content's fixed size (and the emblem, which was the only
        // over-budgeted band, gives its surplus back):
        private const float EmblemPx  = 64f;   // 80 -> 64: the emblem scales, it never needed 80
        private const float SubLinePx = 60f;   // 54 -> 60: FontBody 50 line box is ~57.5px
        // WO-894 §3: 48 -> 72. The 48 was budgeted for the 45-deg DIAMOND's rotated bbox. A real
        // 5-point star is RADIALLY bounded (it rotates inside its own circumscribed circle, so the
        // bbox never grows during the spin) — the band instead has to seat the 56px hero star PLUS
        // the §4.2 overshoot: 56 x 1.15 = 64.4px at the pop's peak, which 72 clears with 7.6px spare.
        private const float StarsPx   = 72f;   // 48 -> 72: 56px hero star + the 1.15x spin overshoot
        private const float TimePx    = 48f;   // 44 -> 48: FontLabel 40 bold line box is ~46px
        private const float RowPx     = 64f;   // 56 -> 64: seats the fixed 40px icon + plate inset
        private const float BandGapPx = 8f;

        /// <summary>Total body-well pixels the VM's bands demand (drives the panel solve).
        /// <paramref name="canvasH"/> is the post-scale canvas height — the subtitle's wrapped
        /// line count depends on the real column width, which depends on it.</summary>
        private static float RequiredBodyPx(EndStateVM vm, float canvasH)
        {
            float px = 0f; int n = 0;
            if (vm.Emblem != null) { px += EmblemPx; n++; }
            if (!string.IsNullOrEmpty(vm.Subtitle)) { px += SubLinePx * SubtitleLines(vm.Subtitle, canvasH, PanelWidthFracFor(vm)); n++; }
            if (vm.Stars >= 0) { px += StarsPx; n++; }
            if (vm.TimeSeconds >= 0f) { px += TimePx; n++; }
            int spoilBands = SpoilBandCount(vm, canvasH);
            px += spoilBands * RowPx; n += spoilBands;
            if (n > 1) px += BandGapPx * (n - 1);
            return px;
        }

        // ── SPOILS COLUMNS (WO-894, orchestrator ruling — a DELIBERATE, DOCUMENTED deviation
        //    from the WO's §2 wireframe, which draws spoils as one vertical list) ──────────
        // WHY: the wireframe was drawn without knowing the content does not fit the surface.
        // Measured at 2670x1200 (post-scale canvas 965.4 x 2148.0 ref px), an arena win with
        // five spoils rows demands a 1027 ref px panel on a 965 ref px canvas — the content is
        // literally TALLER THAN THE SCREEN, so it hit the MaxPanelHalf clamp and every band was
        // squashed to 0.859. No spacing tweak can fix that; each one only moves the squeeze.
        // Two columns takes the spoils stack 320px -> 192px and the whole body 628px -> 484px,
        // which solves to an 832px panel UNCLAMPED at scale 1.000 — the only lever that clears
        // it without shrinking the header band (which failed exactly this way on 2026-07-08,
        // rendering zero title glyphs).
        // It is also the right SHAPE: a single narrow column of five rows starves the axis we
        // have most of (2148px of width) to overflow the one we have least.
        // LANDSCAPE ONLY — see MinSpoilColumnPx.

        /// <summary>Legibility floor for ONE spoils column, in reference px. DERIVED, not picked:
        /// the label column is (0.62 - labelLeft) of the plate, the plate is 0.88 of the cell, and
        /// the label's fixed furniture is 72px (18 inset + 40 icon + 14 gap). The longest stock
        /// reward label, "Experience", measures ~250px at FontBody 50, so it stays above the
        /// FontFloor(30) only while 0.62 * plate - 72 >= 250 * 30/50 = 150px, i.e. plate >= 358px,
        /// i.e. cell >= 407px. 420 keeps a margin. At 2670x1200 a column is 535px (28% clear);
        /// in portrait it is 269px, so portrait stays SINGLE-COLUMN as ruled.</summary>
        private const float MinSpoilColumnPx = 420f;

        /// <summary>Body-well width in reference px (the full width a spoils band spans).
        /// <paramref name="panelWidthFrac"/> = this screen's canvas-width fraction
        /// (<see cref="PanelWidthFracFor"/> — WO-952: the banner is 0.70, the modal 0.56).</summary>
        private static float SpoilsBodyWidthPx(float canvasH, float panelWidthFrac)
        {
            return PostScaleCanvasWidth(canvasH) * panelWidthFrac * BodyZoneWidthFrac;
        }

        /// <summary>Spoils columns for this screen: 2 only when a column clears
        /// <see cref="MinSpoilColumnPx"/>. The test is on the derived WIDTH, so it keys off the
        /// real aspect ratio and never off a hardcoded resolution. Compact banners stay single
        /// (their damage-report rows carry long "Rebuild 120 wood, 40 iron" amounts), and a lone
        /// reward stays single (one half-width plate beside nothing reads as a broken row).</summary>
        private static int SpoilColumns(EndStateVM vm, float canvasH)
        {
            if (vm == null || vm.Compact || vm.Spoils.Count < 2) return 1;
            return SpoilsBodyWidthPx(canvasH, PanelWidthFracFor(vm)) * 0.5f >= MinSpoilColumnPx ? 2 : 1;
        }

        /// <summary>How many BANDS the spoils occupy — the number the panel solve must budget.</summary>
        private static int SpoilBandCount(EndStateVM vm, float canvasH)
        {
            if (vm == null || vm.Spoils.Count == 0) return 0;
            return Mathf.CeilToInt(vm.Spoils.Count / (float)SpoilColumns(vm, canvasH));
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
                bands.Add((SubLinePx * SubtitleLines(vm.Subtitle, _canvasH, PanelWidthFracFor(vm)), host =>
                {
                    var l = ElarionUiKit.Label(host, vm.Subtitle, 0f, 1f, ElarionUi.Parchment,
                        ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
                    // OWNER F8 2026-08-05 (the subtitle painted over the emblem above it): the raw
                    // enableAutoSizing path below does NOT set an overflow mode, and TMP's default
                    // Overflow RENDERS OUTSIDE THE RECT — so once the band was shorter than the
                    // wrapped copy the text simply escaped upward. FitBlock is the kit's bounded
                    // block fitter: normal wrap + bounded auto-size + TextOverflowModes.Truncate
                    // (ElarionUiKitObsidian.cs:2609-2623), so the copy is now STRUCTURALLY unable
                    // to paint on a sibling. With SubLinePx 60 >= the 50pt line box it never has to.
                    ElarionUiKit.FitBlock(l);
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

            // Spoils: one band per ROW of the grid (2 columns in landscape, 1 in portrait).
            // SpoilBandCount is the same function RequiredBodyPx budgeted with, so the panel
            // solve and the layout can never disagree about how many bands there are.
            int spoilCols = SpoilColumns(vm, _canvasH);
            float spoilBodyPx = SpoilsBodyWidthPx(_canvasH, PanelWidthFracFor(vm));
            int spoilBands = SpoilBandCount(vm, _canvasH);
            for (int b = 0; b < spoilBands; b++)
            {
                int bandIdx = b;
                bands.Add((RowPx, host => BuildSpoilBand(host, vm, bandIdx, spoilCols, spoilBodyPx)));
            }

            // Lay the bands out top-down at their OWN pixel heights. Only when the well is
            // still shorter than the total (panel extension clamped at 94% screen height)
            // do all bands compress by one uniform factor — logged, never silent.
            if (bands.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            // OWNED GEOMETRY: use the well height the geometry pass SOLVED, not a creation-frame
            // rect read (that returns raw screen px before the CanvasScaler applies —
            // ElarionUiKit.cs:1014-1018). Compact banners still measure, as before.
            float wellH = _wellPx > 1f ? _wellPx : body.rect.height;
            float totalPx = BandGapPx * (bands.Count - 1);
            foreach (var b in bands) totalPx += b.px;
            float scale = wellH > 1f && totalPx > wellH ? wellH / totalPx : 1f;
            // ERROR, not a warning (owner F8 2026-08-05). Compression means EVERY band resolves
            // BELOW its own content's fixed size — the subtitle under its line box, the diamonds
            // under their rotated bbox, the rows under their icon. A screen that ships like that
            // is broken, and a Warn is exactly how it shipped unnoticed. Fail is loud, and the F8
            // harness captures it.
            //
            // EPSILON (owner captures 2026-08-08, twice: "need=412px well=412px scale=1").
            // BOTH solves are SELF-FITTING: the full modal sets panelPx = (need + CanonCta) /
            // BodyFracOfPanel and then derives wellPx back out of it, and the compact banner
            // grows by exactly need/well — so when neither hits its clamp the well resolves to
            // need to within float residue, and `scale < 1f` tripped a FAIL at scale 0.9997.
            // That is why the captured line printed IDENTICAL need and well numbers: a real
            // clamp leaves them different (a clamped well is visibly SHORTER than the need).
            // A hairline residue is not "every band below its content size" — it is the solve
            // landing on target. Fail below 0.995 (a real clamp lands far below that: the
            // 8-row damage report measured 0.71, the F8-35 case 0.36); log the exact fit as a
            // Step so the number is still on the record.
            const float CompressFailBelow = 0.995f;
            if (scale < CompressFailBelow)
                FlowTrace.Fail("EndState",
                    $"body rows COMPRESSED to fit: need={totalPx:0}px well={wellH:0}px scale={scale:0.###} " +
                    "- the panel hit its screen-height clamp; every band is now below its own content size");
            else if (scale < 1f)
                FlowTrace.Step("EndState",
                    $"body solved to an EXACT fit: need={totalPx:0.#}px well={wellH:0.#}px scale={scale:0.#####} " +
                    "- float residue from the self-fitting solve, not a clamp");
            float y = 0f;
            foreach (var (px, build) in bands)
            {
                var host = MakeZonePx(body, "Band", y, px * scale);
                y += (px + BandGapPx) * scale;
                build(host);
            }
        }

        /// <summary>One spoils BAND — up to <paramref name="cols"/> reward rows side by side.
        ///
        /// FILL ORDER = ROW-MAJOR (across, then down). The VM builds Spoils in descending
        /// importance (Experience, Wisdom, Wood, Iron, gear), and row-major is the order the eye
        /// already reads: it is the single vertical list of the wireframe simply folded, so
        /// "earlier = higher up, then left" still holds. Column-major (down, then across) would
        /// require the player to know the TOTAL count to know where the left column stops, which
        /// is unreadable at 2-3 rows.
        ///
        /// ODD TAIL: a lone final reward spans the FULL band width rather than sitting in a half
        /// cell beside an empty one. An empty cell reads as a reward that failed to load — the
        /// exact "icons are missing" complaint this WO is already fixing. A full-width capstone
        /// reads as deliberate, and on an arena win the odd tail IS the gear drop, the most
        /// notable line on the screen. So 5 rewards lay out as [1][2] / [3][4] / [ 5 ].</summary>
        private void BuildSpoilBand(RectTransform host, EndStateVM vm, int bandIdx, int cols, float bodyWidthPx)
        {
            if (vm == null || cols < 1) return;
            int first = bandIdx * cols;
            int remaining = vm.Spoils.Count - first;
            if (remaining <= 0) return;
            int inBand = Mathf.Min(cols, remaining);
            bool fullWidth = inBand == 1;

            for (int c = 0; c < inBand; c++)
            {
                int itemIdx = first + c;
                // Cells split the band evenly with NO explicit gutter: each plate is already
                // inset 0.06 of its own cell, so two neighbours leave ~2x6% of clear space
                // between them. One less constant, and the single-column look is unchanged.
                float x0 = fullWidth ? 0f : c / (float)cols;
                float x1 = fullWidth ? 1f : (c + 1) / (float)cols;
                float cellPx = fullWidth ? bodyWidthPx : bodyWidthPx / cols;
                var cell = MakeZone(host, "SpoilCell" + itemIdx, x0, 0f, x1, 1f);
                int captured = itemIdx;
                // Stagger stays keyed to the ITEM index, so the reveal still sweeps in reading
                // order and the CTA (Track'd at Spoils.Count * 0.05) still lands last.
                Guard.Try("EndState", "spoils row " + captured,
                    () => BuildSpoilRow(cell, vm.Spoils[captured], 0.25f + captured * 0.05f, cellPx));
            }
        }

        /// <summary>The concept ids a spoils row offers the icon table, best first: its own label,
        /// then the DE-PLURALISED label. Both are the row's OWN text — no icon name is chosen in
        /// C#, the table still decides (ConceptIconResolver.ResolveAny takes the first that
        /// resolves, and Resolve(null) is a no-op, so a singular label costs nothing).
        ///
        /// The plural is why the raid victory's crystal row was broken: FromRaidVictory labels it
        /// "Crystals" (EndStateVM.cs:302) but concept-icons.json is keyed "crystal"
        /// (concept-icons.json:209), so it missed the table and fell through to icon_inventory —
        /// a CHEST (RpgUiCatalog.cs:220). The plural keys are now IN the table too (both the
        /// Resources and StreamingAssets copies); this stays as the belt to that braces, so a
        /// future plural label resolves even before someone remembers to add the key.</summary>
        private static string[] RowConcepts(SpoilRowVM row)
        {
            string concept = (row != null ? row.Label ?? "" : "").Trim().ToLowerInvariant();
            string singular = concept.Length > 3 && concept.EndsWith("s", StringComparison.Ordinal)
                ? concept.Substring(0, concept.Length - 1)
                : null;
            return new[] { concept, singular };
        }

        // The row's horizontal furniture in FIXED reference px. These were fractions of the plate,
        // which is why a 40px icon sat 105px away from its label on the wide landscape plate (the
        // 0.17 label inset resolved to ~160px) while being correct in portrait. A fraction cannot
        // hold a constant gap across a 2x width change; pixels can. Now the icon-to-label gap is
        // 14px on EVERY plate width, which matters far more with two columns halving the plate.
        private const float SpoilIconPx      = 40f;   // the fixed reward icon square (unchanged)
        private const float SpoilIconInsetPx = 18f;   // icon's left inset inside the plate
        private const float SpoilIconGapPx   = 14f;   // icon -> label
        private const float SpoilEdgeInsetPx = 18f;   // amount's right inset inside the plate

        /// <summary>One spoils row: kit slot plate + icon (null-safe) + label + amount.
        /// <paramref name="cellWidthPx"/> is the row's own cell width in reference px (a full
        /// body well in one column, half of it in two) — it converts the pixel insets above into
        /// the fractions the anchors need.</summary>
        private void BuildSpoilRow(RectTransform host, SpoilRowVM row, float revealDelay, float cellWidthPx)
        {
            if (row == null) return;
            // The plate spans 0.06..0.94 of the cell, so it is 0.88 of it. Floored so a bad
            // measurement can never produce inset fractions above 1 (which would invert a rect).
            float platePx = Mathf.Max(200f, cellWidthPx * 0.88f);
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
            // WO-894: the reward CONCEPT gets first refusal via the resolver's designed OPT-IN
            // path. ResolveAnyOverride returns a sprite ONLY for entries flagged `override:true`
            // in concept-icons.json (ConceptIconResolver.cs:136-152), so today it returns null for
            // every reward row and nothing changes — but it means any wrong reward icon can be
            // repointed by adding ONE data entry, with no C# change and no icon name chosen in
            // code. That is the lever for "Wisdom" (see the RESULT notes: it currently shows
            // icon_tree, which RpgUiCatalog.cs:226 itself documents as a campfire stand-in, and
            // Resources holds no sprite that reads as wisdom to swap it for).
            var iconSprite = ConceptIconResolver.ResolveAnyOverride(RowConcepts(row));
            if (iconSprite == null) iconSprite = row.Icon;
            // SWEEP 9413 R2 (#7): the generic IconInventory fallback painted a plain yellow
            // square beside every art-less reward line. Resolve the reward CONCEPT icon first
            // (concept-icons.json maps gold/wood/iron → the currency sprites) from the row label;
            // only then the generic kit fallback — never a bare placeholder square.
            // WO-894 (owner F8 "reward icons are broken/missing"): the row LABEL is a plural
            // display string — FromRaidVictory sets Label = "Crystals" (EndStateVM.cs:302) — but
            // concept-icons.json is keyed SINGULAR ("crystal", concept-icons.json:209). "crystals"
            // therefore missed the table outright and fell through to the generic fallback, which
            // is icon_inventory — a CHEST (RpgUiCatalog.cs:220). That is the broken icon: not
            // missing art (currency_crystal.png is committed and imported as a Sprite), a missed
            // LOOKUP. Offer the singular as a second CANDIDATE; ResolveAny exists precisely for
            // "let the DATA decide which of these ids resolves", so no icon name is chosen in C#.
            if (iconSprite == null)
                iconSprite = ConceptIconResolver.ResolveAny(RowConcepts(row));
            if (iconSprite == null)
                iconSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconInventory);
            // Icon size first: the label's left inset is measured from the icon's REAL right
            // edge, so a band-clamped (compressed) icon does not leave a hole beside itself.
            float iconPx = Mathf.Min(SpoilIconPx, host.rect.height * 0.80f);
            float labelLeftFrac = iconSprite != null
                ? (SpoilIconInsetPx + iconPx + SpoilIconGapPx) / platePx
                : SpoilIconInsetPx / platePx;
            float amountRightFrac = 1f - SpoilEdgeInsetPx / platePx;

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
                float iconLeftFrac = SpoilIconInsetPx / platePx;
                rt.anchorMin = new Vector2(iconLeftFrac, 0.5f);
                rt.anchorMax = new Vector2(iconLeftFrac, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                // OWNER F8 2026-08-05 (icons floating off their bars): the 40px square is
                // vertically CENTRED, so in a row plate that resolved to 18.7px it spilled
                // ~10.6px above AND below the bar. 40 is still the size we want — RowPx 64
                // seats it natively — but clamp it to the host band so a compressed row can
                // never put the icon outside its own plate again.
                rt.sizeDelta = Vector2.one * iconPx;
            }
            // F8-35: label left / value right, each FIT to ONE line in its own column —
            // "Equipped" wrapped to "Equipp/d" and long gear names spilled into the value
            // column at the fixed FontBody size. FitSingleLine (§1.14) shrinks-to-fit with
            // ellipsis so neither side can ever wrap or cross the column split again.
            var label = ElarionUiKit.Label(plate.transform, row.Label ?? "", 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft,
                labelLeftFrac, 0.62f);
            ElarionUiKit.FitSingleLine(label);
            label.raycastTarget = false;
            var amount = ElarionUiKit.Label(plate.transform, row.Amount ?? "", 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineRight,
                0.64f, amountRightFrac, bold: true);
            ElarionUiKit.FitSingleLine(amount);
            amount.raycastTarget = false;
            Track(plate, revealDelay, 0.96f);
        }

        // ── ④ STAR ROW (WO-894) ───────────────────────────────────────────────────
        // Every number here is the WO's §3 spacing table / §4.2 spin table, named so a
        // future reader can diff the code against the spec without re-deriving anything.
        private const float StarSizePx        = 56f;    // §3: star diameter (square bbox 56x56)
        private const float StarSpacingPx     = 80f;    // §3: centre-to-centre => centres at -80 / 0 / +80
        private const float StarsBaseDelay    = 0.18f;  // §4.3: between the subtitle (0.14) and time (0.20) beats
        private const float StarStaggerSec    = 0.15f;  // §4.2: star i starts at base + i * 0.15, left->right
        private const float StarSpinSec       = 0.40f;  // §4.2: rotation + scale duration
        private const float StarSpinDegrees   = 540f;   // §4.2: +540 -> 0 = 1.5 clockwise turns
        private const float StarFadeSec       = 0.12f;  // §4.2: alpha 0 -> 1 over the first 0.12s, linear
        private const float StarLandPulseSec  = 0.12f;  // §4.2: the landing stamp
        private const float StarLandPulse     = 0.08f;  // §4.2: 1.0 -> 1.08 -> 1.0
        private const float StarTwinkleAmp    = 0.03f;  // §4.2: idle +-3% scale...
        private const float StarTwinkleHz     = 0.5f;   // §4.2: ...at ~0.5 Hz (NEVER a perpetual full spin)
        private const float StarOvershootC1   = 2.17f;  // ease-out-back constant solved for a 1.15 peak (see EaseOutBack)
        private const float UnearnedStarAlpha = 0.14f;  // §4.1: dim OUTLINE variant at ~14%
        private const float UnearnedFadeSec   = 0.20f;  // §4.1: unearned stars fade in, they never spin

        /// <summary>Rating row (WO-894): three REAL 5-point stars — earned ones SPIN in
        /// (540deg -> 0) with an overshoot pop, staggered left-to-right, then land and settle
        /// into a gentle twinkle; unearned ones are a dim OUTLINE star that only fades.
        ///
        /// The old pips were 45-degree rotated squares (diamonds). That was NOT a font
        /// fallback — it was a deliberate sprite-free workaround for the build font having no
        /// TMP star glyph. See <see cref="StarSolidSprite"/> for why the replacement is a
        /// generated sprite rather than a glyph or a pack asset.
        ///
        /// COLOURBLIND LAW (owner is red/green colourblind): the rating reads by the NUMBER OF
        /// FILLED SHAPES and by SHAPE (solid star vs hollow outline) — never by hue. There is
        /// deliberately no "n/3" numeral on this row either: the HUD font renders the numeral 1
        /// as a bare vertical stroke, which would be unreadable beside a slash or a star point.</summary>
        private void BuildStarRow(RectTransform host, int stars)
        {
            var rowGo = new GameObject("Stars", typeof(RectTransform));
            rowGo.transform.SetParent(host, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = Vector2.zero; rowRt.anchorMax = Vector2.one;
            rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;

            // DEGRADE LADDER (orchestrator ruling: the row must never VANISH — a rating that
            // silently disappears is worse than diamonds, because the player cannot tell 3 stars
            // from 0). §12 no silent failure: every rung logs.
            //   1. the generated 5-point star        (the deliverable)
            //   2. the kit's circular pip            (a real sprite, still a countable shape)
            //   3. the legacy 45-degree square       (the pre-WO diamond — ugly, but VISIBLE)
            var solid   = StarSolidSprite;
            var outline = StarOutlineSprite;
            bool legacyDiamond = false;
            if (solid == null)
            {
                solid = ElarionUiKit.CircleSprite;
                outline = solid;
                FlowTrace.Fail("EndState", solid != null
                    ? "star sprite build failed - degraded to the kit circle pip"
                    : "star AND circle sprite builds both failed - degraded to the legacy 45deg square");
                legacyDiamond = solid == null;
            }
            if (outline == null) outline = solid;

            // §3 wants a FIXED 56px star. Clamp it to the band anyway: when BuildBody's
            // uniform compression fires (it logs a Fail when it does) the band resolves BELOW
            // 72px, and a hard 56 would then overhang its own band and print through the Time
            // line above/below — the very defect the 2026-08-05 pass fixed for the diamonds.
            // 72 * 0.78 = 56.16, so at the authored band size this yields exactly the §3 56px.
            float size = Mathf.Max(8f, Mathf.Min(StarSizePx, host.rect.height * 0.78f));
            // A star (and a circle) is RADIALLY bounded, so its bbox is its size. A rotated
            // SQUARE is not: its axis-aligned box is side * sqrt(2), so the legacy rung has to
            // shrink or it overprints its neighbours (the original 2026-08-05 defect).
            if (legacyDiamond) size /= 1.414f;

            for (int i = 0; i < 3; i++)
            {
                bool earned = i < stars;
                var go = new GameObject("Star" + i, typeof(Image));
                go.transform.SetParent(rowRt, false);
                var img = go.GetComponent<Image>();
                // On the legacy rung sprite stays NULL on purpose: a sprite-less Image draws a
                // white quad, which rotated 45deg is exactly the old diamond. Visible beats absent.
                img.sprite = earned ? solid : outline;
                img.preserveAspect = true;
                img.raycastTarget = false;   // §4.1: the rating is never a touch target
                if (legacyDiamond) img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                // Earned = solid GOLD star; unearned = the hollow outline at ~14%. The hue is the
                // decoration; the SHAPE and the COUNT carry the meaning (colourblind law).
                var tint = earned ? ElarionUiKit.ObsidianTrim
                                  : new Color(1f, 1f, 1f, UnearnedStarAlpha);
                img.color = new Color(tint.r, tint.g, tint.b, 0f);   // the reveal owns alpha

                // §3 EXACT: anchor + pivot dead-centre, centres at anchoredPosition.x -80 / 0 / +80.
                // FIXED PIXELS, never a fraction of the parent — the previous 0.13-of-width spacing
                // resolved to ~144px on the 2670x1200 landscape panel and ~70px on a narrow one, so
                // the group was a different shape on every device.
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - 1) * StarSpacingPx, 0f);
                rt.sizeDelta = new Vector2(size, size);

                // §4.4: earned stars own their OWN tween — they must NOT ride the generic
                // Track/RevealRoutine, which has no rotation curve. Unearned stars just fade.
                // On the legacy rung NOTHING spins: the spin lands at rotation 0, which would
                // straighten the diamond back into a square mid-animation.
                float delay = StarsBaseDelay + i * StarStaggerSec;
                if (earned && !legacyDiamond) StartCoroutine(SpinStarIn(img, rt, delay, tint.a));
                else                          StartCoroutine(FadeStarIn(img, delay, tint.a));
            }

            FlowTrace.Step("EndState",
                $"star row: {Mathf.Clamp(stars, 0, 3)}/3 earned, star={size:0.#}px (band {host.rect.height:0.#}px) " +
                $"centres -{StarSpacingPx:0}/0/+{StarSpacingPx:0}px, spin {StarSpinDegrees:0}deg over {StarSpinSec:0.00}s");
        }

        // ── the SPIN (WO-894 §4.2) ────────────────────────────────────────────────
        // All on Time.unscaledDeltaTime: this screen never pauses time, and the hero-death
        // variant narrates a coroutine that runs on SCALED time — same rule as RevealRoutine.

        /// <summary>Ease-out-back with the overshoot constant SOLVED for the WO's exact 1.15 peak.
        /// The textbook c1 = 1.70158 only reaches 1.099. The peak of this curve is
        /// 1 + 4c1^3 / (27(c1+1)^2), which equals 1.150 at c1 = 2.17. f(0) = 0 and f(1) = 1 hold
        /// for any c1, so the "0.0 -> 1.15 -> 1.0" of the spec is exact, not approximated.</summary>
        private static float EaseOutBack(float u)
        {
            const float c1 = StarOvershootC1;
            const float c3 = c1 + 1f;
            float p = u - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        /// <summary>One EARNED star: spin 540deg -> 0 (ease-out-cubic) while popping 0 -> 1.15 -> 1
        /// (ease-out-back) and fading in over the first 0.12s, then a land pulse, then a gentle
        /// forever-twinkle. Never a continuous full spin — that reads as a loading spinner.</summary>
        private IEnumerator SpinStarIn(Image img, RectTransform rt, float delay, float targetAlpha)
        {
            if (img == null || rt == null) yield break;
            rt.localScale = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, StarSpinDegrees);

            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                if (img == null || rt == null) yield break;   // torn down before its turn
                yield return null;
            }

            t = 0f;
            while (t < StarSpinSec)
            {
                t += Time.unscaledDeltaTime;
                if (img == null || rt == null) yield break;
                float u = Mathf.Clamp01(t / StarSpinSec);
                float spun = 1f - Mathf.Pow(1f - u, 3f);      // ease-out-cubic on the rotation
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(StarSpinDegrees, 0f, spun));
                rt.localScale = Vector3.one * EaseOutBack(u);
                var c = img.color;
                c.a = targetAlpha * Mathf.Clamp01(t / StarFadeSec);   // linear, first 0.12s
                img.color = c;
                yield return null;
            }
            if (img == null || rt == null) yield break;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            var landed = img.color; landed.a = targetAlpha; img.color = landed;

            // LAND PULSE: a half-sine stamp so the star arrives with weight.
            t = 0f;
            while (t < StarLandPulseSec)
            {
                t += Time.unscaledDeltaTime;
                if (rt == null) yield break;
                float u = Mathf.Clamp01(t / StarLandPulseSec);
                rt.localScale = Vector3.one * (1f + StarLandPulse * Mathf.Sin(Mathf.PI * u));
                yield return null;
            }

            // IDLE TWINKLE: +-3% at ~0.5 Hz, forever. Cheap (one scale write/frame/star) and it
            // stops on its own when the screen tears down (the coroutine host dies with it).
            t = 0f;
            while (rt != null)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one *
                    (1f + StarTwinkleAmp * Mathf.Sin(2f * Mathf.PI * StarTwinkleHz * t));
                yield return null;
            }
        }

        /// <summary>One UNEARNED star (§4.1): a plain 0.2s fade to the dim alpha at its own slot.
        /// No spin — only what you EARNED spins, so the count reads from the motion too.</summary>
        private IEnumerator FadeStarIn(Image img, float delay, float targetAlpha)
        {
            if (img == null) yield break;

            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                if (img == null) yield break;
                yield return null;
            }

            t = 0f;
            while (t < UnearnedFadeSec)
            {
                t += Time.unscaledDeltaTime;
                if (img == null) yield break;
                var c = img.color;
                c.a = targetAlpha * Mathf.Clamp01(t / UnearnedFadeSec);
                img.color = c;
                yield return null;
            }
            if (img == null) yield break;
            var done = img.color; done.a = targetAlpha; img.color = done;
        }

        // ── the STAR SPRITE (WO-894 §4.1) ─────────────────────────────────────────
        // WHY GENERATED, and not a glyph or a pack asset — verified in the tree, not assumed:
        //   • NO star sprite is reachable at runtime. RpgUiCatalog exposes crown_tier1..3
        //     (Resources/RpgUi/crown/) and no star role at all; the only star*.png in the
        //     project live in VFX packs (Hovl / Lana / Mirza) OUTSIDE Assets/Resources, so
        //     Resources.Load can never see them, and they are soft particle glows, not UI art.
        //   • A TMP star glyph is out: the build font tofu'd it (that tofu is the whole reason
        //     the row was drawing rotated squares in the first place).
        //   • The crown art is out: it carries a white fringe (owner F8).
        // So the star is BUILT — the same lazily-cached, try/catch-guarded, null-safe way the kit
        // builds its own rounded / circle / ring sprites (ElarionUiKit.cs:2288-2424). No import
        // step, no missing-art path, and it renders identically in a player build.
        // KIT-PROMOTION CANDIDATE (alongside RevealRoutine) once a second screen needs a star.

        private const int   StarTexSize    = 128;    // texture px; drawn at 56 ref px, so ~2x for crisp points
        private const float StarInnerRatio = 0.45f;  // inner/outer radius — fatter than a pentagram (0.382)
                                                     // so the points stay solid and legible at phone size
        private const float StarStrokePx   = 5f;     // outline-variant stroke, texture px (~2.2px at 56)

        private static Sprite _starSolid;   private static bool _starSolidTried;
        private static Sprite _starOutline; private static bool _starOutlineTried;

        /// <summary>Filled 5-point star, white with a baked top-lit bevel so an
        /// <c>Image.color</c> gold tint reads as gilded metal. Null only if texture creation
        /// itself failed (caller falls back — never a white quad).</summary>
        private static Sprite StarSolidSprite
        {
            get
            {
                if (!_starSolidTried)
                {
                    _starSolidTried = true;
                    try { _starSolid = BuildStarSprite(false); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[EndState] solid star sprite build failed: " + e.Message);
                        _starSolid = null;
                    }
                }
                return _starSolid;
            }
        }

        /// <summary>Hollow 5-point star (stroke only) — the UNEARNED slot. A different SHAPE, not
        /// just a dimmer colour, so the earned count survives any colour perception.</summary>
        private static Sprite StarOutlineSprite
        {
            get
            {
                if (!_starOutlineTried)
                {
                    _starOutlineTried = true;
                    try { _starOutline = BuildStarSprite(true); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[EndState] outline star sprite build failed: " + e.Message);
                        _starOutline = null;
                    }
                }
                return _starOutline;
            }
        }

        private static Sprite BuildStarSprite(bool hollow)
        {
            const int size = StarTexSize;
            const float half = size * 0.5f;
            const float outerPx = half - 3f;   // 3px margin for the AA ramp / bevel

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                // Baked top-lit ramp: bright at the top point, ~0.72 at the bottom points. It is a
                // MULTIPLIER on Image.color, so the gold tint comes out as a gilded gradient rather
                // than a flat sticker. The outline variant stays flat white (it is barely visible
                // at 14% alpha; a gradient there would just make it read as noise).
                float lumRow = hollow ? 1f : Mathf.Lerp(0.72f, 1f, (y + 0.5f) / size);
                for (int x = 0; x < size; x++)
                {
                    // Evaluate the SDF in UNIT space (outer radius 1.0) then scale back to texture
                    // px, so the 1px alpha ramp below is a true 1px feather at any texture size.
                    float d = Star5Distance((x + 0.5f - half) / outerPx,
                                            (y + 0.5f - half) / outerPx,
                                            StarInnerRatio) * outerPx;

                    float a = hollow
                        ? Mathf.Clamp01(StarStrokePx * 0.5f - Mathf.Abs(d) + 0.5f)   // ring around the edge
                        : Mathf.Clamp01(0.5f - d);                                   // solid interior
                    if (a <= 0f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // Soft ~2px bevel: darken the last band inside the edge so the star has a rim
                    // and does not dissolve into a bright panel.
                    float lum = lumRow;
                    if (!hollow && d > -2.5f) lum *= 0.62f;

                    byte c8 = (byte)Mathf.Clamp(Mathf.RoundToInt(lum * 255f), 0, 255);
                    px[y * size + x] = new Color32(c8, c8, c8,
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Signed distance from (<paramref name="x"/>,<paramref name="y"/>) to a regular
        /// POINT-UP 5-pointed star of outer radius 1 and inner/outer ratio <paramref name="inner"/>;
        /// negative inside. The standard polar-fold star field: mirror in x, reflect across the two
        /// pentagon edge normals (cos/sin of pi/5), mirror again — which folds the whole plane onto
        /// ONE star edge, so the answer is the exact distance to that single segment. Exact distance
        /// (not a bounded approximation) is what lets a flat 1px alpha ramp antialias all five points
        /// and all five notches cleanly.</summary>
        private static float Star5Distance(float x, float y, float inner)
        {
            const float k1x = 0.809016994f, k1y = -0.587785252f;   // cos(pi/5), -sin(pi/5)
            const float k2x = -k1x, k2y = k1y;

            x = Mathf.Abs(x);
            float d1 = x * k1x + y * k1y;
            if (d1 > 0f) { x -= 2f * d1 * k1x; y -= 2f * d1 * k1y; }
            float d2 = x * k2x + y * k2y;
            if (d2 > 0f) { x -= 2f * d2 * k2x; y -= 2f * d2 * k2y; }
            x = Mathf.Abs(x);
            y -= 1f;                                   // origin -> the star's tip

            // The one surviving edge: tip (0,0) -> inner vertex, at radius `inner` and 54deg.
            float bax = inner * -k1y;                  // = inner * sin(pi/5)
            float bay = inner * k1x - 1f;
            float h = Mathf.Clamp01((x * bax + y * bay) / (bax * bax + bay * bay));
            float dx = x - bax * h, dy = y - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Sign(y * bax - x * bay);
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
            // tear down WITHOUT firing the primary route.
            //
            // Section 12 - this used to be SILENT, and that silence cost us. An end-state torn
            // down here takes its Primary action with it; when that action was an arena's only
            // route home, the player was stranded and NOTHING in the log said why.
            // Destroying without firing is still CORRECT (a displaced end-state must never
            // silently trigger continue/respawn) - it just must never be invisible.
            AbandonedPrimaryWarn("OnSceneLoaded (scene '" + s.name + "' loaded under the panel)");
            _fired = true;
            Destroy(gameObject);
        }

        /// <summary>
        /// Section 12 - no silent failures. Announces that this end-state is being destroyed WITHOUT
        /// running its Primary action, and says loudly when that action was load-bearing. Whoever
        /// owns a route that matters must not delegate it to a UI object other systems can destroy;
        /// BattleArena's stranding watchdog exists because of exactly this.
        /// </summary>
        private void AbandonedPrimaryWarn(string reason)
        {
            bool hadPrimary = _vm != null && _vm.Primary != null && !_fired;
            string title = _vm != null ? _vm.Title : "?";
            if (hadPrimary)
                FlowTrace.Warn("EndState",
                    $"'{title}' destroyed WITHOUT firing its primary action - {reason}. " +
                    "That action is now abandoned. If it was an arena home-return, the player is " +
                    "stranded until BattleArena's watchdog fires.");
            else
                FlowTrace.Step("EndState",
                    $"'{title}' torn down ({reason}) - no primary action pending, nothing abandoned.");
        }

        /// <summary>HUD-2: the single-modal arbiter swapped us out (another modal opened over the
        /// end-state). Tear down WITHOUT firing the primary route — a displaced end-state must not
        /// silently trigger continue/respawn (mirrors <see cref="OnSceneLoaded"/>).</summary>
        private void CloseFromArbiter()
        {
            // Section 12: was SILENT. This fires whenever ANY other modal opens over the
            // end-state (PanelManager.NotifyOpened), so it is the widest of the three
            // abandon paths - and it left no trace at all.
            AbandonedPrimaryWarn("CloseFromArbiter (another modal opened over this end-state)");
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
