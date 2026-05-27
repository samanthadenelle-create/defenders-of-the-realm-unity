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

        // Tower visual meshes wired into the DevTower's upgrades[].visualPrefab
        // (level 1 / 2 / 3). >>> SWAP HERE <<< when the owner's real tower model
        // lands — point these at the new asset path(s) and re-run the menu item.
        // Missing paths fall back to the procedural placeholder (null is safe).
        private static readonly string[] DevTowerModelPaths =
        {
            // Owner's real model (viking watch tower). One model across all three
            // levels for now — the upgrade still bumps stats + fires the VFX; vary
            // the mesh/scale per level later if desired.
            "Assets/Art/Towers/VikingWatchTower/Tower.fbx",
            "Assets/Art/Towers/VikingWatchTower/Tower.fbx",
            "Assets/Art/Towers/VikingWatchTower/Tower.fbx",
        };

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

            // Free, ungated tower wired with real KayKit hex models —
            // TowerLoopDevHarness loads this one ("Towers/DevTower") so the dev
            // B/U loop shows a real tower stepping up A -> B -> cannon per level.
            CreateDevTower();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TowerDataSeeder] Seeded 4 TowerData assets into {Dir} (incl. DevTower with models).");
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

        /// <summary>
        /// A free, skill-gate-free tower wired with the <see cref="DevTowerModelPaths"/>
        /// meshes — what TowerLoopDevHarness loads ("Towers/DevTower") so the dev
        /// B/U loop shows a real tower per level. A missing/unfound model leaves
        /// visualPrefab null, and Tower falls back to its procedural placeholder.
        /// </summary>
        private static void CreateDevTower()
        {
            string path = $"{Dir}/DevTower.asset";

            var data = ScriptableObject.CreateInstance<TowerData>();
            data.towerName = "DevTower";
            data.cost = 0;
            data.buildTime = 2f;
            data.requiredSkill = new SkillRequirement { type = SkillType.None, minLevel = 0 };
            data.upgrades = new TowerUpgrade[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject model = null;
                if (i < DevTowerModelPaths.Length)
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(DevTowerModelPaths[i]);
                    if (model == null)
                        Debug.LogWarning($"[TowerDataSeeder] DevTower L{i + 1} model not found at " +
                                         $"'{DevTowerModelPaths[i]}' — Tower will use the procedural placeholder.");
                }
                data.upgrades[i] = new TowerUpgrade
                {
                    visualPrefab = model,
                    ability      = SpecialAbility.None,
                    range        = 8f + i * 2f,
                    damage       = 6f + i * 3f,
                    upgradeCost  = 0,
                };
            }

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
