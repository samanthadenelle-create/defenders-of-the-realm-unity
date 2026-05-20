// =============================================================================
// AdminOverlayBootstrap — autospawns AdminOverlay in any scene that has a
// usable UIDocument. The overlay itself stays hidden until either the
// debug chord (Ctrl+Shift+A) fires or the wallet match succeeds.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    public static class AdminOverlayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => SpawnInScene(scene);

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            foreach (var existing in UnityEngine.Object.FindObjectsByType<AdminOverlay>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
            }
            var go = new GameObject("AdminOverlay");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<AdminOverlay>();
        }
    }
}
