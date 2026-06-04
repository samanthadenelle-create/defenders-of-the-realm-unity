// =============================================================================
// WorldBakeOrchestrator — runs the FULL, in-order world bake in ONE batchmode
// launch, so the documented bake-order (Village → OuterWorld → Exterior →
// NavMesh) can't be split across processes and skipped (the recurring
// empty-world / can't-walk-out bug comes from a missed BuildExterior).
// -----------------------------------------------------------------------------
// All four builders are public static in DeNelle.Editor, so this is just a
// sequenced call chain with progress logs. Run headless:
//   -executeMethod DeNelle.Editor.WorldBakeOrchestrator.BakeFullWorld
// =============================================================================

using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class WorldBakeOrchestrator
    {
        [MenuItem("Defenders/World/Bake Full World (Village → Outer → Exterior → NavMesh)")]
        public static void BakeFullWorld()
        {
            Debug.Log("[WorldBake] === FULL WORLD BAKE START ===");

            Debug.Log("[WorldBake] 1/4 VillageSceneBuilder.BuildVillage");
            VillageSceneBuilder.BuildVillage();

            Debug.Log("[WorldBake] 2/4 OuterWorldBuilder.BuildOuterWorld");
            OuterWorldBuilder.BuildOuterWorld();

            Debug.Log("[WorldBake] 3/4 ExteriorTerrainBuilder.BuildExterior");
            ExteriorTerrainBuilder.BuildExterior();

            Debug.Log("[WorldBake] 4/4 OuterWorldBuilder.BakeWorldNavMesh");
            OuterWorldBuilder.BakeWorldNavMesh();

            Debug.Log("[WorldBake] === FULL WORLD BAKE COMPLETE ===");
        }
    }
}
