using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    /// <summary>
    /// SURGICAL finalize pass for the Village2 enemy-outpost scene.
    ///
    /// Village2 is a ONE-WAY outpost: the hero ports IN but cannot fast-travel back to
    /// OuterWorld from here (owner directive — "remove option to travel to outerworld").
    /// The scene was hand-authored (cubes / plane / navlink / removed colliders), so we do
    /// NOT rebake or rebuild it. We only:
    ///   1. DELETE every GameObject named exactly "ReturnToOuterWorld_Seam".
    ///   2. DEFENSIVELY delete any SceneTransitionTrigger whose targetSceneName == "OuterWorld"
    ///      (covers a renamed seam), logging each so it's auditable.
    ///   3. LOG the position of "HeroStartPoint_PlayerSpawn" (owner positioned arrival — do NOT move).
    /// Then mark dirty + save the open scene in place.
    ///
    /// Run later in batchmode by the orchestrator (not authored to fire here).
    /// </summary>
    public static class Village2OutpostFinalize
    {
        public const string ScenePath = "Assets/Scenes/Village2.unity";

        private const string SeamName = "ReturnToOuterWorld_Seam";
        private const string SpawnName = "HeroStartPoint_PlayerSpawn";
        private const string Pfx = "[V2Finalize]";

        [MenuItem("Defenders/Village2/Finalize Outpost (remove OuterWorld seam)")]
        public static void Run()
        {
            Debug.Log($"{Pfx} Opening scene '{ScenePath}' (Single)...");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"{Pfx} Scene '{ScenePath}' did not open / is not valid — aborting (nothing changed).");
                return;
            }

            // Collect every transform in the scene (roots + all children, including inactive).
            var allTransforms = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));
            }

            // De-dup the kill list so we never DestroyImmediate the same object twice
            // (a typed-target seam could also carry the canonical name).
            var toRemove = new HashSet<GameObject>();

            // (1) Exact-name match: "ReturnToOuterWorld_Seam".
            int nameMatches = 0;
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                if (t.name == SeamName)
                {
                    var go = t.gameObject;
                    Debug.Log($"{Pfx} FOUND seam by name: path='{FullPath(t)}' pos={go.transform.position}");
                    if (toRemove.Add(go)) nameMatches++;
                }
            }
            if (nameMatches == 0)
                Debug.LogWarning($"{Pfx} No GameObject named '{SeamName}' found (may already be removed).");

            // (2) Defensive type match: any SceneTransitionTrigger -> "OuterWorld" (covers a renamed seam).
            //     DeNelle.Editor cannot reference DeNelle.Village, so we match the component by type
            //     NAME and read its public `targetSceneName` field via reflection.
            int typeMatches = 0;
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                foreach (var comp in t.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    if (comp.GetType().Name != "SceneTransitionTrigger") continue;
                    var field = comp.GetType().GetField("targetSceneName",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var target = field?.GetValue(comp) as string;
                    if (target == "OuterWorld")
                    {
                        var go = comp.gameObject;
                        Debug.Log($"{Pfx} FOUND SceneTransitionTrigger -> 'OuterWorld': path='{FullPath(go.transform)}' " +
                                  $"name='{go.name}' pos={go.transform.position}");
                        if (toRemove.Add(go)) typeMatches++;
                    }
                }
            }
            if (typeMatches == 0)
                Debug.Log($"{Pfx} No additional SceneTransitionTrigger targeting 'OuterWorld' found (beyond name matches).");

            // Destroy the collected set.
            int removed = 0;
            foreach (var go in toRemove)
            {
                if (go == null) continue;
                string path = FullPath(go.transform);
                Object.DestroyImmediate(go);
                removed++;
                Debug.Log($"{Pfx} REMOVED '{path}' (total removed so far: {removed}).");
            }

            // (3) Log the hero spawn position — do NOT move it.
            var spawn = FindByNameInScene(scene, SpawnName);
            if (spawn != null)
                Debug.Log($"{Pfx} '{SpawnName}' present at pos={spawn.transform.position} (left as-is — owner positioned arrival).");
            else
                Debug.LogWarning($"{Pfx} '{SpawnName}' NOT found in scene — hero arrival spawn may be missing.");

            // Save in place. Do NOT rebake / rebuild — surgical edit only.
            EditorSceneManager.MarkAllScenesDirty();
            bool saved = EditorSceneManager.SaveOpenScenes();
            Debug.Log($"{Pfx} SaveOpenScenes -> {(saved ? "SAVED" : "FAILED")}.");

            Debug.Log($"{Pfx} DONE. Removed {removed} object(s) " +
                      $"({nameMatches} by name '{SeamName}', {typeMatches} extra by OuterWorld type-match). " +
                      $"Scene='{ScenePath}'.");
        }

        // Build a full hierarchy path "Root/Child/Leaf" for auditable logging.
        private static string FullPath(Transform t)
        {
            if (t == null) return "(null)";
            var stack = new Stack<string>();
            for (var cur = t; cur != null; cur = cur.parent)
                stack.Push(cur.name);
            return string.Join("/", stack);
        }

        // Find the first GameObject with an exact name across all roots + children (incl. inactive).
        private static GameObject FindByNameInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == name)
                        return t.gameObject;
                }
            }
            return null;
        }
    }
}
