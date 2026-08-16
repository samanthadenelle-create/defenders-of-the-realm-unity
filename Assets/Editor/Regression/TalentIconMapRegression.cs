// =============================================================================
// TalentIconMapRegression [talent-icons] -- pins the talent icon map's integrity
// (WO-1023). Until 2026-08-15 NOTHING guarded talent-icon-map.json: every property
// (83/83 coverage, unique art, resolvable paths, two byte-identical copies) was true
// by care alone -- the exact WO-996 armor.json shape, where Resources wins at runtime
// so the Editor looks fine while both copies drift.
// -----------------------------------------------------------------------------
// Five assertions (WO-1023 section 2):
//   1. every node id in hero-talents.json (all trees + the Shared pool, hidden
//      included) has exactly ONE map entry -- no unmapped node, no orphan entry
//   2. no duplicate blinkSource across entries (two talents rendering the same
//      picture is a recognition failure; hue can't rescue it -- the owner is
//      red/green colourblind, so silhouette identity carries the load)
//   3. every map iconPath loads through Resources.Load<Sprite> -- the SAME call
//      HeroSkillTreePanelMvvm.LoadIcon makes at runtime, so this stays honest in
//      a player build (an on-disk-only check would pass an unimported texture)
//   4. every map iconPath equals the matching hero-talents.json node's iconPath
//      (the map is the provenance record for the art the node actually shows)
//   5. the Resources and StreamingAssets copies of the map are byte-identical
//
// The catalog side loads through the REAL HeroTalentCatalog (same loader the game
// uses); the map side parses the Resources JSON file with Newtonsoft.
//
// Marker: TALENT_ICON_MAP_OK / TALENT_ICON_MAP_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!DeNelle.Editor.Regression.TalentIconMapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[talent-icons] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Village.Talents;

namespace DeNelle.Editor.Regression
{
    public static class TalentIconMapRegression
    {
        private const string ResourcesMapPath = "Assets/Resources/Data/Canonical/talent-icon-map.json";
        private const string StreamingMapPath = "Assets/StreamingAssets/Data/Canonical/talent-icon-map.json";

        [Serializable]
        private sealed class MapEntry
        {
            [JsonProperty("id")] public string Id;
            [JsonProperty("name")] public string Name;
            [JsonProperty("iconPath")] public string IconPath;
            [JsonProperty("blinkSource")] public string BlinkSource;
            [JsonProperty("why")] public string Why;
        }

        [Serializable]
        private sealed class MapFile
        {
            [JsonProperty("skills")] public List<MapEntry> Skills = new List<MapEntry>();
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TALENT ICON MAP (coverage / unique art / resolvable / in-sync / twin copies) ---");

            // ── load the map (Resources copy = the runtime winner) ────────────────
            MapFile map = null;
            if (!File.Exists(ResourcesMapPath))
                failures.Add($"[talent-icons] map missing at {ResourcesMapPath}");
            else
            {
                try { map = JsonConvert.DeserializeObject<MapFile>(File.ReadAllText(ResourcesMapPath)); }
                catch (Exception ex) { failures.Add($"[talent-icons] map parse threw: {ex.GetType().Name}: {ex.Message}"); }
            }
            if (map == null || map.Skills == null || map.Skills.Count == 0)
            {
                if (failures.Count == 0) failures.Add("[talent-icons] map deserialized to 0 entries (mapping break or empty 'skills' array)");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            // ── load every node through the REAL catalog (all trees + Shared pool) ─
            var nodes = new Dictionary<string, HeroTalentNodeDef>();
            foreach (var tree in HeroTalentCatalog.AllTrees)
                foreach (var n in tree.Nodes)
                    if (n != null && !string.IsNullOrEmpty(n.Id)) nodes[n.Id] = n;
            foreach (var s in HeroTalentCatalog.SharedNodes)
                if (s != null && !string.IsNullOrEmpty(s.Id)) nodes[s.Id] = s;
            log.AppendLine($"hero-talents.json -> {nodes.Count} node(s); talent-icon-map.json -> {map.Skills.Count} entrie(s)");
            if (nodes.Count == 0)
                failures.Add("[talent-icons] HeroTalentCatalog yielded 0 nodes (catalog mapping break) -- coverage cannot be judged");

            // ── 1: exact 1:1 coverage (no unmapped node, no orphan, no dup entry) ──
            var mapIds = new Dictionary<string, MapEntry>();
            foreach (var e in map.Skills)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) { failures.Add("[talent-icons] map entry with null/empty id"); continue; }
                if (mapIds.ContainsKey(e.Id)) failures.Add($"[talent-icons] duplicate map entry for '{e.Id}'");
                else mapIds[e.Id] = e;
            }
            foreach (var id in nodes.Keys)
                if (!mapIds.ContainsKey(id))
                    failures.Add($"[talent-icons] UNMAPPED node '{id}' -- every talent must have a map entry (its art provenance)");
            foreach (var id in mapIds.Keys)
                if (nodes.Count > 0 && !nodes.ContainsKey(id))
                    failures.Add($"[talent-icons] ORPHAN map entry '{id}' -- no such node in hero-talents.json");

            // ── 2: no two talents share the same source art ────────────────────────
            var bySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in map.Skills)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                if (string.IsNullOrEmpty(e.BlinkSource)) { failures.Add($"[talent-icons] '{e.Id}' has empty blinkSource"); continue; }
                if (bySource.TryGetValue(e.BlinkSource, out var prev))
                    failures.Add($"[talent-icons] DUPLICATE ART: '{e.Id}' and '{prev}' both use '{e.BlinkSource}' -- two talents rendering the identical icon is a recognition failure (WO-1023 section 1)");
                else bySource[e.BlinkSource] = e.Id;
            }

            // ── 3 + 4: iconPath resolves at runtime AND matches the node's ─────────
            int resolved = 0;
            foreach (var e in map.Skills)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                if (string.IsNullOrEmpty(e.IconPath)) { failures.Add($"[talent-icons] '{e.Id}' has empty iconPath"); continue; }

                // the SAME load the runtime panel makes (HeroSkillTreePanelMvvm.LoadIcon)
                var sprite = Resources.Load<Sprite>(e.IconPath);
                if (sprite == null)
                    failures.Add($"[talent-icons] '{e.Id}' iconPath '{e.IconPath}' does not Resources.Load<Sprite> -- the node would render iconless in a player build");
                else resolved++;

                if (nodes.TryGetValue(e.Id, out var node) && !string.Equals(node.IconPath ?? "", e.IconPath, StringComparison.Ordinal))
                    failures.Add($"[talent-icons] '{e.Id}' iconPath DRIFT: map says '{e.IconPath}' but hero-talents.json says '{node.IconPath}' -- the map must record the art the node actually shows");
            }
            log.AppendLine($"iconPath Resources.Load<Sprite> resolved: {resolved}/{map.Skills.Count}");

            // ── 5: the two canonical copies are byte-identical (the WO-996 lesson) ─
            if (!File.Exists(StreamingMapPath))
                failures.Add($"[talent-icons] StreamingAssets copy missing at {StreamingMapPath}");
            else
            {
                var res = File.ReadAllBytes(ResourcesMapPath);
                var sa = File.ReadAllBytes(StreamingMapPath);
                bool same = res.Length == sa.Length;
                if (same) for (int i = 0; i < res.Length; i++) { if (res[i] != sa[i]) { same = false; break; } }
                if (!same)
                    failures.Add($"[talent-icons] Resources ({res.Length} B) and StreamingAssets ({sa.Length} B) copies of talent-icon-map.json DIFFER -- Resources wins at runtime, so the drift is invisible in the Editor (the WO-996 armor.json shape)");
                else log.AppendLine($"Resources/StreamingAssets copies byte-identical ({res.Length} B)");
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "TALENT_ICON_MAP_OK");
                return "TALENT ICON MAP OK -- full 1:1 node coverage, unique art per talent, every iconPath sprite-loads, map==catalog, twin copies identical";
            }
            string reason = "talent-icons: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TALENT_ICON_MAP_FAIL: " + reason);
            return reason;
        }
    }
}
