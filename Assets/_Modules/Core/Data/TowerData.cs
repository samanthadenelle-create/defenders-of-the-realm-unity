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

namespace DeNelle.Core.Data
{
    /// <summary>Authoring data for one tower type and its 3-level upgrade chain.</summary>
    [CreateAssetMenu(menuName = "Defenders/Tower Data", fileName = "TowerData")]
    public class TowerData : ScriptableObject
    {
        public string towerName = "Archer Tower";
        public int cost = 150;

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

        [Header("UI")]
        public GameObject upgradeUIPrefab;
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
}
