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

        public bool IsActive  => string.Equals(Type, "active",  StringComparison.OrdinalIgnoreCase);
        public bool IsPassive => string.Equals(Type, "passive", StringComparison.OrdinalIgnoreCase);
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
        [JsonProperty("trees")] public Dictionary<string, PetSkillTreeDef> Trees =
            new Dictionary<string, PetSkillTreeDef>();
    }

    public static class PetSkillTreeCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/pet-skill-trees.json";
        private static PetSkillTreeData _data;

        public static int PetMaxLevel    { get { EnsureLoaded(); return _data.PetMaxLevel; } }
        public static int PetLoadoutSize { get { EnsureLoaded(); return _data.PetLoadoutSize; } }

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
