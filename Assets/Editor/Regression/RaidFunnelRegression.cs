// =============================================================================
// RaidFunnelRegression [raid-funnel]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: RAID_FUNNEL_OK / _FAIL.
//
// WO-1374 / north-star map section 11. THE HIGHEST-VALUE ITEM IN THE TICKET,
// because it is what makes everything else falsifiable:
//
//   Barracks unlocked -> Army trained -> First raid attempted -> First raid won
//      -> Raid reward spent -> SECOND RAID ATTEMPTED WITHIN 24H
//
//   Owner: "That last one is the gold nugget. If someone raids once and chooses
//   to raid again, your loop is beginning to work. If they don't, more tutorial
//   text probably isn't the answer."
//
// -----------------------------------------------------------------------------
// WHAT AN ANALYTICS ORACLE CAN AND CANNOT PROVE, STATED HONESTLY.
// -----------------------------------------------------------------------------
// It CANNOT prove an event reached Neon - that needs a network and a database,
// and a suite that pretends otherwise would be worse than none. What it CAN
// prove, decidably and offline, is every way this feature dies quietly:
//
//   (A) the six steps exist, are distinct, and are named on ONE convention -
//       a duplicate or a typo'd name is a step that reports as zero forever;
//   (B) the 24h window arithmetic is right at every boundary INCLUDING the ones
//       a real device produces (a clock that moved backwards, a missing stamp);
//   (C) the emission goes through the EXISTING EventTracker rail and nothing
//       else - the work order forbids a second telemetry path in capitals;
//   (D) all six steps are actually WIRED to a call site in shipping code. An
//       event class nobody calls is the analytics equivalent of an unregistered
//       oracle, and this repo has been bitten by that twice (WO-973, WO-978 5F).
//
// PROVEN RED FIRST: (D) fails by construction against the pre-WO-1374 tree,
// where RaidFunnel did not exist and none of the five call sites named it. (B)
// was driven against a deliberately-wrong window first - a `<=` bound passes
// "exactly 24h is inside", which case B5 rejects.
//
// Zero scene, zero network, zero PlayMode. PlayerPrefs is snapshotted, cleared
// and restored so a run can never latch a funnel step on the build machine - a
// test that only passes the first time is not a test.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.Analytics;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Pins the WO-1374 six-event funnel: distinct names on one rail, correct 24h
    /// boundaries, and every step wired to a real call site.
    /// </summary>
    public static class RaidFunnelRegression
    {
        private const long Hour = 60L * 60L * 1000L;
        private const long Day = 24L * Hour;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- RAID FUNNEL (WO-1374, map section 11) ---");

            // Snapshot every key the funnel owns so nothing this suite does survives it.
            var snapshot = SnapshotFunnelPrefs();

            try
            {
                // =============================================================
                //  (A) SIX STEPS, DISTINCT, ONE NAMING CONVENTION.
                // =============================================================
                var names = RaidFunnel.EventNames;
                if (names == null || names.Length != 6)
                {
                    failures.Add("[A] the funnel declares " + (names == null ? "null" : names.Length.ToString()) +
                                 " events, expected exactly 6. The map names six steps and a funnel with a " +
                                 "missing rung cannot be read.");
                }
                else
                {
                    var seen = new HashSet<string>();
                    foreach (var n in names)
                    {
                        if (string.IsNullOrEmpty(n)) { failures.Add("[A] a funnel event name is empty"); continue; }
                        if (!seen.Add(n))
                            failures.Add("[A] duplicate funnel event name '" + n + "' - two steps sharing a name " +
                                         "makes one of them permanently unreadable in analytics_events");
                        if (!n.StartsWith("raid_funnel_", System.StringComparison.Ordinal))
                            failures.Add("[A] funnel event '" + n + "' is not prefixed raid_funnel_ - the six must " +
                                         "be pullable with one query and never confusable with a gameplay event");
                        foreach (char c in n)
                            if (!(char.IsLower(c) || char.IsDigit(c) || c == '_'))
                            { failures.Add("[A] funnel event '" + n + "' is not snake_case ASCII (EventTracker's convention)"); break; }
                    }
                    log.AppendLine("  events: " + string.Join(", ", names));
                }

                // The order in EventNames IS the funnel order - a reader of the array must
                // be able to trust it as the sequence, not as an arbitrary list.
                if (names != null && names.Length == 6)
                {
                    if (names[0] != RaidFunnel.EventBarracksUnlocked ||
                        names[1] != RaidFunnel.EventArmyTrained ||
                        names[2] != RaidFunnel.EventFirstRaidAttempted ||
                        names[3] != RaidFunnel.EventFirstRaidWon ||
                        names[4] != RaidFunnel.EventRaidRewardSpent ||
                        names[5] != RaidFunnel.EventSecondRaidWithin24h)
                        failures.Add("[A] EventNames is not in the map's funnel order (barracks -> army -> " +
                                     "attempted -> won -> spent -> second within 24h)");
                }

                // =============================================================
                //  (B) THE 24-HOUR WINDOW, AT EVERY BOUNDARY THAT MATTERS.
                // =============================================================
                if (RaidFunnel.SecondRaidWindowMs != Day)
                    failures.Add("[B] the second-raid window is " + RaidFunnel.SecondRaidWindowMs +
                                 "ms, expected 24h (" + Day + "ms) - the map says WITHIN 24H");

                long t0 = 1_700_000_000_000L;   // an arbitrary but fixed epoch; nothing reads the clock
                AssertWindow(failures, "B1 same instant", t0, t0, true);
                AssertWindow(failures, "B2 one hour later", t0, t0 + Hour, true);
                AssertWindow(failures, "B3 one ms inside the bound", t0, t0 + Day - 1, true);
                // ⛔ EXCLUSIVE at the bound. A `<=` here would report "within 24 hours" for a
                // raid at exactly 24:00:00.000, which is not what the map asked and is the
                // kind of off-by-one that silently inflates the one number the programme is
                // judged on. This case is what red-proved the bound.
                AssertWindow(failures, "B4 exactly at the bound", t0, t0 + Day, false);
                AssertWindow(failures, "B5 a minute past the bound", t0, t0 + Day + 60000L, false);
                // Real devices produce both of these.
                AssertWindow(failures, "B6 no first attempt recorded", 0L, t0, false);
                AssertWindow(failures, "B7 negative first attempt", -5L, t0, false);
                AssertWindow(failures, "B8 clock moved BACKWARDS", t0, t0 - Hour, false);

                // =============================================================
                //  (C) ONE RAIL. The work order forbids a second telemetry path.
                // =============================================================
                string funnelCode = RaidLootCurrencyRegression.ReadStripped("RaidFunnel.cs");
                if (funnelCode == null)
                {
                    failures.Add("[C] RaidFunnel.cs not found under Assets/_Modules - the rail lint cannot run, " +
                                 "and a lint that silently skips is worse than no lint");
                }
                else
                {
                    if (!funnelCode.Contains("EventTracker.Track"))
                        failures.Add("[C] RaidFunnel.cs live code never calls EventTracker.Track - the six events " +
                                     "reach no rail at all");
                    // A second path would look like exactly one of these.
                    string[] banned = { "UnityWebRequest", "HttpClient", "/api/", "WebRequest.Post", "vercel.app" };
                    foreach (var b in banned)
                        if (funnelCode.Contains(b))
                            failures.Add("[C] RaidFunnel.cs live code contains '" + b + "' - that is a SECOND " +
                                         "telemetry path. WO-1374 forbids it: a parallel POST gets its own queue, " +
                                         "its own outage behaviour and its own idea of playerId, so the funnel " +
                                         "would be measured on a different reliability curve from every other " +
                                         "event and nobody could say which half was lying.");
                }

                // =============================================================
                //  (D) EVERY STEP IS WIRED. An uncalled event reports zero forever.
                // =============================================================
                RequireCaller(failures, "SceneRouter.cs", "step 3 + 6 (raid attempted / the gold nugget)", "RaidAttempted");
                RequireCaller(failures, "RaidVictoryController.cs", "step 4 (raid won) + the step-5 arm", "RaidWon");
                RequireCaller(failures, "BarracksProgression.cs", "step 2 (army trained)", "ArmyTrained");
                RequireCaller(failures, "StarterArmyGrant.cs", "step 1 (barracks unlocked)", "BarracksUnlocked");
                RequireCaller(failures, "EconomyService.cs", "step 5 (raid reward spent), wallet surface", "RewardSpent");
                RequireCaller(failures, "ResourceBuildingProgression.cs", "step 5, upgrade-ledger surface", "RewardSpent");
            }
            finally
            {
                RestoreFunnelPrefs(snapshot);
            }

            if (failures.Count == 0)
            {
                reason = "RAID FUNNEL OK - the map's six steps exist as distinct raid_funnel_* names in funnel " +
                         "order, the 24h second-raid window is exclusive at the bound and refuses a missing " +
                         "stamp and a backwards clock rather than fabricating a conversion, every emission goes " +
                         "through the EXISTING EventTracker rail with no second telemetry path, and all six " +
                         "steps are wired to real call sites in shipping code";
                Debug.Log(log.ToString() + "RAID_FUNNEL_OK");
                return true;
            }

            reason = "raid-funnel: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "RAID_FUNNEL_FAIL: " + reason);
            return false;
        }


        // =====================================================================
        //  THE PLAYERPREFS FENCE, shared with StarterArmyGrantRegression.
        // =====================================================================
        // (!) TYPED READS, NOT A BLIND GetString. Unity's PlayerPrefs THROWS when a key
        // stored as an int is read as a string, and the funnel stores both kinds (six
        // int latches plus one long-as-string timestamp). A snapshot that assumed one
        // type would throw on the very state it exists to preserve - and inside a
        // Guard.Try registration that throw makes the suite VANISH from the denominator
        // rather than go red, which is the failure mode DataRegression's [suite-count]
        // check was added for. So each key is probed as an int first and falls back to a
        // string, and every read is individually guarded.

        /// <summary>One saved PlayerPrefs value, remembering which type it was stored as.</summary>
        internal struct PrefSnapshot
        {
            public bool IsInt;
            public int IntValue;
            public string StringValue;
        }

        /// <summary>Captures every RaidFunnel PlayerPrefs key that is currently set.</summary>
        internal static Dictionary<string, PrefSnapshot> SnapshotFunnelPrefs()
        {
            var snap = new Dictionary<string, PrefSnapshot>();
            foreach (var k in RaidFunnel.PrefKeys)
            {
                if (!PlayerPrefs.HasKey(k)) continue;
                try
                {
                    // GetInt throws on a string-typed key; -2147483648 is not a value the
                    // funnel can legitimately store, so it doubles as "not an int".
                    int probe = PlayerPrefs.GetInt(k, int.MinValue);
                    if (probe != int.MinValue)
                    {
                        snap[k] = new PrefSnapshot { IsInt = true, IntValue = probe };
                        continue;
                    }
                }
                catch { /* falls through to the string read below */ }

                try { snap[k] = new PrefSnapshot { IsInt = false, StringValue = PlayerPrefs.GetString(k, null) }; }
                catch { /* unreadable either way: leave it out and let Restore simply delete it */ }
            }
            return snap;
        }

        /// <summary>Clears every funnel key, then puts back exactly what was there.</summary>
        internal static void RestoreFunnelPrefs(Dictionary<string, PrefSnapshot> snap)
        {
            foreach (var k in RaidFunnel.PrefKeys)
            {
                try { PlayerPrefs.DeleteKey(k); } catch { }
            }
            if (snap != null)
            {
                foreach (var kv in snap)
                {
                    try
                    {
                        if (kv.Value.IsInt) PlayerPrefs.SetInt(kv.Key, kv.Value.IntValue);
                        else if (kv.Value.StringValue != null) PlayerPrefs.SetString(kv.Key, kv.Value.StringValue);
                    }
                    catch { }
                }
            }
            try { PlayerPrefs.Save(); } catch { }
        }

        private static void AssertWindow(List<string> failures, string label,
                                         long firstMs, long nowMs, bool expected)
        {
            bool actual = RaidFunnel.IsWithinSecondRaidWindow(firstMs, nowMs);
            if (actual != expected)
                failures.Add("[" + label + "] IsWithinSecondRaidWindow(first=" + firstMs + ", now=" + nowMs +
                             ") returned " + actual + ", expected " + expected);
        }

        /// <summary>
        /// A funnel step is only real if shipping code calls it. Checked on
        /// COMMENT-STRIPPED source, because a file that merely EXPLAINS the funnel in a
        /// header would otherwise satisfy the lint - which is exactly how a source-lint
        /// stops being able to tell code from prose (WO-1112's lesson, applied here
        /// rather than re-learned).
        /// </summary>
        private static void RequireCaller(List<string> failures, string file, string step, string method)
        {
            string code = RaidLootCurrencyRegression.ReadStripped(file);
            if (code == null)
            {
                failures.Add("[D] " + file + " not found under Assets/_Modules - cannot prove " + step +
                             " is wired");
                return;
            }
            if (!code.Contains("RaidFunnel." + method))
                failures.Add("[D] " + file + " live code never calls RaidFunnel." + method + ", so " + step +
                             " is never emitted. That step will read as ZERO in analytics forever, and the " +
                             "funnel below it will look like a cliff that does not exist.");
        }
    }
}
