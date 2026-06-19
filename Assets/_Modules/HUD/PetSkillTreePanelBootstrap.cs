// =============================================================================
// PetSkillTreePanelBootstrap — auto-spawns a PetSkillTreePanel in any scene
// that has a hero present. Mirrors DailyQuestHudBootstrap so the panel only
// shows up in the actual play scenes (Village, Dungeon) — Title / HeroSelect skip.
// -----------------------------------------------------------------------------
// Mobile-first (WO-437): the legacy 'P' toggle hotkey is REMOVED. Pet Skills
// opens via its world interactable (Pet House -> PanelRouter.Open(PanelId.
// PetSkillTree)); ESC is owned centrally by PauseController.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class PetSkillTreePanelBootstrap
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

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<PetSkillTreePanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate PetSkillTreePanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            var panel = FindPanelSettings();
            if (panel == null) return;

            var go = new GameObject("PetSkillTreePanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 105; // above HUD chips, below HelpMenu toast.
            go.AddComponent<PetSkillTreePanel>();
            go.AddComponent<PetSkillTreePanelKeyDriver>();
            FlowTrace.Step("UI", "PetSkillTreePanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = UnityEngine.Object.FindObjectOfType(t) as Component;
            return obj != null ? obj.transform : null;
        }

        private static PanelSettings FindPanelSettings()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }
    }

    /// <summary>
    /// Hidden helper component that polls the legacy Input Manager every frame
    /// and toggles the panel on P. Kept separate so PetSkillTreePanel itself
    /// stays focused on UI rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetSkillTreePanelKeyDriver : MonoBehaviour
    {
        private PetSkillTreePanel _panel;

        private void Awake()
        {
            _panel = GetComponent<PetSkillTreePanel>();
        }

        // WO-437: the global 'P' hotkey (and the per-panel ESC polling that raced the
        // central handler) are REMOVED. Pet Skills opens only via its world interactable
        // (Pet House -> PanelRouter.Open(PanelId.PetSkillTree)); ESC is owned centrally
        // by PauseController (closes the top modal, else pauses). The panel itself is
        // unchanged and still gated by PanelManager's battle-lock.
    }
}
