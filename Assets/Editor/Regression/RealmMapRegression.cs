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
//   (e) WO-941 NODE FOOTPRINT: every authored node pair that shares a horizontal
//       corridor must clear RealmMapPanel.RequiredPitchPx on the panel's declared
//       CONTRACT plate. The 2026-08-09 capture had the Starfall disc sitting on the
//       "ELARION" title by 18.3 ref px because the plate resolved 84 px shorter than
//       the fixed footprints need -- a purely arithmetic failure that only a PNG was
//       catching. Now it fails at gate speed on any mapPoint / region / footprint edit.
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
using DeNelle.Village.Hero;  // RealmMapPanel -- the published node-footprint budget (WO-941)

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

            // -- (e) WO-941: NODE FOOTPRINT vs AUTHORED PITCH -------------------
            // THE DEFECT THIS PINS (UICap-GEO, 2026-08-09, both landscape sizes):
            //   'Nodes/Node_starfall-reach/Disc' covers 'Nodes/Node_avalon/Label' ("ELARION")
            //   by 96 x 18.3 ref px.
            // A node's footprint is FIXED PIXELS (disc + a 34px title band hung under it); the
            // PITCH between two nodes is a FRACTION of the map plate. They only stay disjoint
            // while the plate is tall enough, so the plate size is a CONTRACT, not an accident --
            // RealmMapPanel.LandscapeMinPlateWidth/HeightPx declare it and WO-941's content-host
            // reclaim is what makes the shipped layout meet it.
            // This check re-derives every authored pair against that contract, so ADDING A REGION,
            // MOVING A mapPoint, or GROWING A DISC/TITLE BAND fails here -- at gate speed, from
            // the data -- instead of surfacing as one more overlap line in a capture nobody opened.
            // It is arithmetic on the authored numbers; the capture oracle remains the proof that
            // the real layout meets the contract.
            {
                float plateW = RealmMapPanel.LandscapeMinPlateWidthPx;
                float plateH = RealmMapPanel.LandscapeMinPlateHeightPx;

                var nodeIds = new List<string>();
                var nodeX = new List<float>();
                var nodeY = new List<float>();
                var nodeHome = new List<bool>();

                if (home != null && home.MapPoint != null)
                {
                    nodeIds.Add(home.Title ?? "homeBase");
                    nodeX.Add(home.MapPoint.X); nodeY.Add(home.MapPoint.Y); nodeHome.Add(true);
                }
                foreach (var r in regions)
                {
                    if (r == null || r.MapPoint == null) continue;
                    nodeIds.Add(r.Id ?? "<no id>");
                    nodeX.Add(r.MapPoint.X); nodeY.Add(r.MapPoint.Y); nodeHome.Add(false);
                }

                int corridorPairs = 0;
                for (int i = 0; i < nodeIds.Count; i++)
                {
                    for (int j = 0; j < nodeIds.Count; j++)
                    {
                        if (i == j) continue;
                        // mapPoint y is authored DOWNWARD (the React realm-map-layout convention
                        // the View mirrors), so a LARGER y is LOWER on the plate. Only a node
                        // BELOW another can have its disc land on that node's title band.
                        if (nodeY[j] <= nodeY[i]) continue;

                        float dxPx = Mathf.Abs(nodeX[i] - nodeX[j]) * 0.01f * plateW;
                        if (dxPx >= RealmMapPanel.RequiredCorridorPx(nodeHome[j])) continue;   // corridors disjoint

                        corridorPairs++;
                        float pitchPx = (nodeY[j] - nodeY[i]) * 0.01f * plateH;
                        float needPx = RealmMapPanel.RequiredPitchPx(nodeHome[i], nodeHome[j]);
                        if (pitchPx + 0.01f < needPx)
                            failures.Add($"[node-footprint] '{nodeIds[j]}' disc would cover '{nodeIds[i]}' title band " +
                                         $"on the contract plate ({plateW}x{plateH} ref px): pitch {pitchPx:F1} px < " +
                                         $"required {needPx:F1} px (disc halves + {RealmMapPanel.NodeLabelGapPx} gap + " +
                                         $"{RealmMapPanel.NodeLabelBandPx} title band). Move the mapPoint, shrink the " +
                                         "footprint, or give the map plate more height -- do NOT raise the contract " +
                                         "to make this pass.");
                    }
                }
                log.AppendLine($"node footprint: {nodeIds.Count} node(s), {corridorPairs} corridor-sharing pair(s) " +
                               $"checked on the {plateW}x{plateH} contract plate");
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
