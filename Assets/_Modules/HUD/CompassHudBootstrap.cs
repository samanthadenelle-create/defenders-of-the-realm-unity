// =============================================================================
// CompassHudBootstrap — auto-spawns a CompassHud in any scene that has a
// recognisable hero + a UIDocument to hang the overlay off. Same
// RuntimeInitializeOnLoadMethod pattern as the Help menu / GameStateService
// bootstraps. Idempotent + scene-scoped — no duplicate compasses across loads.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CompassHud>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null && existing.gameObject.scene == scene) return;
            }

            var hero = FindHero();
            if (hero == null) return; // No hero in this scene (e.g., Title/HeroSelect) → no compass.

            // WO-322 ROOT CAUSE: the compass spawned itself but NEVER got a
            // PanelSettings. It relied on CompassHud.Awake scanning the scene for a
            // UIDocument to borrow one from — but at AfterSceneLoad the sibling HUD
            // UIDocuments (DailyQuest/QuestTracker) may not have spawned yet, so the
            // scan came up empty and CompassHud.Awake did `enabled = false`
            // PERMANENTLY (the spawned GameObject then satisfied the existing-instance
            // guard above, so it was never retried). Result: no compass, ever.
            //
            // FIX — mirror DailyQuestHudBootstrap / QuestTrackerHudBootstrap exactly:
            // resolve the PanelSettings HERE and BAIL (return, no GameObject) if none
            // is available yet, so the NEXT sceneLoaded retries. Assign PanelSettings
            // BEFORE adding the CompassHud component so its UIDocument renders.
            var panel = FindPanelSettings();
            if (panel == null) return; // retry on next sceneLoaded once a panel exists.

            var go = new GameObject("CompassHud");
            SceneManager.MoveGameObjectToScene(go, scene);
            var ui = go.AddComponent<UIDocument>();
            ui.panelSettings = panel;
            ui.sortingOrder = 90; // below Help menu (100), above default HUD chrome
            var compass = go.AddComponent<CompassHud>();
            compass.Hero = hero;

            // Hook a tiny ticker that refreshes the enemy target list every
            // ~0.5 s so we don't FindObjectsByType every frame.
            var ticker = go.AddComponent<EnemyTargetTicker>();
            ticker.Compass = compass;
        }

        private static PanelSettings FindPanelSettings()
        {
            // Borrow from any existing UIDocument in the scene (no Resources-by-name
            // load). Mirrors DailyQuestHudBootstrap / QuestTrackerHudBootstrap.
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
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
