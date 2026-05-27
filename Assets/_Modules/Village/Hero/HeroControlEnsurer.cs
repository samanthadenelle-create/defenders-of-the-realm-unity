// =============================================================================
// HeroControlEnsurer - guarantees the village hero is controllable on load.
// -----------------------------------------------------------------------------
// Symptom (player build): the village loaded but the hero could not move, with
// ZERO exceptions. Player.log showed HeroLocomotion.Start never logged -> the
// movement controller never ran -> no input -> "frozen". The hero GameObject
// exists (the camera targets "Hero (Blaise)") and HeroLocomotion is NOT stripped
// (no missing-script warnings), so it was baked disabled / on an inactive object.
//
// This self-bootstrapping DDOL safety-net (no Village.unity edit -> no scene
// re-save corruption risk) ensures the hero on every Village load, TWICE:
//   - immediately (AfterSceneLoad / sceneLoaded): fixes a baked inactive/disabled
//     hero, which is a static state nothing re-touches.
//   - again after a short delay (0.5s + 2 frames): fixes the case where a script
//     re-disables the hero in its own Start() / a late system, which would run
//     AFTER the immediate pass.
// Both passes are idempotent and log the pre-fix hero state so the exact cause is
// visible. Referencing HeroLocomotion directly also preserves it from any future
// IL2CPP/WebGL stripping (it was previously only reflection-referenced).
// =============================================================================

using System.Collections;
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
            if (SceneManager.GetActiveScene().name == TargetScene) RunEnsure();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) RunEnsure();
        }

        private void RunEnsure()
        {
            Ensure("load");                 // static inactive/disabled state
            StartCoroutine(DelayedEnsure()); // late script re-disable
        }

        private IEnumerator DelayedEnsure()
        {
            yield return new WaitForSeconds(0.5f);
            yield return null;   // let any same-frame late disabler settle
            yield return null;
            Ensure("delayed");
        }

        private void Ensure(string phase)
        {
            // Find the hero even if its GameObject is inactive or its component disabled.
            var loco = FindObjectsByType<HeroLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
            GameObject hero = loco != null ? loco.gameObject : FindHeroByName();

            if (hero == null)
            {
                Debug.LogWarning($"[HeroControlEnsurer] ({phase}) no hero found in Village.");
                return;
            }

            bool wasActive = hero.activeInHierarchy;
            if (!hero.activeSelf) hero.SetActive(true);

            var l = hero.GetComponent<HeroLocomotion>();
            bool added = false;
            if (l == null) { l = hero.AddComponent<HeroLocomotion>(); added = true; }
            bool wasEnabled = l.enabled;
            l.enabled = true;

            Debug.Log($"[HeroControlEnsurer] ({phase}) hero='{hero.name}' wasActive={wasActive} " +
                      $"wasLocoEnabled={wasEnabled} locoAdded={added} -> active={hero.activeInHierarchy}, locoEnabled={l.enabled}.");
        }

        private static GameObject FindHeroByName()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero (")) return t.gameObject;
            return null;
        }
    }
}
