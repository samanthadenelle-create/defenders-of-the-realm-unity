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
    /// <summary>
    /// WO-432 — one Gold-cost RESEARCH perk unlocked at a building tier (the WC3 "research at the
    /// Blacksmith" pillar). Numerical upgrades (damage/armor Lvl 1/2/3) and creative-owned ability
    /// unlocks both ride this contract — a perk's effect IS a <see cref="GameModifiers"/>, compiled by
    /// ModifierService exactly like a tier's. Bought with Gold (economy Coins); owned set persists in
    /// GameState.OwnedBuildingPerks keyed "buildingId:perkId".
    /// </summary>
    [Serializable]
    public sealed class BuildingPerkDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        /// <summary>Gold (economy Coins) cost to research this perk.</summary>
        [JsonProperty("goldCost")] public int GoldCost;
        /// <summary>Sprite id under Resources/HudItems/BuildingUpgrades/&lt;iconId&gt;; defaults to <see cref="Id"/>.</summary>
        [JsonProperty("iconId")] public string IconId;
        /// <summary>The Tier-3 capstone signature (gilt-highlighted in the panel). Creative-owned design.</summary>
        [JsonProperty("isSignature")] public bool IsSignature;
        /// <summary>This perk's contribution to the active modifier set (compiled like a tier's).</summary>
        [JsonProperty("modifiers")] public GameModifiers Modifiers = new GameModifiers();
    }

    /// <summary>One upgrade tier of one building: cost + the cumulative modifiers at that tier.</summary>
    [Serializable]
    public sealed class BuildingTierDef
    {
        [JsonProperty("tier")] public int Tier;
        [JsonProperty("name")] public string Name;
        [JsonProperty("costWood")] public int CostWood;
        [JsonProperty("costFood")] public int CostFood;
        [JsonProperty("costCrystal")] public int CostCrystal;
        /// <summary>WO-432 tech-gate — this tier (and its research) is locked until the global Village/
        /// Stronghold Tier (Heart of Elarion) reaches this value. 0 = no gate (always available).</summary>
        [JsonProperty("requiresVillageTier")] public int RequiresVillageTier;
        /// <summary>Cumulative perk contribution at this tier (unset fields = no-op 1.0/false).</summary>
        [JsonProperty("modifiers")] public GameModifiers Modifiers = new GameModifiers();
        /// <summary>WO-432 — Gold-cost research perks UNLOCKED at this tier (buyable once the building
        /// reaches this tier). Never null.</summary>
        [JsonProperty("perks")] public System.Collections.Generic.List<BuildingPerkDef> Perks
            = new System.Collections.Generic.List<BuildingPerkDef>();
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

        /// <summary>WO-432 — the research-perk def for (building, perk), or null. Searches every tier's perks.</summary>
        public static BuildingPerkDef FindPerk(string buildingId, string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return null;
            var b = Find(buildingId);
            if (b == null || b.Tiers == null) return null;
            foreach (var t in b.Tiers)
            {
                if (t == null || t.Perks == null) continue;
                foreach (var p in t.Perks)
                    if (p != null && p.Id == perkId) return p;
            }
            return null;
        }

        /// <summary>WO-432 — the building tier at which a perk unlocks (its owning Tiers[] entry), or
        /// int.MaxValue if the perk is unknown. The perk's research gate (building tier + village tier).</summary>
        public static int PerkUnlockTier(string buildingId, string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return int.MaxValue;
            var b = Find(buildingId);
            if (b == null || b.Tiers == null) return int.MaxValue;
            foreach (var t in b.Tiers)
            {
                if (t == null || t.Perks == null) continue;
                foreach (var p in t.Perks)
                    if (p != null && p.Id == perkId) return t.Tier;
            }
            return int.MaxValue;
        }

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
