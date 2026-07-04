// =============================================================================
// HeartHudBridgeBootstrap — guarantee the economy/heart → HUD bridge exists in
// EVERY gameplay scene, not just the Village.
// -----------------------------------------------------------------------------
// ROOT CAUSE this fixes (owner playtest 2026-06-13):
//   HeartHudBridge (the component that subscribes to EconomyService.OnChanged and
//   pushes the wood/iron/food/crystal wallet onto the HUD resource bar + the Heart
//   HP bar) is attached ONLY by VillageController.EnsureHeartHudBridge — and
//   VillageController exists only in the VILLAGE scene. In the castle home hub
//   (MainCastle_Hall) and the overworld there is no VillageController, so the bridge
//   is never attached. VillageHudBootstrap (DeNelle.HUD) spawns the HUD itself in
//   those scenes, but it CANNOT attach HeartHudBridge — DeNelle.HUD may reference
//   DeNelle.Core only, never DeNelle.Village. Net result: the HUD exists but no
//   one feeds it the wallet → the resource bar stays frozen while harvest changes
//   the real totals. The log proves it: "OnChanged fired W754 ... (subscribers=0)".
//
// THE FIX (mirror of VillageHudBootstrap, but on the VILLAGE side so it CAN attach
//   the Village-assembly bridge):
//   A RuntimeInitializeOnLoadMethod(AfterSceneLoad) + sceneLoaded guarantor that,
//   on every gameplay scene load, ENSURES exactly one HeartHudBridge exists. It is
//   idempotent: if the scene already has a bridge, or has a VillageController (which
//   will attach its own), it does nothing. The bridge is self-resolving (finds the
//   HUD + EconomyService by itself and gives up cleanly if absent), so a spawned
//   instance in the castle/OuterWorld just works once those services come up.
//
//   Menu / front-end scenes are skipped (same exclusion list as VillageHudBootstrap)
//   — they own their UI and have no wallet to show.
//
// ASMDEF: DeNelle.Village (same assembly as HeartHudBridge + VillageController +
//   EconomyService) — that's the whole point; the HUD-side bootstrap could not do
//   this across the asmdef boundary.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Ensures a <see cref="HeartHudBridge"/> (the economy/heart → HUD feed) exists
    /// in every gameplay scene, so the resource bar updates in the castle hub and
    /// the overworld, not only in the Village where VillageController authors one.
    /// </summary>
    public static class HeartHudBridgeBootstrap
    {
        // Same front-end exclusion list as VillageHudBootstrap — these own their UI
        // and carry no wallet bar. Matched case-insensitively against the scene name.
        private static readonly string[] MenuScenes =
        {
            "Title", "HeroSelect", "PetSelect", "Intro", "IntroFlow",
            "Store", "PackStore", "Boot", "Bootstrap", "MainMenu", "GameOver",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureBridgeForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureBridgeForScene(scene);
        }

        private static void EnsureBridgeForScene(Scene scene)
        {
            try
            {
                if (IsMenuScene(scene.name)) return;

                // Idempotent: a bridge already exists, or a VillageController will
                // attach its own (the Village scene) — do nothing either way.
                if (Object.FindAnyObjectByType<HeartHudBridge>() != null) return;
                if (Object.FindAnyObjectByType<VillageController>() != null) return;

                var go = new GameObject("HeartHudBridge (bootstrapped)");
                // Parent to the just-loaded gameplay scene so it unloads with it
                // (no DontDestroyOnLoad — mirrors VillageHudBootstrap's HUD host).
                if (scene.IsValid() && scene.isLoaded)
                    SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<HeartHudBridge>();
                Debug.Log($"[HeartHudBridgeBootstrap] economy/heart → HUD bridge ensured for scene '{scene.name}' " +
                          "(no VillageController here — castle hub / OuterWorld).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HeartHudBridgeBootstrap] EnsureBridgeForScene failed: " + e.Message);
            }
        }

        private static bool IsMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < MenuScenes.Length; i++)
            {
                if (string.Equals(sceneName, MenuScenes[i], System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
