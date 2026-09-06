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
        private const float HeaderBandPx = 120f;   // title 56 + subtitle 40 stacked; queue door 112 tall
        private const float TabsBandPx = 120f;     // chips at 0.04-0.96 of the band = 115px >= MinTouchPx(112)
        private const float FiltersBandPx = 120f;  // same as tabs; 0 when the tab supplies no filters
        private const float ActivityBandPx = 120f; // strip is tappable (OpenQueue) so it is a touch band
        private const float BandGapPx = 12f;       // guaranteed gutter - no two bands ever touch

        // Selection card sub-bands, top-down inside the selection band.
        private const float SelTitlePx = 44f;      // >= 28 floor
        private const float SelLevelPx = 34f;      // >= 28 floor
        private const float SelDescPx = 44f;       // >= 28 floor (two short lines at 20pt)
        private const float SelStatsPx = 34f;      // >= 28 floor
        private const float SelCostPx = 34f;       // >= 28 floor
        private const float SelWhyPx = 34f;        // >= 28 floor - the "why can I not act" sentence
        private const float SelActionPx = 120f;    // CTA band; the button fills 0.02-0.98 = 115px
        private const float SelGapPx = 8f;
        // Full card 44+34+44+34+34+34+120 + 6*8 = 392px.
        // Floor (title + cost + why + action + 3 gaps) = 256px - the four bands canon 11 makes
        // non-negotiable: what is it, what does it cost, why can I not act, what can I do.
        private const float SelectionFullPx = 392f;
        private const float SelectionFloorPx = 256f;

        private const float MinGridPx = 162f;      // one MinTileHeightPx row plus a gap
        private const float MinTileHeightPx = 150f;// state 30 + title 33 + portrait 80 stack; >> MinTouchPx(112)
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
        private const float TileProgY0 = 0.005f, TileProgY1 = 0.045f; //  4.0% ->  6px /  8px (a BAR, no text)
        private const float TileStateY0 = 0.05f, TileStateY1 = 0.25f; // 20.0% -> 30px / 38px  >= 28
        private const float TileTitleY0 = 0.26f, TileTitleY1 = 0.48f; // 22.0% -> 33px / 42px  >= 28
        private const float TilePortY0 = 0.50f, TilePortY1 = 0.97f;   // 47.0% -> 70px / 89px
        private const float TilePortX0 = 0.16f, TilePortX1 = 0.84f;
        private const float TileMedX0 = 0.63f, TileMedX1 = 0.98f;     // status medallion, top-right
        private const float TileMedY0 = 0.60f, TileMedY1 = 0.95f;
        private const float TileSelBarX1 = 0.07f;                     // selection SHAPE cue: left-edge bar

        // ── Handles ───────────────────────────────────────────────────────────
        private readonly RectTransform _body;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>Last measured well height in reference px. Diagnostics only.</summary>
        public float LastWellPx { get; private set; }

        /// <summary>Last computed grid band height in reference px. Diagnostics only.</summary>
        public float LastGridPx { get; private set; }

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

            ManageTabVM tab = vm.ActiveTab;
            bool hasFilters = tab != null && tab.Filters != null && tab.Filters.Count > 0;
            bool hasActivity = tab != null && tab.Activity != null && tab.Activity.Visible;

            float fixedTop = HeaderBandPx + BandGapPx + TabsBandPx + BandGapPx
                           + (hasFilters ? FiltersBandPx + BandGapPx : 0f);
            float fixedBottom = (hasActivity ? ActivityBandPx + BandGapPx : 0f);

            // The selection card is the band that YIELDS when the well is short, because the
            // grid is the screen's reason to exist (canon 1: expose more, not less). It yields
            // by DROPPING sub-bands in a stated order - never by shrinking one under the cull
            // floor.
            float selection = SelectionFullPx;
            float grid = well - fixedTop - fixedBottom - selection - BandGapPx;
            if (grid < MinGridPx)
            {
                float shortfall = MinGridPx - grid;
                float reduced = Mathf.Max(SelectionFloorPx, selection - shortfall);
                FlowTrace.Warn("Manage", "well=" + well.ToString("0") + "px cannot seat the full band stack " +
                    "(fixed=" + (fixedTop + fixedBottom).ToString("0") + " selection=" + selection.ToString("0") +
                    " grid=" + grid.ToString("0") + "). Selection card trims to " + reduced.ToString("0") +
                    "px; sub-bands are dropped, never shrunk under the " + MinTextBandPx + "px TMP cull floor");
                selection = reduced;
                grid = well - fixedTop - fixedBottom - selection - BandGapPx;
            }
            if (grid < MinTileHeightPx)
            {
                FlowTrace.Warn("Manage", "grid band is " + grid.ToString("0") + "px - under one " +
                    MinTileHeightPx + "px tile row. The workspace needs a taller well than this host " +
                    "gives it (WO-2001); the grid is clamped and will scroll");
                grid = Mathf.Max(grid, MinTileHeightPx);
            }
            LastGridPx = grid;

            float cursor = 0f;
            BuildHeader(BandFromTop(_body, "ManageHeader", cursor, HeaderBandPx), vm);
            cursor += HeaderBandPx + BandGapPx;

            BuildTabs(BandFromTop(_body, "ManageTabs", cursor, TabsBandPx), vm);
            cursor += TabsBandPx + BandGapPx;

            if (hasFilters)
            {
                BuildFilters(BandFromTop(_body, "ManageFilters", cursor, FiltersBandPx), tab);
                cursor += FiltersBandPx + BandGapPx;
            }

            BuildGrid(BandFromTop(_body, "ManageGrid", cursor, grid), tab);
            cursor += grid + BandGapPx;

            if (hasActivity)
            {
                BuildActivity(BandFromTop(_body, "ManageActivity", cursor, ActivityBandPx), tab.Activity);
                cursor += ActivityBandPx + BandGapPx;
            }

            BuildSelection(BandFromTop(_body, "ManageSelection", cursor, selection), tab, selection);
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

        private void BuildHeader(RectTransform band, ManageWorkspaceVM vm)
        {
            // Title 56px + subtitle 40px inside a 120px band; both clear the 28px cull floor.
            var title = ElarionUiKit.Label(band, vm.HeaderTitle ?? string.Empty, 0.50f, 0.98f,
                ElarionUi.Gold, ElarionUi.FontHead, TextAlignmentOptions.Left, 0.02f, 0.64f, bold: true);
            ElarionUiKit.FitSingleLine(title, 30f, 52f);      // band 56px

            var sub = ElarionUiKit.Label(band, vm.HeaderSubtitle ?? string.Empty, 0.06f, 0.44f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.64f);
            ElarionUiKit.FitSingleLine(sub, 22f, 34f);        // band 46px

            BuildQueueDoor(band, vm.Queue);
        }

        /// <summary>
        /// The global QUEUE door (ruling 17). <c>AtCapacity</c> is a MODEL verdict; this paints
        /// a WORD for it and a filled bar beside it - never a colour on its own, because the
        /// owner is red/green colourblind.
        /// </summary>
        private void BuildQueueDoor(RectTransform band, ManageQueueVM queue)
        {
            if (queue == null || !queue.Visible) return;

            // 0.68-0.98 of the header band, inset 0.02-0.98 vertically: 0.96 x 120 = 115.2px,
            // margin +3.2px over MinTouchPx(112). A 0.03/0.97 inset would give 112.8px (+0.8),
            // which the kit Button's own padding could eat - so it uses the same 0.02/0.98 inset
            // as every other CTA on this surface. Authored, not left to ClampMinTouch.
            var door = ElarionUiKit.Button(band, queue.Label ?? string.Empty,
                ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.68f, 0.02f), new Vector2(0.98f, 0.98f),
                () => queue.Open?.Invoke());
            Track(door);

            // The capacity cue: a WORD supplied by the model plus a solid pip, so "full" is
            // legible in greyscale.
            if (queue.AtCapacity)
            {
                var pip = ElarionUiKit.AddImage(door.transform, "QueueFullPip",
                    new Vector2(0.03f, 0.06f), new Vector2(0.09f, 0.94f), ElarionUi.Gold);
                var pipImage = pip.GetComponent<Image>();
                if (pipImage != null) pipImage.raycastTarget = false;
            }

            string countLine = Join(queue.CountText, queue.CapacityText);
            if (!string.IsNullOrEmpty(countLine))
            {
                var count = ElarionUiKit.Label(band, countLine, 0.02f, 0.30f,
                    ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.66f, 0.99f);
                ElarionUiKit.FitSingleLine(count, 20f, 30f);   // band 34px >= 28
            }
        }

        private void BuildTabs(RectTransform band, ManageWorkspaceVM vm)
        {
            var tabs = vm.Tabs;
            if (tabs == null || tabs.Count == 0)
            {
                FlowTrace.Warn("Manage", "the workspace VM supplied no tabs - the navigation band " +
                    "renders empty (canon 2 expects BUILD / ARMY / RESEARCH)");
                return;
            }
            for (int i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                if (t == null) continue;
                float x0 = SlotStart(i, tabs.Count);
                float x1 = SlotEnd(i, tabs.Count);
                // Full band height inset 0.04-0.96 => 115px at TabsBandPx=120, margin +3px over
                // MinTouchPx(112).
                var btn = ElarionUiKit.Button(band, t.Label ?? string.Empty,
                    t.IsActive ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(x0, 0.04f), new Vector2(x1, 0.96f),
                    MakeInvoker(t.Activate));
                Track(btn);
                // SHAPE cue for the active tab, not colour alone: a solid underline bar.
                if (t.IsActive) Underline(btn.transform);
            }
        }

        private void BuildFilters(RectTransform band, ManageTabVM tab)
        {
            var filters = tab.Filters;
            for (int i = 0; i < filters.Count; i++)
            {
                var f = filters[i];
                if (f == null) continue;
                var btn = ElarionUiKit.Button(band, f.Label ?? string.Empty,
                    f.IsActive ? ElarionUiKit.ButtonKind.Gold : ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(SlotStart(i, filters.Count), 0.04f),
                    new Vector2(SlotEnd(i, filters.Count), 0.96f),
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

            int columns = tab.GridColumns > 0 ? tab.GridColumns : 3;
            float bandW = band.rect.width > 1f ? band.rect.width : 1080f;
            float bandH = band.rect.height > 1f ? band.rect.height : LastGridPx;

            float cellW = (bandW - (columns - 1) * TileGapPx) / columns;
            float cellH = Mathf.Clamp(cellW, MinTileHeightPx, MaxTileHeightPx);
            if (cellW < 1f)
            {
                FlowTrace.Warn("Manage", "grid width resolved to " + bandW.ToString("0") + "px for " +
                    columns + " columns - the model's column request cannot be honoured and the tiles " +
                    "would be sub-pixel. Reporting rather than re-columning silently");
                return;
            }

            // The model REQUESTS columns; the renderer reports in px when the well cannot show a
            // full row, rather than quietly changing the layout the tab asked for.
            int visibleRows = Mathf.FloorToInt((bandH + TileGapPx) / (cellH + TileGapPx));
            if (visibleRows < 1)
                FlowTrace.Warn("Manage", "grid band " + bandH.ToString("0") + "px cannot seat one " +
                    cellH.ToString("0") + "px tile row at " + columns + " columns");

            var scrollGo = new GameObject("ManageGridScroll", typeof(RectTransform), typeof(Image),
                typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(band, false);
            Track(scrollGo);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
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
        }

        /// <summary>
        /// One tile. The layer stack is fixed - plate, portrait, frame overlay, selection
        /// overlay, medallion - so a FRAME SWAP IS A SPRITE SWAP even though the delivered
        /// frames are not a consistent set (see ManageArt's header: two opaque centres, two
        /// hollow, and frame-selected's glow bleeds outside its rect). The plate is always
        /// painted so the two hollow frames have something behind them; frame-selected lives on
        /// its OWN, LARGER rect so its bleed does not have to 9-slice against the others.
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

            // LAYER 2 - the portrait.
            var portZone = Zone(cell, "TilePortrait",
                new Vector2(TilePortX0, TilePortY0), new Vector2(TilePortX1, TilePortY1));
            ElarionUiKit.Portrait(portZone, ManageArt.LoadSprite(tile.PortraitKey), active: false);

            // LAYER 3 - the state frame, preserve-aspect overlay. NOT sliced (ManageArt header).
            PaintSprite(cell, "TileFrame", Vector2.zero, Vector2.one, tile.FrameKey);

            // LAYER 4 - selection, as SHAPE + frame, never colour alone (owner is colourblind).
            if (tile.IsSelected)
            {
                // frame-selected's glow bleeds OUTSIDE its rect, so it gets a rect 6% larger on
                // every side. Overlaying it on the same rect as the others clips the glow and
                // makes the two frame families disagree at the edge.
                PaintSprite(cell, "TileSelectedGlow", new Vector2(-0.06f, -0.06f),
                    new Vector2(1.06f, 1.06f), ManageArt.FrameSelected);
                var bar = ElarionUiKit.AddImage(cell, "TileSelectedBar",
                    new Vector2(0f, 0f), new Vector2(TileSelBarX1, 1f), ElarionUi.Gold);
                var barImage = bar.GetComponent<Image>();
                if (barImage != null) barImage.raycastTarget = false;
            }

            // LAYER 5 - the status medallion (canon 8: mandatory, model-supplied).
            PaintSprite(cell, "TileStatus", new Vector2(TileMedX0, TileMedY0),
                new Vector2(TileMedX1, TileMedY1), tile.StateIconKey);

            // TEXT. Both bands clear the 28px cull floor at cellH >= 150.
            var title = ElarionUiKit.Label(cell, tile.Title ?? string.Empty, TileTitleY0, TileTitleY1,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: tile.IsSelected);
            ElarionUiKit.FitSingleLine(title, 20f, 30f);      // band 33px at cellH=150

            string stateLine = Join(tile.StateText, tile.TimerText);
            if (string.IsNullOrEmpty(stateLine)) stateLine = tile.Subtitle;
            var state = ElarionUiKit.Label(cell, stateLine ?? string.Empty, TileStateY0, TileStateY1,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.05f, 0.95f);
            ElarionUiKit.FitSingleLine(state, 18f, 26f);      // band 30px at cellH=150

            // A BAR, not text - exempt from the cull floor because nothing is typeset in it.
            if (tile.Progress01.HasValue) ProgressBar(cell, tile.Progress01.Value,
                TileProgY0, TileProgY1, 0.05f, 0.95f);

            if (cellH < MinTileHeightPx)
                FlowTrace.Warn("Manage", "tile cell resolved to " + cellH.ToString("0") + "px, under the " +
                    MinTileHeightPx + "px floor - its text bands fall under the " + MinTextBandPx +
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

            var title = ElarionUiKit.Label(band, activity.Title ?? string.Empty, 0.52f, 0.94f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.14f, 0.62f, bold: true);
            ElarionUiKit.FitSingleLine(title, 22f, 32f);      // band 50px

            var timer = ElarionUiKit.Label(band, Join(activity.TimerText, activity.QueuedCountText),
                0.08f, 0.48f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.Left, 0.14f, 0.62f);
            ElarionUiKit.FitSingleLine(timer, 20f, 30f);      // band 48px

            // 0.64-0.98 x, 0.06-0.94 y => 106px at ActivityBandPx=120. Under MinTouchPx(112), so
            // the band inset is widened to 0.02-0.98 => 115px instead. Stated because a 106px CTA
            // is exactly the case ElarionUiKit.cs:1100 says not to leave to ClampMinTouch.
            var open = ElarionUiKit.Button(band, "QUEUE", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.64f, 0.02f), new Vector2(0.98f, 0.98f),
                () => activity.OpenQueue?.Invoke());
            Track(open);
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

            ManageSelectionVM sel = tab != null ? tab.Selection : null;
            if (sel == null || !sel.Visible)
            {
                string empty = sel != null ? sel.EmptyText : null;
                var label = ElarionUiKit.Label(band, empty ?? string.Empty, 0.36f, 0.64f,
                    ElarionUi.ParchmentDim, ElarionUi.FontBody, TextAlignmentOptions.Center);
                ElarionUiKit.FitSingleLine(label, 24f, 38f);
                return;
            }

            // The CTA faces are resolved FIRST because whether any of them is refused decides
            // whether the "why" band is needed, and that band is part of the fixed floor.
            var faces = VisibleFaces(sel);
            bool needWhy = false;
            for (int i = 0; i < faces.Count; i++)
                if (!faces[i].Enabled && !string.IsNullOrEmpty(faces[i].DisabledReasonText)) needWhy = true;

            // Which sub-bands fit, in the stated drop order. Every drop is announced in px; no
            // band is ever shrunk under the cull floor.
            bool showLevel = true, showDesc = true, showStats = true;
            float need = SelTitlePx + SelLevelPx + SelDescPx + SelStatsPx + SelCostPx + SelActionPx
                       + 5f * SelGapPx
                       + (needWhy ? SelWhyPx + SelGapPx : 0f);
            if (need > bandPx) { showStats = false; need -= SelStatsPx + SelGapPx; }
            if (need > bandPx) { showDesc = false; need -= SelDescPx + SelGapPx; }
            if (need > bandPx) { showLevel = false; need -= SelLevelPx + SelGapPx; }
            if (need > bandPx)
                FlowTrace.Warn("Manage", "selection card needs " + need.ToString("0") + "px in a " +
                    bandPx.ToString("0") + "px band even after dropping stats/description/level. The " +
                    "title and cost lines may be clipped; the CTA band is never sacrificed");
            if (!showStats || !showDesc || !showLevel)
                FlowTrace.Warn("Manage", "selection card at " + bandPx.ToString("0") + "px dropped bands " +
                    "(level=" + showLevel + " description=" + showDesc + " stats=" + showStats + ") rather " +
                    "than shrink them under the " + MinTextBandPx + "px TMP cull floor");

            float cursor = 0f;
            var titleBand = BandFromTop(band, "SelTitle", cursor, SelTitlePx);
            cursor += SelTitlePx + SelGapPx;

            PaintSprite(titleBand, "SelStatus", new Vector2(0.90f, 0.02f), new Vector2(0.99f, 0.98f),
                sel.StateIconKey);
            var title = ElarionUiKit.Label(titleBand, sel.Title ?? string.Empty, 0.02f, 0.98f,
                ElarionUi.Gold, ElarionUi.FontBody, TextAlignmentOptions.Left, 0.02f, 0.60f, bold: true);
            ElarionUiKit.FitSingleLine(title, 26f, 40f);      // band 44px
            var stateWord = ElarionUiKit.Label(titleBand, sel.StateText ?? string.Empty, 0.02f, 0.98f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Right, 0.62f, 0.88f);
            ElarionUiKit.FitSingleLine(stateWord, 20f, 30f);  // band 44px

            if (showLevel)
            {
                var level = ElarionUiKit.Label(BandFromTop(band, "SelLevel", cursor, SelLevelPx),
                    Join(sel.LevelText, sel.ProgressText), 0.02f, 0.98f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.98f);
                ElarionUiKit.FitSingleLine(level, 20f, 30f);  // band 34px
                cursor += SelLevelPx + SelGapPx;
            }

            if (showDesc)
            {
                var descBand = BandFromTop(band, "SelDesc", cursor, SelDescPx);
                var desc = ElarionUiKit.Label(descBand, Join(sel.Description, sel.AuxiliaryText),
                    0.02f, 0.98f, ElarionUi.Parchment, ElarionUi.FontLabel,
                    TextAlignmentOptions.TopLeft, 0.02f, 0.98f);
                desc.enableAutoSizing = false;
                desc.fontSize = 24f;                          // band 44px seats two 22px lines
                desc.overflowMode = TextOverflowModes.Ellipsis;
                cursor += SelDescPx + SelGapPx;
            }

            if (showStats)
            {
                var statsBand = BandFromTop(band, "SelStats", cursor, SelStatsPx);
                var stats = ElarionUiKit.Label(statsBand, StatsLine(sel.Stats), 0.02f, 0.98f,
                    ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.98f);
                ElarionUiKit.FitSingleLine(stats, 20f, 28f);  // band 34px
                cursor += SelStatsPx + SelGapPx;
            }

            var costBand = BandFromTop(band, "SelCost", cursor, SelCostPx);
            var cost = ElarionUiKit.Label(costBand, CostLine(sel.Costs), 0.02f, 0.98f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.98f);
            ElarionUiKit.FitSingleLine(cost, 20f, 28f);       // band 34px
            cursor += SelCostPx + SelGapPx;

            if (needWhy)
            {
                // Canon 11 question 6: if I cannot act, WHY. This band exists so the sentence has
                // a real 34px home instead of being squeezed under a 120px CTA - a sentence with
                // nowhere to sit is a sentence TMP culls, and a greyed button with no reason is
                // the exact defect this program exists to remove.
                var whyBand = BandFromTop(band, "SelWhy", cursor, SelWhyPx);
                var why = ElarionUiKit.Label(whyBand, WhyLine(faces), 0.02f, 0.98f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, 0.98f);
                ElarionUiKit.FitSingleLine(why, 18f, 26f);    // band 34px >= 28 floor
                cursor += SelWhyPx + SelGapPx;
            }

            BuildActionRow(BandFromTop(band, "SelActions", cursor, SelActionPx), faces);

            if (sel.Progress.HasValue)
                ProgressBar(costBand, sel.Progress.Value, 0.00f, 0.10f, 0.02f, 0.98f);
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
        /// At SelActionPx=120 a 0.04-0.96 vertical inset would give 110px, UNDER MinTouchPx(112);
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

        private static string StatsLine(IReadOnlyList<ManageStatVM> stats)
        {
            if (stats == null || stats.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i];
                if (s == null) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(s.Label).Append(' ').Append(s.Value);
                if (!string.IsNullOrEmpty(s.DeltaText)) sb.Append(' ').Append(s.DeltaText);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Concatenates the model's cost rows. The AFFORDABLE verdict is the model's - this only
        /// marks the ones it flagged, with a WORD-shaped marker so the cue survives greyscale.
        /// </summary>
        private static string CostLine(IReadOnlyList<ManageCostVM> costs)
        {
            if (costs == null || costs.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                if (c == null) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(c.AmountText).Append(' ').Append(c.Label);
                if (!c.Affordable) sb.Append(" (SHORT)");
            }
            return sb.ToString();
        }

        private void Track(GameObject go) { if (go != null) _spawned.Add(go); }
        private void Track(Component c) { if (c != null) _spawned.Add(c.gameObject); }
    }
}
