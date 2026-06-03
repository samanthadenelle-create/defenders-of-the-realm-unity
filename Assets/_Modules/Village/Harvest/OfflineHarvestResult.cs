// =============================================================================
// OfflineHarvestResult — the per-resource haul accrued while the player was away
// (WO-115). A plain value carrier raised to the welcome-back popup; it never
// touches GameState itself (the service banks; this only reports what it banked).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Resource buckets map 1:1 to MineNode.MineResource / the GameState wallet fields
// (Iron / Wood / Food / AetherCrystals) so the popup can render +N rows without
// re-deriving anything. Pet harvest (WO-111 Phase 4) folds into the same buckets
// when it lands — no shape change needed.
// =============================================================================

namespace DeNelle.Village
{
    /// <summary>
    /// The result of an offline-accrual claim: how much of each resource was
    /// banked, how long the player was away, and whether the cap clipped it.
    /// </summary>
    public sealed class OfflineHarvestResult
    {
        /// <summary>Iron banked this claim.</summary>
        public int Iron;
        /// <summary>Wood banked this claim.</summary>
        public int Wood;
        /// <summary>Food banked this claim (the retired "Stone" axis, repurposed — DEF-121).</summary>
        public int Food;
        /// <summary>Aether Crystals banked this claim.</summary>
        public int AetherCrystals;

        /// <summary>Real seconds since the last claim (BEFORE the cap clamp) — for the away-time line.</summary>
        public double AwaySeconds;
        /// <summary>True when <see cref="AwaySeconds"/> exceeded the offline cap (the gentle nudge).</summary>
        public bool WasCapped;

        /// <summary>Total units banked across every resource (popup-trigger gate: show only when &gt; 0).</summary>
        public int Total => Iron + Wood + Food + AetherCrystals;

        /// <summary>A zero haul — nothing accrued, no popup.</summary>
        public static OfflineHarvestResult None => new OfflineHarvestResult();

        /// <summary>Add an integer amount to the bucket for <paramref name="resource"/>.</summary>
        public void Add(MineResource resource, int amount)
        {
            if (amount <= 0) return;
            switch (resource)
            {
                case MineResource.Iron:          Iron += amount;           break;
                case MineResource.Wood:          Wood += amount;           break;
                case MineResource.Food:          Food += amount;          break;
                case MineResource.AetherCrystal: AetherCrystals += amount; break;
            }
        }
    }
}
