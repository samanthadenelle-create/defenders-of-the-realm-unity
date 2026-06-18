// =============================================================================
// BuildingUpgradePanelBootstrap — auto-spawns the BuildingUpgradePanel in any
// scene with a hero present. Mirrors VillageCraftingPanelBootstrap exactly.
// WO-151 / DEF-121 (WO-230).
// -----------------------------------------------------------------------------
// Two open paths (same convention as the crafting panel):
//   • U  — anywhere in the village (dev shortcut, always-on while panel exists).
//   • F  — when the hero is within ActivateRadius of a resource building
//          (Farm / Lumbermill / Forge). BuildingInteractable shows its own
//          proximity prompt; we run an INDEPENDENT proximity watcher (we cannot
//          modify BuildingInteractable per the hard rule) and open the panel on
//          the same F press, idempotent.
//
// A resource building is recognised by Building.BuildingId matching one of the
// progression ids (farm / lumbermill / forge). Farm exists as a BuildingType;
// Lumbermill / Forge are id-keyed (no enum value) — so we match on id, not type.
// =============================================================================

using DeNelle.Village;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.Village.Buildings.Progression
{
    public static class BuildingUpgradePanelBootstrap
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

            // FLAG GATE: when the MVVM building-upgrade panel is ON, the code-built
            // BuildingUpgradePanelMvvm owns PanelId.BuildingUpgrade. Suppress this legacy
            // UIDocument panel entirely so the two never double-register the id (last
            // writer wins — gating here avoids the race). Flag OFF -> this panel owns it.
            if (DeNelle.Core.FeatureFlags.BuildingUpgradePanel) return;

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in Object.FindObjectsByType<BuildingUpgradePanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate BuildingUpgradePanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var panelSettings = FindPanelSettings();
            if (panelSettings == null) return;

            var go = new GameObject("BuildingUpgradePanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panelSettings;
            ui.sortingOrder = 121; // just above the crafting panel (120)
            go.AddComponent<BuildingUpgradePanel>();
            go.AddComponent<BuildingUpgradePanelInput>();
            // T-025: the harvest-tick driver that makes the upgrade ladder's
            // speed/size fields actually pay out (consumes HarvestInterval +
            // effective yield → ResourceLedger.Credit). Shares the panel's lifetime.
            go.AddComponent<ResourceBuildingHarvester>();
            FlowTrace.Step("UI", "BuildingUpgradePanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindObjectOfType<HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }

        private static PanelSettings FindPanelSettings()
        {
            // Reuse an existing UIDocument's PanelSettings — same trick as the
            // crafting / daily-quest bootstraps (avoids a Resources.Load path).
            var docs = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }

    /// <summary>
    /// Input watcher for the building-upgrade panel — owns the U toggle and the
    /// resource-building F proximity open. Lives on the same GameObject as the
    /// panel so it shares its lifetime.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class BuildingUpgradePanelInput : MonoBehaviour
    {
        private BuildingUpgradePanel _panel;

        private void Awake()
        {
            _panel = GetComponent<BuildingUpgradePanel>();
        }

        private void Update()
        {
            // Mobile-first: Building Upgrade opens ONLY via its world interactable
            // (resource buildings -> PanelRouter.Open(PanelId.BuildingUpgrade)); the panel
            // is battle-locked by PanelManager. The desktop 'U' key trigger was removed.
            MobileInteractButton.Release(this);   // never request the shared button
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }
    }
}
