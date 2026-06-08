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
//      the build spawn point and wires SmartMobileCamera (primary) + legacy fallback
//      so the camera follows instead of staying stuck on the tree. (PatriciaLight descoped.)
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
        private const int MaxEmergencySpawns = 8;

        // PatriciaLight (DTT) is descoped. Activate for Village* scenes AND the new Castle Hub
        // (MainCastle_Hall, CastleHub*, etc.) so the ensurer + SmartMobileCamera target wiring
        // and emergency recovery works when the Castle is the project start / primary world scene.
        private static bool IsVillageScene(string name) =>
            !string.IsNullOrEmpty(name) && (
                name == "Village" || name.StartsWith("Village") || name.Contains("Village") ||
                name.Contains("Castle") || name.Contains("MainCastle") || name.Contains("CastleHub")
            );

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
            Begin();  // always attempt - Ensure will early-out if nothing to do; IsVillageScene only gates the Watch emergency loop
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Begin();  // always attempt for robustness on loads (web, title->village, editor play of Village2 etc.)
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
            if (hero == null)
            {
                if (IsVillageScene(SceneManager.GetActiveScene().name))
                {
                    // In primary scene (Village2), ensure a hero exists immediately on load so
                    // camera can acquire target and not stay stuck on the tree. Watch() still
                    // monitors for later destruction.
                    SpawnEmergencyHero();
                    hero = FindLoco()?.gameObject ?? FindHeroByName();
                }
                if (hero == null) return;
            }

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
            // Default gear stats (even for emergency capsule or non-swapped heroes): GearLoadout
            // pulls level-1 starters from GearCatalog (now populated) and drives WeaponMult/ArmorDefense.
            if (hero.GetComponent<GearLoadout>() == null) hero.AddComponent<GearLoadout>();
            // DEF-205: the always-on blue ground "reach ring" read as a mystery indicator
            // while walking (players couldn't tell what it meant). Removed — do NOT attach
            // HeroReachRing. The class is kept (HeroReachRing.cs) in case a gated, opt-in
            // reach hint is wanted later, but it must NOT render during normal play.
            // (Intentionally not adding HeroReachRing here.)

            // Find the primary gameplay camera (prefer tagged MainCamera or "main"/"game" in name, enabled, no render texture).
            // This is more reliable than Camera.main alone in editor or complex Village2 setups.
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<SmartMobileCamera>() != null)
            {
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (c == null || !c.enabled || c.targetTexture != null) continue;
                    if (c.CompareTag("MainCamera") || c.name.ToLower().Contains("main") || c.name.ToLower().Contains("game") || c.name.ToLower().Contains("camera"))
                    {
                        cam = c;
                        break;
                    }
                }
            }
            if (cam != null && cam.GetComponent<SmartMobileCamera>() == null)
            {
                cam.gameObject.AddComponent<SmartMobileCamera>();
                Debug.Log($"[HeroControlEnsurer] runtime-attached SmartMobileCamera to gameplay camera '{cam.name}'");
            }

            // Disable CinemachineBrain if present (can override transform in some Village2 setups).
            // Our SmartMobile follow + ForceFollowImmediate should control the view.
            var brain = cam != null ? cam.GetComponent("CinemachineBrain") as Behaviour : null;
            if (brain != null && brain.enabled)
            {
                brain.enabled = false;
                Debug.Log("[HeroControlEnsurer] disabled CinemachineBrain on gameplay camera");
            }

            // Force sole camera and high depth so this is the one used in Game view.
            var smc2 = cam != null ? cam.GetComponent<SmartMobileCamera>() : null;
            if (smc2 != null)
            {
                smc2.EnforceSoleCamera();
            }

            // Wire the authoritative camera (SmartMobileCamera) so it follows the hero.
            // Legacy VillageCamera is a fallback (SmartMobileCamera disables it at runtime).
            // This ensures the camera is never left "stuck on tree" (origin) when hero is
            // ensured/recovered — especially important on WebGL clean loads where timing/tags
            // can cause SmartMobileCamera's own TryFindHero fallback to miss on first frames.
            var smc = cam != null ? cam.GetComponent<SmartMobileCamera>() : FindFirstObjectByType<SmartMobileCamera>();
            if (smc != null)
            {
                smc.SetTarget(hero.transform);
                smc.ForceFollowImmediate();  // ensure instant snap off the tree on load
            }
            else
            {
                var legacyCam = FindFirstObjectByType<VillageCamera>();
                if (legacyCam != null) legacyCam.SetTarget(hero.transform);
            }

            // Quick direct force (easier workaround while full follow is stabilizing):
            // Explicitly move the gameplay camera to a follow position behind the hero.
            // This guarantees the view leaves the tree immediately on load in Village2,
            // even if LateUpdate follow or additive scene is interfering.
            if (cam != null && hero != null)
            {
                Vector3 followOffset = new Vector3(0f, 5f, -10f); // simple behind/above the hero
                cam.transform.position = hero.transform.position + followOffset;
                cam.transform.LookAt(hero.transform.position + Vector3.up * 1.8f); // look at hero chest/head
                Debug.Log("[HeroControlEnsurer] direct force: camera moved to follow hero at " + cam.transform.position);
            }

            Debug.Log($"[HeroControlEnsurer] ensured hero='{hero.name}' active={hero.activeInHierarchy} locoEnabled={l.enabled}.");
        }

        // Re-check while in the village; if the hero is gone, spawn an emergency one.
        private IEnumerator Watch()
        {
            int spawns = 0;
            while (IsVillageScene(SceneManager.GetActiveScene().name))
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

            // Wire camera for emergency hero (prefer modern SmartMobileCamera; legacy fallback).
            // Prevents "camera stuck on tree" in recovery scenarios on WebGL / clean loads.
            var smc = FindFirstObjectByType<SmartMobileCamera>();
            Transform camTarget = null;
            if (smc != null)
            {
                smc.SetTarget(go.transform);
                camTarget = go.transform;
            }
            else
            {
                var legacyCam = FindObjectsByType<VillageCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .FirstOrDefault();
                if (legacyCam != null)
                {
                    legacyCam.SetTarget(go.transform);
                    camTarget = go.transform;
                }
            }

            Debug.LogWarning($"[HeroControlEnsurer] real hero missing — spawned EMERGENCY movable hero at " +
                             $"{go.transform.position}; camera retargeted={(camTarget != null)}.");
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
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Village2")
                Debug.LogWarning($"[HeroDeathLogger] '{gameObject.name}' destroyed while in Village " +
                                 $"(frame={Time.frameCount}) — unexpected; investigate.");
        }
    }
}
