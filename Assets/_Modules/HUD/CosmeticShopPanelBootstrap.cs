// =============================================================================
// CosmeticShopPanelBootstrap - spawns one CosmeticShopPanel per gameplay scene
// (anything with a hero). Mirrors DailyQuestHudBootstrap so the shop never
// flashes on Title / HeroSelect. The C key toggles the panel in-scene.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class CosmeticShopPanelBootstrap
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

            // WO-550: economy/store panels do NOT bootstrap in enemy-owned RAID scenes (Village2);
            // the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene (player context).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "CosmeticShopPanel suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes) — not per-scene. Additive
            // scene loads fire sceneLoaded with new scenes, and a per-scene check
            // would miss the live instance, spawning a duplicate.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CosmeticShopPanel>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate CosmeticShopPanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // skip Title / HeroSelect

            // WO-F conversion (2026-07-03): the panel is a kit uGUI modal now — no
            // UIDocument/PanelSettings host needed (old FindPanelSettings gate removed).
            var go = new GameObject("CosmeticShopPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<CosmeticShopPanel>();
            FlowTrace.Step("UI", "CosmeticShopPanel created (single instance)");
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
