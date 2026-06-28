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
            // WO-564: the harvest-tick driver that makes the upgrade ladder's speed/size
            // fields actually pay out (consumes HarvestInterval + effective yield ->
            // EconomyService/ResourceLedger). The LEGACY bootstrap AddComponent's this, but
            // it short-circuits when ff.buildingupgradepanel is ON (default) — so on the
            // live MVVM path the "upgrade building -> earn more" passive-income loop never
            // ticked. Add it here too. The harvester self-guards a singleton (its Awake
            // destroys a duplicate component) and the global dedupe above guarantees only
            // ONE panel GO is ever spawned, so this can never double-add even if both
            // bootstrap paths somehow ran.
            go.AddComponent<ResourceBuildingHarvester>();
            FlowTrace.Step("UI", "BuildingUpgradePanelMvvm created (single instance, flag ON; harvester attached)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindObjectOfType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
