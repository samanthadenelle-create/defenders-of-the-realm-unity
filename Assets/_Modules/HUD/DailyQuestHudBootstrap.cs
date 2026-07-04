// =============================================================================
// DailyQuestHudBootstrap — auto-spawns a DailyQuestHud in any scene with a
// hero present. Mirrors the CompassHudBootstrap pattern so the quest stack
// only shows in the actual play scenes (Village, Dungeon), never Title.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class DailyQuestHudBootstrap
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
            // WO-411: spawns HIDDEN now (DailyQuestHud.Build → display:None); the TOWN ACTIONS "Quests"
            // button toggles it on-demand instead of free-floating top-right.

            // WO-550: town daily-quest HUD does NOT bootstrap in enemy-owned RAID scenes (Village2);
            // the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene (player context).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "DailyQuestHud suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<DailyQuestHud>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate DailyQuestHud suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title/HeroSelect skip.

            // WO-F: DailyQuestHud is now code-built uGUI (its own overlay canvas) — no
            // UIDocument/PanelSettings host needed (mirrors the Leaderboard/HelpMenu
            // host-free bootstraps after their kit conversions).
            var go = new GameObject("DailyQuestHud");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<DailyQuestHud>();
            FlowTrace.Step("UI", "DailyQuestHud created (single instance)");
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
