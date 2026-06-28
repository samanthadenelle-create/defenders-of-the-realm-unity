// =============================================================================
// BuildingUpgradePanelMvvmBootstrap — spawns the code-built MVVM upgrade panel
// (BuildingUpgradePanelMvvm) once per gameplay scene, ONLY when the feature flag
// is ON. Mirrors BuildingUpgradePanelBootstrap's lifecycle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// FLAG GATE: the MVVM panel and the legacy UIDocument BuildingUpgradePanel BOTH
// register PanelId.BuildingUpgrade (last writer wins). To avoid a double-register
// race, EXACTLY ONE is spawned:
//   * flag ON  -> this bootstrap spawns the MVVM panel; the legacy bootstrap
//                 short-circuits (it checks the same flag and does NOT spawn).
//   * flag OFF -> this bootstrap does nothing; the legacy UIDocument panel spawns.
//
// The MVVM panel is pure code-built uGUI (it builds its own Canvas on Open), so —
// unlike the UIDocument panel — it needs NO PanelSettings. It just needs a hero in
// the scene (Title / HeroSelect skip), matching the legacy bootstrap's gate.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Buildings.Progression
{
    public static class BuildingUpgradePanelMvvmBootstrap
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
            if (!DeNelle.Core.FeatureFlags.BuildingUpgradePanel) return; // flag OFF -> legacy panel owns the id

            // WO-550: base-building upgrade (town) does NOT bootstrap in enemy-owned RAID scenes
            // (Village2); the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene.
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "BuildingUpgradePanelMvvm suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<BuildingUpgradePanelMvvm>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate BuildingUpgradePanelMvvm suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var go = new GameObject("BuildingUpgradePanelMvvm");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BuildingUpgradePanelMvvm>();
            FlowTrace.Step("UI", "BuildingUpgradePanelMvvm created (single instance, flag ON)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindObjectOfType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
