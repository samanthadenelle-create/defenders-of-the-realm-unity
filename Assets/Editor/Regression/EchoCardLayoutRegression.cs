// =============================================================================
// EchoCardLayoutRegression [echo-card-layout] (WO-852) - the Echo card's picker
// can never go back to fraction bands / sub-touch-floor chips.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT BROKE (owner felt-test 2026-08-02): EchoCardView stacked six FRACTION-
// anchored text bands plus a picker that sliced its host into 1/n equal fractions
// (rowH = 1f / n). Each slice resolved to ~34 reference px, so the kit touch floor
// (ElarionUiKit.ClampMinTouch / UiKitMinTouchGuard) grew every chip button
// SYMMETRICALLY ABOUT ITS CENTRE up to MinTouchPx(112) - ~39 px past the slice on
// BOTH sides. The chips stacked on each other and the top one climbed into the
// info text; the LAST-built chip ("Crystals") won every overlapping raycast, which
// is exactly what the owner saw. Same bug class as WO-832 Sec.4 / WO-841.
//
// This oracle is a CHEAP structural guard, not a pixel test - it pins the two
// properties that make the bug impossible, both headlessly decidable:
//
//   1 [floors]     EchoCardView's public layout constants (read by REFLECTION so
//                  this file needs no UnityEngine.UI / TMP asmdef reference):
//                  ChipButtonPx >= ElarionUiKit.MinTouchPx  (the mobile touch floor -
//                  a chip smaller than this is untappable on the owner's phone), and
//                  every text band >= one TMP line box at the kit's FontFloor
//                  (a shorter band silently CULLS glyphs - the WO-832 lesson).
//   2 [no-fraction] Source law on EchoCardView.cs: RebuildChips no longer computes a
//                  1/n row fraction, the picker is hosted in a kit scroll well
//                  (MakeScrollZone), rows are sized by sizeDelta (the kit scroll-column
//                  row law), and the fixed-pixel band pins exist. Also: the file still
//                  routes through ElarionUiKit (no hand-rolled uGUI - the
//                  UiObsidianConformanceRegression law) and is ASCII-only.
//
// A pixel-perfect "no two rects overlap" assertion needs a live canvas at both
// capture aspects; that stays the job of RunCaptureHeadless + eyes-on. This oracle
// catches the REGRESSION (someone re-introduces a fraction slice or shrinks a chip)
// which is the failure mode that actually recurs.
//
// Markers: ECHO_CARD_LAYOUT_OK / ECHO_CARD_LAYOUT_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.EchoCardLayoutRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-card-layout suite", () => { if (!DeNelle.Editor.Regression.EchoCardLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-card-layout] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EchoCardLayoutRegression
    {
        private const string ViewSrc = "Assets/_Modules/Village/Harvest/EchoCardView.cs";

        private const string ViewType = "DeNelle.Village.EchoCardView";
        private const string KitType = "DeNelle.Core.UI.ElarionUiKit";

        // The TMP line box multiplier the card's bands are built from (~1.25em).
        private const float LineBoxMul = 1.25f;

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ECHO_CARD_LAYOUT_OK - " + reason);
            else Debug.LogError("ECHO_CARD_LAYOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "floors", () => Case1_TouchAndLineFloors(failures, notes));
                Case(failures, "no-fraction", () => Case2_SourceLaws(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "ECHO CARD LAYOUT OK - every picker chip is at least the kit touch floor tall, " +
                         "every text band is a whole TMP line box in FIXED reference pixels, and the picker " +
                         "is a kit scroll well with sizeDelta rows (no 1/n fraction slicing)" + noteStr;
                return true;
            }
            reason = "echo-card-layout FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  CASE 1 - the numeric floors (touch floor + TMP line box)
        // =====================================================================
        private static void Case1_TouchAndLineFloors(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            if (view == null)
            {
                failures.Add("[floors] " + ViewType + " not found - the Echo card view was renamed or " +
                             "removed; re-point this oracle (it is the only guard on the picker touch floor)");
                return;
            }
            Type kit = FindType(KitType);
            if (kit == null)
            {
                failures.Add("[floors] " + KitType + " not found - cannot read the kit touch floor");
                return;
            }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[floors]");
            float fontFloor = ConstFloat(kit, "FontFloor", failures, "[floors]");
            if (minTouch <= 0f || fontFloor <= 0f) return;

            float lineBox = fontFloor * LineBoxMul;   // one readable line, before slack

            float chipBtn = ConstFloat(view, "ChipButtonPx", failures, "[floors]");
            float chipNote = ConstFloat(view, "ChipNotePx", failures, "[floors]");
            float floorLine = ConstFloat(view, "FloorLinePx", failures, "[floors]");
            float askBand = ConstFloat(view, "AskBandPx", failures, "[floors]");
            if (chipBtn <= 0f || chipNote <= 0f || floorLine <= 0f || askBand <= 0f) return;

            // THE owner-facing invariant: a chip you cannot hit with a thumb is a broken
            // picker, and shrinking below the floor is never an acceptable way to make
            // five chips "fit" (that is what WO-852 rejected).
            if (chipBtn < minTouch)
                failures.Add($"[floors] EchoCardView.ChipButtonPx={chipBtn} is BELOW the kit touch floor " +
                             $"MinTouchPx={minTouch} - the resource chips would be untappable on a phone; " +
                             "scroll the picker instead of shrinking the chips");

            if (floorLine < lineBox)
                failures.Add($"[floors] EchoCardView.FloorLinePx={floorLine} is shorter than one TMP line box " +
                             $"at the kit FontFloor ({fontFloor} x {LineBoxMul} = {lineBox}) - a band shorter " +
                             "than its line box silently CULLS glyphs (WO-832 Sec.4 / WO-841)");

            if (chipNote < lineBox)
                failures.Add($"[floors] EchoCardView.ChipNotePx={chipNote} is shorter than one line box " +
                             $"({lineBox}) - the affinity note ('best -- this Echo's calling') would clip");

            if (askBand < lineBox)
                failures.Add($"[floors] EchoCardView.AskBandPx={askBand} is shorter than one line box " +
                             $"({lineBox}) - the 'What should X gather?' ask would clip");

            // The note must be its OWN band, never carved out of the button's height -
            // the pre-WO-852 card gave the button 58% of the row when a note was present.
            if (chipNote >= chipBtn)
                notes.Add($"ChipNotePx({chipNote}) >= ChipButtonPx({chipBtn}) - unusual, the note is taller " +
                          "than the chip it annotates; check the row budget");

            notes.Add($"chip={chipBtn}px (floor {minTouch}), bands: floorLine={floorLine}, note={chipNote}, ask={askBand}");
        }

        // =====================================================================
        //  CASE 2 - the source laws that make the bug unreachable
        // =====================================================================
        private static void Case2_SourceLaws(List<string> failures, List<string> notes)
        {
            string src = ReadSource(ViewSrc, failures);
            if (src == null) return;

            // THE regression: a 1/n row fraction. This is the exact line that shipped the
            // bug (`float rowH = 1f / n;`), so match the shape, not the variable name.
            var fractionSlice = new Regex(@"1f\s*/\s*(?:n|count|Length|chips\.Length)\b", RegexOptions.IgnoreCase);
            if (fractionSlice.IsMatch(StripComments(src)))
                failures.Add("[no-fraction] EchoCardView slices the picker into 1/n FRACTIONS again - each slice " +
                             "resolves below MinTouchPx and ClampMinTouch then grows the button past the slice " +
                             "on BOTH sides, stacking the chips over each other and over the info block " +
                             "(the WO-852 bug, verbatim). Size rows in FIXED reference pixels.");

            // The picker must live in the kit scroll well - that is what lets five
            // touch-floor chips exist inside a body well that cannot show them all.
            if (src.IndexOf("MakeScrollZone", StringComparison.Ordinal) < 0)
                failures.Add("[no-fraction] EchoCardView no longer hosts the picker in a kit scroll well " +
                             "(ElarionUiKit.MakeScrollZone) - five chips at MinTouchPx need ~560 ref px and " +
                             "FrameCore's body well resolves to ~418 px at 2340x1080; without the well the " +
                             "chips must overlap or drop below the touch floor");

            // Kit scroll-column row law: rows carry their own height via sizeDelta.
            if (!Regex.IsMatch(src, @"sizeDelta\s*=\s*new\s+Vector2\s*\(\s*0f\s*,\s*rowPx"))
                failures.Add("[no-fraction] EchoCardView's picker rows are not sized by an explicit pixel " +
                             "sizeDelta - MakeScrollZone runs childControlHeight:false, so a row without its " +
                             "own sizeDelta collapses to height 0 and the picker renders empty");

            // The fixed-pixel band pins (the WO-832 Sec.4 / WO-841 pattern).
            if (src.IndexOf("PinBandFromTop", StringComparison.Ordinal) < 0 ||
                src.IndexOf("PinBandFromBottom", StringComparison.Ordinal) < 0)
                failures.Add("[no-fraction] EchoCardView lost its fixed-pixel band pins " +
                             "(PinBandFromTop / PinBandFromBottom) - text bands are back on parent fractions, " +
                             "which under-height the TMP line box and cull glyphs (WO-832 Sec.4 / WO-841)");

            // The churn gate: EchoService.AddToSilo raises Changed EVERY FRAME while the
            // silo fills. Without a rendered-state signature the picker is destroyed and
            // rebuilt per frame, which also resets the scroll position per frame - the
            // owner could never scroll to the last chip.
            if (src.IndexOf("_lastChipSig", StringComparison.Ordinal) < 0)
                failures.Add("[no-fraction] EchoCardView lost the picker churn gate (_lastChipSig) - " +
                             "EchoService raises Changed once per FRAME while harvesting, so the chip rows " +
                             "would be destroyed + rebuilt every frame and the scroll well would reset its " +
                             "position every frame (unscrollable picker + layout jitter)");

            // Style-everything-obsidian: the card must route through the kit.
            if (src.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[no-fraction] EchoCardView does not go through ElarionUiKit - the " +
                             "UiObsidianConformanceRegression hand-rolled-uGUI law");

            // ASCII-only: a non-ASCII glyph renders as tofu on the shipped TMP font.
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] > 127)
                {
                    int line = 1;
                    for (int j = 0; j < i; j++) if (src[j] == '\n') line++;
                    failures.Add($"[no-fraction] EchoCardView.cs carries a NON-ASCII character " +
                                 $"(U+{(int)src[i]:X4}) at line {line} - it renders as tofu on the shipped TMP font");
                    break;
                }
            }

            if (src.IndexOf('\0') >= 0)
                failures.Add("[no-fraction] EchoCardView.cs contains an embedded NUL byte (mount-garble, " +
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

        private static string ReadSource(string path, List<string> failures)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add("[no-fraction] source not found: " + path);
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add($"[no-fraction] could not read {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Blank out // and block comments so a lesson written in prose (which
        /// deliberately quotes the old "1f / n" line) can never fail the source law.</summary>
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
