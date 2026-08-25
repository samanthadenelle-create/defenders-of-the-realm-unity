// =============================================================================
// RumorBoardPanel (WO-304 . WO-810 layout rework . WO-866 tab-band + chrome pass)
// Brom's rumor board: the BROWSE / ACCEPT surface for the realm's questlines.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// READ-ONLY consumer of RumorBoardVM (strict MVVM): the View renders VM
// projections and routes taps to Accept/Track/SetTab; it never touches
// QuestService / QuestCatalog / DailyQuestService.
//
// -----------------------------------------------------------------------------
// WO-866 (Seeker capture 2026-08-04, docs/ui-review/2026-08-04-seeker/04-rumor-board.png)
// -----------------------------------------------------------------------------
// TWO defects, both the SAME class (a band sized against something that is not a
// fixed pixel budget), both PROVEN by arithmetic that reproduces the capture:
//
//  1. THE CLIPPED TAB STRIP. The strip parents into chrome.layout.body, which on
//     FrameQuest is the DARK LEFT WELL ONLY (x 0.035-0.495 of the frame). At
//     2340x1080 the modal canvas resolves to 2120x978 reference px (CanvasScaler
//     1080x1920, match 0.5 -> scale 1.104), the panel to 1780x783, so the left
//     well is 819 ref px wide and the old 0.03-0.97 strip was 770. Five chips at a
//     HARDCODED preferredWidth of 220 + 4x10 spacing ask for 1140 px, so the
//     strip's RectMask2D cut chip 4 at 770 px -- 80 px into a 220 px chip, i.e.
//     ~36% of "Gear", which is EXACTLY the lone "G" in the capture. It only LOOKED
//     like the detail pane crossed the tabs: the mask edge lands at frame x 0.481
//     and the detail pane starts at 0.505 -- 24 px apart on screen.
//     FIX: the tab band is now its OWN fixed-pixel band (TabBandPx) hung from the
//     LEFT well's top line and X-BOUNDED BY THAT WELL, parented to chrome.content
//     as a LATER sibling than every zone. The detail pane lives in the RIGHT well.
//     They are horizontally DISJOINT rects in different columns, so the detail pane
//     cannot cross the tab band by construction -- not by tuning. And the chips no
//     longer carry a hardcoded width: they FLEX-FILL the band (flexibleWidth 1,
//     minWidth = MinTouchPx), so all five are always fully visible.
//
//  2. THE CULLED DETAIL BODY (found while measuring #1 -- the same class the F8
//     ObsidianQueueHud header bug was). The old detail stack reserved 148 px of
//     top bands + 212 px of bottom bands = 360 px inside a right well that is only
//     349 px tall at 2340x1080, so the body label's rect resolved to -11 px and TMP
//     culled the tale WHOLE. That is why the capture shows tag chips, a title,
//     reward chips and the CTAs but NO quest text at all. At 1920x1080 (the headless
//     capture aspect) the same band computes to +39 px and squeaks out one line,
//     which is precisely why RunCaptureHeadless never caught it.
//     FIX: the whole detail stack is a declared FIXED-PIXEL budget
//     (DetailFixedStackPx = 310) proven against the measured pane height, and the
//     pane is TOP-ALIGNED to the left well's top line (anchorMax.y > 1 by the ratio
//     of the two wells' spans) so both columns start on one line and the detail
//     gains the 77 px the frame's lower parchment zone was giving away. Body band
//     is then 116 px at 2340x1080 / 174 px at 1920x1080 -- two whole FontLabel line
//     boxes with slack. RumorBoardLayoutRegression pins all of it.
//
// STYLING (WO-866 sec.2), all presentation-only:
//   . ONE chip language. Tag chips, reward chips AND the list row's state pip are
//     now the SAME MakeChip widget with ONE ink and ONE border -- meaning is
//     carried by the WORD, never by colour (the owner is red/green colourblind).
//   . Content chrome now answers the ornate frame: 2 px gilt-dim borders on the
//     detail plate and on every card, gilt hairline rules under the tab band and
//     under each section label, and a footer band that gives the shared Close a
//     home instead of leaving it floating over the frame's bottom border.
//   . An EARLY STATE card replaces the dead black region when the board is sparse.
//
// KEEP (owner ruling, do NOT restyle away): the selected tab is marked with BOTH a
// leading "*" AND an underline bar. Text-encoded state, never colour alone. It is
// the pattern the rest of this file now follows.
//
// ASCII-ONLY (including comments): the shipped LiberationSans SDF has no non-Latin
// glyphs and RumorBoardLayoutRegression asserts the whole file is ASCII.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Hero
{
    public sealed class RumorBoardPanel : MonoBehaviour
    {
        // =====================================================================
        //  WO-866 LAYOUT BUDGET -- fixed reference pixels, never a fraction of
        //  parent. Public so RumorBoardLayoutRegression can pin them without a
        //  reflection bridge into private state.
        // =====================================================================

        /// <summary>Height of the filter-tab band (>= ElarionUiKit.MinTouchPx).</summary>
        public const float TabBandPx = 124f;
        /// <summary>Breathing gap between the tab band and the list below it.</summary>
        public const float TabBandGapPx = 10f;
        /// <summary>Horizontal inset of the tab band inside the list well.</summary>
        public const float TabBandInsetPx = 8f;
        /// <summary>Gap between two tab chips.</summary>
        public const float TabChipSpacingPx = 8f;
        /// <summary>Floor width of one tab chip. A chip never shrinks below the kit touch floor.</summary>
        public const float TabChipMinPx = 112f;
        /// <summary>Slack a tab chip adds around its MEASURED label.</summary>
        public const float TabChipPadPx = 24f;
        /// <summary>The fraction of a kit button's width its label rect actually gets
        /// (BuildObsidianButton insets the label 0.04..0.96). A measured label width is grossed
        /// up by this before it becomes the chip's ask, or the label lands on the rect edge.</summary>
        public const float TabLabelInsetFrac = 0.92f;
        /// <summary>Tab count the band must seat. Mirrors RumorBoardVM.TabKeys.Length --
        /// BuildTabStrip warns if they diverge and RumorBoardLayoutRegression fails on it.</summary>
        public const int TabCount = 5;
        /// <summary>Narrowest list well the tab band can seat every tab in at the touch floor.</summary>
        public const float TabRowMinWidthPx =
            TabCount * TabChipMinPx + (TabCount - 1) * TabChipSpacingPx + 2f * TabBandInsetPx + 12f;
        /// <summary>Pixels the list viewport is inset below the well's top for the tab band.</summary>
        public const float ListTopInsetPx = TabBandPx + TabBandGapPx;
        /// <summary>Pixels reserved at the list column's bottom for the status line.</summary>
        public const float ListBottomInsetPx = 52f;
        /// <summary>Status line band (one FontMicro line box + margin).</summary>
        public const float StatusBandPx = 44f;
        /// <summary>Card height. Above the touch floor (112) because the card seats TWO line
        /// boxes: a FontBody(50) title (62.5) over a FontMicro(32) hook (40) plus padding --
        /// at 112 the two bands would have to touch, which is how a descender gets clipped.</summary>
        public const float CardHeightPx = 124f;
        /// <summary>Height of the early-state note card in the list.</summary>
        public const float EarlyNotePx = 132f;
        /// <summary>Section label band (one FontLabel line box + the hairline rule).</summary>
        public const float SectionBandPx = 56f;
        /// <summary>Flavor line band (one FontMicro line box + margin).</summary>
        public const float FlavorBandPx = 48f;

        /// <summary>Detail pane edge padding.</summary>
        public const float DetailPadPx = 6f;
        /// <summary>Detail pane gap between two bands.</summary>
        public const float DetailGapPx = 6f;
        /// <summary>Chip row band -- 48 seats the 48px chip whose 44px label rect holds a FontMicro line box.</summary>
        public const float DetailChipRowPx = 48f;
        /// <summary>Detail title band -- 64 >= one FontBody(50) line box (62.5).</summary>
        public const float DetailTitlePx = 64f;
        /// <summary>Gilt hairline under the title.</summary>
        public const float DetailRulePx = 2f;
        /// <summary>Detail CTA band (the kit touch floor).</summary>
        public const float DetailCtaPx = 112f;
        /// <summary>Everything in the detail stack EXCEPT the body, in fixed px.
        /// top: pad + chips + gap + title + rule + gap   bottom: pad + cta + gap + chips + gap.</summary>
        public const float DetailFixedStackPx =
            (DetailPadPx + DetailChipRowPx + DetailGapPx + DetailTitlePx + DetailRulePx + DetailGapPx) +
            (DetailPadPx + DetailCtaPx + DetailGapPx + DetailChipRowPx + DetailGapPx);
        /// <summary>Two whole FontLabel(40) line boxes -- the body's honest minimum.</summary>
        public const float DetailBodyMinPx = 100f;
        /// <summary>Footer band that seats the shared Close, hung from the wells' floor.</summary>
        public const float FooterBandPx = 200f;

        // =====================================================================
        //  WO-941 -- THE PORTRAIT CLOSE BAND, PUBLISHED AND SUBTRACTED
        // -----------------------------------------------------------------------------
        //  LANDSCAPE gives the shared Close a home: BuildFooterBand paints a 200px plate
        //  under it and the two wells end above it. PORTRAIT had no such band -- the detail
        //  pane simply claimed 0.05..0.46 of chrome.content, while the kit seats the ONE
        //  shared Close as a FIXED CanonCtaWidth x CanonCtaHeight box on that SAME
        //  chrome.content, bottom-centred, growing UPWARD from its band's lower edge
        //  (ElarionUiKit.SeatSharedCloseInside). Two surfaces, one band -- exactly the
        //  failure UI_PLAYBOOK sec.8 names, and the geometry oracle read it as 14
        //  `BUTTON OVER TEXT` assertions at BOTH portrait sizes: Close over the reward chip
        //  labels ("Food 90" / "Magic 45" / "Relic Drowned Ledger") and over the
        //  Accept/Track CTA labels, and Accept/Track back over "Close".
        //
        //  The fix is the playbook's, not a tune: PUBLISH the band the Close owns and
        //  SUBTRACT it. CloseReserveTopFraction reads the Close's own seated anchor and
        //  its canonical pixel height and returns where that box really TOPS OUT, in the
        //  panel's fraction space -- so the pane's floor tracks the Close on every aspect
        //  instead of hoping a hardcoded 0.05 clears it.
        // =====================================================================

        /// <summary>The modal's own vertical anchor band. Declared ONCE and read by both
        /// <c>BuildObsidianModal</c> and the close-band math, so the two can never drift
        /// (the duplicated-constant failure CLAUDE.md sec.2/sec.5 keeps catching).</summary>
        public const float PanelAnchorMinY = 0.10f;
        /// <inheritdoc cref="PanelAnchorMinY"/>
        public const float PanelAnchorMaxY = 0.90f;
        /// <summary>Breathing gap above the shared Close box before any other surface may start.</summary>
        public const float CloseReserveGapFrac = 0.02f;
        /// <summary>Floor for the portrait detail pane: also the fallback when the Close cannot
        /// be measured (no button / no canvas), so a missing measurement never re-opens the band.</summary>
        public const float PortraitDetailFloorY = 0.16f;
        /// <summary>Sanity ceiling -- a very short canvas must not lose the whole pane to the band.</summary>
        public const float CloseReserveMaxFrac = 0.45f;
        /// <summary>Portrait detail pane's ceiling (the list well starts at 0.48).</summary>
        public const float PortraitDetailTopY = 0.46f;
        /// <summary>Hard ceiling the pane may grow to on a degenerate canvas before it would
        /// touch the list well above it.</summary>
        public const float PortraitDetailTopMaxY = 0.47f;

        private GameObject _ui;
        private Transform _panelRoot;
        private Transform _chromeContent;
        private RectTransform _zoneLeft;
        private RectTransform _zoneRight;
        private GameObject _contentRoot;
        private TMPro.TextMeshProUGUI _statusText;

        // Detail pane widgets (rebuilt content, persistent hosts).
        private RectTransform _detailTagRow;
        private TMPro.TextMeshProUGUI _detailTitle;
        private TMPro.TextMeshProUGUI _detailBody;
        private RectTransform _detailRewardRow;
        private GameObject _detailCtaGo;
        private RectTransform _detailPane;

        private RumorBoardVM _vm;
        private GameObject _tabStrip;
        private PanelHandle _handle;

        // WO-810 selection model: the detail pane always binds this. Kind disambiguates the
        // CTA (Accept vs Track vs none) without re-deriving from the VM lists on every tap.
        private enum RowKind { None, Available, Active, Daily }
        private string _selectedId;
        private RowKind _selectedKind = RowKind.None;

        // -- Chip metrics (iteration 3 -- the store-listing capture RCA) -----------
        // MEASURED capture 2026-08-03 (RumorBoard_1920x1080.png): a chip sized from a
        // per-character guess (text.Length*18+28) over-asked by ~37% against the measured
        // ~13.1 px/char of a FontMicro glyph, the HorizontalLayoutGroup shrank every chip to
        // 60.6% of its ask, and the labels -- authored NoWrap + Overflow -- kept painting at
        // FULL width straight through their neighbours. Two rules kill it for good:
        //   1. a chip is sized from its label's MEASURED preferred width, never a guess;
        //   2. a chip label is FITTED (bounded auto-size -> ellipsis), so even a short row
        //      shrinks text INSIDE the borders instead of across the next chip.
        private const float ChipPadPx = 16f;      // 8 per side around the measured label
        private const float ChipSpacingPx = 6f;   // gap between chips
        private const float ChipHeightPx = 48f;   // 44px label rect -- one FontMicro line box
        // Fitted floor for a chip label: well above the kit's FontHardFloor (20).
        private const float ChipMinFontPx = 26f;

        // WO-866: ONE chip language. Border + fill + ink are the same for a type tag, a
        // state tag and a reward -- the WORD carries the meaning, never the colour.
        private static readonly Color ChipBorder = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.55f);
        private static readonly Color ChipFill = new Color(0.05f, 0.045f, 0.04f, 1f);
        private static readonly Color PlateInk = new Color(0.05f, 0.045f, 0.04f, 1f);
        // The hairline/border tone that answers the frame's gilt metal without shouting.
        private static readonly Color HairLine = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.42f);

        // -- Public API ----------------------------------------------------------

        public void Open()
        {
            Close();

            if (_handle == null)
                _handle = PanelManager.Register("Rumor Board", Close, () => _ui != null);

            _vm = RumorBoardVM.CreateDefault(Close);
            _vm.Changed += Repaint;

            var modal = ElarionUiKit.BuildObsidianModal("RumorBoardPanelUI", "Brom's Rumor Board",
                new Vector2(0.08f, PanelAnchorMinY), new Vector2(0.92f, PanelAnchorMaxY), Close, sortingOrder: 1000,
                frameName: RpgUiCatalog.FrameQuest, medallionIcon: "quest");
            _ui = modal.canvas;
            var panel = modal.chrome.content;
            _chromeContent = panel != null ? panel.transform : null;
            var bodyHost = (modal.chrome.layout != null && modal.chrome.layout.body != null)
                ? modal.chrome.layout.body : (RectTransform)panel.transform;
            _panelRoot = bodyHost;

            // The panes parent to the kit's MEASURED FrameQuest drop-zones -- bodyLeft (the
            // dark list well) and bodyRight (the detail well). Fallbacks keep panel fractions
            // for a frameless procedural panel and for portrait (stacked panes).
            // Kit surface, not Screen.* - same value at runtime; a capture drives it so the
            // portrait BRANCH ITSELF is exercised by a portrait shot (Screen never moves in batchmode).
            bool portrait = ElarionUiKit.SurfaceHeight > ElarionUiKit.SurfaceWidth;
            var zoneLeft = modal.chrome.layout != null ? modal.chrome.layout.bodyLeft : null;
            var zoneRight = modal.chrome.layout != null ? modal.chrome.layout.bodyRight : null;
            _zoneLeft = zoneLeft;
            _zoneRight = zoneRight;

            Vector2 listMin, listMax, detailMin, detailMax;
            Transform listHost, detailHost;
            float listTopInsetPx = 0f;
            float listBottomInsetPx = 0f;
            // WO-1076: the portrait pane's FLOOR, carried as a PIXEL OFFSET rather than folded
            // into detailMin.y. Zero on every non-portrait path (the anchors alone place those).
            float detailBottomOffsetPx = 0f;
            if (portrait)
            {
                // WO-941: the pane's FLOOR is the top of the shared Close box + a gap, MEASURED
                // off the Close the kit just seated on this same chrome.content -- never the old
                // hardcoded 0.05, which put the reward-chip row and the Accept/Track CTA band
                // inside the Close's fixed 360x132 box at every portrait size.
                float panelHPx = Mathf.Max(1f, (PanelAnchorMaxY - PanelAnchorMinY) *
                                                ElarionUiKit.PostScaleCanvasHeight(panel.transform));
                float detailFloorY = CloseReserveTopFraction(modal.chrome.close, panelHPx);
                float detailTopY = PortraitDetailTopY;
                // The detail stack is a DECLARED fixed-pixel budget; if the reserved band would
                // starve it, grow the pane UP into the slack under the list well (0.48) rather
                // than back DOWN into the band the Close owns. Collapse, never invert (sec.8).
                float needFrac = (DetailFixedStackPx + DetailBodyMinPx) / panelHPx;
                if (detailTopY - detailFloorY < needFrac)
                {
                    detailTopY = Mathf.Min(PortraitDetailTopMaxY, detailFloorY + needFrac);
                    Debug.LogWarning("[RumorBoardPanel] WO-941: portrait detail pane is tight after the " +
                        "Close-band reserve (floor " + detailFloorY.ToString("F3") +
                        ", top " + detailTopY.ToString("F3") + ", panelH " + panelHPx.ToString("F0") + " px).");
                }

                // =============================================================
                //  WO-1076 -- THE FLOOR MOVES OUT OF THE ANCHOR AND INTO THE OFFSET.
                // -------------------------------------------------------------
                //  The WO-941 arithmetic above is CORRECT and was never the defect: at
                //  1080x2340 it yields floor 0.16 (closeTop 0.128 + gap 0.02, clamped up),
                //  which seats the CTA row at y -570..-458 against a Close at -763..-631 --
                //  60.7 ref px of daylight. The fresh 2026-08-25 capture nonetheless measured
                //  the CTA at -757.1..-645.1, i.e. a pane bottom of EXACTLY 0.05, the retired
                //  literal. The reason is not in this file: the capture harness re-asserts
                //  its own copy of the portrait anchors onto _detailPane AFTER Open() returns
                //  and BEFORE the geometry audit, and its copy still carries the pre-WO-941
                //  0.05. One number, authored twice -- the duplicated-state failure CLAUDE.md
                //  sec.2/sec.5 keeps catching, arriving this time through a test harness.
                //
                //  So the floor is expressed where an anchor rewrite CANNOT reach it: as a
                //  fixed pixel OFFSET above whatever the pane's bottom anchor resolves to.
                //   - Untouched (the shipped device path): anchor 0 + floorPx == the exact
                //     rect the fraction produced. Byte-identical geometry, zero visual delta.
                //   - Overwritten with a lower anchor: the offset still applies on top, so the
                //     pane can only ever be pushed FURTHER FROM the Close, never back into it.
                //  A floor that can only fail SAFE. This is geometry, not z-order: nothing is
                //  re-sorted, and a transparent raycaster could not have been fixed by sorting.
                // =============================================================
                detailBottomOffsetPx = detailFloorY * panelHPx;
                listMin = new Vector2(0.03f, 0.48f); listMax = new Vector2(0.97f, 0.855f);
                detailMin = new Vector2(0.05f, 0f); detailMax = new Vector2(0.95f, detailTopY);
                DeNelle.Core.Diagnostics.FlowTrace.Step("UI", string.Format(
                    "RumorBoard portrait detail floor: frac={0:F3} -> {1:F1} px above the pane's " +
                    "bottom anchor (panelH {2:F0} px, top {3:F3}). CTA band bottom = that + {4:F0} px.",
                    detailFloorY, detailBottomOffsetPx, panelHPx, detailTopY, DetailPadPx));
                listHost = bodyHost;
                detailHost = panel.transform;
                // Portrait stacks the panes and the band hangs from the BODY top, well above
                // the list's own 0.855 ceiling -- no inset to take (landscape is the shipped
                // orientation; this path only has to stay sane).
                listTopInsetPx = 0f;
            }
            else
            {
                if (zoneLeft != null)
                {
                    // The list fills the dark left well, inset below the tab band by a FIXED
                    // pixel budget (the band hangs from the SAME 1.0 line, so the inset is
                    // exact on every aspect -- no fraction can drift into the band).
                    listHost = zoneLeft;
                    listMin = Vector2.zero;
                    listMax = Vector2.one;
                    listTopInsetPx = ListTopInsetPx;
                    listBottomInsetPx = ListBottomInsetPx;
                }
                else
                {
                    listHost = bodyHost;
                    listMin = new Vector2(0.03f, 0.07f); listMax = new Vector2(0.97f, 1f);
                    listTopInsetPx = ListTopInsetPx;
                }

                if (zoneRight != null)
                {
                    // WO-866: TOP-ALIGN the detail pane with the LIST well's top line. The
                    // frame's parchment zone starts 0.098 of the frame lower than the dark
                    // well (349 px vs 426 px tall at 2340x1080), which both misaligned the two
                    // columns AND starved the detail stack until its body band went NEGATIVE.
                    // anchorMax.y > 1 is legal and exact: it is the left well's top expressed
                    // as a fraction of the right well's own span, read off the zones' anchors
                    // (available at build time -- no layout pass, no hardcoded kit fractions).
                    detailHost = zoneRight;
                    detailMin = Vector2.zero;
                    detailMax = new Vector2(1f, TopAlignFraction(zoneLeft, zoneRight));
                }
                else
                { detailHost = panel.transform; detailMin = new Vector2(0.51f, 0.30f); detailMax = new Vector2(0.955f, 0.78f); }
            }

            // -- LEFT: the scrollable card list (WO-795 -- rows never stack/overlap) --
            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(listHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = listMin;
            vpr.anchorMax = listMax;
            vpr.offsetMin = new Vector2(0f, listBottomInsetPx);
            vpr.offsetMax = new Vector2(0f, -listTopInsetPx);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            _contentRoot = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _contentRoot.transform.SetParent(viewportGo.transform, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;
            var vlg = _contentRoot.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            // Bottom pad = one card so the last row scrolls fully clear of the mask.
            vlg.padding = new RectOffset(6, 6, 6, 104);
            var csf = _contentRoot.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            // -- RIGHT: the detail pane (obsidian plate, gilt border, bound to the selection) --
            var detailGo = new GameObject("DetailPane", typeof(Image));
            detailGo.transform.SetParent(detailHost, false);
            _detailPane = detailGo.GetComponent<RectTransform>();
            _detailPane.anchorMin = detailMin;
            _detailPane.anchorMax = detailMax;
            // WO-1076: the portrait Close-band reserve rides in offsetMin.y (see the block that
            // sets detailBottomOffsetPx). Zero on every other path, so those rects are unchanged.
            _detailPane.offsetMin = new Vector2(0f, detailBottomOffsetPx); _detailPane.offsetMax = Vector2.zero;
            var dImg = detailGo.GetComponent<Image>();
            // WO-866: the outer Image is now the 2px BORDER (chip language, scaled up) and an
            // inset Fill child carries the obsidian. Solid alpha 1: the plate must COVER the
            // frame's tan parchment page beneath it (a 0.92 alpha over tan reads KHAKI in
            // linear space -- pixel-proven).
            dImg.color = HairLine;
            dImg.raycastTarget = false;
            var detailFill = new GameObject("Fill", typeof(Image));
            detailFill.transform.SetParent(detailGo.transform, false);
            var dfr = detailFill.GetComponent<RectTransform>();
            dfr.anchorMin = Vector2.zero; dfr.anchorMax = Vector2.one;
            dfr.offsetMin = new Vector2(2f, 2f); dfr.offsetMax = new Vector2(-2f, -2f);
            var dfImg = detailFill.GetComponent<Image>();
            dfImg.color = PlateInk;
            dfImg.raycastTarget = false;

            // The kit RAISES the bodyRight zone floor above the shared Close band, leaving the
            // frame art's baked tan parchment page visible BETWEEN the zone bottom and the page
            // bottom. Cover that remainder with an alpha-1 obsidian strip hung from the zone's
            // bottom edge. Children of Zone_BodyRight render UNDER the later-sibling Close, so
            // this can never occlude Close.
            if (!portrait && zoneRight != null)
            {
                var under = new GameObject("DetailUnderPlate", typeof(Image));
                under.transform.SetParent(zoneRight, false);
                under.transform.SetAsFirstSibling();
                var urt = under.GetComponent<RectTransform>();
                urt.anchorMin = new Vector2(0f, 0f);
                urt.anchorMax = new Vector2(1f, 0f);
                urt.pivot = new Vector2(0.5f, 1f);
                urt.offsetMin = Vector2.zero; urt.offsetMax = Vector2.zero;
                urt.sizeDelta = new Vector2(0f, 165f);   // the reserved-band remainder (~155-160 ref px)
                urt.anchoredPosition = Vector2.zero;      // top edge = the zone's bottom edge
                var underImg = under.GetComponent<Image>();
                underImg.color = PlateInk;
                underImg.raycastTarget = false;
            }

            // -- The detail stack: a DECLARED fixed-pixel budget (DetailFixedStackPx) ------
            // top-down:  pad 6 | chips 48 | gap 6 | title 64 | rule 2 | gap 6      = 132
            // bottom-up: pad 6 | cta 112  | gap 6 | chips 48 | gap 6               = 178
            // body fills the remainder: 116 px @ 2340x1080, 174 px @ 1920x1080.
            const float TopChips = DetailPadPx;                                     // 6
            const float TopTitle = TopChips + DetailChipRowPx + DetailGapPx;        // 60
            const float TopRule = TopTitle + DetailTitlePx;                         // 124
            const float BodyTop = TopRule + DetailRulePx + DetailGapPx;             // 132
            const float BotCta = DetailPadPx;                                       // 6
            const float BotRewards = BotCta + DetailCtaPx + DetailGapPx;            // 124
            const float BodyBottom = BotRewards + DetailChipRowPx + DetailGapPx;    // 178

            _detailTagRow = MakeChipRow(detailGo.transform, "DetailTagRow",
                topPx: TopChips, heightPx: DetailChipRowPx);

            _detailTitle = MakeDetailLabel(detailGo.transform, "DetailTitle",
                new Vector2(0.04f, 1f), new Vector2(0.96f, 1f),
                ElarionUi.Gilt, ElarionUi.FontBody, bold: true);
            var titleRt = _detailTitle.rectTransform;
            titleRt.offsetMin = new Vector2(0f, -(TopTitle + DetailTitlePx));
            titleRt.offsetMax = new Vector2(0f, -TopTitle);

            // Gilt hairline under the title -- the cheapest way to make the content chrome
            // answer the frame's metal instead of reading as a bare black rectangle.
            var rule = new GameObject("TitleRule", typeof(Image));
            rule.transform.SetParent(detailGo.transform, false);
            var rrt = rule.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.04f, 1f);
            rrt.anchorMax = new Vector2(0.96f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(0f, DetailRulePx);
            rrt.anchoredPosition = new Vector2(0f, -TopRule);
            var ruleImg = rule.GetComponent<Image>();
            ruleImg.color = HairLine;
            ruleImg.raycastTarget = false;

            _detailBody = MakeDetailLabel(detailGo.transform, "DetailBody",
                new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                ElarionUi.Parchment, ElarionUi.FontLabel, bold: false);
            _detailBody.alignment = TMPro.TextAlignmentOptions.TopLeft;
            _detailBody.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var bodyRt = _detailBody.rectTransform;
            bodyRt.offsetMin = new Vector2(0f, BodyBottom);
            bodyRt.offsetMax = new Vector2(0f, -BodyTop);

            // Reward chips, bottom-hung just above the fixed-height CTA band. Same widget,
            // same ink and same border as the tag chips: ONE language for one class of info.
            _detailRewardRow = MakeChipRow(detailGo.transform, "DetailRewardRow",
                topPx: 0f, heightPx: DetailChipRowPx, fromBottomPx: BotRewards,
                sideFrac: 0.02f, spacingPx: ChipSpacingPx);

            // -- The tab band: its OWN band, in the list column, above every zone ---------
            BuildTabStrip();

            // -- Footer band: the shared Close gets panel chrome instead of floating -------
            BuildFooterBand(portrait);

            // Status line at the BOTTOM OF THE LIST COLUMN (fixed 44px band, FontMicro; the
            // viewport is inset above it), so it can never touch Close or the parchment band.
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            var sRect = statusGo.GetComponent<RectTransform>();
            if (!portrait && zoneLeft != null)
            {
                statusGo.transform.SetParent(zoneLeft, false);
                sRect.anchorMin = new Vector2(0f, 0f);
                sRect.anchorMax = new Vector2(1f, 0f);
                sRect.pivot = new Vector2(0.5f, 0f);
                sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;
                sRect.sizeDelta = new Vector2(0f, StatusBandPx);   // one FontMicro line (never culled)
                sRect.anchoredPosition = new Vector2(0f, 4f);
            }
            else
            {
                statusGo.transform.SetParent(bodyHost, false);
                sRect.anchorMin = new Vector2(0.03f, 0f);
                sRect.anchorMax = new Vector2(0.97f, 0f);
                sRect.pivot = new Vector2(0.5f, 0f);
                sRect.sizeDelta = new Vector2(0f, StatusBandPx);
                sRect.anchoredPosition = new Vector2(0f, 4f);
            }
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(_statusText);
            _statusText.fontSize = ElarionUi.FontMicro;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            _statusText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            SetStatus(_vm.Status);

            Repaint();

            if (!PanelManager.NotifyOpened(_handle)) return;

            Debug.Log("[RumorBoardPanel] Opened (WO-866 tab band + fixed detail stack).");
        }

        public void Close()
        {
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }

            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _statusText = null;
            _detailTagRow = null;
            _detailTitle = null;
            _detailBody = null;
            _detailRewardRow = null;
            _detailCtaGo = null;
            _detailPane = null;
            _tabStrip = null;
            _chromeContent = null;
            _zoneLeft = null;
            _zoneRight = null;
            _panelRoot = null;
            _selectedId = null;
            _selectedKind = RowKind.None;
            PanelManager.NotifyClosed(_handle);
        }

        private void OnDestroy()
        {
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
        }

        /// <summary>
        /// WO-941 -- where the ONE shared Close box really TOPS OUT, expressed as a fraction of
        /// this panel's own height, plus <see cref="CloseReserveGapFrac"/>. Any surface parented
        /// to chrome.content must start at or above this line.
        ///
        /// The Close is seated by <c>ElarionUiKit.SeatSharedCloseInside</c>: anchorMin.y ==
        /// anchorMax.y == the band's lower edge, pivot y = 0, sizeDelta y ==
        /// <c>ElarionUiKit.CanonCtaHeight</c> -- a FIXED pixel box growing UPWARD. So its top is
        /// the seated anchor plus that fixed height over the panel's height in the SAME
        /// post-scale reference px the anchors resolve against. <paramref name="panelHPx"/> is
        /// derived from <c>PostScaleCanvasHeight</c> (never a live <c>rect.height</c>, which
        /// returns RAW SCREEN PIXELS on the canvas's creation frame -- the F8-5 root cause the
        /// kit documents at <c>ElarionUiKit.PostScaleCanvasHeight</c>).
        ///
        /// Returns <see cref="PortraitDetailFloorY"/> when the Close cannot be measured, so a
        /// missing measurement can never silently re-open the band.
        /// </summary>
        private static float CloseReserveTopFraction(UnityEngine.UI.Button close, float panelHPx)
        {
            if (close == null || panelHPx <= 1f) return PortraitDetailFloorY;
            var crt = close.transform as RectTransform;
            if (crt == null) return PortraitDetailFloorY;
            float closeTop = crt.anchorMin.y + ElarionUiKit.CanonCtaHeight / panelHPx;
            return Mathf.Clamp(closeTop + CloseReserveGapFrac, PortraitDetailFloorY, CloseReserveMaxFrac);
        }

        /// <summary>The LIST well's top line expressed as a fraction of the DETAIL well's own
        /// vertical span, so a rect anchored there tops out on the same screen line as the list.
        /// Reads the zones' ANCHORS (set by the kit's Zone() at build time -- valid before any
        /// layout pass) and never shrinks the detail below its own zone top.</summary>
        private static float TopAlignFraction(RectTransform zoneLeft, RectTransform zoneRight)
        {
            if (zoneLeft == null || zoneRight == null) return 1f;
            float rb = zoneRight.anchorMin.y, rt = zoneRight.anchorMax.y;
            float span = rt - rb;
            if (span <= 0.0001f) return 1f;
            float f = (zoneLeft.anchorMax.y - rb) / span;
            if (f < 1f) f = 1f;      // pure floor -- a taller detail well is left alone
            if (f > 1.6f) f = 1.6f;  // sanity clamp: never swallow the frame's header band
            return f;
        }

        // -- Paint ---------------------------------------------------------------

        private void Repaint()
        {
            if (_contentRoot == null || _vm == null) return;
            ClearContent();

            int rows = 0;
            if (_vm.IsDailyTab)
            {
                rows = RepaintDaily();
            }
            else
            {
                // Empty In Progress = ONE quiet dim line, no section slab; populated = a
                // compact section of cards above the Rumors list.
                if (_vm.ActiveQuests.Count == 0)
                {
                    CreateFlavorRow(_contentRoot.transform, "In Progress - nothing underway.");
                }
                else
                {
                    CreateSectionLabel(_contentRoot.transform, "- In Progress -");
                    foreach (var item in _vm.ActiveQuests)
                    {
                        CreateCard(_contentRoot.transform, item.Id, item.Name,
                            _vm.ObjectiveFor(item.Id),
                            item.Equipped ? "Tracked" : "Underway", RowKind.Active);
                        rows++;
                    }
                }

                CreateSectionLabel(_contentRoot.transform, "- Rumors & Requests -");
                if (_vm.AvailableQuests.Count == 0)
                    CreateFlavorRow(_contentRoot.transform, "You've answered every call. For now.");
                foreach (var item in _vm.AvailableQuests)
                {
                    CreateCard(_contentRoot.transform, item.Id, item.Name,
                        _vm.HookFor(item.Id), "New", RowKind.Available);
                    rows++;
                }
            }

            // WO-866: an EARLY STATE instead of a large dead-black region. A board with one
            // rumor on it is not a broken board -- say so, in words, where the black was.
            if (!_vm.IsDailyTab && rows <= 1)
                CreateEarlyNote(_contentRoot.transform, rows == 0);

            // Auto-select: the detail is never blank while anything is listable.
            EnsureSelection();
            RenderDetail();
        }

        // Keep (or establish) a valid selection: prefer the current one if it still exists
        // under this tab; else first available, first active, first daily; else none.
        private void EnsureSelection()
        {
            if (_vm == null) { _selectedId = null; _selectedKind = RowKind.None; return; }

            if (_vm.IsDailyTab)
            {
                var daily = _vm.DailyQuests;
                if (_selectedKind == RowKind.Daily && FindDaily(_selectedId).HasValue) return;
                if (daily != null && daily.Count > 0) { _selectedId = daily[0].Id; _selectedKind = RowKind.Daily; }
                else { _selectedId = null; _selectedKind = RowKind.None; }
                return;
            }

            if (_selectedId != null)
            {
                foreach (var i in _vm.AvailableQuests) if (i.Id == _selectedId) { _selectedKind = RowKind.Available; return; }
                foreach (var i in _vm.ActiveQuests) if (i.Id == _selectedId) { _selectedKind = RowKind.Active; return; }
            }
            if (_vm.AvailableQuests.Count > 0) { _selectedId = _vm.AvailableQuests[0].Id; _selectedKind = RowKind.Available; return; }
            if (_vm.ActiveQuests.Count > 0) { _selectedId = _vm.ActiveQuests[0].Id; _selectedKind = RowKind.Active; return; }
            _selectedId = null;
            _selectedKind = RowKind.None;
        }

        private RumorBoardVM.DailyRow? FindDaily(string id)
        {
            var daily = _vm != null ? _vm.DailyQuests : null;
            if (daily == null || id == null) return null;
            foreach (var d in daily) if (d.Id == id) return d;
            return null;
        }

        // -- Filter tabs (WO-866 -- one fixed band in the list column, chips flex-fill) --

        /// <summary>The tab band. Parented to chrome.content (a LATER sibling than every kit
        /// zone, so nothing can paint over it) but X-BOUNDED BY THE LIST WELL and hung from
        /// that well's top line with a FIXED pixel height. The detail pane lives in the RIGHT
        /// well, so the two rects are horizontally disjoint -- the overlap the capture showed
        /// is structurally impossible now, not merely tuned away.</summary>
        private void BuildTabStrip()
        {
            if (_tabStrip != null) { SafeDestroy(_tabStrip); _tabStrip = null; }

            Transform host;
            float xMin, xMax, topY;
            if (_chromeContent != null && _zoneLeft != null
                && ElarionUiKit.SurfaceWidth >= ElarionUiKit.SurfaceHeight)   // kit surface: capture-drivable
            {
                host = _chromeContent;
                xMin = _zoneLeft.anchorMin.x;
                xMax = _zoneLeft.anchorMax.x;
                topY = _zoneLeft.anchorMax.y;
            }
            else
            {
                host = _panelRoot != null ? _panelRoot : (_ui != null ? _ui.transform : null);
                xMin = 0.02f; xMax = 0.98f; topY = 1f;
            }
            if (host == null) return;

            _tabStrip = new GameObject("TabBand", typeof(Image), typeof(RectMask2D));
            _tabStrip.transform.SetParent(host, false);
            var sr = _tabStrip.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(xMin, topY);
            sr.anchorMax = new Vector2(xMax, topY);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.sizeDelta = new Vector2(-2f * TabBandInsetPx, TabBandPx);
            sr.anchoredPosition = Vector2.zero;
            var bandImg = _tabStrip.GetComponent<Image>();
            bandImg.color = PlateInk;
            bandImg.raycastTarget = false;

            // The band declares itself with a gilt hairline along its base -- the same rule
            // language as the detail title and the footer, so the chrome reads as one set.
            var bandRule = new GameObject("BandRule", typeof(Image));
            bandRule.transform.SetParent(_tabStrip.transform, false);
            var brr = bandRule.GetComponent<RectTransform>();
            brr.anchorMin = new Vector2(0f, 0f);
            brr.anchorMax = new Vector2(1f, 0f);
            brr.pivot = new Vector2(0.5f, 0f);
            brr.sizeDelta = new Vector2(0f, 2f);
            brr.anchoredPosition = Vector2.zero;
            var brImg = bandRule.GetComponent<Image>();
            brImg.color = HairLine;
            brImg.raycastTarget = false;

            // Chips FILL the band. No hardcoded preferredWidth (that 220 is what overflowed
            // the mask and clipped "Gear"): flexibleWidth shares the band evenly and minWidth
            // holds the kit touch floor, so five tabs always fit and none is ever untappable.
            var content = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            content.transform.SetParent(_tabStrip.transform, false);
            var ccr = content.GetComponent<RectTransform>();
            ccr.anchorMin = Vector2.zero;
            ccr.anchorMax = Vector2.one;
            ccr.offsetMin = Vector2.zero;
            ccr.offsetMax = Vector2.zero;
            var hlg = content.GetComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childForceExpandWidth = true;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = TabChipSpacingPx;
            hlg.padding = new RectOffset(6, 6, 4, 6);

            // The band's width budget (TabRowMinWidthPx) is declared for TabCount tabs. If the
            // VM grows a sixth, the budget -- and the regression that proves it fits the well --
            // has to grow with it; never let it silently squeeze under the touch floor again.
            if (RumorBoardVM.TabKeys.Length != TabCount)
                Debug.LogWarning("[RumorBoardPanel] TabKeys.Length=" + RumorBoardVM.TabKeys.Length +
                    " but TabCount=" + TabCount + " -- re-derive TabRowMinWidthPx and the layout regression.");

            string activeTab = _vm != null ? _vm.ActiveTab : "all";
            for (int i = 0; i < RumorBoardVM.TabKeys.Length; i++)
            {
                string key = RumorBoardVM.TabKeys[i];
                bool isActive = key == activeTab;
                string tabKey = key;

                var chipHost = new GameObject("Chip_" + key, typeof(RectTransform), typeof(LayoutElement));
                chipHost.transform.SetParent(content.transform, false);
                var le = chipHost.GetComponent<LayoutElement>();
                le.minWidth = TabChipMinPx;      // never below the kit touch floor
                le.flexibleWidth = 1f;           // the band's slack is shared evenly

                // KEEP (owner ruling): selected = a leading "*" AND an underline bar.
                // Shape + word carry the state. NEVER a colour highlight.
                string label = (isActive ? "* " : "") + RumorBoardVM.TabLabels[i];
                var btn = ElarionUiKit.BuildObsidianButton(chipHost.transform, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    isActive ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    Vector2.zero, Vector2.one,
                    () => SetTab(tabKey));

                // One type size across the whole row: the kit fits a button label against its
                // own rect, so a long tab would otherwise render at a different size than its
                // neighbours (one of the "three visual languages" the review called out).
                var lbl = btn != null ? btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) : null;
                if (lbl != null)
                {
                    lbl.fontSize = ElarionUi.FontLabel;
                    // Ask for the label's MEASURED width (TMP's own metrics, rect-independent
                    // and valid before any layout pass) grossed up for the button art's own
                    // label inset. First capture after the flex-fill fix: an equal 1/5 share
                    // gave every tab ~159 ref px, whose ~132 px label rect lost "Endgame" by a
                    // hair -> "Endga...". With a measured ask the HLG seats each tab's natural
                    // width FIRST and only then shares the slack, so the longest tab gets the
                    // room it needs while every tab still clears TabChipMinPx.
                    float ask = lbl.GetPreferredValues(label).x;
                    if (ask <= 1f) ask = label.Length * 18f;   // atlas not ready -- honest estimate
                    le.preferredWidth = ask / TabLabelInsetFrac + TabChipPadPx;
                    ElarionUiKit.FitSingleLine(lbl, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);
                }
                else
                {
                    le.preferredWidth = TabChipMinPx;
                }

                if (isActive)
                {
                    var bar = new GameObject("Underline", typeof(Image));
                    bar.transform.SetParent(chipHost.transform, false);
                    var brt = bar.GetComponent<RectTransform>();
                    brt.anchorMin = new Vector2(0.10f, 0f);
                    brt.anchorMax = new Vector2(0.90f, 0f);
                    brt.pivot = new Vector2(0.5f, 0f);
                    brt.sizeDelta = new Vector2(0f, 6f);          // fixed px, never a fraction
                    brt.anchoredPosition = new Vector2(0f, 6f);
                    var bi = bar.GetComponent<Image>();
                    bi.color = ElarionUi.Gilt;
                    bi.raycastTarget = false;
                }
            }
        }

        /// <summary>An obsidian footer band hung from the wells' floor, with a gilt hairline on
        /// top. The shared kit Close is seated in that band by the factory and was reading as a
        /// button floating over the frame's bottom border (WO-866); the band is built as the
        /// FIRST child of chrome.content so it renders BEHIND the Close (and behind everything
        /// else) and simply gives it chrome to sit in.</summary>
        private void BuildFooterBand(bool portrait)
        {
            if (portrait || _chromeContent == null || _zoneLeft == null || _zoneRight == null) return;

            var go = new GameObject("FooterBand", typeof(Image));
            go.transform.SetParent(_chromeContent, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(_zoneLeft.anchorMin.x, _zoneLeft.anchorMin.y);
            rt.anchorMax = new Vector2(_zoneRight.anchorMax.x, _zoneLeft.anchorMin.y);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, FooterBandPx);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = PlateInk;
            img.raycastTarget = false;

            var rule = new GameObject("FooterRule", typeof(Image));
            rule.transform.SetParent(go.transform, false);
            var rrt = rule.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(0f, 2f);
            rrt.anchoredPosition = Vector2.zero;
            var ri = rule.GetComponent<Image>();
            ri.color = HairLine;
            ri.raycastTarget = false;
        }

        private void SetTab(string tab)
        {
            if (_vm == null || _vm.ActiveTab == tab) return;
            _selectedId = null;                    // the new tab auto-selects its first row
            _selectedKind = RowKind.None;
            _vm.SetTab(tab);                       // raises Changed -> Repaint -> EnsureSelection
            if (_ui != null) BuildTabStrip();
        }

        // -- Daily tab -----------------------------------------------------------

        private int RepaintDaily()
        {
            CreateSectionLabel(_contentRoot.transform, "- Daily Quests -");
            var daily = _vm.DailyQuests;
            if (daily == null || daily.Count == 0)
            {
                CreateFlavorRow(_contentRoot.transform, "No daily quests rolled yet. Check back later.");
                return 0;
            }
            int n = 0;
            foreach (var q in daily)
            {
                string hook = q.Completed ? "Complete (" + q.Target + "/" + q.Target + ")"
                                          : q.Progress + "/" + q.Target;
                CreateCard(_contentRoot.transform, q.Id, q.Title, hook,
                    q.Completed ? "Done" : "Today", RowKind.Daily);
                n++;
            }
            return n;
        }

        // -- Cards (two lines, no buttons, the whole card selects) ----------------

        private void CreateSectionLabel(Transform parent, string txt)
        {
            var go = new GameObject("Section", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = SectionBandPx;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = ElarionUi.FontLabel;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.BottomLeft;

            // The same gilt hairline the detail title and the footer band use.
            var rule = new GameObject("SectionRule", typeof(Image));
            rule.transform.SetParent(go.transform, false);
            var rrt = rule.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 2f);
            rrt.anchoredPosition = Vector2.zero;
            var ri = rule.GetComponent<Image>();
            ri.color = HairLine;
            ri.raycastTarget = false;
        }

        private void CreateFlavorRow(Transform parent, string txt)
        {
            var go = new GameObject("Flavor", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = FlavorBandPx;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = txt;
            t.fontSize = ElarionUi.FontMicro;
            t.fontStyle = TMPro.FontStyles.Italic;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Left;
        }

        /// <summary>WO-866 EARLY STATE. The review's "roughly half the panel is dead space":
        /// an early-game board has one rumor on it and the rest of the column was flat black,
        /// which reads as a broken panel rather than as an early one. This says it in words,
        /// in the same bordered-plate language as the cards.</summary>
        private void CreateEarlyNote(Transform parent, bool empty)
        {
            var plate = new GameObject("EarlyNote", typeof(Image), typeof(LayoutElement));
            plate.transform.SetParent(parent, false);
            plate.GetComponent<LayoutElement>().preferredHeight = EarlyNotePx;
            var border = plate.GetComponent<Image>();
            border.color = HairLine;
            border.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(plate.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var fimg = fill.GetComponent<Image>();
            fimg.color = PlateInk;
            fimg.raycastTarget = false;

            var head = new GameObject("Head", typeof(TMPro.TextMeshProUGUI));
            head.transform.SetParent(plate.transform, false);
            var hrt = head.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.04f, 1f);
            hrt.anchorMax = new Vector2(0.96f, 1f);
            hrt.offsetMin = new Vector2(0f, -58f);
            hrt.offsetMax = new Vector2(0f, -8f);
            var ht = head.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(ht);
            ht.text = empty ? "The board is quiet." : "You are early.";
            ht.fontSize = ElarionUi.FontLabel;
            ht.fontStyle = TMPro.FontStyles.Bold;
            ht.color = ElarionUi.Gilt;
            ht.alignment = TMPro.TextAlignmentOptions.Left;
            ht.raycastTarget = false;
            ElarionUiKit.FitSingleLine(ht, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);

            var body = new GameObject("Body", typeof(TMPro.TextMeshProUGUI));
            body.transform.SetParent(plate.transform, false);
            var brt = body.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.04f, 0f);
            brt.anchorMax = new Vector2(0.96f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, 54f);
            brt.anchoredPosition = new Vector2(0f, 8f);
            var bt = body.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(bt);
            // One FontMicro line at the list column's width -- long enough to say it, short
            // enough that the 54px band never has to wrap into a second (culled) line.
            bt.text = "Brom posts more as Elarion wakes.";
            bt.fontSize = ElarionUi.FontMicro;
            bt.color = ElarionUi.ParchmentDim;
            bt.alignment = TMPro.TextAlignmentOptions.TopLeft;
            bt.raycastTarget = false;
            ElarionUiKit.FitBlock(bt, ElarionUi.FontFloorMobile, ElarionUi.FontMicro);
        }

        private void CreateCard(Transform parent, string id, string title, string hook,
                                string pip, RowKind kind)
        {
            bool selected = id == _selectedId;

            var row = new GameObject("Card_" + id, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            // Cards are select-targets -- floor them at the kit touch floor.
            row.GetComponent<LayoutElement>().preferredHeight = CardHeightPx;
            // WO-866: the outer Image is the 2px border (chip language) so a row is a CRAFTED
            // plate, not the flat black bar the review called a debug list.
            var img = row.GetComponent<Image>();
            img.color = selected ? ElarionUi.Gilt : HairLine;

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(row.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var fimg = fill.GetComponent<Image>();
            // Selected = warmer fill; unselected = the dark stone plate.
            fimg.color = selected
                ? new Color(ElarionUi.PanelStone.r * 1.35f, ElarionUi.PanelStone.g * 1.30f,
                            ElarionUi.PanelStone.b * 1.10f, 0.95f)
                : new Color(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g,
                            ElarionUi.PanelStoneDark.b, 0.85f);
            fimg.raycastTarget = false;

            // Selected card carries a gilt left border (shape marker, not colour alone) --
            // the same text/shape-encoded discipline as the "*" on the selected tab.
            if (selected)
            {
                var edge = new GameObject("SelEdge", typeof(Image));
                edge.transform.SetParent(row.transform, false);
                var ert = edge.GetComponent<RectTransform>();
                ert.anchorMin = new Vector2(0f, 0f);
                ert.anchorMax = new Vector2(0f, 1f);
                ert.pivot = new Vector2(0f, 0.5f);
                ert.offsetMin = Vector2.zero;
                ert.offsetMax = new Vector2(8f, 0f);
                var ei = edge.GetComponent<Image>();
                ei.color = ElarionUi.Gilt;
                ei.raycastTarget = false;
            }

            // Line 1: title (bold parchment) + the state as a CHIP -- the same widget the
            // detail pane uses, so the list and the detail speak ONE language (WO-866).
            bool hasPip = !string.IsNullOrEmpty(pip);
            var titleGo = new GameObject("Title", typeof(TMPro.TextMeshProUGUI));
            titleGo.transform.SetParent(row.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.04f, 1f);
            trt.anchorMax = new Vector2(hasPip ? 0.70f : 0.96f, 1f);
            // 66px band: one FontBody(50) line box is 62.5 -- anything shorter culls the line.
            trt.offsetMin = new Vector2(0f, -70f);
            trt.offsetMax = new Vector2(0f, -4f);
            var tt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(tt);
            tt.text = title;
            tt.fontSize = ElarionUi.FontBody;
            tt.fontStyle = TMPro.FontStyles.Bold;
            tt.color = ElarionUi.Parchment;
            tt.alignment = TMPro.TextAlignmentOptions.Left;
            tt.raycastTarget = false;
            ElarionUiKit.FitSingleLine(tt, ElarionUi.FontFloorMobile, ElarionUi.FontBody);

            if (hasPip)
            {
                // A right-aligned one-chip row: fixed 48px band, so it can never be culled.
                var pipRow = MakeChipRow(row.transform, "PipRow",
                    topPx: 8f, heightPx: ChipHeightPx, fromBottomPx: -1f,
                    sideFrac: 0f, spacingPx: ChipSpacingPx, align: TextAnchor.MiddleRight);
                var prt = pipRow;
                prt.anchorMin = new Vector2(0.70f, 1f);
                prt.anchorMax = new Vector2(0.96f, 1f);
                prt.pivot = new Vector2(0.5f, 1f);
                prt.sizeDelta = new Vector2(0f, ChipHeightPx);
                prt.anchoredPosition = new Vector2(0f, -8f);
                MakeChip(pipRow, pip);
            }

            // Line 2: one-line hook, dim, ellipsized (never wraps into a third line).
            var hookGo = new GameObject("Hook", typeof(TMPro.TextMeshProUGUI));
            hookGo.transform.SetParent(row.transform, false);
            var hrt = hookGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.04f, 0f);
            hrt.anchorMax = new Vector2(0.96f, 0f);
            // 44px band under the 66px title inside a 124px card: 4px of clearance between
            // them, and one whole FontMicro(32) line box (40) so the hook is never culled.
            hrt.offsetMin = new Vector2(0f, 6f);
            hrt.offsetMax = new Vector2(0f, 50f);
            var ht2 = hookGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(ht2);
            ht2.text = hook;
            ht2.fontSize = ElarionUi.FontMicro;
            ht2.color = ElarionUi.ParchmentDim;
            ht2.alignment = TMPro.TextAlignmentOptions.Left;
            ht2.raycastTarget = false;
            ElarionUiKit.FitSingleLine(ht2, ElarionUi.FontFloorMobile, ElarionUi.FontMicro);

            // The whole card is the select target -- no buttons live in rows.
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            string rid = id;
            RowKind rkind = kind;
            btn.onClick.AddListener(() => SelectRow(rid, rkind));
        }

        private void SelectRow(string id, RowKind kind)
        {
            if (_selectedId == id && _selectedKind == kind) return;
            _selectedId = id;
            _selectedKind = kind;
            Repaint();   // repaints the card highlight + rebinds the detail
        }

        // -- Detail pane (always bound to the selection) --------------------------

        private void RenderDetail()
        {
            if (_detailTitle == null || _detailBody == null || _vm == null) return;

            // Chip rows + CTA are rebuilt per selection (their content/state change per row).
            ClearChildren(_detailTagRow);
            ClearChildren(_detailRewardRow);
            if (_detailCtaGo != null) { SafeDestroy(_detailCtaGo); _detailCtaGo = null; }

            if (_selectedId == null || _selectedKind == RowKind.None)
            {
                _detailTitle.text = "The Board Awaits";
                ElarionUiKit.FitSingleLine(_detailTitle, ElarionUi.FontFloorMobile, ElarionUi.FontBody);
                _detailBody.text = "Pick a rumor on the left and Brom will tell you where the trouble started.";
                _detailBody.fontSize = ElarionUi.FontLabel;
                ElarionUiKit.FitBlock(_detailBody, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);
                return;
            }

            if (_selectedKind == RowKind.Daily)
            {
                var d = FindDaily(_selectedId);
                MakeChip(_detailTagRow, "Daily Quest");
                if (d.HasValue)
                    MakeChip(_detailTagRow, d.Value.Completed ? "Complete" : "In Progress");
                _detailTitle.text = d.HasValue ? d.Value.Title : _selectedId;
                ElarionUiKit.FitSingleLine(_detailTitle, ElarionUi.FontFloorMobile, ElarionUi.FontBody);
                string prog = d.HasValue
                    ? (d.Value.Completed
                        ? "Complete (" + d.Value.Target + "/" + d.Value.Target + ")"
                        : d.Value.Progress + " of " + d.Value.Target)
                    : "";
                _detailBody.text = "Objective: " + prog + "\nDailies reset with the day.";
                _detailBody.fontSize = ElarionUi.FontLabel;
                ElarionUiKit.FitBlock(_detailBody, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);
                return;   // dailies have no CTA -- they advance through play
            }

            bool active = _selectedKind == RowKind.Active;
            string title = TitleOf(_selectedId);
            bool tracked = IsTracked(_selectedId);

            // Tag chips: quest type + state. Bordered shape + the WORD, one ink, never colour.
            MakeChip(_detailTagRow, _vm.TypeFor(_selectedId) + " Quest");
            MakeChip(_detailTagRow, active ? (tracked ? "Tracked" : "In Progress") : "New");

            _detailTitle.text = title;
            ElarionUiKit.FitSingleLine(_detailTitle, ElarionUi.FontFloorMobile, ElarionUi.FontBody);

            // Body: the objective, in the band's honest budget. The old copy repeated the hook
            // and then appended the SAME generic paragraph to every quest -- filler that made
            // the pane read as a template, and it did not render at all (the band was -11 px).
            _detailBody.text = active
                ? "Objective: " + _vm.ObjectiveFor(_selectedId) +
                  "\nTrack pins it to your HUD."
                : "Objective: " + _vm.HookFor(_selectedId) +
                  "\nAccept to add it to your ledger.";
            _detailBody.fontSize = ElarionUi.FontLabel;
            ElarionUiKit.FitBlock(_detailBody, ElarionUi.FontFloorMobile, ElarionUi.FontLabel);

            // Reward chips: one chip per authored reward part. The VM hands over READY-TO-DRAW
            // parts (resolved item display names included) -- the View never re-parses a joined
            // string to find the chip boundaries.
            var rewardParts = _vm.RewardPartsFor(_selectedId);
            if (rewardParts != null)
                foreach (var part in rewardParts)
                    if (!string.IsNullOrEmpty(part))
                        MakeChip(_detailRewardRow, part, ChipMinFontPx);

            // CTA row -- bottom-hung FIXED-PIXEL band at the kit touch floor, inside the pane's
            // declared budget (DetailFixedStackPx). The kit's close-band reservation already
            // keeps the well clear of the shared Close.
            _detailCtaGo = new GameObject("DetailCta", typeof(RectTransform));
            _detailCtaGo.transform.SetParent(_detailPane, false);
            var crt = _detailCtaGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.04f, 0f);
            crt.anchorMax = new Vector2(0.96f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(0f, DetailCtaPx);
            crt.anchoredPosition = new Vector2(0f, DetailPadPx);

            string id = _selectedId;
            if (!active)
            {
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform, "Accept",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0f, 0f), new Vector2(0.52f, 1f), () => OnAccept(id));
                // Track rides SECONDARY beside Accept -- accept-and-pin in one visit (Track on
                // an available rumor accepts it first, then pins + closes).
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform, "Track",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.56f, 0f), new Vector2(1f, 1f), () => { OnAccept(id); OnTrack(id); });
            }
            else
            {
                // The WORD carries the state ("Pinned" vs "Track") -- never colour alone.
                ElarionUiKit.BuildObsidianButton(_detailCtaGo.transform,
                    tracked ? "Pinned" : "Track",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    tracked ? ElarionUiKit.ObsidianButtonColor.Green
                            : ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0f, 0f), new Vector2(0.52f, 1f), () => OnTrack(id));
            }
        }

        private string TitleOf(string id)
        {
            foreach (var i in _vm.AvailableQuests) if (i.Id == id) return i.Name;
            foreach (var i in _vm.ActiveQuests) if (i.Id == id) return i.Name;
            return id;
        }

        private bool IsTracked(string id)
        {
            foreach (var i in _vm.ActiveQuests) if (i.Id == id) return i.Equipped;
            return false;
        }

        private TMPro.TextMeshProUGUI MakeDetailLabel(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, Color color, float size, bool bold)
        {
            var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = size;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            t.color = color;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.raycastTarget = false;
            return t;
        }

        // -- Chip rows (the ONE chip language) -----------------------------------

        /// <summary>A horizontal chip host as a FIXED-PIXEL band (a fraction band scales with
        /// the zone, under-heights the chips and TMP culls their labels whole). Top-hung at
        /// <paramref name="topPx"/> below the parent top, or bottom-hung at
        /// <paramref name="fromBottomPx"/> above the parent bottom when &gt;= 0.</summary>
        private static RectTransform MakeChipRow(Transform parent, string name,
            float topPx, float heightPx, float fromBottomPx = -1f,
            float sideFrac = 0.04f, float spacingPx = 8f,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            bool fromBottom = fromBottomPx >= 0f;
            rt.anchorMin = new Vector2(sideFrac, fromBottom ? 0f : 1f);
            rt.anchorMax = new Vector2(1f - sideFrac, fromBottom ? 0f : 1f);
            rt.pivot = new Vector2(0.5f, fromBottom ? 0f : 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, heightPx);
            rt.anchoredPosition = new Vector2(0f, fromBottom ? fromBottomPx : -topPx);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = false;
            hlg.childAlignment = align;
            hlg.spacing = spacingPx;
            return rt;
        }

        /// <summary>ONE chip: gilt-dim border frame, obsidian fill, parchment micro label.
        /// WO-866: the SAME widget, ink and border for a type tag, a state tag, a list row's
        /// state pip and a reward -- the review found three visual languages for one class of
        /// information. Shape (the border) + the WORD carry the meaning; nothing is encoded in
        /// colour (colourblind law). The chip is sized from its label's MEASURED width and the
        /// label is FITTED to the chip, so a crowded row shrinks text INSIDE the borders
        /// instead of painting it across the next chip.</summary>
        private static void MakeChip(RectTransform row, string text, float minFontPx = 0f)
        {
            if (row == null || string.IsNullOrEmpty(text)) return;

            var chip = new GameObject("Chip", typeof(Image), typeof(LayoutElement));
            chip.transform.SetParent(row, false);
            var borderImg = chip.GetComponent<Image>();
            borderImg.color = ChipBorder;
            borderImg.raycastTarget = false;
            var le = chip.GetComponent<LayoutElement>();
            // Height 48 inside the 48px row: the fill inset leaves a 44px label rect, which
            // seats the FontMicro (~40px) line box AND the fitted floor's (~33px) -- never
            // culled vertically, which is why Ellipsis is safe on this label (below).
            le.preferredHeight = ChipHeightPx;
            le.flexibleWidth = 0f;   // a chip never absorbs slack -- it stays word-sized

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(chip.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = ChipFill;
            fillImg.raycastTarget = false;

            var lblGo = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            lblGo.transform.SetParent(fill.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var t = lblGo.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = ElarionUi.FontMicro;
            t.color = ElarionUi.Parchment;   // ONE ink for every chip
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.raycastTarget = false;

            // MEASURE the label (TMP's own metrics, rect-independent -- valid at build time,
            // before any layout pass) and size the chip to it.
            float textW = t.GetPreferredValues(text).x;
            if (textW <= 1f) textW = text.Length * 14f;   // font atlas not ready -- honest estimate
            le.preferredWidth = textW + ChipPadPx;
            le.minWidth = 0f;   // a too-full row still shrinks rather than spilling off the pane

            // FIT the label to whatever width the chip ends up with: bounded auto-size, then
            // Ellipsis. The 44px label rect seats the floor's line box with room to spare, so
            // Ellipsis's vertical-cull trap does not apply here.
            ElarionUiKit.FitSingleLine(t, minFontPx > 0f ? minFontPx : ElarionUiKit.FontFloor,
                                       ElarionUi.FontMicro);
        }

        // -- Commands ------------------------------------------------------------

        private void OnAccept(string id)
        {
            if (_vm == null) return;
            _vm.Accept(id);     // StartQuest + status; the VM raises Changed -> Repaint
            SetStatus(_vm.Status);
        }

        private void OnTrack(string id)
        {
            // The VM pins it (SetTracked) then invokes onClose -> this View's Close.
            if (_vm != null) _vm.Track(id);
        }

        // -- Helpers -------------------------------------------------------------

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        private void ClearContent()
        {
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) SafeDestroy(c.gameObject);
            }
        }

        /// <summary>Destroys immediately in edit mode (the UICaptureLaunch headless-screenshot
        /// path repaints without Play -- runtime Destroy is edit-illegal), normally in play.</summary>
        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static void ClearChildren(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var c = host.GetChild(i);
                if (c != null) SafeDestroy(c.gameObject);
            }
        }
    }
}
