// =============================================================================
// TownMovementFloorRegression [town-movement-floor]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// P0 2026-08-10, owner F8 seq 2319, verbatim: "No locomotioonj in town? Works in
// builder mode not in here" (scene Main_Castle_Overworld). Related seq 2318,
// STEP-STUCK founding_walk - one fact, not two: a hero that cannot walk cannot
// reach the gate.
//
// THE TWO WAYS TOWN LOCOMOTION DIES WHILE EVERYTHING ELSE LOOKS HEALTHY. Both
// share the owner's discriminator - build mode keeps working, because it drives
// movement through the HUD kit's own input path and the unscaled clock:
//
//   (A) THE WORLD CLOCK STOPPED. Every writer in HeroLocomotion.Update scales by
//       Time.deltaTime (Velocity via MoveTowards, the facing Slerp, agent.Move),
//       so at Time.timeScale 0 the hero cannot move, cannot turn and cannot
//       animate WHILE INPUT IS STILL BEING READ. Two independent systems freeze
//       the world - PauseController (background auto-pause) and
//       BreakCaptureHarness.FlagHere (the F8 note box) - and each captures
//       Time.timeScale to restore later. When one freezes while the other is
//       already frozen it captures 0, and its restore RE-ARMS the freeze
//       permanently, with no menu on screen to explain it. The captured
//       signature is unmistakable once you can see it: live input in
//       [Flow:HeroDrift] (input=(0.00,1.00)) with vel=(0.000,0.000) and a
//       frozen animator (baseNt pinned at 0.00) while the camera still orbits.
//
//   (B) THE SCRIPTED-MOVE STOMP LEAKED. DungeonController.EnsureSingleDungeonMover
//       neutralizes the injected HeroLocomotion with SetScriptedMove(zero), and
//       ReadMoveInput returns that value VERBATIM. Left armed on the return path
//       it would zero town input with build mode unaffected - the same felt
//       symptom from a completely different cause. (This was NOT the 08-10 cause:
//       the owner's capture shows ReadMoveInput returning live input.y=1.00, which
//       is impossible while the stomp is armed. It is pinned here anyway because it
//       is the other member of the class and it is one refactor away from real.)
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (1) LIVE STATIC CONTRACT - the scripted-move seam disarms for real:
//       ScriptedMoveActive is false at rest, true while armed with ZERO (the
//       dungeon's exact call - a zero vector must still read as ARMED, or the
//       leak becomes invisible), and false again after ClearScriptedMove. The
//       suite restores whatever it found, so it cannot itself leak the stomp.
//
//   (2) SOURCE INVARIANT - the dungeon's neutralize is PAIRED: every
//       SetScriptedMove in DungeonController has a ClearScriptedMove on a
//       teardown path reachable from BOTH the explicit exit and OnDestroy.
//
//   (3) SOURCE INVARIANT - the world-clock floor. Neither freeze owner may
//       restore a non-positive timeScale, and the HeroOwner heartbeat must keep
//       printing timeScale - the field whose absence made a three-hour P0
//       capture unreadable. Instrumentation is permanent (CLAUDE.md S12).
//
//   NOT provable here: that the hero FEELS right walking through town - that is
//   the owner's felt-verify (PO closes, per docs/TICKET_PIPELINE.md).
//
// Markers: TOWN_MOVEMENT_FLOOR_OK / TOWN_MOVEMENT_FLOOR_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TownMovementFloorRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class TownMovementFloorRegression
    {
        private const string DungeonCtrlSrc = "Assets/_Modules/Dungeons/DungeonController.cs";
        private const string PauseSrc       = "Assets/_Modules/Settings/PauseController.cs";
        private const string WorldHoldSrc = "Assets/_Modules/Core/UI/WorldHold.cs";
        private const string BreakSrc       = "Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs";
        private const string HeroLocoSrc    = "Assets/_Modules/Village/Hero/HeroLocomotion.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TOWN_MOVEMENT_FLOOR_OK - " + reason);
            else Debug.LogError("TOWN_MOVEMENT_FLOOR_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "scripted-move-disarms", () => Case1_ScriptedMoveDisarms(failures));
                Case(failures, "dungeon-neutralize-paired", () => Case2_NeutralizeIsPaired(failures));
                Case(failures, "world-clock-floor", () => Case3_WorldClockFloor(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "TOWN MOVEMENT FLOOR OK - the scripted-move stomp reads as ARMED even when " +
                         "armed with zero and truly disarms on clear, the dungeon neutralize is paired " +
                         "with a teardown restore reachable from both exit and OnDestroy, neither " +
                         "world-clock freeze owner can restore a non-positive timeScale, and the " +
                         "[Flow:HeroOwner] heartbeat still reports the world clock.";
                return true;
            }
            reason = "town-movement-floor FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the LIVE scripted-move contract
        // =====================================================================

        // The whole leak turns on ONE property being honest: a stomp armed with the ZERO
        // vector (exactly what DungeonController passes) must still report ARMED. If
        // ScriptedMoveActive were ever derived from the vector's magnitude instead of the
        // arm flag, the dungeon neutralize would be permanently invisible to every trace
        // and every test - the failure mode this suite exists to make impossible.
        private static void Case1_ScriptedMoveDisarms(List<string> failures)
        {
            bool armedAtEntry = HeroLocomotion.ScriptedMoveActive;
            try
            {
                HeroLocomotion.ClearScriptedMove();
                if (HeroLocomotion.ScriptedMoveActive)
                    failures.Add("[scripted-move] ClearScriptedMove() left HeroLocomotion.ScriptedMoveActive " +
                                 "TRUE - the disarm does not disarm, so a dungeon neutralize can never be " +
                                 "undone and town input stays zeroed (build mode unaffected).");

                HeroLocomotion.SetScriptedMove(Vector2.zero);
                if (!HeroLocomotion.ScriptedMoveActive)
                    failures.Add("[scripted-move] SetScriptedMove(Vector2.zero) did NOT report as armed. " +
                                 "Zero is the exact value DungeonController.EnsureSingleDungeonMover passes, " +
                                 "so an armed-with-zero stomp that reads 'off' is an UNOBSERVABLE town-input " +
                                 "kill - ReadMoveInput returns the scripted value verbatim.");

                HeroLocomotion.ClearScriptedMove();
                if (HeroLocomotion.ScriptedMoveActive)
                    failures.Add("[scripted-move] ScriptedMoveActive stayed TRUE after a clear that " +
                                 "followed an arm - the stomp survives its own teardown.");
            }
            finally
            {
                // Never let the suite itself leak the very state it is guarding.
                if (armedAtEntry) HeroLocomotion.SetScriptedMove(Vector2.zero);
                else HeroLocomotion.ClearScriptedMove();
            }
        }

        // =====================================================================
        //  Case 2 - the dungeon neutralize is PAIRED (source, comment-stripped)
        // =====================================================================

        private static void Case2_NeutralizeIsPaired(List<string> failures)
        {
            string src = StripComments(File.ReadAllText(DungeonCtrlSrc));

            int arms   = Regex.Matches(src, @"HeroLocomotion\s*\.\s*SetScriptedMove").Count;
            int clears = Regex.Matches(src, @"HeroLocomotion\s*\.\s*ClearScriptedMove").Count;

            if (arms > 0 && clears == 0)
                failures.Add("[neutralize-pairing] " + DungeonCtrlSrc + " arms the scripted-move stomp (" +
                             arms + "x) and NEVER clears it. The stomp is a STATIC shared with the town " +
                             "hero, so it would follow the player back into Main_Castle_Overworld and zero " +
                             "town input while build mode kept working.");

            // The restore must be reachable from BOTH the deliberate exit and the destruction path -
            // an exception on the way out must not be able to strand the static armed.
            if (arms > 0 && !src.Contains("RestoreInjectedHeroMover"))
                failures.Add("[neutralize-pairing] " + DungeonCtrlSrc + " no longer routes its teardown " +
                             "through RestoreInjectedHeroMover - the named single owner of undoing the " +
                             "neutralize is gone.");

            if (arms > 0 && !Regex.IsMatch(src, @"OnDestroy[\s\S]{0,600}?RestoreInjectedHeroMover"))
                failures.Add("[neutralize-pairing] " + DungeonCtrlSrc + " does not call " +
                             "RestoreInjectedHeroMover from OnDestroy. A dungeon torn down by scene " +
                             "unload (rather than the explicit exit) would leak the stomp into town.");
        }

        // =====================================================================
        //  Case 3 - the world-clock floor (source, comment-stripped)
        // =====================================================================

        // A freeze owner restoring a captured 0 is the softlock: the world never restarts and
        // NOTHING on screen says why. Both owners must degrade a non-positive capture to 1.
        private static void Case3_WorldClockFloor(List<string> failures)
        {
            string pause = StripComments(File.ReadAllText(PauseSrc));
            string brk   = StripComments(File.ReadAllText(BreakSrc));
            string loco  = StripComments(File.ReadAllText(HeroLocoSrc));

            // PauseController: neither the capture nor the restore may pass a non-positive scale
            // through unguarded. The guard reads as a "> 0f ?" ternary at both ends.
            // ⛔ RE-POINTED 2026-08-22 - THE INVARIANT MOVED, IT DID NOT DISAPPEAR.
            // WO-1149 made WorldHold the SINGLE writer of Time.timeScale: PauseController is now a
            // CLIENT that takes a ref-counted hold instead of zeroing the clock itself, and the
            // "never capture an already-frozen clock" guard moved into WorldHold.Acquire where it
            // protects EVERY caller instead of just the pause menu.
            //
            // This case used to regex PauseController for `_timeScaleBeforePause > 0f ?`. Left as it
            // was it would go RED ON A CORRECT TREE, and the obvious way to "fix" that red is to put
            // a second Time.timeScale writer back into PauseController - which is exactly the WO-1016
            // defect this case exists to prevent. Same invariant, new address.
            string hold = File.Exists(WorldHoldSrc) ? StripComments(File.ReadAllText(WorldHoldSrc)) : "";
            if (hold.Length == 0)
                failures.Add("[world-clock] " + WorldHoldSrc + " is missing. It owns the world clock " +
                             "since WO-1149; without it nothing guards the freeze.");
            else
            {
                if (!Regex.IsMatch(hold, @">\s*0f\s*\?") && !Regex.IsMatch(hold, @"<=\s*0f"))
                    failures.Add("[world-clock] " + WorldHoldSrc + " captures/restores a timeScale " +
                                 "without a non-positive guard. Acquiring a hold while the clock is " +
                                 "ALREADY frozen would capture 0, and the final release would then " +
                                 "re-arm a PERMANENT invisible freeze - the hero cannot move, with no " +
                                 "pause menu on screen (WO-1016).");

                if (Regex.IsMatch(pause, @"Time\s*\.\s*timeScale\s*="))
                    failures.Add("[world-clock] " + PauseSrc + " assigns Time.timeScale directly. Since " +
                                 "WO-1149 there is exactly ONE writer (" + WorldHoldSrc + "); a second " +
                                 "owner is how a captured-zero freeze gets re-armed behind the first.");
            }

            // BreakCaptureHarness: same contract on the F8 note freeze.
            if (!Regex.IsMatch(brk, @"_prevTimeScale\s*=\s*[A-Za-z_][A-Za-z0-9_.]*\s*>\s*0f\s*\?"))
                failures.Add("[world-clock] " + BreakSrc + " captures Time.timeScale for the F8 note " +
                             "freeze without a '> 0f' guard. F8 pressed while the app is background-paused " +
                             "captures 0 and CommitFlag restores the freeze forever.");

            if (!Regex.IsMatch(brk, @"Time\s*\.\s*timeScale\s*=\s*_prevTimeScale\s*>\s*0f\s*\?"))
                failures.Add("[world-clock] " + BreakSrc + " restores _prevTimeScale without a '> 0f' " +
                             "guard on commit.");

            // The observability half. CLAUDE.md S12: instrumentation is PERMANENT - the missing
            // timeScale field is precisely why the 08-10 capture could not be read.
            // NOTE: matched without a literal brace so the repo's naive brace-balance gate
            // (CLAUDE.md S1) never sees an unpaired open-brace inside this lint's own source.
            if (!(loco.Contains("timeScale=") && loco.Contains("Time.timeScale:F2")))
                failures.Add("[world-clock] " + HeroLocoSrc + " no longer prints timeScale in the " +
                             "[Flow:HeroOwner] heartbeat. Without it a frozen world is indistinguishable " +
                             "from a broken locomotion path in any capture - the exact ambiguity that " +
                             "cost the 2026-08-10 P0 session.");

            if (!loco.Contains("WORLD CLOCK FROZEN"))
                failures.Add("[world-clock] " + HeroLocoSrc + " no longer raises the WORLD CLOCK FROZEN " +
                             "call-out. A stopped clock must name itself, not wait for a reader to spot a " +
                             "field.");
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
