// =============================================================================
// HarvestSourceRegistry — O(1) registry of live harvest sources (WO-656).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>Tracks all active <see cref="IHarvestSource"/> instances.</summary>
    public static class HarvestSourceRegistry
    {
        private static readonly List<IHarvestSource> s_sources = new List<IHarvestSource>(8);

        public static IReadOnlyList<IHarvestSource> Active => s_sources;

        public static void Register(IHarvestSource source)
        {
            if (source == null || s_sources.Contains(source)) return;
            s_sources.Add(source);
            FlowTrace.Step("Harvest", $"register id={source.SourceId} pending={source.PendingAmount:F0}/{source.Capacity:F0}");
        }

        public static void Unregister(IHarvestSource source)
        {
            if (source == null) return;
            if (s_sources.Remove(source))
                FlowTrace.Step("Harvest", $"unregister id={source.SourceId}");
        }
    }
}