// =============================================================================
// ClanChatPanelBootstrap — auto-spawns a ClanChatPanel in any gameplay scene
// that has a hero present. Mirrors LeaderboardPanelBootstrap so the social chat
// only lights up in the gameplay scenes (Village, Dungeon), never Title.
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03): ClanChatPanel is now a code-built uGUI Obsidian
// modal that builds its OWN ScreenSpaceOverlay canvas lazily on first open — it no
// longer needs a UIDocument / PanelSettings. The bootstrap just spawns the bare
// component; opening is driven by the kit HUD chat dock (HudKitController).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Services;

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
            if (!ClanFeatureGate.PlayerFacingEnabled) return;
            if (!scene.IsValid()) return;

            // WO-550: social panels do NOT bootstrap in enemy-owned RAID scenes (Village2); the
            // home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene (player context).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "ClanChatPanel suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<ClanChatPanel>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate ClanChatPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var go = new GameObject("ClanChatPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<ClanChatPanel>();
            FlowTrace.Step("UI", "ClanChatPanel created (single instance, code-built kit modal)");
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
