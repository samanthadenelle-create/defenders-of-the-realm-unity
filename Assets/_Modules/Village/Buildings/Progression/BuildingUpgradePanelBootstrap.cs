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
            // DEV-ONLY shortcut now. The proximity-F open + shared-button request were
            // REMOVED: BuildingInteractable is the single interaction authority and routes
            // resource buildings to the parameterized Yarn hook (DialogueService.PlayStructure).
            // This legacy watcher used to hijack F near farm/lumbermill/forge (mobile priority
            // 10) and open the old multi-building panel, so only Pet House (not a resource
            // building) reached the new hook. U still opens the dev panel for quick balance checks.
            if (Input.GetKeyDown(KeyCode.U) && _panel != null)
                _panel.Toggle();

            MobileInteractButton.Release(this);   // never request the shared button
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }
    }
}
