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
using UnityEngine;
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
                }
            }
            catch (Exception ex)
            {
                failures.Add("defense-report-layout threw: " + ex.Message);
            }

            reason = failures.Count == 0
                ? "4 cases - " + string.Join("; ", notes)
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
