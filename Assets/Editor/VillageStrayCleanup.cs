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

        // Replaces the Tree-of-Life's oversized (canopy) collider with a small
        // trunk capsule so the hero can walk the plaza. Enemies hit the Heart by
        // proximity (not a collider), so a slim trunk blocker is all that's needed.
        // Sizes the capsule to ~2 m radius / 8 m height in WORLD space (divided by
        // the object's lossyScale) so it's correct whatever scale the tree has.
        [MenuItem("Defenders/Cleanup/Fix Tree-of-Life Collider")]
        public static void FixTreeOfLifeCollider()
        {
            var scene = EditorSceneManager.OpenScene(VillagePath, OpenSceneMode.Single);

            Type heartType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                heartType = asm.GetType("DeNelle.Village.HeartController", false);
                if (heartType != null) break;
            }
            if (heartType == null)
            {
                Debug.LogError("[TreeCollider] HeartController type not found — aborting (scene NOT saved).");
                EditorApplication.Exit(2);
                return;
            }
            var heart = UnityEngine.Object.FindAnyObjectByType(heartType) as Component;
            if (heart == null)
            {
                Debug.LogError("[TreeCollider] No HeartController in the village — aborting.");
                EditorApplication.Exit(2);
                return;
            }
            var root = heart.gameObject;

            int removed = 0;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                Debug.Log($"[TreeCollider] Removing {c.GetType().Name} on '{c.gameObject.name}'.");
                UnityEngine.Object.DestroyImmediate(c);
                removed++;
            }

            float s = Mathf.Max(0.01f, root.transform.lossyScale.x);
            var cap = root.AddComponent<CapsuleCollider>();
            cap.direction = 1;                       // Y-axis (upright trunk)
            cap.radius = 2.0f / s;
            cap.height = 8.0f / s;
            cap.center = new Vector3(0f, (8.0f / s) * 0.5f, 0f);  // feet at root origin
            Debug.Log($"[TreeCollider] root='{root.name}' lossyScale={root.transform.lossyScale} — removed " +
                      $"{removed} collider(s); added trunk CapsuleCollider (world ~2 m x 8 m; local r={cap.radius:F2} h={cap.height:F2}).");

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TreeCollider] Village.unity saved: {ok}.");
        }
    }
}
