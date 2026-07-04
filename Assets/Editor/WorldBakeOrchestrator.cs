// =============================================================================
// WorldBakeOrchestrator — runs the world bake sequence in ONE batchmode
// launch. With MergedWorld (WO-608), OuterWorld is deleted; the castle +
// overworld are merged into Main_Castle_Overworld (pre-built). Sequence:
// Village → Exterior → NavMesh (Main_Castle_Overworld is in-git, rebuilt
// via WorldMergeBuilder on-demand by MergedWorldController).
// Run headless: -executeMethod DeNelle.Editor.WorldBakeOrchestrator.BakeFullWorld
// =============================================================================

using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor
{
    public static class WorldBakeOrchestrator
    {
        [MenuItem("Defenders/World/Bake Full World (Village → Exterior → NavMesh)")]
        public static void BakeFullWorld()
        {
            Debug.Log("[WorldBake] === FULL WORLD BAKE START ===");

            Debug.Log("[WorldBake] 1/3 VillageSceneBuilder.BuildVillage");
            VillageSceneBuilder.BuildVillage();

            Debug.Log("[WorldBake] 2/3 ExteriorTerrainBuilder.BuildExterior");
            ExteriorTerrainBuilder.BuildExterior();

            Debug.Log("[WorldBake] 3/3 NavMesh bake (Main_Castle_Overworld pre-built, in-git)");
            Debug.Log("[WorldBake] OuterWorld removed (WO-608 MergedWorld). Use WorldMergeBuilder to rebuild Main_Castle_Overworld if needed.");

            Debug.Log("[WorldBake] === FULL WORLD BAKE COMPLETE ===");
        }
    }
}
