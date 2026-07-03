// =============================================================================
// CraftingPanelBootstrap — spawns the code-built MVVM consumable-crafting (Alchemy)
// panel (CraftingPanelMvvm) once per gameplay scene.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors HeroSkillTreePanelBootstrap. The panel is pure code-built uGUI (it builds
// its own Canvas on Open) so it needs NO PanelSettings — just a hero in the scene
// (Title / HeroSelect skip). It self-registers PanelId.ConsumableCrafting in Awake,
// so once spawned any interactable / dialogue command can open it via
// PanelRouter.Open(PanelId.ConsumableCrafting).
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Items
{
    public static class CraftingPanelBootstrap
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

            // WO-550: town/economy alchemy crafting does NOT bootstrap in enemy-owned RAID scenes
            // (Village2); the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene.
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "CraftingPanelMvvm suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<CraftingPanelMvvm>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate CraftingPanelMvvm suppressed (one already exists)");
                    return;
                }
            }

            var go = new GameObject("CraftingPanelMvvm");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<CraftingPanelMvvm>();
            FlowTrace.Step("UI", "CraftingPanelMvvm created (single instance)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindAnyObjectByType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
