// =============================================================================
// PetSkillTreeCatalog — loads pet-skill-trees.json (StreamingAssets) and
// exposes typed lookup over the per-species progression trees.
// -----------------------------------------------------------------------------
// Source: docs/draft-backend-endpoints / docs/pet-skill-tree-spec.md and the
// React PET_SKILL_TREES export in src/data/gameDesign.ts. The shape is:
//   trees[species] = {
//     species, element, displayName,
//     skills[]: { id, name, type, tier, description, cooldownSeconds?,
//                 unlockLevel, prerequisites[] }
//   }
// UI consumers (HUD pet-roster panel, eventual Skill Tree screen) read off
// the catalog. Save-side unlock state lives in GameState, not here.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Pets
{
    [Serializable]
    public sealed class PetSkillDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("type")] public string Type;            // active | passive
        [JsonProperty("tier")] public string Tier;            // starter | tier1 | tier2 | ultimate
        [JsonProperty("description")] public string Description;
        [JsonProperty("cooldownSeconds")] public float? CooldownSeconds;
        [JsonProperty("unlockLevel")] public int UnlockLevel;
        [JsonProperty("prerequisites")] public List<string> Prerequisites = new List<string>();

        // ── WO-298 additive content fields (optional; legacy skills omit them) ──
        // Which of the four design branches this node lives on (DESIGN_PET_SYSTEM §4).
        [JsonProperty("branch")] public string Branch;        // harvest | combat | utility | aura | signature
        // Numeric balance for passive bonuses. Magnitude is the additive amount
        // (e.g. 0.10 for +10% yield, 2 for +2 carry); MagnitudeType names what it
        // applies to so the runtime can route the effect (yieldPct, gatherSpeedPct,
        // autoRange, offlineCapPct, carryCap, moveSpeedPct, attack, healPerSec, ...).
        [JsonProperty("magnitude")] public float? Magnitude;
        [JsonProperty("magnitudeType")] public string MagnitudeType;
        // Apex/signature gate: id of a quest item required to unlock (null = none).
        [JsonProperty("questItem")] public string QuestItem;
        // Skill-point cost to unlock (default 1 = one level's point).
        [JsonProperty("cost")] public int Cost = 1;

        public bool IsActive  => string.Equals(Type, "active",  StringComparison.OrdinalIgnoreCase);
        public bool IsPassive => string.Equals(Type, "passive", StringComparison.OrdinalIgnoreCase);
        public bool IsSignature => string.Equals(Branch, "signature", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(Tier, "signature", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class PetSkillTreeDef
    {
        [JsonProperty("species")] public string Species;
        [JsonProperty("element")] public string Element;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("skills")] public List<PetSkillDef> Skills = new List<PetSkillDef>();
    }

    [Serializable]
    public sealed class PetSkillTreeData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("petMaxLevel")] public int PetMaxLevel = 20;
        [JsonProperty("petLoadoutSize")] public int PetLoadoutSize = 3;
        // WO-298: respec at the Stables (DESIGN_PET_SYSTEM §4). Cost in Food + Glimmer.
        [JsonProperty("respecFoodCost")] public int RespecFoodCost = 50;
        [JsonProperty("respecGlimmerCost")] public int RespecGlimmerCost = 5;
        [JsonProperty("trees")] public Dictionary<string, PetSkillTreeDef> Trees =
            new Dictionary<string, PetSkillTreeDef>();
    }

    public static class PetSkillTreeCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/pet-skill-trees.json";
        private static PetSkillTreeData _data;

        public static int PetMaxLevel    { get { EnsureLoaded(); return _data.PetMaxLevel; } }
        public static int PetLoadoutSize { get { EnsureLoaded(); return _data.PetLoadoutSize; } }
        public static int RespecFoodCost    { get { EnsureLoaded(); return _data.RespecFoodCost; } }
        public static int RespecGlimmerCost { get { EnsureLoaded(); return _data.RespecGlimmerCost; } }

        /// <summary>All skills on a species' tree that belong to <paramref name="branch"/>
        /// (harvest | combat | utility | aura | signature). Empty if none/unknown.</summary>
        public static IEnumerable<PetSkillDef> SkillsInBranch(string species, string branch)
        {
            var tree = GetTree(species);
            if (tree == null || tree.Skills == null) yield break;
            foreach (var s in tree.Skills)
                if (s != null && string.Equals(s.Branch, branch, StringComparison.OrdinalIgnoreCase))
                    yield return s;
        }

        /// <summary>The signature (apex) skill for a species, or null.</summary>
        public static PetSkillDef GetSignature(string species)
        {
            var tree = GetTree(species);
            if (tree == null || tree.Skills == null) return null;
            foreach (var s in tree.Skills)
                if (s != null && s.IsSignature) return s;
            return null;
        }

        public static PetSkillTreeDef GetTree(string species)
        {
            if (string.IsNullOrEmpty(species)) return null;
            EnsureLoaded();
            _data.Trees.TryGetValue(species, out var tree);
            return tree;
        }

        public static IEnumerable<PetSkillTreeDef> AllTrees
        {
            get { EnsureLoaded(); return _data.Trees.Values; }
        }

        public static PetSkillDef FindSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            EnsureLoaded();
            foreach (var tree in _data.Trees.Values)
                foreach (var s in tree.Skills)
                    if (s.Id == skillId) return s;
            return null;
        }

        /// <summary>
        /// Returns true if the prereq tree for <paramref name="skillId"/> is
        /// satisfied — every prerequisite skill exists in
        /// <paramref name="unlocked"/> AND the unlock-level requirement is met
        /// by <paramref name="petLevel"/>.
        /// </summary>
        public static bool CanUnlock(string skillId, int petLevel, HashSet<string> unlocked)
        {
            var s = FindSkill(skillId);
            if (s == null) return false;
            if (petLevel < s.UnlockLevel) return false;
            if (unlocked == null) unlocked = new HashSet<string>();
            foreach (var pr in s.Prerequisites)
                if (!unlocked.Contains(pr)) return false;
            return true;
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            // WebGL-safe load via CanonicalJson (Resources first, StreamingAssets fallback).
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<PetSkillTreeData>(json);
                    if (parsed != null && parsed.Trees != null && parsed.Trees.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError($"[PetSkillTreeCatalog] {StreamingRelativePath} parsed empty.");
                }
                else Debug.LogError($"[PetSkillTreeCatalog] {StreamingRelativePath} not found (Resources or StreamingAssets).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PetSkillTreeCatalog] Read failed: {ex.Message}");
            }
            _data = new PetSkillTreeData();
        }
    }
}
