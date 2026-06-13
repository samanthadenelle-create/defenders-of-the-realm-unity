// =============================================================================
// VillageCraftingPanelBootstrap — auto-spawns the VillageCraftingPanel in any
// scene with a hero present. Mirrors DailyQuestHudBootstrap.
// -----------------------------------------------------------------------------
// Two open paths:
//   • K  — anywhere in the village (dev shortcut, always-on while panel exists).
//   • F  — when the hero is within ActivateRadius of the Workshop building.
//          BuildingInteractable already shows the proximity prompt + its own
//          "Workshop crafting — Week 7" toast. We CANNOT modify it (hard rule),
//          so this bootstrap runs an independent proximity watcher and opens
//          the panel on the same F press, idempotent against multiple presses.
//          The toast is harmless — the panel is the actual interaction.
//
// Single panel instance: only one VillageCraftingPanel ever exists; calling
// Toggle()/Open() on it is idempotent regardless of which trigger fired.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Crafting
{
    public static class VillageCraftingPanelBootstrap
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
            foreach (var existing in UnityEngine.Object.FindObjectsByType<VillageCraftingPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate VillageCraftingPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var panelSettings = FindPanelSettings();
            if (panelSettings == null) return;

            var go = new GameObject("VillageCraftingPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panelSettings;
            ui.sortingOrder = 120; // above HUD chips / below admin overlay
            go.AddComponent<VillageCraftingPanel>();
            go.AddComponent<VillageCraftingPanelInput>();
            FlowTrace.Step("UI", "VillageCraftingPanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var hero = UnityEngine.Object.FindObjectOfType<HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }

        private static PanelSettings FindPanelSettings()
        {
            // Reuse an existing UIDocument's PanelSettings so we don't try to
            // load by Resources path. Same trick as DailyQuestHudBootstrap.
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }

    /// <summary>
    /// Input watcher for the village crafting panel — owns the K toggle and the
    /// Workshop F proximity open. Lives on the same GameObject as the panel so
    /// it shares its lifetime.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class VillageCraftingPanelInput : MonoBehaviour
    {
        private VillageCraftingPanel _panel;

        private void Awake()
        {
            _panel = GetComponent<VillageCraftingPanel>();
        }

        private void Update()
        {
            // DEV-ONLY now. The Workshop proximity-F open + shared-button request were
            // REMOVED — BuildingInteractable is the single interaction authority and routes
            // the Workshop to the parameterized Yarn hook. This watcher used to hijack F at
            // the Workshop (priority 10) and open the Crafting panel (which has no content
            // yet → "can't exit"). K still toggles the dev panel.
            if (Input.GetKeyDown(KeyCode.K) && _panel != null)
                _panel.Toggle();

            MobileInteractButton.Release(this);   // never request the shared button
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }
    }
}
