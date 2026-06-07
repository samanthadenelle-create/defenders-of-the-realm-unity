using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Sandbox;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless batchmode tester for the sandbox CastleBuilder. Resolves prefabs by
    /// name (with fallbacks), builds a castle into a fresh scene, and saves it to
    /// Assets/Scenes/CastleTest.unity. Run via:
    ///   Defenders/Sandbox/Test CastleBuilder
    /// or batchmode -executeMethod DeNelle.Editor.CastleBuilderTester.TestBuildCastle
    /// </summary>
    public static class CastleBuilderTester
    {
        private const string ScenePath = "Assets/Scenes/CastleTest.unity";

        [MenuItem("Defenders/Sandbox/Test CastleBuilder")]
        public static void TestBuildCastle()
        {
            var log = new StringBuilder();

            // 1. Fresh empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Host GameObject at origin + component
            var hostGo = new GameObject("CastleBuilderTest");
            hostGo.transform.position = Vector3.zero;
            var castle = hostGo.AddComponent<CastleBuilder>();

            // 3. Resolve prefabs (primary name first, then fallbacks)
            var resolved = new List<string>();
            var missing = new List<string>();

            castle.floorStonePrefab = Resolve("floorStonePrefab", log, resolved, missing,
                "Floor_Stone_3x3m", "Floor_Stone", "Floor_Medieval", "Floor_Brick", "Floor_");
            castle.wallStonePrefab = Resolve("wallStonePrefab", log, resolved, missing,
                "Wall_Stone_3x3_A", "Wall_Stone_3x3_B", "Wall_Stone", "Wall_Medieval_Stone");
            // Corner: try real corner mesh, else fall back to the straight wall we resolved.
            castle.wallCornerPrefab = Resolve("wallCornerPrefab", log, resolved, missing,
                "Wall_Stone_Corner_A", "Wall_Stone_Corner", "Corner");
            if (castle.wallCornerPrefab == null && castle.wallStonePrefab != null)
            {
                castle.wallCornerPrefab = castle.wallStonePrefab;
                log.AppendLine("  wallCornerPrefab: FALLBACK to wallStonePrefab (no corner mesh).");
                // Move it from missing -> resolved bookkeeping.
                missing.Remove("wallCornerPrefab");
                resolved.Add("wallCornerPrefab (=wallStonePrefab fallback)");
            }
            castle.spiralStairsPrefab = Resolve("spiralStairsPrefab", log, resolved, missing,
                "Stairs_House_Spiral_2m", "Spiral", "Stairs_House", "Stairs");
            castle.towerBasePrefab = Resolve("towerBasePrefab", log, resolved, missing,
                "Tower_Medieval_Big", "Tower_Medieval", "Tower");
            // Ramparts reuse the floor prefab.
            castle.rampartFloorPrefab = castle.floorStonePrefab;
            if (castle.rampartFloorPrefab != null)
                log.AppendLine("  rampartFloorPrefab: reuses floorStonePrefab.");
            else
                log.AppendLine("  rampartFloorPrefab: MISSING (no floor prefab to reuse).");
            castle.gatePrefab = Resolve("gatePrefab", log, resolved, missing,
                "Gate_Medieval_Medium", "Gate_Medieval", "Gate");

            // 4. Build
            castle.BuildCastle();

            // 5. Count spawned children under the castle root
            int childCount = 0;
            string rootName = null;
            if (hostGo.transform.childCount > 0)
            {
                var root = hostGo.transform.GetChild(0);
                rootName = root.name;
                // Total descendants under the root (excludes the root itself).
                childCount = root.GetComponentsInChildren<Transform>(true).Length - 1;
            }

            // 6. Save scene
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);

            // 7. Summary
            log.AppendLine("---- Prefab resolution ----");
            log.AppendLine("RESOLVED: " + (resolved.Count > 0 ? string.Join(", ", resolved) : "(none)"));
            log.AppendLine("MISSING:  " + (missing.Count > 0 ? string.Join(", ", missing) : "(none)"));

            bool ok = saved && childCount > 0 && missing.Count == 0;
            if (ok)
                log.AppendLine($"CASTLE_TEST_OK childCount={childCount} root='{rootName}' scene='{ScenePath}'");
            else
            {
                string reason = !saved ? "scene save failed"
                    : childCount == 0 ? "no children spawned (prefabs unresolved?)"
                    : "missing prefabs: " + string.Join(", ", missing);
                log.AppendLine($"CASTLE_TEST_FAIL {reason} (childCount={childCount})");
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Finds the first prefab whose asset name matches one of the candidate name
        /// fragments (tried in order). Logs the resolved path or marks the field MISSING.
        /// </summary>
        private static GameObject Resolve(string fieldName, StringBuilder log,
            List<string> resolved, List<string> missing, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                var guids = AssetDatabase.FindAssets("t:Prefab " + c);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        log.AppendLine($"  {fieldName}: RESOLVED '{prefab.name}' (matched '{c}') @ {path}");
                        resolved.Add(fieldName);
                        return prefab;
                    }
                }
            }
            log.AppendLine($"  {fieldName}: MISSING (tried: {string.Join(", ", candidates)})");
            missing.Add(fieldName);
            return null;
        }
    }
}
