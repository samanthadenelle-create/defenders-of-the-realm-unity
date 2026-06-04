// =============================================================================
// CrystalGrade — the rarity band of a Crystal-type resource (WO-144 §1a, minimal).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World (beside ZoneManager /
// RegionZone — the shared region/danger model the grade gate reads).
//
// SCOPE NOTE (reconciliation): this is the LEAN slice of WO-144 that WO-153 (the
// world Crystal Mine) hard-depends on — the orthogonal grade enum + the pure
// danger->grade mapping. It is INTENTIONALLY *not* the full WO-144 build: there is
// no per-grade CrystalLedger, no SaveSchema round-trip, no CrystalEconomy overload
// here. Crystals still bank through the single AetherCrystals total today; the
// grade is a forward-compatible classification a node yields. When WO-144 lands its
// ledger, it consumes THIS enum + THIS helper (no fork) and adds the persistence.
//
// Grade is ORTHOGONAL to ResourceType (a crystal is always ResourceType.Crystal;
// CrystalGrade is its rarity), per WO-144's CRITICAL section — do NOT add per-grade
// ResourceType members. Order = rarity = danger.
// =============================================================================
namespace DeNelle.Core.World
{
    /// <summary>
    /// Quality grade of a Crystal-type resource. Orthogonal to ResourceType:
    /// a crystal is always the Crystal resource; <see cref="CrystalGrade"/> is its
    /// rarity band. Higher grades only come from higher-danger regions
    /// (see <see cref="CrystalRegion.TopGradeFor"/>). Order = rarity = danger.
    /// </summary>
    public enum CrystalGrade
    {
        /// <summary>Common baseline crystal — harvestable in any region (today's AetherCrystals).</summary>
        Aether = 0,
        /// <summary>Low/neutral-danger regions (Goldfields / Stoneback) — modest reward.</summary>
        Verdant = 1,
        /// <summary>Heavy-danger region (Mirewood) — risky, richer.</summary>
        Mire = 2,
        /// <summary>The front line (Ashwood) — rarest, richest; fortify-it-or-lose-it.</summary>
        Wraith = 3,
    }

    /// <summary>
    /// Pure danger -> crystal-grade mapping (WO-144 §2). The single source of truth
    /// for "which grade does a region's crystal yield" so the danger=reward spine is
    /// not hand-tuned in two places. Read by the world Crystal Mine (WO-153) and,
    /// later, by the WO-143 raid-pressure scaler. No UnityEngine, headless-safe.
    /// </summary>
    public static class CrystalRegion
    {
        /// <summary>
        /// The top crystal grade a region of the given danger tier may yield. Tiers
        /// follow <see cref="ZoneManager"/> (Village 0, Goldfields 1, Stoneback 2,
        /// Mirewood 3, Ashwood 4). A region yields its tier grade and any lower grade,
        /// so common Aether is available anywhere and the rare grades concentrate at the
        /// dangerous edges. Clamps gracefully for any out-of-range tier.
        /// </summary>
        public static CrystalGrade TopGradeFor(int dangerTier)
        {
            if (dangerTier <= 0) return CrystalGrade.Aether;   // Village / safe
            if (dangerTier <= 2) return CrystalGrade.Verdant;  // Goldfields (1) / Stoneback (2)
            if (dangerTier == 3) return CrystalGrade.Mire;     // Mirewood
            return CrystalGrade.Wraith;                        // Ashwood (4+) — the front line
        }
    }
}
