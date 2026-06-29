// =============================================================================
// GameGuidePanelBootstrap — spawns the code-built Game Guide codex (GameGuidePanel)
// once per scene so it registers PanelId.GameGuide (WO-588).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The Settings panel (DeNelle.Settings, which references Core only) opens the guide
// via PanelRouter.Open(PanelId.GameGuide). That route resolves only when a
// GameGuidePanel exists in the scene and has registered itself — so this bootstrap
// guarantees one instance is present. No flag / hero gate: the guide is an opt-in
// help codex available wherever Settings can be reached. Mirrors
// PartyShopPanelMvvmBootstrap's lifecycle (minus the gates).
//
// The panel is pure code-built uGUI (it builds its own Canvas on Open), so it needs
// NO PanelSettings.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    public static class GameGuidePanelBootstrap
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

            // GLOBAL dedupe across all loaded scenes — one guide host is enough.
            foreach (var existing in Object.FindObjectsByType<GameGuidePanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null) return;
            }

            var go = new GameObject("GameGuidePanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<GameGuidePanel>();
            FlowTrace.Step("UI", "GameGuidePanel created (single instance — opens via Settings -> PanelId.GameGuide)");
        }
    }
}
