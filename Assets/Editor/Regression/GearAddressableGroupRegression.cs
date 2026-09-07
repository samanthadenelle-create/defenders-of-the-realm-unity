// =============================================================================
// GearAddressableGroupRegression — WO-975: Gear.asset entries must resolve to
// real assets on disk. A tracked Addressables group whose GUIDs point at a
// gitignored pack (Assets/Blink/) is a silent hollow ship on fresh clones.
//
// HARD-fails when any serialize-entry GUID has no AssetDatabase path (dangling)
// or resolves under a path that does not exist on disk. Does NOT remove entries
// (WO-975: never "fix" by emptying the group).
//
// Menu / batch: DeNelle.Editor.GearAddressableGroupRegression.Run
// Marker: GEAR_ADDRESSABLE_GROUP_OK <n>/<n>  |  GEAR_ADDRESSABLE_GROUP_FAIL
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class GearAddressableGroupRegression
    {
        private const string MarkerOk   = "GEAR_ADDRESSABLE_GROUP_OK";
        private const string MarkerFail = "GEAR_ADDRESSABLE_GROUP_FAIL";
        private const string GearAsset  = "Assets/AddressableAssetsData/AssetGroups/Gear.asset";

        // Only serialize-entry GUIDs (indented list items), not the group's own m_GUID.
        private static readonly Regex EntryGuid =
            new Regex(@"^\s+-\s+m_GUID:\s+([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);

        // WO-1496: menu / standalone entry. The registered `Run(out string)` below carries
        // the assertions; this one only routes the verdict to the console markers.
        [MenuItem("Defenders/Regression/Gear Addressable Group (WO-975)")]
        public static void Run()
        {
            Run(out _);
        }

        /// <summary>Registered-suite entry point (DataRegression.RunAll, WO-1496).</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            int total = 0;
            int ok = 0;

            if (!File.Exists(GearAsset))
            {
                failures.Add($"Gear.asset missing at {GearAsset}");
            }
            else
            {
                string text = File.ReadAllText(GearAsset);
                var seen = new HashSet<string>();
                foreach (Match m in EntryGuid.Matches(text))
                {
                    string guid = m.Groups[1].Value.ToLowerInvariant();
                    if (!seen.Add(guid)) continue;
                    total++;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        failures.Add($"dangling GUID {guid} — AssetDatabase has no path (pack missing / entry orphan)");
                        continue;
                    }
                    // Meta-only or missing file on disk (gitignored Blink on a fresh clone).
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        failures.Add($"GUID {guid} -> '{path}' does not exist on disk (likely gitignored pack)");
                        continue;
                    }
                    ok++;
                }

                if (total == 0)
                    failures.Add("Gear.asset has ZERO serialize-entry GUIDs — group is empty or format drifted");
            }

            if (failures.Count > 0)
            {
                foreach (var f in failures)
                    Debug.LogError($"[GearAddressableGroup] {f}");
                Debug.LogError($"{MarkerFail} {ok}/{total} resolvable :: {failures.Count} defect(s) — " +
                               "WO-975: tracked Gear group asserts content that is not on disk.");
                reason = $"GEAR ADDRESSABLE GROUP: {failures.Count} defect(s) ({ok}/{total} resolvable): " +
                         string.Join(" | ", failures.ToArray());
                return false;
            }

            Debug.Log($"{MarkerOk} {ok}/{total} resolvable :: every Gear.asset entry GUID maps to an on-disk asset.");
            reason = $"GEAR ADDRESSABLE GROUP OK {ok}/{total} entry GUID(s) resolve to an on-disk asset " +
                     "(no dangling entry, no gitignored-pack hollow)";
            return true;
        }
    }
}
