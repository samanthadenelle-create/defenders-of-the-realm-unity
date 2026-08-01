// =============================================================================
// RealmMapRegression [realm-map] -- WO-826 dual-copy + loader oracle.
// -----------------------------------------------------------------------------
// THE GAP: realm-map.json is DUAL-COPY (Resources wins at runtime, the
// StreamingAssets file is the desktop fallback + source). A divergence -- an
// edit landing in only one copy -- ships a map whose editor run and player run
// disagree, silently. And the file feeds the new RealmMapCatalog typed loader:
// a renamed key parses to an EMPTY catalog with no compile error.
//
// Checks:
//   (a) BOTH copies exist and parse as JSON.
//   (b) FIELD PARITY across the copies (the WO-826 spec's oracle): the
//       regions[].id set, each region's mapPoint {x,y}, each region's gate
//       object (deep-equal), and the homeBase mapPoint all match. (Byte parity
//       is asserted too as the stronger signal, reported as a failure only when
//       the FIELD parity also breaks -- comments may legitimately drift is NOT
//       our policy: byte divergence is itself a failure per the CanonicalJson
//       law, so both are hard checks.)
//   (c) LOADER oracle through the REAL RealmMapCatalog (CanonicalJson path):
//       home present and titled "Elarion", 5 regions, every region carries an
//       id + title + mapPoint + gate of a known kind, regionCleared gates
//       reference real region ids.
//   (d) CANON law: no player-facing title/epithet/description says "Avalon"
//       (the data ID "avalon" is wire-compat with the React save and allowed).
//
// Wire (DataRegression.RunAll):
//   Guard.Try("Regression", "realm-map suite", () => { if (!RealmMapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[realm-map] " + r); });
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core;          // CanonicalJson -- the runtime loader's Resources-first read
using DeNelle.Core.World;    // RealmMapCatalog / RealmRegionGate -- the real typed loader

namespace DeNelle.Editor
{
    public static class RealmMapRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- REALM MAP (WO-826: dual-copy field parity + typed-loader oracle) ---");

            // -- (a) both copies exist + parse ---------------------------------
            string resPath = Path.Combine(Application.dataPath, "Resources/Data/Canonical/realm-map.json");
            string samPath = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/realm-map.json");

            JObject resJo = ParseCopy("Resources", resPath, failures);
            JObject samJo = ParseCopy("StreamingAssets", samPath, failures);

            // Byte parity is the CanonicalJson law (copies are kept byte-identical).
            if (File.Exists(resPath) && File.Exists(samPath) &&
                File.ReadAllText(resPath) != File.ReadAllText(samPath))
                failures.Add("realm-map.json dual copies diverged byte-wise (Resources vs StreamingAssets -- CanonicalJson law)");

            // -- (b) field parity: regions[].id + mapPoint + gates + home mapPoint --
            if (resJo != null && samJo != null)
            {
                var resRegions = IndexRegions(resJo, "Resources", failures);
                var samRegions = IndexRegions(samJo, "StreamingAssets", failures);

                if (resRegions != null && samRegions != null)
                {
                    foreach (var id in resRegions.Keys)
                        if (!samRegions.ContainsKey(id))
                            failures.Add($"region '{id}' present in Resources copy but missing from StreamingAssets copy");
                    foreach (var id in samRegions.Keys)
                        if (!resRegions.ContainsKey(id))
                            failures.Add($"region '{id}' present in StreamingAssets copy but missing from Resources copy");

                    foreach (var id in resRegions.Keys)
                    {
                        if (!samRegions.TryGetValue(id, out var other)) continue;
                        var mine = resRegions[id];
                        if (!JToken.DeepEquals(mine["mapPoint"], other["mapPoint"]))
                            failures.Add($"region '{id}' mapPoint differs between the dual copies");
                        if (!JToken.DeepEquals(mine["gate"], other["gate"]))
                            failures.Add($"region '{id}' gate differs between the dual copies");
                    }
                    log.AppendLine($"field parity checked over {resRegions.Count} region id(s)");
                }

                if (!JToken.DeepEquals(resJo["homeBase"]?["mapPoint"], samJo["homeBase"]?["mapPoint"]))
                    failures.Add("homeBase.mapPoint differs between the dual copies");
            }

            // -- (c) typed-loader oracle (the REAL runtime path) ----------------
            RealmMapCatalog.Reload();
            var home = RealmMapCatalog.Home;
            var regions = RealmMapCatalog.Regions;

            if (home == null)
                failures.Add("RealmMapCatalog.Home deserialized null (homeBase key/mapping break)");
            else if (home.Title != "Elarion")
                failures.Add($"homeBase.title is '{home.Title}' -- canon home base title must be 'Elarion'");

            log.AppendLine($"realm-map.json -> {regions.Count} RealmRegionDef objects, home='{(home != null ? home.Title : "<null>")}'");
            if (regions.Count != 5)
                failures.Add($"realm-map.json deserialized to {regions.Count} regions -- the authored catalog carries 5");

            var knownIds = new HashSet<string>();
            foreach (var r in regions)
                if (r != null && !string.IsNullOrEmpty(r.Id)) knownIds.Add(r.Id);

            foreach (var r in regions)
            {
                if (r == null) { failures.Add("null region entry in RealmMapCatalog.Regions"); continue; }
                if (string.IsNullOrEmpty(r.Id)) failures.Add("region with null/empty id (blank map node)");
                if (string.IsNullOrEmpty(r.Title)) failures.Add($"region '{r.Id}' has null/empty title (blank detail header)");
                if (r.MapPoint == null) failures.Add($"region '{r.Id}' has no mapPoint (node cannot be placed)");
                if (r.Gate == null)
                {
                    failures.Add($"region '{r.Id}' has no gate (locked-state derivation breaks)");
                }
                else if (r.Gate.Kind == RealmRegionGate.KindRegionCleared)
                {
                    if (string.IsNullOrEmpty(r.Gate.RegionId) || !knownIds.Contains(r.Gate.RegionId))
                        failures.Add($"region '{r.Id}' regionCleared gate references unknown region '{r.Gate.RegionId}'");
                }
                else if (r.Gate.Kind != RealmRegionGate.KindBestWave)
                {
                    failures.Add($"region '{r.Id}' gate kind '{r.Gate.Kind}' is not a known union member (bestWave|regionCleared)");
                }
                else
                {
                    log.AppendLine($"  R {r.Id} | '{r.Title}' | gate bestWave>={r.Gate.Value} | mapPoint ({r.MapPoint?.X},{r.MapPoint?.Y})");
                }
            }

            // -- (d) canon: never "Avalon" in player-facing strings -------------
            if (home != null)
            {
                if (ContainsAvalon(home.Title)) failures.Add("homeBase.title contains 'Avalon' (canon: Elarion)");
                if (ContainsAvalon(home.Epithet)) failures.Add("homeBase.epithet contains 'Avalon' (canon: Elarion)");
                if (ContainsAvalon(home.Description)) failures.Add("homeBase.description contains 'Avalon' (canon: Elarion)");
            }
            foreach (var r in regions)
            {
                if (r == null) continue;
                if (ContainsAvalon(r.Title)) failures.Add($"region '{r.Id}' title contains 'Avalon' (canon: Elarion)");
                if (ContainsAvalon(r.Description)) failures.Add($"region '{r.Id}' description contains 'Avalon' (canon: Elarion)");
            }

            // -- verdict --------------------------------------------------------
            if (failures.Count == 0)
            {
                reason = $"REALM_MAP_OK ({regions.Count} regions, home Elarion, dual copies in parity)";
                Debug.Log(log.ToString());
                return true;
            }
            reason = "REALM_MAP_FAIL: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "\n" + reason);
            return false;
        }

        private static JObject ParseCopy(string label, string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"realm-map.json {label} copy missing at '{path}'");
                return null;
            }
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (System.Exception e)
            {
                failures.Add($"realm-map.json {label} copy failed to parse: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        private static Dictionary<string, JObject> IndexRegions(JObject root, string label, List<string> failures)
        {
            var arr = root["regions"] as JArray;
            if (arr == null)
            {
                failures.Add($"realm-map.json {label} copy has no top-level 'regions' array");
                return null;
            }
            var map = new Dictionary<string, JObject>();
            foreach (var t in arr)
            {
                var o = t as JObject;
                string id = o?["id"]?.ToString();
                if (o == null || string.IsNullOrEmpty(id))
                {
                    failures.Add($"realm-map.json {label} copy carries a region entry with no id");
                    continue;
                }
                map[id] = o;
            }
            return map;
        }

        private static bool ContainsAvalon(string s) =>
            !string.IsNullOrEmpty(s) && s.IndexOf("Avalon", System.StringComparison.Ordinal) >= 0;
    }
}
