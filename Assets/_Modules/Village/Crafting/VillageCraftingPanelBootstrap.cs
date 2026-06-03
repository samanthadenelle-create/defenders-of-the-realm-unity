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

            // One panel per scene — bail if it's already alive.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<VillageCraftingPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
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
        // Same radius as BuildingInteractable's prompt so the F press lines up
        // with the visible "〔 F 〕 Workshop" cue.
        private const float WorkshopActivateRadius = 6f;

        private VillageCraftingPanel _panel;
        private Transform _hero;
        private Building _workshop;

        private void Awake()
        {
            _panel = GetComponent<VillageCraftingPanel>();
        }

        private void Update()
        {
            // Dev shortcut — works anywhere in the village.
            if (Input.GetKeyDown(KeyCode.K))
            {
                if (_panel != null) _panel.Toggle();
                return;
            }

            if (_panel == null || _panel.IsOpen)
            {
                // Panel open (or none yet) — the panel owns its own close UI.
                MobileInteractButton.Release(this);
                return;
            }

            // Resolve refs lazily — they may not exist at scene-load time.
            if (_hero == null)
            {
                var hero = UnityEngine.Object.FindObjectOfType<HeroLocomotion>();
                if (hero == null) return;
                _hero = hero.transform;
            }
            if (_workshop == null)
            {
                foreach (var b in UnityEngine.Object.FindObjectsByType<Building>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (b != null && b.Type == BuildingType.Workshop)
                    {
                        _workshop = b;
                        break;
                    }
                }
                if (_workshop == null) return;
            }

            float distSqr = (_hero.position - _workshop.transform.position).sqrMagnitude;
            bool inRange = distSqr <= WorkshopActivateRadius * WorkshopActivateRadius;

            // DEF-203: register the shared on-screen Interact button while in range so
            // touch/mobile (no keyboard) can open crafting too. Desktop F + K unchanged.
            if (inRange)
                // DEF-217: priority 10 so on the Workshop this richer "Open Crafting" action
                // wins the single shared button over BuildingInteractable's plain "Interact"
                // (priority 0), instead of the two flickering frame-to-frame.
                MobileInteractButton.Request(this, "Open: Workshop Crafting", _panel.Open, 10);
            else
                MobileInteractButton.Release(this);

            if (inRange && Input.GetKeyDown(KeyCode.F))
                _panel.Open();
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }
    }
}
