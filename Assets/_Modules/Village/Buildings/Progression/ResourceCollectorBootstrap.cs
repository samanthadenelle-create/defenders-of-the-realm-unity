// =============================================================================
// ResourceCollectorBootstrap — wire typed hub collectors + DDOL fallbacks (WO-664).
// No scene hand-edit — finds storefronts by name or spawns logical hosts.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    public static class ResourceCollectorBootstrap
    {
        private const string HostName = "ResourceCollectorHost";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureHost();
            WireScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => WireScene(scene);

        private static void EnsureHost()
        {
            var host = GameObject.Find(HostName);
            if (host == null)
            {
                host = new GameObject(HostName);
                Object.DontDestroyOnLoad(host);
                FlowTrace.Step("Harvest", "ResourceCollectorHost DDOL created");
            }
            // WO-900 §4 — the AMBIENT tell needs a publisher, and this DDOL host is the one
            // object that already outlives every scene load the collectors do. Additive and
            // idempotent: an existing host (a re-entered scene) just keeps the component it has.
            if (host.GetComponent<CollectorStatusPublisher>() == null)
                host.AddComponent<CollectorStatusPublisher>();
        }

        private static void WireScene(Scene scene)
        {
            if (!scene.IsValid()) return;

            // WO-673 (always on — WO-682 removed ff.strategicplacement): placed collector_*
            // structures carry their OWN ResourceCollector via StructureFactory's
            // "ResourceCollector" behavior case — the old hub-storefront NAME-wire is
            // permanently stood down (ONE owner per concern, review G-B). Only the DDOL
            // logical fallback below remains, for economy continuity while NO collector of
            // an id exists anywhere (EnsureFallbackCollector is registry-gated, so a
            // placed/migrated collector suppresses it).
            FlowTrace.Once("Harvest", "wo673-namewire-standdown",
                "storefront name-wire stood down (strategic placement always on; fallback-only)");

            // Logical fallbacks when hub storefronts are absent (OuterWorld / Village2).
            EnsureFallbackCollector(ResourceBuildingProgression.FarmId);
            EnsureFallbackCollector(ResourceBuildingProgression.LumbermillId);
            EnsureFallbackCollector(ResourceBuildingProgression.ForgeId);
        }

        // (WO-682: the flag-off storefront NAME-wire helper EnsureCollectorOn was deleted
        // with ff.strategicplacement — placed collector_* structures own their collectors
        // via StructureFactory; only the registry-gated logical fallback below remains.)

        /// <summary>
        /// Stand up the DDOL logical fallback collector for <paramref name="buildingId"/> - but ONLY
        /// for a building the player has actually built.
        /// <para>
        /// ! P0, WO-859 sec.2 R4 - THE BACK DOOR COMMIT 35485f31 DID NOT CLOSE. That commit gated the
        /// harvest RULE (<see cref="ResourceBuildingHarvester.MayHarvest"/>) on the persisted WO-834
        /// ever-built ledger, but <see cref="MayHarvest"/> returns true the instant a LIVE collector
        /// is registered (`:236`) - and this method used to create one for farm, lumbermill and
        /// forge UNCONDITIONALLY, consulting only the registry. So the rule was gated and the
        /// gate was bypassed.
        /// </para>
        /// <para>
        /// PROVEN HEADLESS BEFORE THE FIX (sec.12 - captured, not inferred), blank-town run,
        /// everBuiltStructureIds = []:
        /// <code>
        /// [Flow:Harvest] collector fallback host for building=farm
        /// [Flow:Harvest] existence gate OPEN for 'farm' (liveCollector=yes, everBuilt=[&lt;empty&gt;]) - this id may tick
        /// [Flow:Harvest] accrue-pending building=... pending=87/600
        /// </code>
        /// A town with nothing in it was earning again. The second consequence was worse: hub
        /// collectors unregister on unload, <see cref="WireScene"/> fires for the DUNGEON, the
        /// fallbacks are re-created there, and the harvester DOES bootstrap in dungeons (they are
        /// not enemy-owned) - so full town income accrued while the player was off in a dungeon,
        /// which is the exact defect the removed direct-grant was blamed for, surviving by another
        /// route.
        /// </para>
        /// The gate resolves catalog ids through
        /// <see cref="ResourceBuildingHarvester.CatalogIdsForBuilding"/> - the SAME resolution the
        /// harvest gate uses - so the two can never disagree about what "built" means.
        /// </summary>
        private static void EnsureFallbackCollector(string buildingId)
        {
            if (ResourceCollectorRegistry.Get(buildingId) != null) return;

            if (!HasEverBuilt(buildingId))
            {
                FlowTrace.Once("Harvest", $"fallback-skipped-{buildingId}",
                    $"NO fallback collector for '{buildingId}' - it is not in the WO-834 ever-built ledger " +
                    "(a live collector would open the existence gate, so an unbuilt building must never get one).");
                return;
            }

            var host = GameObject.Find(HostName);
            if (host == null) return;

            var childName = "Collector_" + buildingId;
            Transform child = host.transform.Find(childName);
            GameObject go;
            if (child != null)
                go = child.gameObject;
            else
            {
                go = new GameObject(childName);
                go.transform.SetParent(host.transform, false);
            }

            var col = go.GetComponent<ResourceCollector>();
            if (col == null) col = go.AddComponent<ResourceCollector>();
            col.Configure(buildingId);
            if (go.GetComponent<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(3f, 2f, 3f);
                box.isTrigger = false;
            }
            FlowTrace.Once("Harvest", $"fallback-{buildingId}",
                $"collector fallback host for building={buildingId} (ever-built ledger confirms it exists)");
        }

        /// <summary>
        /// True when any COLLECTOR catalog id standing for <paramref name="buildingId"/> is in the
        /// persisted WO-834 <c>GameState.EverBuiltStructureIds</c> ledger. Null-safe by design: no
        /// GameStateService / no state (Title, a headless boot before the save loads) reads as
        /// "nothing built", which is the SAFE answer - it withholds a fallback rather than opening
        /// the income gate on an unproven town.
        /// </summary>
        private static bool HasEverBuilt(string buildingId)
        {
            var state = DeNelle.Core.State.GameStateService.Instance?.State;
            if (state == null) return false;

            var catalogIds = ResourceBuildingHarvester.CatalogIdsForBuilding(buildingId);
            if (catalogIds == null) return false;
            for (int i = 0; i < catalogIds.Count; i++)
                if (state.HasEverBuilt(catalogIds[i])) return true;
            return false;
        }
    }
}
