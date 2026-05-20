// =============================================================================
// CompassHudBootstrap — auto-spawns a CompassHud in any scene that has a
// recognisable hero + a UIDocument to hang the overlay off. Same
// RuntimeInitializeOnLoadMethod pattern as the Help menu / GameStateService
// bootstraps. Idempotent + scene-scoped — no duplicate compasses across loads.
// =============================================================================

using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            var go = new GameObject("CompassHud");
            SceneManager.MoveGameObjectToScene(go, scene);
            var compass = go.AddComponent<CompassHud>();
            compass.Hero = hero;

            // Hook a tiny ticker that refreshes the enemy target list every
            // ~0.5 s so we don't FindObjectsByType every frame.
            var ticker = go.AddComponent<EnemyTargetTicker>();
            ticker.Compass = compass;
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
