// =============================================================================
// EnemyProbeCadenceRegression - WO-1450 (probe log evicts the logcat ring) and
// WO-1459 sec.2 suspect 3 (per-frame ProbeForStructure physics from Enemy:Update).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - registered into DataRegression.RunAll by the orchestrator.
//
// HONEST SCOPE (WO-1494: six suites claimed to MEASURE and were source text lint).
// THIS SUITE IS A SOURCE LINT AND SAYS SO IN EVERY REASON STRING. It reads
// Enemy.cs, strips comments and string literals, and asserts the SHAPE of the two
// guards holds. It does NOT and CANNOT prove the device line count or the frame
// cost - those are a device capture (WO-1450 acceptance 1, WO-1459 acceptance 1).
// What a lint CAN do is stop the guards being deleted or reverted by a later edit,
// which is exactly how the unthrottled Step got back in front of a tester.
//
// WHAT THIS PINS, AND WHY EACH ONE IS HERE
//   1. [no-step]     the acquire trace is NOT FlowTrace.Step. That single call emitted
//                    38,018 lines at ~320/sec into a 256 KiB Android ring, evicting the
//                    boot window in under two seconds. A revert to Step must FAIL here.
//   2. [kept]        ...and the trace still EXISTS. CLAUDE.md sec.12: instrumentation is
//                    permanent. "Fixing" the spam by deleting the line is the banned
//                    outcome - it turns a logged acquisition back into a silent one.
//   3. [change-gate] the acquire trace fires on a target CHANGE (_lastProbeTargetId),
//                    not on every probe hit. Without this the throttle alone still lets
//                    one line per second per enemy through forever.
//   4. [cadence]     a _nextProbeAt gate guards the ProbeForStructure() call and the
//                    interval is a real, bounded number. ProbeForStructure runs a
//                    SphereCast plus an all-layer (~0) OverlapSphere; ungated that is
//                    ~1,560 physics queries a second at the captured 13 enemies.
//   5. [bounded]     ...and the interval is NEVER so long the enemy goes blind. A gate
//                    of several seconds is a worse bug (an enemy stands in front of a
//                    wall doing nothing), so "still responsive" is an assertion.
//   6. [drop-paths]  all THREE _currentTarget = null sites in the combat path carry a
//                    PERMANENT throttled trace naming WHICH release fired. This is the
//                    evidence the next capture needs to name a re-acquisition thrash;
//                    a capture that cannot tell the three apart is why WO-1459 could
//                    not choose between its three suspects.
//   7. [reset]       the pool reset clears both new fields, so a reused body probes on
//                    its first live frame and its first acquire reads as a CHANGE.
//   8. [semantics]   the guards did NOT change target selection: the faction rule
//                    (CombatFactionRules.MayAttack) and the hero-primary suppression
//                    are still in ProbeForStructure. WO-1439 shipped that faction test
//                    at the cost of 11,620 friendly-fire lines; a cadence lane must not
//                    quietly cost it back.
// =============================================================================
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EnemyProbeCadenceRegression
    {
        private const string EnemyPath = "Assets/_Modules/Village/Enemies/Enemy.cs";

        // The probe fires a SphereCast + an all-layer OverlapSphere. Faster than this and
        // the gate is not buying back the frame cost WO-1459 measured; slower and the
        // enemy is visibly blind in front of a wall it should be hitting.
        private const float MinSaneInterval = 0.05f;
        private const float MaxSaneInterval = 1.0f;

        public static bool Run(out string reason)
        {
            var notes = new StringBuilder();

            string full = Path.Combine(Directory.GetCurrentDirectory(), EnemyPath);
            if (!File.Exists(full))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL: " + EnemyPath + " not found.";
                return false;
            }

            string raw = File.ReadAllText(full);
            // TWO views, and the difference matters. `code` has comments AND string literals
            // stripped - prose in a header cannot fake a pass (the AggroLeashRegression
            // discipline). But a FlowTrace THROTTLE KEY legitimately LIVES in a literal, so
            // asserting the keys against `code` would check text that was just deleted and
            // fail forever. Key/message checks therefore run against `nocomments` (comments
            // gone, literals kept); identifier and call-shape checks run against `code`.
            string nocomments = StripComments(raw);
            string code = StripLiterals(nocomments);

            // --- 1/2. the acquire trace is throttled, and it still exists -----------------
            // Scanned, not regexed: an interpolated message contains parentheses and braces,
            // so a `[^)]*` pattern reads past the call it is trying to bound.
            string stepOffender = FirstStepCallContaining(
                nocomments, new[] { "ProbeForStructure", "probe-hit-" });
            if (stepOffender != null)
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [no-step]: an unthrottled " +
                         "FlowTrace.Step still reports a ProbeForStructure acquisition (offending " +
                         "token: " + stepOffender + "). That call emitted 38,018 device lines at " +
                         "~320/sec (WO-1450) and evicts the 256 KiB Android ring in under two " +
                         "seconds. Use FlowTrace.Throttle.";
                return false;
            }

            bool hasThrottledAcquire = nocomments.Contains("probe-hit-");
            if (!hasThrottledAcquire)
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [kept]: the structure-acquire trace " +
                         "is GONE, not throttled. CLAUDE.md sec.12 - instrumentation is permanent; " +
                         "throttle or flag it off, never strip it. Restore the probe-hit- Throttle.";
                return false;
            }
            notes.Append("acquire-trace=Throttle(probe-hit) ");

            // --- 3. the acquire trace is gated on a target CHANGE -------------------------
            if (!code.Contains("_lastProbeTargetId"))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [change-gate]: no _lastProbeTargetId. " +
                         "A throttle alone still emits one line per second per enemy forever; the " +
                         "acquire trace must fire on a target CHANGE, which is the readable event.";
                return false;
            }
            notes.Append("change-gate=_lastProbeTargetId ");

            // --- 4/5. the probe call is cadence-gated, and the interval is sane -----------
            if (!code.Contains("_nextProbeAt"))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [cadence]: no _nextProbeAt gate. " +
                         "ProbeForStructure runs a SphereCast plus an OverlapSphere on mask ~0 (ALL " +
                         "layers); ungated at the captured 13 enemies that is ~1,560 physics queries " +
                         "a second (WO-1459: LOW fps=11 ms=87.4, timeScale=1.00).";
                return false;
            }

            var m = Regex.Match(code,
                @"ProbeIntervalSeconds\s*=\s*([0-9]*\.?[0-9]+)\s*f?\s*;");
            if (!m.Success)
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [cadence]: ProbeIntervalSeconds is not " +
                         "a readable literal constant. The cadence must be a named, auditable number - " +
                         "a magic expression cannot be range-checked by this oracle.";
                return false;
            }

            float interval;
            if (!float.TryParse(m.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out interval))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [cadence]: ProbeIntervalSeconds value '" +
                         m.Groups[1].Value + "' did not parse.";
                return false;
            }

            if (interval < MinSaneInterval || interval > MaxSaneInterval)
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [bounded]: ProbeIntervalSeconds=" +
                         interval.ToString("F2") + " is outside [" + MinSaneInterval.ToString("F2") +
                         ".." + MaxSaneInterval.ToString("F2") + "]. Too small buys back none of the " +
                         "measured frame cost; too large blinds the enemy in front of a wall it " +
                         "should be striking - a worse bug than the one being fixed.";
                return false;
            }
            notes.Append("interval=").Append(interval.ToString("F2")).Append("s ");

            // --- 6. all three drop paths are named in the trace ---------------------------
            string[] dropKeys = { "drop-dead-", "drop-dist-", "drop-death-" };
            foreach (string key in dropKeys)
            {
                if (!nocomments.Contains(key))
                {
                    reason = "[enemy-probe-cadence] SOURCE LINT FAIL [drop-paths]: missing the '" + key +
                             "' release trace. All three _currentTarget = null sites must name which " +
                             "path fired, or the next capture cannot tell a re-acquisition thrash from " +
                             "a death - which is precisely why WO-1459 could not pick among its three " +
                             "suspects without another device session.";
                    return false;
                }
            }
            notes.Append("drop-paths=3/3 ");

            // A drop trace that is itself unthrottled would reintroduce the defect this
            // ticket exists to close, so the release lines must be Throttle, not Step.
            string dropStepOffender = FirstStepCallContaining(nocomments, dropKeys);
            if (dropStepOffender != null)
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [drop-paths]: the '" + dropStepOffender +
                         "' release trace is a FlowTrace.Step. A drop path can fire every frame " +
                         "while a target oscillates across the drop ring - that is the same ring " +
                         "eviction WO-1450 is closing. Use FlowTrace.Throttle.";
                return false;
            }

            // --- 7. the pool reset clears both fields ------------------------------------
            int resetIdx = code.IndexOf("ResetForPool", StringComparison.Ordinal);
            if (resetIdx < 0)
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [reset]: ResetForPool not found in " +
                         EnemyPath + ".";
                return false;
            }
            string resetTail = code.Substring(resetIdx);
            if (!Regex.IsMatch(resetTail, @"_nextProbeAt\s*=") ||
                !Regex.IsMatch(resetTail, @"_lastProbeTargetId\s*=\s*0"))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [reset]: ResetForPool does not clear " +
                         "BOTH _nextProbeAt and _lastProbeTargetId. A reused body would inherit the " +
                         "dead one's target id and swallow the acquire trace for its first real " +
                         "target - a silent hole in the very evidence this ticket adds.";
                return false;
            }
            notes.Append("pool-reset=clears-both ");

            // --- 8. selection semantics untouched ----------------------------------------
            if (!code.Contains("CombatFactionRules"))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [semantics]: the WO-1439 faction test " +
                         "(CombatFactionRules.MayAttack) is gone from Enemy.cs. A cadence lane must not " +
                         "change who may be attacked - that rule cost 11,620 'hit RaidSpire' lines to " +
                         "learn, when a Hostile garrison attacked its own objective.";
                return false;
            }
            if (!code.Contains("IsHeroWithinAggro"))
            {
                reason = "[enemy-probe-cadence] SOURCE LINT FAIL [semantics]: the HERO-PRIMARY " +
                         "suppression (IsHeroWithinAggro) is gone from the probe. Rate-limiting the " +
                         "probe must not re-order target selection.";
                return false;
            }
            notes.Append("semantics=faction+hero-primary intact");

            reason = "[enemy-probe-cadence] SOURCE LINT PASS (shape only - the device line count and " +
                     "the frame cost are proven by a capture, not by this suite): " + notes;
            return true;
        }

        /// <summary>
        /// Comments out. Prose in a header must never satisfy an assertion about the code.
        /// Deliberately simple - it runs over one known file, not arbitrary C#.
        /// </summary>
        private static string StripComments(string src)
        {
            string s = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            s = Regex.Replace(s, @"//[^\n]*", " ");
            return s;
        }

        /// <summary>String literal BODIES out (verbatim first, then plain/interpolated).</summary>
        private static string StripLiterals(string src)
        {
            string s = Regex.Replace(src, "@\"(?:[^\"]|\"\")*\"", "\"\"", RegexOptions.Singleline);
            s = Regex.Replace(s, "\"(?:\\\\.|[^\"\\\\\\n])*\"", "\"\"");
            return s;
        }

        /// <summary>
        /// Returns the first token in <paramref name="tokens"/> that appears inside a
        /// FlowTrace.Step( ... ) call, or null. A brace-counted scan rather than a regex:
        /// an interpolated trace message carries its own parens and braces, so any
        /// character-class pattern bounding the call reads straight past it.
        /// </summary>
        private static string FirstStepCallContaining(string src, string[] tokens)
        {
            const string Marker = "FlowTrace.Step(";
            int i = 0;
            while (true)
            {
                int start = src.IndexOf(Marker, i, StringComparison.Ordinal);
                if (start < 0) return null;

                int p = start + Marker.Length;
                int depth = 1;
                while (p < src.Length && depth > 0)
                {
                    char c = src[p];
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    p++;
                }

                string call = src.Substring(start, Math.Min(p, src.Length) - start);
                foreach (string t in tokens)
                    if (call.IndexOf(t, StringComparison.Ordinal) >= 0) return t;

                i = start + Marker.Length;
            }
        }
    }
}
