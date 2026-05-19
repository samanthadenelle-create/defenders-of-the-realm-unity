// =============================================================================
// GrantPolishBuilder — one-shot orchestrator for the 2026-05-19 grant-polish
// feature pass. Runs every scene builder touched by the pass, in dependency
// order, so the whole pass rebuilds in a single batchmode session:
//
//   Unity.exe -batchmode -quit -projectPath <proj> \
//             -executeMethod DeNelle.Editor.GrantPolishBuilder.BuildAll
//
// Order matters: the village must rebuild BEFORE the wall-repair wiring is
// re-applied (a full village rebuild clears VillageRoot, so the repair
// controller must be re-added afterwards).
// =============================================================================

using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    /// <summary>
    /// Rebuilds, in order, every scene affected by the grant-polish pass:
    /// village (townsfolk + camera) → wall-repair wiring → intro flow
    /// (hero/pet select) → dungeon (crafting + oil HUD) → battle (camera).
    /// </summary>
    public static class GrantPolishBuilder
    {
        [MenuItem("Defenders/Build All (Grant-Polish Pass)")]
        public static void BuildAll()
        {
            Debug.Log("[GrantPolish] 1/5 Village (townsfolk + camera) ...");
            VillageSceneBuilder.BuildVillage();

            Debug.Log("[GrantPolish] 2/5 Wall-repair wiring ...");
            WallRepairSceneSetup.AddWallRepairToVillage();

            Debug.Log("[GrantPolish] 3/5 Intro flow (hero + pet select) ...");
            IntroFlowSceneBuilder.BuildAll();

            Debug.Log("[GrantPolish] 4/5 Dungeon (crafting + oil HUD) ...");
            DungeonSceneBuilder.BuildHealersCottage();

            Debug.Log("[GrantPolish] 5/5 Battle (camera) ...");
            BattleSceneBuilder.BuildBattleScene();

            Debug.Log("[GrantPolish] DONE - all 5 builders complete.");
        }
    }
}
