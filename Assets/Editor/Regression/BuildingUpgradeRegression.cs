// =============================================================================
// BuildingUpgradeRegression — headless oracle for the resource-building upgrade
// tables (Farm / Lumbermill / Forge) and the DEF-121 Magic-gated Arcane Forge tier.
// -----------------------------------------------------------------------------
// "Real object in -> assert -> one marker": loads the REAL ResourceBuildingProgression
// catalog through the same lazy/Guarded path the game uses (ById -> Build()) and asserts
// the leveling invariants the upgrade flow (ResourceBuildingState.TryUpgrade) depends on:
//   1. The catalog builds all three resource buildings (no type-init poison / empty fallback).
//   2. Each building has MaxLevel >= 2, ascending YieldPerTick, and a MONOTONICALLY
//      non-increasing HarvestInterval (an upgrade must never tick slower).
//   3. Every NON-max level carries a non-empty UpgradeCost; the max level carries none.
//   4. The Forge is the only building with a Magic-gated tier: its top HARVEST level
//      (level 5) is IsMagicGated with MagicCost>0 and UnlocksTechNode == arcane_forge,
//      and the appended arcane level (6) is the true IsMaxLevel with a size multiplier > 1.
//   5. Farm/Lumbermill are 5 flat levels with NO magic gate anywhere.
//
// Pure data + logic — NO PlayMode, NO GameState — so it runs inside DataRegression.RunAll.
// Mirrors MonetizationCovenantRegression: public static bool Run(out string reason).
// =============================================================================
using System.Collections.Generic;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Editor
{
    public static class BuildingUpgradeRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            var farm = ResourceBuildingProgression.Find(ResourceBuildingProgression.FarmId);
            var mill = ResourceBuildingProgression.Find(ResourceBuildingProgression.LumbermillId);
            var forge = ResourceBuildingProgression.Find(ResourceBuildingProgression.ForgeId);

            if (farm == null) failures.Add("Farm curve missing (catalog build poisoned/empty fallback)");
            if (mill == null) failures.Add("Lumbermill curve missing (catalog build poisoned/empty fallback)");
            if (forge == null) failures.Add("Forge curve missing (catalog build poisoned/empty fallback)");

            // 1..3 general shape on every building that resolved.
            foreach (var def in new[] { farm, mill, forge })
            {
                if (def == null) continue;
                if (def.Levels == null || def.MaxLevel < 2)
                { failures.Add($"'{def.BuildingId}' has < 2 levels ({def.MaxLevel}) — not an upgrade curve"); continue; }

                int prevYield = int.MinValue;
                float prevInterval = float.MaxValue;
                for (int i = 0; i < def.Levels.Length; i++)
                {
                    var lvl = def.Levels[i];
                    if (lvl == null) { failures.Add($"'{def.BuildingId}' level index {i} is null"); continue; }

                    if (lvl.YieldPerTick <= prevYield)
                        failures.Add($"'{def.BuildingId}' level {lvl.Level} yield {lvl.YieldPerTick} did not ascend (prev {prevYield})");
                    prevYield = lvl.YieldPerTick;

                    // Speed ladder: never SLOWER than the level below (equal allowed at the arcane cap).
                    if (lvl.HarvestInterval > prevInterval + 0.0001f)
                        failures.Add($"'{def.BuildingId}' level {lvl.Level} interval {lvl.HarvestInterval}s is SLOWER than the level below ({prevInterval}s)");
                    prevInterval = lvl.HarvestInterval;

                    bool isMax = lvl.IsMaxLevel;
                    int costLines = lvl.UpgradeCost != null ? lvl.UpgradeCost.Count : 0;
                    if (isMax && (costLines > 0 || lvl.MagicCost > 0))
                        failures.Add($"'{def.BuildingId}' max level {lvl.Level} still carries an upgrade cost (should be terminal)");
                    if (!isMax && costLines == 0 && lvl.MagicCost <= 0)
                        failures.Add($"'{def.BuildingId}' non-max level {lvl.Level} has NO upgrade cost (dead-end mid-curve)");
                }
            }

            // 4. Forge — the DEF-121 Magic gate + arcane tier.
            if (forge != null)
            {
                if (forge.MaxLevel != 6)
                    failures.Add($"Forge should be 5 harvest levels + 1 arcane tier (6), found {forge.MaxLevel}");

                var topHarvest = forge.LevelDef(5);
                if (topHarvest == null || !topHarvest.IsMagicGated || topHarvest.MagicCost <= 0)
                    failures.Add("Forge level 5 (top harvest) is not Magic-gated (DEF-121 Arcane Forge gate missing)");
                else if (topHarvest.UnlocksTechNode != TechTree.ArcaneForgeNodeId)
                    failures.Add($"Forge level 5 unlocks '{topHarvest.UnlocksTechNode ?? "<null>"}', expected '{TechTree.ArcaneForgeNodeId}'");

                var arcane = forge.LevelDef(6);
                if (arcane == null || !arcane.IsMaxLevel)
                    failures.Add("Forge level 6 (arcane) is not the terminal max level");
                else if (arcane.YieldSizeMultiplier <= 1f)
                    failures.Add($"Forge arcane tier size multiplier {arcane.YieldSizeMultiplier} should exceed 1.0 (bigger haul)");
            }

            // 5. Farm/Lumbermill must be flat 5-level curves with NO magic gate.
            foreach (var def in new[] { farm, mill })
            {
                if (def == null) continue;
                if (def.MaxLevel != 5)
                    failures.Add($"'{def.BuildingId}' should be 5 flat levels (no magic tier), found {def.MaxLevel}");
                for (int lv = 1; lv <= def.MaxLevel; lv++)
                {
                    var d = def.LevelDef(lv);
                    if (d != null && d.IsMagicGated)
                        failures.Add($"'{def.BuildingId}' level {lv} is Magic-gated — only the Forge carries the tech axis (DEF-121)");
                }
            }

            if (failures.Count == 0)
            {
                reason = "BUILDING UPGRADE OK — Farm/Lumbermill (5 lvl) + Forge (5 + arcane) curves ascend, " +
                         "speeds monotone, costs terminal at max, only Forge Magic-gated (unlocks arcane_forge)";
                return true;
            }
            reason = $"BUILDING UPGRADE FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }
    }
}
