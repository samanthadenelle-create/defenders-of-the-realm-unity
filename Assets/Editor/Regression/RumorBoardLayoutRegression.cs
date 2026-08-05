// =============================================================================
// RumorBoardLayoutRegression [rumor-board-layout] (WO-866) - Brom's rumor board
// can never clip a filter tab or cull the detail body again.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT BROKE (Seeker capture 2026-08-04, docs/ui-review/2026-08-04-seeker/
// 04-rumor-board.png). Two defects, one class - a band sized against something
// other than a fixed pixel budget - and BOTH are reproducible on paper:
//
//   THE CAPTURE GEOMETRY (derived once, here, so every number below is checkable):
//     CanvasScaler = 1080x1920, MatchWidthOrHeight 0.5, so at 2340x1080 the scale
//     is 2^((log2(2340/1080) + log2(1080/1920))/2) = 1.104 and the canvas resolves
//     to 2120x978 REFERENCE px. The modal is anchored (0.08,0.1)-(0.92,0.9), so the
//     panel is 1780x783 ref px. ElarionUiKit's close-band reservation raises the
//     FrameQuest body zones' floor to y=0.3137 (close band top 0.050 + 132/783 =
//     0.2187, footer re-seated to 0.2337-0.2987, body floor 0.2987+0.015). With the
//     measured FrameQuest zones - bodyLeft x 0.035-0.495 / top 0.858 and bodyRight
//     x 0.505-0.966 / top 0.760 - that gives:
//         LIST well   819 x 426 ref px      DETAIL well  821 x 349 ref px
//     At 1920x1080 the same math gives 742 x 484 and 744 x 399.
//
//   1 [tab-band]  The tab strip parented into chrome.layout.body, which on FrameQuest
//                 is the LIST WELL ONLY, and each of the five chips carried a
//                 HARDCODED preferredWidth of 220. 5*220 + 4*10 = 1140 ref px of
//                 chips inside a 0.03-0.97 strip that is 770 ref px wide, so the
//                 strip's RectMask2D cut chip 4 at 770 - 80 px into a 220 px chip,
//                 i.e. ~36% of "Gear". That is EXACTLY the lone "G" in the capture.
//                 (The detail pane never touched it: the mask edge is at frame x
//                 0.481, the detail pane starts at 0.505.)
//   2 [detail]    The detail stack reserved 148 px of top bands + 212 px of bottom
//                 bands = 360 px inside a 349 px well, so the body label's rect
//                 resolved to -11 px and TMP culled the quest text WHOLE - which is
//                 why the capture shows chips, a title and CTAs but no tale. At
//                 1920x1080 the same stack computes to +39 px and squeaks out one
//                 line, which is why RunCaptureHeadless never caught it.
//
// This oracle is a CHEAP structural guard, not a pixel test - it pins the properties
// that make both bugs impossible, all headlessly decidable:
//
//   1 [tab-band]  RumorBoardPanel's public layout constants (read by REFLECTION so
//                 this file needs no UnityEngine.UI / TMP asmdef reference): the band
//                 is at/above the kit touch floor, a chip's FLOOR width is the touch
//                 floor, TabCount matches RumorBoardVM.TabKeys.Length, and the whole
//                 row at its floor width FITS the measured list well at BOTH capture
//                 aspects. That is the "every tab fully visible" assertion.
//   2 [detail]    Every band is at least one TMP line box at the font it renders, and
//                 DetailFixedStackPx + DetailBodyMinPx FITS the measured pane height.
//                 That is the assertion the -11 px body would have failed.
//   3 [no-overlap] Source law on RumorBoardPanel.cs: the tab band takes its X bounds
//                 from the LIST zone and the detail pane parents to the RIGHT zone, so
//                 they are horizontally disjoint rects in different columns - the
//                 detail pane cannot cross the tab band by construction. Plus: no
//                 hardcoded chip width, chips flex-fill at a touch-floor minWidth, and
//                 the KEEP (selected tab = a leading "*" AND an underline bar, never a
//                 colour highlight - the owner is red/green colourblind) still exists.
//
// A live "no two rects overlap" assertion needs a canvas at both capture aspects;
// that stays the job of RunCaptureHeadless + eyes-on. This oracle catches the
// REGRESSION (someone re-hardcodes a chip width, shortens a band, or moves the tab
// band back into the shared body zone), which is the failure mode that recurs.
//
// Markers: RUMOR_BOARD_LAYOUT_OK / RUMOR_BOARD_LAYOUT_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.RumorBoardLayoutRegression.RunAll
// Registered in DataRegression.RunAll as the "rumor-board-layout suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RumorBoardLayoutRegression
    {
        private const string ViewSrc = "Assets/_Modules/Village/Hero/RumorBoardPanel.cs";

        private const string ViewType = "DeNelle.Village.Hero.RumorBoardPanel";
        private const string VmType = "DeNelle.Village.Hero.RumorBoardVM";
        private const string KitType = "DeNelle.Core.UI.ElarionUiKit";
        private const string UiType = "DeNelle.Core.UI.ElarionUi";

        /// <summary>The TMP line box multiplier the bands are budgeted from (~1.25em).</summary>
        private const float LineBoxMul = 1.25f;

        // The MEASURED FrameQuest wells at the two capture aspects (derivation in the
        // header). The list well width is what the tab row has to fit inside; the list
        // well HEIGHT is also the detail pane's height, because WO-866 top-aligns the
        // detail pane to the list well's top line.
        private const float ListWellW_2340 = 819f;
        private const float ListWellH_2340 = 426f;
        private const float ListWellW_1920 = 742f;
        private const float ListWellH_1920 = 484f;

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RUMOR_BOARD_LAYOUT_OK - " + reason);
            else Debug.LogError("RUMOR_BOARD_LAYOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "tab-band", () => Case1_TabBandFitsTheWell(failures, notes));
                Case(failures, "detail", () => Case2_DetailStackFitsThePane(failures, notes));
                Case(failures, "no-overlap", () => Case3_SourceLaws(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "RUMOR BOARD LAYOUT OK - all " + TabCountExpected() + " filter tabs fit the " +
                         "measured list well at their touch-floor width on both capture aspects, the tab band " +
                         "is X-bounded by the list column (the detail pane cannot cross it), every band is a " +
                         "whole TMP line box in FIXED reference pixels, the detail stack + a two-line body fits " +
                         "the pane, and the selected tab still marks itself with '*' + underline (not colour)" + noteStr;
                return true;
            }
            reason = "rumor-board-layout FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        private static int TabCountExpected()
        {
            Type view = FindType(ViewType);
            if (view == null) return 0;
            var f = view.GetField("TabCount", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            object v = f != null ? f.GetValue(null) : null;
            return v is int i ? i : 0;
        }

        // =====================================================================
        //  CASE 1 - the tab row fits the well it lives in (the clipped "G")
        // =====================================================================
        private static void Case1_TabBandFitsTheWell(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            if (view == null)
            {
                failures.Add("[tab-band] " + ViewType + " not found - the rumor board view was renamed or " +
                             "removed; re-point this oracle (it is the only guard on the tab band budget)");
                return;
            }
            if (kit == null) { failures.Add("[tab-band] " + KitType + " not found - cannot read the kit touch floor"); return; }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[tab-band]");
            if (minTouch <= 0f) return;

            float bandPx = ConstFloat(view, "TabBandPx", failures, "[tab-band]");
            float bandGap = ConstFloat(view, "TabBandGapPx", failures, "[tab-band]");
            float chipMin = ConstFloat(view, "TabChipMinPx", failures, "[tab-band]");
            float rowMin = ConstFloat(view, "TabRowMinWidthPx", failures, "[tab-band]");
            float listTop = ConstFloat(view, "ListTopInsetPx", failures, "[tab-band]");
            int tabCount = ConstInt(view, "TabCount", failures, "[tab-band]");
            if (bandPx <= 0f || chipMin <= 0f || rowMin <= 0f || listTop <= 0f || tabCount <= 0) return;

            if (bandPx < minTouch)
                failures.Add("[tab-band] RumorBoardPanel.TabBandPx=" + bandPx + " is BELOW the kit touch floor " +
                             minTouch + " - the filter tabs would be untappable on a phone");

            if (chipMin < minTouch)
                failures.Add("[tab-band] RumorBoardPanel.TabChipMinPx=" + chipMin + " is BELOW the kit touch floor " +
                             minTouch + " - shrinking chips is never how five tabs are made to 'fit' (WO-852 ruling)");

            if (listTop < bandPx + bandGap)
                failures.Add("[tab-band] ListTopInsetPx=" + listTop + " is less than the band it has to clear (" +
                             bandPx + " + " + bandGap + ") - the list would render UNDER the tab band");

            // THE assertion the shipped bug would have failed: the whole row, at its FLOOR
            // width, inside the MEASURED list well - at both aspects RunCaptureHeadless and
            // the Seeker actually render.
            if (rowMin > ListWellW_2340)
                failures.Add("[tab-band] TabRowMinWidthPx=" + rowMin + " does not fit the measured list well at " +
                             "2340x1080 (" + ListWellW_2340 + " ref px) - a chip would be clipped by the band's " +
                             "RectMask2D, which is the WO-866 bug verbatim (the lone 'G' of 'Gear')");
            if (rowMin > ListWellW_1920)
                failures.Add("[tab-band] TabRowMinWidthPx=" + rowMin + " does not fit the measured list well at " +
                             "1920x1080 (" + ListWellW_1920 + " ref px - the headless capture aspect)");

            // The tab count the width budget was derived for must be the tab count the VM ships.
            Type vm = FindType(VmType);
            if (vm != null)
            {
                var keys = vm.GetField("TabKeys", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var arr = keys != null ? keys.GetValue(null) as Array : null;
                if (arr != null && arr.Length != tabCount)
                    failures.Add("[tab-band] RumorBoardVM.TabKeys has " + arr.Length + " tabs but " +
                                 "RumorBoardPanel.TabCount=" + tabCount + " - TabRowMinWidthPx was derived for " +
                                 tabCount + " and no longer proves the row fits");
                else if (arr != null)
                    notes.Add(arr.Length + " tabs, row floor " + rowMin + "px in a " + ListWellW_2340 + "px well");
            }
            else
            {
                notes.Add("RumorBoardVM not loaded - tab count cross-check skipped");
            }
        }

        // =====================================================================
        //  CASE 2 - every band is a whole line box, and the stack fits the pane
        // =====================================================================
        private static void Case2_DetailStackFitsThePane(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            Type ui = FindType(UiType);
            if (view == null || kit == null) { failures.Add("[detail] view/kit type not found"); return; }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[detail]");
            float fontBody = ui != null ? ConstFloat(ui, "FontBody", failures, "[detail]") : 50f;
            float fontLabel = ui != null ? ConstFloat(ui, "FontLabel", failures, "[detail]") : 40f;
            float fontMicro = ui != null ? ConstFloat(ui, "FontMicro", failures, "[detail]") : 32f;
            if (minTouch <= 0f || fontBody <= 0f || fontLabel <= 0f || fontMicro <= 0f) return;

            float bodyLine = fontBody * LineBoxMul;    // 62.5
            float labelLine = fontLabel * LineBoxMul;  // 50
            float microLine = fontMicro * LineBoxMul;  // 40

            float fixedStack = ConstFloat(view, "DetailFixedStackPx", failures, "[detail]");
            float bodyMin = ConstFloat(view, "DetailBodyMinPx", failures, "[detail]");
            float titlePx = ConstFloat(view, "DetailTitlePx", failures, "[detail]");
            float chipRowPx = ConstFloat(view, "DetailChipRowPx", failures, "[detail]");
            float ctaPx = ConstFloat(view, "DetailCtaPx", failures, "[detail]");
            float cardPx = ConstFloat(view, "CardHeightPx", failures, "[detail]");
            float statusPx = ConstFloat(view, "StatusBandPx", failures, "[detail]");
            float sectionPx = ConstFloat(view, "SectionBandPx", failures, "[detail]");
            float flavorPx = ConstFloat(view, "FlavorBandPx", failures, "[detail]");
            float listBottom = ConstFloat(view, "ListBottomInsetPx", failures, "[detail]");
            if (fixedStack <= 0f || bodyMin <= 0f || titlePx <= 0f || chipRowPx <= 0f || ctaPx <= 0f) return;

            // THE assertion the -11px body would have failed, at both capture aspects.
            if (fixedStack + bodyMin > ListWellH_2340)
                failures.Add("[detail] DetailFixedStackPx(" + fixedStack + ") + DetailBodyMinPx(" + bodyMin +
                             ") = " + (fixedStack + bodyMin) + " exceeds the detail pane at 2340x1080 (" +
                             ListWellH_2340 + " ref px) - the body band goes NEGATIVE and TMP culls the quest " +
                             "text whole (the WO-866 bug: the shipped stack asked 360 of a 349px well)");
            if (fixedStack + bodyMin > ListWellH_1920)
                failures.Add("[detail] the detail stack does not fit at 1920x1080 (" + ListWellH_1920 + " ref px)");

            if (titlePx < bodyLine)
                failures.Add("[detail] DetailTitlePx=" + titlePx + " is shorter than one FontBody line box (" +
                             bodyLine + ") - the quest title would be culled");
            if (chipRowPx < microLine)
                failures.Add("[detail] DetailChipRowPx=" + chipRowPx + " is shorter than one FontMicro line box (" +
                             microLine + ") - the tag/reward chips would render as empty outlines (the exact " +
                             "'empty gold chip' symptom from the 2026-08-02 capture)");
            if (ctaPx < minTouch)
                failures.Add("[detail] DetailCtaPx=" + ctaPx + " is below the kit touch floor " + minTouch);
            if (bodyMin < 2f * labelLine)
                failures.Add("[detail] DetailBodyMinPx=" + bodyMin + " is under two FontLabel line boxes (" +
                             (2f * labelLine) + ") - a one-line minimum is not a body, it is a caption");

            // The list column's own bands.
            if (cardPx < minTouch)
                failures.Add("[detail] CardHeightPx=" + cardPx + " is below the kit touch floor " + minTouch +
                             " - a card IS the select target");
            if (cardPx < bodyLine + microLine)
                failures.Add("[detail] CardHeightPx=" + cardPx + " cannot seat its two lines (title " + bodyLine +
                             " + hook " + microLine + " = " + (bodyLine + microLine) + ") - one of them clips");
            if (statusPx < microLine)
                failures.Add("[detail] StatusBandPx=" + statusPx + " is under one FontMicro line box (" + microLine + ")");
            if (sectionPx < labelLine)
                failures.Add("[detail] SectionBandPx=" + sectionPx + " is under one FontLabel line box (" + labelLine + ")");
            if (flavorPx < microLine)
                failures.Add("[detail] FlavorBandPx=" + flavorPx + " is under one FontMicro line box (" + microLine + ")");
            if (listBottom < statusPx)
                failures.Add("[detail] ListBottomInsetPx=" + listBottom + " does not clear the status band (" +
                             statusPx + ") - the last card would render over the status line");

            notes.Add("detail stack " + fixedStack + "px + body >= " + bodyMin + " in a " + ListWellH_2340 +
                      "px pane (body resolves to " + (ListWellH_2340 - fixedStack) + "px @2340x1080)");
        }

        // =====================================================================
        //  CASE 3 - the source laws that keep the two rects in different columns
        // =====================================================================
        private static void Case3_SourceLaws(List<string> failures, List<string> notes)
        {
            string raw = ReadSource(ViewSrc, failures);
            if (raw == null) return;
            string src = StripComments(raw);

            // THE regression: a hardcoded chip width. 5 x 220 + spacing = 1140 px asked of a
            // 770 px strip, and the mask ate chip 4. Chips must FLEX-FILL the band.
            if (Regex.IsMatch(src, @"preferredWidth\s*=\s*\d+(\.\d+)?f\s*;\s*(//)?.{0,40}(chip|tab)", RegexOptions.IgnoreCase)
                || Regex.IsMatch(src, @"Chip_\"".{0,200}?preferredWidth\s*=\s*[1-9]", RegexOptions.Singleline))
                failures.Add("[no-overlap] a tab chip carries a HARDCODED preferredWidth again - that is what " +
                             "asked 1140 px of a 770 px strip and let the RectMask2D clip 'Gear' to 'G'. " +
                             "Chips flex-fill the band (flexibleWidth 1 + minWidth = the touch floor).");

            if (src.IndexOf("flexibleWidth = 1f", StringComparison.Ordinal) < 0)
                failures.Add("[no-overlap] the tab chips no longer flex-fill the band (flexibleWidth = 1f) - " +
                             "without it the row cannot size itself to the well and clips again");
            if (src.IndexOf("le.minWidth = TabChipMinPx", StringComparison.Ordinal) < 0)
                failures.Add("[no-overlap] the tab chips lost their touch-floor minWidth (TabChipMinPx) - a chip " +
                             "may never be squeezed below the kit touch floor to make the row fit");

            // The two rects must live in DIFFERENT columns. The band takes its X bounds from
            // the LIST zone; the detail pane parents to the RIGHT zone. That disjointness is
            // the whole fix - if either half moves, the overlap is reachable again.
            if (!Regex.IsMatch(src, @"xMin\s*=\s*_zoneLeft\.anchorMin\.x") ||
                !Regex.IsMatch(src, @"xMax\s*=\s*_zoneLeft\.anchorMax\.x"))
                failures.Add("[no-overlap] the tab band no longer takes its X bounds from the LIST zone " +
                             "(_zoneLeft.anchorMin.x / .anchorMax.x) - it can extend across the detail column " +
                             "again, which is the overlap WO-866 fixed");
            if (src.IndexOf("detailHost = zoneRight", StringComparison.Ordinal) < 0)
                failures.Add("[no-overlap] the detail pane no longer parents to the RIGHT well (zoneRight) - " +
                             "the tab band and the detail pane are only guaranteed disjoint while they are in " +
                             "different columns");
            if (Regex.IsMatch(src, @"x(Min|Max)\s*=\s*_zoneRight\."))
                failures.Add("[no-overlap] the tab band takes an X bound from the DETAIL zone (_zoneRight) - the " +
                             "band must be bounded by the LIST column only, or the two rects can overlap again");

            // Fixed-pixel band, never a fraction of parent (WO-841 / WO-852 law).
            if (!Regex.IsMatch(src, @"sizeDelta\s*=\s*new\s+Vector2\s*\(\s*-2f\s*\*\s*TabBandInsetPx\s*,\s*TabBandPx\s*\)"))
                failures.Add("[no-overlap] the tab band is no longer a FIXED-PIXEL band (sizeDelta ... TabBandPx) - " +
                             "a fraction band scales with the well and culls/clips the moment the aspect changes");

            // THE KEEP (owner ruling): the selected tab is marked by a leading "*" AND an
            // underline bar. Text/shape-encoded, never colour alone - the owner is red/green
            // colourblind and this was the one thing on the screen already correct.
            if (src.IndexOf("isActive ? \"* \" : \"\"", StringComparison.Ordinal) < 0)
                failures.Add("[no-overlap] the selected tab lost its leading '*' marker - selection may NEVER be " +
                             "carried by colour alone (the owner is red/green colourblind; this marker is the " +
                             "pattern the rest of the board follows)");
            if (src.IndexOf("\"Underline\"", StringComparison.Ordinal) < 0)
                failures.Add("[no-overlap] the selected tab lost its underline bar - the '*' and the underline are " +
                             "BOTH the KEEP from the WO-866 review");

            // Style-everything-obsidian: the board must route through the kit.
            if (src.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[no-overlap] RumorBoardPanel does not go through ElarionUiKit - the " +
                             "UiObsidianConformanceRegression hand-rolled-uGUI law");

            // Strict MVVM: the View reads the VM, never the quest services.
            foreach (string forbidden in new[] { "QuestService", "QuestCatalog", "DailyQuestService" })
                if (Regex.IsMatch(src, @"\b" + forbidden + @"\s*\."))
                    failures.Add("[no-overlap] RumorBoardPanel touches " + forbidden + " directly - the View is a " +
                                 "read-only consumer of RumorBoardVM (strict MVVM, [ui-mvvm] ratchet armed)");

            // ASCII-only: a non-ASCII glyph renders as tofu on the shipped TMP font.
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] > 127)
                {
                    int line = 1;
                    for (int j = 0; j < i; j++) if (raw[j] == '\n') line++;
                    failures.Add("[no-overlap] RumorBoardPanel.cs carries a NON-ASCII character (U+" +
                                 ((int)raw[i]).ToString("X4") + ") at line " + line +
                                 " - it renders as tofu on the shipped TMP font");
                    break;
                }
            }

            if (raw.IndexOf('\0') >= 0)
                failures.Add("[no-overlap] RumorBoardPanel.cs contains an embedded NUL byte (mount-garble, " +
                             "CLAUDE.md Sec.0) - the compile gate rejects this");

            notes.Add("source laws checked on " + ViewSrc);
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>Read a public const float by reflection (no asmdef reference needed).</summary>
        private static float ConstFloat(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add(tag + " " + t.Name + "." + name + " does not exist - the layout constant this oracle " +
                             "pins was renamed or removed; re-point it rather than deleting the guard");
                return 0f;
            }
            object v = f.GetValue(null);
            if (v is float fv) return fv;
            if (v is int iv) return iv;
            if (v is double dv) return (float)dv;
            failures.Add(tag + " " + t.Name + "." + name + " is not a numeric constant");
            return 0f;
        }

        private static int ConstInt(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null) { failures.Add(tag + " " + t.Name + "." + name + " does not exist"); return 0; }
            object v = f.GetValue(null);
            if (v is int iv) return iv;
            if (v is float fv) return (int)fv;
            failures.Add(tag + " " + t.Name + "." + name + " is not an integer constant");
            return 0;
        }

        private static string ReadSource(string path, List<string> failures)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add("[no-overlap] source not found: " + path);
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add("[no-overlap] could not read " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Blank out // and block comments so a lesson written in prose (which quotes
        /// the old hardcoded width) can never fail a source law.</summary>
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
