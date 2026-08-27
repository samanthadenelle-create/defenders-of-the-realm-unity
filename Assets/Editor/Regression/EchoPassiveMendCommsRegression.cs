// =============================================================================
// EchoPassiveMendCommsRegression -- headless oracle for WO-1231: passive Echo
// mending must SAY what it is doing, and must say where the materials went.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS (owner felt-test 2026-08-26: "if its passive we should
// somehow let them know"): passive repair was CORRECT and COMPLETELY SILENT. A
// sweep of Assets/_Modules for player-facing strings about Echo mending returned
// ZERO -- every hit was a FlowTrace line, an editor [Tooltip] or a log message.
// The player was never told the walls mend themselves, never told MORE ECHOES
// MENDS FASTER, and -- the P1 half -- never told that mending DEBITS WOOD AND
// IRON, or that it had STALLED because the wallet was empty. Materials left with
// no cause shown and repair stopped with no reason shown.
//
// HOW THIS WAS PROVEN RED (WO-1138). Every assertion below is keyed to a string
// that did not exist ANYWHERE in the tree before this change. On the pre-change
// HEAD, `git grep -n "PASSIVE MENDING"`, `git grep -n "spent while mending"` and
// `git grep -n "waiting for materials"` followed by a named resource all return ZERO
// hits under Assets/ outside a FlowTrace line, and
// EchoMendCopy did not exist -- so cases A, B, D, E, F and H could not have
// passed. Case I is red the other way round: it FAILS if anyone re-adds the
// retired repair ASSIGNMENT, which is the way this ticket could be "fixed" wrongly.
//
// ⛔ WHAT THIS ORACLE DELIBERATELY DOES NOT DO: it does not assert a rate value, a
// cost, or the count x level math. WO-1231 is COMMUNICATION ONLY and the owner
// ruled 2026-08-26 that the material SPEND STAYS. An oracle that pinned the
// economy here would turn a comms ticket into an economy freeze.
//
// ⛔ AND IT NEVER RE-OPENS THE ASSIGNMENT DOOR. Repair is PASSIVE and COUNT-DRIVEN
// (owner ruling WO-1108); EchoAssignments refuses AssignRepair in as many words.
// Case I lints that neither Echo card file has grown a repair verb on the strength
// of the explainer existing.
//
// NO HOLLOW PASSES: every case below ASSERTS. There is no "dependency missing ->
// return true" path anywhere in this file -- a source file that cannot be read is
// a FAILURE (the surface is gone), not a skip, because "the file is missing" is
// exactly the regression this lint exists to catch.
//
// Shape mirrors OfflineClaimFanOutRegression: public static bool Run(out string reason).
// Pure data + source lint -- no scene, no GameState install, no PlayerPrefs.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class EchoPassiveMendCommsRegression
    {
        private const string CardViewPath = "Assets/_Modules/Village/Harvest/EchoCardView.cs";
        private const string CardVmPath = "Assets/_Modules/Village/Harvest/EchoCardVM.cs";
        private const string PopupPath = "Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs";
        private const string ServicePath = "Assets/_Modules/Village/Harvest/EchoRepairService.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            CaseA_CopyExistsAndSaysTheRightThings(failures);
            CaseB_AsciiOnly(failures);
            CaseC_RateLineIsLiveNotALiteral(failures);
            CaseD_StallChipNamesTheResource(failures);
            CaseE_SpendAttribution(failures);
            CaseF_AStallIsNewsOnItsOwn(failures);
            CaseG_AwaySummaryGateSeesMendNews(failures);
            CaseH_BothSurfacesActuallyRenderTheCopy(failures);
            CaseI_NoRepairAssignmentCameBack(failures);

            if (failures.Count > 0)
            {
                reason = "echo-passive-mend-comms FAILED: " + string.Join(" | ", failures.ToArray());
                return false;
            }
            reason = "echo-passive-mend-comms OK: 9/9 cases -- explainer + live rate + spend note on the " +
                     "Echo card, spend attribution + stall in the away summary, ASCII-clean, no repair " +
                     "assignment re-added.";
            return true;
        }

        // -- A. the copy exists, and says the three things that were missing ----

        private static void CaseA_CopyExistsAndSaysTheRightThings(List<string> f)
        {
            if (string.IsNullOrEmpty(EchoMendCopy.Header))
                f.Add("A: EchoMendCopy.Header is empty -- the card block has no heading");

            string ex = EchoMendCopy.Explainer ?? "";
            if (ex.Length == 0)
                f.Add("A: EchoMendCopy.Explainer is empty -- the player is still never told mending exists");
            if (ex.IndexOf("Echo", System.StringComparison.Ordinal) < 0)
                f.Add("A: the explainer never says 'Echo' -- it cannot connect the mending to the roster");
            // The monetisation-relevant fact that was a total secret: more Echoes = faster.
            if (ex.IndexOf("faster", System.StringComparison.OrdinalIgnoreCase) < 0)
                f.Add("A: the explainer never says mending gets FASTER with more Echoes -- the one fact " +
                      "WO-1231 calls out as a total secret");

            string spend = EchoMendCopy.SpendNote ?? "";
            if (spend.IndexOf("Wood", System.StringComparison.Ordinal) < 0 ||
                spend.IndexOf("Iron", System.StringComparison.Ordinal) < 0)
                f.Add("A: the spend note does not name Wood and Iron -- the debit is still unattributed " +
                      "on the card (P1: materials appear to vanish)");
        }

        // -- B. ASCII only (a non-ASCII glyph is a tofu box on device) ----------

        private static void CaseB_AsciiOnly(List<string> f)
        {
            var report = new EchoMendReport
            {
                Repairs = 2, HealthFraction = 0.42f, SpentWood = 120, SpentIron = 40,
                SpentStone = 5, SpentCrystals = 1, StalledResource = "Wood",
            };
            AssertAscii(f, "Header", EchoMendCopy.Header);
            AssertAscii(f, "Explainer", EchoMendCopy.Explainer);
            AssertAscii(f, "SpendNote", EchoMendCopy.SpendNote);
            AssertAscii(f, "RateNone", EchoMendCopy.RateNone);
            AssertAscii(f, "RateLine", EchoMendCopy.RateLine(0.35f / 3600f));
            AssertAscii(f, "StallChip", EchoMendCopy.StallChip("Wood"));
            AssertAscii(f, "AwayMendedLine", EchoMendCopy.AwayMendedLine(report));
            AssertAscii(f, "AwaySpentLine", EchoMendCopy.AwaySpentLine(report));
            AssertAscii(f, "AwayStallLine", EchoMendCopy.AwayStallLine(report));
        }

        private static void AssertAscii(List<string> f, string what, string s)
        {
            if (s == null) { f.Add("B: " + what + " is null"); return; }
            for (int i = 0; i < s.Length; i++)
                if (s[i] > '~' || (s[i] < ' ' && s[i] != '\n'))
                {
                    f.Add("B: " + what + " carries a non-ASCII char U+" + ((int)s[i]).ToString("X4") +
                          " at " + i + " -- renders as a tofu box on device");
                    return;
                }
        }

        // -- C. the rate line is BOUND to the calculator, never a literal -------

        private static void CaseC_RateLineIsLiveNotALiteral(List<string> f)
        {
            // Zero roster -> the honest "none", never a "0%" that reads as a broken system.
            if (EchoMendCopy.RateLine(0f) != EchoMendCopy.RateNone)
                f.Add("C: a zero mend rate does not render the honest RateNone line");

            string one = EchoMendCopy.RateLine(0.35f / 3600f);      // one Lv1 Echo's order of magnitude
            string two = EchoMendCopy.RateLine(0.70f / 3600f);      // the "wake another Echo" case
            if (one.IndexOf('%') < 0)
                f.Add("C: the rate line has no '%' -- it is not stating a rate in player terms");
            if (one.IndexOf("35", System.StringComparison.Ordinal) < 0)
                f.Add("C: RateLine(0.35/h) does not render '35' -- the fraction is not being converted " +
                      "to a percentage per hour");
            // The load-bearing one: a HARDCODED sentence would be identical for both rates,
            // which is exactly how a "we told the player" claim goes stale after a balance edit.
            if (one == two)
                f.Add("C: the rate line is IDENTICAL at two different rates -- it is hardcoded, not bound " +
                      "to EchoBonusCalculator.RepairFractionsPerSecond()");
            // A slow roster must never round to a flat 0 and print a lie about a working system.
            string slow = EchoMendCopy.RateLine(0.02f / 3600f);
            if (slow == EchoMendCopy.RateNone)
                f.Add("C: a slow-but-nonzero rate collapses to the 'none' line");
            if (slow.IndexOf("2", System.StringComparison.Ordinal) < 0)
                f.Add("C: a 2%/h rate does not render '2' -- small rates are being rounded away");
        }

        // -- D. the stall names the resource (the actionable half) --------------

        private static void CaseD_StallChipNamesTheResource(List<string> f)
        {
            string chip = EchoMendCopy.StallChip("Wood");
            if (chip.IndexOf("PAUSED", System.StringComparison.Ordinal) < 0)
                f.Add("D: the stall chip does not carry the WORD 'PAUSED' -- a colourblind player has " +
                      "no cue at all (greyscale law)");
            if (chip.IndexOf("waiting for materials", System.StringComparison.Ordinal) < 0)
                f.Add("D: the stall chip does not say 'waiting for materials'");
            if (chip.IndexOf("Wood", System.StringComparison.Ordinal) < 0)
                f.Add("D: the stall chip does not NAME the short resource -- 'waiting for materials' is " +
                      "only actionable once the player knows which one to go get");
            // The un-named fallback must still be a stall, never an empty string.
            if (EchoMendCopy.StallChip("").IndexOf("PAUSED", System.StringComparison.Ordinal) < 0)
                f.Add("D: the un-named stall fallback lost the PAUSED cue");
        }

        // -- E. THE ONE THAT MATTERS: the spend is attributed -------------------

        private static void CaseE_SpendAttribution(List<string> f)
        {
            var r = new EchoMendReport { Repairs = 1, HealthFraction = 0.12f };
            r.AddSpend(new DeNelle.Core.Catalog.ResourceCost { wood = 120, iron = 40 });

            if (r.SpentWood != 120 || r.SpentIron != 40)
                f.Add("E: AddSpend did not fold the real cost in (wood=" + r.SpentWood +
                      ", iron=" + r.SpentIron + ") -- the away summary would report a number the " +
                      "wallet was never charged");

            string line = EchoMendCopy.AwaySpentLine(r);
            if (line.IndexOf("spent while mending", System.StringComparison.Ordinal) < 0)
                f.Add("E: the away summary has no 'spent while mending' row -- the debit is STILL " +
                      "unattributed, which is the P1 defect");
            if (line.IndexOf("-120 Wood", System.StringComparison.Ordinal) < 0 ||
                line.IndexOf("-40 Iron", System.StringComparison.Ordinal) < 0)
                f.Add("E: the spend row does not render the signed per-resource amounts (got '" + line + "')");

            string mended = EchoMendCopy.AwayMendedLine(r);
            if (mended.IndexOf("12", System.StringComparison.Ordinal) < 0 || mended.IndexOf('%') < 0)
                f.Add("E: the mended row does not render the wall health gained as a percentage (got '" +
                      mended + "')");

            // GOOD PATH, asserted: nothing happened -> no rows, no news, no popup.
            var empty = EchoMendReport.None;
            if (empty.HasContent)
                f.Add("E: an empty mend report claims to have content -- the away summary would open on " +
                      "a window in which mending did nothing");
            if (EchoMendCopy.AwaySpentLine(empty).Length != 0)
                f.Add("E: an empty mend report still renders a spend row");
            if (EchoMendCopy.AwayStallLine(empty).Length != 0)
                f.Add("E: an empty mend report still renders a stall row");
        }

        // -- F. a stall alone is news (nothing gathered, nothing mended) --------

        private static void CaseF_AStallIsNewsOnItsOwn(List<string> f)
        {
            var stalled = new EchoMendReport { StalledResource = "Wood" };
            if (!stalled.Stalled)
                f.Add("F: a named short resource does not read as Stalled");
            if (!stalled.HasContent)
                f.Add("F: a stall alone is not treated as news -- the player would return to walls that " +
                      "never mended, with no reason given anywhere (the state that used to live only " +
                      "in a FlowTrace)");
            string line = EchoMendCopy.AwayStallLine(stalled);
            if (line.IndexOf("Wood", System.StringComparison.Ordinal) < 0 ||
                line.IndexOf("paused", System.StringComparison.OrdinalIgnoreCase) < 0)
                f.Add("F: the away stall row does not say what paused or what ran out (got '" + line + "')");
        }

        // -- G. the popup's trigger gate can SEE mend news ----------------------

        private static void CaseG_AwaySummaryGateSeesMendNews(List<string> f)
        {
            // The exact window WO-1231 calls out: gathered nothing, spent 400 Wood mending.
            var result = new OfflineHarvestResult { AwaySeconds = 7200.0 };
            result.Mend = new EchoMendReport { Repairs = 3, HealthFraction = 0.8f, SpentWood = 400 };

            if (result.Total != 0)
                f.Add("G: test setup drift -- the zero-haul result is not zero");
            if (!result.HasMendNews)
                f.Add("G: a zero-haul window with a 400 Wood mend spend reports no mend news -- the " +
                      "summary gate would suppress the ONE report that explains the missing Wood");

            // GOOD PATH, asserted: a genuinely empty window must still suppress the reveal,
            // or every cold load opens a popup saying nothing.
            var quiet = new OfflineHarvestResult { Mend = EchoMendReport.None };
            if (quiet.Total != 0 || quiet.HasMendNews)
                f.Add("G: an empty window claims news -- the welcome-back reveal would fire on every " +
                      "launch with nothing to say");

            // The staleness guard's floor: a report no claim produced must carry sequence 0,
            // so it can never accidentally MATCH a real claim's sequence and get attached
            // (a re-reported spend is a worse lie than the silence this ticket removed).
            if (EchoMendReport.None.ClaimSequence != 0)
                f.Add("G: a fresh EchoMendReport does not default to ClaimSequence 0 -- the away " +
                      "summary's stale-report guard has no safe floor");
        }

        // -- H. both surfaces actually render the copy --------------------------
        //  Case A proves the sentences EXIST. This proves they REACH A SCREEN. A copy
        //  class nothing binds is the same silence with extra steps.

        private static void CaseH_BothSurfacesActuallyRenderTheCopy(List<string> f)
        {
            // FIXTURE HEALTH IS ASSERTED, NOT ASSUMED (hollow-pass sweep 2026-08-27).
            // Every lint below used to sit inside a bare `if (src != null)`, so a missing or
            // empty source file made the case check ZERO things and still read green. Each
            // fixture now has an EXPLICIT negative branch that FAILS and names exactly which
            // lint went unchecked -- the fixture-absent arm of the three-way rule.
            string cardView = ReadCode(f, CardViewPath);
            if (!HasCodeBody(cardView))
                f.Add("H: no readable code body for " + CardViewPath + " -- the Echo card's " +
                      "mend-copy binding AND its stall surface went UNCHECKED this run");
            else
            {
                if (cardView.IndexOf("EchoMendCopy", System.StringComparison.Ordinal) < 0 &&
                    cardView.IndexOf("MendExplainerText", System.StringComparison.Ordinal) < 0)
                    f.Add("H: EchoCardView renders no passive-mending copy -- the Echo card is silent again");
                if (cardView.IndexOf("MendStall", System.StringComparison.Ordinal) < 0)
                    f.Add("H: EchoCardView has no stall surface -- 'waiting for materials' is back to " +
                          "being FlowTrace-only");
            }

            string vm = ReadCode(f, CardVmPath);
            if (!HasCodeBody(vm))
                f.Add("H: no readable code body for " + CardVmPath + " -- the VM-sources-the-copy " +
                      "(MVVM strict) lint went UNCHECKED this run");
            else if (vm.IndexOf("EchoMendCopy", System.StringComparison.Ordinal) < 0)
                f.Add("H: EchoCardVM no longer sources the mend copy -- the View would have to invent " +
                      "its own strings (MVVM strict violation)");

            string popup = ReadCode(f, PopupPath);
            if (!HasCodeBody(popup))
                f.Add("H: no readable code body for " + PopupPath + " -- the away-summary spend-row " +
                      "lint went UNCHECKED this run");
            else if (popup.IndexOf("AwaySpentLine", System.StringComparison.Ordinal) < 0)
                f.Add("H: WelcomeBackPopup does not render the spend row -- the while-you-were-away " +
                      "summary is back to reporting only half the window");

            string service = ReadCode(f, ServicePath);
            if (!HasCodeBody(service))
                f.Add("H: no readable code body for " + ServicePath + " -- the offline-mend-report " +
                      "publication lint went UNCHECKED this run");
            else if (service.IndexOf("LastOfflineMendReport", System.StringComparison.Ordinal) < 0)
                f.Add("H: EchoRepairService no longer publishes an offline mend report -- nothing feeds " +
                      "the away summary");
        }

        // -- I. the retired ASSIGNMENT did not come back ------------------------

        private static void CaseI_NoRepairAssignmentCameBack(List<string> f)
        {
            // Comments AND string literals stripped, so this file's own explanatory prose --
            // and the FlowTrace lines -- can never trip or satisfy the lint.
            // Same fixture-health rule as case H: an absent source is a FAILURE that names the
            // unchecked lint, never a silent green. A source that strips to an EMPTY body is
            // treated identically -- an empty haystack "proves" the needle is absent.
            string vm = ReadCode(f, CardVmPath);
            string vmCode = StrippedBody(vm);
            if (vmCode == null)
                f.Add("I: no readable code body for " + CardVmPath + " -- the AssignRepair and " +
                      "RepairTaskChip lints on the VM went UNCHECKED this run; the retired repair " +
                      "ASSIGNMENT could have come back unseen");
            else
            {
                if (vmCode.IndexOf("AssignRepair", System.StringComparison.Ordinal) >= 0)
                    f.Add("I: EchoCardVM calls AssignRepair -- repair is PASSIVE and count-driven " +
                          "(owner ruling WO-1108); WO-1231 is communication only");
                if (vmCode.IndexOf("RepairTaskChip", System.StringComparison.Ordinal) >= 0)
                    f.Add("I: the retired WO-811 repair picker chip is back on the Echo card");
            }

            string cardView = ReadCode(f, CardViewPath);
            string cardViewCode = StrippedBody(cardView);
            if (cardViewCode == null)
                f.Add("I: no readable code body for " + CardViewPath + " -- the AssignRepair lint on " +
                      "the View went UNCHECKED this run");
            else if (cardViewCode.IndexOf("AssignRepair", System.StringComparison.Ordinal) >= 0)
                f.Add("I: EchoCardView calls AssignRepair -- there is nothing to assign");
        }

        // -- shared -------------------------------------------------------------

        /// <summary>
        /// Reads a source file the surfaces live in. A missing/unreadable file is a
        /// FAILURE, never a skip: "the file is gone" is precisely the regression these
        /// lints exist to catch, and returning true on it is the hollow pass this repo
        /// treats as a P1 defect.
        /// </summary>
        private static string ReadCode(List<string> f, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    f.Add("lint: " + path + " does not exist -- the surface it hosts is gone");
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (System.Exception e)
            {
                f.Add("lint: " + path + " could not be read (" + e.GetType().Name + ": " + e.Message + ")");
                return null;
            }
        }

        /// <summary>
        /// True when a source read produced an actual code body to lint. An empty (or
        /// whitespace/comment-only) file is NOT a fixture: an empty haystack makes every
        /// "the needle is absent" lint pass while checking nothing.
        /// </summary>
        private static bool HasCodeBody(string src)
        {
            return !string.IsNullOrEmpty(src) && StrippedBody(src) != null;
        }

        /// <summary>
        /// The comment- and string-stripped code body, or null when there is nothing to
        /// lint. Callers turn the null into a NAMED failure -- never a skip.
        /// </summary>
        private static string StrippedBody(string src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            string code = RegressionSourceText.StripCommentsAndStrings(src);
            if (code == null || code.Trim().Length == 0) return null;
            return code;
        }
    }
}
