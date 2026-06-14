// =============================================================================
// BuildingTierCatalog — typed model + loader for building-tiers.json (WO-430).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The city-upgrade ladder is CONTENT, not code (owner: "keep costs/bonuses in
// data"). Mirrors the TroopCatalog/PetCatalog strategy: reads
// Data/Canonical/building-tiers.json via DeNelle.Core.CanonicalJson (Resources
// first — WebGL-safe — then StreamingAssets) and hydrates typed defs. Each tier
// carries its COST (Wood/Food/Crystal) + the CUMULATIVE GameModifiers it grants
// at that tier (the contract is reused: a tier's bonus IS a GameModifiers).
//
// ModifierService compiles GameState.BuildingTiers through this into the active
// GameModifiers; the TryUpgradeBuilding command reads the cost from here.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.State
{
    /// <summary>One upgrade tier of one building: cost + the cumulative modifiers at that tier.</summary>
    [Serializable]
    public sealed class BuildingTierDef
    {
        [JsonProperty("tier")] public int Tier;
        [JsonProperty("name")] public string Name;
        [JsonProperty("costWood")] public int CostWood;
        [JsonProperty("costFood")] public int CostFood;
        [JsonProperty("costCrystal")] public int CostCrystal;
        /// <summary>Cumulative perk contribution at this tier (unset fields = no-op 1.0/false).</summary>
        [JsonProperty("modifiers")] public GameModifiers Modifiers = new GameModifiers();
    }

    /// <summary>One upgradable building and its tier ladder.</summary>
    [Serializable]
    public sealed class BuildingUpgradeDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("tiers")] public List<BuildingTierDef> Tiers = new List<BuildingTierDef>();
    }

    /// <summary>The parsed building-tiers.json root.</summary>
    [Serializable]
    public sealed class BuildingTierCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("buildings")] public List<BuildingUpgradeDef> Buildings = new List<BuildingUpgradeDef>();
    }

    /// <summary>Static surface over building-tiers.json — load + cache + lookup.</summary>
    public static class BuildingTierCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/building-tiers.json";
        private static BuildingTierCatalogData _data;

        /// <summary>All upgradable building defs, in catalog order.</summary>
        public static IReadOnlyList<BuildingUpgradeDef> All { get { EnsureLoaded(); return _data.Buildings; } }

        /// <summary>The building def for an id, or null. (Building ids: arcane-tower, armorer, forge, lumbermill, windmill.)</summary>
        public static BuildingUpgradeDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var b in _data.Buildings)
                if (b != null && b.Id == id) return b;
            return null;
        }

        /// <summary>The tier def for (id, tier) — null if the building/tier is unknown.</summary>
        public static BuildingTierDef TierOf(string id, int tier)
        {
            var b = Find(id);
            if (b == null || b.Tiers == null) return null;
            foreach (var t in b.Tiers)
                if (t != null && t.Tier == tier) return t;
            return null;
        }

        /// <summary>Highest authored tier for a building (0 if unknown).</summary>
        public static int MaxTier(string id)
        {
            var b = Find(id);
            if (b == null || b.Tiers == null) return 0;
            int max = 0;
            foreach (var t in b.Tiers) if (t != null && t.Tier > max) max = t.Tier;
            return max;
        }

        /// <summary>True if this id is an upgradable building (in the catalog).</summary>
        public static bool IsUpgradable(string id) => Find(id) != null;

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
        }

        private static BuildingTierCatalogData LoadCatalog()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<BuildingTierCatalogData>(json);
                    if (parsed != null && parsed.Buildings != null && parsed.Buildings.Count > 0)
                        return parsed;
                    Debug.LogError("[BuildingTierCatalog] building-tiers.json parsed empty.");
                }
                else Debug.LogError("[BuildingTierCatalog] building-tiers.json not found (Resources or StreamingAssets).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingTierCatalog] Failed to read building-tiers.json: {ex.Message}");
            }
            return new BuildingTierCatalogData { Buildings = new List<BuildingUpgradeDef>() };
        }
    }
}
