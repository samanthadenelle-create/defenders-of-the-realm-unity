// =============================================================================
// HelpMenuBootstrap — guarantees a HelpMenu exists in every scene that has a
// UIDocument we can hang off of. Same RuntimeInitializeOnLoadMethod pattern
// as GameStateBootstrap / AudioBootstrap.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    public static class HelpMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded; // idempotent
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SpawnInScene(scene);
        }

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            // Don't double-spawn if a HelpMenu already lives in the scene.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<HelpMenu>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
            }
            var go = new GameObject("HelpMenu");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<HelpMenu>();
        }
    }
}
