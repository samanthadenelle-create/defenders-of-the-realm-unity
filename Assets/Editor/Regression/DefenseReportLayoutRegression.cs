// =============================================================================
// DefenseReportLayoutRegression [defense-report-layout] - the defence report can
// never again paint a tan slab under light ink, and its list rows can never again
// overlap each other (WO-1515).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT BROKE (owner device frame, build 2026.09.07.358574, 20:03 -
// Logs/device/screens/owner-defense-report-20260906-200350.png):
//
//   * The DETAIL pane rendered as a flat TAN rectangle with grey text on it. Every
//     detail line was near-invisible: "They never reached your inner ring.",
//     "Defence score 100/100 - Clean hold", "ATTACKER", "Strength 147 - wave 13 -
//     lasted 44s". Only the brightest line and the gold "HELD" heading survived,
//     and "HELD" was gold-on-tan.
//   * The LIST rows overlapped: "HOLLOW HOST - 6H AGO" painted across the gold
//     bezel of the BREACHED row beneath it - two rows sharing one band.
//
// THE TWO CAUSES, both read at source rather than inferred:
//
//   1  DefenseReportPanel.StyleObsidianWell built ONE Image, seeded it with
//      ObsidianFill, then overwrote that fill with the bezel sprite and white
//      ("img.sprite = frame; img.color = Color.white"). card-frame-empty is a
//      HOLLOW border, so no dark surface remained. FrameQuest is a twoToneBody
//      frame, so ElarionUiKit paints ZoneBacking(bodyRight, TwoToneParchmentFill)
//      = RGB(0.827, 0.760, 0.576) behind the detail zone; that tan read straight
//      through the hole, under ink the panel had already chosen for a DARK surface.
//      The LEFT well took the IDENTICAL call and looked correct - its backing is
//      the dark TwoToneWellFill. One code path, two surfaces, one broken: proof
//      the fill was never doing the work it was credited with.
//
//   2  The list row label carried a hard "\n". FitSingleLine is a WIDTH fit
//      (NoWrap + Ellipsis + autosize); a hard break survives NoWrap, so autosizing
//      never shrank the label to make its SECOND line fit the fixed 132px band, and
//      the overflow painted over the next row.
//
// THE CASES (all decidable headlessly, at gate speed):
//   1 [derived-pitch]  The row band is DERIVED from the row font, not a literal;
//                      the rendered line box plus padding FITS inside the band; the
//                      pitch clears the band; the band is at/above the kit touch
//                      floor; and at every landscape capture aspect at least two
//                      whole rows fit the list well, so the "two rows, one band"
//                      frame is arithmetically unreachable.
//   2 [dark-plate]     Every ink this panel renders detail with clears WCAG AA
//                      (4.5:1) against the well plate it actually sits on. The SAME
//                      routine run against TwoToneParchmentFill - the surface in the
//                      owner's frame - returns ~1.05:1, which is this case failing
//                      red on the shipped build.
//   3 [source-laws]    The plate and the bezel stay TWO images, the plate keeps an
//                      opaque fill and never takes the frame sprite, the row label
//                      carries no "\n", FitSingleLine is armed on the row caption,
//                      the panel never flips itself to parchment ink, and the file
//                      is NUL-free.
//   4 [chip-gate]     WO-1515 sec.2B door half: DefenseReportChipModel.Compose is
//                      INVISIBLE and caption-less at zero unread reports (sec.3
//                      forbids a permanent chip) and VISIBLE with the outcome WORD
//                      on its two-line face at one - HELD / BREACHED / OVERRUN, the
//                      only part that survives greyscale. Also that the word has one
//                      authority (the panel may not re-grow its own switch) and that
//                      DefenseReportPanel.Open marks the LANDING report read, without
//                      which the chip's own door could never clear the chip; that the
//                      chip still opens PanelId.DefenseReport; and that EVERY LINE of
//                      the longest caption MEASURES inside the 220 x 0.92 label rect at
//                      the 22 px FitBlock floor - the WO-1144 "Tap to collec" lesson,
//                      same chip family, and the reason the face is two lines. A
//                      FIXTURE case - Compose is pure, so no GameState is needed.
//   5 [sizedelta-law] WO-1585. The list row band and the map plate band BOTH reach
//                      sizeDelta, the plate band is DERIVED from the measured detail
//                      viewport, and plate labels stay NoWrap + floored at
//                      ElarionUiKit.FontFloor.
//   6 [measured-plate] WO-1585, BUILT AND MEASURED at three landscape surfaces: the
//                      real kit scroll zone + the real DefenseMapPlate.BuildBand +
//                      real TMP paragraphs, settled twice, then measured. The band
//                      gets the height it was built at (100px is the shipped defect),
//                      no text row's rect intersects the band (LayoutOracle.Overlaps -
//                      the WO-1060 engine), and every plate label is one line inside
//                      the band. It also carries a RED FIXTURE: a band built the OLD
//                      way (LayoutElement only, no sizeDelta) is MEASURED, and the case
//                      FAILS if it ever gets the height it asks for - the only thing
//                      that proves this suite still measures the MECHANISM rather than
//                      restating the panel's intent.
//
// ⛔ WHY 5 AND 6 EXIST, AND WHY CASE 1 WAS GREEN THROUGH WO-1585: case 1 does
//    ARITHMETIC ON SOURCE CONSTANTS and assumes the height a row ASKS for is the
//    height it GETS. ElarionUiKit.MakeScrollZone runs childControlHeight:false, so
//    uGUI reads child.sizeDelta and ignores the LayoutElement the panel was setting
//    (HorizontalOrVerticalLayoutGroup.cs:224-229). The rows shipped at the 100px
//    RectTransform default and the plate band with them, and every number case 1
//    checked was correct. A suite that measures intent proves nothing about pixels.
//
// NO HOLLOW PASSES: every early return below is preceded by a recorded FAILURE. A
// missing constant or an unreadable source file is a FAILURE here, never a note.
//
// Markers: DEFENSE_REPORT_LAYOUT_OK / DEFENSE_REPORT_LAYOUT_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DefenseReportLayoutRegression.RunAll
// Registered in DataRegression.RunAll as the "defense-report-layout suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Defense;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class DefenseReportLayoutRegression
    {
        private const string PanelSrc = "Assets/_Modules/Village/UI/Defense/DefenseReportPanel.cs";

        /// <summary>The HUD View the WO-1515 chip lives in. Its rail-chip box constants are
        /// private, so they are RESTATED below as a budget and asserted against this file's own
        /// literals (the HudLabelFitRegression Case0 pattern, same chip family).</summary>
        private const string HudSrc = "Assets/_Modules/HUD/Kit/HudKitController.cs";

        /// <summary>HudKitController.RailChipWidthPx. Three rail chips share one right edge, so
        /// this is canon, not a knob - the fix for a face that does not fit is FEWER CHARACTERS.</summary>
        private const float ChipWidthPx = 220f;

        /// <summary>The kit button's label inset (ElarionUiKit obsidian button face).</summary>
        private const float ChipLabelInset = 0.92f;

        /// <summary>The FitBlock floor the View arms on the chip label. A floor, never a knob.</summary>
        private const float ChipFontFloorPx = 22f;

        /// <summary>TMP line box multiplier (~1.25em) - the same budget the other layout
        /// oracles in this folder use.</summary>
        private const float LineBoxMul = 1.25f;

        /// <summary>The kit touch floor (ElarionUiKit.MinTouchPx). Restated here as a BUDGET,
        /// and asserted against the live constant by name in case 1 so it cannot drift.</summary>
        private const float TouchFloorPx = 112f;

        /// <summary>Canvas scaler contract from ElarionUiKit.BuildModalCanvas: 1080x1920
        /// reference, ScaleWithScreenSize, match 0.5 (geometric mean).</summary>
        private const float RefW = 1080f, RefH = 1920f;

        /// <summary>The panel rect BuildChrome opens at: anchors 0.05 -> 0.95 of the screen.</summary>
        private const float PanelYMin = 0.05f, PanelYMax = 0.95f;

        /// <summary>FrameQuest bodyLeft (the LIST well) as fractions of the panel rect, read from
        /// ElarionUiKit.ZonesFor: new Vector4(0.035f, 0.115f, 0.495f, 0.858f).</summary>
        private const float ListZoneYMin = 0.115f, ListZoneYMax = 0.858f;

        /// <summary>MakeScrollZone padding on the list column (top + bottom).</summary>
        private const float ListPadPx = 22f;

        /// <summary>The LANDSCAPE capture aspects. The game is landscape-only (owner ruling
        /// 2026-08-26); 2670x1200 is the owner's device and the aspect the WO names.</summary>
        private static readonly int[,] Aspects = { { 1920, 1080 }, { 2340, 1080 }, { 2670, 1200 } };

        /// <summary>WCAG AA for body text.</summary>
        private const float ContrastFloor = 4.5f;

        // The surfaces. Values read at source this session, not remembered.
        private static readonly Color WellFill = new Color(0.02f, 0.02f, 0.025f, 1f);           // panel plate
        private static readonly Color ShippedTan = new Color(0.827f, 0.760f, 0.576f, 1f);       // TwoToneParchmentFill

        // The inks RebuildDetail renders with while _onParchment is false.
        private static readonly Color InkTitle = new Color(0.933f, 0.784f, 0.282f, 1f);         // ElarionUi.Gilt
        private static readonly Color InkBody = new Color(0.953f, 0.918f, 0.827f, 1f);          // ElarionUi.Parchment
        private static readonly Color InkDim = new Color(0.78f, 0.74f, 0.66f, 1f);              // ElarionUi.ParchmentDim

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DEFENSE_REPORT_LAYOUT_OK - " + reason);
            else Debug.LogError("DEFENSE_REPORT_LAYOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                string src = ReadSource(PanelSrc, failures);
                if (src != null)
                {
                    CaseDerivedPitch(src, failures, notes);
                    CaseDarkPlate(failures, notes);
                    CaseSourceLaws(src, failures, notes);
                    CaseChipGate(src, failures, notes);
                    CaseSizeDeltaLaw(src, failures, notes);
                }
                CaseMeasuredPlate(failures, notes);
            }
            catch (Exception ex)
            {
                failures.Add("defense-report-layout threw: " + ex.Message);
            }

            reason = failures.Count == 0
                ? "6 cases - " + string.Join("; ", notes)
                : failures.Count + " finding(s): " + string.Join(" | ", failures);
            return failures.Count == 0;
        }

        // ── 1. [derived-pitch] ───────────────────────────────────────────────────

        private static void CaseDerivedPitch(string src, List<string> failures, List<string> notes)
        {
            // The band must be COMPUTED. A re-hardcoded literal is the defect returning.
            if (src.IndexOf("Mathf.Max(ElarionUiKit.MinTouchPx", StringComparison.Ordinal) < 0)
                failures.Add("[derived-pitch] ListRowPx is no longer derived from "
                    + "ElarionUiKit.MinTouchPx - a hand-typed band is how the 132px row and its "
                    + "two-line label disagreed in the first place.");

            float fontMax = ConstFloat(src, "RowFontMax", failures);
            float fontMin = ConstFloat(src, "RowFontMin", failures);
            float mul = ConstFloat(src, "RowLineBoxMul", failures);
            float pad = ConstFloat(src, "RowPadPx", failures);
            float gap = ConstFloat(src, "ListRowGapPx", failures);
            if (float.IsNaN(fontMax) || float.IsNaN(mul) || float.IsNaN(pad) || float.IsNaN(gap))
                return;   // ConstFloat already recorded the failure

            float band = Mathf.Max(TouchFloorPx, fontMax * mul + pad);
            float content = fontMax * mul + pad;
            float pitch = band + gap;

            if (content > band + 0.01f)
                failures.Add("[derived-pitch] a row's rendered line box + padding is " + Px(content)
                    + " but its band is only " + Px(band) + " - the overflow lands on the next row.");
            if (band < TouchFloorPx)
                failures.Add("[derived-pitch] row band " + Px(band) + " is under the kit touch floor "
                    + Px(TouchFloorPx) + ".");
            if (pitch <= band)
                failures.Add("[derived-pitch] pitch " + Px(pitch) + " does not clear the band "
                    + Px(band) + " - adjacent bands touch.");
            if (!float.IsNaN(fontMin) && fontMin > fontMax)
                failures.Add("[derived-pitch] RowFontMin " + Px(fontMin) + " exceeds RowFontMax "
                    + Px(fontMax) + " - FitSingleLine would have no range to shrink into.");

            // The measured half: how many WHOLE rows the list well holds at each capture aspect.
            for (int i = 0; i < Aspects.GetLength(0); i++)
            {
                int w = Aspects[i, 0], h = Aspects[i, 1];
                float scale = Mathf.Pow(w / RefW, 0.5f) * Mathf.Pow(h / RefH, 0.5f);
                if (scale <= 0f) { failures.Add("[derived-pitch] degenerate scaler at " + w + "x" + h); continue; }
                float canvasH = h / scale;                                   // screen px -> canvas units
                float panelH = canvasH * (PanelYMax - PanelYMin);
                float wellH = panelH * (ListZoneYMax - ListZoneYMin);
                float usable = wellH - 2f * ListPadPx;
                int rows = Mathf.FloorToInt((usable + gap) / pitch);
                if (rows < 2)
                    failures.Add("[derived-pitch] at " + w + "x" + h + " the list well holds only "
                        + rows + " whole row(s) (usable " + Px(usable) + ", pitch " + Px(pitch)
                        + ") - two reports cannot be seen without one overlapping the other.");
                if (w == 2670 && h == 1200)
                    notes.Add("[derived-pitch] 2670x1200: well " + Px(wellH) + ", band " + Px(band)
                        + ", pitch " + Px(pitch) + ", " + rows + " whole rows, zero overlap");
            }
        }

        // ── 2. [dark-plate] ──────────────────────────────────────────────────────

        private static void CaseDarkPlate(List<string> failures, List<string> notes)
        {
            Check("title (Gilt)", InkTitle, failures);
            Check("body (Parchment)", InkBody, failures);
            Check("dim (ParchmentDim)", InkDim, failures);

            // The negative fixture: the SAME arithmetic against the surface the owner's frame
            // actually rendered. If this ever clears the floor, the oracle has stopped measuring.
            float shipped = Contrast(InkDim, ShippedTan);
            if (shipped >= ContrastFloor)
                failures.Add("[dark-plate] the shipped tan surface scores " + Ratio(shipped)
                    + " against dim ink, which contradicts the captured frame - this case is "
                    + "no longer measuring what it claims to.");
            else
                notes.Add("[dark-plate] min ink/plate " + Ratio(Contrast(InkDim, WellFill))
                    + " (shipped tan was " + Ratio(shipped) + ")");
        }

        private static void Check(string what, Color ink, List<string> failures)
        {
            float c = Contrast(ink, WellFill);
            if (c < ContrastFloor)
                failures.Add("[dark-plate] detail " + what + " scores " + Ratio(c)
                    + " on the well plate, under the " + Ratio(ContrastFloor) + " floor.");
        }

        /// <summary>WCAG 2.x contrast ratio. sRGB channel values, gamma-expanded per the spec.</summary>
        private static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            float hi = Mathf.Max(la, lb), lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        private static float Luminance(Color c)
            => 0.2126f * Expand(c.r) + 0.7152f * Expand(c.g) + 0.0722f * Expand(c.b);

        private static float Expand(float c)
            => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        // ── 3. [source-laws] ─────────────────────────────────────────────────────

        private static void CaseSourceLaws(string src, List<string> failures, List<string> notes)
        {
            // The forbidden-line regexes run against CODE ONLY. The panel's own RCA comment
            // quotes `img.sprite = frame` verbatim - deliberately, so a future edit cannot
            // collapse the plate and bezel back without deleting the line that says why - and
            // matching that quote reported the fixed panel as broken. Documenting a defect is
            // not committing it; strip comments, then match.
            string code = StripComments(src);

            // The plate and the bezel are TWO images. Collapsing them back is the original bug.
            if (src.IndexOf("Bezel", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] StyleObsidianWell no longer builds a separate bezel "
                    + "image - a single image that takes the hollow card-frame sprite leaves no "
                    + "dark surface, and the kit's parchment backing reads through it.");
            if (Regex.IsMatch(code, @"(?<![A-Za-z_])(img|plateImg)\s*\.\s*sprite\s*=\s*frame"))
                failures.Add("[source-laws] the well FILL image is taking the bezel sprite again "
                    + "(img.sprite = frame) - that is verbatim the line that produced the tan slab.");
            if (src.IndexOf("WellFill", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the opaque WellFill plate colour is gone.");

            // The row label is ONE line.
            if (Regex.IsMatch(code, @"string\s+label\s*=[^;]*\\n"))
                failures.Add("[source-laws] the list row label carries a hard \"\\n\" again - "
                    + "NoWrap does not remove a hard break, so the second line overflows the band.");
            if (src.IndexOf("ElarionUiKit.FitSingleLine(caption", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] FitSingleLine is no longer armed on the list row caption.");

            // Ink and surface are one decision.
            if (Regex.IsMatch(code, @"_onParchment\s*=\s*true"))
                failures.Add("[source-laws] the panel sets _onParchment = true while painting an "
                    + "obsidian plate - dark ink on a dark surface is the same defect inverted.");

            if (src.IndexOf('\0') >= 0)
                failures.Add("[source-laws] " + PanelSrc + " carries a NUL byte (CLAUDE.md section 1).");

            int opens = 0, closes = 0;
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == '{') opens++;
                else if (src[i] == '}') closes++;
            }
            if (opens != closes)
                failures.Add("[source-laws] brace mismatch in " + PanelSrc + ": " + opens + " vs " + closes + ".");
            else
                notes.Add("[source-laws] plate+bezel split, one-line fitted rows, " + opens + " balanced braces");
        }

        // -- 4. [chip-gate] ------------------------------------------------------

        /// <summary>
        /// WO-1515 sec.2B/2D - the ATTACK REPORT chip exists ONLY while a report is unread.
        ///
        /// This is a FIXTURE case, not a source lint: it drives
        /// DeNelle.Core.HudModel.DefenseReportChipModel.Compose directly, which is why that
        /// method is pure. No GameState, no save file, no running HUD - so the visibility rule
        /// the owner ruled on is decided at gate speed, in the same place the View reads it.
        ///
        /// The negative half is the load-bearing half: WO-1515 sec.3 forbids a permanent chip
        /// ("a fifth status glance competing with the four that earn their place"), so a
        /// zero-unread snapshot must be invisible AND caption-less. A model that returned an
        /// "off" caption would let a View paint a greyed chip and still pass a visibility-only
        /// assert.
        /// </summary>
        private static void CaseChipGate(string panelSrc, List<string> failures, List<string> notes)
        {
            int before = failures.Count;
            // Nothing unread -> no chip at all.
            var none = DefenseReportChipModel.Compose(0, DefenseOutcome.Held);
            if (none.Visible)
                failures.Add("[chip-gate] the chip is VISIBLE with zero unread reports - WO-1515 "
                    + "section 3 forbids a permanent chip; it must not render at all, not greyed "
                    + "and not empty-stated.");
            if (!string.IsNullOrEmpty(none.Caption))
                failures.Add("[chip-gate] the invisible chip still carries a caption (\"" + none.Caption
                    + "\") - an off-state string is how a conditional chip becomes a permanent one.");
            if (none.Key != 0)
                failures.Add("[chip-gate] the invisible snapshot's repaint Key is " + none.Key
                    + ", not 0 - the View cannot tell 'no chip' from 'a chip it has not painted yet'.");

            // One unread -> chip, with the OUTCOME WORD on it. The word is what survives
            // greyscale; the owner is red/green colourblind and a tint would say nothing.
            var expectWords = new[] { "HELD", "BREACHED", "OVERRUN" };
            var outcomes = new[] { DefenseOutcome.Held, DefenseOutcome.Breached, DefenseOutcome.Overrun };
            for (int i = 0; i < outcomes.Length; i++)
            {
                var snap = DefenseReportChipModel.Compose(1, outcomes[i]);
                if (!snap.Visible)
                    failures.Add("[chip-gate] one UNREAD " + outcomes[i] + " report and the chip is hidden - "
                        + "the owner's 20:05 ruling is that an existing report puts a button on screen.");
                if (snap.Caption == null || snap.Caption.IndexOf(expectWords[i], StringComparison.Ordinal) < 0)
                    failures.Add("[chip-gate] the " + outcomes[i] + " chip face is \"" + (snap.Caption ?? "<null>")
                        + "\" - it does not carry the word " + expectWords[i]
                        + ", which is the only part of this chip that survives greyscale.");
                if (snap.Caption == null || snap.Caption.IndexOf(DefenseReportChipModel.TitleLine,
                                                                StringComparison.Ordinal) < 0)
                    failures.Add("[chip-gate] the " + outcomes[i] + " chip face does not name the screen it "
                        + "opens (\"" + DefenseReportChipModel.TitleLine + "\").");
                if (snap.Key == none.Key)
                    failures.Add("[chip-gate] the " + outcomes[i] + " snapshot shares its repaint Key with the "
                        + "empty one - the chip would never repaint when a report lands.");
                // The face is TWO LINES on a 220x112 chip. One line cannot seat title + word
                // above the kit legibility floor, and FitSingleLine would ellipsize the word
                // away - the same width-fit RCA as this WO's list-row overlap.
                if (snap.Caption == null || snap.Caption.IndexOf('\n') < 0)
                    failures.Add("[chip-gate] the " + outcomes[i] + " chip face is one line - it cannot seat "
                        + "the title and the outcome word on a 220px chip above the kit font floor.");
            }

            // A second report must move the key too: the chip re-reads as "another one landed".
            if (DefenseReportChipModel.Compose(2, DefenseOutcome.Held).Key ==
                DefenseReportChipModel.Compose(1, DefenseOutcome.Held).Key)
                failures.Add("[chip-gate] one unread and two unread share a repaint Key - a second "
                    + "assault would never repaint the chip.");

            // The word is ONE authority. The panel heading, the list row and the chip all read
            // this switch; a second copy is how three surfaces start disagreeing.
            if (panelSrc != null &&
                Regex.IsMatch(StripComments(panelSrc), @"case\s+DefenseOutcome\s*\.\s*Overrun\s*:\s*return\s*""OVERRUN"""))
                failures.Add("[chip-gate] DefenseReportPanel has re-grown its own OutcomeWord switch - "
                    + "the chip and the panel must read the one in DefenseReportChipModel.");

            // The door has to be answerable: opening the panel MARKS THE LANDING REPORT READ,
            // or the chip the player just tapped is still there when they close it.
            if (panelSrc != null && StripComments(panelSrc).IndexOf("DefenseReportLedger.MarkRead(_selectedId)",
                                                                    StringComparison.Ordinal) < 0)
                failures.Add("[chip-gate] DefenseReportPanel.Open no longer marks the landing report read - "
                    + "the chip's own door would not clear the chip (Select, the row tap, used to be "
                    + "MarkRead's only caller).");

            // MEASURED, not counted. WO-1144's lesson on this exact chip family: "Tap to collec"
            // shipped in all 8 runs of a headed fleet because nobody measured the string against
            // the box. The chip is RailChipWidthPx wide with a ~0.92 label inset, and the View
            // re-fits the face with FitBlock(22, 30) - so EVERY LINE of the longest caption must
            // seat inside that inner width at the FLOOR, 22 px. If it cannot, the fix is fewer
            // characters, never a smaller font (22 is a floor) and never a wider chip (three rail
            // chips share one right edge).
            string hudSrc = ReadSource(HudSrc, failures);
            if (hudSrc != null)
            {
                // The two numbers below are RESTATED here as a budget (the HUD constant is
                // private), so they are asserted against the shipped literal rather than trusted -
                // the HudLabelFitRegression Case0 pattern, same chip family, same reason.
                if (hudSrc.IndexOf("RailChipWidthPx = " + ChipWidthPx.ToString("0") + "f",
                                   StringComparison.Ordinal) < 0)
                    failures.Add("[chip-gate] HudKitController.RailChipWidthPx is no longer "
                        + ChipWidthPx.ToString("0") + "f - every width this case measures against is "
                        + "now an invented number");
                if (hudSrc.IndexOf("ElarionUiKit.FitBlock(_defenseChipLabel, "
                                   + ChipFontFloorPx.ToString("0") + "f", StringComparison.Ordinal) < 0)
                    failures.Add("[chip-gate] the attack report chip's label is no longer re-fitted with "
                        + "FitBlock at a " + ChipFontFloorPx.ToString("0") + " px floor - BuildRailChip arms "
                        + "FitSingleLine, a WIDTH fit, which ellipsizes the outcome word off the end");
                if (hudSrc.IndexOf("PanelRouter.Open(PanelId.DefenseReport)", StringComparison.Ordinal) < 0)
                    failures.Add("[chip-gate] the chip no longer opens PanelId.DefenseReport - it is a "
                        + "status glance with no door, which is not what the owner asked for");
            }

            {
                float inner = ChipWidthPx * ChipLabelInset;
                var longest = DefenseReportChipModel.Compose(1, DefenseOutcome.Breached);
                foreach (string line in (longest.Caption ?? string.Empty).Split('\n'))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string detail;
                    float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, line,
                                                              ChipFontFloorPx, out detail);
                    if (w < 0f)
                        failures.Add("[chip-gate] cannot MEASURE the chip line \"" + line + "\": " + detail +
                                     " - an unmeasured chip face is how 'Tap to collec' shipped (WO-1144)");
                    else if (w > inner)
                        failures.Add("[chip-gate] the chip line \"" + line + "\" measures " + w.ToString("0.0") +
                                     " ref px at the " + ChipFontFloorPx + " px fit floor but the label rect is " +
                                     "only " + inner.ToString("0.0") + " px (" + detail + ") - it would be " +
                                     "truncated, and the half that goes is the outcome word");
                    else
                        notes.Add("[chip-gate] \"" + line + "\" " + w.ToString("0.0") + "/" +
                                  inner.ToString("0.0") + " px");
                }
            }

            if (failures.Count == before)
                notes.Add("[chip-gate] chip absent at 0 unread, present with HELD/BREACHED/OVERRUN on its face");
        }

        // ── 5. [sizedelta-law] ───────────────────────────────────────────────────

        /// <summary>
        /// ⭐ WO-1585. THE CASE THAT WOULD HAVE CAUGHT IT, AND THE REASON CASE 1 DID NOT.
        ///
        /// <para>CaseDerivedPitch does ARITHMETIC ON SOURCE CONSTANTS and assumes the number a
        /// row asks for becomes the height it gets. That assumption is FALSE in a kit scroll
        /// column: ElarionUiKit.MakeScrollZone runs <c>childControlHeight = false</c>, and uGUI's
        /// HorizontalOrVerticalLayoutGroup.GetChildSizes then reads <c>child.sizeDelta[axis]</c>
        /// and ignores the child's LayoutElement entirely (com.unity.ugui .../Layout/
        /// HorizontalOrVerticalLayoutGroup.cs:224-229, read at source 2026-09-07). So the panel
        /// asked for a 112 px row and a 420 px plate band via LayoutElement, got the
        /// RectTransform default of 100 px for both, and case 1 stayed green through the whole
        /// defect. A suite that measures the number a screen WANTS proves nothing about the
        /// pixels a player gets.</para>
        ///
        /// <para>This case pins the MECHANISM in source; case 6 measures the result.</para>
        /// </summary>
        private static void CaseSizeDeltaLaw(string src, List<string> failures, List<string> notes)
        {
            int before = failures.Count;
            string code = StripComments(src);

            if (!Regex.IsMatch(code, @"sizeDelta\s*=\s*new\s+Vector2\s*\(\s*0f?\s*,\s*ListRowPx\s*\)"))
                failures.Add("[sizedelta-law] the list row no longer writes its band to sizeDelta "
                    + "(sizeDelta = new Vector2(0f, ListRowPx)). MakeScrollZone runs "
                    + "childControlHeight:false, so a LayoutElement alone is INVISIBLE to the column "
                    + "and the row falls back to the RectTransform default of 100px - under the "
                    + Px(TouchFloorPx) + " touch floor, which is exactly what the owner's "
                    + "2026-09-07 Seeker frame measures.");

            if (code.IndexOf("DefenseMapPlate.BuildBand", StringComparison.Ordinal) < 0)
                failures.Add("[sizedelta-law] the panel no longer builds its plate through "
                    + "DefenseMapPlate.BuildBand - that seam is the ONE place the band's height "
                    + "reaches sizeDelta, and it is the seam this suite measures.");

            if (code.IndexOf("DefenseMapPlate.DeriveHeightPx", StringComparison.Ordinal) < 0)
                failures.Add("[sizedelta-law] the plate band height is no longer derived from the "
                    + "measured detail viewport (DefenseMapPlate.DeriveHeightPx) - a re-typed "
                    + "literal is sized for one aspect and wrong on every other.");

            if (Regex.IsMatch(code, @"MapPlatePx\s*=\s*[0-9]"))
                failures.Add("[sizedelta-law] a hardcoded MapPlatePx band constant is back in "
                    + PanelSrc + " - the band is derived from the well, not typed.");

            string plate = ReadSource(PlateSrc, failures);
            if (plate != null)
            {
                string pcode = StripComments(plate);
                if (!Regex.IsMatch(pcode, @"brt\s*\.\s*sizeDelta\s*=\s*new\s+Vector2"))
                    failures.Add("[sizedelta-law] DefenseMapPlate.BuildBand no longer writes the "
                        + "band height to sizeDelta - the LayoutElement it also sets is advisory, "
                        + "not the mechanism.");
                // ⚠ MATCHED AGAINST THE STRIPPED SHAPE, DELIBERATELY. StripComments blanks string
                //    LITERAL BODIES while preserving length, so `glyph + "\n" + label` reads as
                //    `glyph + "  " + label` in pcode (two spaces for the two source chars) and the
                //    legitimate one-line `glyph + " " + label` reads as ONE space. A regex written
                //    against the literal "\\n" here would never fire on anything - a hollow assert,
                //    which this suite's header forbids. Case 6 measures the rendered line count as
                //    well, so the property is pinned twice and neither pin is decorative.
                if (Regex.IsMatch(pcode, @"glyph\s*\+\s*""  ""\s*\+\s*label"))
                    failures.Add("[sizedelta-law] the plate mark has re-grown its hard \"\\n\" - a "
                        + "two-line mark in a box sized for one is how the label started painting "
                        + "on the report's sentences.");
                if (pcode.IndexOf("TextWrappingModes.NoWrap", StringComparison.Ordinal) < 0)
                    failures.Add("[sizedelta-law] plate labels are no longer NoWrap - TMP's default "
                        + "wrapping is what broke the word BREACH in half on the owner's frame.");
                if (pcode.IndexOf("ElarionUiKit.FontFloor", StringComparison.Ordinal) < 0)
                    failures.Add("[sizedelta-law] the plate label fit no longer respects "
                        + "ElarionUiKit.FontFloor - a label allowed below the kit legibility floor "
                        + "is unreadable rather than fitted, and the legend fallback never fires.");
            }

            if (failures.Count == before)
                notes.Add("[sizedelta-law] row band + plate band both reach sizeDelta; labels NoWrap, "
                    + "floored at ElarionUiKit.FontFloor");
        }

        // ── 6. [measured-plate] ──────────────────────────────────────────────────

        /// <summary>
        /// ⭐ BUILT AND MEASURED, at three landscape surfaces. This is the half of WO-1585 that
        /// a source lint cannot do: the real kit scroll zone, the real
        /// <see cref="DefenseMapPlate.BuildBand"/>, real TMP paragraphs shaped exactly as
        /// DefenseReportPanel.Paragraph shapes them, settled twice, then measured.
        ///
        /// <para>Two passes because the column reads a child's sizeDelta.y during
        /// CalculateLayoutInputVertical while a ContentSizeFitter WRITES it during
        /// SetLayoutVertical - one rebuild measures pre-fit paragraph heights.</para>
        ///
        /// <para>Asserted: the band gets the derived height (not 100), no text row's rect
        /// intersects the band, and every label on the plate is one line inside the band. The
        /// overlap predicate is <see cref="LayoutOracle.Overlaps"/> - the WO-1060 engine - so this
        /// panel is judged by the same rule as every other surface rather than a local copy.</para>
        /// </summary>
        private static void CaseMeasuredPlate(List<string> failures, List<string> notes)
        {
            for (int i = 0; i < Aspects.GetLength(0); i++)
                MeasureAt(Aspects[i, 0], Aspects[i, 1], failures, notes);
        }

        private static void MeasureAt(int w, int h, List<string> failures, List<string> notes)
        {
            string tag = "[measured-plate:" + w + "x" + h + "]";
            GameObject canvasGo = null;
            try
            {
                float scale = Mathf.Pow(w / RefW, 0.5f) * Mathf.Pow(h / RefH, 0.5f);
                if (scale <= 0f) { failures.Add(tag + " degenerate scaler."); return; }

                canvasGo = NewCanvas("drl-" + w + "x" + h, w / scale, h / scale);
                var rootRt = (RectTransform)canvasGo.transform;
                var panel = Region(rootRt, "Panel",
                    new Vector2(0.05f, PanelYMin), new Vector2(0.95f, PanelYMax));
                // FrameQuest bodyRight, read at source 2026-09-07 from ElarionUiKit.ZonesFor:
                // new Vector4(0.505f, 0.115f, 0.966f, 0.760f). The DETAIL well, not the list well.
                var detail = Region(panel, "DetailWell",
                    new Vector2(DetailZoneXMin, DetailZoneYMin), new Vector2(DetailZoneXMax, DetailZoneYMax));
                Settle(rootRt);

                var zone = ElarionUiKit.MakeScrollZone(detail, spacing: DetailSpacingPx, padding: DetailPadPx);
                if (zone == null || zone.content == null || zone.viewport == null)
                {
                    failures.Add(tag + " MakeScrollZone returned no content column - nothing to measure.");
                    return;
                }
                Settle(rootRt);

                float wellW = zone.viewport.rect.width, wellH = zone.viewport.rect.height;
                float derived = DefenseMapPlate.DeriveHeightPx(Mathf.Max(0f, wellW - 2f * DetailPadPx), wellH);

                // ── ⭐ THE RED FIXTURE. The defect, built the OLD way, MEASURED. ──────────
                //    A LayoutElement asking for 420px and no sizeDelta -- verbatim what
                //    DefenseReportPanel.BuildMapPlate did before WO-1585. If this band comes out
                //    at 420 then the kit column HAS started controlling child height and every
                //    assertion below has quietly stopped measuring what it claims (the same
                //    negative-fixture discipline as CaseDarkPlate's tan surface). It is
                //    deactivated immediately after measuring so it cannot pollute the overlap
                //    sweep. This is the half a source lint cannot do: it proves the MECHANISM,
                //    not the intent.
                var legacy = new GameObject("LegacyBand", typeof(RectTransform), typeof(LayoutElement));
                legacy.transform.SetParent(zone.content, false);
                var legacyLe = legacy.GetComponent<LayoutElement>();
                legacyLe.preferredHeight = LegacyAskPx;
                legacyLe.minHeight = LegacyAskPx;
                legacyLe.flexibleHeight = 0f;
                Settle(rootRt);
                Settle(rootRt);
                float legacyH = ((RectTransform)legacy.transform).rect.height;
                if (Mathf.Abs(legacyH - LegacyAskPx) <= 1.5f)
                    failures.Add(tag + " a band carrying ONLY a LayoutElement(" + Px(LegacyAskPx)
                        + ") measured " + Px(legacyH) + " - the kit scroll column now honours "
                        + "LayoutElement, so this suite has stopped measuring the WO-1585 mechanism "
                        + "and its sizeDelta assertions prove nothing.");
                legacy.SetActive(false);
                UnityEngine.Object.DestroyImmediate(legacy);
                Settle(rootRt);

                // The column, in the SHIPPING order: summary sentences, the plate, then the legend.
                var above = new List<RectTransform>
                {
                    Para(zone.content, "1st BREACH: Open ground at 24s (south-west of the Heart)"),
                    Para(zone.content, "They came from the west."),
                };
                DefenseMapPlate.Plate plate;
                var band = DefenseMapPlate.BuildBand(zone.content, Fixture(), derived, out plate);
                var below = new List<RectTransform>();
                for (int i = 0; i < DefenseMapPlate.Legend.Length; i++)
                    below.Add(Para(zone.content, DefenseMapPlate.Legend[i]));

                Settle(rootRt);
                Settle(rootRt);
                plate?.Relayout();
                Settle(rootRt);

                if (band == null || plate == null)
                {
                    failures.Add(tag + " BuildBand produced no band/plate - the diagram cannot be measured.");
                    return;
                }

                // 1. THE BAND GOT THE HEIGHT IT WAS GIVEN. 100 here is the shipped defect.
                float measured = band.rect.height;
                if (Mathf.Abs(measured - derived) > 1.5f)
                    failures.Add(tag + " the plate band measures " + Px(measured) + " but was built at "
                        + Px(derived) + ". A LayoutElement the kit column does not read is how it "
                        + "shipped at the 100px RectTransform default.");
                if (measured < DefenseMapPlate.PlateMinPx - 1.5f)
                    failures.Add(tag + " the plate band measures " + Px(measured) + ", under the "
                        + Px(DefenseMapPlate.PlateMinPx) + " floor - the marks crowd into a smear.");

                // 2. NO SENTENCE SHARES PIXELS WITH THE DIAGRAM. The WO-1060 predicate, not a copy.
                Rect bandRect = Box(band);
                var rows = new List<RectTransform>(above); rows.AddRange(below);
                for (int i = 0; i < rows.Count; i++)
                {
                    Rect r = Box(rows[i]);
                    float ow, oh;
                    if (r.width > 0.5f && r.height > 0.5f && LayoutOracle.Overlaps(r, bandRect, 0.5f, out ow, out oh))
                        failures.Add(tag + " text row " + i + " " + LayoutOracle.RectStr(r)
                            + " OVERLAPS the plate band " + LayoutOracle.RectStr(bandRect)
                            + " by " + ow.ToString("0.#") + "x" + oh.ToString("0.#")
                            + " px - the diagram and a sentence are on the same pixels.");
                }

                // 3. EVERY LABEL: one line, inside the plate. This is the "1st BREACH" defect.
                int labels = 0, glyphOnly = plate.GlyphOnlyFallbacks;
                foreach (var t in plate.Root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    labels++;
                    if (t.textWrappingMode != TextWrappingModes.NoWrap)
                        failures.Add(tag + " plate label \"" + t.text.Replace("\n", "/")
                            + "\" is not NoWrap - TMP will break it mid-word again.");
                    if (t.text.IndexOf('\n') >= 0)
                        failures.Add(tag + " plate label \"" + t.text.Replace("\n", "/")
                            + "\" carries a hard line break - the box is sized for one line.");
                    t.ForceMeshUpdate();
                    if (t.textInfo != null && t.textInfo.lineCount > 1)
                        failures.Add(tag + " plate label \"" + t.text.Replace("\n", "/")
                            + "\" renders on " + t.textInfo.lineCount + " lines at font "
                            + t.fontSize.ToString("0.#") + " in a " + Px(((RectTransform)t.transform).sizeDelta.x)
                            + " box - that is the wrapped \"1st / BREA / CH\" returning.");
                    if (t.fontSize < ElarionUiKit.FontHardFloor - 0.01f)
                        failures.Add(tag + " plate label \"" + t.text + "\" sits at font "
                            + t.fontSize.ToString("0.#") + ", under the kit hard floor "
                            + ElarionUiKit.FontHardFloor + " - the fallback is the glyph, never sub-legible words.");
                    Rect lr = Box((RectTransform)t.transform);
                    if (lr.width > 0.5f && !Contains(bandRect, lr))
                        failures.Add(tag + " plate label \"" + t.text.Replace("\n", "/") + "\" "
                            + LayoutOracle.RectStr(lr) + " hangs OUTSIDE the plate band "
                            + LayoutOracle.RectStr(bandRect) + " - it paints onto the report's rows.");
                }
                if (labels == 0)
                    failures.Add(tag + " the plate carries NO labels - this case would pass on an "
                        + "empty diagram, which is not evidence.");

                if (w == 2670 && h == 1200)
                    notes.Add(tag + " well " + Px(wellW) + "x" + Px(wellH) + ", band " + Px(measured)
                        + " (derived " + Px(derived) + "), " + labels + " labels one-line, "
                        + glyphOnly + " glyph-only, zero overlap; RED fixture: a LayoutElement-only "
                        + "band asking " + Px(LegacyAskPx) + " measured " + Px(legacyH));
            }
            catch (Exception ex)
            {
                failures.Add(tag + " THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        /// <summary>A well-formed record with a breach, a path and two losses - enough for the
        /// plate to draw every kind of mark, which is what the label assertions need.</summary>
        private static DefenseOutcomeRecord Fixture()
        {
            var r = DefenseOutcomeRecord.NewEmpty();
            r.Outcome = DefenseOutcome.Breached;
            if (r.Defender == null) r.Defender = new DefenderSnapshot();
            r.Defender.CoreX = 0f; r.Defender.CoreZ = 0f;
            r.Defender.CoreRadius = 12f; r.Defender.FrontRadius = 28f;
            if (r.Breaches == null) r.Breaches = new List<BreachRecord>();
            r.Breaches.Add(new BreachRecord { DisplayName = "Open ground", WorldX = -24f, WorldZ = -18f, AtSeconds = 24f });
            r.Breaches.Add(new BreachRecord { DisplayName = "West wall", WorldX = -30f, WorldZ = 4f, AtSeconds = 31f });
            if (r.Path == null) r.Path = new List<AttackPathPoint>();
            r.Path.Add(new AttackPathPoint { WorldX = -46f, WorldZ = -2f });
            r.Path.Add(new AttackPathPoint { WorldX = -24f, WorldZ = -10f });
            r.Path.Add(new AttackPathPoint { WorldX = -4f, WorldZ = -2f });
            if (r.Rows == null) r.Rows = new List<StructureOutcome>();
            r.Rows.Add(new StructureOutcome { DisplayName = "Watchtower", WorldX = -20f, WorldZ = 6f, DistanceFromCore = 21f });
            r.Rows.Add(new StructureOutcome { DisplayName = "Granary", WorldX = 14f, WorldZ = -8f, DistanceFromCore = 16f });
            return r;
        }

        /// <summary>A paragraph shaped exactly as DefenseReportPanel.Paragraph shapes one: TMP,
        /// wrapping, PreferredSize vertical fitter, unconstrained horizontally.</summary>
        private static RectTransform Para(Transform parent, string text)
        {
            var go = new GameObject("Para", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = ElarionUi.FontLabel;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return (RectTransform)go.transform;
        }

        // ── measured-case plumbing (mirrors RaidSelectionLayoutRegression) ───────

        /// <summary>FrameQuest bodyRight, read at source from ElarionUiKit.ZonesFor 2026-09-07.</summary>
        private const float DetailZoneXMin = 0.505f, DetailZoneYMin = 0.115f;
        private const float DetailZoneXMax = 0.966f, DetailZoneYMax = 0.760f;

        /// <summary>The detail column's MakeScrollZone arguments in DefenseReportPanel.BuildChrome.</summary>
        private const float DetailSpacingPx = 12f;
        private const int DetailPadPx = 28;

        private const string PlateSrc = "Assets/_Modules/Core/UI/DefenseMapPlate.cs";

        /// <summary>The height the OLD BuildMapPlate asked for through a LayoutElement and never
        /// got. Used by the RED fixture in case 6 - it is a historical number, not a knob.</summary>
        private const float LegacyAskPx = 420f;

        private static GameObject NewCanvas(string name, float w, float h)
        {
            // WORLD-SPACE and hand-sized: a ScreenSpace canvas in an edit-mode batchmode call
            // reports the editor's own 640x480 (the WO-1060 F8-5 root cause).
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.position = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static RectTransform Region(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void Settle(RectTransform root)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private static readonly Vector3[] _corners = new Vector3[4];

        private static Rect Box(RectTransform rt)
        {
            if (rt == null) return new Rect();
            rt.GetWorldCorners(_corners);
            float x0 = Mathf.Min(_corners[0].x, _corners[2].x), x1 = Mathf.Max(_corners[0].x, _corners[2].x);
            float y0 = Mathf.Min(_corners[0].y, _corners[2].y), y1 = Mathf.Max(_corners[0].y, _corners[2].y);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        private static bool Contains(Rect outer, Rect inner)
            => inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f
            && inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;

        // ── helpers ──────────────────────────────────────────────────────────────

        /// <summary>Blanks // and /* */ comments (and string/char literal bodies, so a quoted
        /// snippet inside a message cannot match either), preserving length and newlines so the
        /// remaining text is code only.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return src ?? string.Empty;
            var buf = src.ToCharArray();
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') { buf[i] = ' '; i++; }
                }
                else if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    while (i < n && !(src[i] == '*' && i + 1 < n && src[i + 1] == '/'))
                    {
                        if (src[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    if (i < n) { buf[i] = ' '; i++; }
                    if (i < n) { buf[i] = ' '; i++; }
                }
                else if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++; // keep the opening quote
                    while (i < n && src[i] != quote)
                    {
                        if (src[i] == '\\' && i + 1 < n)
                        {
                            buf[i] = ' '; i++;
                            if (src[i] != '\n') buf[i] = ' ';
                            i++;
                            continue;
                        }
                        if (src[i] == '\n') break;
                        buf[i] = ' ';
                        i++;
                    }
                    if (i < n && src[i] == quote) i++;
                }
                else i++;
            }
            return new string(buf);
        }

        private static string ReadSource(string path, List<string> failures)
        {
            try
            {
                if (!File.Exists(path)) { failures.Add("source missing: " + path); return null; }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add("could not read " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Reads a `const float NAME = 123f;` (or a readonly float) out of the source.
        /// A missing constant is a FAILURE, never a note - a quiet skip lands green.</summary>
        private static float ConstFloat(string src, string name, List<string> failures)
        {
            var m = Regex.Match(src, @"\b" + Regex.Escape(name) + @"\s*=\s*(-?[0-9]*\.?[0-9]+)f?\s*;");
            if (!m.Success)
            {
                failures.Add("[derived-pitch] constant " + name + " does not exist in " + PanelSrc
                    + " - the row budget cannot be measured.");
                return float.NaN;
            }
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static string Px(float v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "px";
        private static string Ratio(float v) => v.ToString("0.00", CultureInfo.InvariantCulture) + ":1";
    }
}
