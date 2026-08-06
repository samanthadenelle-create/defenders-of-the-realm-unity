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
    /// <summary>
    /// WO-676: the effect-type vocabulary as compile-time keys. Consumer systems (economy /
    /// defense readers, lanes A1/A2) reference THESE constants — never string literals — when
    /// summing via <c>HeroTalentModifiers.StatSum</c>, so a typo is a compile error not a
    /// silent 0. Values are additive FRACTIONS unless noted. Combat types that predate WO-676
    /// keep their literal usage inside HeroTalentModifiers (unchanged behaviour).
    /// </summary>
    public static class HeroTalentEffectTypes
    {
        // ── STEWARD (economy) — WO-676 §A ────────────────────────────────────────
        /// <summary>+fraction echo/collector harvest rate (Provider's Bond). → EchoService tick + ResourceBuildingHarvester accrual.</summary>
        public const string HarvestRate = "harvestRate";
        /// <summary>+fraction collector pending-capacity (Deep Reserves). → ResourceCollector capacity.</summary>
        public const string CollectorCap = "collectorCap";
        /// <summary>fraction OFF repair prices (Master Mason). → WO-672 repair pricing path.</summary>
        public const string RepairCost = "repairCost";
        /// <summary>fraction OFF build/upgrade timer durations (Foreman's Pace). → BuildTimerService duration calc.</summary>
        public const string BuildTime = "buildTime";
        /// <summary>+fraction refunded when selling/losing a structure (Salvager). → BuildModeController sell + WO-672 destroyed-loss.</summary>
        public const string Salvage = "salvage";
        /// <summary>+fraction wave rewards (Bountiful Banners capstone). → wave reward grant path.</summary>
        public const string WaveReward = "waveReward";

        // ── BULWARK (defensive structures) — WO-676 §A ───────────────────────────
        /// <summary>+fraction tower damage (Keen Ballistics). → DefenseTower/ArcaneTower damage calc.</summary>
        public const string TowerDamage = "towerDamage";
        /// <summary>+METERS of tower range (Farsight Emplacements). Additive meters, NOT a fraction.</summary>
        public const string TowerRange = "towerRange";
        /// <summary>fraction OFF damage walls/gates/towers take, always-on (Hardened Ramparts). → structure damage intake.</summary>
        public const string StructureToughness = "structureToughness";
        /// <summary>+fraction tower fire rate (Standing Orders). → tower fire-rate calc.</summary>
        public const string TowerAttackSpeed = "towerAttackSpeed";
        /// <summary>fraction OFF damage ALL defenses take while a wave is ACTIVE only (Warden of
        /// Elarion capstone — owner pin 4: DEFEND-scoped -25%). The consumer adds this to
        /// <see cref="StructureToughness"/> only when WaveManager reports a live wave.</summary>
        public const string StructureToughnessWave = "structureToughnessWave";
    }

    /// <summary>
    /// v2 talent effect payload (data-driven, owner-thinks-in-data-structures). One small
    /// record per node carrying the effect <see cref="Type"/> + the parameters any handler
    /// might read. Unused params stay 0/null so a node only carries what it needs. The
    /// effect interpreter lives in <c>HeroTalentModifiers</c>; pure-stat types aggregate into
    /// the hero's multipliers, behavioural/ally types are data-only in V1 (see node notes).
    /// </summary>
    [Serializable]
    public sealed class HeroTalentEffectDef
    {
        [JsonProperty("type")] public string Type;        // damageReduction | blockChance | defense | maxHpPct | damageBonus | cdReduction | unlockAbility | modifyAbility | aura | onEvent | proc | taunt | reflect | laststand | invuln | summon | stealth | stun | mark | pull | allStatsPct | critChance | attackSpeed | manaRegen | manaCostReduction | healthRegen | shieldStrength | wisdomPerLevel | moveSpeed | range | dodge | harvestRate | collectorCap | repairCost | buildTime | salvage | waveReward | towerDamage | towerRange | structureToughness | towerAttackSpeed | structureToughnessWave  (WO-676 strategic types = the HeroTalentEffectTypes consts above)
        [JsonProperty("value")] public float Value;       // primary magnitude (fraction or amount)
        [JsonProperty("stat")] public string Stat;        // disambiguator for modifyAbility (e.g. "heal")
        [JsonProperty("ability")] public string Ability;  // target/unlock ability id
        [JsonProperty("targets")] public int Targets;     // affected-count (taunt count etc.)
        [JsonProperty("radius")] public float Radius;
        [JsonProperty("duration")] public float Duration;
        [JsonProperty("cooldown")] public float Cooldown;
        [JsonProperty("chance")] public float Chance;
        [JsonProperty("threshold")] public float Threshold; // HP-fraction trigger (laststand)
        [JsonProperty("reflect")] public float Reflect;
        [JsonProperty("condition")] public string Condition; // e.g. "stationary"
        [JsonProperty("ally")] public bool Ally;          // ally-dependent — no-op in solo V1
        [JsonProperty("allyValue")] public float AllyValue; // the ally-portion magnitude (V2)
        [JsonProperty("note")] public string Note;        // human note (e.g. "(allies — V2)")
    }

    [Serializable]
    public sealed class HeroTalentNodeDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("tier")] public string Tier;            // tier1 | tier2 | tier3 | tier4
        [JsonProperty("slot")] public int Slot;               // 1..5 (v2 column within a tier)
        [JsonProperty("column")] public string Column;        // a | b (legacy v1 only)
        [JsonProperty("cost")] public int Cost;
        [JsonProperty("iconPath")] public string IconPath;    // Resources sprite path (e.g. Talents/knight/knight_01)
        [JsonProperty("description")] public string Description;
        [JsonProperty("effect")] public HeroTalentEffectDef Effect; // v2 effect payload
        [JsonProperty("prerequisites")] public List<string> Prerequisites = new List<string>();

        // WO-676 strategic branches: "war" | "steward" | "bulwark". Absent/empty = war
        // (the legacy combat nodes) — the View uses this for branch section dividers.
        [JsonProperty("branch")] public string Branch;

        // WO-676 G3 (wire-or-hide, no dead nodes): a node whose effect type has NO
        // registered runtime consumer must carry "hidden": true — the node is then omitted
        // from the skill-tree View and the EditMode gate stops failing it. Defaults false.
        //
        // 2026-08-05 (WO-910): until today this comment CLAIMED "the View skips it" while
        // NOTHING read this field at runtime — HeroSkillTreeVM.Rebuild enumerated every node
        // unconditionally. The wire-or-hide law's second option therefore did not exist:
        // setting "hidden": true silenced the EditMode gate and left the node fully clickable
        // in the player's tree. A field whose comment asserts behaviour it does not have is
        // worse than no field, so the READER was added (HeroSkillTreeVM.Rebuild, both the hero
        // tree and the shared pool) rather than the field deleted — the law depends on hidden
        // MEANING something.
        //
        // CAUTION when you do set it: hiding a node does NOT rewrite the prerequisite graph.
        // Any visible node whose only prerequisite is the node you hide becomes permanently
        // unreachable ("Requires <hidden node>"). Check downstream reachability first — see
        // the stranding analysis in WORK_ORDER_910_ranger_mage_talent_consumers.md.
        [JsonProperty("hidden")] public bool Hidden;

        // Node-graph (Path B) layout: canvas-relative position (0..1; y 0=top, 1=bottom)
        // and OPTIONAL extra cosmetic connector targets beyond prerequisites. -1 = unset
        // (the View falls back to a tier/slot auto-position). Connectors are drawn along
        // prerequisites by default; Edges adds non-prereq links only if authored.
        [JsonProperty("x")] public float X = -1f;
        [JsonProperty("y")] public float Y = -1f;
        [JsonProperty("edges")] public List<string> Edges = new List<string>();

        /// <summary>True when this node carries an authored graph position.</summary>
        [JsonIgnore]
        public bool HasPosition => X >= 0f && Y >= 0f;

        // WO-36 (talent -> stat half): additive ability stat modifiers applied while
        // this node is unlocked. Both default to 0 so any node lacking these keys
        // contributes nothing. damageBonus is an additive fraction (0.10 = +10%
        // ability damage); cdReduction is a 0..1 fraction shaved off cooldowns
        // (0.15 = -15% cooldown). Summed across a hero's unlocked nodes by
        // HeroTalentModifiers and folded into HeroAbilities' damage/cooldown math.
        [JsonProperty("damageBonus")] public float DamageBonus;
        [JsonProperty("cdReduction")] public float CdReduction;

        // Knight skill-tree (loadout spine): a SKILL node carries an abilityId that
        // references an entry in abilities.json — the loadout chooser equips these
        // into a Q/W/E/R slot. A STAT node leaves abilityId empty (default) and
        // contributes the passive damageBonus/cdReduction above. 'kind' is an
        // optional explicit label ("skill" | "stat"); IsSkill below treats any node
        // with a non-empty abilityId as a skill node regardless, so the field is a
        // convenience for authoring/UI and defaults empty = stat node (additive, no
        // behaviour change for existing trees).
        [JsonProperty("abilityId")] public string AbilityId;
        [JsonProperty("kind")] public string Kind;

        /// <summary>True when this node equips an ability (carries an abilityId).</summary>
        [JsonIgnore]
        public bool IsSkill => !string.IsNullOrEmpty(AbilityId);
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
        [JsonProperty("tier4")] public int Tier4 = 5;   // v2 capstone tier
    }

    [Serializable]
    public sealed class HeroTalentData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("respecCostCrystals")] public int RespecCostCrystals = 300;
        [JsonProperty("sharedNodeCost")] public int SharedNodeCost = 2;   // v2 universal-pool cost
        [JsonProperty("tierCosts")] public HeroTalentTierCostDef TierCosts = new HeroTalentTierCostDef();
        [JsonProperty("trees")] public Dictionary<string, HeroTalentTreeDef> Trees =
            new Dictionary<string, HeroTalentTreeDef>();
        // v2 Shared Universal pool — 8 free-standing nodes any hero may draw from.
        [JsonProperty("shared")] public List<HeroTalentNodeDef> Shared = new List<HeroTalentNodeDef>();
    }

    public static class HeroTalentCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/hero-talents.json";
        private static HeroTalentData _data;

        public static int RespecCostCrystals { get { EnsureLoaded(); return _data.RespecCostCrystals; } }
        public static int SharedNodeCost { get { EnsureLoaded(); return _data.SharedNodeCost; } }
        public static HeroTalentTierCostDef TierCosts { get { EnsureLoaded(); return _data.TierCosts; } }

        /// <summary>The 8 Shared Universal nodes (v2). Never null.</summary>
        public static IReadOnlyList<HeroTalentNodeDef> SharedNodes
        {
            get { EnsureLoaded(); return _data.Shared ?? new List<HeroTalentNodeDef>(); }
        }

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
            // v2: the Shared Universal pool is a flat list outside the per-hero trees.
            if (_data.Shared != null)
                foreach (var s in _data.Shared)
                    if (s != null && s.Id == nodeId) return s;
            return null;
        }

        /// <summary>
        /// Returns true if the player has enough Wisdom AND every prerequisite
        /// for <paramref name="nodeId"/> is already in <paramref name="unlocked"/>
        /// AND the v2 capstone-exclusivity rule allows it (one Tier-4 per hero tree).
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
            // v2 capstone exclusivity — a hero may hold AT MOST ONE Tier-4 capstone.
            if (IsCapstone(n) && AnotherCapstoneUnlocked(nodeId, unlocked)) return false;
            return true;
        }

        /// <summary>
        /// True when <paramref name="n"/> is a Tier-4 capstone node. The Shared Universal
        /// pool ("shared.*", tier "shared") is explicitly EXCLUDED — shared nodes are not
        /// capstones and are never subject to the one-capstone-per-hero rule.
        /// </summary>
        public static bool IsCapstone(HeroTalentNodeDef n)
        {
            if (n == null) return false;
            if (!string.IsNullOrEmpty(n.Id) && n.Id.StartsWith("shared.", StringComparison.Ordinal))
                return false;
            return string.Equals(n.Tier, "tier4", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The hero-tree slug a node id belongs to (prefix before the first '.'), or "".</summary>
        private static string HeroSlugOf(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return "";
            int dot = nodeId.IndexOf('.');
            return dot > 0 ? nodeId.Substring(0, dot) : "";
        }

        /// <summary>
        /// v2 capstone exclusivity: returns true if ANY Tier-4 node in the SAME hero tree
        /// as <paramref name="nodeId"/> — other than <paramref name="nodeId"/> itself — is
        /// already in <paramref name="unlocked"/>. Used to dim the other capstones once one
        /// is taken; a hero respec clears the set and re-frees the choice.
        /// </summary>
        public static bool AnotherCapstoneUnlocked(string nodeId, HashSet<string> unlocked)
        {
            if (unlocked == null || unlocked.Count == 0) return false;
            var slug = HeroSlugOf(nodeId);
            if (string.IsNullOrEmpty(slug)) return false;
            var tree = GetTree(slug);
            if (tree == null || tree.Nodes == null) return false;
            foreach (var node in tree.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                if (node.Id == nodeId) continue;
                if (IsCapstone(node) && unlocked.Contains(node.Id)) return true;
            }
            return false;
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            // DEF-212: the old path used File.ReadAllText(StreamingAssets), which
            // THROWS in WebGL (no filesystem) → every class showed "catalog
            // unavailable". Route through DeNelle.Core.CanonicalJson, which loads a
            // Resources.Load<TextAsset> copy first (works on ALL platforms incl.
            // WebGL) and falls back to StreamingAssets only on desktop. The dual
            // copy lives at Assets/Resources/Data/Canonical/hero-talents.json —
            // keep it in sync with the StreamingAssets source.
            try
            {
                var json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<HeroTalentData>(json);
                    if (parsed != null && parsed.Trees != null && parsed.Trees.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError($"[HeroTalentCatalog] {StreamingRelativePath} parsed empty.");
                }
                else Debug.LogError($"[HeroTalentCatalog] {StreamingRelativePath} not found (Resources or StreamingAssets).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeroTalentCatalog] Read failed: {ex.Message}");
            }
            _data = new HeroTalentData();
        }
    }
}
