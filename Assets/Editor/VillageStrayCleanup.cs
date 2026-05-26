// =============================================================================
// VillageStrayCleanup — removes stray DungeonController objects from the village
// scene. A DungeonController belongs ONLY in a dungeon scene; left in the village
// it re-places the hero (ResolveSpawnPosition) + installs a dungeon camera, which
// fought the village hero/camera. One survived the owner's manual portal cleanup
// on an empty-named GameObject. Run headless:
//   run-unity-method.ps1 -Method DeNelle.Editor.VillageStrayCleanup.RemoveStrayDungeonControllers -LogName cleanup.log
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class VillageStrayCleanup
    {
        private const string VillagePath = "Assets/Scenes/Village.unity";

        [MenuItem("Defenders/Cleanup/Remove Stray DungeonControllers from Village")]
        public static void RemoveStrayDungeonControllers()
        {
            var scene = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);

            // Editor asmdef can't reference DeNelle.Dungeons — resolve by reflection.
            Type dcType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                dcType = asm.GetType("DeNelle.Dungeons.DungeonController", false);
                if (dcType != null) break;
            }
            if (dcType == null)
            {
                Debug.LogError("[VillageStrayCleanup] DungeonController type not found — aborting (scene NOT saved).");
                EditorApplication.Exit(2);
                return;
            }

            var found = UnityEngine.Object.FindObjectsByType(dcType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removed = 0;
            foreach (var o in found)
            {
                var comp = o as Component;
                if (comp == null) continue;
                var go = comp.gameObject;
                Debug.Log($"[VillageStrayCleanup] Removing stray '{go.name}' (DungeonController) " +
                          $"at {go.transform.position}, children={go.transform.childCount}.");
                UnityEngine.Object.DestroyImmediate(go);
                removed++;
            }

            Debug.Log($"[VillageStrayCleanup] Removed {removed} stray DungeonController object(s).");
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene);
                Debug.Log($"[VillageStrayCleanup] Village.unity saved: {ok}.");
            }
            else
            {
                Debug.Log("[VillageStrayCleanup] No stray DungeonControllers found — scene left untouched.");
            }
        }
    }
}
