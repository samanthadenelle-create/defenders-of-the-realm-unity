// =============================================================================
// JewelerPanelBootstrap — spawns the code-built MVVM jeweler jewelry-crafting panel
// (JewelerPanelMvvm) once per gameplay scene.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors CraftingPanelBootstrap. The panel is pure code-built uGUI (it builds its own
// Canvas on Open) so it needs NO PanelSettings — just a hero in the scene. It
// self-registers PanelId.JewelerCrafting in Awake, so once spawned the Jeweler's Bench
// station can open it via PanelRouter.Open(PanelId.JewelerCrafting).
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Items
{
    public static class JewelerPanelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnInScene(scene);

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (FindHero() == null) return; // Title / HeroSelect skip.

            // Town/economy crafting does NOT bootstrap in enemy-owned RAID scenes (Village2);
            // the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene.
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "JewelerPanelMvvm suppressed in enemy-owned scene");
                return;
            }

            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<JewelerPanelMvvm>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate JewelerPanelMvvm suppressed (one already exists)");
                    return;
                }
            }

            var go = new GameObject("JewelerPanelMvvm");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<JewelerPanelMvvm>();
            FlowTrace.Step("UI", "JewelerPanelMvvm created (single instance)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindAnyObjectByType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
