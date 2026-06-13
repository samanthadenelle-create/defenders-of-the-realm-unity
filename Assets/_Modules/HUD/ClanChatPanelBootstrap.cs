// =============================================================================
// ClanChatPanelBootstrap — auto-spawns a ClanChatPanel in any scene that has
// a hero present. Mirrors DailyQuestHudBootstrap so the chat hotkey (Y) only
// lights up in the gameplay scenes (Village, Dungeon), never Title.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class ClanChatPanelBootstrap
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
            foreach (var existing in UnityEngine.Object.FindObjectsByType<ClanChatPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate ClanChatPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var panel = FindPanelSettings();
            if (panel == null) return;

            var go = new GameObject("ClanChatPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 85; // above the daily-quest stack (80), below modals
            go.AddComponent<ClanChatPanel>();
            FlowTrace.Step("UI", "ClanChatPanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = UnityEngine.Object.FindObjectOfType(t) as Component;
            return obj != null ? obj.transform : null;
        }

        private static PanelSettings FindPanelSettings()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }
}
