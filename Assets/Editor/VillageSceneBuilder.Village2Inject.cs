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

            int placed = 0, skipped = 0, reseated = 0;
            foreach (var b in Buildings)
            {
                var existing = FindVillage2Building(b.Id);
                if (existing != null)
                {
                    // DELTA GUARD: never re-import an existing building — that would
                    // re-trigger the FBX Z-axis rotation bug. Only correct the Y so it
                    // sits on the ground (Village2 ground is Y=0; a bad earlier ground-
                    // seat floated some plots on top of walls/houses). Position-only =
                    // no re-import, no Z-rotation.
                    var p = existing.transform.position;
                    if (Mathf.Abs(p.y) > 0.05f)
                    {
                        existing.transform.position = new Vector3(p.x, 0f, p.z);
                        reseated++;
                    }
                    else skipped++;
                    continue;
                }
                // New plot: place flat at the spec Y=0 (NO raycast seat — it hit other
                // structures and floated the buildings).
                PlaceBuilding(parentGo.transform, b, controller, seatToGround: false);
                placed++;
            }

            // Additive: gives every building lacking one its F-prompt interactable
            // (idempotent; moves/rotates nothing, so existing plots are untouched).
            WireBuildingInteractables();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Village2ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Village2Inject] DONE — placed {placed} new, reseated {reseated} to ground, " +
                      $"skipped {skipped} already-good. NavMesh re-bake still needed for enemy pathing. " +
                      "VILLAGE2_INJECT_OK");
        }

        /// <summary>The "Building-{id} (...)" plot in the open scene, or null.</summary>
        private static GameObject FindVillage2Building(string id)
        {
            string prefix = "Building-" + id + " ";
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return go;
            return null;
        }
    }
}
