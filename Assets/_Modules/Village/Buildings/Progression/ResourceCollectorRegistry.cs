// =============================================================================
// ResourceCollectorRegistry — lookup by building id (farm / lumbermill / forge).
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Village.Buildings.Progression
{
    public static class ResourceCollectorRegistry
    {
        private static readonly Dictionary<string, ResourceCollector> s_byId =
            new Dictionary<string, ResourceCollector>(4);

        public static IReadOnlyCollection<ResourceCollector> All => s_byId.Values;

        public static void Register(ResourceCollector collector)
        {
            if (collector == null || string.IsNullOrEmpty(collector.BuildingId)) return;
            s_byId[collector.BuildingId] = collector;
        }

        public static void Unregister(ResourceCollector collector)
        {
            if (collector == null || string.IsNullOrEmpty(collector.BuildingId)) return;
            if (s_byId.TryGetValue(collector.BuildingId, out var cur) && cur == collector)
                s_byId.Remove(collector.BuildingId);
        }

        public static ResourceCollector Get(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            s_byId.TryGetValue(buildingId, out var c);
            return c;
        }
    }
}