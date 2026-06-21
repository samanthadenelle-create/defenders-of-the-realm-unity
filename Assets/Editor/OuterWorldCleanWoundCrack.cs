// One-shot: delete the leftover DistantWoundCrack from the SAVED OuterWorld scene
// (removed at the source in ExteriorTerrainBuilder; this clears the already-baked instance).
// Run: DeNelle.Editor.OuterWorldCleanWoundCrack.Run  (EDITOR CLOSED)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class OuterWorldCleanWoundCrack
    {
        private const string ScenePath = "Assets/Scenes/OuterWorld.unity";

        [MenuItem("Defenders/World/Clean DistantWoundCrack from OuterWorld")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int removed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name == "DistantWoundCrack")
                { Object.DestroyImmediate(t.gameObject); removed++; }
            }
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            Debug.Log($"[CleanWoundCrack] removed {removed} DistantWoundCrack from OuterWorld (saved={removed > 0}).");
        }
    }
}
