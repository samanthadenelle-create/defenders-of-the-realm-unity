// =============================================================================
// PartyShopPanelMvvmBootstrap — spawns the code-built MVVM party gear shop
// (PartyShopPanelMvvm) once per gameplay scene, ONLY when FeatureFlags.PartyShop
// is ON. Mirrors BuildingUpgradePanelMvvmBootstrap's lifecycle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// FLAG GATE: the MVVM party shop registers PanelId.PartyShop, and CmdOpenShop
// routes a weapon/armor vendor to PanelRouter→PartyShop only when the flag is ON
// (legacy DeNelle.Village.Hero.ShopPanel path when OFF). So:
//   • flag ON  -> this bootstrap spawns the MVVM panel; CmdOpenShop routes to it.
//   • flag OFF -> this bootstrap does nothing; the legacy ShopPanel opens as before.
//
// The MVVM panel is pure code-built uGUI (it builds its own Canvas on Open), so it
// needs NO PanelSettings — it just needs a hero in the scene (Title / HeroSelect skip),
// matching BuildingUpgradePanelMvvmBootstrap's gate.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Hero
{
    public static class PartyShopPanelMvvmBootstrap
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
            if (!DeNelle.Core.FeatureFlags.PartyShop) return;   // flag OFF -> legacy ShopPanel owns the open

            // WO-550: the party gear SHOP (economy) does NOT bootstrap in enemy-owned RAID scenes
            // (Village2); the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene.
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "PartyShopPanelMvvm suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<PartyShopPanelMvvm>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate PartyShopPanelMvvm suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return;   // Title / HeroSelect skip.

            var go = new GameObject("PartyShopPanelMvvm");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PartyShopPanelMvvm>();
            FlowTrace.Step("UI", "PartyShopPanelMvvm created (single instance, flag ON)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindObjectOfType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
