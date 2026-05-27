// =============================================================================
// LevelUpSkillPopupBootstrap — Sprint C wiring. Self-installs the DEF-77
// LevelUpSkillPopup at runtime (no scene authoring, no VillageSceneBuilder /
// scene re-save), mirroring DeNelle.Settings.MusicToggleBootstrap exactly:
//   • [RuntimeInitializeOnLoadMethod] + a SceneManager.sceneLoaded hook re-installs
//     per scene.
//   • Borrows the top-most live UIDocument's PanelSettings — a code-built UIDocument
//     with no PanelSettings renders NOTHING (the documented empty-UI trap).
//   • Inactive-then-activate so the UIDocument builds its root with PanelSettings
//     already assigned before the popup's OnEnable runs.
//
// TIMING CAVEAT: the popup subscribes (in its OnEnable) to HeroProgression.Instance
// .OnLevelUp. HeroProgression is attached at runtime by ProgressionManager, so if a
// scene installs the popup before the hero exists, the popup will not catch level-
// ups in that scene. In the village/combat scene the hero is present by the time a
// level-up can occur, so this is fine for play-testing; a hardened re-subscribe is a
// follow-up if it ever matters in practice.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.UI
{
    /// <summary>Installs the level-up skill popup into each loaded scene at runtime.</summary>
    public static class LevelUpSkillPopupBootstrap
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install();

        private static void Install()
        {
            if (Object.FindAnyObjectByType<LevelUpSkillPopup>() != null) return;

            // Borrow a PanelSettings from the top-most UIDocument in the scene
            // (mirrors MusicToggleBootstrap) — no PanelSettings ⇒ nothing renders.
            PanelSettings ps = null;
            float topSort = float.MinValue;
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (doc == null || doc.panelSettings == null) continue;
                if (doc.sortingOrder >= topSort) { topSort = doc.sortingOrder; ps = doc.panelSettings; }
            }
            if (ps == null) return;   // no UI in this scene to host the popup

            var go = new GameObject("LevelUpSkillPopup");
            go.SetActive(false);
            var doc2 = go.AddComponent<UIDocument>();
            doc2.panelSettings = ps;
            doc2.sortingOrder = topSort + 60;   // above the scene's own UI (and the music toggle)
            go.AddComponent<LevelUpSkillPopup>();
            go.SetActive(true);
        }
    }
}
