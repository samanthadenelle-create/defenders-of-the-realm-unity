// =============================================================================
// LootTableCatalog - typed model + WebGL-safe loader for loot-tables.json.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors GearCatalog.cs exactly: canonical JSON read via DeNelle.Core.CanonicalJson
// (Resources first, StreamingAssets fallback), parsed by Newtonsoft.Json. Loot is
// CONTENT, not code - add/retune drop tables by editing the JSON, no recompile.
//
// A loot table maps an enemy/boss kind to weighted material drops. On a kill the
// drop watcher rolls EACH entry independently against its chance, and on a hit
// awards a random count in [minCount, maxCount]. Materials are deposited into the
// existing village larder (VillageInventory) and feed the consumable recipes
// (potions, tent kits, food) the Workshop already crafts.
//
// SHAPE (loot-tables.json):
//   {
//     "version": 1,
//     "tables": [
//       {
//         "id": "common-grunt",            // keyed by enemy def id or kind
//         "source": "enemy",               // "enemy" | "boss" | "dungeon" (doc only)
//         "drops": [
//           { "materialId": "monster-hide", "chance": 0.35, "minCount": 1, "maxCount": 2 }
//         ]
//       }
//     ],
//     "defaults": { "enemy": "common-grunt", "boss": "boss-hoard" }
//   }
//
// GRACEFUL: every query null-guards, so a missing/empty catalog simply yields no
// drops (existing gameplay is unchanged - drops are an additive, dark layer).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;   // WO-556: FlowTrace the boss-only gem rolls

namespace DeNelle.Village.Items
{
    /// <summary>One weighted drop line: roll <see cref="Chance"/>, on hit award a
    /// random count in [<see cref="MinCount"/>, <see cref="MaxCount"/>].</summary>
    [Serializable]
    public sealed class LootDropLine
    {
        [JsonProperty("materialId")] public string MaterialId;
        [JsonProperty("chance")] public float Chance = 1f;   // 0..1
        [JsonProperty("minCount")] public int MinCount = 1;
        [JsonProperty("maxCount")] public int MaxCount = 1;
        /// <summary>WO-556: when true this line only rolls on a BOSS kill (gem/gear gate).
        /// Ordinary kills skip it. Defaults false so every existing line is unaffected.</summary>
        [JsonProperty("bossOnly")] public bool BossOnly = false;
    }

    /// <summary>A loot table for one enemy/boss kind.</summary>
    [Serializable]
    public sealed class LootTableDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("source")] public string Source;   // "enemy" | "boss" | "dungeon" (documentation)
        [JsonProperty("drops")] public List<LootDropLine> Drops = new List<LootDropLine>();
    }

    /// <summary>Fallback table ids used when a specific enemy/boss has no own table.</summary>
    [Serializable]
    public sealed class LootDefaults
    {
        [JsonProperty("enemy")] public string Enemy;
        [JsonProperty("boss")] public string Boss;
        [JsonProperty("dungeon")] public string Dungeon;
    }

    [Serializable]
    public sealed class LootTableData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("tables")] public List<LootTableDef> Tables = new List<LootTableDef>();
        [JsonProperty("defaults")] public LootDefaults Defaults = new LootDefaults();
    }

    /// <summary>
    /// Static loader + roll surface for the loot tables. Graceful: a missing/empty
    /// catalog yields no drops. The whole feature is gated by ItemDropSystem.Enabled.
    /// </summary>
    public static class LootTableCatalog
    {
        private const string CanonicalRelativePath = "Data/Canonical/loot-tables.json";
        private static LootTableData _data;

        /// <summary>Forces a re-read of the catalog.</summary>
        public static void Reload() { _data = null; EnsureLoaded(); }

        /// <summary>All loaded tables (never null after load).</summary>
        public static IReadOnlyList<LootTableDef> All { get { EnsureLoaded(); return _data.Tables; } }

        /// <summary>Default enemy table id (fallback when an enemy has no own table).</summary>
        public static string DefaultEnemyTableId { get { EnsureLoaded(); return _data.Defaults?.Enemy; } }

        /// <summary>Default boss table id (fallback when a boss has no own table).</summary>
        public static string DefaultBossTableId { get { EnsureLoaded(); return _data.Defaults?.Boss; } }

        /// <summary>Find a table by id, or null.</summary>
        public static LootTableDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var t in _data.Tables)
                if (t != null && t.Id == id) return t;
            return null;
        }

        /// <summary>
        /// Roll the table with <paramref name="tableId"/> for an ORDINARY kill: each drop line is
        /// rolled independently against its chance, and on a hit a random count is awarded.
        /// <see cref="LootDropLine.BossOnly"/> lines are SKIPPED. Back-compat default.
        /// Returns a materialId -> count map (empty when nothing dropped / no table).
        /// </summary>
        public static Dictionary<string, int> Roll(string tableId) => Roll(tableId, includeBossOnly: false);

        /// <summary>
        /// WO-556: roll the table, optionally INCLUDING <see cref="LootDropLine.BossOnly"/> lines.
        /// Pass <paramref name="includeBossOnly"/> = true on a BOSS kill so the gated gem/gear lines
        /// can drop; false (ordinary kill) skips them. Each line is rolled independently.
        /// </summary>
        public static Dictionary<string, int> Roll(string tableId, bool includeBossOnly)
        {
            var result = new Dictionary<string, int>();
            var table = Find(tableId);
            if (table == null || table.Drops == null) return result;

            foreach (var line in table.Drops)
            {
                if (line == null || string.IsNullOrEmpty(line.MaterialId)) continue;
                if (line.BossOnly && !includeBossOnly) continue;   // gem/gear gate: boss kills only
                float chance = Mathf.Clamp01(line.Chance);
                if (chance <= 0f) continue;
                if (UnityEngine.Random.value > chance) continue;

                int lo = Mathf.Max(0, line.MinCount);
                int hi = Mathf.Max(lo, line.MaxCount);
                int count = (hi <= lo) ? lo : UnityEngine.Random.Range(lo, hi + 1);
                if (count <= 0) continue;

                if (result.ContainsKey(line.MaterialId)) result[line.MaterialId] += count;
                else result[line.MaterialId] = count;

                // WO-556: prove the boss-gated gem/gear drop fired (only reachable on a boss kill).
                if (line.BossOnly)
                    FlowTrace.Step("Loot", $"LOOT bossOnly drop '{line.MaterialId}' x{count} from table '{tableId}'.");
            }
            return result;
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CanonicalRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<LootTableData>(json);
                    if (parsed != null) { _data = parsed; if (_data.Tables == null) _data.Tables = new List<LootTableDef>(); return; }
                }
                Debug.LogWarning("[LootTableCatalog] loot-tables.json not found (Resources or StreamingAssets) - drops disabled.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[LootTableCatalog] Read failed: " + ex.Message);
            }
            _data = new LootTableData();
        }
    }
}
