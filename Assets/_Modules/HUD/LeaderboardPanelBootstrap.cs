// =============================================================================
// LeaderboardPanelBootstrap — auto-spawns a LeaderboardPanel in any gameplay
// scene that has a hero present (Village, Dungeon), never the Title scene.
// Mirrors ClanChatPanelBootstrap so the leaderboard hotkey (L) only lights up
// where it makes sense.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    public static class LeaderboardPanelBootstrap
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

            foreach (var existing in UnityEngine.Object.FindObjectsByType<LeaderboardPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var panel = FindPanelSettings();
            if (panel == null) return;

            var go = new GameObject("LeaderboardPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 86; // just above ClanChatPanel (85), below modals
            go.AddComponent<LeaderboardPanel>();
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
