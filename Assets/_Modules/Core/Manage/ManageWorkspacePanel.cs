// =============================================================================
// ManageWorkspacePanel - WO-2002. THE ONE DUMB RENDERER every Manage tab
// (BUILD / ARMY / RESEARCH) paints through.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Manage
//
// ============================ THE DUMB-UI RULE ===============================
// Canon 9. This file MAY: bind text supplied by the VM, bind sprites by supplied
// asset key, show/hide by explicit state fields, invoke supplied callbacks, render
// supplied progress values, apply common visual primitives.
//
// This file MAY NOT: calculate costs, inspect player resources, decide locks,
// determine Heart requirements, determine max level, read queue service state,
// calculate queue capacity, derive labels from enum names, parse ids, decide which
// destination a prerequisite CTA opens, calculate upgrade deltas, mutate save data,
// or call Barracks / BuildTimer / Research / Heart services.
//
// ⛔ THAT LIST IS ENFORCED BY A SOURCE ORACLE, NOT BY REVIEW:
//    Assets/Editor/Regression/ManageDumbViewRegression.cs, marker MANAGE_DUMB_VIEW_OK.
// It scans THIS FILE for each banned shape, self-tests every pattern RED against a
// fixture first, and carries a REVERT RECIPE per case. A rule nobody can check is a
// rule that decays - and this repo has the receipts (CLAUDE.md 2, 5, 16).
//
// TWO STRUCTURAL GUARDS BACK THE ORACLE UP:
//  1. ASSEMBLY. This file lives in DeNelle.Core, which does NOT reference
//     DeNelle.Village - so BuildTimerService (Village/Buildings/BuildTimerService.cs)
//     and BarracksService (Village/Troops/BarracksService.cs) are not merely
//     forbidden here, they are UNREACHABLE. The oracle still bans them by name
//     because GameStateService DOES live in Core (Core/State/GameStateService.cs)
//     and the assembly boundary alone would not stop a save read.
//  2. NO INFERENCE FROM NULL. Every state the renderer reads is an explicit field
//     (Visible / Enabled / IsSelected / VisualState). It calls `cb?.Invoke()` and
//     never `if (cb != null)` as a state test; the oracle bans the comparison form.
//
// ⛔ NOT A MonoBehaviour, AND THAT IS DELIBERATE. WO-2002 names it a "panel", but
// PanelDoorRegression (Assets/Editor/Regression/PanelDoorRegression.cs:20-29)
// defines panel-like as MonoBehaviour + a name ending in "Panel", and FAILS any
// such type with no door. This is a RENDERER a host embeds - the same shape as
// ElarionUiKit.BuildObsidianPanel: build under a supplied parent, return handles -
// not a destination the player routes to. Keeping it a plain class is honest about
// that AND keeps the door oracle's teeth sharp for real panels. The dumb-view
// oracle asserts this file contains no ": MonoBehaviour" so nobody "upgrades" it
// later and silently trips panel-door. WO-2001 owns the host that opens it.
//
// ============================= THE BAND LAW ==================================
// ⚠ TMP CULLS AN ENTIRE LINE whose fontSizeMin cannot seat in its rect: a text band
// under about 24 reference px renders BLANK, not small. That cost three separate
// defects on 2026-09-06. So:
//   * every band's height is a FIXED PIXEL constant, stated below with its px;
//   * the heights are SUMMED, subtracted from the MEASURED well, and the GRID takes
//     the remainder - the same law ManageScreenPanel.cs:60-72 already holds after the
//     BUILD-1 overprinting defect;
//   * MinTextBandPx (28) is a HARD FLOOR. A band that cannot be given 28px is
//     OMITTED and announced in px through FlowTrace - never shrunk. An omitted band
//     is visibly missing; a culled one is invisible, and invisible is the trap;
//   * every tap target is authored at or above ElarionUiKit.MinTouchPx (112) so
//     ClampMinTouch never has to rescue it. ElarionUiKit.cs:1100 states the rule:
//     "Author the band above MinTouchPx; do not rely on the clamp." A clamp growth
//     is a control spilling into its neighbour.
//
// MEASURED WELLS this table is annotated against: 533 / 542 / 612 reference px, the
// three captured Manage body wells (ManageScreenPanel.cs:194 "at 2670x1200 well=533";
// HeartPanel.cs:23-30 cites the same span from Builds/manage-redesign-capture.log).
// ⚠ FINDING HANDED BACK WITH THIS WORK ORDER, AND IT IS ARITHMETIC, NOT AN OPINION.
// The MINIMUM stack is header 120 + tabs 120 + selection FLOOR 256 + three 12px
// gaps = 532px, before any filter row, any activity strip and any grid at all. The
// three captured Manage wells are 533 / 542 / 612px. So in the old modal chrome the
// workspace has 1px left for the grid - i.e. THE GRID CANNOT EXIST THERE, and
// canon 3's "at least 12 visible tiles" is unreachable by a factor of about four.
// ⛔ AND THE CONSEQUENCE IS WORSE THAN A CRAMPED GRID. Once the grid clamps to its
// 150px floor the cursor stands at 264 + 150 + 12 = 426px, and the 256px selection
// floor then ends at 682px - inside a 533px well. THE CTA ROW, the single most
// important control on the screen, IS OFF THE BOTTOM on the old modal chrome.
// A full-screen well of roughly 1450px is what the canon actually implies:
// 532 fixed + the filter row 132 + a 4-row BUILD grid at the 190px tile ceiling
// (4 x 190 + 3 x 10 = 790) = about 1454px.
// ⛔ THAT IS WO-2001's CALL (information architecture / the host chrome), NOT this
// file's. This renderer never silently re-columns, never shrinks a text band and
// never paints a culled screen: it measures, trims the selection card in a stated
// order, and REPORTS the shortfall in px through FlowTrace.
//
// ⚠ THE 532px FIGURE ABOVE IS RETIRED ARITHMETIC (WO-1443, 2026-09-06). THREE of its
// four terms are gone on a grid screen: the HEADER BAND no longer exists (breadcrumb
// -> the host's panel title, QUEUE -> the host's top-right pill), the SELECTION band
// collapses to 0 when nothing is selected, and the ACTIVITY strip is retired from
// Manage entirely (the mockup carries running work on the QUEUE badge and in the
// QUEUE overlay, screen 8 - not on every screen). The minimum stack on a grid screen
// is now tabs 120 + one gap 12 = 132px, or 264px with a filter row.
//
// AND THE WELL ITSELF GREW, which is the half that actually mattered. The measured
// gap was one number: MANAGE_FLOW_INVENTORY reported ARMY content=590px in a 190px
// viewport against a mockup panel that says "All 9 troops visible, no scrolling".
// The host now takes 0.02-0.98 of the canvas (was 0.05-0.95) and lifts the body
// ceiling to meet the chrome row at 0.845 (the strip between 0.835 and the frame's
// own header zone at 0.900 held nothing), so the well goes ~533px -> ~553px.
// Against that well, with SQUARE cells and AUTHORED capacity (ManageTabVM.GridRows):
//   ARMY  3x3: grid 553 - 132(tabs)          = 421px -> cell 134px, NINE tiles, no scroll
//   BUILD 5x2: grid 553 - 132(tabs) - 132(chips) = 289px -> cell 140px, TEN tiles, no scroll
// Both clear MinTileHeightPx(120) - which is only legal because the tile now carries
// ONE text band instead of two. Retiring the tab row into the host chrome (it belongs
// to the mockup's HUB, screen 1) is the next ~132px and takes ARMY's cell to ~178px.
// ⚠ AND THE OTHER HALF OF FIX (a) IS STILL OPEN: a DETAIL screen has no tiles, yet
// the grid band is still reserved at its MinTileHeightPx floor. Skipping it when the
// tab has no tiles is the matching change and belongs to WO-2001, not here.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.Manage
{
    /// <summary>
    /// The common dumb Manage renderer. Construct it over a body RectTransform, call
    /// <see cref="Bind"/> whenever the model changes, <see cref="Clear"/> to tear down.
    /// It has no Update: re-binding on a tick is the host's job, which keeps every
    /// value on screen a value the model handed over.
    /// </summary>
    public sealed class ManageWorkspacePanel
    {
        // ── BAND TABLE (fixed reference px). Every one clears MinTextBandPx. ──
        // ⛔ THERE IS NO HeaderBandPx. WO-1443 emptied the header band (breadcrumb -> the host's
        // panel title, sub line -> deleted, QUEUE door -> the tab row) and then deleted the band
        // rather than leaving a 120px constant nothing seats in. See BuildTabs' summary.
        // ⛔ NO TabsBandPx EITHER. The BUILD | ARMY | RESEARCH row is gone with the header band -
        // the mockup navigates by the HUB (panel 1) and the back arrow, and no panel draws a tab
        // row. Its 120px + 12px went to the grid. See BuildFilters' band note for the live one.
        private const float FiltersBandPx = 120f;  // same as tabs; 0 when the tab supplies no filters
        private const float ActivityBandPx = 120f; // strip is tappable (OpenQueue) so it is a touch band
        private const float BandGapPx = 12f;       // guaranteed gutter - no two bands ever touch

        // Selection card sub-bands, top-down inside the selection band.
        // ⛔ THE OLD STACKED-CARD BAND TABLE IS DELETED - EIGHT CONSTANTS AND TWO HELPERS.
        // SelTitlePx / SelLevelPx / SelDescPx / SelStatsPx / SelCostPx / SelWhyPx / SelGapPx /
        // SelActionPx / SelectionFullPx, plus StatsLine() and CostLine(), belonged to the card that
        // shared a well with the grid and DROPPED sub-bands when it was short. WO-1443 gave the
        // detail screen the whole body and a two-column layout that flows in px, so none of them was
        // read any more. They are deleted rather than left standing: a constant nothing reads is the
        // duplicated state this file has already been burned by twice today (HeaderSubtitle, and the
        // AtCapacity flag whose pip rendered outside the panel).
        // SelectionFloorPx SURVIVES - the card-shortfall warn reads it, and it is the four bands
        // canon 11 makes non-negotiable: what is it, what does it cost, why can I not act, what can
        // I do.
        private const float SelectionFloorPx = 256f;

        private const float MinGridPx = 130f;      // one MinTileHeightPx(120) row plus a gap
        // 120px: one 0.24 name band = 28.8px, clear of the MinTextBandPx(28) cull floor, and 120 is
        // itself MinTouchPx. Was 150 while the tile carried TWO text bands; the mockup's tile carries
        // one (see the tile band table below), and 150 would have made the mockup's own stated
        // capacity - "All 9 troops visible, no scrolling" - arithmetically impossible in this well.
        private const float MinTileHeightPx = 120f;
        // ⚠ A CEILING IS AS NECESSARY AS A FLOOR. Cell height tracks cell WIDTH so tiles stay
        // squarish, and on a wide band four columns give ~250-300px of width - which would make
        // one row of tiles taller than the entire grid band and silently defeat canon 3's
        // "12 visible tiles". Clamped, so the tile fractions below stay annotated over a range
        // that is actually reachable (150-190px).
        private const float MaxTileHeightPx = 190f;
        private const float TileGapPx = 10f;

        /// <summary>
        /// ⚠ THE TMP CULL FLOOR. A text band below this renders BLANK, not small. Bands that
        /// cannot be given this much are OMITTED and reported, never shrunk.
        /// </summary>
        private const float MinTextBandPx = 28f;

        // ── Tile internals, as fractions of the cell. px stated at cell=150 / 190. ──
        // ⛔ THE TILE IS ART + ONE NAME STRIP. That is what the mockup draws, on both grid panels.
        // docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png screens 2 and 4: every tile is a square of
        // art with the name on a strip across its bottom. There is no second text line on a grid
        // tile - the STATE lives on the detail screen (screen 9 draws "Requires Barracks Tier 4"
        // there, beside a padlock), and on the tile it is the medallion that carries it.
        // ⚠ THIS IS ALSO WHAT MAKES THE MOCKUP'S CAPACITY LEGAL. Two text bands at 0.20 and 0.22 of
        // the cell need a 150px cell to clear the 28px TMP cull floor (a band under it renders
        // BLANK, not small). One band at 0.24 clears the same floor at 117px, which is why
        // MinTileHeightPx could honestly come down to 120 and nine troops can be on screen at once.
        // Dropping the second band is not a cosmetic simplification; it is the arithmetic.
        private const float TileProgY0 = 0.005f, TileProgY1 = 0.02f;  // a BAR, no text - exempt
        private const float TileTitleY0 = 0.02f, TileTitleY1 = 0.26f; // 24.0% -> 29px at the 120px floor
        private const float TilePortY0 = 0.28f, TilePortY1 = 0.99f;   // the art, square, above the name
        private const float TilePortX0 = 0.14f, TilePortX1 = 0.86f;
        private const float TileMedX0 = 0.63f, TileMedX1 = 0.98f;     // status medallion, top-right
        private const float TileMedY0 = 0.60f, TileMedY1 = 0.95f;
        // ⛔ NO TileSelBarX1. The selected tile's cue is a GOLD BORDER around the whole tile
        // (CAPTURE_LOOP_GOAL 3.0b, and the mockup draws it that way on screens 2/4/6), not the
        // left-edge bar this constant used to seat. The constant went with the bar rather than
        // sitting here unread.

        // ── Handles ───────────────────────────────────────────────────────────
        private readonly RectTransform _body;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>Last measured well height in reference px. Diagnostics only.</summary>
        public float LastWellPx { get; private set; }

        /// <summary>Last computed grid band height in reference px. Diagnostics only.</summary>
        public float LastGridPx { get; private set; }

        /// <summary>
        /// The queue model from the most recent <see cref="Bind"/>. The HOST paints the door - a
        /// small top-right pill with a count badge, exactly as the owner's mockup draws it on every
        /// panel - and reads the model from here so there is ONE composed queue projection on the
        /// screen instead of the host quietly growing a second one.
        /// </summary>
        public ManageQueueVM Queue { get; private set; }

        public ManageWorkspacePanel(RectTransform body)
        {
            _body = body;
            if (_body == null)
                FlowTrace.Fail("Manage", "ManageWorkspacePanel was constructed over a null body rect - " +
                    "nothing will render and no tab will report why");
        }

        // =====================================================================
        //  BIND - the only entry point. Rebuilds the whole workspace from the VM.
        // =====================================================================

        /// <summary>
        /// Paints <paramref name="vm"/>. A null VM clears the surface and says so; it never
        /// leaves the previous frame's pixels on screen pretending to be current.
        /// </summary>
        public void Bind(ManageWorkspaceVM vm)
        {
            Clear();
            if (_body == null) return;
            if (vm == null)
            {
                FlowTrace.Warn("Manage", "Bind received a null ManageWorkspaceVM - the workspace is " +
                    "cleared rather than left showing a stale frame");
                return;
            }
            Guard.Try("Manage", "render manage workspace", () => Build(vm));
        }

        /// <summary>Destroys everything this renderer spawned. Safe to call repeatedly.</summary>
        public void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var go = _spawned[i];
                if (go == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        // =====================================================================
        //  LAYOUT - the band law, in one place
        // =====================================================================

        private void Build(ManageWorkspaceVM vm)
        {
            float well = MeasureWellPx();
            LastWellPx = well;

            // The renderer no longer PAINTS the queue door (it is the host's top-right pill since
            // WO-1443), but it still binds the model and hands it over, so there is exactly one
            // composed queue projection on the screen rather than the host growing a second one.
            Queue = vm.Queue;

            ManageTabVM tab = vm.ActiveTab;
            bool hasFilters = tab != null && tab.Filters != null && tab.Filters.Count > 0;
            bool hasActivity = tab != null && tab.Activity != null && tab.Activity.Visible;

            // ⛔ NOTHING ABOVE THE GRID BUT THE FILTER CHIPS. Do not put a band back here.
            // The header band went first (breadcrumb -> the host's panel title, QUEUE -> the host's
            // top-right pill). The TAB row went second, with the hub: mockup panels 2/4/6 carry a
            // title, chips on BUILD, and then the grid - no tab row anywhere. Navigation is panel 1
            // plus the back arrow. Between them that is 264px handed to the tiles, which is what
            // finally lets ARMY seat three ~188px rows instead of three ~134px ones.
            float fixedTop = (hasFilters ? FiltersBandPx + BandGapPx : 0f);
            float fixedBottom = (hasActivity ? ActivityBandPx + BandGapPx : 0f);

            // ⛔ WO-1443 section 3 - THE SELECTION BAND IS NOT RESERVED WHEN NOTHING IS SELECTED.
            // Owner felt-test 2026-09-06, verbatim: "dont need the bottom line, close button is
            // enough". Her capture showed roughly 40% of the screen given to a bordered box holding
            // one hint sentence, because the band was reserved unconditionally and the model filled
            // it with EmptyText. The sentence is deleted at source (ManageScreenVM.FillActiveTab
            // sets EmptyText = null) and the band now COLLAPSES TO ZERO with it, so the grid takes
            // the room - the alternative the ruling explicitly forbids is an empty bordered box
            // with a lone button in it, which is the same 40% doing even less.
            // This is also fix (a) that this file's own header hands back to WO-2001: "the renderer
            // must skip the selection band when Selection.Visible is false".
            bool hasSelection = tab != null && tab.Selection != null && tab.Selection.Visible;
            bool hasTiles = tab != null && tab.Tiles != null && tab.Tiles.Count > 0;

            // ⛔ A GRID SCREEN AND A DETAIL SCREEN ARE DIFFERENT SCREENS. They never share the well.
            // The owner's mockup is explicit about this and it is the shape the model already emits:
            //   panels 2 / 4 / 6 - title, chips on BUILD, GRID. No card.
            //   panels 3 / 5 / 9 - title, one item's DETAIL, filling the panel. No grid.
            // The old layout reserved BOTH on every screen, which is what produced a 392px bordered
            // box holding one hint sentence under a grid that had 150px (the owner's original
            // complaint) and, on a detail screen, an empty 150px grid band above the card.
            // So the well goes to whichever screen this IS - and that completes fix (a) from this
            // file's header, whose second half ("skip the grid band when the tab has no tiles") had
            // been owed since WO-2002.
            float body = well - fixedTop - fixedBottom;
            float selection = 0f, grid = 0f;
            if (hasTiles)
            {
                grid = body;
            }
            else if (hasSelection)
            {
                // The DETAIL screen takes the whole body. It no longer trims sub-bands to fit a
                // 392px reservation, because it is not sharing with anything.
                selection = body;
            }
            else
            {
                // Neither: the grid band paints the model's EmptyText sentence, which is the honest
                // empty state ("Nothing in this filter yet.").
                grid = body;
            }

            if (hasTiles && grid < MinTileHeightPx)
            {
                FlowTrace.Warn("Manage", "grid band is " + grid.ToString("0") + "px - under one " +
                    MinTileHeightPx + "px tile row. The workspace needs a taller well than this host " +
                    "gives it (WO-2001); the grid is clamped and will scroll");
                grid = Mathf.Max(grid, MinTileHeightPx);
            }
            if (hasSelection && selection < SelectionFloorPx)
                FlowTrace.Warn("Manage", "detail card has " + selection.ToString("0") + "px, under the " +
                    SelectionFloorPx + "px floor for the four bands canon 11 makes non-negotiable " +
                    "(what is it, what does it cost, why can I not act, what can I do)");
            LastGridPx = grid;

            float cursor = 0f;
            if (hasFilters)
            {
                BuildFilters(BandFromTop(_body, "ManageFilters", cursor, FiltersBandPx), tab);
                cursor += FiltersBandPx + BandGapPx;
            }

            // Exactly ONE of these two paints. See the note above: a grid screen and a detail screen
            // are different screens and never share the well.
            if (grid > 0f)
            {
                BuildGrid(BandFromTop(_body, "ManageGrid", cursor, grid), tab);
                cursor += grid + BandGapPx;
            }

            if (hasActivity)
            {
                BuildActivity(BandFromTop(_body, "ManageActivity", cursor, ActivityBandPx), tab.Activity);
                cursor += ActivityBandPx + BandGapPx;
            }

            if (selection > 0f)
                BuildSelection(BandFromTop(_body, "ManageSelection", cursor, selection), tab, selection);

            FlowTrace.Step("Manage", "MANAGE_SCREEN " + (hasTiles ? "GRID" : hasSelection ? "DETAIL" : "EMPTY") +
                " well=" + well.ToString("0") + " grid=" + grid.ToString("0") +
                " detail=" + selection.ToString("0") + "px");
        }

        /// <summary>
        /// The body's height in reference px. Falls back to the kit's post-scale canvas height
        /// when the rect has not been laid out yet - and SAYS SO, because a silent fallback here
        /// would make every band number in the trace a fiction.
        /// </summary>
        private float MeasureWellPx()
        {
            float h = _body != null ? _body.rect.height : 0f;
            if (h > 1f) return h;
            float fallback = ElarionUiKit.PostScaleCanvasHeight(_body);
            FlowTrace.Warn("Manage", "body rect has no height yet (rect.height=" + h.ToString("0.##") +
                ") - band arithmetic falls back to the canvas height " + fallback.ToString("0") +
                "px. Bind after a layout pass to get real numbers");
            return fallback;
        }

        // =====================================================================
        //  BANDS
        // =====================================================================

        /// <summary>
        /// ⛔ THERE IS NO HEADER BAND. Do not add one back.
        /// <para>WO-1443 sections 1 and 1B, owner felt-test 2026-09-06: <i>"remove the manage army and
        /// sub line replace the manage top"</i> and <i>"remove heart level queue"</i>. The band used to
        /// carry THREE things - a breadcrumb copy, a sub line, and the QUEUE chip with an
        /// "IDLE . 0 OF 5" line under it. The breadcrumb moved UP into the host chrome's panel title
        /// (ManageScreenPanel.ApplyWorkspaceTitle), the sub line is deleted from the contract, and the
        /// QUEUE door moved into the HOST CHROME as a small top-right pill. With nothing left to hold
        /// the band itself is gone, and its 120px + 12px gutter went to the grid - which, together
        /// with the collapsing selection band (section 3), is where the space the owner wanted comes
        /// from.</para>
        /// <para>⛔ AND THERE IS NO QUEUE FACE IN THIS ROW EITHER - THAT SEAT IS RETIRED.
        /// It existed for about four hours on 2026-09-06, built from the owner's words before her
        /// MOCKUP was in the repo. The mockup
        /// (docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png) draws QUEUE as a SMALL PILL AT TOP-RIGHT
        /// with a count badge on every one of its eight numbered panels, and had said so since 09:26
        /// that morning; she restated it in words the same day - <i>"the queuing doesn't deserve a
        /// place here... something small up with like the previous next back kind of buttons - I don't
        /// think it deserves its own lane."</i> The tab-row seat also produced two defects of its own
        /// in the 14:59 capture: a truncated <c>QUEUE . FULL 5 O...</c> face and a stray gold bar.
        /// Both die with the seat. The door now lives at
        /// ManageScreenPanel.BuildTabs (the host's chrome row) - it MOVED, it was never dropped.</para>
        ///
        /// <para>⛔ AND THERE IS NO TAB ROW AT ALL ANY MORE - BuildTabs IS DELETED, NOT EMPTIED.
        /// Mockup panels 2, 4 and 6 carry a title, the filter chips on BUILD, and then the grid.
        /// There is no BUILD | ARMY | RESEARCH row on any panel; navigation is the HUB (panel 1)
        /// plus the back arrow, which is what ManageScreenPanel.ShowLauncher restores. Its 120px
        /// band and 12px gutter were the last reserve standing between ARMY and the mockup's own
        /// stated capacity, and they are now the tiles': three rows go from ~134px to ~188px.
        /// The model still composes <c>Tabs</c> - the hub and ActiveTabIndex both need it - so this
        /// is a RENDERING that stopped, not a concept that was removed.</para>
        /// </summary>

        private void BuildFilters(RectTransform band, ManageTabVM tab)
        {
            var filters = tab.Filters;
            for (int i = 0; i < filters.Count; i++)
            {
                var f = filters[i];
                if (f == null) continue;
                // Full band height (0f..1f) = 120px >= MinTouchPx(112). See BuildTabs' note.
                var btn = ElarionUiKit.Button(band, f.Label ?? string.Empty,
                    f.IsActive ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(SlotStart(i, filters.Count), 0f),
                    new Vector2(SlotEnd(i, filters.Count), 1f),
                    MakeInvoker(f.Activate));
                Track(btn);
                if (f.IsActive) Underline(btn.transform);
            }
        }

        // =====================================================================
        //  GRID
        // =====================================================================

        private void BuildGrid(RectTransform band, ManageTabVM tab)
        {
            if (tab == null) return;
            var tiles = tab.Tiles;
            if (tiles == null || tiles.Count == 0)
            {
                var empty = ElarionUiKit.Label(band, tab.EmptyText ?? string.Empty, 0.42f, 0.58f,
                    ElarionUi.ParchmentDim, ElarionUi.FontBody, TextAlignmentOptions.Center);
                ElarionUiKit.FitSingleLine(empty, 24f, 40f);
                return;
            }

            // ⛔ THE CELL IS SQUARE AND THE CAPACITY IS AUTHORED. Do not go back to sizing the cell
            // from the band width and letting the row count fall out.
            // TWO measured defects came from that, both on 2026-09-06:
            //  (a) MANAGE_FLOW_INVENTORY BUILD: tiles=17 columns=4 visibleRows=0.8 - less than ONE
            //      row of seventeen tiles, under a chip that says ALL. ARMY: tiles=9 rows=3 but
            //      visibleRows=1.0 against content=590px in a 190px viewport, while the mockup's
            //      screen 4 says in words "All 9 troops visible, no scrolling".
            //  (b) A cell sized from the width was ~536x190 - nearly 3:1 - and the delivered frames
            //      are SQUARE 512px art drawn with preserveAspect, so the frame collapsed to a
            //      cellH square in the middle of a wide cell and ran straight through the name.
            // A square cell fixes the second outright and makes the first arithmetic instead of luck.
            int columns = tab.GridColumns > 0 ? tab.GridColumns : 3;
            int rows = tab.GridRows > 0 ? tab.GridRows : 3;
            float bandW = band.rect.width > 1f ? band.rect.width : 1080f;
            float bandH = band.rect.height > 1f ? band.rect.height : LastGridPx;

            // ⛔ THE GRID FILLS THE BAND. THE CELL IS NOT SQUARE. Do not "fix" this back to a square.
            // MEASURED in Builds/ui-capture/ManageFlow_ARMY_gridtop_2670x1200.png: a square cell
            // sized by the band HEIGHT gave 3 x 134px = 422px of grid inside an ~1800px band - the
            // tiles huddled in a narrow strip with the panel black on both sides, occupying about
            // 22% of the width. Every element was present and correctly ordered and it still read as
            // a different screen from the mockup, because in the mockup the grid spans the content
            // edge to edge.
            // The geometry is forced and there is no third option: the mockup's panel content is
            // roughly 2:1 and this modal's band is roughly 8:1, so square tiles CANNOT also fill the
            // width. Filling the width is what the picture shows, so the cell is WIDE:
            //   width  = the band divided by the AUTHORED columns (5 / 3 / 4)
            //   height = the band divided by the AUTHORED rows    (2 / 3 / 1)
            // The ART stays square regardless - the portrait and its frame are painted preserveAspect
            // inside TilePort*, which is why a wide cell is safe here and was not before WO-1443
            // moved the frame off the full-cell rect.
            // A wider cell also un-truncates the names ("Siege Catap...", "Echo Legio..."), which was
            // a second symptom of the same cause and not a font problem.
            float cellW = (bandW - (columns - 1) * TileGapPx) / columns;
            float cellH = (bandH - (rows - 1) * TileGapPx) / rows;
            cellH = Mathf.Min(cellH, MaxTileHeightPx);
            float cell = cellH;   // the vertical governor: text bands and the touch floor read this
            if (cellW < 1f || cellH < 1f)
            {
                FlowTrace.Warn("Manage", "grid resolved to a " + cell.ToString("0") + "px cell for " +
                    columns + "x" + rows + " in a " + bandW.ToString("0") + "x" + bandH.ToString("0") +
                    "px band - the model's capacity request cannot be honoured and the tiles would be " +
                    "sub-pixel. Reporting rather than re-columning silently");
                return;
            }
            if (cell < MinTileHeightPx)
                FlowTrace.Warn("Manage", "grid cell is " + cell.ToString("0") + "px, under the " +
                    MinTileHeightPx + "px floor, because the band is " + bandH.ToString("0") +
                    "px and the mockup asks for " + rows + " rows (it needs " +
                    (rows * MinTileHeightPx + (rows - 1) * TileGapPx).ToString("0") + "px). The " +
                    "TILE is the screen; the well has to grow, and nothing here will shrink text to " +
                    "hide that");

            // ⛔ WHOLE ROWS ONLY, AND THE REMAINDER SAYS HOW MANY ARE HIDDEN.
            // MEASURED in Builds/ui-capture/ManageFlow_Troops_railtop_2670x1200.png (2026-09-06,
            // 14:59): the grid took the whole band, so ARMY's second row was sliced through the
            // middle - three portraits visible from the top down, their NAMES cut off entirely.
            // A half tile is not a scroll hint; it is a tile whose label has been deleted, which is
            // the same class of defect as a text band under the cull floor. And the same frame
            // shows BUILD offering FOUR tiles under a chip that says ALL, with ~24 authored - a
            // claim the screen does not honour, and the exact seam-oracle defect WO-1430 catalogued.
            //
            // So: the viewport is trimmed to a whole number of rows, and the reclaimed strip
            // carries an explicit count of what is off-screen. The grid still scrolls - this adds
            // the WORDS that were missing, it does not replace the gesture. Nothing is shrunk:
            // the strip is only drawn when it clears MinTextBandPx on its own.
            int wholeRows = Mathf.Max(1, Mathf.FloorToInt((bandH + TileGapPx) / (cell + TileGapPx)));
            float rowsPx = wholeRows * cell + (wholeRows - 1) * TileGapPx;
            float gridW = columns * cellW + (columns - 1) * TileGapPx;
            int shown = wholeRows * columns;
            int hidden = tiles.Count - shown;
            float leftoverPx = bandH - rowsPx - TileGapPx;
            bool overflowStrip = hidden > 0 && leftoverPx >= MinTextBandPx && bandH > 1f;
            float viewportPx = overflowStrip ? rowsPx : bandH;

            FlowTrace.Step("Manage", "MANAGE_GRID tiles=" + tiles.Count + " want=" + columns + "x" + rows +
                " cell=" + cell.ToString("0") + "px band=" + bandW.ToString("0") + "x" +
                bandH.ToString("0") + " rowsFit=" + wholeRows + " shown=" + shown +
                " hidden=" + hidden + " gridW=" + gridW.ToString("0"));
            // ⚠ TWO DIFFERENT THINGS, AND THE OLD MESSAGE CONFLATED THEM.
            // It called ANY off-screen tile a "WELL SHORTFALL", which made the BUILD screen report
            // a failure while it was doing exactly what the mockup asks: panel 2 draws TEN tiles,
            // the catalog holds 22, and the other 12 scroll. That is a scrolling grid, not a defect.
            // A shortfall is when the well cannot seat the AUTHORED capacity - shown < columns*rows.
            if (shown < columns * rows)
                FlowTrace.Warn("Manage", "grid seats only " + shown + " tiles where the mockup asks for " +
                    (columns * rows) + " (" + columns + "x" + rows + ") - a WELL SHORTFALL, not a " +
                    "layout preference. Band " + bandW.ToString("0") + "x" + bandH.ToString("0") + "px");
            else if (hidden > 0)
                FlowTrace.Step("Manage", "grid shows the authored " + shown + " of " + tiles.Count +
                    " tiles; " + hidden + " scroll" +
                    (overflowStrip ? " and the count is on screen in the overflow strip"
                                   : " (no room for the overflow strip)"));

            var scrollGo = new GameObject("ManageGridScroll", typeof(RectTransform), typeof(Image),
                typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(band, false);
            Track(scrollGo);
            // FULL BAND WIDTH, top-anchored, sized to WHOLE rows. The cell width is the band divided
            // by the authored columns, so the grid meets both edges by construction - there is no
            // centring step and no leftover margin to explain.
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 1f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 1f);
            scrollRt.offsetMin = new Vector2(0f, 0f);
            scrollRt.offsetMax = new Vector2(0f, 0f);
            scrollRt.sizeDelta = new Vector2(0f, viewportPx);
            scrollRt.anchoredPosition = Vector2.zero;
            var backing = scrollGo.GetComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.001f);   // raycast surface for the drag, visually inert

            var contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(0f, 0f);
            contentRt.offsetMax = new Vector2(0f, 0f);

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(TileGapPx, TileGapPx);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperLeft;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            for (int i = 0; i < tiles.Count; i++) BuildTile(contentRt, tiles[i], cellH);

            // The honest overflow line. It exists ONLY while the well is too short to seat the
            // capacity the mockup asks for; once the well grows it never renders, which is the
            // point - a filter that says ALL must either show all or say what it is holding back.
            if (overflowStrip)
            {
                float y1 = Mathf.Clamp01((leftoverPx - TileGapPx) / bandH);
                var more = ElarionUiKit.Label(band, hidden + " MORE - SCROLL", 0f, y1,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                    0.05f, 0.95f);
                ElarionUiKit.FitSingleLine(more, 20f, 28f);
            }
        }

        /// <summary>
        /// One tile. The layer stack is fixed - plate, state frame, selection glow, PORTRAIT,
        /// selection bar, medallion - so a FRAME SWAP IS A SPRITE SWAP even though the delivered
        /// frames are not a consistent set (see ManageArt's header: two opaque centres, two
        /// hollow, and frame-selected's glow bleeds outside its rect). The plate is always
        /// painted so the two hollow frames have something behind them; frame-selected lives on
        /// its OWN, LARGER rect so its bleed does not have to 9-slice against the others.
        ///
        /// <para>⛔ THE FRAMES ARE PAINTED **UNDER** THE PORTRAIT, AND THAT ORDER IS THE FIX FOR
        /// WO-1443 section 2 - DO NOT PUT THEM BACK ON TOP. Until 2026-09-06 the frame was a LAYER-3
        /// overlay above the portrait, which is correct only if every frame's centre is hollow.
        /// MEASURED that day from the delivered PNGs in Assets/Resources/RpgUi/manage/ (System.
        /// Drawing pixel sample, not read off a comment):
        ///   frame-tile      centre alpha 253/255, and 253 across the whole portrait zone up to
        ///                   v=0.75 (transparent only above v~0.95)
        ///   frame-selected  centre alpha 253/255
        ///   frame-locked    centre alpha 0
        ///   frame-max       centre alpha 0
        /// So the two OPAQUE members painted a near-black plate over the portrait and the tile read
        /// as an EMPTY FRAME. That is exactly what the owner captured: Footman and Archer
        /// (unlockBarracksTier 1 -> unlocked -> FrameTile -> covered) rendered blank while Spearman
        /// (tier 2 -> Locked -> FrameLocked, hollow) showed its art. Nothing was missing and nothing
        /// failed to load - which is why ManageArt logged no art-miss line for any troop.
        /// The same defect hit every OWNED building tile on the BUILD tab; one order fixes both.</para>
        /// </summary>
        private void BuildTile(RectTransform parent, ManageTileVM tile, float cellH)
        {
            if (tile == null) return;

            var cellGo = new GameObject("ManageTile", typeof(RectTransform), typeof(Image), typeof(Button));
            cellGo.transform.SetParent(parent, false);
            var cell = cellGo.GetComponent<RectTransform>();

            // LAYER 1 - the plate. Always present, opaque enough to back a hollow frame.
            var plate = cellGo.GetComponent<Image>();
            plate.color = new Color(0.05f, 0.04f, 0.03f, 0.72f);

            // The whole cell is the tap target: cellH >= MinTileHeightPx(150) >= MinTouchPx(112),
            // margin +38px at the floor. Authored, never clamped.
            var press = cellGo.GetComponent<Button>();
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(MakeUnityInvoker(tile.Activate));

            // LAYER 2 - the state frame, preserve-aspect. NOT sliced (ManageArt header), and NOT
            // above the portrait: two of the four delivered frames have an opaque centre, measured
            // (see this method's summary). Under the portrait an opaque frame reads as the tile's
            // backing art and a hollow one still shows its ring, so ONE order is right for all four.
            //
            // ⛔ AND IT IS PAINTED OVER THE **PORTRAIT ZONE**, NOT Vector2.zero..one. THAT IS THE
            // FIX FOR THE NAMES SITTING ON THE FRAME - do not widen it back to the cell.
            // MEASURED in Builds/ui-capture/ManageFlow_Troops_railtop and _Buildings_railtop
            // (2026-09-06, 14:59): "Footman" / "Archer" / "Spearman" and "Archer Tower" / "Ballista"
            // rendered ON the frame's lower border instead of below it, with the state word cramped
            // under them. The cause is geometry, not the text bands. The frames are SQUARE 512px
            // art drawn with preserveAspect, and a grid cell here is about 536x190 - so a
            // full-cell frame collapses to a cellH-sided square (190px) CENTRED IN THE CELL, i.e.
            // spanning y 0..1 and straight through the title band (0.26-0.48) and the state band
            // (0.05-0.25). It was never able to act as a tile border on a wide cell; it is a
            // portrait medallion frame, and this is the rect it was drawn for. The tile's own
            // border is LAYER 1's plate, which does fill the cell.
            PaintSprite(cell, "TileFrame", new Vector2(TilePortX0, TilePortY0),
                new Vector2(TilePortX1, TilePortY1), tile.FrameKey);

            // LAYER 3 - selection GLOW, also under the portrait (frame-selected's centre is opaque
            // at alpha 253, so on top it blanked the portrait of whichever tile was selected).
            if (tile.IsSelected)
            {
                // frame-selected's glow bleeds OUTSIDE its rect, so it gets a rect grown past the
                // portrait zone on every side. Overlaying it on the same rect as the others clips
                // the glow and makes the two frame families disagree at the edge. It is grown in
                // CELL-normalised space, which is why x and y use different amounts: the cell is
                // roughly 2.8x wider than tall, so an equal normalised inset would not be an equal
                // number of pixels.
                PaintSprite(cell, "TileSelectedGlow",
                    new Vector2(TilePortX0 - 0.02f, TilePortY0 - 0.03f),
                    new Vector2(TilePortX1 + 0.02f, Mathf.Min(1f, TilePortY1 + 0.03f)),
                    ManageArt.FrameSelected);
            }

            // LAYER 4 - the portrait, ABOVE every frame, on the SAME rect as the frame so the two
            // are concentric and neither can reach the title band below them.
            var portZone = Zone(cell, "TilePortrait",
                new Vector2(TilePortX0, TilePortY0), new Vector2(TilePortX1, TilePortY1));
            ElarionUiKit.Portrait(portZone, ManageArt.LoadSprite(tile.PortraitKey), active: false);

            // LAYER 5 - SELECTION IS A GOLD BORDER AROUND THE WHOLE TILE.
            // CAPTURE_LOOP_GOAL 3.0b: "Selected tile carries a gold border", and the mockup draws it
            // that way on screens 2, 4 and 6. It replaces the old left-edge bar, which was a shape
            // cue invented before the picture existed. A border is still SHAPE, not hue alone: it
            // changes the tile's silhouette and reads in greyscale.
            if (tile.IsSelected) ElarionUiKit.GoldPerimeter(cell);

            // LAYER 6 - the status medallion (canon 8: mandatory, model-supplied). This is the
            // channel that carries state on a grid tile now that the state TEXT band is gone; the
            // words themselves live on the detail screen, which is where the mockup puts them.
            PaintSprite(cell, "TileStatus", new Vector2(TileMedX0, TileMedY0),
                new Vector2(TileMedX1, TileMedY1), tile.StateIconKey);

            // THE NAME STRIP - one band, and the only text on the tile. A dark plate behind it so
            // the name reads against whatever the art happens to be, exactly as the mockup draws.
            var namePlate = ElarionUiKit.AddImage(cell, "TileNamePlate",
                new Vector2(0f, TileTitleY0 - 0.02f), new Vector2(1f, TileTitleY1 + 0.02f),
                new Color(0.03f, 0.03f, 0.03f, 0.86f));
            var namePlateImage = namePlate != null ? namePlate.GetComponent<Image>() : null;
            if (namePlateImage != null) namePlateImage.raycastTarget = false;

            var title = ElarionUiKit.Label(cell, tile.Title ?? string.Empty, TileTitleY0, TileTitleY1,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: tile.IsSelected);
            ElarionUiKit.FitSingleLine(title, 20f, 30f);      // 0.24 band = 29px at the 120px floor

            // A BAR, not text - exempt from the cull floor because nothing is typeset in it.
            if (tile.Progress01.HasValue) ProgressBar(cell, tile.Progress01.Value,
                TileProgY0, TileProgY1, 0.05f, 0.95f);

            if (cellH < MinTileHeightPx)
                FlowTrace.Warn("Manage", "tile cell resolved to " + cellH.ToString("0") + "px, under the " +
                    MinTileHeightPx + "px floor - its name band falls under the " + MinTextBandPx +
                    "px TMP cull threshold and would render BLANK");
        }

        // =====================================================================
        //  ACTIVITY STRIP
        // =====================================================================

        private void BuildActivity(RectTransform band, ManageActivityVM activity)
        {
            var plate = ElarionUiKit.AddImage(band, "ActivityPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.62f));
            var plateImage = plate.GetComponent<Image>();
            if (plateImage != null) plateImage.raycastTarget = false;

            PaintSprite(band, "ActivityIcon", new Vector2(0.02f, 0.12f), new Vector2(0.12f, 0.88f),
                activity.IconKey);

            // ⚠ The two label bands are inset INSIDE the plate, not flush to it. The capture
            // auditor measured ManageActivity/Label overflowing its backing by 3.7px at the old
            // 0.94 / 0.08 edges, which is a text band spilling past the thing that is supposed to
            // contain it. 0.90 / 0.10 keeps both inside at ActivityBandPx=120 with room to spare,
            // and neither band drops under the 28px cull floor: 0.52-0.90 = 45.6px, 0.10-0.48 = 45.6px.
            var title = ElarionUiKit.Label(band, activity.Title ?? string.Empty, 0.52f, 0.90f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.14f, 0.62f, bold: true);
            ElarionUiKit.FitSingleLine(title, 22f, 32f);      // band 46px

            var timer = ElarionUiKit.Label(band, Join(activity.TimerText, activity.QueuedCountText),
                0.10f, 0.48f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Left, 0.14f, 0.62f);
            ElarionUiKit.FitSingleLine(timer, 20f, 30f);      // band 46px

            // ⛔ THE STRIP CARRIES NO QUEUE BUTTON. Do not add one back.
            // MEASURED in Builds/ui-capture/ManageFlow_Troops_railtop_2670x1200.png (2026-09-06,
            // 14:59): TWO queue affordances were on screen at once - the tab-row door and a second
            // "QUEUE" face here, bottom right. I had reported "exactly one affordance on screen",
            // and that claim was WRONG: I had counted the retired ManageScreenPanel header toggle
            // and missed this one, because it is built from a hardcoded literal in the view rather
            // than from the queue model. The capture is the evidence; the claim was not.
            // Canon's rule is ONE queue entry (CLAUDE.md 7, ruling Q10+Q13), and the owner's ruling
            // named which one: the tab-row door. So this face stands down and the strip returns to
            // what its name says it is - a STATUS GLANCE (what is running, how long, how many
            // queued). Every one of those words is still painted above.
        }

        // =====================================================================
        //  SELECTION CARD
        // =====================================================================

        private void BuildSelection(RectTransform band, ManageTabVM tab, float bandPx)
        {
            var plate = ElarionUiKit.AddImage(band, "SelectionPlate", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.04f, 0.03f, 0.78f));
            var plateImage = plate.GetComponent<Image>();
            if (plateImage != null) plateImage.raycastTarget = false;
            ElarionUiKit.GoldPerimeter(band);

            // ⚠ WO-1443 section 3: the caller now COLLAPSES this band rather than reserving it, so
            // this arm is a guard, not a layout state. It no longer paints EmptyText - painting a
            // hint sentence in a reserved bordered box is precisely the defect the owner captured -
            // and it SAYS SO rather than drawing an empty plate in silence.
            ManageSelectionVM sel = tab != null ? tab.Selection : null;
            if (sel == null || !sel.Visible)
            {
                FlowTrace.Warn("Manage", "BuildSelection reached with no visible selection - Build() " +
                    "should have collapsed the band. An empty bordered plate is on screen taking " +
                    band.rect.height.ToString("0") + "px.");
                return;
            }

            // The CTA faces are resolved FIRST because whether any of them is refused decides
            // whether the "why" band is needed, and that band is part of the fixed floor.
            var faces = VisibleFaces(sel);
            bool needWhy = false;
            for (int i = 0; i < faces.Count; i++)
                if (!faces[i].Enabled && !string.IsNullOrEmpty(faces[i].DisabledReasonText)) needWhy = true;

            // ⭐ TWO COLUMNS: BIG ART LEFT, EVERYTHING ELSE RIGHT. This is the owner's mockup -
            // panel 3 (LUMBER MILL), panel 5 (ARCHER) and panel 9 (OUTRIDER, locked) are all the
            // same shape: "Big art LEFT; right = name, level, one-line purpose, a before -> after
            // stats table, cost with icons, time, one gold button" (CAPTURE_LOOP_GOAL 3.0).
            //
            // ⛔ THE RIGHT COLUMN FLOWS TOP-DOWN IN PIXELS AND ONLY SEATS WHAT EXISTS.
            // The first draft placed every band at a FIXED FRACTION of the card. On a LOCKED item -
            // which has no stats and no costs - that left two empty fractions in the middle and
            // stranded the CTA at the bottom of a two-thirds-empty column, which is what
            // ManageFlow_ARMY_locked showed. A fixed fraction assumes every screen has the same
            // content; these screens do not.
            // ⛔ AND THE CTA IS AUTHORED AT THE TOUCH FLOOR, IN PX, NOT AS A FRACTION.
            // MEASURED by the capture auditor: 'ObsBtn_VIEW BARRACKS' resolved 626.3x104.1 ref px -
            // 7.9px UNDER MinTouchPx(112) - and the auditor's own warning says why that matters:
            // "ClampMinTouch will grow it SYMMETRICALLY about its centre and spill it into both
            // neighbours. Author the band AT the floor." A fraction of a card whose height varies
            // cannot promise a px floor, so the band is now taken in px and the rest of the column
            // flows above it.
            float cardH = bandPx > 1f ? bandPx : (band.rect.height > 1f ? band.rect.height : 1f);
            float cardW = band.rect.width > 1f ? band.rect.width : 1f;

            // ⚠ THE ART ZONE IS SQUARE. The delivered troop art is square (1254x1254) and is
            // painted preserveAspect, so a NON-square zone leaves a transparent margin inside the
            // portrait rect - and the delivered PNGs carry a TRANSPARENCY CHECKERBOARD BAKED INTO
            // THEIR RGB (measured 2026-09-06: troop-outrider corner pixels read alpha 0 with RGB
            // varying 252/253/250/247/248, which is a checker pattern left in the colour channels
            // when the alpha was zeroed). A square zone is both the mockup's shape and the smallest
            // transparent margin, so it is the safer of the two either way.
            float artFrac = Mathf.Min(0.38f, (cardH * 0.92f) / Mathf.Max(1f, cardW));
            var portrait = Zone(band, "SelPortrait",
                new Vector2(0.02f, 0.04f), new Vector2(0.02f + artFrac, 0.96f));
            ElarionUiKit.Portrait(portrait, ManageArt.LoadSprite(sel.PortraitKey), active: false);

            const float RightX1 = 0.98f;
            float rightX0 = Mathf.Min(0.60f, 0.02f + artFrac + 0.04f);

            // ---- the column FLOWS: level, description, stats, costs, why, then the CTA ----
            // ⛔ THE CTA IS NOT PINNED TO THE BOTTOM. It was, and the capture showed the result:
            // on a LOCKED item - description and one requirement sentence, no stats, no costs - the
            // right column was two-thirds empty with the button stranded at the floor. The mockup's
            // panels are vertically COMPACT and sit high; panel 9's content is four short things in
            // the top half. So the CTA flows with everything else and the column simply ends.
            // ⛔ BUT ITS HEIGHT IS STILL TAKEN IN PX, NOT AS A FRACTION. The auditor caught
            // 'ObsBtn_VIEW BARRACKS' at 626.3x104.1 ref px - 7.9px under MinTouchPx(112) - because a
            // fraction of a card whose height varies cannot promise a px floor. Its own words:
            // "ClampMinTouch will grow it SYMMETRICALLY about its centre and spill it into both
            // neighbours. Author the band AT the floor."
            float ctaPx = Mathf.Max(ElarionUiKit.MinTouchPx + 8f, cardH * 0.16f);
            if (ctaPx > cardH * 0.42f) ctaPx = cardH * 0.42f;

            float cursorY = 0.96f;                               // fraction, top-down
            float gapF = 12f / cardH;

            // LEVEL + STATE on one line. ⛔ NOT a gold heading of its own: the capture showed a
            // locked troop headed "LOCKED" in gold where the mockup has no heading at all. The state
            // word rides WITH the level, and the requirement sentence below carries the meaning.
            string levelText = Join(sel.LevelText, sel.StateText);
            if (!string.IsNullOrEmpty(levelText))
            {
                float h = 44f / cardH;
                var levelLine = ElarionUiKit.Label(band, levelText, cursorY - h, cursorY,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Left, rightX0, RightX1);
                ElarionUiKit.FitSingleLine(levelLine, 22f, 32f);
                cursorY -= h + gapF;
            }

            string descText = Join(sel.Description, sel.AuxiliaryText);
            if (!string.IsNullOrEmpty(descText))
            {
                float h = 92f / cardH;
                var desc = ElarionUiKit.Label(band, descText, cursorY - h, cursorY,
                    ElarionUi.Parchment, ElarionUi.FontLabel,
                    TextAlignmentOptions.TopLeft, rightX0, RightX1);
                desc.enableAutoSizing = false;
                desc.fontSize = 26f;
                desc.overflowMode = TextOverflowModes.Ellipsis;
                cursorY -= h + gapF;
            }

            // THE STATS TABLE - one ROW PER STAT. The mockup draws
            // "Production   120 / hour  ->  180 / hour" as a table, and that before -> after is what
            // the upgrade BUYS. A locked item has none, and then it takes no room at all.
            if (sel.Stats != null && sel.Stats.Count > 0)
            {
                float h = Mathf.Min(sel.Stats.Count, 5) * 40f / cardH;
                BuildStatRows(band, sel.Stats, rightX0, RightX1, cursorY - h, cursorY);
                cursorY -= h + gapF;
            }

            if (sel.Costs != null && sel.Costs.Count > 0)
            {
                float h = 58f / cardH;
                BuildCostRow(band, sel.Costs, rightX0, RightX1, cursorY - h, cursorY);
                cursorY -= h + gapF;
            }

            if (needWhy)
            {
                // Canon 11 question 6: if I cannot act, WHY - panel 9's "Requires Barracks Tier 4".
                // ⛔ THE PADLOCK IS PAINTED INLINE, TO THE LEFT OF THE SENTENCE, AND THE TEXT
                // STARTS AFTER IT. The previous round hung it off the column's left edge at a
                // negative offset and it VANISHED from the capture - a rect outside its column is a
                // rect nobody sees, which is the same mistake as the badge that ended up outside the
                // panel. It matters more here than anywhere else on the screen: the owner is
                // red/green colourblind, so the padlock is the SHAPE channel for "locked" and the
                // dim word alone is not enough (CAPTURE_LOOP_GOAL 3c).
                float h = 46f / cardH;
                float lockW = 0.045f;
                PaintSprite(band, "SelWhyLock",
                    new Vector2(rightX0, cursorY - h), new Vector2(rightX0 + lockW, cursorY),
                    sel.StateIconKey);
                var why = ElarionUiKit.Label(band, WhyLine(faces), cursorY - h, cursorY,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Left,
                    rightX0 + lockW + 0.012f, RightX1);
                ElarionUiKit.FitSingleLine(why, 18f, 26f);
                cursorY -= h + gapF;
            }

            if (sel.Progress.HasValue)
            {
                float h = 14f / cardH;
                ProgressBar(band, sel.Progress.Value, cursorY - h, cursorY, rightX0, RightX1);
                cursorY -= h + gapF;
            }

            // ONE GOLD CTA, directly under the content rather than at the floor of the card.
            float ctaH = ctaPx / cardH;
            float ctaY0 = Mathf.Max(0.02f, cursorY - ctaH);
            var actionBand = Zone(band, "SelActions",
                new Vector2(rightX0, ctaY0), new Vector2(RightX1, ctaY0 + ctaH));
            BuildActionRow(actionBand, faces);
            if (ctaPx < ElarionUiKit.MinTouchPx)
                FlowTrace.Warn("Manage", "detail CTA band is " + ctaPx.ToString("0") + "px against the " +
                    ElarionUiKit.MinTouchPx + "px touch floor in a " + cardH.ToString("0") +
                    "px card - the well is too short for one authored button");
            FlowTrace.Step("Manage", "MANAGE_DETAIL card=" + cardH.ToString("0") + "x" +
                cardW.ToString("0") + "px art=" + artFrac.ToString("0.##") + " cta=" +
                ctaPx.ToString("0") + "px stats=" + (sel.Stats != null ? sel.Stats.Count : 0) +
                " costs=" + (sel.Costs != null ? sel.Costs.Count : 0) +
                " faces=" + faces.Count + " why=" + needWhy);
        }

        /// <summary>
        /// The before -> after stats table, one ROW PER STAT (mockup panel 3's
        /// "Production 120 / hour -> 180 / hour"). Rows lay top-down inside the supplied fraction
        /// band and are DROPPED, never shrunk, when the band cannot seat another at the
        /// <see cref="MinTextBandPx"/> cull floor - an omitted row is visibly missing, a culled one
        /// is invisible, and invisible is the trap.
        /// <para>⚠ The delta is carried by the model's own words plus an ASCII arrow, never by
        /// colour. The mockup prints the new value in green; the owner is red/green colourblind, so
        /// the ARROW and the VALUE are the channel and Gold is only emphasis.</para>
        /// </summary>
        private void BuildStatRows(RectTransform band, IReadOnlyList<ManageStatVM> stats,
            float x0, float x1, float y0, float y1)
        {
            if (stats == null || stats.Count == 0) return;
            float bandH = band.rect.height > 1f ? band.rect.height : 1f;
            float spanPx = (y1 - y0) * bandH;
            int seats = Mathf.FloorToInt(spanPx / MinTextBandPx);
            if (seats < 1)
            {
                FlowTrace.Warn("Manage", "the detail card's stats band is " + spanPx.ToString("0") +
                    "px - it cannot seat one row at the " + MinTextBandPx + "px cull floor, so the " +
                    "whole table is omitted rather than rendered blank");
                return;
            }
            int rows = Mathf.Min(seats, stats.Count);
            int hidden = stats.Count - rows;
            if (hidden > 0)
                FlowTrace.Warn("Manage", "detail card shows " + rows + " of " + stats.Count +
                    " stat rows - the band is " + spanPx.ToString("0") + "px");

            float rowH = (y1 - y0) / rows;
            float mid = x0 + (x1 - x0) * 0.42f;
            for (int i = 0; i < rows; i++)
            {
                var s = stats[i];
                if (s == null) continue;
                float rowBottom = y1 - (i + 1) * rowH;
                float inner = rowH * 0.10f;

                var label = ElarionUiKit.Label(band, s.Label ?? string.Empty,
                    rowBottom + inner, rowBottom + rowH - inner,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Left, x0, mid);
                ElarionUiKit.FitSingleLine(label, 18f, 26f);

                string valueLine = string.IsNullOrEmpty(s.DeltaText)
                    ? (s.Value ?? string.Empty)
                    : (s.Value ?? string.Empty) + "  ->  " + s.DeltaText;
                var value = ElarionUiKit.Label(band, valueLine,
                    rowBottom + inner, rowBottom + rowH - inner,
                    string.IsNullOrEmpty(s.DeltaText) ? ElarionUi.Parchment : ElarionUi.Gold,
                    ElarionUi.FontLabel, TextAlignmentOptions.Right, mid, x1);
                ElarionUiKit.FitSingleLine(value, 18f, 26f);
            }
        }

        /// <summary>
        /// The cost basket as ICONS + AMOUNTS across one row, which is how the mockup draws it: a
        /// wood glyph and 1,200, an iron glyph and 600, a clock and 45m.
        /// <para><c>Affordable</c> is a MODEL verdict. An unaffordable line is dimmed AND its
        /// refusal sentence is already on the why band - never colour alone.</para>
        /// </summary>
        private void BuildCostRow(RectTransform band, IReadOnlyList<ManageCostVM> costs,
            float x0, float x1, float y0, float y1)
        {
            if (costs == null || costs.Count == 0) return;
            int n = costs.Count;
            float w = (x1 - x0) / n;
            float pad = (y1 - y0) * 0.15f;
            for (int i = 0; i < n; i++)
            {
                var c = costs[i];
                if (c == null) continue;
                float cx0 = x0 + i * w;
                PaintSprite(band, "SelCostIcon" + i,
                    new Vector2(cx0, y0 + pad), new Vector2(cx0 + w * 0.28f, y1 - pad), c.IconKey);
                var amount = ElarionUiKit.Label(band, c.AmountText ?? string.Empty, y0, y1,
                    c.Affordable ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                    ElarionUi.FontLabel, TextAlignmentOptions.Left, cx0 + w * 0.30f, cx0 + w * 0.98f);
                ElarionUiKit.FitSingleLine(amount, 20f, 30f);
            }
        }

        /// <summary>The visible CTA faces, in reading order: the door, the secondary, the primary.</summary>
        private static List<ManageActionVM> VisibleFaces(ManageSelectionVM sel)
        {
            var faces = new List<ManageActionVM>();
            if (sel.RequirementAction != null && sel.RequirementAction.Visible) faces.Add(sel.RequirementAction);
            if (sel.SecondaryAction != null && sel.SecondaryAction.Visible) faces.Add(sel.SecondaryAction);
            if (sel.PrimaryAction != null && sel.PrimaryAction.Visible) faces.Add(sel.PrimaryAction);
            return faces;
        }

        /// <summary>The model's refusal sentences, joined. Joined, never composed.</summary>
        private static string WhyLine(List<ManageActionVM> faces)
        {
            string line = null;
            for (int i = 0; i < faces.Count; i++)
            {
                var f = faces[i];
                if (f.Enabled || string.IsNullOrEmpty(f.DisabledReasonText)) continue;
                line = Join(line, f.DisabledReasonText);
            }
            return line ?? string.Empty;
        }

        /// <summary>
        /// The CTA row. Up to three slots - requirement (ruling 18's door), secondary, primary -
        /// laid by even split so the primary is always the rightmost face.
        /// A 0.04-0.96 vertical inset of a 120px band gives 110px, UNDER MinTouchPx(112);
        /// the inset is therefore 0.02-0.98 => 115px, a measured margin of +3px. Authored, not
        /// clamped (ElarionUiKit.cs:1100).
        /// </summary>
        private void BuildActionRow(RectTransform band, List<ManageActionVM> faces)
        {
            if (faces.Count == 0) return;

            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                var btn = ElarionUiKit.Button(band, Join(face.Label, face.CostText),
                    KindFor(face.StyleRole),
                    new Vector2(SlotStart(i, faces.Count), 0.02f),
                    new Vector2(SlotEnd(i, faces.Count), 0.98f),
                    MakeInvoker(face.Activate));
                Track(btn);

                // Explicit field, never inferred from the callback (canon 9 / ManageStateModel's
                // "Invoke being null is an implementation detail, NOT a state").
                btn.interactable = face.Enabled;
            }
        }

        // =====================================================================
        //  PRIMITIVES
        // =====================================================================

        /// <summary>A horizontal band pinned <paramref name="topPx"/> below the parent's top edge.</summary>
        private RectTransform BandFromTop(RectTransform parent, string name, float topPx, float heightPx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Track(go);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(topPx + heightPx));
            rt.offsetMax = new Vector2(0f, -topPx);
            return rt;
        }

        private RectTransform Zone(RectTransform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// Paints a supplied asset KEY into a preserve-aspect Image. A key that does not resolve
        /// renders fully transparent (ManageArt has already announced the miss once) rather than
        /// as a white box - a white box reads as art, and art that is wrong is worse than absent.
        /// </summary>
        private void PaintSprite(RectTransform parent, string name, Vector2 min, Vector2 max, string key)
        {
            var go = ElarionUiKit.AddImage(parent, name, min, max, Color.white, rounded: false);
            var img = go != null ? go.GetComponent<Image>() : null;
            if (img == null) return;
            img.sprite = ManageArt.LoadSprite(key);
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (img.sprite == null) img.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>A two-part fill bar. Graphic only - nothing is typeset, so no cull floor applies.</summary>
        private void ProgressBar(RectTransform parent, float fill01, float y0, float y1, float x0, float x1)
        {
            float clamped = Mathf.Clamp01(fill01);
            var track = ElarionUiKit.AddImage(parent, "ProgressTrack",
                new Vector2(x0, y0), new Vector2(x1, y1), new Color(0f, 0f, 0f, 0.55f));
            var trackImage = track.GetComponent<Image>();
            if (trackImage != null) trackImage.raycastTarget = false;
            var fill = ElarionUiKit.AddImage(track.transform, "ProgressFill",
                Vector2.zero, new Vector2(clamped, 1f), ElarionUi.Gold);
            var fillImage = fill.GetComponent<Image>();
            if (fillImage != null) fillImage.raycastTarget = false;
        }

        /// <summary>The active-chip SHAPE cue: a solid underline, legible in greyscale.</summary>
        private void Underline(Transform host)
        {
            var bar = ElarionUiKit.AddImage(host, "ActiveUnderline",
                new Vector2(0.10f, 0.00f), new Vector2(0.90f, 0.06f), ElarionUi.Gold);
            var img = bar.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }

        private static ElarionUiKit.ButtonKind KindFor(ManageActionStyleRole role)
        {
            switch (role)
            {
                case ManageActionStyleRole.Destructive: return ElarionUiKit.ButtonKind.Danger;
                case ManageActionStyleRole.Navigate: return ElarionUiKit.ButtonKind.Confirm;
                case ManageActionStyleRole.Secondary: return ElarionUiKit.ButtonKind.Quiet;
                default: return ElarionUiKit.ButtonKind.Gold;
            }
        }

        // Even split with a gutter. Shared by tabs, filters and the CTA row so three bands
        // cannot drift into three different slot arithmetics.
        private static float SlotStart(int index, int count) =>
            0.02f + index * (0.96f / Mathf.Max(1, count)) + 0.006f;

        private static float SlotEnd(int index, int count) =>
            0.02f + (index + 1) * (0.96f / Mathf.Max(1, count)) - 0.006f;

        /// <summary>
        /// Wraps a model callback for the kit's Action-shaped click hook. Always non-null, so the
        /// renderer never has to test a callback to decide whether a control exists - that
        /// decision belongs to ManageActionVM.Visible / Enabled.
        /// </summary>
        private static Action MakeInvoker(Action callback) => () => callback?.Invoke();

        private static UnityEngine.Events.UnityAction MakeUnityInvoker(Action callback) =>
            () => callback?.Invoke();

        /// <summary>Joins two model-supplied fragments with a separator. Joins; never invents.</summary>
        private static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            return a + "  .  " + b;
        }

        private void Track(GameObject go) { if (go != null) _spawned.Add(go); }
        private void Track(Component c) { if (c != null) _spawned.Add(c.gameObject); }
    }
}
