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
            // Owner pick 2026-06-04: lightweight polyperfect PIRATE towers (match the pirate
            // rampart platform). Wood (L1) -> Stone (L2/L3); upgrade bumps stats + grows the mesh.
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Pirates_M/Tower_Pirate_Wood.prefab",
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Pirates_M/Tower_Pirate_Stone.prefab",
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Pirates_M/Tower_Pirate_Stone.prefab",
        };

        // DEF-244: real polyperfect _M medieval/castle tower meshes, indexed per
        // upgrade tier (L1 / L2 / L3). Each roster tower swaps mesh on upgrade so the
        // player FEELS the climb (watchtower -> keep -> crenellated bastion). Paths
        // are LogWarning-safe on miss (pack is gitignored) -> procedural placeholder.
        // Folder convention per docs/polyperfect-asset-catalog.md (_M/Prefabs_M/<Cat>_M/).
        private const string MedievalDir =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M";

        // Archer (single-target ranged): wooden watchtower -> square keep -> round bastion.
        private static readonly string[] ArcherModelPaths =
        {
            MedievalDir + "/Tower_Medieval_Wood.prefab",
            MedievalDir + "/Tower_Castle_Square.prefab",
            MedievalDir + "/Tower_Castle_Round.prefab",
        };

        // Mage (AoE arcane): the tall standalone spire reads as a ward-stone tower at every tier.
        private static readonly string[] MageModelPaths =
        {
            MedievalDir + "/Tower_Medieval_Big.prefab",
            MedievalDir + "/Tower_Medieval_Big.prefab",
            MedievalDir + "/Tower_Castle_Round.prefab",
        };

        // Frost (slow / crowd control): wooden -> square -> round, distinct from Archer by stats/ability.
        private static readonly string[] FrostModelPaths =
        {
            MedievalDir + "/Tower_Medieval_Wood.prefab",
            MedievalDir + "/Tower_Castle_Square.prefab",
            MedievalDir + "/Tower_Castle_Round.prefab",
        };

        [MenuItem("Defenders/Seed Tower Data")]
        public static void Seed()
        {
            EnsureFolder();

            // ---- DEF-244: 3 role-distinct demo towers, each with a 3-tier upgrade
            //      path (escalating range/damage/cost + tier-specific SpecialAbility)
            //      and real per-tier polyperfect _M meshes. DevTower/pirate paths are
            //      intentionally untouched (leave them alone to avoid a merge conflict).

            // 1) ARCHER TOWER — single-target ranged generalist. The first tower a
            //    player builds: high rate of fire, no gate. T3 sharpens into a true
            //    marksman bastion (long range + heavy hit).
            CreateTower("ArcherTower", "Archer Tower", 150,
                new SkillRequirement { type = SkillType.None, minLevel = 0 },
                tierAbilities: new[] { SpecialAbility.None, SpecialAbility.None, SpecialAbility.None },
                tierRange:  new[] { 18f, 20f, 22f },
                tierDamage: new[] { 22f, 35f, 50f },
                tierCost:   new[] { 0,   100,  220 },
                modelPaths: ArcherModelPaths,
                // Air/ground matrix: arrows reach the sky — the generalist that also
                // covers anti-air, so a player always has *some* answer to a flyer.
                targets: TowerTargets.Both);

            // 2) MAGE TOWER — AoE arcane caster. Lower fire rate, hits clusters; the
            //    Arcane skill gate (Arcane Library, DEF-209) unlocks it. MagicalAffinity
            //    from T2 up = the arcane AoE pulse.
            CreateTower("MageTower", "Mage Tower", 220,
                new SkillRequirement { type = SkillType.Arcane, minLevel = 1 },
                tierAbilities: new[] { SpecialAbility.None, SpecialAbility.MagicalAffinity, SpecialAbility.MagicalAffinity },
                tierRange:  new[] { 14f, 16f, 18f },
                tierDamage: new[] { 18f, 28f, 40f },
                tierCost:   new[] { 0,   140,  300 },
                modelPaths: MageModelPaths,
                // Air/ground matrix: the ward-stone's arcane bolts are the dedicated
                // ANTI-AIR pick (skybound only) — pairs with a ground tower for full
                // coverage and makes the dragon wave a real "did you build anti-air?" check.
                targets: TowerTargets.Air);

            // 3) FROST TOWER — crowd control / slow. Modest damage but its value is the
            //    SlowEnemies field (T2) escalating to a FrostNova burst (T3). Blacksmith
            //    gate (forged frost-iron bolts, DEF-209).
            CreateTower("FrostTower", "Frost Tower", 200,
                new SkillRequirement { type = SkillType.Blacksmith, minLevel = 1 },
                tierAbilities: new[] { SpecialAbility.None, SpecialAbility.SlowEnemies, SpecialAbility.FrostNova },
                tierRange:  new[] { 12f, 14f, 16f },
                tierDamage: new[] { 8f,  14f, 22f },
                tierCost:   new[] { 0,   130,  280 },
                modelPaths: FrostModelPaths,
                // Air/ground matrix: the frost field is a GROUND crowd-control anchor —
                // great against the marching swarm, useless against the dragon (build
                // anti-air for that). This is the rock-paper-scissors trade.
                targets: TowerTargets.Ground);

            // Free, ungated tower wired with real KayKit hex models —
            // TowerLoopDevHarness loads this one ("Towers/DevTower") so the dev
            // B/U loop shows a real tower stepping up A -> B -> cannon per level.
            CreateDevTower();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TowerDataSeeder] Seeded 4 TowerData assets into {Dir} " +
                      "(Archer/Mage/Frost roster + DevTower, all with per-tier models).");
        }

        /// <summary>
        /// Creates one roster TowerData with an explicit per-tier upgrade chain:
        /// distinct mesh, ability, range, damage, and upgrade-into cost at each of the
        /// 3 levels. Arrays are indexed [0]=L1, [1]=L2, [2]=L3. Missing meshes fall back
        /// to the procedural placeholder (LogWarning, not error — pack is gitignored).
        /// </summary>
        private static void CreateTower(
            string assetName, string towerName, int cost, SkillRequirement req,
            SpecialAbility[] tierAbilities, float[] tierRange, float[] tierDamage,
            int[] tierCost, string[] modelPaths, TowerTargets targets = TowerTargets.Ground)
        {
            string path = $"{Dir}/{assetName}.asset";

            var data = ScriptableObject.CreateInstance<TowerData>();
            data.towerName = towerName;
            data.cost = cost;
            data.requiredSkill = req;
            data.targets = targets;   // air/ground targeting matrix
            data.upgrades = new TowerUpgrade[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject model = null;
                if (modelPaths != null && i < modelPaths.Length)
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[i]);
                    if (model == null)
                        Debug.LogWarning($"[TowerDataSeeder] {towerName} L{i + 1} model not found at " +
                                         $"'{modelPaths[i]}' — Tower will use the procedural placeholder.");
                }

                data.upgrades[i] = new TowerUpgrade
                {
                    visualPrefab = model,
                    ability      = tierAbilities[i],
                    range        = tierRange[i],
                    damage       = tierDamage[i],
                    upgradeCost  = tierCost[i],
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
