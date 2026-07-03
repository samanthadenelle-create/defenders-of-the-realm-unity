// =============================================================================
// LeaderboardPanelBootstrap — auto-spawns a LeaderboardPanel in any gameplay
// scene that has a hero present (Village, Dungeon), never the Title scene.
// Mirrors ClanChatPanelBootstrap. NOTE (WO-563): there is NO 'L' hotkey — the panel
// opens via LeaderboardPanel.Toggle(), surfaced by the touch-friendly SocialAccessCluster.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

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

            // WO-550: social leaderboard does NOT bootstrap in enemy-owned RAID scenes (Village2);
            // the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene (player context).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "LeaderboardPanel suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<LeaderboardPanel>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate LeaderboardPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            // WO-F conversion (2026-07-03): the panel is a kit uGUI modal now — no
            // UIDocument/PanelSettings host needed (the old FindPanelSettings gate is gone).
            var go = new GameObject("LeaderboardPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<LeaderboardPanel>();
            FlowTrace.Step("UI", "LeaderboardPanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = UnityEngine.Object.FindAnyObjectByType(t) as Component;
            return obj != null ? obj.transform : null;
        }
    }
}
