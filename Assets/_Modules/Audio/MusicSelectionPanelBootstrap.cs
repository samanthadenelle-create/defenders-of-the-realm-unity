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

            // One panel per scene.
            foreach (var existing in Object.FindObjectsByType<MusicSelectionPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
            }

            var panel = FindPanelSettings();
            if (panel == null) return; // no HUD canvas in this scene — stay quiet.

            var go = new GameObject("MusicSelectionPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 96; // matches the panel's own sortingOrder
            go.AddComponent<MusicSelectionPanel>();
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
