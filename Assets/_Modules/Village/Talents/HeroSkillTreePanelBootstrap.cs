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

            // WO-550: the SKILL TREE is a town/progression panel — suppress it in enemy-owned RAID
            // scenes (Village2). The LOADOUT panel is COMBAT-relevant (hot-swap gear) and STILL spawns
            // in raids. The home hub (MainCastle_Hall) is unaffected by either gate.
            if (!DeNelle.Core.HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name))
                SpawnOne<HeroSkillTreePanelMvvm>(scene, "HeroSkillTreePanelMvvm");
            else
                FlowTrace.Step("UI", "HeroSkillTreePanelMvvm suppressed in enemy-owned scene (WO-550); loadout still spawns");

            SpawnOne<HeroLoadoutPanelMvvm>(scene, "HeroLoadoutPanelMvvm");
        }

        private static void SpawnOne<T>(Scene scene, string goName) where T : MonoBehaviour
        {
            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<T>(
                         FindObjectsInactive.Include))
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
            var hero = Object.FindAnyObjectByType<DeNelle.Village.HeroLocomotion>();
            return hero != null ? hero.transform : null;
        }
    }
}
