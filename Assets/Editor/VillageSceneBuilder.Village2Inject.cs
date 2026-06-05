// =============================================================================
// VillageSceneBuilder.Village2Inject — surgically drops the gameplay/economy
// buildings into the GENERATED Village2 scene (the canonical village).
// -----------------------------------------------------------------------------
// Village2 is the shipping village (generated, regenerable; we pivoted off the
// corruption-cursed hand-built Village.unity — DEF-243). Its structures are good,
// but the KEY gameplay buildings (Pet House / Forge / Farm / Market / Sawmill /
// Armorer / Arcane Tower) were never carried over.
//
// This REUSES the proven PlaceBuilding code + the DEF-101-cleared Buildings[]
// specs (positions already considered), guarded by an IF-NOT-EXISTS check so only
// the MISSING delta is placed. The existing Village2 objects are NEVER re-imported
// — re-importing would re-trigger the FBX Z-axis rotation bug (DEF-254) on the
// already-correct structures (owner's explicit reason for the guard). New plots are
// ground-seated onto Village2's terrain. Nothing already present is touched.
//
//   Defenders > Village2 > Inject Gameplay Buildings
//   (batchmode: DeNelle.Editor.VillageSceneBuilder.InjectGameplayBuildingsIntoVillage2)
//
// NOTE: a NavMesh re-bake is still needed afterward for enemy pathing around the new
// footprints (hero is blocked by the footprint colliders regardless).
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static partial class VillageSceneBuilder
    {
        private const string Village2ScenePath = ScenesDir + "/Village2.unity";

        [MenuItem("Defenders/Village2/Inject Gameplay Buildings")]
        public static void InjectGameplayBuildingsIntoVillage2()
        {
            var scene = EditorSceneManager.OpenScene(Village2ScenePath, OpenSceneMode.Single);

            // VillageController for RegisterBuilding (null-tolerant — placement still works).
            Component controller = null;
            var ctrlType = FindType(TypeVillageController);
            if (ctrlType != null)
                controller = UnityEngine.Object.FindAnyObjectByType(ctrlType, FindObjectsInactive.Include) as Component;

            var parentGo = GameObject.Find("GameplayBuildings");
            if (parentGo == null) parentGo = new GameObject("GameplayBuildings");

            int placed = 0, skipped = 0;
            foreach (var b in Buildings)
            {
                // DELTA GUARD: never re-place an existing building — re-importing would
                // re-trigger the FBX Z-axis rotation bug on the already-correct objects.
                if (Village2HasBuilding(b.Id)) { skipped++; continue; }
                PlaceBuilding(parentGo.transform, b, controller, seatToGround: true);
                placed++;
            }

            // Additive: gives every building lacking one its F-prompt interactable
            // (idempotent; moves/rotates nothing, so existing plots are untouched).
            WireBuildingInteractables();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Village2ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Village2Inject] DONE — placed {placed} new building(s), skipped {skipped} existing. " +
                      "NavMesh re-bake still needed for enemy pathing around them. VILLAGE2_INJECT_OK");
        }

        /// <summary>True if a "Building-{id} (...)" plot already exists in the open scene.</summary>
        private static bool Village2HasBuilding(string id)
        {
            string prefix = "Building-" + id + " ";
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
