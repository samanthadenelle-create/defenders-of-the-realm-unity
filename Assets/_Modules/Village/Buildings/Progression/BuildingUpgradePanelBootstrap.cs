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

            foreach (var existing in Object.FindObjectsByType<BuildingUpgradePanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
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
        private const float ActivateRadius = 6f;
        private const float ProximityCheckInterval = 0.15f;

        private BuildingUpgradePanel _panel;
        private Transform _hero;
        private float _nextProximityCheck;
        private bool _inRangeOfResourceBuilding;

        private void Awake()
        {
            _panel = GetComponent<BuildingUpgradePanel>();
        }

        private void Update()
        {
            // Dev shortcut — works anywhere in the village.
            if (Input.GetKeyDown(KeyCode.U))
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

            if (_hero == null)
            {
                var hero = Object.FindObjectOfType<HeroLocomotion>();
                if (hero == null) return;
                _hero = hero.transform;
            }

            // Throttle the building scan — it walks every Building each tick, so
            // don't run it per-frame (mirrors the proximity throttle the other
            // structure interactions use).
            if (Time.time >= _nextProximityCheck)
            {
                _nextProximityCheck = Time.time + ProximityCheckInterval;
                _inRangeOfResourceBuilding = HeroNearResourceBuilding();
            }

            // DEF-203: register the shared on-screen Interact button while in range so
            // touch/mobile (no keyboard) can open upgrades too. Desktop F + U unchanged.
            if (_inRangeOfResourceBuilding)
                // DEF-217: priority 10 so on a resource building this richer "Open Upgrades"
                // action wins the single shared button over BuildingInteractable's plain
                // "Interact" (priority 0) instead of the two flickering frame-to-frame.
                MobileInteractButton.Request(this, "Open: Building Upgrades", _panel.Open, 10);
            else
                MobileInteractButton.Release(this);

            if (_inRangeOfResourceBuilding && Input.GetKeyDown(KeyCode.F))
                _panel.Open();
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }

        /// <summary>True when the hero is within range of ANY resource building.</summary>
        private bool HeroNearResourceBuilding()
        {
            if (_hero == null) return false;
            foreach (var b in Object.FindObjectsByType<Building>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (b == null) continue;
                if (!IsResourceBuilding(b)) continue;
                float distSqr = (_hero.position - b.transform.position).sqrMagnitude;
                if (distSqr <= ActivateRadius * ActivateRadius)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when the building is one of the three resource buildings. Matches
        /// by BuildingId first (lumbermill / forge are id-only), then falls back
        /// to BuildingType.Farm (the one resource building that has an enum value).
        /// </summary>
        private static bool IsResourceBuilding(Building b)
        {
            if (!string.IsNullOrEmpty(b.BuildingId) &&
                ResourceBuildingProgression.IsResourceBuilding(b.BuildingId))
                return true;
            return b.Type == BuildingType.Farm;
        }
    }
}
