// =============================================================================
// FeatureFlagSnapshot -- WO-1540. Flag-hygiene instrument for the regression run.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS. FeatureFlags are read-only properties backed by PlayerPrefs
// ("ff.<name>": 0=off, 1=on, absent=the compiled default -- FeatureFlags.Get,
// Assets/_Modules/Core/FeatureFlags.cs:1373-1379). In batchmode PlayerPrefs is
// PERSISTENT process-external state (the Windows registry on this machine), so a
// suite that sets a flag and does not restore it changes the environment for
// every LATER suite in the same run AND for the owner's next editor session.
// That makes results depend on run ORDER, which is silent: a later suite either
// passes wrong or fails for a reason that is nowhere in its own file.
//
// WHAT IT DOES AND DOES NOT DO (state it plainly, WO-1540 sec.2):
//   DOES  -- capture every "ff.*" key's raw pref value, later restore each key to
//            exactly what it was (DeleteKey when it was absent), and NAME every
//            key that drifted so a bleed is a RED line instead of a mystery.
//   DOES NOT -- isolate suite N from suite N-1. DataRegression.RunAll between its
//            two fences is ~200 FLAT `if (!X.Run(out var r))` lines, not a loop;
//            per-suite wrapping means touching every registration line and is the
//            lead's call, not this lane's. Fence-level capture/restore is the
//            durable half that fits without reordering a single registration.
//
// KEY ENUMERATION IS DERIVED, NEVER LISTED. PlayerPrefs has no key-listing API.
// A hardcoded flag array here would be duplicated state and would rot exactly
// like every copy CLAUDE.md sec.2/sec.5/sec.16 documents -- and a flag added
// tomorrow would simply not be watched. So the key set is regexed out of
// FeatureFlags.cs itself (the source-lint idiom this folder already uses), which
// means the watch list is the authority by construction.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class FeatureFlagSnapshot
    {
        /// <summary>Sentinel for "this key had no PlayerPrefs entry at capture time"
        /// (which is what makes FeatureFlags.Get return its compiled default).</summary>
        public const int Absent = int.MinValue;

        /// <summary>The one authority the key list is derived from.</summary>
        public const string FlagsSourceRelative = "_Modules/Core/FeatureFlags.cs";

        private static List<string> _cachedKeys;

        /// <summary>
        /// Every "ff.&lt;name&gt;" PlayerPrefs key FeatureFlags.cs actually reads, derived
        /// from the source's own Get("&lt;name&gt;", ...) call-sites. Empty list (never null)
        /// if the source cannot be read -- the caller reports that as a stand-down, not a pass.
        /// </summary>
        public static List<string> KnownKeys()
        {
            if (_cachedKeys != null) return _cachedKeys;

            var keys = new List<string>();
            try
            {
                string path = Path.Combine(Application.dataPath, FlagsSourceRelative);
                if (File.Exists(path))
                {
                    string src = File.ReadAllText(path);
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Match m in Regex.Matches(src, "Get\\(\\s*\"([A-Za-z0-9_.\\-]+)\""))
                    {
                        string name = m.Groups[1].Value;
                        if (name.Length == 0) continue;
                        if (seen.Add(name)) keys.Add("ff." + name);
                    }
                }
            }
            catch (Exception)
            {
                // Deliberately swallowed to an EMPTY list: the callers below treat an empty
                // key set as "could not watch anything" and say so out loud, which is the
                // honest outcome. A throw here would take the whole gate down over a lint.
                keys.Clear();
            }

            _cachedKeys = keys;
            return _cachedKeys;
        }

        /// <summary>Raw PlayerPrefs value for every known flag key, or Absent when unset.</summary>
        public static Dictionary<string, int> Capture()
        {
            var snap = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in KnownKeys())
                snap[key] = PlayerPrefs.GetInt(key, Absent);
            return snap;
        }

        /// <summary>
        /// Restore every captured key to exactly its captured value and report the drift.
        /// Returns TRUE when nothing drifted. On FALSE, <paramref name="reason"/> names each
        /// drifted key with before -&gt; after, so the bleed is actionable without a rerun.
        /// Restoration happens either way -- the diff is the report, not the remedy.
        /// </summary>
        public static bool RestoreAndDiff(Dictionary<string, int> before, out string reason)
        {
            if (before == null || before.Count == 0)
            {
                reason = "flag-hygiene: no snapshot to restore (FeatureFlags.cs unreadable or no " +
                         "Get(\"<name>\") call-sites found) -- flag drift was NOT watched this run";
                return false;
            }

            var drifted = new List<string>();
            foreach (var pair in before)
            {
                int now = PlayerPrefs.GetInt(pair.Key, Absent);
                if (now != pair.Value)
                    drifted.Add(pair.Key + " " + Describe(pair.Value) + " -> " + Describe(now));
                Apply(pair.Key, pair.Value);
            }
            PlayerPrefs.Save();

            if (drifted.Count > 0)
            {
                reason = "FLAG BLEED: " + drifted.Count + " feature flag(s) were left changed by a " +
                         "registered suite -- every later suite ran in an environment nobody authored, " +
                         "and results depend on run order: " + string.Join(" | ", drifted) +
                         ". The setter must restore what it set (snapshot the pref, DeleteKey when it " +
                         "was absent) -- see CheckEnemyStructureSweep's finally block for the shape. " +
                         "The values have been restored by FeatureFlagSnapshot so the rest of this run " +
                         "and the next editor session are clean.";
                return false;
            }

            reason = "flag-hygiene: " + before.Count + " ff.* key(s) watched, none drifted across the " +
                     "registered-suite fence";
            return true;
        }

        /// <summary>Write one key back to a captured raw value (Absent means delete).</summary>
        public static void Apply(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (value == Absent) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, value);
        }

        private static string Describe(int raw)
        {
            if (raw == Absent) return "<unset:default>";
            return raw.ToString();
        }
    }
}
