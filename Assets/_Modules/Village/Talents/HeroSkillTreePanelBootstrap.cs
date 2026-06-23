// =============================================================================
// HeroSkillTreePanelBootstrap — spawns the code-built MVVM skill-tree + loadout
// panels (HeroSkillTreePanelMvvm + HeroLoadoutPanelMvvm) once per gameplay scene.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Talents
//
// Mirrors BuildingUpgradePanelMvvmBootstrap. These panels are pure code-built
// uGUI (they build their own Canvas on Open) so they need NO PanelSettings — just
// a hero in the scene (Title / HeroSelect skip). Each self-registers its PanelId
// in Awake (HeroSkillTree + the legacy HeroTalents route; HeroLoadout), so once
// spawned the inventory "Skills" tab + the skill-tree "Equip" button can open them.
//
// REPLACES the UIDocument HeroTalentPanel (its bootstrap is now suppressed — it
// rendered empty in player builds, §8).
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Talents
{
    public static class HeroSkillTreePanelBootstrap
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
            if (FindHero() == null) return; // Title / HeroSelect skip.

            SpawnOne<HeroSkillTreePanelMvvm>(scene, "HeroSkillTreePanelMvvm");
            SpawnOne<HeroLoadoutPanelMvvm>(scene, "HeroLoadoutPanelMvvm");
        }

        private static void SpawnOne<T>(Scene scene, string goName) where T : MonoBehaviour
        {
            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<T>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate " + goName + " suppressed (one already exists)");
                    return;
                }
            }

            var go = new GameObject(goName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<T>();
            FlowTrace.Step("UI", goName + " created (single instance)");
        }

        private static Transform FindHero()
        {
            var hero = Object.FindObjectOfType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
