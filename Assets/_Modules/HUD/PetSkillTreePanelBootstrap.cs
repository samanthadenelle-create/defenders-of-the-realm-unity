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

            // WO-550: pet/town progression panel does NOT bootstrap in enemy-owned RAID scenes (Village2);
            // the home hub (MainCastle_Hall) is unaffected. Gate on the ACTIVE scene (player context).
            if (DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
            {
                FlowTrace.Warn("UI", "PetSkillTreePanel suppressed in enemy-owned scene (WO-550)");
                return;
            }

            // GLOBAL dedupe (across ALL loaded scenes) — see HelpMenuBootstrap.
            foreach (var existing in UnityEngine.Object.FindObjectsByType<PetSkillTreePanel>(
                         FindObjectsInactive.Include))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate PetSkillTreePanel suppressed (one already exists)");
                    return;
                }
            }

            if (FindHero() == null) return; // Title / HeroSelect skip.

            // WO-F conversion (2026-07-03): the panel is a kit uGUI modal now — no
            // UIDocument/PanelSettings host needed (old FindPanelSettings gate removed).
            var go = new GameObject("PetSkillTreePanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PetSkillTreePanel>();
            go.AddComponent<PetSkillTreePanelKeyDriver>();
            FlowTrace.Step("UI", "PetSkillTreePanel created (single instance)");
        }

        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = UnityEngine.Object.FindAnyObjectByType(t) as Component;
            return obj != null ? obj.transform : null;
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
