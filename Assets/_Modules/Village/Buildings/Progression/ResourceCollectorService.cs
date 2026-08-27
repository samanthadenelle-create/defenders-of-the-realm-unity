// =============================================================================
// ResourceCollectorService - Collect All at Heart (pipe home, WO-663).
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.Ops;
using DeNelle.Core.UI;
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

            // WO-1243 OPERATOR KILL SWITCH: farming.
            // Gated HERE and not at the chip tap because CollectAll has more than one
            // caller (the collectors chip AND AutoHarvestService), and a gate only the
            // button honours is no gate at all. Refuses BEFORE the first c.Collect(),
            // so nothing is banked and no pending is consumed.
            // !! This is the COURTESY half. The seal itself is enforced server side -
            // see api/_lib/maintenance.js. Fail-OPEN: with the table unreachable this
            // returns false and farming carries on (owner ruling 2026-08-27).
            if (MaintenanceCatalog.Refuses(MaintenanceArea.Farming, "collect-all", out string sealedMsg))
            {
                ElarionUiKit.ShowToast(sealedMsg, ElarionUiKit.ToastTone.Info);
                return 0;
            }

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