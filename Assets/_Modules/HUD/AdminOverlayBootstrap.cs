// =============================================================================
// AdminOverlayBootstrap — autospawns AdminOverlay in any scene that has a
// usable UIDocument. The overlay itself stays hidden until either the
// debug chord (Ctrl+Shift+A) fires or the wallet match succeeds.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

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
            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            // A per-scene check let the additive OuterWorld load spawn a second
            // AdminOverlay that intercepted the Dev-tools button.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<AdminOverlay>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate AdminOverlay suppressed (one already exists)");
                    return;
                }
            }
            var go = new GameObject("AdminOverlay");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<AdminOverlay>();
            FlowTrace.Step("UI", "AdminOverlay created (single instance)");
        }
    }
}
