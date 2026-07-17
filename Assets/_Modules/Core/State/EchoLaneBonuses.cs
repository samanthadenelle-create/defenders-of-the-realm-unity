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
// OWNERSHIP (phase split): the VILLAGE layer (EchoService / a bonus-recompute pass)
// WRITES these values in phase 2 from the per-echo lane assignment + element match
// + cross/set bonuses. The HOST systems READ them when they land:
//   CraftingMult    -> Forge / crafting yield+speed
//   DefenseMult     -> the OFFLINE async city-raid resolver (echoes NEVER fight live)
//   ExplorationMult -> the dungeon-run reward grant (dungeon loot ONLY)
//   HarvestBonusMult-> EchoService.RatePerSecond / DumpSilos capped total bonus
//
// This is a Core-owned STATIC holder (Core may not reference Village, so Village
// writes INTO Core, hosts read FROM Core -- the GameModifiers/CoreServices pattern).
// Pure data: no MonoBehaviour, no Village refs, no side effects. Not persisted --
// it is RECOMPUTED from the persisted EchoLanes assignment on load (phase 2), the
// same way GameModifiers is recompiled from BuildingTiers rather than saved.
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
