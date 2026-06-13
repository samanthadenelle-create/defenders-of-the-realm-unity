// =============================================================================
// CompassHudBootstrap — auto-spawns a CompassHud in any scene that has a
// recognisable hero. Same RuntimeInitializeOnLoadMethod pattern as the Help
// menu / GameStateService bootstraps. Idempotent + scene-scoped — no duplicate
// compasses across loads.
//
// WO-322 RE-FIX (2026-06-12): CompassHud is now a CODE-BUILT uGUI overlay (its
// own ScreenSpaceOverlay Canvas) — it no longer needs a UIDocument or a
// PanelSettings. The previous bootstrap REQUIRED a PanelSettings-bearing
// UIDocument in the scene and BAILED forever when none existed (the main HUD,
// VillageHudController, is pure uGUI with NO PanelSettings) → the compass never
// spawned. We drop that requirement entirely: spawn as soon as a hero exists.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    public static class CompassHudBootstrap
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
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CompassHud>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                {
                    FlowTrace.Warn("UI", "duplicate CompassHud suppressed (one already exists)");
                    return;
                }
            }

            var hero = FindHero();
            if (hero == null) return; // No hero in this scene (e.g., Title/HeroSelect) → no compass.

            var go = new GameObject("CompassHud");
            SceneManager.MoveGameObjectToScene(go, scene);
            var compass = go.AddComponent<CompassHud>();
            compass.Hero = hero;

            // Refresh the enemy target list ~2 Hz so we don't FindObjectsByType
            // every frame.
            var ticker = go.AddComponent<EnemyTargetTicker>();
            ticker.Compass = compass;
            FlowTrace.Step("UI", "CompassHud created (single instance)");
        }

        /// <summary>
        /// Find the hero transform by reflecting against the
        /// <c>DeNelle.Village.HeroLocomotion</c> type. Using reflection lets
        /// the HUD asmdef stay decoupled from DeNelle.Village.
        /// </summary>
        private static Transform FindHero()
        {
            var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            var obj = UnityEngine.Object.FindObjectOfType(t) as Component;
            return obj != null ? obj.transform : null;
        }
    }

    /// <summary>
    /// Refreshes <see cref="CompassHud.Targets"/> every <see cref="_intervalSec"/>
    /// seconds by reflection-looking for every DeNelle.Village.Enemy in the
    /// scene. Cheap enough at 2 Hz with the live-enemy count we have.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTargetTicker : MonoBehaviour
    {
        public CompassHud Compass;
        private float _next;
        private const float _intervalSec = 0.5f;
        private System.Type _enemyType;

        private void Awake()
        {
            _enemyType = System.Type.GetType("DeNelle.Village.Enemy, DeNelle.Village");
        }

        private void Update()
        {
            if (Compass == null || _enemyType == null) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _intervalSec;

            Compass.Targets.Clear();
            var found = UnityEngine.Object.FindObjectsByType(
                _enemyType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (UnityEngine.Object o in found)
            {
                if (o is Component c) Compass.Targets.Add(c.transform);
            }
        }
    }
}
