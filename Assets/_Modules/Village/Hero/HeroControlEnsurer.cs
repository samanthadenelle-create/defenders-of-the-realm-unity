// =============================================================================
// HeroControlEnsurer - guarantees the village hero is controllable on load.
// -----------------------------------------------------------------------------
// Symptom (player build): the village loaded but the hero could not move, with
// ZERO exceptions. Player.log showed HeroLocomotion.Start never logged -> the
// movement controller never ran -> no input -> "frozen". The hero GameObject
// exists (the camera targets "Hero (Blaise)") and HeroLocomotion is NOT stripped
// (no missing-script warnings), so it was baked disabled / on an inactive object
// in this build for some reason the scene re-bake can't be touched safely.
//
// This DDOL safety-net (same pattern as VillageNpcInjector) re-activates the hero
// and enables (or adds) HeroLocomotion on every Village load, so control always
// works regardless of the baked state. It logs the hero's pre-fix state so the
// exact cause is visible in the next run.
// =============================================================================

using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Re-activates the hero + ensures HeroLocomotion runs on Village load.</summary>
    public sealed class HeroControlEnsurer : MonoBehaviour
    {
        public static HeroControlEnsurer Instance { get; private set; }
        private const string TargetScene = "Village";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("HeroControlEnsurer").AddComponent<HeroControlEnsurer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Ensure();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Ensure();
        }

        private void Ensure()
        {
            // Find the hero even if its GameObject is inactive or its component disabled.
            var loco = FindObjectsByType<HeroLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
            GameObject hero = loco != null ? loco.gameObject : FindHeroByName();

            if (hero == null)
            {
                Debug.LogWarning("[HeroControlEnsurer] no hero found in Village - cannot ensure control.");
                return;
            }

            bool wasActive = hero.activeSelf;
            if (!hero.activeSelf) hero.SetActive(true);

            var l = hero.GetComponent<HeroLocomotion>();
            bool added = false;
            if (l == null) { l = hero.AddComponent<HeroLocomotion>(); added = true; }
            bool wasEnabled = l.enabled;
            l.enabled = true;

            Debug.Log($"[HeroControlEnsurer] hero='{hero.name}' wasActive={wasActive} " +
                      $"wasLocoEnabled={wasEnabled} locoAdded={added} -> forced active + enabled.");
        }

        private static GameObject FindHeroByName()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero (")) return t.gameObject;
            return null;
        }
    }
}
