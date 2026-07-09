// =============================================================================
// ResourceCollectorService — Collect All at Heart (pipe home, WO-663).
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Sweeps every live collector pending into the central wallet.</summary>
    public static class ResourceCollectorService
    {
        /// <summary>
        /// Collect All: hub collectors + echo silo dump in one CoC swoosh.
        /// Returns total integer resources banked.
        /// </summary>
        public static int CollectAll()
        {
            using var _ = FlowTrace.Enter("Harvest", "CollectAll");
            int total = 0;
            foreach (var c in ResourceCollectorRegistry.All)
            {
                if (c == null) continue;
                total += c.Collect();
            }

            var echo = EchoService.Instance;
            if (echo != null)
                total += echo.DumpSilos();

            FlowTrace.Step("Harvest", $"collect-all total-banked={total}");
            return total;
        }

        /// <summary>Sum of pending across all collectors (HUD readout).</summary>
        public static int TotalPending()
        {
            int sum = 0;
            foreach (var c in ResourceCollectorRegistry.All)
            {
                if (c != null) sum += (int)System.Math.Floor(c.PendingAmount);
            }
            return sum;
        }

        /// <summary>Max fill fraction across collectors (siege telegraph).</summary>
        public static float MaxFillFraction()
        {
            float max = 0f;
            foreach (var c in ResourceCollectorRegistry.All)
            {
                if (c != null && c.FillFraction > max) max = c.FillFraction;
            }
            return max;
        }
    }
}