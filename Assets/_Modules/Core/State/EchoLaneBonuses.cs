// =============================================================================
// EchoLaneBonuses -- the Core-side passive per-lane multiplier contract (WO-738).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The FOUNDATION seam for WO-738's non-Harvest lanes (Crafting / Defense /
// Exploration) plus a Harvest bonus. Mirrors the GameModifiers design law: every
// multiplier DEFAULTS to a no-op (1.0), so an untouched contract changes nothing;
// a host multiplies its yield by the relevant field and a missing/never-written
// value is identity.
//
// OWNERSHIP: the VILLAGE layer (EchoBonusCalculator.Recompute, event-driven) WRITES
// all four values from the per-echo assignment + affinity match + pair/set bonuses.
// CONSUMPTION STATUS (verified from code, WO-830 pass): ALL FOUR are currently
// WRITE-ONLY MIRRORS -- EchoService.RatePerSecond reads
// EchoBonusCalculator.AggregateHarvestMultiplier() LIVE (never this holder), so
// HarvestBonusMult is a diagnostic mirror of the APPLIED aggregate (it may carry the
// WO-830 hidden tri-synergy term; safe precisely BECAUSE nothing player-facing reads
// it). The other three are the FORWARD SEAM, pending host wiring:
//   HarvestBonusMult-> MIRROR (unconsumed): the applied harvest aggregate, for hosts/diagnostics
//   CraftingMult    -> STUB (unconsumed): intended for Forge / crafting yield+speed
//   DefenseMult     -> STUB (unconsumed): intended for the OFFLINE async city-raid resolver
//   ExplorationMult -> STUB (unconsumed): intended for the dungeon-run reward grant
//
// This is a Core-owned STATIC holder (Core may not reference Village, so Village
// writes INTO Core, hosts read FROM Core -- the GameModifiers/CoreServices pattern).
// Pure data: no MonoBehaviour, no Village refs, no side effects. Not persisted --
// it is RECOMPUTED from the persisted EchoLanes assignment on load (live since
// WO-738), the same way GameModifiers is recompiled from BuildingTiers rather than saved.
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// Passive per-lane Echo multipliers (WO-738). All default to 1.0 (no-op).
    /// Village writes; hosts read. See the file header for the ownership split.
    /// </summary>
    public static class EchoLaneBonuses
    {
        /// <summary>Harvest-lane TOTAL faucet bonus (capped). 1.0 = no bonus. Read by EchoService.</summary>
        public static float HarvestBonusMult { get; set; } = 1f;

        /// <summary>Crafting-lane yield/speed multiplier. 1.0 = no-op. Read by the Forge when wired.</summary>
        public static float CraftingMult { get; set; } = 1f;

        /// <summary>Defense-lane passive buff for the OFFLINE async raid resolver. 1.0 = no-op.</summary>
        public static float DefenseMult { get; set; } = 1f;

        /// <summary>Exploration-lane dungeon-loot multiplier. 1.0 = no-op. Read at the dungeon reward grant.</summary>
        public static float ExplorationMult { get; set; } = 1f;

        /// <summary>Restore every multiplier to its no-op (1.0) default. Called before a recompute.</summary>
        public static void Reset()
        {
            HarvestBonusMult = 1f;
            CraftingMult = 1f;
            DefenseMult = 1f;
            ExplorationMult = 1f;
        }
    }
}
