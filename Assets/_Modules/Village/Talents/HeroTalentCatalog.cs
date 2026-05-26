// =============================================================================
// HeroTalentCatalog - loads hero-talents.json (StreamingAssets) and exposes
// typed lookup over the per-hero talent trees.
// -----------------------------------------------------------------------------
// Source: docs/hero-talent-trees-spec.md. Shape:
//   trees[heroSlug] = {
//     heroSlug, displayName,
//     nodes[]: { id, name, tier, column, cost, description, prerequisites[] }
//   }
// Each hero has 6 nodes (3 tiers x 2 columns). Tier costs: 1 / 2 / 3 Wisdom.
// Mirrors PetSkillTreeCatalog line-for-line so the loader pattern stays
// consistent across the codebase. Save-side unlock state lives in
// WisdomCurrencyService, not here.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Talents
{
    [Serializable]
    public sealed class HeroTalentNodeDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("tier")] public string Tier;            // tier1 | tier2 | tier3
        [JsonProperty("column")] public string Column;        // a | b
        [JsonProperty("cost")] public int Cost;
        [JsonProperty("description")] public string Description;
        [JsonProperty("prerequisites")] public List<string> Prerequisites = new List<string>();

        // WO-36 (talent -> stat half): additive ability stat modifiers applied while
        // this node is unlocked. Both default to 0 so any node lacking these keys
        // contributes nothing. damageBonus is an additive fraction (0.10 = +10%
        // ability damage); cdReduction is a 0..1 fraction shaved off cooldowns
        // (0.15 = -15% cooldown). Summed across a hero's unlocked nodes by
        // HeroTalentModifiers and folded into HeroAbilities' damage/cooldown math.
        [JsonProperty("damageBonus")] public float DamageBonus;
        [JsonProperty("cdReduction")] public float CdReduction;
    }

    [Serializable]
    public sealed class HeroTalentTreeDef
    {
        [JsonProperty("heroSlug")] public string HeroSlug;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("nodes")] public List<HeroTalentNodeDef> Nodes = new List<HeroTalentNodeDef>();
    }

    [Serializable]
    public sealed class HeroTalentTierCostDef
    {
        [JsonProperty("tier1")] public int Tier1 = 1;
        [JsonProperty("tier2")] public int Tier2 = 2;
        [JsonProperty("tier3")] public int Tier3 = 3;
    }

    [Serializable]
    public sealed class HeroTalentData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("respecCostCrystals")] public int RespecCostCrystals = 300;
        [JsonProperty("tierCosts")] public HeroTalentTierCostDef TierCosts = new HeroTalentTierCostDef();
        [JsonProperty("trees")] public Dictionary<string, HeroTalentTreeDef> Trees =
            new Dictionary<string, HeroTalentTreeDef>();
    }

    public static class HeroTalentCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/hero-talents.json";
        private static HeroTalentData _data;

        public static int RespecCostCrystals { get { EnsureLoaded(); return _data.RespecCostCrystals; } }
        public static HeroTalentTierCostDef TierCosts { get { EnsureLoaded(); return _data.TierCosts; } }

        public static HeroTalentTreeDef GetTree(string heroSlug)
        {
            if (string.IsNullOrEmpty(heroSlug)) return null;
            EnsureLoaded();
            _data.Trees.TryGetValue(heroSlug, out var tree);
            return tree;
        }

        public static IEnumerable<HeroTalentTreeDef> AllTrees
        {
            get { EnsureLoaded(); return _data.Trees.Values; }
        }

        public static HeroTalentNodeDef FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            EnsureLoaded();
            foreach (var tree in _data.Trees.Values)
                foreach (var n in tree.Nodes)
                    if (n.Id == nodeId) return n;
            return null;
        }

        /// <summary>
        /// Returns true if the player has enough Wisdom AND every prerequisite
        /// for <paramref name="nodeId"/> is already in <paramref name="unlocked"/>.
        /// </summary>
        public static bool CanUnlock(string nodeId, int wisdom, HashSet<string> unlocked)
        {
            var n = FindNode(nodeId);
            if (n == null) return false;
            if (unlocked != null && unlocked.Contains(nodeId)) return false;
            if (wisdom < n.Cost) return false;
            if (unlocked == null) unlocked = new HashSet<string>();
            foreach (var pr in n.Prerequisites)
                if (!unlocked.Contains(pr)) return false;
            return true;
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            var full = Path.Combine(Application.streamingAssetsPath, StreamingRelativePath);
            try
            {
                if (File.Exists(full))
                {
                    var parsed = JsonConvert.DeserializeObject<HeroTalentData>(File.ReadAllText(full));
                    if (parsed != null && parsed.Trees != null && parsed.Trees.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError($"[HeroTalentCatalog] {StreamingRelativePath} parsed empty.");
                }
                else Debug.LogError($"[HeroTalentCatalog] file not found at {full}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeroTalentCatalog] Read failed: {ex.Message}");
            }
            _data = new HeroTalentData();
        }
    }
}
