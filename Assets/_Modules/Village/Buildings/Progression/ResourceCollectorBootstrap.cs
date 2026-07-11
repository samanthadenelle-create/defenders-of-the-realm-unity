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
            var existing = GameObject.Find(HostName);
            if (existing != null) return;
            var host = new GameObject(HostName);
            Object.DontDestroyOnLoad(host);
            FlowTrace.Step("Harvest", "ResourceCollectorHost DDOL created");
        }

        private static void WireScene(Scene scene)
        {
            if (!scene.IsValid()) return;

            // WO-673 (ff.strategicplacement): under strategic placement, placed collector_*
            // structures carry their OWN ResourceCollector via StructureFactory's
            // "ResourceCollector" behavior case — the hub-storefront NAME-wire stands down
            // entirely (ONE owner per concern, review G-B). Only the DDOL logical fallback
            // below remains, for economy continuity while NO collector of an id exists
            // anywhere (EnsureFallbackCollector is registry-gated, so a placed/migrated
            // collector suppresses it). Flag OFF = today's name-wire, byte-identical.
            if (DeNelle.Core.FeatureFlags.StrategicPlacement)
            {
                FlowTrace.Once("Harvest", "wo673-namewire-standdown",
                    "strategic placement ON — storefront name-wire stood down (fallback-only)");
            }
            else
            {
                EnsureCollectorOn(ResourceBuildingProgression.FarmId, "Windmill_Food_Storefront");
                EnsureCollectorOn(ResourceBuildingProgression.LumbermillId, "Lumbermill_Wood_Storefront");
                EnsureCollectorOn(ResourceBuildingProgression.ForgeId, "Forge_Armor_Storefront");
            }

            // Logical fallbacks when hub storefronts are absent (OuterWorld / Village2).
            EnsureFallbackCollector(ResourceBuildingProgression.FarmId);
            EnsureFallbackCollector(ResourceBuildingProgression.LumbermillId);
            EnsureFallbackCollector(ResourceBuildingProgression.ForgeId);
        }

        private static void EnsureCollectorOn(string buildingId, string storefrontName)
        {
            var found = GameObject.Find(storefrontName);
            if (found == null) return;

            // WO-673 G-B (census-proven double-spawn risk): the registry is last-write-wins
            // per id (ResourceCollectorRegistry.Register overwrites), so name-wiring a second
            // collector while another live one (e.g. a player-placed / migrated collector_*)
            // already owns this id would leave TWO ResourceCollectors sharing one id, with
            // registration order deciding which one the economy/damage systems see. Consult
            // the registry first and SKIP unless the registered owner is this storefront's
            // own collector (idempotent per-scene-load re-wire stays allowed).
            var existing = ResourceCollectorRegistry.Get(buildingId);
            if (existing != null && existing.gameObject != found)
            {
                FlowTrace.Step("Harvest",
                    $"collector '{buildingId}' already owned by '{existing.gameObject.name}' — " +
                    $"storefront name-wire skipped for '{storefrontName}' (no double-spawn)");
                return;
            }

            var col = found.GetComponent<ResourceCollector>();
            if (col == null) col = found.AddComponent<ResourceCollector>();
            col.Configure(buildingId);

            // Slim hitbox for siege contact (non-trigger, Default layer).
            if (found.GetComponent<Collider>() == null)
            {
                var box = found.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 3f, 4f);
                box.center = new Vector3(0f, 1.5f, 0f);
                box.isTrigger = false;
            }

            // WO-665a: diegetic CoC-style fill stack (separate presentation component).
            // Only the visible storefront hosts get it — the origin-parked DDOL fallback
            // collectors are logical-only (CollectorStackView.Attach self-skips them).
            CollectorStackView.Attach(col);

            FlowTrace.Once("Harvest", $"wire-{buildingId}",
                $"collector wired building={buildingId} storefront={storefrontName}");
        }

        private static void EnsureFallbackCollector(string buildingId)
        {
            if (ResourceCollectorRegistry.Get(buildingId) != null) return;

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
                $"collector fallback host for building={buildingId}");
        }
    }
}