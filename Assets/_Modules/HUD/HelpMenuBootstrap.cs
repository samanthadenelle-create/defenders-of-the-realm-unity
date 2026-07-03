// =============================================================================
// HelpMenuBootstrap — guarantees a HelpMenu exists in every scene that has a
// UIDocument we can hang off of. Same RuntimeInitializeOnLoadMethod pattern
// as GameStateBootstrap / AudioBootstrap.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

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
            // GLOBAL dedupe (across ALL loaded scenes) — not per-scene. The
            // additive OuterWorld load fires sceneLoaded with a new scene, and a
            // per-scene check would miss the live MainCastle_Hall instance and
            // spawn a second full-screen panel that steals pointer events.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<HelpMenu>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate HelpMenu suppressed (one already exists)");
                    return;
                }
            }
            var go = new GameObject("HelpMenu");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<HelpMenu>();
            FlowTrace.Step("UI", "HelpMenu created (single instance)");
        }
    }
}
