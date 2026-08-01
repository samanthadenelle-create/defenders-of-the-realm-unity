// =============================================================================
// RealmMapPanelBootstrap — spawns the RealmMapPanel host once per gameplay
// scene so PanelRouter always has a live PanelId.RealmMap opener (WO-826).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Mirrors PartyShopPanelMvvmBootstrap's lifecycle exactly: the panel is pure
// code-built uGUI (it builds its own Canvas lazily on Open), so the bootstrap
// just spawns the bare component wherever a hero exists (Title / HeroSelect
// skip). The component's Awake registers the PanelRouter opener; the HUD kit
// Map button and the DevPanel entry both open through that route.
//
// WO-550: the town map does NOT bootstrap in enemy-owned RAID scenes.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Hero
{
    public static class RealmMapPanelBootstrap
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

            // WO-550: town surfaces stay out of enemy-owned scenes (Village2 raids).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("RealmMap", "RealmMapPanel suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe across all loaded scenes (the HelpMenuBootstrap pattern).
            foreach (var existing in Object.FindObjectsByType<RealmMapPanel>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("RealmMap", "duplicate RealmMapPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return;   // Title / HeroSelect skip.

            var go = new GameObject("RealmMapPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<RealmMapPanel>();
            FlowTrace.Step("RealmMap", "RealmMapPanel created (single instance, router-registered)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindAnyObjectByType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
