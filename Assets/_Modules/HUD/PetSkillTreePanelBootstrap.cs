// =============================================================================
// PetSkillTreePanelBootstrap — auto-spawns a PetSkillTreePanel in any scene
// that has a hero present, and watches for the P key each frame to toggle it.
// Mirrors DailyQuestHudBootstrap so the panel only shows up in the actual play
// scenes (Village, Dungeon) — Title / HeroSelect skip.
// -----------------------------------------------------------------------------
// Key choice 2026-05-20: P is unused elsewhere in the project (verified via
// grep over KeyCode.P). Lives on a hidden driver MonoBehaviour so legacy
// Input.GetKeyDown can be polled without leaking into static state.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

            foreach (var existing in UnityEngine.Object.FindObjectsByType<PetSkillTreePanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
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

        private void Update()
        {
            if (_panel == null) return;
            // Plain P toggles. Guard against typing in a text field — if any
            // focused TextField exists in our document, swallow the keystroke.
            if (Input.GetKeyDown(KeyCode.P) && !IsTypingInUiText())
            {
                _panel.Toggle();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && _panel.IsOpen)
            {
                _panel.Close();
            }
        }

        private bool IsTypingInUiText()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return false;
            var focused = doc.rootVisualElement.focusController?.focusedElement;
            return focused is TextField;
        }
    }
}
