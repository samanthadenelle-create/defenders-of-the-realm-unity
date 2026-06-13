// =============================================================================
// HeroTalentPanelBootstrap - spawns a HeroTalentPanel UIDocument in every
// scene that has a hero, and watches the legacy input manager for the "T"
// hotkey to toggle the panel visibility.
// -----------------------------------------------------------------------------
// Mirrors DailyQuestHudBootstrap so the talent screen never appears on Title
// / HeroSelect (no hero -> no panel). PanelSettings are borrowed from an
// existing UIDocument in the scene (same trick as HelpMenu, avoids
// hard-referencing a Resources asset).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class HeroTalentPanelBootstrap
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
            foreach (var existing in Object.FindObjectsByType<HeroTalentPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate HeroTalentPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var panelSettings = FindPanelSettings();
            if (panelSettings == null) return;

            var go = new GameObject("HeroTalentPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.sortingOrder = 110; // above HUD, above HelpMenu (100)
            go.AddComponent<HeroTalentPanel>();
            go.AddComponent<HeroTalentHotkey>();
            FlowTrace.Step("UI", "HeroTalentPanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = Object.FindObjectOfType(t) as Component;
            return obj != null ? obj.transform : null;
        }

        private static PanelSettings FindPanelSettings()
        {
            var docs = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }

    /// <summary>
    /// Lightweight legacy-input listener that toggles its sibling
    /// HeroTalentPanel when the T key is pressed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroTalentHotkey : MonoBehaviour
    {
        private HeroTalentPanel _panel;

        private void Awake()
        {
            _panel = GetComponent<HeroTalentPanel>();
        }

        private void Update()
        {
            // Legacy input manager so DeNelle.HUD does not need a reference
            // to Unity.InputSystem (AdminOverlay uses the same approach).
            if (_panel != null && Input.GetKeyDown(KeyCode.T))
            {
                _panel.Toggle();
            }
        }
    }
}
