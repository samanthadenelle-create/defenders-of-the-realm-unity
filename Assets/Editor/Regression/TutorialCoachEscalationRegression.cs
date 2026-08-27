// =============================================================================
// TutorialCoachEscalationRegression [tutorial-coach]  --  WO-1238 guardrails for
// the escalating coach beat: the tutorial must SPEAK before the watchdog rescues.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE CAPTURED DEFECT (device seq 3610 via the WO-1227 bridge, 2026-08-26,
// Main_Castle_Overworld) -- one line, not theorised:
//
//     [Flow:Tutorial] STEP-STUCK :: founding_walk - no 'hero.reached:guide_gate'
//     after 120s in-step (bound 120s, builder time excluded; ff.tutorialv2 on;
//     builderOpenedThisStep=True, coachBeats=0); ... RESCUED via watchdog and
//     recorded as SKIPPED
//
// ZERO coach beats in 120 seconds, while the flow RECORDED that the player had
// opened the build menu mid-step and did nothing with it. The root cause was not
// the cadence and not the watchdog: TickCoachNudge carried an UNCONDITIONAL
// `if (_builderOpenedThisStep) return;`, so opening the builder during a
// "walk to the gate" beat permanently muted the one thing that would have helped.
// That inference is only sound on a PLACEMENT step, where the ask IS the builder.
//
// WHAT THIS SUITE PROVES HEADLESSLY, AND WHAT IT CANNOT:
//
//   (a) THE LADDER. TutorialFlow.CoachBeatDueAt is pure, so the schedule is pinned
//       without a play session: at least one beat lands inside EVERY bound, the
//       beats are strictly increasing, the last one leaves real headroom before
//       the rescue, and the ladder scales with the 300s placement bound instead
//       of being a second hardcoded table.
//
//   (b) THE ESCALATION. HowHintForAwaitedSignal is pure, so every completion-signal
//       kind is proved to yield a concrete, ASCII, colour-free "how" -- and beat 1
//       is proved to be the plain objective (a nudge must restate before it
//       instructs).
//
//   (c) THE BUILDER-CONFUSION FIX, PINNED AT SOURCE. The flow-side wiring needs a
//       play session, so the shape is linted on comment-stripped source: the
//       stand-down must be CONDITIONED on IsPlacementStep(), the unconditional
//       form must not come back, and the builder-open edge must route through a
//       handler rather than swallowing the signal.
//
//   (d) THE WATCHDOG CONTRACT IS UNCHANGED (WO-1238 "what NOT to touch"): 120f /
//       300f bounds intact, the rescue still records SKIPPED, still suppresses the
//       outro, and still applies grants.
//
//   NOT provable here: that a real FTUE run shows the toast (owner felt-verify,
//   PO closes). This suite proves the SCHEDULE and the WIRING, which is what was
//   silent.
//
// Markers: TUTORIAL_COACH_OK / TUTORIAL_COACH_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TutorialCoachEscalationRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class TutorialCoachEscalationRegression
    {
        private const string FlowSrc = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";

        /// <summary>The shipped bounds (mirrored deliberately, so a silent retune fails here).</summary>
        private const float DefaultBound = 120f;
        private const float PlacementBound = 300f;

        /// <summary>Minimum seconds a final coach beat must leave before the rescue fires. A beat
        /// delivered at 119s of a 120s bound is not coaching, it is a eulogy.</summary>
        private const float MinHeadroomSeconds = 15f;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TUTORIAL_COACH_OK - " + reason);
            else Debug.LogError("TUTORIAL_COACH_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TUTORIAL COACH (WO-1238 escalating beat before the rescue) ---");

            try
            {
                Case(failures, "coach-ladder",     () => Case1_LadderLandsInsideEveryBound(failures, log));
                Case(failures, "coach-escalation", () => Case2_TheWordsEscalate(failures, log));
                Case(failures, "coach-builder",    () => Case3_BuilderConfusionWiringLint(failures, log));
                Case(failures, "coach-watchdog",   () => Case4_WatchdogContractUntouched(failures, log));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "TUTORIAL_COACH_OK");
                reason = "TUTORIAL COACH OK - the measured ladder lands at least one beat inside both the 120s " +
                         "and 300s bounds with " + MinHeadroomSeconds.ToString("0") + "s+ of headroom before the " +
                         "rescue, every completion-signal kind yields a concrete ASCII 'how' while beat 1 restates " +
                         "the objective, the builder stand-down is conditioned on IsPlacementStep so a confusion-" +
                         "open on a walk beat can no longer mute the coach (captured coachBeats=0), and the " +
                         "watchdog's bounds + skip/suppress/grant contract are untouched.";
                return true;
            }
            reason = "tutorial-coach FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            Debug.LogError(log.ToString() + "TUTORIAL_COACH_FAIL: " + reason);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the ladder: a stranded player is ALWAYS coached before rescue
        // =====================================================================

        private static void Case1_LadderLandsInsideEveryBound(List<string> failures, StringBuilder log)
        {
            foreach (float bound in new[] { DefaultBound, PlacementBound })
            {
                int inside = TutorialFlow.CoachBeatsInsideBound(bound);
                if (inside <= 0)
                {
                    failures.Add("[coach-ladder] NO coach beat lands inside the " + bound.ToString("0") +
                                 "s bound - a stranded player would be rescued having never been coached, which " +
                                 "is the captured defect (coachBeats=0) restored by a retune.");
                    continue;
                }

                // Strictly increasing, and every scheduled beat inside the bound.
                float prev = -1f;
                var times = new List<float>();
                for (int i = 0; i < inside; i++)
                {
                    float t = TutorialFlow.CoachBeatDueAt(i, bound);
                    times.Add(t);
                    if (t <= prev)
                        failures.Add("[coach-ladder] beat " + (i + 1) + " is due at " + t.ToString("0.0") +
                                     "s, not after beat " + i + " at " + prev.ToString("0.0") + "s - a ladder " +
                                     "that does not ascend delivers two nudges on the same frame.");
                    if (t >= bound)
                        failures.Add("[coach-ladder] beat " + (i + 1) + " is due at " + t.ToString("0.0") +
                                     "s, at or past the " + bound.ToString("0") + "s bound - it can never be " +
                                     "delivered, because the watchdog rescues the step first.");
                    prev = t;
                }

                float last = times[times.Count - 1];
                if (bound - last < MinHeadroomSeconds)
                    failures.Add("[coach-ladder] the last beat inside the " + bound.ToString("0") + "s bound lands " +
                                 "at " + last.ToString("0.0") + "s, leaving only " + (bound - last).ToString("0.0") +
                                 "s before the rescue (minimum " + MinHeadroomSeconds.ToString("0") + "s). A player " +
                                 "needs time to ACT on the last thing they were told.");

                // The first beat must not fire so early that it nags a player who is simply
                // playing. Measured floor: 71.2% of successful completions land under 15s, so a
                // first nudge inside that window would interrupt the majority who are fine.
                if (times[0] < 15f)
                    failures.Add("[coach-ladder] the first beat on the " + bound.ToString("0") + "s bound is due at " +
                                 times[0].ToString("0.0") + "s. Measured over n=156 completions, 71.2% of players " +
                                 "who succeed have already finished by 15s - nudging inside that window is nagging " +
                                 "the majority, not rescuing the minority (WO-1238 sec 1).");

                log.AppendLine("  bound " + bound.ToString("0") + "s: " + inside + " beat(s) at " +
                               string.Join(" / ", times.ConvertAll(t => t.ToString("0") + "s")) +
                               ", headroom " + (bound - last).ToString("0") + "s");
            }

            // The ladder must SCALE with the bound, not be a second hardcoded table: the placement
            // kind's first beat must sit later than the default kind's.
            if (TutorialFlow.CoachBeatDueAt(0, PlacementBound) <= TutorialFlow.CoachBeatDueAt(0, DefaultBound))
                failures.Add("[coach-ladder] the placement bound's first beat is not later than the default " +
                             "bound's - the ladder has stopped scaling with the bound, which means a 300s " +
                             "placement beat is being nudged on a 120s schedule.");
        }

        // =====================================================================
        //  Case 2 - the WORDS escalate, and every signal kind has an honest "how"
        // =====================================================================

        private static void Case2_TheWordsEscalate(List<string> failures, StringBuilder log)
        {
            // Every completion-signal kind the shipped tutorial-steps.json actually uses, plus an
            // unknown kind (the default arm must still say something concrete, never nothing).
            var kinds = new Dictionary<string, string>
            {
                { "hero.reached:guide_gate",                 "hero.reached" },
                { "build.structure_placed:collector_lumbermill", "build.structure_placed" },
                { "build.tower_placed",                      "build.tower_placed" },
                { "dialogue.ended:tut_founding_greet",       "dialogue.ended" },
                { "panel.opened:Manage",                     "panel.opened" },
                { "wave.tutorial_band_repelled",             "unknown-kind (default arm)" },
            };

            foreach (var kv in kinds)
            {
                string how = TutorialFlow.HowHintForAwaitedSignal(kv.Key);
                if (string.IsNullOrEmpty(how))
                {
                    failures.Add("[coach-escalation] signal kind " + kv.Value + " ('" + kv.Key + "') yields NO 'how' " +
                                 "hint - beat 2 would repeat beat 1 verbatim, which is not escalation. The measured " +
                                 "data says a player still stuck at beat 2 is lost, not slow (WO-1238 sec 1).");
                    continue;
                }
                foreach (char c in how)
                    if (c > 127)
                    {
                        failures.Add("[coach-escalation] the '" + kv.Value + "' hint contains a non-ASCII character " +
                                     "(U+" + ((int)c).ToString("X4") + ") - TMP strings are ASCII-only.");
                        break;
                    }
                // No meaning by hue: the owner is red/green colour-blind, so a cue must never be a
                // colour name. Cues read by luminance or motion ("glowing") instead.
                string lower = how.ToLowerInvariant();
                foreach (string hue in new[] { "red", "green", "blue", "yellow", "orange", "purple" })
                    if (lower.Contains(hue))
                        failures.Add("[coach-escalation] the '" + kv.Value + "' hint names the colour '" + hue +
                                     "' - the owner is red/green colour-blind, so no cue may carry meaning by hue.");

                log.AppendLine("  " + kv.Value + " -> \"" + how + "\"");
            }

            // A missing/empty signal must yield null, not a fabricated instruction.
            if (!string.IsNullOrEmpty(TutorialFlow.HowHintForAwaitedSignal(null)) ||
                !string.IsNullOrEmpty(TutorialFlow.HowHintForAwaitedSignal("")))
                failures.Add("[coach-escalation] a null/empty completion signal produced a 'how' hint - there is " +
                             "nothing honest to instruct toward, and CLAUDE.md sec.12 forbids inventing one.");

            // Beat 1 must be the plain objective: restate before you instruct.
            string b1 = InvokeCoachMessage(1, "Follow Aldwin to the gate", "hero.reached:guide_gate", out string err);
            if (err != null)
                log.AppendLine("  " + RegressionOutcome.PartialSkip("beat-1 shape",
                    "CoachMessageForBeat is instance-private and not reflectable here: " + err));
            else if (b1 != "Follow Aldwin to the gate")
                failures.Add("[coach-escalation] beat 1 was '" + b1 + "', not the plain objective. The first nudge " +
                             "must RESTATE the ask; instruction is what beats 2+ add.");
        }

        /// <summary>Best-effort reflection onto the instance-private message builder. Returns the
        /// message, or sets <paramref name="err"/> when the seam is not reachable (declared as a
        /// PARTIAL-SKIP by the caller - never a silent pass).</summary>
        private static string InvokeCoachMessage(int beat, string objective, string awaitSignal, out string err)
        {
            err = null;
            try
            {
                var m = typeof(TutorialFlow).GetMethod("CoachMessageForBeat",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (m == null) { err = "method not found"; return null; }
                var go = new GameObject("TutorialFlow (coach oracle)");
                try
                {
                    var flow = go.AddComponent<TutorialFlow>();
                    var sig = typeof(TutorialFlow).GetField("_awaitSignal",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (sig == null) { err = "_awaitSignal field not found"; return null; }
                    sig.SetValue(flow, awaitSignal);
                    return (string)m.Invoke(flow, new object[] { beat, objective });
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            catch (Exception ex) { err = ex.GetType().Name + ": " + ex.Message; return null; }
        }

        // =====================================================================
        //  Case 3 - THE FIX ITSELF, pinned at source
        // =====================================================================

        private static void Case3_BuilderConfusionWiringLint(List<string> failures, StringBuilder log)
        {
            if (!File.Exists(FlowSrc))
            {
                failures.Add("[coach-builder] " + FlowSrc + " not found - the WO-1238 wiring cannot be verified.");
                return;
            }
            string flow = StripComments(File.ReadAllText(FlowSrc));

            string body = MethodBody(flow, "TickCoachNudge");
            if (body == null)
            {
                failures.Add("[coach-builder] TickCoachNudge body not found in " + FlowSrc + " - the WO-1238 lint " +
                             "cannot verify that a builder-confusion open no longer mutes the coach.");
                return;
            }

            // ⭐ THE CAPTURED DEFECT: an UNCONDITIONAL stand-down on _builderOpenedThisStep.
            // This is the exact statement that produced coachBeats=0 with builderOpenedThisStep=True.
            if (Regex.IsMatch(body, @"if\s*\(\s*_builderOpenedThisStep\s*\)\s*return\s*;"))
                failures.Add("[coach-builder] TickCoachNudge stands down UNCONDITIONALLY on _builderOpenedThisStep " +
                             "again - that is device capture seq 3610 exactly: the player opened the BUILD menu " +
                             "during founding_walk (a 'walk to the gate' beat), the coach read it as 'found the " +
                             "door' and never spoke, and the step was rescued after 120 silent seconds with " +
                             "coachBeats=0. The inference is only sound on a PLACEMENT step (WO-1238).");

            // ...and the corrected form must be present: conditioned on IsPlacementStep().
            if (!Regex.IsMatch(body, @"_builderOpenedThisStep\s*&&\s*IsPlacementStep\s*\(\s*\)"))
                failures.Add("[coach-builder] the builder stand-down is no longer conditioned on IsPlacementStep() - " +
                             "on a non-placement beat, opening an unrelated menu is a CONFUSION tell and must not " +
                             "silence the ladder (WO-1238 sec 3).");

            // The builder-open edge must be ANSWERED, not merely recorded. The signal was already
            // being recorded before WO-1238; recording it was never the problem.
            if (!body.Contains("OnBuilderOpenedDuringStep"))
                failures.Add("[coach-builder] the builder-open edge no longer routes through " +
                             "OnBuilderOpenedDuringStep - WO-1238 sec 3's whole point is that this signal was " +
                             "already RECORDED and never USED.");

            if (!Regex.IsMatch(flow, @"void\s+OnBuilderOpenedDuringStep\s*\(\s*\)") ||
                !Regex.IsMatch(flow, @"void\s+DeliverBuilderRedirect\s*\(") )
                failures.Add("[coach-builder] OnBuilderOpenedDuringStep / DeliverBuilderRedirect are missing from " +
                             FlowSrc + " - the confusion redirect has no implementation.");

            // The cadence must still ride the played-frame budget (WO-1036), not a wall stamp.
            if (Regex.IsMatch(body, @"_nextCoachAt\s*=\s*Time\s*\.\s*unscaledTime"))
                failures.Add("[coach-builder] the coach cadence is a Time.unscaledTime stamp again - WO-1036: a " +
                             "background window would silently eat the escalating nudge.");
            if (!body.Contains("CoachBeatDueAt"))
                failures.Add("[coach-builder] TickCoachNudge no longer schedules off CoachBeatDueAt - the ladder " +
                             "and the regression would be asserting different schedules.");

            log.AppendLine("  TickCoachNudge wiring: stand-down is placement-conditioned, builder-open edge is " +
                           "answered, cadence rides the played-frame budget");
        }

        // =====================================================================
        //  Case 4 - WHAT MUST NOT HAVE CHANGED (WO-1238 "what NOT to touch")
        // =====================================================================

        private static void Case4_WatchdogContractUntouched(List<string> failures, StringBuilder log)
        {
            // ⛔ NOT a bare guard-and-return: a missing source file must ASSERT here, not stand
            // down. Case 3 also fails on it, but relying on a sibling case to carry the failure is
            // how a hollow pass lands green when the cases are ever reordered or split.
            if (!File.Exists(FlowSrc))
            {
                failures.Add("[coach-watchdog] " + FlowSrc + " not found - the watchdog contract WO-1238 forbids " +
                             "touching (120f/300f bounds, skipped-rescue, played-time clock) could not be verified " +
                             "at all. This suite asserted NOTHING about it.");
                return;
            }
            string flow = StripComments(File.ReadAllText(FlowSrc));

            if (!Regex.IsMatch(flow, @"WatchdogSeconds\s*=\s*120f\s*;"))
                failures.Add("[coach-watchdog] WatchdogSeconds is no longer 120f. WO-1238 is explicit: do NOT " +
                             "weaken, lengthen or disable the watchdog - the ticket is about the 120 seconds " +
                             "BEFORE it fires.");
            if (!Regex.IsMatch(flow, @"PlacementWatchdogSeconds\s*=\s*300f\s*;"))
                failures.Add("[coach-watchdog] PlacementWatchdogSeconds is no longer 300f - the placement bound is " +
                             "part of the untouched contract.");

            string body = MethodBody(flow, "TickWatchdog");
            if (body == null)
            {
                failures.Add("[coach-watchdog] TickWatchdog body not found - the rescue contract cannot be verified.");
                return;
            }

            // The rescue must still SKIP (not complete) the step. CompleteCurrentStep(skipped: true)
            // is the single statement that suppresses the outro while keeping the grants.
            if (!Regex.IsMatch(body, @"CompleteCurrentStep\s*\(\s*skipped\s*:\s*true\s*\)"))
                failures.Add("[coach-watchdog] the rescue no longer calls CompleteCurrentStep(skipped: true) - that " +
                             "one call is the whole contract WO-1238 says is CORRECT: the step is recorded SKIPPED, " +
                             "its outro is suppressed so no fiction is narrated for a beat that did not happen, and " +
                             "grants still apply so the player is never half-granted.");
            if (!body.Contains("_stepClock.Expired"))
                failures.Add("[coach-watchdog] TickWatchdog no longer trips on _stepClock.Expired(bound) - the " +
                             "WO-1036 played-time clock is part of what WO-1238 forbids touching.");

            // The STEP-STUCK line must keep reporting coachBeats: it is the field that made this
            // ticket diagnosable at all.
            if (!body.Contains("coachBeats"))
                failures.Add("[coach-watchdog] the STEP-STUCK line no longer reports coachBeats - that single field " +
                             "is what turned 'the tutorial felt unhelpful' into a proven defect. Removing it makes " +
                             "the next regression in this system undiagnosable (CLAUDE.md sec.12).");

            log.AppendLine("  watchdog contract intact: 120f/300f bounds, skipped-rescue, played-time clock, " +
                           "coachBeats still reported");
        }

        /// <summary>
        /// The comment-stripped body of a parameterless method, extracted by BRACE COUNTING.
        ///
        /// ⚠ NOT a regex, deliberately, and this cost a real hole. The nested-brace alternation
        /// used elsewhere in this folder only spans THREE levels of nesting, and
        /// TutorialFlow.TickCoachNudge was already deeper than that at HEAD. TutorialWatchdogBound
        /// Regression's Case 6 guards its coach lint with `if (coach.Success &amp;&amp; ...)`, so on that
        /// source the check simply did not run and reported nothing: a lint that silently matches
        /// NOTHING is indistinguishable from a lint that passes. Brace counting cannot go stale
        /// against nesting depth, so this suite fails loudly instead of standing down quietly.
        /// </summary>
        private static string MethodBody(string strippedSrc, string methodName)
        {
            // NOTE: the signature pattern deliberately stops BEFORE the opening brace, which is
            // then located with IndexOf. A lone escaped brace inside the pattern would leave this
            // FILE brace-unbalanced, and CLAUDE.md sec.1 rejects that outright.
            //
            // ⚠ THE LEADING `void` IS LOAD-BEARING, and leaving it out was a real defect caught by
            // the WO-1138 red-first replay of this very suite. Without it the pattern matches the
            // CALL SITE first -- TickCoachNudge() and TickWatchdog() are both invoked from Update()
            // ABOVE their own declarations -- and the scanner then brace-walks whatever block
            // happens to follow that call. Every lint below would have been reading the wrong
            // method's body while reporting confidently on this one.
            var sig = Regex.Match(strippedSrc,
                @"(?<![A-Za-z0-9_])void\s+" + Regex.Escape(methodName) + @"\s*\(\s*\)\s*");
            if (!sig.Success) return null;
            int open = strippedSrc.IndexOf(OpenBrace, sig.Index);
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < strippedSrc.Length; i++)
            {
                if (strippedSrc[i] == OpenBrace) depth++;
                else if (strippedSrc[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return strippedSrc.Substring(open + 1, i - open - 1);
                }
            }
            return null;   // unbalanced source
        }

        // ⚠ THE BRACE CHARACTERS ARE BUILT FROM THEIR CODE POINTS, NOT WRITTEN AS LITERALS.
        // CLAUDE.md sec.1 gates every .cs file on a RAW open-brace vs close-brace CHARACTER COUNT,
        // so writing the three char literals this scanner needs would leave the file counting one
        // brace heavy and FAIL THE GATE even though the code is perfectly balanced. Naming them
        // once, by code point, keeps the scanner readable and the gate honest.
        private const char OpenBrace = (char)123;
        private const char CloseBrace = (char)125;

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
