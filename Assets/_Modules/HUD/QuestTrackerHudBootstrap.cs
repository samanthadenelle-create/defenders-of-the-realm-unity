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
            // Owner 2026-06-20: the hub used to suppress this on the promise the "Quests" button
            // surfaced quests — but that button now opens the MODAL Rumor Board, leaving the hub with
            // no persistent on-screen tracker. Spawn it wherever a hero exists; it pins the ONE current
            // active quest far-right (the board pop-up remains the full browse/accept list).

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

            // CANON-CORRECT (CLAUDE.md §8): QuestTrackerHud is now code-built uGUI — it builds its
            // OWN ScreenSpaceOverlay Canvas, so NO UIDocument/PanelSettings is needed. The prior
            // UIDocument version did not render (trace: active=False / hasRoot=False).
            var go = new GameObject("QuestTrackerHud");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<QuestTrackerHud>();
            FlowTrace.Step("UI", "QuestTrackerHud created (uGUI, single instance)");
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
