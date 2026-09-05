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
//
// Standalone: run-unity-method DeNelle.Editor.Regression.SkillsPanelLayoutRegression.RunAll
// =============================================================================
using System;
using System.Collections.Generic;
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
                         "quick-swap slots and their hint are disjoint bands measured on the real builder (WO-1401)" + noteStr;
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
            Law(failures, code, "SelectedSuggestedSlot",
                "the learn-to-assign flow no longer names its destination/replacement slot");
            Law(failures, code, "UI/ElarionMedieval/frames/circular-bezel-four-point",
                "skill nodes no longer use the canonical black-iron/four-point-gold medallion");
            Law(failures, code, "fillGo.AddComponent<Mask>()",
                "skill artwork is no longer clipped into the circular medallion well");
            Law(failures, code, "_wisdomLabel.text = \"WISDOM  \" + _vm.RemainingWisdom",
                "the top-right talent balance no longer names and reads the Wisdom currency it spends");
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
                Law(failures, vmCode, "SelectedSuggestedSlot",
                    "the VM no longer exposes the explicit assign/replace destination");
                if (!Regex.IsMatch(vmCode,
                        "bool\\s+active\\s*=.*AbilityIdOf\\s*\\(\\s*learned\\s*\\).*if\\s*\\(\\s*!active\\s*\\)\\s*_selectedId\\s*=\\s*\"\"",
                        RegexOptions.Singleline))
                    failures.Add("[source-flow] SpendSelected no longer preserves selection for a newly learned " +
                                 "active - the player must find and tap the node again before assigning it");
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

            notes.Add("source laws checked on " + ViewSrc);
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
