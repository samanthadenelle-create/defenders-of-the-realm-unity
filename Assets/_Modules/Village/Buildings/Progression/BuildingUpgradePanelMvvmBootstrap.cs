// =============================================================================
// BuildingUpgradePanelMvvmBootstrap — spawns the code-built MVVM enhancement
// (perk-grid) panel BuildingUpgradePanelMvvm once per gameplay scene, when
// FeatureFlags.BuildingUpgradePanel is ON (DEFAULT ON — this is the live panel).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// FLAG GATE (history): the flag used to arbitrate against a legacy UIDocument
// BuildingUpgradePanel twin (both registered PanelId.BuildingUpgrade). The twin
// was DELETED 2026-07-02 (UI Blink conformance audit §3.1 — dead since the flag
// defaulted ON). Flag OFF now means NO building-enhancement panel spawns at all
// (kept only as a kill-switch, not a legacy toggle).
//
// The MVVM panel is pure code-built uGUI (it builds its own Canvas on Open), so
// it needs NO PanelSettings. It just needs a hero in the scene (Title /
// HeroSelect skip).
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
            if (!DeNelle.Core.FeatureFlags.BuildingUpgradePanel) return; // kill-switch (legacy twin deleted 2026-07-02)

            // WO-550: base-building upgrade (town) does NOT bootstrap in enemy-owned RAID scenes
            // (Village2); the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene.
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "BuildingUpgradePanelMvvm suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<BuildingUpgradePanelMvvm>(
                         FindObjectsInactive.Include))
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
            var hero = Object.FindAnyObjectByType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
