// =============================================================================
// MissingPrefabInstanceCleaner — strip PrefabInstances whose source asset no
// longer exists from a saved scene, WITHOUT hand-editing the YAML (CLAUDE.md §3).
//
// PROVEN ROOT (F8 2026-07-02 20:57:31 "Missing Prefab Asset: 'HeroBody (Missing
// Prefab with guid: be1690ec95b9d1445aaa4a0024c41370)'" + read-only RCA):
// MainCastle_Hall.unity holds a 'HeroBody' PrefabInstance sourced from
// Assets/Resources/Heroes/Mage.fbx, which the 07-01 size-cut commit 0cec81a7
// intentionally DELETED — leaving a dangling reference that trips "Problem
// detected while opening the Scene file" on every open. Runtime is unaffected
// (HeroBodySwapper.Start destroys the 'HeroBody' placeholder and rebuilds the
// real body), so the stale instance is pure debris. Restoring Mage.fbx would
// reverse the deliberate size cut — removal is the right fix.
//
// Generic on purpose: removes EVERY missing-source prefab instance in the scene
// (outermost roots only), so future asset deletions can't leave the same debris.
// Idempotent: a clean scene is a no-op.
//
// Run (EDITOR CLOSED, batchmode):
//   -executeMethod DeNelle.Editor.MissingPrefabInstanceCleaner.CleanMainCastle
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class MissingPrefabInstanceCleaner
    {
        private const string MainCastlePath = "Assets/Scenes/MainCastle_Hall.unity";

        [MenuItem("Defenders/Castle/Remove Missing-Prefab Instances (MainCastle_Hall)")]
        public static void CleanMainCastle() => Clean(MainCastlePath);

        public static void Clean(string scenePath)
        {
            Log("=== missing-prefab cleanup START: " + scenePath + " ===");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int removed = CleanOpenScene(scene);

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.SaveAssets();
                Log($"=== cleanup DONE — removed {removed} instance(s), saved={saved} ===");
            }
            else Log("=== cleanup DONE — scene already clean, nothing removed ===");
        }

        /// <summary>
        /// Remove every missing-source prefab instance from an ALREADY-OPEN scene (no open/save —
        /// the caller owns scene lifecycle). Called by CastleHubBuilder.BatchRebuildCastleFromRecipeAndBake
        /// so every castle rebuild produces a clean scene (WO-593 F8: the stale 'HeroBody' instance,
        /// guid be1690ec95b9d1445aaa4a0024c41370 = the size-cut-deleted Mage.fbx, tripped
        /// "Missing Prefab Asset" on every open). Returns the number of instances removed.
        /// </summary>
        public static int CleanOpenScene(UnityEngine.SceneManagement.Scene scene)
        {
            // Collect outermost missing-asset prefab instance roots first (destroying
            // while iterating GetComponentsInChildren invalidates the walk).
            var doomed = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.MissingAsset) continue;
                    var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go;
                    if (!doomed.Contains(outer)) doomed.Add(outer);
                }
            }

            foreach (var go in doomed)
            {
                Log($"removing missing-source prefab instance '{go.name}' (parent '{(go.transform.parent != null ? go.transform.parent.name : "<scene root>")}', pos {go.transform.position})");
                Object.DestroyImmediate(go);
            }
            return doomed.Count;
        }

        private static void Log(string m) => Debug.Log("[MissingPrefabInstanceCleaner] " + m);
    }
}
