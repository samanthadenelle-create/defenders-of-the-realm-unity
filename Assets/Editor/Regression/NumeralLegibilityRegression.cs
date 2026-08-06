// =============================================================================
// NumeralLegibilityRegression [numeral-legibility] - no UI font may draw the
// numeral 1 as a bare vertical stroke.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT BROKE (captured, not inferred - owner defect 2026-08-05):
//   Builds/ui-capture/QueueCardRail_2670x1200.png rendered the HUD Builders chip's
//   "Builders 1/2 | Training 1" as THREE IDENTICAL VERTICAL MARKS carrying three
//   different meanings (two counts and a separator). docs/qa/UI_REVIEW_2026-08-05
//   _seeker.md reports the same bare stroke on "Echoes 1/6", the gold "1|3" chip and
//   the "SKILL |75" wisdom chip. The SAME capture shows the Work Queue panel drawing
//   "BUILDERS 1/2 busy" with a properly flagged 1 - two surfaces, two fonts, one
//   digit, so the FONT was the variable, not the string.
//
//   Root cause, measured off the assets: the Obsidian kit's FontRole.Body asset
//   (Resources/RpgUi/font/font_body = Alata-Regular) draws '1' with 7.23 units of ink
//   in its 64pt space, against a lowercase 'l' at 6.84 and a '|' at 6.14 - a bare stem
//   with no flag and no foot. Confirmed at the source outline too (Alata-Regular.ttf,
//   1000 upem: '1' 113 wide vs 'l' 107 vs '|' 96), so it is the TYPEFACE, not the SDF
//   atlas: regenerating font_body from Alata cannot fix it.
//
// THE LAW THIS PINS: whatever font each typographic role EFFECTIVELY renders with -
// the role asset when it passes ElarionUiKit's numeral gate, or the default chain it
// falls back to - must draw a 1 that is measurably wider than that same font's own
// bare strokes ('|' / 'l'). This is a MEASUREMENT off the live glyph metrics (the very
// numbers TMP lays text out with), not a font-name allowlist, so it stays true through
// any future font swap and cannot be satisfied by renaming an asset.
//
// Undecidable cases (a glyph absent, an unpopulated character table) never FAIL - we
// do not condemn a font on absence of evidence; glyph COVERAGE / tofu is owned by
// HudUiRegression's tofu oracle, which is the right place for it.
//
// Markers: NUMERAL_LEGIBILITY_OK / NUMERAL_LEGIBILITY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.NumeralLegibilityRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "numeral-legibility suite", () => { if (!DeNelle.Editor.Regression.NumeralLegibilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[numeral-legibility] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class NumeralLegibilityRegression
    {
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("NUMERAL_LEGIBILITY_OK - " + reason);
            else Debug.LogError("NUMERAL_LEGIBILITY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                // Every role, not just Body: Title and Stamp render counts too (combat
                // stamps are literally "x3" / damage numbers), and the gate is the same.
                foreach (ElarionUiKit.FontRole role in Enum.GetValues(typeof(ElarionUiKit.FontRole)))
                {
                    bool ok;
                    string line = ElarionUiKit.NumeralLegibilityReport(role, out ok);
                    if (ok) notes.Add(line);
                    else failures.Add("[" + role + "] " + line +
                                      " - a HUD made of counts cannot ship a font whose 1 reads as '|' or 'l'");
                }

                // The gate itself must still be armed. A zeroed ratio is the documented
                // owner veto, so it is a NOTE (a deliberate choice), never a silent pass.
                if (ElarionUiKit.NumeralStrokeRatio <= 0f)
                    notes.Add("gate DISABLED (ElarionUiKit.NumeralStrokeRatio = 0) - owner veto in force; " +
                              "role fonts are accepted without a numeral check");
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "NUMERAL LEGIBILITY OK - every typographic role renders its numeral 1 " +
                         "distinguishably from its own bare strokes" + noteStr;
                return true;
            }
            reason = "numeral-legibility FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }
    }
}
