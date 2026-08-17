// =============================================================================
// TutorialWatchdogBoundRegression [tutorial-watchdog-bound]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-1036 (F8 seq 2513 2026-08-17, seq 2343 2026-08-15, seq 2433 2026-08-16 —
// Main_Castle_Overworld). THE CAPTURED DEFECT, in the owner's own Player.log, one frame:
//
//     [Flow:Tutorial] coach :: step 'founding_walk' idle 245s awaiting 'hero.reached:guide_gate'
//                     with the builder never opened - re-stated the objective (beat 2/4).
//     [Flow:Tutorial] STEP-STUCK :: founding_walk - no 'hero.reached:guide_gate' after 245s
//                     in-step (bound 120s, builder time excluded; builderOpenedThisStep=False,
//                     coachBeats=2); RESCUED via watchdog and recorded as SKIPPED
//     [Flow:Offline]  Claim #6 (resume): resume window -- counting from the background edge
//     [Flow:Offline]  Claim #6 (resume): ONE delta = 196s (0.05h) ...
//
// Coach beat 2 is due at 90s and fired at 245s; only 2 of 4 beats had been spent in what
// the wall clock called four minutes. TWO INDEPENDENT wall-clock timers were late by the
// SAME ~196s, which is a stopped frame loop plus a resume jump — NOT a doubled bound.
// 45s (beat 1) + 196s (background) = 241s, the 2026-08-15 capture to the second. The bound
// was never doubled: Time.unscaledTime is not clamped by Time.maximumDeltaTime, so the first
// frame after the OS restored the app carried the entire suspend window as ONE
// unscaledDeltaTime and the old wall-stamp watchdog charged all of it to the step. The
// player had ~49s on the beat and it was rescued-and-SKIPPED on the resume frame.
//
// Compounding it, from the same harvest: PauseController.OnApplicationPause(true) auto-pauses
// to timeScale 0 and NEVER auto-resumes, so
//     [Flow:HeroOwner] WORLD CLOCK FROZEN: Time.timeScale=0.00 ... The hero CANNOT move
// held while the rescue fired. A frozen player has not abandoned the beat.
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (a) THE BOUND IS HONOURED, replayed frame by frame against TutorialFlow.StepClock — the
//       single owner of the in-step budget. A clean 60 FPS timeline trips within tolerance of
//       the bound; the captured 196s suspend jump contributes at most ONE clamped frame and is
//       recorded as a discarded gap; builder-open and timeScale<=0 frames are excluded (a true
//       pause, not a reset, so pre-builder idle survives); the placement bound (300s) and the
//       default bound (120s) are both honoured; and the coach cadence, riding the SAME budget,
//       delivers all 4 beats before the 120s bound expires instead of 2 in four wall minutes.
//
//   (b) SOURCE INVARIANTS (comment-stripped lint) — the flow-side wiring needs a play session,
//       so it is pinned at source: TickWatchdog must NOT compare a wall-clock stamp against the
//       bound, it must read the clock's Expired(); the clock must be ticked from Update; the
//       coach cadence must not be a Time.unscaledTime stamp; and — WO-962 §3's forbidden fix —
//       WatchdogSeconds stays 120f (this WO must not be "fixed" by lengthening the bound).
//
//   NOT provable here: that a real FTUE run walks the hero to the gate (that is the AutoPilot /
//   owner felt-verify, PO closes). This suite proves the CLOCK, which is what skipped the beat.
//
// Markers: TUTORIAL_WATCHDOG_BOUND_OK / TUTORIAL_WATCHDOG_BOUND_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TutorialWatchdogBoundRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class TutorialWatchdogBoundRegression
    {
        private const string FlowSrc = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";

        /// <summary>The shipped bounds (mirrored, deliberately, so a silent retune fails here).</summary>
        private const float DefaultBound   = 120f;
        private const float PlacementBound = 300f;

        /// <summary>How far past the bound a trip may land and still count as honoured. One
        /// clamped frame (StepClock.MaxFrameStepSeconds) plus a frame of slack.</summary>
        private const float ToleranceSeconds = 2f;

        /// <summary>The captured background window (F8 seq 2513: "ONE delta = 196s").</summary>
        private const float CapturedSuspendSeconds = 196f;

        private const float Frame60 = 1f / 60f;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TUTORIAL_WATCHDOG_BOUND_OK - " + reason);
            else Debug.LogError("TUTORIAL_WATCHDOG_BOUND_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "bound-clean",     () => Case1_CleanTimelineTripsAtTheBound(failures));
                Case(failures, "bound-suspend",   () => Case2_SuspendJumpIsNotCharged(failures));
                Case(failures, "bound-excluded",  () => Case3_BuilderAndFrozenFramesExcluded(failures));
                Case(failures, "bound-placement", () => Case4_PlacementBoundHonoured(failures));
                Case(failures, "bound-coach",     () => Case5_CoachCadenceFitsInsideTheBound(failures));
                Case(failures, "bound-wiring",    () => Case6_WiringAndForbiddenFixesLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "TUTORIAL WATCHDOG BOUND OK - the STEP-STUCK bound is spent in PLAYED frames: a clean " +
                         "60 FPS timeline trips within " + ToleranceSeconds.ToString("0") + "s of the 120s bound, the " +
                         "captured " + CapturedSuspendSeconds.ToString("0") + "s app-suspend jump (F8 seq 2513) " +
                         "contributes one clamped frame and is recorded as a discarded gap instead of skipping the " +
                         "beat, builder-open and timeScale<=0 frames are excluded as a true pause, the 300s placement " +
                         "bound holds, all 4 coach beats land inside the bound, and the flow-side wiring plus the " +
                         "untouched 120s bound are pinned at source.";
                return true;
            }
            reason = "tutorial-watchdog-bound FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  The replay harness — frames in, trip time out
        // =====================================================================

        /// <summary>Runs <paramref name="clock"/> forward one frame at a time and returns the
        /// PLAYED seconds at which it first reports Expired(bound), or -1 if it never does.</summary>
        private static float PlayUntilExpired(TutorialFlow.StepClock clock, float bound,
                                              float frameDelta, bool excluded, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                clock.Tick(frameDelta, excluded);
                if (clock.Expired(bound)) return clock.Charged;
            }
            return -1f;
        }

        // =====================================================================
        //  Case 1 - a clean timeline trips AT the bound, not at twice it
        // =====================================================================

        private static void Case1_CleanTimelineTripsAtTheBound(List<string> failures)
        {
            var clock = new TutorialFlow.StepClock();
            float trip = PlayUntilExpired(clock, DefaultBound, Frame60, false, 60 * 200);

            if (trip < 0f)
            {
                failures.Add("[bound-clean] 200s of clean 60 FPS frames never expired the 120s bound - the watchdog " +
                             "would never rescue a genuinely abandoned step.");
                return;
            }
            if (trip < DefaultBound - ToleranceSeconds || trip > DefaultBound + ToleranceSeconds)
                failures.Add("[bound-clean] the bound tripped at " + trip.ToString("0.00") + "s of played time " +
                             "against a stated bound of " + DefaultBound.ToString("0") + "s (tolerance " +
                             ToleranceSeconds.ToString("0") + "s) - the STEP-STUCK line's 'bound' must be the number " +
                             "the clock actually enforces (WO-1036).");
            if (clock.DiscardedJumpSeconds > 0f)
                failures.Add("[bound-clean] a clean 60 FPS timeline recorded " + clock.DiscardedJumpSeconds.ToString("0.00") +
                             "s of discarded suspend gap - normal frames must never be mistaken for a background window.");
        }

        // =====================================================================
        //  Case 2 - THE CAPTURE: a 196s suspend jump must not skip the beat
        // =====================================================================

        private static void Case2_SuspendJumpIsNotCharged(List<string> failures)
        {
            var clock = new TutorialFlow.StepClock();

            // The captured shape: ~49s of real play on founding_walk...
            for (int i = 0; i < 49 * 60; i++) clock.Tick(Frame60, false);
            float playedBefore = clock.Charged;
            if (clock.Expired(DefaultBound))
                failures.Add("[bound-suspend] 49s of play already expired the 120s bound - the harness is wrong.");

            // ... then the OS backgrounds the app for 196s and restores it. Unity hands the whole
            // window back as ONE unscaledDeltaTime on the resume frame (it is not clamped by
            // Time.maximumDeltaTime). This single Tick IS the shipped bug.
            clock.Tick(CapturedSuspendSeconds, false);

            if (clock.Expired(DefaultBound))
                failures.Add("[bound-suspend] the captured " + CapturedSuspendSeconds.ToString("0") + "s app-suspend " +
                             "jump EXPIRED the 120s bound on the resume frame - that is F8 seq 2513 exactly: the beat " +
                             "was rescued-and-SKIPPED after ~49s of real play, before the player could move. Background " +
                             "time is not idle time (WO-1036).");

            float chargedByJump = clock.Charged - playedBefore;
            if (chargedByJump > TutorialFlow.StepClock.MaxFrameStepSeconds + 0.001f)
                failures.Add("[bound-suspend] the suspend frame charged " + chargedByJump.ToString("0.00") + "s against " +
                             "the bound - a single frame may contribute at most MaxFrameStepSeconds (" +
                             TutorialFlow.StepClock.MaxFrameStepSeconds.ToString("0.00") + "s).");

            if (clock.DiscardedJumpFrames != 1)
                failures.Add("[bound-suspend] the suspend jump was recorded as " + clock.DiscardedJumpFrames +
                             " discarded frame(s), expected exactly 1 - the discard counter is the evidence line " +
                             "that names a background window, and §12 forbids a silent one.");
            float expectedGap = CapturedSuspendSeconds - TutorialFlow.StepClock.MaxFrameStepSeconds;
            if (Mathf.Abs(clock.DiscardedJumpSeconds - expectedGap) > 0.01f)
                failures.Add("[bound-suspend] DiscardedJumpSeconds reads " + clock.DiscardedJumpSeconds.ToString("0.00") +
                             "s, expected " + expectedGap.ToString("0.00") + "s - the STEP-STUCK line reports this " +
                             "number, so a wrong one mis-triages the next capture.");

            // And the beat must still be rescuable after the resume, on PLAYED time.
            float trip = PlayUntilExpired(clock, DefaultBound, Frame60, false, 60 * 200);
            if (trip < 0f)
                failures.Add("[bound-suspend] after a suspend the bound never expired again - discarding background " +
                             "time must not disarm the watchdog, only stop it charging time the player never had.");
        }

        // =====================================================================
        //  Case 3 - builder / frozen frames are a TRUE pause, never a reset
        // =====================================================================

        private static void Case3_BuilderAndFrozenFramesExcluded(List<string> failures)
        {
            var clock = new TutorialFlow.StepClock();

            // 60s of idle, then 600s inside the builder (F8 seq 603: the player is DOING the ask),
            // then idle again. The bound must trip 60s after the builder closes - not during it,
            // and not 120s after it (which would be a reset, losing the pre-builder idle).
            for (int i = 0; i < 60 * 60; i++) clock.Tick(Frame60, false);
            float beforeBuilder = clock.Charged;
            for (int i = 0; i < 600 * 60; i++) clock.Tick(Frame60, true);

            if (clock.Expired(DefaultBound))
                failures.Add("[bound-excluded] 600s of builder time expired the bound - build-mode time must never " +
                             "count against it (F8 seq 603 rule).");
            if (Mathf.Abs(clock.Charged - beforeBuilder) > 0.01f)
                failures.Add("[bound-excluded] the charged budget moved during builder frames (" +
                             beforeBuilder.ToString("0.00") + " -> " + clock.Charged.ToString("0.00") + ").");
            if (clock.Excluded < 599f)
                failures.Add("[bound-excluded] only " + clock.Excluded.ToString("0") + "s of the 600s builder session " +
                             "was recorded as excluded - the split is what makes a STEP-STUCK line triageable.");

            float trip = PlayUntilExpired(clock, DefaultBound, Frame60, false, 60 * 200);
            if (trip < 0f || Mathf.Abs(trip - DefaultBound) > ToleranceSeconds)
                failures.Add("[bound-excluded] after the builder closed the bound tripped at " + trip.ToString("0.00") +
                             "s of charged time instead of ~" + DefaultBound.ToString("0") + "s - a PAUSE keeps the " +
                             "pre-builder idle; a RESET throws it away.");

            // A frozen world clock (pause menu / OnApplicationPause auto-pause, which never
            // auto-resumes) is excluded on exactly the same footing: the hero cannot move.
            var frozen = new TutorialFlow.StepClock();
            for (int i = 0; i < 600 * 60; i++) frozen.Tick(Frame60, true);
            if (frozen.Expired(DefaultBound))
                failures.Add("[bound-excluded] 600s of timeScale<=0 frames expired the bound - the captured " +
                             "[Flow:HeroOwner] 'WORLD CLOCK FROZEN' state means the hero CANNOT walk, so it is not " +
                             "idle time (WO-1036).");
        }

        // =====================================================================
        //  Case 4 - the placement bound (300s) is honoured on the same clock
        // =====================================================================

        private static void Case4_PlacementBoundHonoured(List<string> failures)
        {
            var clock = new TutorialFlow.StepClock();
            float trip = PlayUntilExpired(clock, PlacementBound, Frame60, false, 60 * 400);
            if (trip < 0f)
            {
                failures.Add("[bound-placement] 400s of clean frames never expired the 300s placement bound.");
                return;
            }
            if (Mathf.Abs(trip - PlacementBound) > ToleranceSeconds)
                failures.Add("[bound-placement] the placement bound tripped at " + trip.ToString("0.00") + "s instead " +
                             "of ~" + PlacementBound.ToString("0") + "s - the WO-1036 question was whether the bound " +
                             "is honoured for OTHER steps too, and this is the answer for the placement kind.");
        }

        // =====================================================================
        //  Case 5 - the coach cadence rides the SAME budget as the watchdog
        // ---------------------------------------------------------------------
        //  The invariant is NOT "all 4 beats land" (45s x 4 = 180s does not fit a 120s bound —
        //  2 beats before a rescue is correct). It is that BACKGROUNDING THE APP CANNOT CHANGE
        //  THE COACHED EXPERIENCE: the player must get the same nudges, at the same played
        //  moments, whether or not the OS suspended the game in the middle of the beat. That is
        //  precisely what the capture violated — 'beat 2/4' delivered at an alleged 245s, with
        //  the watchdog rescuing on the very same frame.
        // =====================================================================

        private static void Case5_CoachCadenceFitsInsideTheBound(List<string> failures)
        {
            const float coachEvery = 45f;   // TutorialFlow.CoachNudgeSeconds
            const int   maxBeats   = 4;     // TutorialFlow.CoachNudgeMaxBeats

            // suspendAtFrame < 0 = a clean session; otherwise the captured 196s jump lands there.
            Func<int, List<float>> replay = suspendAtFrame =>
            {
                var clock = new TutorialFlow.StepClock();
                int beats = 0;
                float next = coachEvery;
                var beatTimes = new List<float>();

                for (int i = 0; i < 60 * 400; i++)
                {
                    if (i == suspendAtFrame) clock.Tick(CapturedSuspendSeconds, false);
                    else clock.Tick(Frame60, false);

                    if (beats < maxBeats && clock.Charged >= next)
                    {
                        beats++;
                        next = clock.Charged + coachEvery;
                        beatTimes.Add(clock.Charged);
                    }
                    if (clock.Expired(DefaultBound)) break;
                }
                return beatTimes;
            };

            List<float> clean     = replay(-1);
            List<float> suspended = replay(49 * 60);   // the captured session: backgrounded ~49s in

            if (clean.Count == 0)
                failures.Add("[bound-coach] the clean replay delivered NO coach beats before the bound expired - a " +
                             "stranded player must always be coached before being rescued (F8 seq 632 root cause 4).");

            if (clean.Count != suspended.Count)
            {
                failures.Add("[bound-coach] a " + CapturedSuspendSeconds.ToString("0") + "s app-suspend changed the " +
                             "number of coach beats delivered before the rescue (" + clean.Count + " clean vs " +
                             suspended.Count + " suspended). Captured defect: the owner got 'beat 2/4' at an alleged " +
                             "245s because a wall-clock cadence burned beats during a background window she never " +
                             "saw. Backgrounding the app must be invisible to the coach (WO-1036).");
            }
            else
            {
                for (int i = 0; i < clean.Count; i++)
                    if (Mathf.Abs(clean[i] - suspended[i]) > TutorialFlow.StepClock.MaxFrameStepSeconds)
                        failures.Add("[bound-coach] coach beat " + (i + 1) + " landed at " + clean[i].ToString("0.0") +
                                     "s played in a clean session but " + suspended[i].ToString("0.0") + "s after a " +
                                     "suspend - the cadence must ride played time, not the wall clock.");
            }
        }

        // =====================================================================
        //  Case 6 - flow wiring + the forbidden "fix", pinned at source
        // =====================================================================

        private static void Case6_WiringAndForbiddenFixesLint(List<string> failures)
        {
            string flow = StripComments(File.ReadAllText(FlowSrc));

            var tick = Regex.Match(flow,
                @"void\s+TickWatchdog\s*\(\s*\)\s*\{(?<body>(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*)\}",
                RegexOptions.Singleline);
            if (!tick.Success)
            {
                failures.Add("[bound-wiring] TickWatchdog body not found in " + FlowSrc + " - the WO-1036 lint cannot " +
                             "verify the bound is spent in played frames.");
            }
            else
            {
                string body = tick.Groups["body"].Value;

                // THE DEFECT ITSELF: a wall-clock delta compared against the bound.
                if (Regex.IsMatch(body, @"Time\s*\.\s*unscaledTime\s*-\s*_\w+\s*<\s*bound") ||
                    Regex.IsMatch(body, @"Time\s*\.\s*unscaledTime\s*-\s*_\w+\s*>=?\s*bound"))
                    failures.Add("[bound-wiring] TickWatchdog compares a WALL-CLOCK stamp against the bound again - " +
                                 "that is the F8 seq 2513 defect: Time.unscaledTime jumps by the whole app-suspend " +
                                 "window on the resume frame, so the entire background window is charged to the step " +
                                 "and the beat is skipped before the player can move (WO-1036).");

                if (!body.Contains("_stepClock.Expired"))
                    failures.Add("[bound-wiring] TickWatchdog no longer trips on _stepClock.Expired(bound) - the played-" +
                                 "frame budget is the single owner of the in-step clock; a second accounting is how the " +
                                 "watchdog and the coach came to disagree.");

                if (!body.Contains("WorldClockFrozen"))
                    failures.Add("[bound-wiring] TickWatchdog no longer stands down while the world clock is frozen - " +
                                 "PauseController.OnApplicationPause auto-pauses and NEVER auto-resumes, so a rescue " +
                                 "can fire while the hero physically cannot walk (captured 'WORLD CLOCK FROZEN').");
            }

            if (!Regex.IsMatch(flow, @"TickStepClock\s*\(\s*\)\s*;"))
                failures.Add("[bound-wiring] TickStepClock is never called from " + FlowSrc + "'s Update loop - the " +
                             "budget would never advance and no step could ever be rescued.");

            // The coach must not re-grow a second, wall-clock cadence.
            var coach = Regex.Match(flow,
                @"void\s+TickCoachNudge\s*\(\s*\)\s*\{(?<body>(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*)\}",
                RegexOptions.Singleline);
            if (coach.Success && Regex.IsMatch(coach.Groups["body"].Value, @"_nextCoachAt\s*=\s*Time\s*\.\s*unscaledTime"))
                failures.Add("[bound-wiring] the coach cadence is a Time.unscaledTime stamp again - it must ride the " +
                             "same played-frame budget as the watchdog, or a background window silently eats the " +
                             "escalating nudge (captured: 'beat 2/4' after an alleged 245s).");

            // THE FORBIDDEN FIX (WO-962 §3, re-asserted by WO-1036): lengthening the bound.
            if (!Regex.IsMatch(flow, @"WatchdogSeconds\s*=\s*120f\s*;"))
                failures.Add("[bound-wiring] TutorialFlow.WatchdogSeconds is no longer 120f - WO-1036 is a CLOCK bug, " +
                             "not a bound that is too short; lengthening it hides the background-time defect instead " +
                             "of fixing it (and WO-962 sec 3 forbids it outright).");
            if (!Regex.IsMatch(flow, @"PlacementWatchdogSeconds\s*=\s*300f\s*;"))
                failures.Add("[bound-wiring] PlacementWatchdogSeconds is no longer 300f - the placement bound is " +
                             "asserted by this suite; retune it deliberately or the oracle is lying.");
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
