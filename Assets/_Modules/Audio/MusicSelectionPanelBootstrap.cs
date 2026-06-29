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
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate MusicSelectionPanel suppressed (one already exists)");
                    return;
                }
            }

            var panel = FindPanelSettings();
            if (panel == null) return; // no HUD canvas in this scene — stay quiet.

            var go = new GameObject("MusicSelectionPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 96; // matches the panel's own sortingOrder
            go.AddComponent<MusicSelectionPanel>();
            FlowTrace.Step("UI", "MusicSelectionPanel created (single instance)");
        }

        private static PanelSettings FindPanelSettings()
        {
            var docs = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }
}
