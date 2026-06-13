// =============================================================================
// QuestTrackerHudBootstrap — auto-spawns a QuestTrackerHud in any scene with a
// hero present. Mirrors DailyQuestHudBootstrap so the story-quest tracker only
// shows in the actual play scenes (Village, Dungeon), never Title / HeroSelect.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class QuestTrackerHudBootstrap
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
            // WO-411: no free-floating story-quest tracker on the town hub — quests reach via the
            // TOWN ACTIONS "Quests" button there. Still spawns in combat/dungeon play scenes.
            if (DeNelle.Core.HubScenes.IsHub(scene.name)) return;

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<QuestTrackerHud>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate QuestTrackerHud suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title/HeroSelect skip.

            var panel = FindPanelSettings();
            if (panel == null) return;

            var go = new GameObject("QuestTrackerHud");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 80; // above wave timer / below modals (same band as daily chips)
            go.AddComponent<QuestTrackerHud>();
            FlowTrace.Step("UI", "QuestTrackerHud created (single instance)");
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
            // Pull from an existing UIDocument in the scene so we don't load a
            // Resources asset by name. Mirrors DailyQuestHudBootstrap.
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }
}
