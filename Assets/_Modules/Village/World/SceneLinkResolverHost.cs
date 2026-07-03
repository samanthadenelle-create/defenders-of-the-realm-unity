// =============================================================================
// SceneLinkResolverHost — the RUNTIME host that implements ISceneLinkResolver (WO1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Self-bootstraps (like RuntimeRegionGate) into a hidden DontDestroyOnLoad object,
// loads Resources/Data/scene-links.json via JsonUtility into a dict, and registers
// itself via CoreServices.RegisterSceneLinkResolver so any module can route a
// crossing with CoreServices.SceneLinkResolver?.TravelTo(id) — no scene wiring.
//
// TravelTo replicates the proven SceneTransitionTrigger.Cross load+reposition flow
// (do NOT modify that primitive; this is the data-driven equivalent):
//   1. Guard Application.CanStreamedLevelBeLoaded(toScene) — if the target space
//      isn't in Build Settings yet (WO2 builds the actual scenes), the link is
//      INERT and logged, never thrown. The chain seeds placeholder scene names.
//   2. Single load → DontDestroyOnLoad(hero.root) so the hero survives the swap.
//   3. SceneManager.LoadScene(toScene, single|additive).
//   4. Coroutine: wait → resolve landing (spawnPoint object else targetPosition) →
//      HeroLocomotion.WarpTo (NavMesh-snapped) → validate on-mesh → for single load
//      MoveGameObjectToScene(hero, active) → optionally unload the fromScene.
//
// Instrumented per §12: [Flow:Resolver] Step/Warn/Fail + Guard.Try on risky ops.
// The Core data/interface stay pure (no Village ref); this HOST lives in Village.
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime resolver/host for the data-driven scene-link catalog. One hidden
    /// DontDestroyOnLoad instance, spun up by the static bootstrap. Implements the
    /// Core-defined ISceneLinkResolver so consumers never reference Village.
    /// </summary>
    public sealed class SceneLinkResolverHost : MonoBehaviour, ISceneLinkResolver
    {
        private const string CatalogResourcePath = "Data/scene-links";   // Resources/Data/scene-links.json

        private readonly Dictionary<string, SceneLink> _byId = new Dictionary<string, SceneLink>();
        private readonly List<SceneLink> _all = new List<SceneLink>();

        // --------------------------------------------------------------------
        //  SELF-BOOTSTRAP (mirrors RuntimeRegionGate: AfterSceneLoad).
        // --------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (FindAnyObjectByType<SceneLinkResolverHost>() != null) return;   // already present
            var go = new GameObject("__SceneLinkResolver");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SceneLinkResolverHost>();
        }

        private void Awake()
        {
            LoadCatalog();
            CoreServices.RegisterSceneLinkResolver(this);
            FlowTrace.Step("Resolver", $"registered with {_all.Count} scene-link(s) loaded.");
        }

        private void OnDestroy()
        {
            CoreServices.UnregisterSceneLinkResolver(this);
        }

        // --------------------------------------------------------------------
        //  CATALOG LOAD (JsonUtility, same pattern as RuntimeRegionGate).
        // --------------------------------------------------------------------
        private void LoadCatalog()
        {
            Guard.Try("Resolver", "load scene-links.json", () =>
            {
                var ta = Resources.Load<TextAsset>(CatalogResourcePath);
                if (ta == null)
                {
                    FlowTrace.Warn("Resolver", "scene-links.json not found in Resources/Data — no scene links loaded.");
                    return;
                }
                var file = JsonUtility.FromJson<SceneLinkFile>(ta.text);
                if (file == null || file.links == null)
                {
                    FlowTrace.Warn("Resolver", "scene-links.json parsed to no rows — catalog empty.");
                    return;
                }
                foreach (var link in file.links)
                {
                    if (link == null || string.IsNullOrEmpty(link.id)) continue;
                    if (_byId.ContainsKey(link.id))
                    {
                        FlowTrace.Warn("Resolver", $"duplicate scene-link id '{link.id}' — keeping the first.");
                        continue;
                    }
                    _byId[link.id] = link;
                    _all.Add(link);
                }
            });
        }

        // --------------------------------------------------------------------
        //  ISceneLinkResolver
        // --------------------------------------------------------------------
        public bool TryGetLink(string id, out SceneLink link)
        {
            if (!string.IsNullOrEmpty(id)) return _byId.TryGetValue(id, out link);
            link = null;
            return false;
        }

        public IReadOnlyList<SceneLink> AllLinks => _all;

        public void TravelTo(string linkId)
        {
            Guard.Try("Resolver", $"TravelTo '{linkId}'", () =>
            {
                if (!TryGetLink(linkId, out var link) || link == null)
                {
                    FlowTrace.Fail("Resolver", $"unknown scene-link id '{linkId}' — cannot travel.");
                    return;
                }

                FlowTrace.Step("Resolver",
                    $"Travelling {link.fromScene}->{link.toScene} ({link.type}) loadMode={link.loadMode}.");

                // GRACEFUL: the target space may not be baked/added yet (WO2 builds the
                // actual Outpost1/Dungeon/Outpost2 scenes). The link is inert until then.
                if (!Application.CanStreamedLevelBeLoaded(link.toScene))
                {
                    FlowTrace.Warn("Resolver",
                        $"target '{link.toScene}' not in Build Settings — link inert until the space is baked/added.");
                    return;
                }

                bool single = string.Equals(link.loadMode, "single", System.StringComparison.OrdinalIgnoreCase);

                // Locate the hero (Player tag, fallback HeroTarget) BEFORE the load.
                Transform hero = ResolveHero();
                if (hero == null)
                    FlowTrace.Warn("Resolver", "hero not found (Player/HeroTarget) before load — will re-resolve after load.");

                // SINGLE-LOAD HERO CARRY: a Single load destroys the old scene's roots
                // (incl. the hero); mark it DontDestroyOnLoad so the same instance survives.
                // Additive keeps the hero alive already — no DDOL needed.
                if (single && hero != null && hero.root != null)
                {
                    Object.DontDestroyOnLoad(hero.root.gameObject);
                    FlowTrace.Step("Resolver",
                        $"carry: DontDestroyOnLoad hero '{hero.root.name}' across Single load to '{link.toScene}'.");
                }

                SceneManager.LoadScene(link.toScene, single ? LoadSceneMode.Single : LoadSceneMode.Additive);
                StartCoroutine(RepositionAfterLoad(link, single));
            });
        }

        // --------------------------------------------------------------------
        //  REPOSITION (replicates SceneTransitionTrigger.RepositionPlayerAfterLoad).
        // --------------------------------------------------------------------
        private IEnumerator RepositionAfterLoad(SceneLink link, bool single)
        {
            // Let the target scene's roots (spawn markers) come alive before resolving.
            yield return new WaitForSeconds(0.15f);

            Guard.Try("Resolver", $"reposition into '{link.toScene}'", () =>
            {
                // Landing = the named spawn object if present, else the catalog fallback.
                Vector3 landing = link.targetPosition;
                if (!string.IsNullOrEmpty(link.spawnPoint))
                {
                    var spawnGo = GameObject.Find(link.spawnPoint);
                    if (spawnGo != null) landing = spawnGo.transform.position;
                    else FlowTrace.Warn("Resolver",
                        $"spawnPoint '{link.spawnPoint}' not found in '{link.toScene}' — using targetPosition {landing}.");
                }

                var loco = FindAnyObjectByType<HeroLocomotion>();
                if (loco == null)
                    FlowTrace.Warn("Resolver", $"no HeroLocomotion in '{link.toScene}' — cannot warp hero to {landing}.");
                loco?.WarpTo(landing);

                // NavMesh validation — same on-mesh oracle as the seam.
                bool onMesh = NavMesh.SamplePosition(landing, out NavMeshHit _, 2f, NavMesh.AllAreas);
                if (!onMesh)
                    FlowTrace.Fail("Resolver", $"SPAWN_OFF_MESH [{link.toScene}] — landing {landing} is not on the navmesh.");
                else
                    FlowTrace.Step("Resolver", $"ARRIVED {link.toScene} on-mesh @ {landing}.");

                // For a Single load, re-home the carried hero into the now-active scene.
                if (single && loco != null && loco.transform.root != null)
                    SceneManager.MoveGameObjectToScene(loco.transform.root.gameObject, SceneManager.GetActiveScene());

                // Optional fromScene unload (only meaningful for additive chained spaces).
                if (link.unloadFrom && !single)
                {
                    var from = SceneManager.GetSceneByName(link.fromScene);
                    if (from.isLoaded)
                    {
                        SceneManager.UnloadSceneAsync(link.fromScene);
                        FlowTrace.Step("Resolver", $"unloaded source scene '{link.fromScene}' after arrival.");
                    }
                }
            });
        }

        private static Transform ResolveHero()
        {
            var p = SafeFindWithTag("Player") ?? SafeFindWithTag("HeroTarget");
            return p != null ? p.transform : null;
        }

        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }
    }
}
