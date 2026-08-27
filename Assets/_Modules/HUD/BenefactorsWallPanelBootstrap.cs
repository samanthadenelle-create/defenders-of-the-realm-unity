// =============================================================================
// BenefactorsWallPanelBootstrap - WO-1073, spawns the one BenefactorsWallPanel.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// Mirrors LeaderboardPanelBootstrap exactly. The panel registers
// PanelId.Benefactors in its own Awake, so the Founders Monument's door has
// something to open the moment the hub is up.
//
// GATED TO HUB SCENES. The monument is hub furniture placed beside the Heart, so
// there is no door to this panel anywhere else - spawning it in a dungeon or a
// raid would be a registered id nothing can reach.
//
// ⛔ NO HOTKEY, NO ACTION-BAR FACE, NO MENU ITEM. Owner ruling 2026-08-27(c):
// "walking up to the monument and reading the names is the moment; a menu item
// is not." Adding a second entry point is a design change, not a convenience.
//
// ASCII only. Instrumentation: FlowTrace tag "Benefactors". Never strip it.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.Patronage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    public static class BenefactorsWallPanelBootstrap
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

            if (!DeNelle.Core.HubScenes.IsHub(scene.name)) return;

            // The raid target Village2 counts as a hub scene by name but is enemy-owned; the
            // town HUD stands down there and so does the monument's world door.
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn(BenefactorsCatalog.Sys,
                    "BenefactorsWallPanel suppressed in an enemy-owned scene (WO-550 rule).");
                return;
            }

            // GLOBAL dedupe across ALL loaded scenes - see HelpMenuBootstrap.
            foreach (var existing in Object.FindObjectsByType<BenefactorsWallPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Step(BenefactorsCatalog.Sys,
                        "duplicate BenefactorsWallPanel suppressed (one already exists).");
                    return;
                }
            }

            if (FindHero() == null) return;   // Title / HeroSelect skip.

            var go = new GameObject("BenefactorsWallPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BenefactorsWallPanel>();
            FlowTrace.Step(BenefactorsCatalog.Sys, "BenefactorsWallPanel created (single instance).");
        }

        // ⛔ REFLECTION ON PURPOSE, AND IT IS EVIDENCE OF THE RULE, NOT A VIOLATION OF IT.
        // DeNelle.HUD.asmdef references DeNelle.Core + DeNelle.Data ONLY and must never
        // reference DeNelle.Village (CLAUDE.md section 5 - the one enforced invariant).
        // HeroLocomotion is a Village type, so it is reached by name, exactly as
        // LeaderboardPanelBootstrap.FindHero does.
        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = Object.FindAnyObjectByType(t) as Component;
            return obj != null ? obj.transform : null;
        }
    }
}
