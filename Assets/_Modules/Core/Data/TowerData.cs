// =============================================================================
// TowerData — DEF-73 / DEF-74 (Linear). ScriptableObject defining a tower type:
// name, build cost, skill gate, and a 3-level upgrade chain.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Data (the namespace inside the existing DeNelle.Core
// asmdef — no new asmdef per the codebase adaptation; Village already references
// Core). Per DEF-73/74 Correction Passes:
//   • namespace DeNelle.Core.Data + [CreateAssetMenu(... fileName="TowerData")].
//   • SkillRequirement / SkillType / SpecialAbility are NOT declared here — they
//     live in SkillTypes.cs / SpecialAbility.cs (shared with SkillSystem).
//   • NO `prefab` / `basePrefab` field — that was dead code. ALL visuals come from
//     upgrades[level-1].visualPrefab; the Level 1 visual is upgrades[0].
//   • upgrades is exactly 3 entries: [0]=Level 1, [1]=Level 2, [2]=Level 3.
//   • TowerUpgrade.upgradeCost (DEF-74) feeds the upgrade button's cost check.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Core.Data
{
    /// <summary>
    /// Which air/ground layers a tower can acquire + fire at — the tower side of
    /// the TD targeting matrix. Anti-ground towers (melee/cannon/spike) cannot
    /// touch flyers; anti-air towers (archer/arcane) can; <see cref="Both"/> hits
    /// everything. Defaults to <see cref="Ground"/> so an un-tagged tower keeps the
    /// original all-ground behaviour.
    /// </summary>
    public enum TowerTargets
    {
        /// <summary>Ground enemies only — cannot hit flyers. The default.</summary>
        Ground = 0,
        /// <summary>Flying enemies only — dedicated anti-air.</summary>
        Air = 1,
        /// <summary>Both ground and air — the flexible (usually pricier) pick.</summary>
        Both = 2,
    }

    /// <summary>Authoring data for one tower type and its 3-level upgrade chain.</summary>
    [CreateAssetMenu(menuName = "Defenders/Tower Data", fileName = "TowerData")]
    public class TowerData : ScriptableObject
    {
        public string towerName = "Archer Tower";
        public int cost = 150;

        [Header("Targeting matrix (air/ground)")]
        [Tooltip("Which enemy layers this tower can acquire + fire at. Ground = can't " +
                 "hit flyers; Air = anti-air only; Both = hits everything. Defaults to " +
                 "Ground so untagged towers keep the original all-ground behaviour.")]
        public TowerTargets targets = TowerTargets.Ground;

        [Header("Requirements")]
        public SkillRequirement requiredSkill;

        [Header("Upgrades (3 Levels)")]
        public TowerUpgrade[] upgrades = new TowerUpgrade[3];

        // DEF-76 — construction additive delta. There is intentionally NO `prefab`
        // field (DEF-73 removed it): the tower root is a bare GameObject and its
        // visual is built by Tower.Initialize. These three drive TowerConstruction.
        [Header("Construction (DEF-76)")]
        public GameObject scaffoldingPrefab;     // shown while building (null → code placeholder)
        public float buildTime = 5f;             // seconds to raise the tower
        public ParticleSystem workerHammerVFX;   // looping worker FX during construction (optional)

        [Header("Empowerment (unlocked at Max Level)")]
        [Tooltip("Leave null or ability=None to disable the Empower button for this tower type.")]
        public TowerEmpowermentData empowerment;

        [Header("UI")]
        public GameObject upgradeUIPrefab;

        /// <summary>
        /// The central air/ground matrix gate: true when a tower whose mask is
        /// <paramref name="targets"/> may fire at a target on <paramref name="layer"/>.
        /// Ground towers hit only Ground; Air towers hit only Flying; Both hits all.
        /// This is the single rock-paper-scissors rule — the tower acquisition loop
        /// calls it and skips any target it returns false for.
        /// </summary>
        public static bool CanTarget(TowerTargets targets, CombatLayer layer)
        {
            switch (targets)
            {
                case TowerTargets.Both: return true;
                case TowerTargets.Air:  return layer == CombatLayer.Flying;
                default:                return layer == CombatLayer.Ground;   // Ground
            }
        }
    }

    /// <summary>
    /// One level of a tower's upgrade chain. <see cref="visualPrefab"/> is the model
    /// shown at that level (null → Tower builds a procedural placeholder). The Level
    /// N entry is <c>upgrades[N - 1]</c>.
    /// </summary>
    [System.Serializable]
    public class TowerUpgrade
    {
        public GameObject visualPrefab;
        public SpecialAbility ability = SpecialAbility.None;
        public float range  = 10f;
        public float damage = 8f;
        public int upgradeCost = 100;   // DEF-74 — cost to upgrade INTO this level
    }

    /// <summary>
    /// Authoring data for a tower's max-level Empowerment — the ability unlocked
    /// after Level 3. Cost is in Aether Crystals (local-only, off-chain currency).
    /// Assign once per TowerData asset; leave <see cref="ability"/> as None and the
    /// Empower button will not appear for that tower type.
    /// </summary>
    [System.Serializable]
    public class TowerEmpowermentData
    {
        [Tooltip("Human-readable ability name shown in the Empower button and tooltip.")]
        public string abilityName = "Empowerment";

        [Tooltip("Short description shown in the Empower confirm popup.")]
        [TextArea(2, 4)]
        public string abilityDescription = "Unlocks a unique ability for this tower.";

        [Tooltip("Aether Crystal cost to activate. Deducted by CrystalEconomy.TrySpend().")]
        [Min(1)] public int crystalCost = 8;

        [Tooltip("Which empowerment behavior to activate in TowerCombat.")]
        public EmpowermentAbility ability = EmpowermentAbility.None;

        [Tooltip("One-shot VFX prefab instantiated at world origin at the moment of empowerment (nova burst). Auto-destroyed after 4 s.")]
        public GameObject empowerNovaPrefab;

        [Tooltip("Persistent VFX prefab parented to the tower permanently after empowerment (aura glow loop).")]
        public GameObject empowerAuraPrefab;
    }
}
