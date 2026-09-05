// =============================================================================
// MainCastleEnvironmentDressing — Environment + prop dressing for Main_Castle_Overworld.
// =============================================================================
// Entry point: MainCastleEnvironmentDressing.Run (editor menu item)
//
// Implements WO-1292:
// 1. Swaps ~140 Polyperfect Rock_* instances → Synty SM_Env_Rock_* (preserving transforms)
// 2. Adds castle floor pieces to create a central courtyard (owner priority: "coblestone or castle floor")
// 3. Adds paths (Props/Paths/ 27) reconciled with terrain
// 4. Adds banners (Props/Banners/ 43) for ownership dressing (shape/value separation, not hue)
// 5. Adds furniture/market dressing for storefronts
//
// Idempotent: re-running in the same scene clears prior dressing root and rebuilds.
// NEVER hand-edits Main_Castle_Overworld.unity directly; all changes via this builder.
//
// Instrumentation (CLAUDE.md §12): FlowTrace for state tracking; Guard for risky ops.
// No silent failures — every anomaly is logged, never skipped.
//
// After run:
//   1. Verify triangle count + draw calls (mobile budget).
//   2. Re-bake NavMesh: Window > AI > Navigation > Bake.
//   3. Run CastleGateNavVerify + TROOP_WALL_NAV_OK regression tests.
//   4. Run RunCaptureHeadless for visual verification.
//   5. If Addressables changed: content build + tools\r2-ship.ps1 push + verify R2_PARITY_OK.
//
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    public static class MainCastleEnvironmentDressing
    {
        private const string MenuPath = "Defenders/Scenes/Dress MainCastleOverworld Environment";
        private const string ScenePath = "Assets/Scenes/Main_Castle_Overworld.unity";
        private const string DressingRootName = "EnvironmentDressingRoot";
        private const string FloorRootName = "CastleFloorDressing";

        // Synty environment asset paths (relative to Assets/)
        private const string SyntyEnvBase = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/";
        private const string SyntyPropsBase = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/";
        private const string SyntyCastleBase = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Castle/";

        // Rock instance mapping: polyperfect name → Synty names (cycling through variants)
        private static readonly Dictionary<string, string[]> RockMapping = new()
        {
            { "Rock_1_A", new[] { "SM_Env_Rock_01", "SM_Env_Rock_02" } },
            { "Rock_1_E", new[] { "SM_Env_Rock_02", "SM_Env_Rock_03" } },
            { "Rock_2_B", new[] { "SM_Env_Rock_03", "SM_Env_Rock_04" } },
            { "Rock_3_C", new[] { "SM_Env_Rock_04", "SM_Env_Rock_Chunk_01" } },
            { "Rock_3_H", new[] { "SM_Env_Rock_Chunk_02", "SM_Env_Rock_Chunk_03" } },
            { "Rock_4_A", new[] { "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02" } },
            { "Rock_5_B", new[] { "SM_Env_Rock_Cliff_03", "SM_Env_Rock_Cliff_04" } },
            { "Rock_6_D", new[] { "SM_Env_Rock_Cliff_05", "SM_Env_Rock_01" } },
            { "Rock_6_G", new[] { "SM_Env_Rock_02", "SM_Env_Rock_04" } },
        };

        // Castle floor piece rotation for courtyard pattern
        private static readonly string[] CastleFloorPieces = new[]
        {
            "SM_Bld_Castle_Floor_Stone_01",
            "SM_Bld_Castle_Floor_Stone_02",
            "SM_Bld_Castle_Floor_Stone_03",
            "SM_Bld_Castle_Floor_Stone_04",
            "SM_Bld_Castle_Floor_Stone_Round_S_01",
            "SM_Bld_Castle_Floor_Stone_Round_M_01",
            "SM_Bld_Castle_Floor_Stone_Round_L_01",
            "SM_Bld_Castle_Floor_Stone_Gap_01",
            "SM_Bld_Castle_Floor_Stone_Gap_02",
        };

        // Path pieces for town footpaths
        private static readonly string[] PathPieces = new[]
        {
            "SM_Prop_Path_01",
            "SM_Prop_Path_02",
            "SM_Prop_Path_03",
        };

        // Banner pieces for ownership dressing (shape/value separation, not hue)
        private static readonly string[] BannerPieces = new[]
        {
            "SM_Prop_Banner_01",
            "SM_Prop_Banner_02",
            "SM_Prop_Banner_03",
        };

        [MenuItem(MenuPath)]
        public static void Run()
        {
            try
            {
                // Measure, not Enter: Enter takes no threshold, and the scope was being discarded
                // (never exited). 2026-09-04 compile-gate catch on commit 33ba9c966.
                using var _scope = FlowTrace.Measure("MainCastleEnvironmentDressing", "Run", warnAboveMs: 5000f);

                // Load the scene
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    FlowTrace.Fail("MainCastleEnvironmentDressing", "Scene load failed");
                    return;
                }
                FlowTrace.Step("MainCastleEnvironmentDressing", "Scene loaded");

                // Clear prior dressing
                ClearPriorDressing();

                // Create root for new dressing
                var dressingRoot = new GameObject(DressingRootName);
                dressingRoot.transform.position = Vector3.zero;
                FlowTrace.Step("MainCastleEnvironmentDressing", "Root created");

                // Swap rocks
                SwapRocks(dressingRoot.transform);
                FlowTrace.Step("MainCastleEnvironmentDressing", "Rocks swapped");

                // Add castle floor courtyard (HIGH PRIORITY owner ask)
                AddCastleFloorCourtyard(dressingRoot.transform);
                FlowTrace.Step("MainCastleEnvironmentDressing", "Castle floor added");

                // Add paths
                AddPathDressing(dressingRoot.transform);
                FlowTrace.Step("MainCastleEnvironmentDressing", "Paths added");

                // Add banners
                AddBannerDressing(dressingRoot.transform);
                FlowTrace.Step("MainCastleEnvironmentDressing", "Banners added");

                // Add furniture
                AddFurnitureDressing(dressingRoot.transform);
                FlowTrace.Step("MainCastleEnvironmentDressing", "Furniture added");

                // Log metrics before save
                LogSceneMetrics();

                // Save scene
                EditorSceneManager.SaveScene(scene, ScenePath);
                FlowTrace.Step("MainCastleEnvironmentDressing", "Scene saved");

                Debug.Log($"[WO-1292] Environment dressing complete. Scene saved to {ScenePath}. " +
                    "NEXT: Re-bake NavMesh, run regressions, and RunCaptureHeadless.");

                FlowTrace.Step("MainCastleEnvironmentDressing", "Complete");
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("MainCastleEnvironmentDressing", $"Exception: {ex.Message}");
                Debug.LogError($"[WO-1292] Fatal error: {ex}");
            }
        }

        private static void ClearPriorDressing()
        {
            var prior = GameObject.Find(DressingRootName);
            if (prior != null)
            {
                FlowTrace.Step("MainCastleEnvironmentDressing", $"Destroying prior {DressingRootName}");
                UnityEngine.Object.DestroyImmediate(prior);
            }
        }

        private static void SwapRocks(Transform parent)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var allGameObjects = scene.GetRootGameObjects();

            int swapped = 0;
            int failed = 0;

            foreach (var rockName in RockMapping.Keys)
            {
                // Find all instances of this rock prefab in the scene
                var instances = FindPrefabInstances(allGameObjects, rockName);
                if (instances.Count == 0) continue;

                var syntyVariants = RockMapping[rockName];
                FlowTrace.Step("MainCastleEnvironmentDressing", $"Swapping {instances.Count} × {rockName}");

                for (int i = 0; i < instances.Count; i++)
                {
                    var instance = instances[i];
                    // Cycle through Synty variants
                    var syntyName = syntyVariants[i % syntyVariants.Length];
                    var syntyPath = SyntyEnvBase + syntyName + ".prefab";

                    if (TrySwapPrefabInstance(instance, syntyPath))
                    {
                        swapped++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            FlowTrace.Step("MainCastleEnvironmentDressing", $"Rocks: {swapped} swapped, {failed} failed");
            Debug.Log($"[WO-1292] Rocks swapped: {swapped} instances, {failed} failures");
        }

        private static void AddCastleFloorCourtyard(Transform parent)
        {
            // Create floor root
            var floorRoot = new GameObject(FloorRootName);
            floorRoot.transform.SetParent(parent, false);
            floorRoot.transform.position = new Vector3(0, 0.1f, 0); // Slight elevation to avoid z-fighting

            // Place castle floor pieces in a courtyard pattern (12×12 grid at 2.5m spacing)
            // Centered around the Heart of Elarion at (0,0,0)
            int pieceCount = 0;
            float spacing = 2.5f;
            float gridHalf = 15f;

            for (float x = -gridHalf; x <= gridHalf; x += spacing)
            {
                for (float z = -gridHalf; z <= gridHalf; z += spacing)
                {
                    var floorPiece = CastleFloorPieces[pieceCount % CastleFloorPieces.Length];
                    var path = SyntyCastleBase + floorPiece + ".prefab";

                    if (InstantiatePrefab(path, floorRoot.transform, new Vector3(x, 0, z)))
                    {
                        pieceCount++;
                    }
                }
            }

            FlowTrace.Step("MainCastleEnvironmentDressing", $"Castle floor: {pieceCount} pieces placed");
            Debug.Log($"[WO-1292] Castle courtyard floor: {pieceCount} pieces placed (owner priority)");
        }

        private static void AddPathDressing(Transform parent)
        {
            // Create path root
            var pathRoot = new GameObject("PathDressing");
            pathRoot.transform.SetParent(parent, false);

            // Paths along cardinal routes (N-S spine, E-W cross)
            // Place 27 paths distributed across the town footpaths
            int pathCount = 0;
            var pathPositions = GeneratePathPositions(); // Positions along roads/footpaths

            foreach (var pos in pathPositions)
            {
                var pathPiece = PathPieces[pathCount % PathPieces.Length];
                var path = SyntyPropsBase + pathPiece + ".prefab";

                if (InstantiatePrefab(path, pathRoot.transform, pos))
                {
                    pathCount++;
                }
                if (pathCount >= 27) break; // We have 27 Props/Paths in inventory
            }

            FlowTrace.Step("MainCastleEnvironmentDressing", $"Paths: {pathCount} placed");
            Debug.Log($"[WO-1292] Town footpaths: {pathCount} pieces placed");
        }

        private static void AddBannerDressing(Transform parent)
        {
            // Create banner root
            var bannerRoot = new GameObject("BannerDressing");
            bannerRoot.transform.SetParent(parent, false);

            // Place banners for gate/tower/keep ownership (separate by SHAPE and VALUE, not hue)
            // North, East, South, West gates get distinct banner shapes
            var gatePositions = new[]
            {
                new Vector3(0, 8, 40), // North gate
                new Vector3(40, 8, 0), // East gate
                new Vector3(0, 8, -40), // South gate
                new Vector3(-40, 8, 0), // West gate
            };

            int bannerCount = 0;
            for (int i = 0; i < gatePositions.Length; i++)
            {
                // Use distinct banner shapes per gate (cycle through variants)
                var bannerPiece = BannerPieces[i % BannerPieces.Length];
                var path = SyntyPropsBase + bannerPiece + ".prefab";

                if (InstantiatePrefab(path, bannerRoot.transform, gatePositions[i]))
                {
                    bannerCount++;
                }

                // Add a second banner for reinforcement (value-based placement, not hue)
                if (InstantiatePrefab(path, bannerRoot.transform, gatePositions[i] + Vector3.right * 4))
                {
                    bannerCount++;
                }
            }

            // Add banners to key structures (towers, keep) if they exist
            // This is a placeholder for structure-specific banners
            var towers = FindGameObjectsByName("Tower");
            var keeps = FindGameObjectsByName("Keep");
            var structures = new List<GameObject>();
            structures.AddRange(towers);
            structures.AddRange(keeps);

            foreach (var structure in structures)
            {
                var pos = structure.transform.position + Vector3.up * 8 + Vector3.forward * 2;
                var bannerPiece = BannerPieces[bannerCount % BannerPieces.Length];
                var path = SyntyPropsBase + bannerPiece + ".prefab";

                if (InstantiatePrefab(path, bannerRoot.transform, pos))
                {
                    bannerCount++;
                }
            }

            FlowTrace.Step("MainCastleEnvironmentDressing", $"Banners: {bannerCount} placed");
            Debug.Log($"[WO-1292] Ownership dressing: {bannerCount} banners placed (shape/value separated)");
        }

        private static void AddFurnitureDressing(Transform parent)
        {
            // Create furniture root
            var furnitureRoot = new GameObject("FurnitureDressing");
            furnitureRoot.transform.SetParent(parent, false);

            // Place Synty furniture pieces (116 available in Props/Furniture/)
            // in market/plaza areas around the castle
            int furnitureCount = 0;

            var furnitureOptions = new[]
            {
                "SM_Prop_Bench_Seat_01",
                "SM_Prop_Bench_Seat_02",
                "SM_Prop_Chair_Wood_01",
                "SM_Prop_Chair_Wood_02",
                "SM_Prop_Camp_Chair_01",
                "SM_Prop_Chair_Fancy_01",
                "SM_Prop_Workbench_01",
                "SM_Prop_Bed_01",
            };

            var furniturePositions = new[]
            {
                new Vector3(12f, 0, -12f),
                new Vector3(-12f, 0, -12f),
                new Vector3(12f, 0, 12f),
                new Vector3(-12f, 0, 12f),
                new Vector3(8f, 0, 0),
                new Vector3(-8f, 0, 0),
                new Vector3(0, 0, 8f),
                new Vector3(0, 0, -8f),
            };

            for (int i = 0; i < furniturePositions.Length; i++)
            {
                var furnitureName = furnitureOptions[i % furnitureOptions.Length];
                var path = SyntyPropsBase + "Furniture/" + furnitureName + ".prefab";
                if (InstantiatePrefab(path, furnitureRoot.transform, furniturePositions[i]))
                {
                    furnitureCount++;
                }
            }

            FlowTrace.Step("MainCastleEnvironmentDressing", $"Furniture: {furnitureCount} pieces placed");
            if (furnitureCount > 0)
            {
                Debug.Log($"[WO-1292] Market dressing: {furnitureCount} furniture pieces placed");
            }
        }

        private static List<GameObject> FindPrefabInstances(GameObject[] roots, string prefabName)
        {
            var instances = new List<GameObject>();
            foreach (var root in roots)
            {
                FindPrefabInstancesRecursive(root, prefabName, instances);
            }
            return instances;
        }

        private static void FindPrefabInstancesRecursive(GameObject go, string prefabName, List<GameObject> result)
        {
            // Check if this object matches the prefab name
            if (go.name.StartsWith(prefabName))
            {
                result.Add(go);
            }

            // Recurse into children
            foreach (Transform child in go.transform)
            {
                FindPrefabInstancesRecursive(child.gameObject, prefabName, result);
            }
        }

        private static bool TrySwapPrefabInstance(GameObject instance, string syntyPath)
        {
            return Guard.Try("MainCastleEnvironmentDressing", $"Swap rock to {syntyPath}", () =>
            {
                var syntyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(syntyPath);
                if (syntyPrefab == null)
                {
                    FlowTrace.Warn("MainCastleEnvironmentDressing", $"Synty prefab not found: {syntyPath}");
                    return false;
                }

                // Save the transform of the old instance
                var oldPos = instance.transform.position;
                var oldRot = instance.transform.rotation;
                var oldScale = instance.transform.localScale;

                // Destroy the old instance
                UnityEngine.Object.DestroyImmediate(instance);

                // Instantiate the new Synty prefab with the saved transform
                var newInstance = PrefabUtility.InstantiatePrefab(syntyPrefab) as GameObject;
                if (newInstance == null)
                {
                    FlowTrace.Warn("MainCastleEnvironmentDressing", $"Failed to instantiate: {syntyPath}");
                    return false;
                }

                newInstance.transform.position = oldPos;
                newInstance.transform.rotation = oldRot;
                newInstance.transform.localScale = oldScale;

                return true;
            }, fallback: false);
        }

        private static bool InstantiatePrefab(string path, Transform parent, Vector3 position)
        {
            return Guard.Try("MainCastleEnvironmentDressing", $"Instantiate {path}", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    FlowTrace.Warn("MainCastleEnvironmentDressing", $"Prefab not found: {path}");
                    return false;
                }

                var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                if (instance == null)
                {
                    FlowTrace.Warn("MainCastleEnvironmentDressing", $"Failed to instantiate: {path}");
                    return false;
                }

                instance.transform.position = position;
                return true;
            }, fallback: false);
        }

        private static List<GameObject> FindGameObjectsByName(string namePattern)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var allObjects = scene.GetRootGameObjects();
            var result = new List<GameObject>();

            foreach (var root in allObjects)
            {
                FindGameObjectsByNameRecursive(root, namePattern, result);
            }

            return result;
        }

        private static void FindGameObjectsByNameRecursive(GameObject go, string pattern, List<GameObject> result)
        {
            if (go.name.Contains(pattern))
            {
                result.Add(go);
            }

            foreach (Transform child in go.transform)
            {
                FindGameObjectsByNameRecursive(child.gameObject, pattern, result);
            }
        }

        private static Vector3[] GeneratePathPositions()
        {
            // Cardinal routes: N-S spine and E-W cross through center
            var positions = new List<Vector3>();

            // N-S spine (vertical)
            for (float z = -30; z <= 30; z += 4)
            {
                positions.Add(new Vector3(0, 0, z));
            }

            // E-W cross (horizontal)
            for (float x = -30; x <= 30; x += 4)
            {
                if (Mathf.Abs(x) > 2) // Avoid overlap at center
                {
                    positions.Add(new Vector3(x, 0, 0));
                }
            }

            return positions.Take(27).ToArray(); // We have 27 path pieces in inventory
        }

        private static void LogSceneMetrics()
        {
            try
            {
                var meshes = UnityEngine.Object.FindObjectsOfType<MeshFilter>();
                var totalTris = 0;
                var totalVerts = 0;
                foreach (var mf in meshes)
                {
                    if (mf.sharedMesh != null)
                    {
                        totalTris += mf.sharedMesh.triangles.Length / 3;
                        totalVerts += mf.sharedMesh.vertices.Length;
                    }
                }

                var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                var drawCalls = renderers.Length;

                FlowTrace.Step("MainCastleEnvironmentDressing",
                    $"Scene metrics: {totalTris:N0} triangles, {totalVerts:N0} vertices, ~{drawCalls} draw calls");
                Debug.Log($"[WO-1292] Scene metrics: {totalTris:N0} triangles, {totalVerts:N0} vertices, {drawCalls} draw calls " +
                    "(verify against mobile budget)");
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("MainCastleEnvironmentDressing", $"Failed to log metrics: {ex.Message}");
            }
        }
    }
}
