#if UNITY_EDITOR
// =============================================================================
// FreshSaveFtueLaneRegression [freshsave-ftue-lane]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// Markers: FRESH_SAVE_FTUE_LANE_OK / FRESH_SAVE_FTUE_LANE_FAIL.
//
// WHAT WAS BLIND (WO-1500, captured evidence, 2026-09-06). Across ALL FIVE fleet
// logs taken that day there were ZERO [Flow:Onboard*] lines; every one carried
// 'raid.first_completed already latched' and echoes=4/6. Every run booted a
// RETURNING save, so every fresh-save assertion in AutoPilotDriver went N/A and the
// fleet reported green while asserting nothing whatsoever about the first ten
// minutes. The only artefacts of minute one were PNGs from 2026-09-01 - five days
// and the entire Manage 2000-block earlier. For a retention-limited product that is
// the worst place in the game to be unobserved.
//
// WHAT THIS ORACLE CAN AND CANNOT DO, said plainly so nobody reads more into a green
// line than it carries. It CANNOT run the fleet - no scene, no play mode, no player
// exe. What it pins is the WIRING that makes the nightly observation exist at all:
// the phase is in the driver's sequence, it still founds its own New Game, it still
// reads the three things that make a claim honest, it prints the marker the fleet
// judges it by, and the fleet script still asks for that marker and REFUSES without
// it. Every one of those is decidable from source text, and every one of them is a
// thing that, deleted quietly, would restore the exact 2026-09-06 silence while
// leaving a phase list that still looks complete.
//
//   RULE 0 [fixture-exists]  ⛔ THE ONE THAT MUST STAY FIRST. Every file this suite
//          guards is asserted READABLE before any rule runs, and a missing one is a
//          FAILURE naming it - never a skip, never a green.
//          ⚠ THIS SUITE WAS RED FOR EXACTLY THIS ON ITS FIRST GATE RUN
//          (Builds/reg-wave10.log: HollowPassScanner arm D-vacuous-against-absent-
//          fixture, FreshSaveFtueLaneRegression.cs:150). Every rule sat inside
//          `if (driver != null) { ... }`, so a renamed or deleted AutoPilotDriver.cs
//          would have made this method check ZERO things and still answer green -
//          the suite would then ACTIVELY ASSERT that the fresh-save lane is wired on
//          a tree where it is gone. That is the same shape as the coverage hole
//          WO-1500 exists to close, one layer up: a pass with nothing behind it.
//          It also survived a careful reading, which is the lesson. ReadRepoFile
//          really does record a failure for a missing file, so the method could not
//          actually green - but THE SHAPE carries no such proof, and the next rule
//          added inside that block would have inherited the hazard for free. The
//          sanctioned shape is producer-reports-then-return, and everything below
//          RULE 0 is deliberately UN-NESTED at method level. Do not re-wrap it.
//
//   RULE 1 [phase-wired]  AutoPilotDriver declares AssertFreshSaveFtue, RUNS it from
//          the phase sequence, and gives it a TimeoutFor entry. A phase that exists
//          but is never invoked is the same as no phase; a phase with no timeout
//          entry silently takes the 30s default and is cut off mid-walk.
//
//   RULE 2 [founds-its-own-town]  The phase body still calls ResetToNewGame and still
//          reads Onboarded / LastHarvestClaimMs / EverBuiltStructureIds. This is the
//          load-bearing rule: the moment the lane stops founding its own save it
//          becomes another phase that goes N/A on whatever save the machine booted,
//          which is precisely how the coverage was lost the first time.
//
//   RULE 3 [claims-nothing]  The phase still asserts the FIRST claim on that town -
//          WasFreshClock, a zero window, and no WelcomeBackPopup on screen. This is
//          the FLOW half of the owner's 2026-09-05 "YOUR REALM WORKED FOR 8h 22m" on
//          START NEW; OfflineHarvestRegression case 6 pins the MODEL half, and the
//          two are deliberately separate - a fixture cannot prove the shipped path
//          runs, and the shipped path cannot be run in editmode.
//
//   RULE 4 [beats-observed]  The phase still reads TutorialFlow.CurrentStepId, so the
//          guide beats are NAMED in the log by id. "The FTUE ran" is not observation;
//          a list of the beats a new player reached is.
//
//   RULE 4b [honest-verdict]  The three things that keep the marker from lying, all
//          found and fixed on 2026-09-07 BEFORE the lane had ever run:
//            * RanThisSession is checked as a PRECONDITION, ahead of the reset. It
//              latches in TutorialFlow.Bootstrap and no New Game clears it, so inside
//              a full sweep the reloaded flow correctly parks Finished, the beat walk
//              records nothing, and the lane would have failed a healthy build nightly.
//            * an unmeetable precondition prints FRESH_SAVE_FTUE_SOFT - a distinct,
//              non-OK line - so the fleet reports the question as unanswered rather
//              than either passing it or raising a false ticket.
//            * the OK marker is GATED on a live flow AND at least one named beat.
//              Ungated, a run with ff.tutorialv2 OFF printed OK with beats=0: green,
//              nothing asserted - the exact 2026-09-06 shape, inside the lane written
//              to end it.
//          Plus the teardown: the lane completes the town it founded through SkipAll,
//          so the save it leaves is a RETURNING one. A fresh save left on disk arms the
//          flow on the next boot, latches RanThisSession, and the lane's own
//          precondition then refuses - a nightly lane that works exactly once.
//
//   RULE 5 [lane-judged]  run-autopilot-fleet.ps1 still declares the freshsave-ftue
//          lane, still points it at the FreshSaveFtue phase filter, still greps the
//          phase's marker per instance, and still turns a miss into FLEET_LANE_FAIL
//          with a non-zero exit. Without the refusal the lane is a suggestion: this
//          repo's runners exit 0 on refusals and FAILs (CLAUDE.md section 8), and a
//          phase that went N/A still finishes the run perfectly.
//
//   RULE 6 [bag-map-rail-gone]  InventoryUIBuilder carries no `case RailMap` branch
//          and no KeyRailMapSoon "soon" label, and RailEntryCount equals the number
//          of BuildRailEntry calls. The owner's ruling was HIDE, never "label it as
//          coming" (CLAUDE.md section 7: the Realm Map's ONE public door is the
//          Journey deck card). PublicNavigationRetirementRegression pins that the
//          Bag builds no RailMap ENTRY; this pins that the SECTION behind it is gone
//          too, which is what WO-1396 left half-done and WO-1500 finished.
//
// RED-FIRST - one-line mutations that turn this suite red on the fixed tree: re-wrap
// the rules in `if (driver != null)` and delete the RULE 0 gate (arm D again); delete
// the RunPhase("AssertFreshSaveFtue" line; drop ResetToNewGame from the phase body;
// drop the WasFreshClock assertion; remove the Marker entry from the $Lanes table;
// change FLEET_LANE_FAIL into a warning; restore `case RailMap:` to NextStepLine; set
// RailEntryCount back to 8.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.FreshSaveFtueLaneRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class FreshSaveFtueLaneRegression
    {
        private const string DriverSrc = "Assets/_Modules/DevTools/AutoPilotDriver.cs";
        private const string FleetSrc  = "run-autopilot-fleet.ps1";
        private const string BagSrc    = "Assets/_Modules/Village/Hero/InventoryUIBuilder.cs";

        /// <summary>The phase name, used as the driver method, the RunPhase label AND the
        /// --phases filter token. One string on purpose: three copies is how a lane ends up
        /// pointing at a phase that no longer exists under that name.</summary>
        private const string PhaseName = "AssertFreshSaveFtue";

        /// <summary>The lane's key in the fleet script's $Lanes table.</summary>
        private const string LaneKey = "freshsave-ftue";

        /// <summary>The --phases token the lane passes (a substring match in the driver).</summary>
        private const string PhaseFilterToken = "FreshSaveFtue";

        /// <summary>The marker the PHASE prints and the FLEET greps. Held as a const so this
        /// oracle never spells it inline in a log sink - the driver owns the emission.</summary>
        private const string PhaseMarker = "FRESH_SAVE_FTUE_OK";

        /// <summary>How much source after the phase's signature counts as "inside" it. The
        /// method spans ~21,500 chars including its evidence comments (measured 2026-09-07, and
        /// its success marker sits at ~18,600), so 26,000 covers it with room. A window is used
        /// rather than a brace walk because every assertion below is "does the phase touch this
        /// seam", which a window answers exactly and cheaply.
        /// ⚠ If a needle below starts failing on a phase that clearly still names it, this
        /// number is the first suspect - the window silently truncates, and a truncated slice
        /// reports a missing seam identically to a deleted one.</summary>
        private const int PhaseWindowChars = 26000;

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("FRESH_SAVE_FTUE_LANE_OK - " + reason);
            else Debug.LogError("FRESH_SAVE_FTUE_LANE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            string driver = ReadRepoFile(DriverSrc, failures);
            string fleet  = ReadRepoFile(FleetSrc, failures);
            string bag    = ReadRepoFile(BagSrc, failures);

            // =====================================================================
            //  RULE 0 [fixture-exists] - THE GATE, AND IT COMES FIRST
            // ---------------------------------------------------------------------
            //  ⛔ NEVER NEST THE RULES BELOW INSIDE `if (driver != null) { ... }`.
            //  That is precisely how this file went red on its first gate run
            //  (HollowPassScanner arm D-vacuous-against-absent-fixture, reported
            //  against FreshSaveFtueLaneRegression.cs:150): with EVERY assertion
            //  inside a positive-existence guard and nothing asserting the fixture
            //  exists, a renamed or deleted AutoPilotDriver.cs makes this method
            //  check ZERO things and still answer GREEN - the suite would then
            //  ACTIVELY ASSERT that the fresh-save lane is wired, on a tree where
            //  it is gone. That is the same failure shape as the coverage hole
            //  WO-1500 was opened on, one layer up.
            //
            //  It fooled a reading of the code, too, which is why the shape and not
            //  the intent is what matters: ReadRepoFile DOES add a failure for a
            //  missing file, so the method could not really green - but the guard
            //  shape carries no such proof, and the next edit that adds a rule
            //  inside the same block inherits the hazard for free. The sanctioned
            //  shape is producer-reports-then-return: assert existence HERE, fail
            //  NAMING what is missing, and leave every rule below un-nested.
            // =====================================================================
            if (driver == null || fleet == null || bag == null)
            {
                var missing = new List<string>();
                if (driver == null) missing.Add(DriverSrc + " (the phase itself)");
                if (fleet == null) missing.Add(FleetSrc + " (the standing lane)");
                if (bag == null) missing.Add(BagSrc + " (the retired Bag Map section)");
                failures.Add("[fixture-exists] this suite guards files that are NOT PRESENT: " +
                             string.Join(", ", missing) + ". A fixture that cannot be read is a FAILURE " +
                             "naming what is missing - never a skip, and never a green. If a file moved, " +
                             "re-point the const at the top of this suite in the SAME change that moved it");
                reason = string.Format("FRESH-SAVE FTUE LANE x{0}: {1}", failures.Count, string.Join(" | ", failures));
                return false;
            }

            // -- RULE 1 [phase-wired] -----------------------------------------------
            // Flat, at method level. Everything from here down is guaranteed to run,
            // because RULE 0 above has already proven every fixture is readable.
            if (driver.IndexOf("private IEnumerator " + PhaseName + "(", StringComparison.Ordinal) < 0)
                failures.Add("[phase-wired] " + DriverSrc + " no longer declares 'private IEnumerator " +
                             PhaseName + "(' - the fresh-save FTUE lane has no phase to run, and every " +
                             "fresh-save assertion in the fleet falls back to going N/A on whatever save " +
                             "the machine happened to boot (the 2026-09-06 state: five logs, zero " +
                             "[Flow:Onboard*] lines)");
            if (driver.IndexOf("RunPhase(\"" + PhaseName + "\"", StringComparison.Ordinal) < 0)
                failures.Add("[phase-wired] " + DriverSrc + " never calls RunPhase(\"" + PhaseName +
                             "\", ...) - the phase exists but is not in the sequence, which is " +
                             "indistinguishable from not existing on any run");
            if (driver.IndexOf("case \"" + PhaseName + "\":", StringComparison.Ordinal) < 0)
                failures.Add("[phase-wired] TimeoutFor has no 'case \"" + PhaseName + "\":' entry, so the " +
                             "phase silently takes the 30s default. It founds a New Game, reloads the hub " +
                             "and walks the beat spine; 30s cuts it off part-way and the cut-off is " +
                             "reported as a phase TIMEOUT rather than as the missing budget it is");

            // The slice is the SECOND fixture this suite depends on, and it gets the same
            // treatment as the files: an absent phase body is asserted and NAMED, then the
            // per-seam rules run against an EMPTY string so each one still reports its own
            // missing seam. Deliberately noisy on that path - ten named seams is a clearer
            // account of a deleted phase than one line saying the slice failed, and it keeps
            // every rule below un-nested (the arm-D shape this suite was red for).
            string body = SliceMethod(driver, "private IEnumerator " + PhaseName + "(");
            if (body == null)
            {
                failures.Add("[phase-wired] could not slice the body of " + PhaseName + " out of " + DriverSrc +
                             " - the phase signature is gone, so none of the seam rules below can find their " +
                             "evidence and each will report its own seam as missing");
                body = string.Empty;
            }

            // -- RULE 2 [founds-its-own-town] ---------------------------------------
            RequireInBody(failures, body, "founds-its-own-town", "ResetToNewGame()",
                "the lane must FOUND its own town. A phase that reads the booted save is a phase " +
                "that goes N/A on a returning save, which is the entire defect WO-1500 was opened on");
            RequireInBody(failures, body, "founds-its-own-town", "Onboarded",
                "the fresh-save proof reads GameState.Onboarded - false is what arms the FTUE at all");
            RequireInBody(failures, body, "founds-its-own-town", "LastHarvestClaimMs",
                "the fresh-save proof reads the harvest clock. A non-zero clock on a new town is the " +
                "owner's 2026-09-05 'YOUR REALM WORKED FOR 8h 22m' on START NEW");
            RequireInBody(failures, body, "founds-its-own-town", "EverBuiltStructureIds",
                "the fresh-save proof reads the ever-built ledger. A surviving ledger pays HELD ticks " +
                "for a farm and a lumbermill the new town does not have");

            // -- RULE 3 [claims-nothing] --------------------------------------------
            RequireInBody(failures, body, "claims-nothing", "WasFreshClock",
                "the lane must assert the FIRST claim took the coordinator's fresh-clock arm. Without " +
                "it nothing in the FLOW proves a new town claims nothing - only the editmode fixture " +
                "(OfflineHarvestRegression case 6) does, and a fixture cannot prove the shipped path runs");
            RequireInBody(failures, body, "claims-nothing", "ElapsedSeconds",
                "the lane must assert the first window is ZERO seconds wide");
            RequireInBody(failures, body, "claims-nothing", "WelcomeBackPopup",
                "the lane must assert no welcome-back report is on screen on a town founded seconds " +
                "ago - that screen IS the symptom the owner reported twice");

            // -- RULE 4 [beats-observed] --------------------------------------------
            RequireInBody(failures, body, "beats-observed", "CurrentStepId",
                "the lane must read TutorialFlow.CurrentStepId so every guide beat a new player " +
                "reaches is NAMED in the log by id. 'the FTUE ran' is not an observation");

            // -- RULE 4b [honest-verdict] -------------------------------------------
            // The three fixes made on 2026-09-07 before this lane ever ran, each of
            // which turns a green line into a lie if it is undone.
            RequireInBody(failures, body, "honest-verdict", "TutorialFlow.RanThisSession",
                "the lane must check RanThisSession as a PRECONDITION, before it founds anything. " +
                "s_ranThisSession latches in Bootstrap and no New Game clears it, so inside a full " +
                "sweep the reloaded flow parks Finished, the beat walk records zero beats, and the " +
                "lane FAILS a healthy build every night");
            RequireInBody(failures, body, "honest-verdict", "FRESH_SAVE_FTUE_SOFT",
                "a precondition the run cannot meet must print a distinct SOFT line, never the OK " +
                "marker and never a bare Fail. Without it, either the fleet reads a pass over an " +
                "unanswered question or the ranked tickets fill with false ones");
            RequireInBody(failures, body, "honest-verdict", "beats.Count > 0",
                "the OK marker must be GATED on the FTUE actually being observed (a live flow AND " +
                "at least one named beat). Ungated, a flag-gated run prints OK with beats=0 - which " +
                "is the 2026-09-06 shape (green, nothing asserted) reproduced inside the lane " +
                "written to end it");
            RequireInBody(failures, body, "honest-verdict", "SkipAll()",
                "the lane must LEAVE A RETURNING SAVE. It founds a town and that save stays on disk; " +
                "if it stays FRESH, the next boot arms the flow itself, RanThisSession latches, and " +
                "the lane's own precondition refuses - a nightly lane that works exactly once. " +
                "SkipAll is the sanctioned completer (same end state as a completer, one FinishFlow)");

            // -- The marker the fleet judges the lane by ----------------------------
            if (body.IndexOf(PhaseMarker, StringComparison.Ordinal) < 0)
                failures.Add("[lane-judged] the phase body no longer prints its success marker '" +
                             PhaseMarker + "'. The fleet greps that line per instance; without it a lane " +
                             "that asserted nothing and a lane that passed produce identical logs");

            // -- RULE 5 [lane-judged] -----------------------------------------------
            if (fleet.IndexOf("'" + LaneKey + "'", StringComparison.Ordinal) < 0)
                failures.Add("[lane-judged] " + FleetSrc + " no longer declares the '" + LaneKey +
                             "' lane. The lane is the standing part: without it the fresh-save run is a " +
                             "command somebody has to remember, and the 2026-09-06 evidence is what " +
                             "remembering looks like in practice");
            if (fleet.IndexOf("'" + PhaseFilterToken + "'", StringComparison.Ordinal) < 0)
                failures.Add("[lane-judged] " + FleetSrc + " does not point the lane at the '" +
                             PhaseFilterToken + "' phase filter, so the lane would run the FULL sweep - " +
                             "which is the one shape the phase must not run in (TutorialFlow.RanThisSession " +
                             "is process state, and the 420s global cap is already spent by the tail)");
            if (fleet.IndexOf(PhaseMarker, StringComparison.Ordinal) < 0)
                failures.Add("[lane-judged] " + FleetSrc + " no longer names the phase's marker, so nothing " +
                             "judges whether the lane ASSERTED anything - only whether the bot finished");
            if (fleet.IndexOf("FLEET_LANE_FAIL", StringComparison.Ordinal) < 0)
                failures.Add("[lane-judged] " + FleetSrc + " no longer emits FLEET_LANE_FAIL on a missing " +
                             "lane marker. Marker absence on a fresh log is a FAILURE, not an unknown");
            if (!Regex.IsMatch(fleet, @"\$fleetExit\s*=\s*5"))
                failures.Add("[lane-judged] " + FleetSrc + " no longer sets a non-zero exit for a lane miss. " +
                             "A gate whose failure path exits 0 is not a gate (memory " +
                             "gates-report-success-without-proving-it)");

            // -- RULE 6 [bag-map-rail-gone] -----------------------------------------
            string bagCode = StripLineComments(bag);
            if (Regex.IsMatch(bagCode, @"case\s+RailMap\s*:"))
                failures.Add("[bag-map-rail-gone] " + BagSrc + " has a `case RailMap:` branch again. Nothing " +
                             "builds a RailMap entry, so the branch can only answer for a section the player " +
                             "cannot reach - and an unreachable Map branch reads as a Map section that exists, " +
                             "which is what a seat re-wires an entry back onto");
            if (bagCode.IndexOf("KeyRailMapSoon", StringComparison.Ordinal) >= 0)
                failures.Add("[bag-map-rail-gone] " + BagSrc + " paints the KeyRailMapSoon 'soon' label again. " +
                             "The owner's resolution was HIDE, never 'label it as coming': a labelled entry " +
                             "that does nothing on the first screen a new player explores is the worst of the " +
                             "three options (WO-1500 section 2)");

            int entries = Regex.Matches(bagCode, @"BuildRailEntry\s*\(\s*content\.transform").Count;
            // Matched on the COMMENT-STRIPPED text: the constant's own doc comment records
            // that it read 8 until 2026-09-07, and a raw match could bind to that history.
            var declared = Regex.Match(bagCode, @"RailEntryCount\s*=\s*(\d+)");
            if (entries == 0)
                failures.Add("[bag-map-rail-gone] no BuildRailEntry(content.transform, ...) calls found in " +
                             BagSrc + " - the rail builder was renamed and this rule can no longer count it");
            else if (!declared.Success)
                failures.Add("[bag-map-rail-gone] " + BagSrc + " no longer declares RailEntryCount, which the " +
                             "touch-floor case in InventoryArmoryRailRegression parses to size the rail");
            else if (int.Parse(declared.Groups[1].Value) != entries)
                failures.Add(string.Format(
                    "[bag-map-rail-gone] RailEntryCount is {0} but the rail builds {1} entries. The " +
                    "constant went stale exactly this way once already: the Realm Map entry was retired " +
                    "2026-08-31 and the count stayed 8 until 2026-09-07, so the touch-floor arithmetic " +
                    "sized the rail for an entry that is never built. Count the BuildRailEntry calls.",
                    declared.Groups[1].Value, entries));

            if (failures.Count == 0)
            {
                reason = "the fresh-save FTUE lane is wired end to end: AutoPilotDriver runs " + PhaseName +
                         " with its own budget, the phase founds its own New Game and asserts the first claim " +
                         "takes the fresh-clock arm with a zero window and no welcome-back screen, the guide " +
                         "beats are named by id, and run-autopilot-fleet.ps1 -Lane " + LaneKey +
                         " judges the run by the phase's own marker and refuses without it; the Bag's retired " +
                         "Map section carries no branch, no 'soon' label and no stale entry count";
                return true;
            }

            reason = string.Format("FRESH-SAVE FTUE LANE x{0}: {1}", failures.Count, string.Join(" | ", failures));
            return false;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Read a repo-relative file, RECORDING a failure and returning null when it
        /// cannot be read. A missing file is a FAILURE, never a skip: a suite that answers OK
        /// because it could not find what it guards is a hollow pass.
        /// ⚠ The caller must still gate on the null (RULE 0) rather than wrapping its rules in
        /// `if (x != null)`. This method recording the failure is NOT enough on its own - it was
        /// already doing so when the hollow-pass scanner flagged the suite, because the guard
        /// SHAPE, not the intent, is what a reader and a detector can both verify.</summary>
        private static string ReadRepoFile(string relative, List<string> failures)
        {
            try
            {
                string full = Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                {
                    failures.Add("cannot read '" + relative + "' (looked at '" + full + "') - the file this rule " +
                                 "guards is gone or moved; the rule cannot pass by default");
                    return null;
                }
                return File.ReadAllText(full);
            }
            catch (Exception ex)
            {
                failures.Add("reading '" + relative + "' threw " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Repo root = the folder holding Assets/. Application.dataPath ends in
        /// /Assets, so its parent is the root - never a hardcoded drive letter (the root is
        /// C:\eoa on one machine and D:\eoa on another).</summary>
        private static string RepoRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static void RequireInBody(List<string> failures, string body, string rule,
                                          string needle, string why)
        {
            if (body.IndexOf(needle, StringComparison.Ordinal) >= 0) return;
            failures.Add("[" + rule + "] the " + PhaseName + " body no longer names '" + needle + "' - " + why);
        }

        /// <summary>The text following a method signature, bounded by a character window. A
        /// window rather than a brace walk because every assertion above is "does the phase
        /// touch this seam", which a window answers exactly and cheaply.</summary>
        private static string SliceMethod(string src, string signature)
        {
            int a = src.IndexOf(signature, StringComparison.Ordinal);
            if (a < 0) return null;
            int len = Math.Min(PhaseWindowChars, src.Length - a);
            return src.Substring(a, len);
        }

        /// <summary>Strip `//` line comments so a note that NAMES a retired token stays legal
        /// documentation while a LINE that uses it is the regression. Same technique, and same
        /// reason, as PublicNavigationRetirementRegression's StripLineComments.</summary>
        private static string StripLineComments(string src)
        {
            return Regex.Replace(src, @"^\s*//.*$", "", RegexOptions.Multiline);
        }
    }
}
#endif
