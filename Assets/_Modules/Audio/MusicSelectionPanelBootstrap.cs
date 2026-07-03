// =============================================================================
// MusicSelectionPanelBootstrap — spawns the WO-162 jukebox panel per scene.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Audio   Namespace: DeNelle.Audio
//
// Mirrors CosmeticShopPanelBootstrap (DeNelle.HUD): auto-runs after each scene
// load, finds a PanelSettings already in the scene, and attaches one
// MusicSelectionPanel (the J-key jukebox). No scene/prefab hand-edit — the panel
// is created in code at runtime, so this never touches .unity / .prefab files.
//
// Spawns only when the scene actually has a UIDocument with a PanelSettings (so
// it stays silent on Title / loaders that have no HUD canvas). Idempotent — one
// panel per scene.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Audio
{
    /// <summary>
    /// Ensures a <see cref="MusicSelectionPanel"/> exists in each gameplay scene
    /// that has a HUD canvas (a UIDocument with PanelSettings). Auto-run; no
    /// manual wiring.
    /// </summary>
    public static class MusicSelectionPanelBootstrap
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

            // WO-550: the town jukebox does NOT bootstrap in enemy-owned RAID scenes (Village2);
            // the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene (player context).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "MusicSelectionPanel suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes) — not per-scene. The
            // additive OuterWorld load fires sceneLoaded with a new scene and a
            // per-scene check missed the live instance, spawning a duplicate.
            foreach (var existing in Object.FindObjectsByType<MusicSelectionPanel>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate MusicSelectionPanel suppressed (one already exists)");
                    return;
                }
            }

            // WO-F conversion (2026-07-03): the panel is a kit uGUI modal now — no
            // UIDocument/PanelSettings needed. The old "spawn only where a HUD canvas
            // exists" gate is replaced by the enemy-owned suppression above plus this
            // front-end guard (menu scenes own their UI; the jukebox is gameplay-only —
            // same list as VillageHudBootstrap.MenuScenes, inlined: Audio can't
            // reference DeNelle.HUD under the cross-assembly rule).
            string active = SceneManager.GetActiveScene().name;
            foreach (var menu in new[] { "Title", "HeroSelect", "PetSelect", "Intro", "Store", "GameOver" })
                if (string.Equals(active, menu, System.StringComparison.OrdinalIgnoreCase)) return;

            var go = new GameObject("MusicSelectionPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<MusicSelectionPanel>();
            FlowTrace.Step("UI", "MusicSelectionPanel created (single instance)");
        }
    }
}
