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