// =============================================================================
// HeroControlEnsurer - keeps the village hero controllable, and (new) recovers
// when something DESTROYS the hero root early in the village load.
// -----------------------------------------------------------------------------
// Player.log proved the baked "Hero (Blaise)" is present + healthy at load but
// DESTROYED within the first frame (before HeroLocomotion.Start runs) by an
// as-yet-unidentified third party (not VillageNpcInjector, not HeroBodySwapper,
// not HeroProgression - all ruled out). With no hero, there's nothing to re-enable,
// so this:
//   1. Ensures a present-but-disabled hero is active + its HeroLocomotion enabled.
//   2. Attaches HeroDeathLogger to the live hero so its OnDestroy logs WHEN (frame/
//      time) + a stack trace - which names the destroyer if it used DestroyImmediate.
//   3. Watches; if the hero vanishes it spawns an EMERGENCY movable capsule-hero at
//      the build spawn point and re-points VillageCamera at it - so the player can
//      move + test the rest of the game while the root destroyer is hunted.
// Self-bootstrapping DDOL; no Village.unity edit.
// =============================================================================

using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Ensures / recovers the village hero so the player can always move.</summary>
    public sealed class HeroControlEnsurer : MonoBehaviour
    {
        public static HeroControlEnsurer Instance { get; private set; }
        private const string TargetScene = "Village";
        private const int MaxEmergencySpawns = 8;

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
            if (SceneManager.GetActiveScene().name == TargetScene) Begin();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Begin();
        }

        private void Begin()
        {
            Ensure();
            StopAllCoroutines();
            StartCoroutine(Watch());
        }

        private static HeroLocomotion FindLoco() =>
            FindObjectsByType<HeroLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();

        private void Ensure()
        {
            var loco = FindLoco();
            GameObject hero = loco != null ? loco.gameObject : FindHeroByName();
            if (hero == null) return;   // Watch() will emergency-spawn

            if (!hero.activeSelf) hero.SetActive(true);
            var l = hero.GetComponent<HeroLocomotion>() ?? hero.AddComponent<HeroLocomotion>();
            l.enabled = true;
            if (hero.GetComponent<HeroDeathLogger>() == null) hero.AddComponent<HeroDeathLogger>();
            // Open-world combat readability: reticle over the nearest hostile target.
            if (hero.GetComponent<HeroTargetIndicator>() == null) hero.AddComponent<HeroTargetIndicator>();
            // DEF (combat feel): wire the melee swing that was BUILT but never attached.
            // PlayerAttackController.Awake self-configures (_enemyLayer -> "Enemy", animator/audio),
            // so a bare AddComponent is safe. Melee fires on Space / gamepad-South. NOTE: added for
            // EVERY class right now (the Knight's sword was the ask); Mage/Ranger get a melee with no
            // swing anim (their animators lack the Attack trigger — damage still lands). Restrict to
            // Knight later if desired.
            if (hero.GetComponent<PlayerAttackController>() == null) hero.AddComponent<PlayerAttackController>();
            // Combat readability: a faint ground ring showing basic-attack reach (the hitbox).
            // Added AFTER PlayerAttackController so HeroReachRing.Start reads the real range.
            if (hero.GetComponent<HeroReachRing>() == null) hero.AddComponent<HeroReachRing>();
            Debug.Log($"[HeroControlEnsurer] ensured hero='{hero.name}' active={hero.activeInHierarchy} locoEnabled={l.enabled}.");
        }

        // Re-check while in the village; if the hero is gone, spawn an emergency one.
        private IEnumerator Watch()
        {
            int spawns = 0;
            while (SceneManager.GetActiveScene().name == TargetScene)
            {
                yield return new WaitForSeconds(0.5f);
                if (FindLoco() == null && spawns < MaxEmergencySpawns)
                {
                    SpawnEmergencyHero();
                    spawns++;
                }
            }
        }

        private void SpawnEmergencyHero()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Hero (Blaise)";                       // so camera / NPCs find it by name
            go.transform.position = new Vector3(6f, 1f, 4f); // BuildHero's spawn (capsule centre at y=1)

            // Drop the primitive collider so HeroLocomotion's CapsuleCast can't
            // self-block (it sweeps against OTHER colliders for walls).
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = go.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh != null && mr != null)
            {
                var m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.60f, 0.45f, 0.85f));
                if (m.HasProperty("_Color"))     m.SetColor("_Color",     new Color(0.60f, 0.45f, 0.85f));
                mr.sharedMaterial = m;
            }

            go.AddComponent<HeroLocomotion>();
            go.AddComponent<HeroDeathLogger>();   // catch it too, in case the destroyer is periodic
            go.AddComponent<HeroTargetIndicator>();

            var cam = FindObjectsByType<VillageCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                      .FirstOrDefault();
            if (cam != null) cam.SetTarget(go.transform);

            Debug.LogWarning($"[HeroControlEnsurer] real hero missing — spawned EMERGENCY movable hero at " +
                             $"{go.transform.position}; camera retargeted={(cam != null)}.");
        }

        private static GameObject FindHeroByName()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero (")) return t.gameObject;
            return null;
        }
    }

    /// <summary>Diagnostic: logs when (and from where, if DestroyImmediate) the hero dies.</summary>
    public sealed class HeroDeathLogger : MonoBehaviour
    {
        private void OnDestroy()
        {
            // Diagnostic retired: the hero-deletion bug is fixed, and this fired
            // (harmlessly) on every normal scene-unload. Only warn if the hero dies
            // while the Village is still the active scene (i.e. an unexpected delete).
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Village")
                Debug.LogWarning($"[HeroDeathLogger] '{gameObject.name}' destroyed while in Village " +
                                 $"(frame={Time.frameCount}) — unexpected; investigate.");
        }
    }
}
