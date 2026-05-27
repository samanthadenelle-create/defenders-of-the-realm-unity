// =============================================================================
// TowerDataSeeder — DEF-73/74 (Linear) test fixture. Creates a few sample
// TowerData .asset files so the placement → upgrade loop is testable without
// hand-authoring ScriptableObjects in the Inspector.
// -----------------------------------------------------------------------------
// Editor-only (DeNelle.Editor asmdef, which references DeNelle.Core → TowerData).
// Run via the menu: Defenders → Seed Tower Data. Writes to
// Assets/Resources/Towers/ so the assets can be loaded with Resources.Load if a
// runtime catalog wants them later. visualPrefab is left null on purpose — Tower
// builds a per-level procedural placeholder (no authored tower art yet).
// =============================================================================

using UnityEditor;
using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Editor
{
    public static class TowerDataSeeder
    {
        private const string Dir = "Assets/Resources/Towers";

        [MenuItem("Defenders/Seed Tower Data")]
        public static void Seed()
        {
            EnsureFolder();

            CreateTower("ArcherTower", "Archer Tower", 150,
                new SkillRequirement { type = SkillType.None, minLevel = 0 },
                ability: SpecialAbility.None,
                baseRange: 10f, baseDamage: 8f, baseUpgradeCost: 100);

            CreateTower("MageTower", "Mage Tower", 220,
                new SkillRequirement { type = SkillType.Arcane, minLevel = 1 },
                ability: SpecialAbility.MagicalAffinity,
                baseRange: 8f, baseDamage: 12f, baseUpgradeCost: 140);

            CreateTower("FrostTower", "Frost Tower", 200,
                new SkillRequirement { type = SkillType.Blacksmith, minLevel = 1 },
                ability: SpecialAbility.SlowEnemies,
                baseRange: 9f, baseDamage: 6f, baseUpgradeCost: 130);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TowerDataSeeder] Seeded 3 TowerData assets into {Dir}.");
        }

        private static void CreateTower(
            string assetName, string towerName, int cost, SkillRequirement req,
            SpecialAbility ability, float baseRange, float baseDamage, int baseUpgradeCost)
        {
            string path = $"{Dir}/{assetName}.asset";

            var data = ScriptableObject.CreateInstance<TowerData>();
            data.towerName = towerName;
            data.cost = cost;
            data.requiredSkill = req;
            data.upgrades = new TowerUpgrade[3];
            for (int i = 0; i < 3; i++)
            {
                data.upgrades[i] = new TowerUpgrade
                {
                    visualPrefab = null,                       // procedural placeholder
                    ability      = i == 0 ? SpecialAbility.None : ability,
                    range        = baseRange + i * 2f,
                    damage       = baseDamage + i * 4f,
                    // Level 1 has no upgrade-INTO cost; L2/L3 escalate.
                    upgradeCost  = i == 0 ? 0 : baseUpgradeCost + (i - 1) * 80,
                };
            }

            // Overwrite any prior seed cleanly so re-running stays idempotent.
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Resources", "Towers");
        }
    }
}
