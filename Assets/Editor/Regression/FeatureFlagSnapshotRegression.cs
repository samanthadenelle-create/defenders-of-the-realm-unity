// =============================================================================
// FeatureFlagSnapshotRegression [flag-snapshot] -- WO-1540.
// Pins the flag-hygiene instrument itself: a dummy "suite" sets a feature flag
// and does NOT restore it; the snapshot must (a) NAME that key as drift and
// (b) put the environment back so the NEXT suite reads the compiled default.
//
// This is the case the ticket asked for. It is deliberately written against a
// flag NO other registered suite touches (ff.petcombat, FeatureFlags.cs:1101,
// default OFF) so the pin cannot itself become the bleed it polices, and it
// restores in a finally so a throw mid-case still leaves the run clean.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor.Regression
{
    public static class FeatureFlagSnapshotRegression
    {
        // The probe flag: default OFF, read by FeatureFlags.PetCombat, set by no suite.
        private const string ProbeKey = "ff.petcombat";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            int priorProbe = PlayerPrefs.GetInt(ProbeKey, FeatureFlagSnapshot.Absent);

            try
            {
                // --- CASE A: the watch list is DERIVED from FeatureFlags.cs, not listed here.
                var keys = FeatureFlagSnapshot.KnownKeys();
                if (keys.Count == 0)
                {
                    return RegressionOutcome.Skip(out reason, "flag-snapshot",
                        "FeatureFlags.cs yielded no Get(\"<name>\") call-sites -- the key set could " +
                        "not be derived, so flag drift is not watched (fix the derivation, do not " +
                        "hardcode a key list)");
                }
                if (!keys.Contains(ProbeKey))
                    failures.Add("CASE A: the derived key set does not contain " + ProbeKey +
                                 " -- the regex no longer matches FeatureFlags.Get call-sites, so the " +
                                 "watch list is silently short (" + keys.Count + " key(s) found)");
                if (!keys.Contains("ff.barracks"))
                    failures.Add("CASE A: the derived key set does not contain ff.barracks -- the key " +
                                 "WO-1540 was raised about would not be watched");

                // --- CASE B: a suite that sets a flag really does change what the next reader sees.
                //     (If this did not hold, the whole hazard would be imaginary and the pin vacuous.)
                var snapshot = FeatureFlagSnapshot.Capture();
                if (!snapshot.ContainsKey(ProbeKey))
                    failures.Add("CASE B: snapshot did not capture " + ProbeKey);

                PlayerPrefs.SetInt(ProbeKey, 1);        // the DUMMY SUITE, leaking on purpose
                if (!FeatureFlags.PetCombat)
                    failures.Add("CASE B: PlayerPrefs " + ProbeKey + "=1 did not make FeatureFlags." +
                                 "PetCombat read true -- the pref key and the property have diverged, " +
                                 "so this oracle is testing nothing");

                // --- CASE C: the snapshot NAMES the drift instead of silently swallowing it.
                bool clean = FeatureFlagSnapshot.RestoreAndDiff(snapshot, out string diff);
                if (clean)
                    failures.Add("CASE C: RestoreAndDiff reported CLEAN after the dummy suite left " +
                                 ProbeKey + " set -- a real bleed would pass unnoticed");
                else if (diff == null || diff.IndexOf(ProbeKey, StringComparison.Ordinal) < 0)
                    failures.Add("CASE C: the drift report does not NAME " + ProbeKey +
                                 " -- an unnamed bleed is not actionable. Reported: " + diff);

                // --- CASE D: THE ACCEPTANCE. The next suite sees the compiled DEFAULT.
                if (FeatureFlags.PetCombat)
                    failures.Add("CASE D: after restore, FeatureFlags.PetCombat still reads true -- the " +
                                 "leaked flag survived into the next suite, which is the WO-1540 defect");
                int captured;
                if (!snapshot.TryGetValue(ProbeKey, out captured)) captured = priorProbe;
                if (PlayerPrefs.GetInt(ProbeKey, FeatureFlagSnapshot.Absent) != captured)
                    failures.Add("CASE D: " + ProbeKey + " was not restored to its captured raw value " +
                                 "(an absent key must be DELETED, not written as 0 -- writing 0 pins the " +
                                 "flag OFF forever instead of restoring the default)");

                // --- CASE E: a run with no drift reports clean (no false red on the happy path).
                var second = FeatureFlagSnapshot.Capture();
                if (!FeatureFlagSnapshot.RestoreAndDiff(second, out string cleanReason))
                    failures.Add("CASE E: RestoreAndDiff reported drift on an UNTOUCHED environment -- " +
                                 "false red: " + cleanReason);

                if (failures.Count > 0)
                {
                    reason = "flag-snapshot: " + failures.Count + " failure(s): " +
                             string.Join(" | ", failures);
                    return false;
                }

                reason = "flag-snapshot: " + keys.Count + " ff.* key(s) derived from FeatureFlags.cs; a " +
                         "dummy suite leaking " + ProbeKey + " is NAMED as drift and restored, and the " +
                         "next reader sees the compiled default";
                return true;
            }
            catch (Exception ex)
            {
                reason = "flag-snapshot: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                // Belt and braces: this suite must never be the bleed it polices.
                FeatureFlagSnapshot.Apply(ProbeKey, priorProbe);
                PlayerPrefs.Save();
            }
        }

        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("FLAG_SNAPSHOT_OK - " + reason);
            else Debug.LogError("FLAG_SNAPSHOT_FAIL - " + reason);
        }
    }
}
