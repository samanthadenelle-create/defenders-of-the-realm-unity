// =============================================================================
// TutorialAnchorLatchRegression [tutorial-anchor-latch]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-962 (owner F8 seq 2301, 2026-08-10, Main_Castle_Overworld). Captured from the
// owner's own Player.log inside ONE founding_walk step:
//
//     STEP-ENTER :: founding_walk (completes on 'hero.reached:guide_gate')
//     guide-lead SET -> (-3.43, 0.08, -38.63)    south gate
//     guide-lead SET -> (37.29, 0.08, -0.21)     east  gate
//     guide-lead SET -> ( 3.07, 0.08,  38.68)    north gate
//     STEP-STUCK :: founding_walk - no 'hero.reached:guide_gate' after 123s
//
// 'guide_gate' resolved LIVE to the gate nearest the HERO, so every step the player
// took toward the target moved the target to another side of town. The beat was
// unreachable by walking and the watchdog SKIPPED it. The fix is a step-ENTER LATCH:
// resolve ONCE, hold for the life of the step, re-resolve only after exit/re-enter.
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (a) LIVE LATCH BEHAVIOUR - TutorialWorldAnchors.LiveResolverOverride (the
//       regression seam) replays the exact F8 seq 2301 walk: a resolver whose answer
//       moves south -> east -> north as the "hero" advances. With a latch taken, the
//       anchor TryResolveAnchor hands back must not move; LatchAnchor must be a no-op
//       while latched; ClearLatch + LatchAnchor must re-resolve to the CURRENT answer
//       (the step exit / re-enter case); an anchor unresolvable at ENTER must latch on
//       the first frame it DOES resolve and then hold that. No scene, hero rig or
//       navmesh bake is needed - the seam is exactly the moving goalpost.
//
//   (b) SOURCE INVARIANTS (comment-stripped lint) - the flow-side wiring cannot run
//       without a play session, so it is pinned at source: EnterStep latches a
//       hero.reached step's anchor, CompleteCurrentStep and FinishFlow clear it, the
//       divergence trace never writes the latch back, and - the forbidden "fixes" of
//       WO-962 sec 3 - ReachedRadius stays 6m and WatchdogSeconds stays 120s. Widening
//       either one hides the defect instead of fixing it, so a change to them fails
//       here deliberately.
//
//   NOT provable here: that the hero physically reaches the latched gate in a real
//   FTUE run (acceptance 2/3) - that is the AutoPilot/felt-verify, PO closes.
//
// Markers: TUTORIAL_ANCHOR_LATCH_OK / TUTORIAL_ANCHOR_LATCH_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TutorialAnchorLatchRegression.RunAll
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
    public static class TutorialAnchorLatchRegression
    {
        private const string AnchorsSrc = "Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs";
        private const string FlowSrc    = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";

        // The three gates from the captured F8 seq 2301 trace, in the order the live
        // resolver produced them as the player walked toward the first one.
        private static readonly Vector3 SouthGate = new Vector3(-3.43f, 0.08f, -38.63f);
        private static readonly Vector3 EastGate  = new Vector3(37.29f, 0.08f, -0.21f);
        private static readonly Vector3 NorthGate = new Vector3(3.07f, 0.08f, 38.68f);

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TUTORIAL_ANCHOR_LATCH_OK - " + reason);
            else Debug.LogError("TUTORIAL_ANCHOR_LATCH_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "latch-holds",   () => Case1_LatchHoldsUnderMovingResolver(failures));
                Case(failures, "latch-relatch", () => Case2_ReResolvesAfterStepExit(failures));
                Case(failures, "latch-late",    () => Case3_LatchesOnFirstResolvableFrame(failures));
                Case(failures, "latch-wiring",  () => Case4_WiringAndForbiddenFixesLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                // Never leave the seam armed for the next suite / the editor.
                TutorialWorldAnchors.LiveResolverOverride = null;
                TutorialWorldAnchors.ClearLatch("tutorial-anchor-latch regression teardown");
            }

            if (failures.Count == 0)
            {
                reason = "TUTORIAL ANCHOR LATCH OK - with a resolver that moves south->east->north " +
                         "(F8 seq 2301), a latched 'guide_gate' answers ONE position for the life of " +
                         "the step, re-latching is a no-op, a step exit re-resolves once, a late anchor " +
                         "latches on its first resolvable frame, and the flow-side latch/clear wiring " +
                         "plus the untouched 6m reach radius and 120s watchdog are pinned at source.";
                return true;
            }
            reason = "tutorial-anchor-latch FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  The regression seam - a resolver that MOVES, exactly like the bug
        // =====================================================================

        /// <summary>A scripted stand-in for ResolveNearestGate: hands back whatever gate the
        /// harness has currently "walked" nearest to. MoveTo() is the player taking a step.</summary>
        private sealed class MovingResolver
        {
            public Vector3 Answer;
            public string AnswerName = "WaveSpawnPoint-S";
            public bool Resolvable = true;

            public bool Resolve(string anchorId, out Vector3 pos, out string sourceName)
            {
                pos = Answer;
                sourceName = AnswerName;
                if (!Resolvable) { pos = default; sourceName = null; return false; }
                return !string.IsNullOrEmpty(anchorId);
            }

            public void MoveTo(Vector3 p, string name) { Answer = p; AnswerName = name; }
        }

        private static MovingResolver Arm()
        {
            var r = new MovingResolver { Answer = SouthGate, AnswerName = "WaveSpawnPoint-S" };
            TutorialWorldAnchors.ClearLatch("regression arm");
            TutorialWorldAnchors.LiveResolverOverride = r.Resolve;
            return r;
        }

        private static void Disarm()
        {
            TutorialWorldAnchors.LiveResolverOverride = null;
            TutorialWorldAnchors.ClearLatch("regression disarm");
        }

        private static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.01f;

        // =====================================================================
        //  Case 1 - the latch HOLDS while the live resolver walks away
        // =====================================================================

        private static void Case1_LatchHoldsUnderMovingResolver(List<string> failures)
        {
            var r = Arm();
            try
            {
                if (!TutorialWorldAnchors.LatchAnchor("guide_gate"))
                {
                    failures.Add("[latch-holds] LatchAnchor('guide_gate') returned false with a resolvable " +
                                 "anchor - a step-ENTER latch must take.");
                    return;
                }
                if (!TutorialWorldAnchors.IsLatched("guide_gate"))
                    failures.Add("[latch-holds] IsLatched('guide_gate') is false straight after a successful latch.");

                if (!TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 first) || !Same(first, SouthGate))
                    failures.Add("[latch-holds] the first read after latching answered " + first +
                                 " - expected the latched south gate " + SouthGate + ".");

                // The player walks south; the east gate becomes nearest. This is the exact
                // moment the shipped bug re-pointed the guide lead to (37.29, 0.08, -0.21).
                r.MoveTo(EastGate, "WaveSpawnPoint-E");
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 afterEast);
                if (!Same(afterEast, SouthGate))
                    failures.Add("[latch-holds] the anchor MOVED to " + afterEast + " when the live resolver " +
                                 "answered the east gate - WO-962: a latched anchor must never re-resolve " +
                                 "inside its step (that is the F8 seq 2301 moving goalpost).");

                // ... and then the north gate.
                r.MoveTo(NorthGate, "WaveSpawnPoint-N");
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 afterNorth);
                if (!Same(afterNorth, SouthGate))
                    failures.Add("[latch-holds] the anchor MOVED to " + afterNorth + " when the live resolver " +
                                 "answered the north gate - the latch is not holding.");

                // Re-latching mid-step (TickProximityProbe calls LatchAnchor every frame to
                // cover a late anchor) must be a NO-OP, never a re-target.
                TutorialWorldAnchors.LatchAnchor("guide_gate");
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 afterRelatch);
                if (!Same(afterRelatch, SouthGate))
                    failures.Add("[latch-holds] a mid-step LatchAnchor re-targeted the latch to " + afterRelatch +
                                 " - LatchAnchor must be idempotent while latched, or the per-frame probe " +
                                 "re-creates the bug it was added to prevent.");
            }
            finally { Disarm(); }
        }

        // =====================================================================
        //  Case 2 - a step EXIT drops the latch; re-entry resolves ONCE again
        // =====================================================================

        private static void Case2_ReResolvesAfterStepExit(List<string> failures)
        {
            var r = Arm();
            try
            {
                TutorialWorldAnchors.LatchAnchor("guide_gate");
                r.MoveTo(NorthGate, "WaveSpawnPoint-N");

                TutorialWorldAnchors.ClearLatch("regression: step exit");
                if (TutorialWorldAnchors.IsLatched("guide_gate"))
                    failures.Add("[latch-relatch] ClearLatch left the anchor latched - a completed/skipped " +
                                 "step must not hand its target to the next entry.");

                // An unlatched read is LIVE again (the resolver stays live for everything else).
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 live);
                if (!Same(live, NorthGate))
                    failures.Add("[latch-relatch] an UNLATCHED read answered " + live + " instead of the live " +
                                 NorthGate + " - clearing the latch must restore live resolution.");

                // Re-enter the step: one fresh resolve, then held again.
                if (!TutorialWorldAnchors.LatchAnchor("guide_gate"))
                    failures.Add("[latch-relatch] re-entering the step failed to take a new latch.");
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 relatched);
                if (!Same(relatched, NorthGate))
                    failures.Add("[latch-relatch] the re-entered step latched " + relatched + " instead of the " +
                                 "current answer " + NorthGate + " - a re-entry must re-resolve, not resurrect " +
                                 "the old target.");

                r.MoveTo(EastGate, "WaveSpawnPoint-E");
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 held);
                if (!Same(held, NorthGate))
                    failures.Add("[latch-relatch] the NEW latch moved to " + held + " - the second latch must " +
                                 "hold exactly as the first did.");
            }
            finally { Disarm(); }
        }

        // =====================================================================
        //  Case 3 - an anchor unresolvable at ENTER latches on its first frame
        // =====================================================================

        private static void Case3_LatchesOnFirstResolvableFrame(List<string> failures)
        {
            var r = Arm();
            try
            {
                r.Resolvable = false;
                if (TutorialWorldAnchors.LatchAnchor("guide_gate"))
                    failures.Add("[latch-late] LatchAnchor reported success while the anchor was unresolvable - " +
                                 "an unresolvable anchor must latch NOTHING (no silent fallback target).");
                if (TutorialWorldAnchors.IsLatched("guide_gate"))
                    failures.Add("[latch-late] a failed latch left IsLatched true.");
                if (TutorialWorldAnchors.TryResolveAnchor("guide_gate", out _))
                    failures.Add("[latch-late] TryResolveAnchor answered true for an unresolvable anchor - the " +
                                 "probe must simply wait (the watchdog self-reports), never invent a position.");

                // The gate spawns: the FIRST resolvable frame becomes the latch...
                r.Resolvable = true;
                r.MoveTo(EastGate, "WaveSpawnPoint-E");
                if (!TutorialWorldAnchors.LatchAnchor("guide_gate"))
                    failures.Add("[latch-late] the anchor became resolvable but the retry latch did not take.");

                // ... and holds even though the resolver keeps moving.
                r.MoveTo(SouthGate, "WaveSpawnPoint-S");
                TutorialWorldAnchors.LatchAnchor("guide_gate");
                TutorialWorldAnchors.TryResolveAnchor("guide_gate", out Vector3 held);
                if (!Same(held, EastGate))
                    failures.Add("[latch-late] the late latch answered " + held + " instead of the gate it first " +
                                 "resolved to " + EastGate + ".");
            }
            finally { Disarm(); }
        }

        // =====================================================================
        //  Case 4 - flow wiring + the two forbidden "fixes", pinned at source
        // =====================================================================

        private static void Case4_WiringAndForbiddenFixesLint(List<string> failures)
        {
            string anchors = StripComments(File.ReadAllText(AnchorsSrc));
            string flow    = StripComments(File.ReadAllText(FlowSrc));

            // (1) The step ENTER latches a hero.reached step's anchor.
            if (!Regex.IsMatch(flow, @"TutorialWorldAnchors\s*\.\s*LatchAnchor"))
                failures.Add("[latch-wiring] " + FlowSrc + " no longer calls TutorialWorldAnchors.LatchAnchor - " +
                             "without the step-ENTER latch the anchor re-resolves per frame (WO-962).");

            // (2) The step EXIT / flow end drops it (at least the two shipped call sites plus
            //     the teardown - fewer than two means a path now leaks a stale latch).
            int clears = Regex.Matches(flow, @"TutorialWorldAnchors\s*\.\s*ClearLatch").Count;
            if (clears < 2)
                failures.Add("[latch-wiring] " + FlowSrc + " calls ClearLatch only " + clears + " time(s) - the " +
                             "latch must die with the step (CompleteCurrentStep) and with the flow (FinishFlow/" +
                             "teardown), or a re-entered step inherits a stale target.");

            // (3) The divergence trace is DIAGNOSTIC: it must never write the latch back.
            var diverge = Regex.Match(anchors,
                @"void\s+TraceDivergenceOnce\s*\([^)]*\)\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}",
                RegexOptions.Singleline);
            if (!diverge.Success)
                failures.Add("[latch-wiring] " + AnchorsSrc + " has no TraceDivergenceOnce body - the WO-962 " +
                             "evidence line (the live resolver would now answer differently) is gone.");
            else if (Regex.IsMatch(diverge.Groups["body"].Value, @"_latchPos\s*=|_latchActive\s*=\s*true"))
                failures.Add("[latch-wiring] TraceDivergenceOnce ASSIGNS the latch - WO-962 sec 3: the divergence is " +
                             "recorded once and NOT acted on; following it is the moving-goalpost defect.");

            // (4) TryResolveAnchor must consult the latch before resolving live.
            var read = Regex.Match(anchors,
                @"bool\s+TryResolveAnchor\s*\([^)]*\)\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}",
                RegexOptions.Singleline);
            if (!read.Success || !read.Groups["body"].Value.Contains("IsLatched"))
                failures.Add("[latch-wiring] TryResolveAnchor in " + AnchorsSrc + " no longer reads the latch - " +
                             "the probe, the guide lead and the gate highlight would drift apart again.");

            // (5) THE FORBIDDEN FIXES (WO-962 sec 3). Widening the reach radius or lengthening the
            //     watchdog makes the symptom go away while the goalpost still moves.
            if (!Regex.IsMatch(flow, @"ReachedRadius\s*=\s*6f\s*;"))
                failures.Add("[latch-wiring] TutorialFlow.ReachedRadius is no longer 6f - WO-962 sec 3 forbids " +
                             "widening the reach radius to paper over a moving anchor.");
            if (!Regex.IsMatch(flow, @"WatchdogSeconds\s*=\s*120f\s*;"))
                failures.Add("[latch-wiring] TutorialFlow.WatchdogSeconds is no longer 120f - WO-962 sec 3 forbids " +
                             "lengthening the watchdog to paper over a moving anchor (the watchdog behaved " +
                             "correctly; it is what surfaced the bug).");

            // (6) The gate pull-back is untouched (acceptance 5 / owner F8 2026-07-08).
            if (!Regex.IsMatch(anchors, @"GateAnchorPullbackMeters\s*=\s*14f\s*;"))
                failures.Add("[latch-wiring] GateAnchorPullbackMeters is no longer 14f - the anchor must stay " +
                             "~14m INSIDE the walls, never on the wave-spawn ring (owner F8 2026-07-08).");
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
