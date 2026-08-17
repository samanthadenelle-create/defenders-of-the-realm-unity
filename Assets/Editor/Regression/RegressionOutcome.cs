// =============================================================================
// RegressionOutcome  --  SKIPPED is a THIRD STATE, not a pass
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  No markers, no Run(out string) entry point:
// a helper, not a suite.
//
// THE ARITHMETIC BUG THIS ENDS (2026-08-16 coverage audit).
// A suite that cannot run headless -- GameStateService will not install, the state
// seam is not reflectable, a data file is absent -- has, until now, taken the
// "NAMED SKIP" shape:
//
//     if (!InstallState(gss, throwaway))
//     { reason = "X skipped: needs fleet"; return true; }
//
// Returning TRUE is right in one sense (a harness limitation is not a product
// defect, and a false FAIL trains the team to ignore red) and catastrophic in
// another: the caller's ONLY channel is the bool, so the skip lands in the GREEN
// column. In an environment where GameStateService will not install, SIX economy
// oracles asserted nothing at all and the gate marker still read FULL GREEN. The
// count that is supposed to be evidence of coverage was counting non-coverage.
//
// The fix is not to flip those to red. It is to make the reason string carry a
// machine-readable third state, so the reporting layer can subtract it:
//
//     REGRESSION_OK 118/125 suites -- 118 green, 0 red, 7 skipped
//
// 7 skipped is a number somebody can look at and act on. "125/125" was not.
//
// USAGE (suite side):
//     if (!InstallState(gss, throwaway))
//         return RegressionOutcome.Skip(out reason, "UPGRADE AUTHORITY",
//                                       "GameStateService state seam not reflectable (needs fleet)");
//
// For a suite that ran MOST of its cases but had to skip one section, use
// PartialSkip so the suite still counts as green while naming what it could not
// reach -- an honest partial is not the same event as a whole suite standing down.
//
// The token is also what the RegressionMarkerRegression RULE 4 hollow-pass ratchet
// accepts as the declaration of an INTENTIONAL stand-down: a guard-and-return-true
// that does NOT carry it is an undeclared hollow pass and fails the ratchet.
// =============================================================================

using System;

namespace DeNelle.Editor.Regression
{
    /// <summary>The three-state vocabulary shared by every registered oracle suite.</summary>
    public static class RegressionOutcome
    {
        /// <summary>
        /// Machine-readable marker for "this suite (or section) asserted NOTHING".
        /// Deliberately bracketed and screaming: it is meant to be greppable in a log
        /// and impossible to mistake for prose. It contains no `_OK` suffix, so it can
        /// never be confused with a gate marker literal by RULE 1/RULE 3.
        /// </summary>
        public const string SkipToken = "[SKIPPED]";

        /// <summary>Marker for "this suite ran, but one named section stood down".</summary>
        public const string PartialSkipToken = "[PARTIAL-SKIP]";

        /// <summary>
        /// Whole-suite stand-down. Returns TRUE (a harness limitation is not a product
        /// defect) but stamps the reason so the reporting layer counts it as SKIPPED
        /// rather than green.
        /// </summary>
        public static bool Skip(out string reason, string suite, string why)
        {
            reason = SkipToken + " " + (suite ?? "suite") + " stood down -- " + (why ?? "no reason given") +
                     " -- ASSERTED NOTHING (this is not a pass)";
            return true;
        }

        /// <summary>
        /// One section of an otherwise-running suite stood down. The suite still counts
        /// green (it did assert things); the note rides along so the log names the hole.
        /// </summary>
        public static string PartialSkip(string section, string why)
        {
            return PartialSkipToken + " " + (section ?? "section") + " -- " + (why ?? "no reason given");
        }

        /// <summary>True when a suite reason declares a whole-suite stand-down.</summary>
        public static bool IsSkipped(string reason)
        {
            return !string.IsNullOrEmpty(reason) && reason.IndexOf(SkipToken, StringComparison.Ordinal) >= 0;
        }

        /// <summary>True when a green reason carries at least one named partial stand-down.</summary>
        public static bool HasPartialSkip(string reason)
        {
            return !string.IsNullOrEmpty(reason) && reason.IndexOf(PartialSkipToken, StringComparison.Ordinal) >= 0;
        }
    }
}
