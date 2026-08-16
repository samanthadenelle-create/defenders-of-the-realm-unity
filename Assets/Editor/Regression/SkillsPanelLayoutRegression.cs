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
//                   the pad is at least half a node plate, the section band owns a
//                   whole reserved row, and the fixed px-per-unit lattice keeps the
//                   TIGHTEST authored column/row gap in hero-talents.json clear of a
//                   node plate (read from the canonical json, not assumed).
//   4 [truncation]  at the reference resolution no label is forced to ellipsize: the
//                   longest UNBREAKABLE word in abilities.json fits an ability tile
//                   at the kit FontFloor, and the longest name in hero-talents.json
//                   fits the detail column.
//   5 [source]      the laws that make the regression unreachable: RectMask2D clip,
//                   top-left content pivot, the fixed-pixel band pins, the reserved
//                   section row, no 1/n fraction slicing, no green ButtonConfirm
//                   overlay (the fill that bled), and no embedded NUL.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.SkillsPanelLayoutRegression.RunAll
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class SkillsPanelLayoutRegression
    {
        private const string ViewSrc = "Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs";
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
                         "the node graph is padded + clipped on a fixed-pixel lattice, and no catalog " +
                         "label is forced to ellipsize at 2340x1080" + noteStr;
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
        //  CASE 3 - the grid can never exceed / be sliced by its container
        // =====================================================================
        private static void Case3_GridContainment(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[grid]");
            if (!L.Ok) return;

            // The content rect must pad by at least HALF A NODE PLATE, or the extreme authored
            // rows/columns hang outside the content and get sliced mid-plate at the mask edge -
            // which is exactly "the grid overflows its container on BOTH sides".
            if (L.GraphPad < L.NodeSize * 0.5f)
                failures.Add($"[grid] GraphPadPx={L.GraphPad} is less than half a node plate " +
                             $"({L.NodeSize * 0.5f}) - the first and last authored rows/columns extend past the " +
                             "scroll content and are cut mid-plate at the mask edge");

            // Node-to-node clearance on the REAL authored lattice. Read the tightest gap that
            // actually exists in hero-talents.json rather than trusting a hand-picked scale.
            float gapX, gapY;
            int rows;
            float bandY = ConstFloat(FindType(ViewType), "SectionBandY", failures, "[grid]");
            if (!TightestAuthoredGaps(failures, bandY, out gapX, out gapY, out rows)) return;

            float pitchX = gapX * L.UnitW;
            if (pitchX < L.NodeSize)
                failures.Add($"[grid] the tightest authored COLUMN gap in hero-talents.json ({gapX:F3}) resolves " +
                             $"to {pitchX:F0} px at GraphUnitWpx={L.UnitW}, under a {L.NodeSize} px node plate - " +
                             "adjacent nodes would overlap each other inside the scroll content");

            // gapY excludes the ONE row pair that straddles the section band: RebuildGraph pushes
            // that pair apart to SectionClearPx (case 1 pins that floor). Every other row pair has
            // to clear a node plate on the raw lattice.
            float pitchY = gapY * L.UnitH;
            if (pitchY < L.NodeSize)
                failures.Add($"[grid] the tightest non-band authored ROW gap ({gapY:F3}) resolves to " +
                             $"{pitchY:F0} px at GraphUnitHpx={L.UnitH}, under a {L.NodeSize} px node plate - " +
                             "stacked node rows would overlap inside the scroll content");

            notes.Add($"lattice: tightest authored gaps x={gapX:F3} ({pitchX:F0}px), y={gapY:F3} ({pitchY:F0}px) " +
                      $"over {rows} authored rows");
        }

        /// <summary>Smallest gap between two DISTINCT authored x values that share a row, and the
        /// smallest gap between two distinct authored y values THAT DO NOT STRADDLE the section
        /// band (that pair is rescued by the reserved row). Parsed straight out of the canonical
        /// json so the oracle tracks the data, not a snapshot of it.</summary>
        private static bool TightestAuthoredGaps(List<string> failures, float bandY,
                                                 out float gapX, out float gapY, out int rowCount)
        {
            gapX = 1f; gapY = 1f; rowCount = 0;
            string src = ReadText(TalentsJson, failures, "[grid]");
            if (src == null) return false;

            // Node objects carry "x": <f>, "y": <f> (in either order in the authored file).
            var rowsByY = new Dictionary<string, List<float>>();
            var ys = new List<float>();
            var mx = Regex.Matches(src, "\"x\"\\s*:\\s*(-?[0-9.]+)\\s*,\\s*\"y\"\\s*:\\s*(-?[0-9.]+)");
            if (mx.Count == 0)
                mx = Regex.Matches(src, "\"y\"\\s*:\\s*(-?[0-9.]+)\\s*,\\s*\"x\"\\s*:\\s*(-?[0-9.]+)");
            if (mx.Count == 0)
            {
                failures.Add("[grid] no authored x/y pairs found in " + TalentsJson + " - the node lattice " +
                             "cannot be checked; the authoring shape changed and this oracle is now blind");
                return false;
            }
            foreach (Match m in mx)
            {
                float a = ParseF(m.Groups[1].Value), b = ParseF(m.Groups[2].Value);
                float x = a, y = b;
                if (x < 0f || y < 0f) continue;   // -1 = unset/auto, laid out at the centre
                string key = y.ToString("F4");
                if (!rowsByY.TryGetValue(key, out var list)) { list = new List<float>(); rowsByY[key] = list; ys.Add(y); }
                list.Add(x);
            }
            rowCount = rowsByY.Count;
            if (rowCount == 0) { failures.Add("[grid] every authored node is unset (-1) - nothing to check"); return false; }

            foreach (var kv in rowsByY)
            {
                var xs = kv.Value;
                xs.Sort();
                for (int i = 1; i < xs.Count; i++)
                {
                    float d = xs[i] - xs[i - 1];
                    if (d > 0.0001f && d < gapX) gapX = d;
                }
            }
            ys.Sort();
            for (int i = 1; i < ys.Count; i++)
            {
                float lo = ys[i - 1], hi = ys[i];
                if (lo <= bandY && hi > bandY) continue;   // the pair the reserved band row splits
                float d = hi - lo;
                if (d > 0.0001f && d < gapY) gapY = d;
            }
            if (gapX >= 1f) gapX = 1f;   // single-column tree: no constraint
            if (gapY >= 1f) gapY = 1f;
            return true;
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
            const int slots = 4;   // the quick-swap bar is 1..4
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
