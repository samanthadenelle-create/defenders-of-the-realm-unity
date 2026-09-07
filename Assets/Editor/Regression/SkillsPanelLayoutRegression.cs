// =============================================================================
// SkillsPanelLayoutRegression [skills-panel-layout] (WO-865) - the Grom (Knight)
// Skills panel can never go back to fraction bands, an unclipped grid, or a label
// that gets painted over / truncated.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
// Markers: SKILLS_PANEL_LAYOUT_OK / SKILLS_PANEL_LAYOUT_FAIL.
//
// WHAT BROKE (real Seeker capture 2026-08-04, 2340x1080 --
// docs/ui-review/2026-08-04-seeker/07-skills-panel.png). Not styling: the panel was
// structurally failing, and every symptom traces to ONE cause -- vertical bands
// expressed as fractions of a body well that is only ~493 reference px tall:
//
//   scaler 1080x1920 match 0.5 @ 2340x1080 -> scale 1.1040 -> canvas 2119.6x978.3
//   panel 0.07,0.05-0.93,0.95            -> 1822.9 x 880.5
//   FrameTalent body, after the shared-Close band reservation -> 1695 x 493 px
//
//   * The action row was anchored 0.070..0.135 OF THAT BODY = 32 px. ElarionUiKit's
//     touch floor (ClampMinTouch / MinTouchPx 112) then grew each button by +-40 px
//     ABOUT ITS CENTRE, so its top reached 106.6 px while the graph well's floor sat
//     at 0.165*493 = 81.4 px. That 25 px is Cancel / CONFIRM / Respec painting OVER
//     the grid, and Respec covering quick-slot 4 outright.
//   * The quick-swap grid sliced its host into 1/2 x 1/2: tiles resolved to ~47 px
//     (below the touch floor) and the NAME band inside them to ~23 px -- under one
//     FontFloor line box -- which is why "Emberbrand Throw" read "Emberbrand Thro".
//   * CONFIRM's rect spanned x 0.52..0.80 while the right column started at 0.675,
//     so its 9-sliced green pack fill ran under the ability list ("bleeds past its
//     own bounds").
//   * The graph content rect was CENTRE-anchored and wider than its mask, so the
//     extreme authored columns (x 0.08 / 0.94) were sliced mid-plate at BOTH edges.
//   * The "Universal - any class" band sat at authored y 0.965 and the shared node
//     row at y 0.98 -- 15.6 px apart with 96 px plates, nodes built after the band.
//
// Same failure class as WO-841 / WO-852 / WO-832 Sec.4. This oracle is a CHEAP
// STRUCTURAL guard, not a pixel test: it re-runs the panel's own band arithmetic at
// the reference body rect and pins the properties that make the bug impossible.
// Pixel truth stays with RunCaptureHeadless + eyes-on the device.
//
//   1 [floors]      every band constant is >= its own floor (touch floor for a
//                   tappable band, a whole TMP line box for a text band).
//   2 [bands]       the band STACK cannot overlap and cannot starve: replaying the
//                   view's arithmetic at the 493 px reference body leaves a positive
//                   graph well, a >= 2-line description, and a tile whose two line
//                   boxes fit inside the ability band.
//   3 [grid]        the graph content can never exceed / be sliced by its container:
//                   the pad is at least half a node plate; the authored json holds its
//                   presence / uniqueness / 0..1 contract; and a FULL PAIRWISE sweep
//                   over the RESOLVED positions - the view's own SolveGraphLatticePx,
//                   run headlessly at the reference well - proves every pair clears
//                   MinNodePitchPx and that no plate sits inside the focus half-plate
//                   inset (WO-1021 sec 2.1b; before this the case read authored x/y
//                   only and skipped every auto-placed node, so it certified a lattice
//                   that does not ship).
//   4 [truncation]  at the reference resolution no label is forced to ellipsize: the
//                   longest UNBREAKABLE word in abilities.json fits an ability tile
//                   at the kit FontFloor, and the longest name in hero-talents.json
//                   fits the detail column.
//   5 [source]      the laws that make the regression unreachable: RectMask2D clip,
//                   top-left content pivot, the fixed-pixel band pins, the reserved
//                   section row, no 1/n fraction slicing, no green ButtonConfirm
//                   overlay (the fill that bled), and no embedded NUL.
//   7 [rail]        WO-1401 (Builds/ui-capture.log 2026-09-05 05:13: BUTTON OVER TEXT x9,
//                   ObsBtn_1..3 covering QuickSwapHint by 112x9 at all three aspects). A
//                   REAL-GEOMETRY pin: it calls the view's OWN BuildQuickSwapRailHost and
//                   ApplyQuickSwapSlotSize into an edit-mode canvas at the reference body,
//                   settles layout, runs LayoutOracle.Audit and asserts that no slot button
//                   intersects any text it does not own, that the hint clears the slot band
//                   by BandGapPx, that a label at the graph well's floor clears the rail, and
//                   that the band constants add up. Mutation that reds it: pin the hint
//                   `PinBandFromTop(hint, QuickSwapHintBandPx + BandGapPx, QuickSwapHintBandPx)`
//                   (the hint drops into the slot band).
//   9 [bezel-art]   WO-1601. An ART oracle: it decodes the PNG named by
//                   HeroSkillTreePanelMvvm.BezelFrameResource and asserts painted alpha exists
//                   inside ALL FOUR of its 9-slice border strips. card-frame-empty's top and
//                   bottom 96 px strips are 100% transparent (painted rows 165..713 of 887), so
//                   Image.Type.Sliced put its rails ~10%/89% INTO the rect -- the gold band the
//                   owner photographed straight across the talent tree, and, on the 172 px shelf
//                   bezel, no edge at all. Mutation that reds it: point BezelFrameResource back
//                   at "UI/ElarionMedieval/frames/card-frame-empty".
//  10 [fit]         WO-1601. Runs the two shipped statics -- SolveGraphLatticePx then
//                   ResolveGraphFitScale -- over the Lv2 board (seven lanes, two ranks) at the
//                   MEASURED device well and asserts nothing overhangs the rest window sideways
//                   and nothing overhangs it downward by half a plate. Mutation that reds it:
//                   delete the `_graphContent.localScale = new Vector3(fitScale, ...)` line, or
//                   make ResolveGraphFitScale return 1f -- 85 px / 85 px of overhang, which is
//                   the sliced 0/1 medallions in Screenshot_20260907-132616.png.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.SkillsPanelLayoutRegression.RunAll
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Village.Talents;

namespace DeNelle.Editor.Regression
{
    public static class SkillsPanelLayoutRegression
    {
        private const string ViewSrc = "Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs";
        private const string VmSrc = "Assets/_Modules/Village/Talents/HeroSkillTreeVM.cs";
        private const string TalentsJson = "Assets/Resources/Data/Canonical/hero-talents.json";
        private const string AbilitiesJson = "Assets/Resources/Data/Canonical/abilities.json";

        private const string ViewType = "DeNelle.Village.Talents.HeroSkillTreePanelMvvm";
        private const string KitType = "DeNelle.Core.UI.ElarionUiKit";
        private const string ObsidianType = "DeNelle.Core.UI.ElarionUiKit";

        /// <summary>The TMP line box multiplier the panel's bands are budgeted from (~1.25em).</summary>
        private const float LineBoxMul = 1.25f;

        /// <summary>Average glyph advance as a fraction of the font size, for bold mixed-case
        /// LiberationSans. Deliberately CONSERVATIVE (the real average is nearer 0.50) so a
        /// pass here means real headroom, not a rounding win.</summary>
        private const float AvgAdvanceEm = 0.55f;

        // ── THE REFERENCE RECT (2340x1080, the device the capture came from) ──────
        // Derived once in the header above; restated here as the numbers the band
        // arithmetic is replayed against. These are a DEVICE fact, not a layout knob:
        // if the frame's body zone or the shared-Close reservation changes, this suite
        // should be re-derived, not relaxed.
        private const float RefBodyWidthPx = 1695f;
        private const float RefBodyHeightPx = 493f;

        // ── THE GRAPH WELL ON THE OWNER'S DEVICE (WO-1601) ────────────────────────
        // MEASURED off Logs/device/seeker-shots/Screenshot_20260907-132616.png (Seeker
        // 2670x1200; canvas 1080x1920 match 0.5 -> scale 1.243 -> 2148x965 units), not derived
        // from the reference body above - the two disagree and the frame is the ground truth:
        //   * the loadout shelf plate reads x 310..2359, y 816..1014 device px. It is the view's
        //     own 0.02..0.98 x QuickSwapRailPx(160) rect, so the workspace is 2134 device px
        //     wide (1717 ref px) starting at device x 267.
        //   * the well's floor is the workspace floor + GraphWellFloorPx(182) = device y 805,
        //     which is exactly where the frame cuts the bottom medallions.
        //   * its ceiling is the workspace top (device y 240, fixed by the WISDOM chip's gold
        //     edge at 247 = top + BodyPadPx) + WisdomBandPx + BandGapPx.
        // -> 1705 x 395 REFERENCE px. Re-measure from a fresh frame if the chrome moves; do not
        //    "tidy" these into a formula, because the formula is what disagreed.
        private const float DeviceWellWpx = 1705f;
        private const float DeviceWellHpx = 395f;

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SKILLS_PANEL_LAYOUT_OK - " + reason);
            else Debug.LogError("SKILLS_PANEL_LAYOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "floors", () => Case1_Floors(failures, notes));
                Case(failures, "bands", () => Case2_BandStack(failures, notes));
                Case(failures, "grid", () => Case3_GridContainment(failures, notes));
                Case(failures, "truncation", () => Case4_Truncation(failures, notes));
                Case(failures, "source", () => Case5_SourceLaws(failures, notes));
                Case(failures, "popup", () => Case6_SpendPopup(failures, notes));
                Case(failures, "rail", () => Case7_QuickSwapRail(failures, notes));
                Case(failures, "elevation", () => Case8_Elevation(failures, notes));
                Case(failures, "bezel-art", () => Case9_BezelArt(failures, notes));
                Case(failures, "fit", () => Case10_GraphFit(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "SKILLS PANEL LAYOUT OK - full-bleed graph body (owner 2026-08-15), " +
                         "spend-popup action band clears the kit touch floor, text floors hold a line box, " +
                         "the node graph is padded + clipped on a fixed-pixel lattice, no catalog " +
                         "label is forced to ellipsize at 2340x1080, and the spend popup wraps its full " +
                         "ASCII-only description inside a frame that encloses it (WO-1342), and the " +
                         "quick-swap slots and their hint are disjoint bands measured on the real builder " +
                         "(WO-1401), and the screen carries a three-tier ELEVATION LADDER whose steps are " +
                         "measured in Rec.709 luma so depth survives a greyscale capture (WO-1522), and the " +
                         "bezel sprite paints inside all four of its own 9-slice border strips so a frame can " +
                         "never draw a band ACROSS what it frames, and the solved board is fitted into its " +
                         "well so no medallion is sliced at the mask edge (WO-1601)" + noteStr;
                return true;
            }
            reason = "skills-panel-layout FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  Shared constant reads
        // =====================================================================

        private sealed class Layout
        {
            public float MinTouch, FontFloor, LineBox;
            public float BodyPad, BandGap, ActionRow, AbilityRow, NodeSize;
            public float Wisdom, DetailHead, DetailName, DetailState;
            public float SlotKey, SlotName, SlotPad;
            public float SectionBand, SectionGap, SectionClear;
            public float UnitW, UnitH, GraphPad;
            public float ColX1, DetailX0, SlotGap, SlotInset, DetailInset;
            public bool Ok;
        }

        private static Layout ReadLayout(List<string> failures, string tag)
        {
            var L = new Layout();
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            if (view == null)
            {
                failures.Add($"{tag} {ViewType} not found - the Skills panel view was renamed or removed; " +
                             "re-point this oracle rather than deleting the only guard on its band stack");
                return L;
            }
            if (kit == null)
            {
                failures.Add($"{tag} {KitType} not found - cannot read the kit touch/font floors");
                return L;
            }

            L.MinTouch = ConstFloat(kit, "MinTouchPx", failures, tag);
            L.FontFloor = ConstFloat(FindType(ObsidianType), "FontFloor", failures, tag);
            if (L.MinTouch <= 0f || L.FontFloor <= 0f) return L;
            L.LineBox = L.FontFloor * LineBoxMul;

            L.BodyPad = ConstFloat(view, "BodyPadPx", failures, tag);
            L.BandGap = ConstFloat(view, "BandGapPx", failures, tag);
            L.ActionRow = ConstFloat(view, "ActionRowPx", failures, tag);
            L.AbilityRow = ConstFloat(view, "AbilityRowPx", failures, tag);
            L.NodeSize = ConstFloat(view, "NodeSizePx", failures, tag);
            L.Wisdom = ConstFloat(view, "WisdomBandPx", failures, tag);
            L.DetailHead = ConstFloat(view, "DetailHeadPx", failures, tag);
            L.DetailName = ConstFloat(view, "DetailNamePx", failures, tag);
            L.DetailState = ConstFloat(view, "DetailStatePx", failures, tag);
            L.SlotKey = ConstFloat(view, "SlotKeyBandPx", failures, tag);
            L.SlotName = ConstFloat(view, "SlotNameBandPx", failures, tag);
            L.SlotPad = ConstFloat(view, "SlotPadPx", failures, tag);
            L.SectionBand = ConstFloat(view, "SectionBandPx", failures, tag);
            L.SectionGap = ConstFloat(view, "SectionGapPx", failures, tag);
            L.SectionClear = ConstFloat(view, "SectionClearPx", failures, tag);
            L.UnitW = ConstFloat(view, "GraphUnitWpx", failures, tag);
            L.UnitH = ConstFloat(view, "GraphUnitHpx", failures, tag);
            L.GraphPad = ConstFloat(view, "GraphPadPx", failures, tag);
            L.ColX1 = ConstFloat(view, "GraphColumnX1", failures, tag);
            L.DetailX0 = ConstFloat(view, "DetailColumnX0", failures, tag);
            L.SlotGap = ConstFloat(view, "SlotGapFrac", failures, tag);
            L.SlotInset = ConstFloat(view, "SlotTextInsetFrac", failures, tag);
            L.DetailInset = ConstFloat(view, "DetailTextInsetFrac", failures, tag);

            L.Ok = L.ActionRow > 0f && L.AbilityRow > 0f && L.NodeSize > 0f && L.UnitW > 0f &&
                   L.UnitH > 0f && L.SlotName > 0f && L.ColX1 > 0f && L.DetailX0 > 0f;
            return L;
        }

        // =====================================================================
        //  CASE 1 - the numeric floors
        // =====================================================================
        private static void Case1_Floors(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[floors]");
            if (!L.Ok) return;

            // TAPPABLE bands must clear the kit touch floor. A band SHORTER than the floor is
            // the bug itself: ClampMinTouch grows the control past the band on both sides and
            // it lands on its neighbours (that is verbatim what put the action row on the grid).
            TouchFloor(failures, "ActionRowPx", L.ActionRow, L.MinTouch,
                "Cancel / Respec / CONFIRM live here; a shorter band is grown by ClampMinTouch " +
                "straight over the graph well and the ability slots");
            TouchFloor(failures, "AbilityRowPx", L.AbilityRow, L.MinTouch,
                "the quick-swap slots are tap targets");
            TouchFloor(failures, "NodeSizePx", L.NodeSize, L.MinTouch,
                "a graph node IS a Button - it was 96 px, below the floor, before WO-865");

            // TEXT bands must seat a whole line box or TMP culls / ellipsizes the line.
            LineFloor(failures, "SlotNameBandPx", L.SlotName, L.LineBox * 2f, L.LineBox,
                "the ability name must be able to WRAP to two whole lines - one line box could " +
                "not hold \"Suppressing Volley\" in a ~250 px tile and it would ellipsize again");
            LineFloor(failures, "SlotKeyBandPx", L.SlotKey, L.LineBox, L.LineBox, "the slot numeral");
            LineFloor(failures, "SectionBandPx", L.SectionBand, L.LineBox, L.LineBox,
                "the \"Universal - any class\" divider label (it was 36 px against a ~44 px line)");
            LineFloor(failures, "DetailHeadPx", L.DetailHead, L.LineBox, L.LineBox, "\"SELECTED TALENT\"");
            LineFloor(failures, "DetailNamePx", L.DetailName, L.LineBox, L.LineBox, "the talent name");
            LineFloor(failures, "DetailStatePx", L.DetailState, L.LineBox, L.LineBox, "the \"Requires ...\" line");
            LineFloor(failures, "WisdomBandPx", L.Wisdom, L.LineBox, L.LineBox, "the WISDOM currency chip");

            // The section band must own a row that can hold a node plate AND the band with air.
            float clearNeeded = L.NodeSize + L.SectionBand + L.SectionGap * 2f;
            if (L.SectionClear < clearNeeded)
                failures.Add($"[floors] SectionClearPx={L.SectionClear} < node plate + band + air ({clearNeeded}) - " +
                             "the reserved row cannot hold both, so a node plate paints over the divider label " +
                             "again (the capture's \"Univers[icon]y class\")");

            notes.Add($"floors: touch={L.MinTouch}, lineBox={L.LineBox:F1}; action={L.ActionRow}, " +
                      $"ability={L.AbilityRow}, node={L.NodeSize}, slotName={L.SlotName}");
        }

        private static void TouchFloor(List<string> failures, string name, float v, float floor, string why)
        {
            if (v < floor)
                failures.Add($"[floors] HeroSkillTreePanelMvvm.{name}={v} is BELOW the kit touch floor " +
                             $"MinTouchPx={floor} - {why}");
        }

        private static void LineFloor(List<string> failures, string name, float v, float need, float lineBox, string why)
        {
            if (v < need)
                failures.Add($"[floors] HeroSkillTreePanelMvvm.{name}={v} is shorter than the {need:F1} px it " +
                             $"needs (one TMP line box at the kit FontFloor is {lineBox:F1}) - {why}; " +
                             "a band shorter than its line box silently CULLS or ellipsizes glyphs " +
                             "(WO-832 Sec.4 / WO-841 / WO-852)");
        }

        // =====================================================================
        //  CASE 2 - the band STACK cannot overlap and cannot starve
        // =====================================================================
        private static void Case2_BandStack(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[bands]");
            if (!L.Ok) return;

            // Owner 2026-08-15: the body is FULL-BLEED GRAPH only (no action/ability/detail
            // bands consuming height). ActionRowPx / AbilityRowPx remain kit-floor constants
            // for the spend popup's button band, but they no longer stack into the body.
            float graphWellH = RefBodyHeightPx - L.BodyPad * 2f;
            if (graphWellH < L.NodeSize * 2f)
                failures.Add($"[bands] the graph well resolves to {graphWellH:F0} px at the reference body - " +
                             $"less than TWO node plates ({L.NodeSize * 2f}). Body padding has eaten the tree");

            // Spend-popup Confirm/Cancel still need the kit touch floor (ActionRowPx).
            if (L.ActionRow < L.MinTouch)
                failures.Add($"[bands] ActionRowPx={L.ActionRow} is below MinTouchPx={L.MinTouch} - the " +
                             "spend-popup Confirm/Cancel band would be grown by ClampMinTouch");

            // Popup prompt/description text floors (detail constants reused as popup copy floors).
            if (L.DetailName < L.LineBox)
                failures.Add($"[bands] DetailNamePx={L.DetailName} is shorter than one line box - the " +
                             "spend-popup talent name would cull");
            if (L.DetailState < L.LineBox)
                failures.Add($"[bands] DetailStatePx={L.DetailState} is shorter than one line box - the " +
                             "spend-popup cost/lock line would cull");

            notes.Add($"@{RefBodyWidthPx}x{RefBodyHeightPx}: full-bleed graphWell={graphWellH:F0}, " +
                      $"popupActionFloor={L.ActionRow}, wisdomChip={L.Wisdom}");
        }

        // =====================================================================
        //  CASE 3 - the grid can never exceed / be sliced by its container, and no
        //  two plates can overlap - MEASURED ON THE POSITIONS THAT SHIP.
        //
        //  MOVED 2026-08-16 (WO-1021 sec 2.1b/2.1c item 5): every earlier shape of
        //  this case measured AUTHORED json x/y through the FALLBACK constants
        //  (1180x780) and `continue`d past every auto-placed node, so it certified a
        //  lattice the runtime no longer draws - which is how the suite stayed GREEN
        //  over the row overlaps measured on device in s3.png. It also demanded the
        //  geometrically impossible (7 rows x 136 px = 952 px of plates inside a
        //  780 px authored lattice), making its own advice line unfollowable.
        //  The pitch assertion now runs the view's OWN SolveGraphLatticePx and
        //  measures ITS output; the authored data is still checked for exactly what
        //  it promises - presence, uniqueness and the 0..1 contract. Nothing was
        //  weakened: the resolved check is STRICTER (Chebyshev pitch >= 226.8 px,
        //  i.e. focus-based clearance, vs the old 136 px NodeSize basis).
        // =====================================================================
        private static void Case3_GridContainment(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[grid]");
            if (!L.Ok) return;

            float nodeFocus = ConstFloat(FindType(ViewType), "NodeFocusPx", failures, "[grid]");
            if (nodeFocus <= 0f) return;

            // The content rect must pad by at least HALF A NODE PLATE, or the extreme authored
            // rows/columns hang outside the content and get sliced mid-plate at the mask edge -
            // which is exactly "the grid overflows its container on BOTH sides".
            if (L.GraphPad < L.NodeSize * 0.5f)
                failures.Add($"[grid] GraphPadPx={L.GraphPad} is less than half a node plate " +
                             $"({L.NodeSize * 0.5f}) - the first and last authored rows/columns extend past the " +
                             "scroll content and are cut mid-plate at the mask edge");

            var pts = ParseAuthoredNodes(failures);
            if (pts == null) return;

            // ── (a) WHAT THE AUTHORED DATA ACTUALLY PROMISES ─────────────────────────
            // Presence, uniqueness and the 0..1 contract. NOT pixel geometry: since
            // WO-1021 sec 2.1b the authored x/y are an ORDERING HINT consumed by
            // SolveGraphLatticePx, not shipped coordinates. Measuring them in plate
            // pixels measured a lattice that does not ship - and demanded the impossible:
            // 7 rows x 136 px = 952 px of plates inside a 780 px authored lattice, with
            // only 1.95 rows of room below class tier-1. That is why the advice line
            // "re-author x/y" was unfollowable. The assertion MOVED to the resolved
            // positions below; it was never weakened.
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            int outOfRange = 0, duplicateIds = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].X > 1.0001f || pts[i].Y > 1.0001f)
                {
                    outOfRange++;
                    if (outOfRange <= 3)
                        failures.Add($"[grid] authored node '{pts[i].Id}' sits at ({pts[i].X:F3},{pts[i].Y:F3}) - " +
                                     "outside the 0..1 authoring contract the solver normalises from");
                }
                if (!seenIds.Add(pts[i].Id))
                {
                    duplicateIds++;
                    if (duplicateIds <= 3)
                        failures.Add($"[grid] authored id '{pts[i].Id}' carries more than one x/y pair - " +
                                     "the ordering hint is ambiguous");
                }
            }

            // ── (b) THE POSITIONS THAT ACTUALLY SHIP ─────────────────────────────────
            // Run the view's OWN solver headlessly and measure ITS output. Before this,
            // every [grid] check read authored json only (and `continue`d past every
            // auto-placed node), which is how the suite stayed green over the overlapping
            // rows measured on device in s3.png 2026-08-16.
            float minPitch = ConstFloat(FindType(ViewType), "MinNodePitchPx", failures, "[grid]");
            float rankBand = ConstFloat(FindType(ViewType), "RankBandPx", failures, "[grid]");
            if (minPitch <= 0f) return;

            var solver = FindType(ViewType).GetMethod("SolveGraphLatticePx",
                BindingFlags.Public | BindingFlags.Static);
            if (solver == null)
            {
                failures.Add("[grid] HeroSkillTreePanelMvvm.SolveGraphLatticePx is gone - position solving has " +
                             "been split back across methods and this oracle can no longer see what ships; " +
                             "re-point it rather than deleting the guard");
                return;
            }

            var flat = new float[pts.Count * 2];
            for (int i = 0; i < pts.Count; i++) { flat[i * 2] = pts[i].X; flat[i * 2 + 1] = pts[i].Y; }

            float boxW = RefBodyWidthPx - L.GraphPad * 2f;
            float boxH = RefBodyHeightPx - L.GraphPad * 2f - rankBand;
            float[] px;
            try { px = (float[])solver.Invoke(null, new object[] { flat, boxW, boxH }); }
            catch (Exception ex)
            {
                failures.Add("[grid] SolveGraphLatticePx THREW " + ex.GetType().Name + ": " + ex.Message);
                return;
            }
            if (px == null || px.Length != flat.Length)
            {
                failures.Add("[grid] SolveGraphLatticePx returned " + (px == null ? "null" : px.Length.ToString()) +
                             " values for " + pts.Count + " nodes");
                return;
            }

            float half = nodeFocus * 0.5f;
            int overlaps = 0;
            float minPairGapPx = float.MaxValue;
            string worstPair = "";
            for (int i = 0; i < pts.Count; i++)
            {
                if (px[i * 2] < half - 0.5f || px[i * 2 + 1] < half - 0.5f)
                    failures.Add($"[grid] resolved plate '{pts[i].Id}' sits at ({px[i * 2]:F0},{px[i * 2 + 1]:F0}), " +
                                 $"inside the FOCUS half-plate inset ({half:F0} px) - it is clipped mid-plate at " +
                                 "the mask edge the moment it is the oversized selected plate (the s2.png top clip)");
                for (int j = i + 1; j < pts.Count; j++)
                {
                    float dx = Mathf.Abs(px[i * 2] - px[j * 2]);
                    float dy = Mathf.Abs(px[i * 2 + 1] - px[j * 2 + 1]);
                    float sep = Mathf.Max(dx, dy);   // Chebyshev: an AABB pair is clear once EITHER axis clears
                    if (sep < minPairGapPx) { minPairGapPx = sep; worstPair = pts[i].Id + "/" + pts[j].Id; }
                    if (sep < minPitch - 0.5f)
                    {
                        overlaps++;
                        if (overlaps <= 3)
                            failures.Add($"[grid] RESOLVED plates break the pitch law: {pts[i].Id} vs {pts[j].Id} " +
                                         $"clear only {sep:F0} px against MinNodePitchPx {minPitch:F0} px " +
                                         $"(dx={dx:F0} dy={dy:F0}) - the plates touch and a corner cost pip lands " +
                                         "on the NEIGHBOURING plate. Fix the solver, never the plate consts");
                    }
                }
            }
            if (overlaps > 3)
                failures.Add($"[grid] ...plus {overlaps - 3} more resolved pitch violation(s)");

            // The measured record prints on EVERY gate run, pass or fail, so drift is visible.
            notes.Add($"resolved minPairGapPx={minPairGapPx:F1} vs law {minPitch:F1}, violations={overlaps} " +
                      $"(solver output over {pts.Count} authored nodes in a {boxW:F0}x{boxH:F0} box; " +
                      $"tightest pair {worstPair}); authored contract: outOfRange={outOfRange} dupIds={duplicateIds}");
        }

        private sealed class AuthoredNode
        {
            public string Id;
            public float X, Y;
        }

        /// <summary>Every authored (id, x, y) triple in the canonical json, in file order. The
        /// id/x/y walk pairs each x/y with the most recent "id" so overlap failures can NAME the
        /// colliding nodes. Parsed straight out of the json so the oracle tracks the data, not a
        /// snapshot of it.
        ///
        /// 2026-08-16 (talent shape law): EVERY node in all three trees and the shared pool now
        /// carries an authored x/y, so nothing is skipped here any more - the old note claiming
        /// "ranger/mage and the knight branches auto-layout and are out of scope" is retired.
        /// One consequence to read the [grid] note with: this parses the file as ONE flat list,
        /// so nodes from different trees that share a y land in the same solved row. That is
        /// harmless for what this case asserts (pitch + inset hold by construction at any row
        /// width) but it is NOT a per-tree row census - TalentTreeShapeRegression owns the
        /// per-tree shape law (bottom row <= 3, branching wider) and reports that census.</summary>
        private static List<AuthoredNode> ParseAuthoredNodes(List<string> failures)
        {
            string src = ReadText(TalentsJson, failures, "[grid]");
            if (src == null) return null;

            var result = new List<AuthoredNode>();
            string lastId = "?";
            var mx = Regex.Matches(src,
                "\"id\"\\s*:\\s*\"([^\"]+)\"|\"x\"\\s*:\\s*(-?[0-9.]+)\\s*,\\s*\"y\"\\s*:\\s*(-?[0-9.]+)");
            foreach (Match m in mx)
            {
                if (m.Groups[1].Success) { lastId = m.Groups[1].Value; continue; }
                float x = ParseF(m.Groups[2].Value), y = ParseF(m.Groups[3].Value);
                if (x < 0f || y < 0f) continue;   // -1 = unset/auto
                result.Add(new AuthoredNode { Id = lastId, X = x, Y = y });
            }
            if (result.Count == 0)
            {
                failures.Add("[grid] no authored x/y pairs found in " + TalentsJson + " - the node lattice " +
                             "cannot be checked; the authoring shape changed and this oracle is now blind");
                return null;
            }
            return result;
        }

        // =====================================================================
        //  CASE 4 - no label is truncated at the reference resolution
        // =====================================================================
        private static void Case4_Truncation(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[truncation]");
            if (!L.Ok) return;

            // -- ability tile: the name WRAPS, so the binding constraint is the longest
            //    UNBREAKABLE WORD, not the longest full name.
            const int slots = DeNelle.Village.AssignableSkillBar.SlotCount; // WO-1294: 1..3
            float leftColW = RefBodyWidthPx * L.ColX1;
            float tileW = leftColW * ((1f - L.SlotGap * (slots - 1)) / slots);
            float tileTextW = tileW * (1f - L.SlotInset * 2f);

            string longestWord = "", longestName = "";
            if (!LongestWordAndName(AbilitiesJson, failures, "[truncation]", ref longestWord, ref longestName))
                return;

            float wordW = longestWord.Length * L.FontFloor * AvgAdvanceEm;
            if (wordW > tileTextW)
                failures.Add($"[truncation] the longest unbreakable ability word \"{longestWord}\" needs " +
                             $"~{wordW:F0} px at the kit FontFloor but an ability tile only offers " +
                             $"{tileTextW:F0} px ({tileW:F0} px tile in a {leftColW:F0} px column) - it would " +
                             "ellipsize, which is the \"Emberbrand Thro\" defect in the 2026-08-04 capture");

            // -- detail column: the talent NAME is single-line (FitSingleLine), so the whole
            //    string has to fit.
            string longestTalent = "", ignored = "";
            if (!LongestWordAndName(TalentsJson, failures, "[truncation]", ref ignored, ref longestTalent))
                return;

            float detailW = RefBodyWidthPx * (1f - L.DetailX0);
            float detailTextW = detailW * (1f - L.DetailInset * 2f);
            float nameW = longestTalent.Length * L.FontFloor * AvgAdvanceEm;
            if (nameW > detailTextW)
                failures.Add($"[truncation] the longest talent name \"{longestTalent}\" needs ~{nameW:F0} px at " +
                             $"the kit FontFloor but the detail column only offers {detailTextW:F0} px - the " +
                             "SELECTED TALENT name would ellipsize");

            notes.Add($"widest word \"{longestWord}\" {wordW:F0}px / tile {tileTextW:F0}px; " +
                      $"widest talent \"{longestTalent}\" {nameW:F0}px / column {detailTextW:F0}px");
        }

        /// <summary>Longest whitespace-delimited WORD and longest whole "name" value in a catalog json.</summary>
        private static bool LongestWordAndName(string path, List<string> failures, string tag,
                                               ref string longestWord, ref string longestName)
        {
            string src = ReadText(path, failures, tag);
            if (src == null) return false;
            var m = Regex.Matches(src, "\"name\"\\s*:\\s*\"([^\"]*)\"");
            if (m.Count == 0)
            {
                failures.Add($"{tag} no \"name\" values found in {path} - the label-width check is blind");
                return false;
            }
            foreach (Match mm in m)
            {
                string name = mm.Groups[1].Value;
                if (name.Length > longestName.Length) longestName = name;
                foreach (var w in name.Split(' '))
                    if (w.Length > longestWord.Length) longestWord = w;
            }
            return true;
        }

        // =====================================================================
        //  CASE 5 - the source laws that make the regression unreachable
        // =====================================================================
        private static void Case5_SourceLaws(List<string> failures, List<string> notes)
        {
            string src = ReadText(ViewSrc, failures, "[source]");
            if (src == null) return;
            string code = StripComments(src);

            Law(failures, code, "RectMask2D",
                "the graph well lost its RectMask2D - without the clip the node plates paint " +
                "straight past the panel frame instead of scrolling inside it");
            Law(failures, code, "PinBandFromTop",
                "the fixed-pixel band pins are gone - bands are back on parent fractions (WO-841/852)");
            Law(failures, code, "PinBandFromBottom",
                "the fixed-pixel band pins are gone - bands are back on parent fractions (WO-841/852)");
            Law(failures, code, "SectionClearPx",
                "the section band's reserved row is gone - a node plate can paint over the " +
                "\"Universal - any class\" label again");
            Law(failures, code, "BuildSpendPopup",
                "the spend popup builder is gone - owner 2026-08-15: node tap must open name/desc/spend " +
                "Confirm without a permanent footer action row");
            Law(failures, code, "BuildActionRow",
                "BuildActionRow token missing (retired stub still required so the source-law oracle " +
                "keeps a stable anchor while the spend popup owns the buttons)");
            Law(failures, code, "BuildNodeTypeBadge",
                "skill nodes no longer state ACTIVE/PASSIVE/SLOT N in words, so hot-swappability " +
                "and the assigned quick-swap position cannot be read without opening every node");
            Law(failures, code, "btn.interactable = false",
                "the Skills quick-swap rail is no longer read-only; Loadout must be the one assignment owner");
            Law(failures, code, "UI/ElarionMedieval/frames/circular-bezel-four-point",
                "skill nodes no longer use the canonical black-iron/four-point-gold medallion");
            Law(failures, code, "fillGo.AddComponent<Mask>()",
                "skill artwork is no longer clipped into the circular medallion well");
            Law(failures, code, "next point at Level",
                "the top-right Wisdom balance no longer explains when the next point arrives");
            Law(failures, code, "UI/ElarionMedieval/frames/circular-bezel-four-point",
                "the three bottom quick-swap slots no longer use the shared circular medallion");
            Law(failures, code, "ConceptIconResolver.Resolve(slot.AbilityId)",
                "assigned quick-swap art no longer resolves through the canonical concept-icon table");
            if (code.IndexOf("SKILL POINTS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                code.IndexOf("RemainingSkillPoints", StringComparison.Ordinal) >= 0)
                failures.Add("[source] the redundant Skill Points header returned beside Wisdom - the talent tree must show one WISDOM balance");
            if (code.IndexOf("slot_talent_", StringComparison.Ordinal) >= 0)
                failures.Add("[source] legacy square slot_talent frames returned to the public Skills graph");

            string vmSrc = ReadText(VmSrc, failures, "[source-flow]");
            if (vmSrc != null)
            {
                string vmCode = StripComments(vmSrc);
                Law(failures, vmCode, "EquippedSlot",
                    "the VM no longer exposes the numbered quick-swap seat for an assigned active");
                Law(failures, vmCode, "KeyHeroLoadout",
                    "the read-only rail no longer points to the canon-named Loadout assignment owner");
                if (vmCode.IndexOf("AssignableSkillBarAccess.Assign(", StringComparison.Ordinal) >= 0 ||
                    vmCode.IndexOf("AssignableSkillBarAccess.Clear(", StringComparison.Ordinal) >= 0)
                    failures.Add("[source-flow] Skills mutates a quick-swap socket again; only Loadout may assign or clear");
                if (!Regex.IsMatch(vmCode, "SpendSelected\\s*\\([^)]*\\).*?_selectedId\\s*=\\s*\"\"",
                        RegexOptions.Singleline))
                    failures.Add("[source-flow] SpendSelected no longer dismisses after learning; Skills is growing a second assignment step");
            }

            // The content rect must be TOP-LEFT pivoted. Centre-pivoting a content rect wider
            // than its mask is what sliced a node off BOTH frame edges.
            if (!Regex.IsMatch(code, @"_graphContent\.pivot\s*=\s*new\s+Vector2\s*\(\s*0f\s*,\s*1f\s*\)"))
                failures.Add("[source] the graph content is no longer TOP-LEFT pivoted - a content rect wider " +
                             "than its mask, centred, is sliced mid-plate at BOTH edges (the capture's " +
                             "\"overflows its container on both sides\")");

            // The 1/n fraction slice - the WO-852 shape, verbatim.
            if (Regex.IsMatch(code, @"1f\s*/\s*(?:n|rows|cols|count|Length)\b", RegexOptions.IgnoreCase))
                failures.Add("[source] the panel slices a host into 1/n FRACTIONS again - each slice resolves " +
                             "below MinTouchPx and ClampMinTouch then grows the control past the slice on BOTH " +
                             "sides, stacking it on its neighbours (WO-852 verbatim). Size bands in fixed px.");

            // The green pack fill that bled past the button bounds.
            if (code.IndexOf("ButtonConfirm", StringComparison.Ordinal) >= 0)
                failures.Add("[source] the action row overlays RpgUiCatalog.ButtonConfirm again - that 9-sliced " +
                             "green fill is what \"bleeds past its own button bounds\" in the capture, and it " +
                             "put a third chrome in a three-button row. One button language, emphasis only.");

            // Kit routing + the no-hand-rolled-uGUI law.
            if (code.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[source] the panel no longer goes through ElarionUiKit - the " +
                             "UiObsidianConformanceRegression hand-rolled-uGUI law");

            if (src.IndexOf('\0') >= 0)
                failures.Add("[source] HeroSkillTreePanelMvvm.cs contains an embedded NUL byte " +
                             "(mount-garble, CLAUDE.md Sec.0) - the compile gate rejects this");

            // =================================================================
            //  WO-1522 - NO AUTHORING NOTE MAY REACH A PLAYER-FACING STRING.
            // -----------------------------------------------------------------
            // Device frame Logs/device/screens/owner-screen-20260906-202355.png: the learn
            // dialog rendered, verbatim,
            //     "NO EFFECT YET - not implemented yet (data note: 'v2'). C"
            // - a data-entry comment, quoting the token an author typed into
            // hero-talents.json, truncated mid-sentence. It got there because
            // TalentEffectLiveness.HasRuntimeConsumer's diagnostic `why` out-string was
            // concatenated straight into HeroSkillTreeVM.SelectedNodeStateLine.
            //
            // RED PROOF (grep against the pre-fix tree, this file's own tokens):
            //   HeroSkillTreeVM.cs:259  why = "not implemented yet (data note: '" + token + "')";
            //   HeroSkillTreeVM.cs:1264 return "NO EFFECT YET - " + deadWhy + ". Costs " ...
            // The second line is what the case below catches: it is the CONCATENATION that
            // turns a diagnostic into copy. The first is legitimate - it stays a trace string -
            // so the case pins the SEAM, not the vocabulary.
            //
            // The word the player gets instead is COMING, composed by the VM with nothing but
            // the node's cost (HeroSkillTreeVM.DeadNodePlayerLine), and stated as a WORD on the
            // node plate too, because the owner is red/green colourblind and a hue cannot carry
            // "this grants nothing yet".
            //
            // MUTATION THAT REDS IT: restore the concatenation in SelectedNodeStateLine.
            Case5b_NoAuthoringNoteInPlayerCopy(failures, notes);

            notes.Add("source laws checked on " + ViewSrc);
        }

        /// <summary>WO-1522. No player-facing string in the Talents silo may be built from the
        /// liveness diagnostic, and no authoring vocabulary may be typed into one directly.</summary>
        private static void Case5b_NoAuthoringNoteInPlayerCopy(List<string> failures, List<string> notes)
        {
            string vmRaw = ReadText(VmSrc, failures, "[authoring-note]");
            if (vmRaw == null) return;
            string vm = StripComments(vmRaw);

            // 1. THE SEAM: the diagnostic out-string may never be concatenated into a returned
            //    sentence. `deadWhy` is the parameter name HasRuntimeConsumer's caller binds.
            //    Comments are already stripped above, and a FlowTrace statement is EXEMPT: routing
            //    the diagnostic to a trace line is the sanctioned home (WO-1522 says "route it to
            //    FlowTrace only"), so matching one there fails the very fix it is pinning.
            foreach (var probe in new[] { @"""[^""\n]*""\s*\+\s*deadWhy", @"deadWhy\s*\+\s*""", "NO EFFECT YET" })
            {
                foreach (Match m in Regex.Matches(vm, probe))
                {
                    if (IsDiagnosticOnly(vm, m.Index)) continue;
                    failures.Add("[authoring-note] the liveness diagnostic is being concatenated into player copy again " +
                                 "(WO-1522: the learn dialog read \"NO EFFECT YET - not implemented yet (data note: " +
                                 "'v2'). C\" on the owner's device). The diagnostic belongs to FlowTrace; the player " +
                                 "sentence is composed by HeroSkillTreeVM.DeadNodePlayerLine and says COMING.");
                    break;
                }
            }

            // 2. THE VOCABULARY: these literals may appear ONLY inside the liveness detector's
            //    own token table / trace lines, never anywhere a label can read them. The
            //    NotWiredNoteTokens array is the one sanctioned home and is excluded by name.
            string vmOutsideTokens = Regex.Replace(vm,
                // Lazy [\s\S] rather than a negated-brace class: a bare closing brace inside a
                // character class unbalances the CLAUDE.md sec.1 brace count on this very file.
                @"NotWiredNoteTokens\s*=\s*\{[\s\S]*?\}", "NotWiredNoteTokens={}", RegexOptions.Singleline);
            foreach (var banned in new[] { "data note", "not implemented" })
            {
                foreach (Match m in Regex.Matches(vmOutsideTokens, "\"[^\"\\n]*\""))
                {
                    string literal = m.Value;
                    if (literal.IndexOf(banned, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    // A trace string, or the diagnostic `why` out-string itself, is fine - neither
                    // can reach a label now that the seam above is closed. Anything else is not.
                    if (IsDiagnosticOnly(vmOutsideTokens, m.Index)) continue;
                    failures.Add("[authoring-note] authoring vocabulary '" + banned + "' appears in a " +
                                 "non-trace string literal in " + VmSrc + " (" + literal + "). Player copy " +
                                 "never quotes the data file's private vocabulary.");
                }
            }

            // 3. THE VIEW: the panel must never type the note vocabulary at all.
            string viewRaw = ReadText(ViewSrc, failures, "[authoring-note]");
            if (viewRaw != null)
            {
                string view = StripComments(viewRaw);
                foreach (var banned in new[] { "data note", "not implemented", "NO EFFECT YET" })
                    if (view.IndexOf(banned, StringComparison.OrdinalIgnoreCase) >= 0)
                        failures.Add("[authoring-note] '" + banned + "' appears in " + ViewSrc +
                                     " - the View paints strings, so any authoring vocabulary here is on screen.");

                // The replacement cue has to actually exist, stated as a WORD (colourblind law).
                Law(failures, view, "\"COMING\"",
                    "the inert node no longer states COMING in words on its plate - an unimplemented " +
                    "perk would again be distinguishable only by a pip glyph");
            }

            Law(failures, vm, "DeadNodePlayerLine",
                "the VM no longer composes the dead-node sentence in one place; the liveness " +
                "diagnostic can leak back into the learn dialog");

            notes.Add("authoring-note lint checked on " + VmSrc + " + " + ViewSrc);
        }

        /// <summary>True when the string literal at <paramref name="index"/> belongs to a statement
        /// that can only ever be DIAGNOSTIC: a FlowTrace call, or the assignment of the liveness
        /// detector's own `why` out-parameter. Those two are the sanctioned homes for the data
        /// file's private vocabulary; every other literal is a candidate label.</summary>
        private static bool IsDiagnosticOnly(string code, int index)
        {
            int start = Math.Max(0, Math.Min(index, code.Length - 1));
            int back = Math.Max(0, start - 400);
            string window = code.Substring(back, Math.Max(0, start - back));
            int cut = window.LastIndexOf(';');
            string statement = cut >= 0 ? window.Substring(cut + 1) : window;
            return statement.IndexOf("FlowTrace.", StringComparison.Ordinal) >= 0
                || Regex.IsMatch(statement, @"\bwhy\s*=");
        }

        // =====================================================================
        //  CASE 6 - the SPEND POPUP: the sentence, the frame, the state colour
        //  (WO-1342, device capture 2026-09-03 Seeker 2670x1200, `Mend` tapped)
        // =====================================================================
        // The capture showed the dialog rendering
        //   "Unlocks Mend <em dash> a small self-heal (25 HP, 12s cd). Assignable to"
        // and then STOPPING, with no ellipsis, while the authored string in
        // hero-talents.json continues "... Assignable to the hot-swap bar." Three
        // separate, separately-pinnable facts came out of that one screenshot:
        //
        //   [ascii] the string shipped a real U+2014 EM DASH. Player-facing strings are
        //           ASCII-only (a device without the glyph draws tofu mid-sentence), and
        //           every talent description lives in ONE file, so the scan is that file
        //           (both the Resources and the StreamingAssets copy - they must stay
        //           byte-identical or the build and the editor disagree).
        //   [wrap]  the description was CULLED, not wrapped-and-shrunk: FitBlock already
        //           wraps, but its overflowMode is Truncate, which draws NO "...". So the
        //           guard is a HEIGHT budget - the band must seat a whole line box per
        //           line the longest authored sentence needs, plus one for the gold talent
        //           name RenderSpendPopup prepends.
        //   [frame] the ornate border did not enclose the modal (owner: "the frame around
        //           the modal"). Frame and plate are the SAME rect - one 9-sliced
        //           content-panel sprite on chrome.root, and the kit's
        //           ZoneBacking(layout.body) plate at a fraction of it - but
        //           content-panel.png's 96 px TOP slice is fully transparent (alpha bbox
        //           starts at row 94 of 941), so the gold edge paints ~96 units BELOW the
        //           rect top while Zone_Body's top sits only ~59 units below it. The plate
        //           overhung the border by ~45 device px, measured in the capture. The fix
        //           is an inset on the content layer, and THIS is the numeric pin on it.
        //   [hue]   the state line was ElarionUi.Affordable GREEN for every state, so a
        //           lock reason and "NO EFFECT YET" were painted in the affordable cue.
        //           The owner is red/green colourblind: state is carried by the WORD.
        private const string TalentsJsonStreaming = "Assets/StreamingAssets/Data/Canonical/hero-talents.json";

        /// <summary>Reference panel height at 2340x1080 (derived in the header: 0.90 of the
        /// post-scale canvas). The spend popup is anchored to the talent WORKSPACE, which is
        /// 0.10..0.84 of this, so the popup's own rect has to be replayed through it.</summary>
        private const float RefPanelHeightPx = 880.5f;
        /// <summary>Workspace band inside the panel (HeroSkillTreePanelMvvm.BuildChrome seats
        /// TalentWorkspace at 0.035..0.965 x 0.10..0.84 of chrome.content).</summary>
        private const float RefWorkspaceY0 = 0.10f, RefWorkspaceY1 = 0.84f;
        /// <summary>FrameCore's BODY zone, as it resolves AFTER BuildObsidianPanel's shared-Close
        /// band reservation: authored y 0.075..0.835, and the reservation raises the floor to
        /// footer.w + 0.015 = 0.417 on this canvas. Restated here as the numbers the popup band
        /// arithmetic is replayed against - a DEVICE/kit fact, to be re-derived if the frame's
        /// zones change, never relaxed.</summary>
        private const float RefPopupBodyY0 = 0.417f, RefPopupBodyY1 = 0.835f;

        private static void Case6_SpendPopup(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            if (view == null || kit == null)
            {
                failures.Add("[popup] " + ViewType + " / " + KitType + " not found - re-point this oracle");
                return;
            }

            // ── [ascii] no non-ASCII codepoint in ANY talent-tree player-facing string ──
            AsciiOnly(failures, TalentsJson);
            AsciiOnly(failures, TalentsJsonStreaming);
            string resJson = ReadText(TalentsJson, failures, "[popup]");
            string strJson = ReadText(TalentsJsonStreaming, failures, "[popup]");
            if (resJson != null && strJson != null && !string.Equals(resJson, strJson, StringComparison.Ordinal))
                failures.Add("[popup] the Resources and StreamingAssets copies of hero-talents.json have " +
                             "DIVERGED - the editor and the build would render different talent copy");

            // ── [frame] the painted border must enclose the content on all four edges ──
            float artTop = ConstFloat(view, "PopupFrameArtTopMarginPx", failures, "[popup]");
            float insetTop = ConstFloat(view, "PopupContentTopInsetPx", failures, "[popup]");
            float insetSide = ConstFloat(view, "PopupContentSideInsetPx", failures, "[popup]");
            float insetBottom = ConstFloat(view, "PopupContentBottomInsetPx", failures, "[popup]");
            if (artTop > 0f && insetTop < artTop)
                failures.Add("[popup] the spend popup's content layer is inset " + insetTop.ToString("F0") +
                             " px from the top but content-panel.png does not paint its border until " +
                             artTop.ToString("F0") + " px down (its 96 px top 9-slice is transparent) - the black " +
                             "ZoneBacking plate and the description overhang the gold frame, which is the owner's " +
                             "\"the frame around the modal\" (capture 2026-09-03: plate top y=472 vs frame top y=517)");
            if (insetSide <= 0f)
                failures.Add("[popup] PopupContentSideInsetPx is 0 - the content plate runs to the frame's outer " +
                             "edge and paints over its pilasters");
            if (insetBottom < 0f)
                failures.Add("[popup] PopupContentBottomInsetPx is negative - the footer would paint below the frame");

            // ── [wrap] the description band must seat every line the sentence needs ──
            float y0 = ConstFloat(view, "PopupAnchorY0", failures, "[popup]");
            float y1 = ConstFloat(view, "PopupAnchorY1", failures, "[popup]");
            float dy0 = ConstFloat(view, "PopupDescBandY0", failures, "[popup]");
            float dy1 = ConstFloat(view, "PopupDescBandY1", failures, "[popup]");
            float py0 = ConstFloat(view, "PopupPromptBandY0", failures, "[popup]");
            float py1 = ConstFloat(view, "PopupPromptBandY1", failures, "[popup]");
            float descMin = ConstFloat(view, "PopupDescFontMin", failures, "[popup]");
            float hardFloor = ConstFloat(FindType(ObsidianType), "FontHardFloor", failures, "[popup]");
            int minLines = (int)ConstFloat(view, "PopupDescMinLineBoxes", failures, "[popup]");
            if (y1 <= y0 || dy1 <= dy0 || descMin <= 0f || minLines <= 0) return;

            if (descMin < hardFloor)
                failures.Add("[popup] PopupDescFontMin " + descMin.ToString("F0") + " is below the kit " +
                             "FontHardFloor " + hardFloor.ToString("F0") + " - sub-legible phone text");
            if (py1 > dy0 + 0.0001f)
                failures.Add("[popup] the description band (" + dy0.ToString("F2") + ".." + dy1.ToString("F2") +
                             ") and the state band (" + py0.ToString("F2") + ".." + py1.ToString("F2") +
                             ") OVERLAP - the wrapped sentence would paint over the state line");

            float workspaceH = (RefWorkspaceY1 - RefWorkspaceY0) * RefPanelHeightPx;
            float popupH = (y1 - y0) * workspaceH;
            float contentH = popupH - insetTop - insetBottom;
            float bodyH = (RefPopupBodyY1 - RefPopupBodyY0) * contentH;
            float descH = (dy1 - dy0) * bodyH;
            float lineBox = descMin * LineBoxMul;
            int seats = (int)Math.Floor(descH / lineBox);
            notes.Add("popup desc band = " + descH.ToString("F0") + " px of a " + bodyH.ToString("F0") +
                      " px body (popup " + popupH.ToString("F0") + " px, inset " + insetTop.ToString("F0") +
                      ") = " + seats + " line boxes at " + descMin.ToString("F0") + " px");
            if (seats < minLines)
                failures.Add("[popup] the description band seats only " + seats + " line box(es) at the " +
                             descMin.ToString("F0") + " px floor but needs " + minLines +
                             " (one for the gold talent name RenderSpendPopup prepends, three for the sentence). " +
                             "FitBlock's overflowMode is Truncate, so the tail is CULLED WITH NO ELLIPSIS - " +
                             "exactly the capture's \"... Assignable to\" (WO-1342 defect a)");

            // The longest authored description must fit the band once WRAPPED - i.e. the
            // rendered length can equal the authored length, so a future one-line regression
            // (or a re-shrunk band) is caught by arithmetic and not by the owner's eyes.
            if (resJson != null)
            {
                string longest = "";
                foreach (Match m in Regex.Matches(resJson, "\"description\"\\s*:\\s*\"([^\"]*)\""))
                {
                    string s = m.Groups[1].Value;
                    if (s.Length > longest.Length) longest = s;
                }
                if (longest.Length > 0)
                {
                    // Body well width at the reference rect, minus the label's 0.06..0.94 inset.
                    float bodyW = RefBodyWidthPx * (0.965f - 0.055f) * 0.88f;
                    float perLine = Math.Max(1f, bodyW / (descMin * AvgAdvanceEm));
                    // +1 line for the prepended gold talent name.
                    int need = 1 + (int)Math.Ceiling(longest.Length / perLine);
                    notes.Add("longest authored description = " + longest.Length + " chars -> ~" + need +
                              " wrapped lines at " + descMin.ToString("F0") + " px");
                    if (need > seats)
                        failures.Add("[popup] the longest authored talent description (" + longest.Length +
                                     " chars) needs ~" + need + " wrapped lines but the band seats " + seats +
                                     " - part of the sentence would be culled silently");
                    if (minLines < need)
                        failures.Add("[popup] PopupDescMinLineBoxes (" + minLines + ") is below what the longest " +
                                     "authored description needs (~" + need + ") - the guard would certify a band " +
                                     "that still truncates");
                }
            }

            // ── [hue] the state line must not carry state in GREEN alone ──
            string src = ReadText(ViewSrc, failures, "[popup]");
            if (src != null)
            {
                string code = StripComments(src);
                var promptCall = Regex.Match(code,
                    @"_popupPrompt\s*=\s*ElarionUiKit\.Label\s*\([^;]*?;", RegexOptions.Singleline);
                if (!promptCall.Success)
                    failures.Add("[popup] the spend popup's state-line label is gone - the dialog would state " +
                                 "no cost, no lock reason and no NO-EFFECT-YET warning before a spend");
                else if (promptCall.Value.IndexOf("ElarionUi.Affordable", StringComparison.Ordinal) >= 0)
                    failures.Add("[popup] the spend popup's state line is built in ElarionUi.Affordable GREEN. " +
                                 "That ONE label carries EVERY state (Owned / Costs N Wisdom / Planned / " +
                                 "NO EFFECT YET / a lock reason) and its colour is set once at build, so green " +
                                 "is not distinguishing states - it paints an affordable cue over lock copy. " +
                                 "The owner is red/green colourblind: the WORD carries the state (COLOURBLIND LAW)");

                ConfirmEmphasisLaws(failures, notes, code);
            }
        }

        // =====================================================================
        //  CASE 6 (b) - the CONFIRM button's SELECTED/available emphasis
        //  (device capture 2026-09-03 shot4.png, owner: "see learn is selected
        //   but that coloring")
        // =====================================================================
        // LEARN rendered as a flat opaque gold rectangle that covered its own ornate
        // button-normal-empty plate, swamped its label (the word read as darker gold
        // THROUGH the fill) and spilled 5 px past the button rect toward the popup
        // frame's right edge. The cause was not a ColorBlock: "ConfirmRing" was a single
        // full-rect Image with ElarionUiKit.ApplyRounded - a FILLED rounded 9-slice, not
        // an outline - and SetAsFirstSibling does NOT put a child behind its parent's own
        // Graphic (a uGUI parent draws before all of its children), so the "ring" painted
        // over the plate instead of behind it.
        //
        // Three separately-pinnable laws come out of that:
        //   [no-fill]   the emphasis may not be a full-rect graphic on the button. A
        //               sprite/fill spanning 0..1 is a slab over the frame art whatever
        //               colour it is - this is the mutation that reproduces the capture.
        //   [no-spill]  the emphasis rect may not GROW outside the button (the -5/+5 that
        //               reached the frame edge). Inset, never grown.
        //   [not-hue]   selection must be carried by SHAPE + LUMINANCE, so all four edges
        //               must be drawn. A single accent bar, or a recoloured fill, would be
        //               hue-only and invisible to the owner (red/green colourblind).
        private static void ConfirmEmphasisLaws(List<string> failures, List<string> notes, string code)
        {
            // Start at the CONSTRUCTION, not at the name literal: the typeof(Image) that
            // caused the slab sits to the LEFT of the string, and a block that began at the
            // literal would read as clean while the fill was still being created.
            var ctor = Regex.Match(code, @"new\s+GameObject\s*\(\s*""ConfirmRing""");
            int start = ctor.Success ? ctor.Index : code.IndexOf("\"ConfirmRing\"", StringComparison.Ordinal);
            int end = start < 0 ? -1 : code.IndexOf("_popupConfirmRing = ring;", start, StringComparison.Ordinal);
            if (start < 0 || end < 0)
            {
                failures.Add("[popup] the confirm button's emphasis marker (\"ConfirmRing\") is gone from " +
                             "BuildSpendPopup - the affirmative action would look identical to CANCEL, and " +
                             "WO-1340's FTUE highlights resolve that rect BY NAME");
                return;
            }
            string block = code.Substring(start, end - start);

            if (Regex.IsMatch(block, @"new\s+GameObject\s*\(\s*""ConfirmRing""\s*,\s*typeof\s*\(\s*Image\s*\)"))
                failures.Add("[popup] \"ConfirmRing\" is built WITH an Image again. A uGUI parent's own Graphic " +
                             "draws BEFORE its children, so a full-rect Image on the confirm button paints a flat " +
                             "slab OVER MedievalUiSkin's ornate plate no matter what SetAsFirstSibling does - that " +
                             "is verbatim the 2026-09-03 capture (\"see learn is selected but that coloring\": " +
                             "LEARN as an opaque yellow rectangle with its label showing through). Emphasis is an " +
                             "OUTLINE of edge bars, never a fill");
            if (block.IndexOf("ApplyRounded", StringComparison.Ordinal) >= 0)
                failures.Add("[popup] the confirm emphasis calls ElarionUiKit.ApplyRounded - that is the shared " +
                             "FILLED rounded 9-slice, not an outline sprite, so it covers the whole button face");
            if (Regex.IsMatch(block, @"\.sprite\s*=") ||
                Regex.IsMatch(block, @"ringImg\s*\.\s*color"))
                failures.Add("[popup] the confirm emphasis assigns a sprite/fill colour to a full-rect graphic " +
                             "again - the frame art must stay visible in EVERY state");

            // [no-spill] - the container rect must not be grown outward.
            var minM = Regex.Match(block, @"ringRt\.offsetMin\s*=\s*([^;]+);");
            var maxM = Regex.Match(block, @"ringRt\.offsetMax\s*=\s*([^;]+);");
            if (!minM.Success || !maxM.Success)
                failures.Add("[popup] the confirm emphasis rect no longer states its own offsets - the guard " +
                             "against growing it past the button (and past the popup frame) is blind");
            else if (minM.Groups[1].Value.IndexOf("Vector2.zero", StringComparison.Ordinal) < 0 ||
                     maxM.Groups[1].Value.IndexOf("Vector2.zero", StringComparison.Ordinal) < 0)
                failures.Add("[popup] the confirm emphasis rect is GROWN outside the button again " +
                             "(offsetMin " + minM.Groups[1].Value.Trim() + " / offsetMax " +
                             maxM.Groups[1].Value.Trim() + "). It must match the button rect exactly and inset " +
                             "its bars inward; the retired -5/+5 overlay is what spilled past the popup frame's " +
                             "right edge in the capture - emphasis is drawn INSIDE the control, never past it");

            // [not-hue] - a border on all four edges: shape + luminance, not colour.
            int bars = Regex.Matches(block, @"ConfirmEdgeBar\s*\(").Count;
            if (bars < 4)
                failures.Add("[popup] the confirm emphasis draws " + bars + " edge bar(s), not 4. Selection has " +
                             "to be carried by SHAPE and BRIGHTNESS - a partial accent (or a recoloured fill) is " +
                             "a hue-only cue and the owner is red/green colourblind (COLOURBLIND LAW)");

            Type view = FindType(ViewType);
            float thick = ConstFloat(view, "ConfirmOutlinePx", failures, "[popup]");
            float inset = ConstFloat(view, "ConfirmOutlineInsetPx", failures, "[popup]");
            if (thick <= 1f)
                failures.Add("[popup] ConfirmOutlinePx is " + thick.ToString("F1") + " - a sub-2 px border is not " +
                             "a visible emphasis at 2670x1200 on a phone");
            if (inset <= 0f)
                failures.Add("[popup] ConfirmOutlineInsetPx is " + inset.ToString("F1") + " - a zero/negative " +
                             "inset puts the outline on (or past) the button's own edge, which is how the retired " +
                             "overlay reached the popup frame");
            notes.Add("confirm emphasis = " + bars + " edge bars, " + thick.ToString("F0") + " px inset " +
                      inset.ToString("F0") + " px, no full-rect fill");
        }

        // =====================================================================
        //  CASE 7 - the QUICK-SWAP RAIL: slots and hint are DISJOINT bands
        //  (WO-1401; Builds/ui-capture.log 2026-09-05 05:13)
        // =====================================================================
        // The capture harness measured, at 1920x1080 / 2340x1080 / 2670x1200 alike:
        //   BUTTON OVER TEXT 'TalentWorkspace/QuickSwapRail/ObsBtn_1|2|3' (y 0..112 of the
        //   rail) covers 'QuickSwapRail/QuickSwapHint' (y 103..132 of the rail) by 112x9.
        // The slot LayoutElement is MinTouchPx (112) and the HorizontalLayoutGroup seats it
        // LOWER, so the slots own the rail's bottom 112 px at every aspect; the hint was a
        // FRACTION of the rail (Label y0 0.78 x 132 = 103), so its bottom 9 px sat inside
        // the slot band at every aspect. A fraction band next to a fixed band is the same
        // failure the header of this file was written about.
        //
        // This case is NOT a re-derivation of the numbers: it builds THE VIEW'S OWN rail
        // (HeroSkillTreePanelMvvm.BuildQuickSwapRailHost) into an edit-mode canvas at the
        // reference body, seats three slot buttons through the view's own sizing helper
        // (ApplyQuickSwapSlotSize), settles layout and measures the resolved rects with the
        // same LayoutOracle the capture harness runs. If the view's construction drifts, this
        // measures the drift; a copy of the construction would only measure the copy.
        //
        //   (a) LayoutOracle.Audit on the probe reports no BUTTON OVER TEXT / BUTTONS OVERLAP.
        //   (b) every slot rect clears the hint rect by >= BandGapPx, every slot is >= MinTouch
        //       tall, and the hint sits inside the rail (no fraction, no escape).
        //   (c) a label seated at the graph well's floor (GraphWellFloorPx) clears the rail.
        //   (d) the constants add up: rail = slot + gap + hint; well floor = rail bottom +
        //       rail + gap; hint band >= one FontFloor line box; slot band >= MinTouchPx.
        //
        // RED FIRST: in BuildQuickSwapRailHost change the hint pin to
        //   PinBandFromTop((RectTransform)hint.transform, QuickSwapHintBandPx + BandGapPx, QuickSwapHintBandPx)
        // and (a) + (b) fail with the hint measured inside the slot band.
        private static void Case7_QuickSwapRail(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            if (view == null || kit == null)
            {
                failures.Add("[rail] " + ViewType + " / " + KitType + " not found - re-point this oracle");
                return;
            }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[rail]");
            float fontFloor = ConstFloat(FindType(ObsidianType), "FontFloor", failures, "[rail]");
            float bandGap = ConstFloat(view, "BandGapPx", failures, "[rail]");
            float slotBand = ConstFloat(view, "QuickSwapSlotBandPx", failures, "[rail]");
            float hintBand = ConstFloat(view, "QuickSwapHintBandPx", failures, "[rail]");
            float railH = ConstFloat(view, "QuickSwapRailPx", failures, "[rail]");
            float railBottom = ConstFloat(view, "QuickSwapRailBottomPx", failures, "[rail]");
            float wellFloor = ConstFloat(view, "GraphWellFloorPx", failures, "[rail]");
            if (minTouch <= 0f || fontFloor <= 0f || slotBand <= 0f || hintBand <= 0f || railH <= 0f) return;

            // (d) the arithmetic the construction is built from.
            if (slotBand < minTouch)
                failures.Add("[rail] QuickSwapSlotBandPx=" + slotBand + " is below MinTouchPx=" + minTouch +
                             " - ClampMinTouch would grow every slot past its band into the hint");
            float lineBox = fontFloor * LineBoxMul;
            if (hintBand < lineBox)
                failures.Add("[rail] QuickSwapHintBandPx=" + hintBand + " is shorter than one FontFloor line box (" +
                             lineBox.ToString("F1") + ") - the hint sentence would cull or ellipsize");
            if (railH < slotBand + bandGap + hintBand - 0.01f)
                failures.Add("[rail] QuickSwapRailPx=" + railH + " < slot " + slotBand + " + gap " + bandGap +
                             " + hint " + hintBand + " - the two bands cannot both fit inside the rail");
            if (wellFloor < railBottom + railH + bandGap - 0.01f)
                failures.Add("[rail] GraphWellFloorPx=" + wellFloor + " < rail bottom " + railBottom + " + rail " +
                             railH + " + gap " + bandGap + " - a node plate (a Button) can sit on the hint");

            // (a)(b)(c) the geometry that ships, measured.
            GameObject canvasGo = null;
            try
            {
                const int probeW = 2340, probeH = 1080;
                canvasGo = RailProbeCanvas(probeW, probeH);
                var root = canvasGo.GetComponent<RectTransform>();

                var workspaceGo = new GameObject("TalentWorkspace", typeof(RectTransform));
                workspaceGo.transform.SetParent(canvasGo.transform, false);
                var workspace = workspaceGo.GetComponent<RectTransform>();
                workspace.anchorMin = workspace.anchorMax = workspace.pivot = new Vector2(0.5f, 0.5f);
                workspace.sizeDelta = new Vector2(RefBodyWidthPx, RefBodyHeightPx);
                workspace.anchoredPosition = Vector2.zero;

                // The view's own graph well seat (BuildChrome: BandHost 0..1, floor = GraphWellFloorPx),
                // carrying a probe label flush on its floor - the nearest text the rail could touch.
                var wellGo = new GameObject("GraphWell", typeof(RectTransform));
                wellGo.transform.SetParent(workspaceGo.transform, false);
                var well = wellGo.GetComponent<RectTransform>();
                well.anchorMin = Vector2.zero; well.anchorMax = Vector2.one;
                well.offsetMin = new Vector2(0f, wellFloor); well.offsetMax = Vector2.zero;
                var floorText = ElarionUiKit.Label(wellGo.transform, "NODE NAME AT THE WELL FLOOR", 0f, 0f,
                    Color.white, 18, TextAlignmentOptions.Center, 0.2f, 0.8f);
                floorText.gameObject.name = "WellFloorProbe";
                var floorRt = (RectTransform)floorText.transform;
                floorRt.offsetMin = new Vector2(floorRt.offsetMin.x, 0f);
                floorRt.offsetMax = new Vector2(floorRt.offsetMax.x, 24f);

                // THE VIEW'S OWN BUILDER - not a copy of it.
                TextMeshProUGUI hint;
                RectTransform rail = HeroSkillTreePanelMvvm.BuildQuickSwapRailHost(workspaceGo.transform, out hint);
                if (rail == null || hint == null)
                {
                    failures.Add("[rail] BuildQuickSwapRailHost returned no rail/hint - the builder the capture " +
                                 "measures is gone; re-point this oracle rather than deleting it");
                    return;
                }

                // Three slots, sized by THE VIEW'S OWN helper. A real Button with a visible graphic
                // (LayoutOracle.HasVisibleGraphic) and the production touch guard, so the oracle
                // sees exactly what it sees on the captured panel.
                var slots = new List<Button>(3);
                for (int i = 1; i <= 3; i++)
                {
                    var go = new GameObject("ObsBtn_" + i, typeof(RectTransform), typeof(CanvasRenderer),
                                            typeof(Image), typeof(Button));
                    go.transform.SetParent(rail, false);
                    var img = go.GetComponent<Image>();
                    img.color = Color.white;
                    var btn = go.GetComponent<Button>();
                    btn.targetGraphic = img;
                    ElarionUiKit.ClampMinTouch(btn);
                    HeroSkillTreePanelMvvm.ApplyQuickSwapSlotSize(go);
                    var face = ElarionUiKit.Label(go.transform, i + "\nEMPTY", 0f, 1f, Color.white, 18,
                        TextAlignmentOptions.Center, 0f, 1f);
                    face.gameObject.name = "Face";
                    slots.Add(btn);
                }

                SettleProbe(canvasGo);

                // (a) the harness's oracle, verbatim.
                var found = LayoutOracle.Audit(canvasGo, "SkillsRailProbe", probeW, probeH);
                for (int i = 0; i < found.Count; i++)
                {
                    if (found[i].Kind == LayoutOracle.FindingKind.ButtonOverText ||
                        found[i].Kind == LayoutOracle.FindingKind.ButtonsOverlap)
                        failures.Add("[rail] " + found[i].Message);
                }

                // (b) the bands, by the numbers the oracle resolves.
                Rect railR, hintR, floorR;
                if (!LayoutOracle.TryRectInRoot(rail, root, out railR) ||
                    !LayoutOracle.TryRectInRoot((RectTransform)hint.transform, root, out hintR) ||
                    !LayoutOracle.TryRectInRoot(floorRt, root, out floorR))
                {
                    failures.Add("[rail] could not resolve the rail / hint / well-floor rects in root space");
                    return;
                }
                if (Mathf.Abs(railR.height - railH) > 0.5f)
                    failures.Add("[rail] the rail resolves " + railR.height.ToString("0.#") + " px tall but " +
                                 "QuickSwapRailPx says " + railH + " - the host is no longer pinned to the constant");
                if (hintR.yMax > railR.yMax + 0.5f || hintR.yMin < railR.yMin - 0.5f)
                    failures.Add("[rail] the hint (y " + hintR.yMin.ToString("0.#") + ".." + hintR.yMax.ToString("0.#") +
                                 ") escapes the rail (y " + railR.yMin.ToString("0.#") + ".." + railR.yMax.ToString("0.#") +
                                 ") - it is not pinned inside its own band");
                if (hintR.height < lineBox - 0.5f)
                    failures.Add("[rail] the hint resolves " + hintR.height.ToString("0.#") + " px tall, under one " +
                                 "FontFloor line box (" + lineBox.ToString("F1") + ")");

                var texts = canvasGo.GetComponentsInChildren<TMP_Text>(false);
                float worstClear = float.MaxValue;
                for (int s = 0; s < slots.Count; s++)
                {
                    Rect br;
                    if (!LayoutOracle.TryRectInRoot((RectTransform)slots[s].transform, root, out br))
                    {
                        failures.Add("[rail] slot " + (s + 1) + " has no resolvable rect");
                        continue;
                    }
                    if (br.height < minTouch - 0.5f || br.width < minTouch - 0.5f)
                        failures.Add("[rail] slot " + (s + 1) + " resolves " + br.width.ToString("0.#") + "x" +
                                     br.height.ToString("0.#") + " - under MinTouchPx " + minTouch);
                    float clear = hintR.yMin - br.yMax;
                    if (clear < worstClear) worstClear = clear;
                    if (clear < bandGap - 0.5f)
                        failures.Add("[rail] slot " + (s + 1) + " top y=" + br.yMax.ToString("0.#") + " vs hint bottom y=" +
                                     hintR.yMin.ToString("0.#") + " - clearance " + clear.ToString("0.#") +
                                     " px is under BandGapPx " + bandGap + " (the capture's 112x9 overlap is clearance -9)");

                    // The brief's law, verbatim: a slot rect intersects NO text rect it does not own.
                    for (int t = 0; t < texts.Length; t++)
                    {
                        var text = texts[t];
                        if (text == null || !text.gameObject.activeInHierarchy) continue;
                        if (LayoutOracle.IsDescendantOf(text.transform, slots[s].transform)) continue;
                        Rect tr;
                        if (!LayoutOracle.TryRectInRoot(text.rectTransform, root, out tr)) continue;
                        float ow, oh;
                        if (LayoutOracle.Overlaps(br, tr, LayoutOracle.OverlapPadPx, out ow, out oh))
                            failures.Add("[rail] slot " + (s + 1) + " covers foreign text '" + text.gameObject.name +
                                         "' by " + ow.ToString("0.#") + "x" + oh.ToString("0.#") + " ref px");
                    }
                }

                // (c) the graph well floor clears the rail by the gap.
                float wellClear = floorR.yMin - railR.yMax;
                if (wellClear < bandGap - 0.5f)
                    failures.Add("[rail] the graph well's floor label (y " + floorR.yMin.ToString("0.#") +
                                 ") clears the rail top (y " + railR.yMax.ToString("0.#") + ") by only " +
                                 wellClear.ToString("0.#") + " px - under BandGapPx " + bandGap +
                                 "; a node plate can paint over the hint");

                notes.Add("rail probe @" + probeW + "x" + probeH + ": rail " + railR.height.ToString("0.#") +
                          " px, hint " + hintR.height.ToString("0.#") + " px, slot-to-hint clearance " +
                          (worstClear == float.MaxValue ? "n/a" : worstClear.ToString("0.#")) +
                          " px, well-floor clearance " + wellClear.ToString("0.#") + " px, oracle findings " +
                          found.Count);
            }
            catch (Exception ex)
            {
                failures.Add("[rail] probe THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        /// <summary>An edit-mode WORLD-SPACE canvas sized like the game's 1080x1920 match-0.5
        /// scaler resolves at the given screen (the same arithmetic UiTouchClampRegression uses;
        /// an overlay canvas in an edit-mode call reports the editor's own window and every
        /// measurement is fiction).</summary>
        private static GameObject RailProbeCanvas(int w, int h)
        {
            const float refW = 1080f, refH = 1920f, match = 0.5f;
            float logW = Mathf.Log(w / refW, 2f);
            float logH = Mathf.Log(h / refH, 2f);
            float sf = Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
            if (!(sf > 0f) || float.IsNaN(sf) || float.IsInfinity(sf)) sf = 1f;

            var go = new GameObject("~SkillsRailProbe", typeof(RectTransform), typeof(Canvas));
            go.hideFlags = HideFlags.HideAndDontSave;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w / sf, h / sf);
            rt.position = Vector3.zero;
            rt.localScale = Vector3.one;
            return go;
        }

        /// <summary>Two synchronous layout passes, matching the capture harness - one is not always
        /// enough for a layout group's children to settle.</summary>
        private static void SettleProbe(GameObject canvas)
        {
            var rt = canvas.GetComponent<RectTransform>();
            for (int pass = 0; pass < 2; pass++)
            {
                Canvas.ForceUpdateCanvases();
                if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        /// <summary>Fail on any codepoint above ASCII, naming file:line:col and the codepoint -
        /// a tofu box mid-sentence on a device without the glyph.</summary>
        private static void AsciiOnly(List<string> failures, string path)
        {
            string text = ReadText(path, failures, "[popup]");
            if (text == null) return;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            int hits = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                for (int c = 0; c < lines[i].Length; c++)
                {
                    if (lines[i][c] <= 127) continue;
                    hits++;
                    if (hits <= 6)
                        failures.Add("[popup] non-ASCII U+" + ((int)lines[i][c]).ToString("X4") + " in " +
                                     path + ":" + (i + 1) + ":" + (c + 1) +
                                     " - player-facing talent copy is ASCII-only (a device without the glyph " +
                                     "draws a tofu box mid-sentence)");
                }
            }
            // The JSON may also carry the codepoint as a \uXXXX ESCAPE, which is the same
            // glyph on screen and is how the em dash hid from a raw-byte scan.
            foreach (Match m in Regex.Matches(text, @"\\u([0-9a-fA-F]{4})"))
            {
                int cp = Convert.ToInt32(m.Groups[1].Value, 16);
                if (cp <= 127) continue;
                hits++;
                int line = 1;
                for (int k = 0; k < m.Index; k++) if (text[k] == '\n') line++;
                if (hits <= 6)
                    failures.Add("[popup] non-ASCII escape \\u" + m.Groups[1].Value + " (U+" +
                                 cp.ToString("X4") + ") in " + path + ":" + line +
                                 " - an escaped em dash renders the SAME tofu box as a raw one");
            }
            if (hits > 6)
                failures.Add("[popup] ... and " + (hits - 6) + " more non-ASCII codepoints in " + path);
        }

        private static void Law(List<string> failures, string code, string token, string why)
        {
            if (code.IndexOf(token, StringComparison.Ordinal) < 0)
                failures.Add($"[source] '{token}' is gone from HeroSkillTreePanelMvvm - {why}");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        private static float ParseF(string s)
        {
            float f;
            return float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out f) ? f : -1f;
        }

        /// <summary>Read a public const float/int by reflection (no asmdef reference needed).</summary>
        private static float ConstFloat(Type t, string name, List<string> failures, string tag)
        {
            if (t == null) return 0f;
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add($"{tag} {t.Name}.{name} does not exist - the layout constant this oracle pins " +
                             "was renamed or removed; re-point it rather than deleting the guard");
                return 0f;
            }
            object v = f.GetValue(null);
            if (v is float fv) return fv;
            if (v is int iv) return iv;
            if (v is double dv) return (float)dv;
            failures.Add($"{tag} {t.Name}.{name} is not a numeric constant (got {(v == null ? "null" : v.GetType().Name)})");
            return 0f;
        }

        // =====================================================================
        //  CASE 8 - WO-1522 [elevation]: the screen has DEPTH, and it survives greyscale
        // =====================================================================
        /// <summary>
        /// Owner words: "loadout screen feels very flat". It was literally flat - the graph
        /// viewport painted a near-black slab with NO edge, the loadout slots sat straight on the
        /// panel frame with nothing behind them, and the wisdom plate was the only bordered
        /// surface. Three tiers of content on one apparent plane.
        ///
        /// THE OWNER IS RED/GREEN COLOURBLIND, so this case does NOT check that the tiers are
        /// different colours - it checks that they are different GREYS. Every assertion below is
        /// on Rec.709 luma, which is exactly what a greyscale capture of the screen shows: if
        /// these pass, depth is readable with hue stripped entirely; if they were hue-only, they
        /// would collapse here.
        ///
        /// It is a FIXTURE case: the TWO authored surfaces (WellSurface, RaisedSurface) are public
        /// statics on the View, read live by reflection, so it measures the shipped values rather
        /// than a copy of them - and a future edit that quietly moves the two rungs together fails
        /// the gate instead of shipping a flat screen a second time. There is deliberately no
        /// third constant: the rung between them is the frame's own textured centre, and a plate
        /// authored for it would be invisible under the opaque graph viewport.
        /// </summary>
        private static void Case8_Elevation(List<string> failures, List<string> notes)
        {
            const string tag = "[elevation]";
            Type view = FindType(ViewType);
            if (view == null) { failures.Add(tag + " HeroSkillTreePanelMvvm type not found"); return; }

            Color well = ColorConst(view, "WellSurface", failures, tag);
            Color raised = ColorConst(view, "RaisedSurface", failures, tag);
            float step = ConstFloat(view, "ElevationLumaStep", failures, tag);
            if (step <= 0f) return;

            float lWell = Luma(well), lRaised = Luma(raised);

            // THE LADDER: recessed well BELOW the frame's own textured centre BELOW the raised
            // shelf. The middle rung is the frame art, not an authored constant (see the View's
            // note - a plate there is either invisible under the graph viewport or it re-covers
            // what MedievalUiSkin.ApplyShell deliberately uncovers), so what is decidable here is
            // the gap ACROSS it: two rungs, hence twice the adjacent-step floor.
            if (lWell >= lRaised)
                failures.Add(tag + " the recessed well (" + lWell.ToString("0.000") + " luma) is not " +
                             "darker than the raised shelf (" + lRaised.ToString("0.000") + ") - a ladder " +
                             "whose rungs are out of order reads as noise, not depth");
            if (lRaised - lWell < step * 2f)
                failures.Add(tag + " the graph well and the loadout shelf are only " +
                             (lRaised - lWell).ToString("0.000") + " luma apart (floor " +
                             (step * 2f).ToString("0.000") + ") - in a greyscale capture they are one " +
                             "surface, which is the owner's 'very flat' verbatim");

            // Fully opaque, or the tier below shows through and the step measured above is a lie.
            foreach (var pair in new[] { new { N = "WellSurface", C = well },
                                         new { N = "RaisedSurface", C = raised } })
                if (pair.C.a < 0.99f)
                    failures.Add(tag + " " + pair.N + " has alpha " + pair.C.a.ToString("0.00") +
                                 " - a translucent tier blends with the one under it and the measured " +
                                 "luma step is not the step the player sees");

            // SIZE is the second depth channel (WO-1021): the focus plate must be materially
            // larger than a normal one, or selection is carried by tint alone.
            float focus = ConstFloat(view, "NodeFocusPx", failures, tag);
            float normal = ConstFloat(view, "NodeSizePx", failures, tag);
            if (focus > 0f && normal > 0f && focus < normal * 1.10f)
                failures.Add(tag + " the focus plate (" + focus.ToString("F0") + " px) is less than 10% " +
                             "larger than a normal one (" + normal.ToString("F0") + " px) - SIZE is the " +
                             "one selection cue that survives greyscale and it has stopped carrying");

            // ...and the surfaces have to actually be BUILT, through the one builder, with the
            // plate and the bezel kept as SEPARATE images. Collapsing them is verbatim the
            // WO-1515 tan-slab defect: card-frame-empty has a transparent centre.
            string src = ReadText(ViewSrc, failures, tag);
            if (src == null) return;
            string code = StripComments(src);
            foreach (string call in new[] { "\"GraphWellBezel\"",
                                            "\"QuickSwapShelfPlate\"", "\"QuickSwapShelfBezel\"" })
                if (code.IndexOf(call) < 0)
                    failures.Add(tag + " the elevation surface " + call + " is no longer built - the " +
                                 "screen loses a tier and goes back toward flat");
            if (code.IndexOf("BuildElevationPlate(") < 0)
                failures.Add(tag + " BuildElevationPlate is gone - the surfaces would be authored " +
                             "several different ways, which is how they drift back together");
            // A plate under the graph viewport is INVISIBLE (the viewport is opaque and fills the
            // same rect). Building one is dead paint that would make the ladder look authored
            // while the player still sees one plane.
            if (Regex.IsMatch(code, @"BuildElevationPlate\s*\(\s*graphWell\s*,[^;]*bezel\s*:\s*false"))
                failures.Add(tag + " a FILL plate is built under the graph viewport - the viewport is " +
                             "opaque and fills the same rect, so that paint is 100% occluded and the " +
                             "tier it claims to add is not on screen");
            if (Regex.IsMatch(code, @"BuildElevationPlate\s*\([^;]*bezel\s*:\s*true[^;]*RaisedSurface"))
                failures.Add(tag + " a bezel and a fill are being asked of ONE image - card-frame-empty " +
                             "has a transparent centre, so that leaves no surface at all (WO-1515)");
            if (Regex.IsMatch(code, @"vImg\.color\s*=\s*new\s+Color"))
                failures.Add(tag + " the graph viewport re-hardcodes its own colour instead of reading " +
                             "WellSurface - a second copy of a tier is how the ladder goes stale");

            // TWO authored rungs, with the frame's own textured centre between them - so the
            // note reports the gap ACROSS that middle rung against twice the adjacent-step floor.
            notes.Add("elevation ladder well " + lWell.ToString("0.000") + " < raised " +
                      lRaised.ToString("0.000") + " (gap " + (lRaised - lWell).ToString("0.000") +
                      ", floor " + (step * 2f).ToString("0.000") + ")");
        }

        // =====================================================================
        //  CASE 9 - WO-1601 [bezel-art]: THE FRAME SPRITE'S ART MUST REACH ITS OWN
        //  9-SLICE BORDER, or a "bezel" paints a band across whatever it frames.
        // =====================================================================
        /// <summary>
        /// Owner frame Logs/device/seeker-shots/Screenshot_20260907-132616.png (Seeker
        /// 2026.09.07.359651): a full-width ornate gold band drawn straight across the middle of
        /// the talent tree, over the ARCANE BOLT node and its connectors - and, on the same
        /// screen, the loadout shelf carrying NO gold edge at all.
        ///
        /// ONE CAUSE, and it is a property of the ART, not of any rect:
        ///   card-frame-empty.png is 1774x887, spriteBorder {96,96,96,96}, and its PAINTED alpha
        ///   bounding box is rows 165..713 / cols 16..1757. The top 96-row slice (0..96) and the
        ///   bottom one (791..887) are therefore 100% TRANSPARENT. Image.Type.Sliced pins those
        ///   two empty strips to the rect's edges and STRETCHES the middle, so the only rows that
        ///   carry the horizontal rails land at (165-96)/695 = 9.9% and (713-96)/695 = 88.8% INTO
        ///   the rect. Over the graph well (device 306..812 px) that predicts rails at 452 / 663;
        ///   the owner's frame measures 449 / 655. Over the 172 px shelf bezel the 192 px of
        ///   border does not fit, Unity shrinks both strips, the middle slice collapses to zero
        ///   and the rails are not drawn at all - the missing shelf edge, same cause.
        ///
        /// No rect can fix it (the rail is 96 + 0.099*(H-192) from the top for every H), so the
        /// law is on the SPRITE: whatever <c>HeroSkillTreePanelMvvm.BezelFrameResource</c> names
        /// must carry painted pixels inside all four of its border strips. This case loads that
        /// PNG's bytes and re-measures them, so it tracks the art rather than a copy of it.
        ///
        /// RED FIRST: point BezelFrameResource back at
        /// "UI/ElarionMedieval/frames/card-frame-empty" and the TOP and BOTTOM strip assertions
        /// fail with 165 and 713 named.
        /// </summary>
        private static void Case9_BezelArt(List<string> failures, List<string> notes)
        {
            const string tag = "[bezel-art]";
            Type view = FindType(ViewType);
            if (view == null) { failures.Add(tag + " HeroSkillTreePanelMvvm type not found"); return; }

            string res = ConstString(view, "BezelFrameResource", failures, tag);
            if (string.IsNullOrEmpty(res)) return;

            // The builder must go through the const, and must not have kept the retired sprite.
            string src = ReadText(ViewSrc, failures, tag);
            if (src == null) return;
            string code = StripComments(src);
            if (code.IndexOf("Resources.Load<Sprite>(BezelFrameResource)", StringComparison.Ordinal) < 0)
                failures.Add(tag + " BuildElevationPlate no longer loads BezelFrameResource - a literal " +
                             "sprite path there is a second copy of this decision, and this case would be " +
                             "measuring the wrong file");
            if (code.IndexOf("card-frame-empty", StringComparison.Ordinal) >= 0)
                failures.Add(tag + " the Skills panel names card-frame-empty again - its 96 px slice strips " +
                             "are fully transparent (painted rows 165..713 of 887), so slicing it paints a " +
                             "band ACROSS whatever it is supposed to frame (WO-1601)");

            string png = "Assets/Resources/" + res + ".png";
            if (!File.Exists(png))
            {
                failures.Add(tag + " " + png + " does not exist - the bezel would silently ship with no edge");
                return;
            }

            Vector4 border = SpriteBorderFromMeta(png + ".meta", failures, tag);
            if (border.x <= 0f && border.y <= 0f && border.z <= 0f && border.w <= 0f)
            {
                failures.Add(tag + " " + res + " has NO 9-slice border - Image.Type.Sliced then simply " +
                             "stretches the whole sprite, so the frame art distorts with the rect");
                return;
            }

            int w, h, r0, r1, c0, c1;
            if (!PaintedAlphaBox(png, out w, out h, out r0, out r1, out c0, out c1))
            {
                failures.Add(tag + " could not decode " + png + " - the art oracle cannot certify the bezel");
                return;
            }

            // Unity's border is {left, bottom, right, top}; PNG row 0 is the TOP row.
            int bl = Mathf.RoundToInt(border.x), bb = Mathf.RoundToInt(border.y);
            int br = Mathf.RoundToInt(border.z), bt = Mathf.RoundToInt(border.w);
            if (r0 >= bt)
                failures.Add(tag + " " + res + ": the TOP " + bt + " px slice is empty - painted art starts " +
                             "at row " + r0 + ". Sliced pins that empty strip to the rect's top edge and the " +
                             "first painted row lands " + ((r0 - bt) * 100 / Mathf.Max(1, h - bt - bb)) +
                             "% INTO the rect - a band across the content, never an edge (WO-1601)");
            if (r1 < h - bb)
                failures.Add(tag + " " + res + ": the BOTTOM " + bb + " px slice is empty - painted art ends " +
                             "at row " + r1 + " of " + h + ". The lower rail draws inside the rect instead of " +
                             "on its floor");
            if (c0 >= bl)
                failures.Add(tag + " " + res + ": the LEFT " + bl + " px slice is empty (art starts at col " +
                             c0 + ") - the left rail draws inside the rect");
            if (c1 < w - br)
                failures.Add(tag + " " + res + ": the RIGHT " + br + " px slice is empty (art ends at col " +
                             c1 + " of " + w + ") - the right rail draws inside the rect");

            // ...and the two rects this sprite is actually asked to frame. The shelf bezel is the
            // SHORT one and it is where the border-shrink collapse bit: state its numbers.
            float railPx = ConstFloat(view, "QuickSwapRailPx", failures, tag);
            float inflate = ConstFloat(view, "WellBezelInsetPx", failures, tag);
            float shelfBezelH = railPx + inflate * 2f;
            if (bt + bb > shelfBezelH + 0.5f)
                notes.Add("bezel border " + (bt + bb) + " px > shelf bezel height " +
                          shelfBezelH.ToString("F0") + " px, so Unity shrinks both strips to " +
                          (shelfBezelH * 0.5f).ToString("F0") + " px each and the middle slice collapses - " +
                          "legal ONLY because " + res + " paints inside its border strips (rows " + r0 +
                          ".." + r1 + "), which is exactly what this case pins");

            notes.Add("bezel art " + res + " " + w + "x" + h + " border L" + bl + " B" + bb + " R" + br +
                      " T" + bt + ", painted rows " + r0 + ".." + r1 + " cols " + c0 + ".." + c1);
        }

        /// <summary>The painted (alpha &gt; 8) bounding box of a PNG on disk, decoded from its
        /// BYTES so it never depends on an importer setting (isReadable) that a future re-import
        /// could flip. Rows are top-down, matching the file.</summary>
        private static bool PaintedAlphaBox(string path, out int w, out int h,
                                            out int rowMin, out int rowMax, out int colMin, out int colMax)
        {
            w = h = 0; rowMin = colMin = int.MaxValue; rowMax = colMax = -1;
            Texture2D tex = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes, false)) return false;
                w = tex.width; h = tex.height;
                var px = tex.GetPixels32();
                // GetPixels32 is BOTTOM-UP; convert to file rows so the numbers in the failure
                // text match what an image tool reports.
                for (int y = 0; y < h; y++)
                {
                    int fileRow = h - 1 - y;
                    for (int x = 0; x < w; x++)
                    {
                        if (px[y * w + x].a <= 8) continue;
                        if (fileRow < rowMin) rowMin = fileRow;
                        if (fileRow > rowMax) rowMax = fileRow;
                        if (x < colMin) colMin = x;
                        if (x > colMax) colMax = x;
                    }
                }
                return rowMax >= 0;
            }
            catch { return false; }
            finally { if (tex != null) UnityEngine.Object.DestroyImmediate(tex); }
        }

        /// <summary>spriteBorder {left, bottom, right, top} read straight out of the .meta - the
        /// authority on how Unity will slice the art.</summary>
        private static Vector4 SpriteBorderFromMeta(string metaPath, List<string> failures, string tag)
        {
            try
            {
                if (!File.Exists(metaPath))
                {
                    failures.Add(tag + " no .meta at " + metaPath + " - the slice border is unknown");
                    return Vector4.zero;
                }
                var m = Regex.Match(File.ReadAllText(metaPath),
                    @"spriteBorder:\s*\{x:\s*([\d.]+),\s*y:\s*([\d.]+),\s*z:\s*([\d.]+),\s*w:\s*([\d.]+)\}");
                if (!m.Success) return Vector4.zero;
                return new Vector4(
                    float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
            }
            catch { return Vector4.zero; }
        }

        // =====================================================================
        //  CASE 10 - WO-1601 [fit]: THE SOLVED BOARD FITS THE WELL IT IS DRAWN IN
        // =====================================================================
        /// <summary>
        /// Second half of the owner's frame: the 0/1 medallions at BOTH ends of the tree are cut
        /// in half by the RectMask2D. Measured against the shipped solver at the device's well
        /// (see the consts below): a Lv2 board solves seven columns at MinColPitchPx into a
        /// 1915 px content rect inside a 1705 px well, so the seventh plate spans 1654..1790 and
        /// the mask cuts it at 1705. The second row is halved the same way.
        ///
        /// The fix is a uniform fit scale on the content (never a re-solve - SolveGraphLatticePx
        /// owns pitch and [grid] pins its laws), floored where a plate would stop being a legal
        /// tap target. This case runs BOTH shipped functions and measures the rest window.
        ///
        /// RED FIRST: delete the _graphContent.localScale assignment in RebuildTracks (or make
        /// ResolveGraphFitScale return 1f) and (b) fails with the right column 85 px outside the
        /// rest window and the bottom row 85 px below it.
        /// </summary>
        private static void Case10_GraphFit(List<string> failures, List<string> notes)
        {
            const string tag = "[fit]";
            Type view = FindType(ViewType);
            if (view == null) { failures.Add(tag + " HeroSkillTreePanelMvvm type not found"); return; }

            float nodeSize = ConstFloat(view, "NodeSizePx", failures, tag);
            float minTouch = ConstFloat(FindType(KitType), "MinTouchPx", failures, tag);
            float floorScale = ConstFloat(view, "MinGraphFitScale", failures, tag);
            float graphPad = ConstFloat(view, "GraphPadPx", failures, tag);
            float rankBand = ConstFloat(view, "RankBandPx", failures, tag);
            float clearPx = ConstFloat(view, "PlateClearPx", failures, tag);
            if (nodeSize <= 0f || minTouch <= 0f || floorScale <= 0f) return;

            // (a) THE LAW ITSELF. The floor is a DERIVATION, not a taste: below it a node plate
            //     renders under the kit touch floor and the board stops being tappable.
            if (Mathf.Abs(floorScale - minTouch / nodeSize) > 0.0005f)
                failures.Add(tag + " MinGraphFitScale=" + floorScale.ToString("F4") + " is not MinTouchPx/" +
                             "NodeSizePx (" + (minTouch / nodeSize).ToString("F4") + ") - the fit floor has " +
                             "become a literal and can drift below the tap-target law");

            var fitFn = view.GetMethod("ResolveGraphFitScale", BindingFlags.Public | BindingFlags.Static);
            var solver = view.GetMethod("SolveGraphLatticePx", BindingFlags.Public | BindingFlags.Static);
            if (fitFn == null || solver == null)
            {
                failures.Add(tag + " ResolveGraphFitScale / SolveGraphLatticePx are not both public statics - " +
                             "this oracle can no longer run what ships; re-point it rather than deleting it");
                return;
            }
            Func<float, float, float, float, float> fit = (cw, ch, ww, wh) =>
                (float)fitFn.Invoke(null, new object[] { cw, ch, ww, wh });

            if (Mathf.Abs(fit(100f, 100f, 4000f, 4000f) - 1f) > 0.0001f)
                failures.Add(tag + " a board SMALLER than the well is being scaled - the solver already " +
                             "centres it; blowing it up would break the pitch law it just solved");
            if (fit(4000f, 4000f, 100f, 100f) < floorScale - 0.0001f)
                failures.Add(tag + " the fit scale went below MinGraphFitScale - a node plate would render " +
                             "under MinTouchPx and stop being a legal tap target");

            // The View must actually APPLY it, and clamp the scroll against the SCALED window.
            string code = StripComments(ReadText(ViewSrc, failures, tag) ?? "");
            if (!Regex.IsMatch(code, @"_graphContent\.localScale\s*=\s*new\s+Vector3\s*\(\s*fitScale"))
                failures.Add(tag + " RebuildTracks no longer scales the graph content by the fit - the " +
                             "board is back to overflowing its mask at the rest position (WO-1601)");
            if (!Regex.IsMatch(code, @"contentH\s*-\s*viewH") || !Regex.IsMatch(code, @"contentW\s*-\s*viewW"))
                failures.Add(tag + " the scroll clamp no longer uses the SCALED rest window (viewW/viewH) - " +
                             "at fit < 1 it would let the board scroll past its own end");

            // (b) THE MEASURED BOARD. A Lv2 hero, the tree at its smallest population and the
            //     shape the owner's frame carries: seven lanes, two ranks. Norm magnitudes are
            //     what the solver clusters on (ColClusterNorm/RowClusterNorm = 0.055), so 1/6
            //     apart is seven distinct columns and 0/1 is two distinct rows.
            const int lanes = 7;
            var norms = new float[lanes * 2];
            for (int i = 0; i < lanes; i++)
            {
                norms[i * 2] = i / (float)(lanes - 1);
                norms[i * 2 + 1] = i % 2;
            }
            float boxW = DeviceWellWpx - graphPad * 2f;
            float boxH = DeviceWellHpx - graphPad * 2f - rankBand;
            float[] px;
            try { px = (float[])solver.Invoke(null, new object[] { norms, boxW, boxH }); }
            catch (Exception ex)
            {
                failures.Add(tag + " SolveGraphLatticePx THREW " + ex.GetType().Name + ": " + ex.Message);
                return;
            }
            if (px == null || px.Length != norms.Length) { failures.Add(tag + " solver returned nothing"); return; }

            float maxX = 0f, maxY = 0f, minX = float.MaxValue, minY = float.MaxValue;
            for (int i = 0; i < lanes; i++)
            {
                maxX = Mathf.Max(maxX, px[i * 2]); minX = Mathf.Min(minX, px[i * 2]);
                maxY = Mathf.Max(maxY, px[i * 2 + 1]); minY = Mathf.Min(minY, px[i * 2 + 1]);
            }
            // RebuildTracks' own content sizing, replayed.
            float contentW = Mathf.Max(maxX + minX, maxX + clearPx * 0.5f + graphPad);
            float contentH = Mathf.Max(maxY + minY, maxY + clearPx * 0.5f + graphPad + rankBand);
            float s = fit(contentW, contentH, DeviceWellWpx, DeviceWellHpx);
            float viewW = DeviceWellWpx / s, viewH = DeviceWellHpx / s;
            float half = nodeSize * 0.5f;

            float worstRight = 0f, worstBottom = 0f, rawRight = 0f, rawBottom = 0f;
            for (int i = 0; i < lanes; i++)
            {
                worstRight = Mathf.Max(worstRight, (px[i * 2] + half) - viewW);
                worstBottom = Mathf.Max(worstBottom, (px[i * 2 + 1] + half) - viewH);
                rawRight = Mathf.Max(rawRight, (px[i * 2] + half) - DeviceWellWpx);
                rawBottom = Mathf.Max(rawBottom, (px[i * 2 + 1] + half) - DeviceWellHpx);
            }

            // The frame's LEFT/RIGHT defect - "0/1 medallions half outside the panel" - must be
            // gone outright. Nothing is allowed to hang off the side of the board at rest.
            if (worstRight > 0.5f)
                failures.Add(tag + " after the fit a plate still overhangs the rest window by " +
                             worstRight.ToString("F0") + " px on the RIGHT (well " + DeviceWellWpx.ToString("F0") +
                             " px, board " + contentW.ToString("F0") + " px, scale " + s.ToString("F3") +
                             ") - that is the sliced medallion in Screenshot_20260907-132616.png");

            // Vertically the touch floor can still bind (two ranks at MinRowPitchPx need more
            // room than this well has). What may NEVER happen again is a plate reading as HALF a
            // medallion: the overhang has to stay under half a plate, so what the mask cuts is
            // the plate's margin, not its face.
            if (worstBottom > half - 0.5f)
                failures.Add(tag + " after the fit the bottom rank overhangs the rest window by " +
                             worstBottom.ToString("F0") + " px - half a plate is " + half.ToString("F0") +
                             " px, so the player is looking at a sliced medallion again, not a scroll cue");

            // ...and the fit has to be doing WORK. Equal numbers mean the scale was never applied.
            if (worstRight >= rawRight - 0.5f && worstBottom >= rawBottom - 0.5f && (rawRight > 0.5f || rawBottom > 0.5f))
                failures.Add(tag + " the fit changed nothing: overhang is still " + rawRight.ToString("F0") +
                             "/" + rawBottom.ToString("F0") + " px - ResolveGraphFitScale is being computed " +
                             "and thrown away");

            notes.Add("fit scale " + s.ToString("F3") + " (floor " + floorScale.ToString("F3") + ") on a " +
                      contentW.ToString("F0") + "x" + contentH.ToString("F0") + " Lv2 board in a " +
                      DeviceWellWpx.ToString("F0") + "x" + DeviceWellHpx.ToString("F0") + " well: overhang " +
                      rawRight.ToString("F0") + "/" + rawBottom.ToString("F0") + " px -> " +
                      worstRight.ToString("F0") + "/" + worstBottom.ToString("F0") + " px");
        }

        /// <summary>Rec.709 relative luminance - what a greyscale capture of the screen shows.
        /// The ONLY depth measure this owner can read, so it is the one the case asserts on.</summary>
        private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Read a public static string const. A missing one is a FAILURE, never "".</summary>
        private static string ConstString(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add(tag + " " + t.Name + "." + name + " does not exist - re-point this oracle " +
                             "rather than deleting the guard");
                return null;
            }
            return f.GetValue(null) as string;
        }

        /// <summary>Read a public static Color field. A MISSING surface is a FAILURE, never a
        /// default: a black default would silently satisfy the "recessed" half of the ladder.</summary>
        private static Color ColorConst(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add(tag + " " + t.Name + "." + name + " does not exist - the elevation ladder " +
                             "this case pins was renamed or removed; re-point it rather than deleting the guard");
                return new Color(0f, 0f, 0f, 0f);
            }
            return f.GetValue(null) is Color c ? c : new Color(0f, 0f, 0f, 0f);
        }

        private static string ReadText(string path, List<string> failures, string tag)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add($"{tag} source not found: {path}");
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add($"{tag} could not read {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Blank out // and block comments so a lesson written in prose (which deliberately
        /// quotes the retired shapes) can never fail a source law.</summary>
        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); }
                catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
