// =============================================================================
// EchoBonusCalculator -- the shared Echo specialization MATH (WO-738, SERVICE lane).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// PURE STATIC, null/absent-safe. The ONE place the specialization curve lives so
// EchoService (harvest income + dump split + silo cadence) and the roster/picker UI
// all read the SAME numbers -- no per-system math scattered. Reads:
//   - EchoAssignments (LaneOf / LevelOf per owned echo index 0..EchoCount-1)
//   - EchoBalanceCatalog (the owner-tunable knobs: MaxLevel, PreferredLaneMatchBonus,
//     BaseContributionPerEcho, SixSetBonusGlobalHarvest, PerLevelBonus, BaseRateFor)
//   - EchoRosterCatalog (the fixed identity table: PreferredLane, HarvestResource, Id)
//
// SAFETY: no GameState / no service => neutral (multipliers 1.0, weights fall back to
// even Wood/Iron/Food). Nothing here mutates state; Recompute() is the ONLY writer and
// it only pushes into the Core EchoLaneBonuses holder (Village writes, hosts read).
//
// COMPOSITION LAW (no double-count): the WO-709 count-quadratic spine stays owned by
// EchoService.GlobalHarvestMultiplier (== EchoCount). AggregateHarvestMultiplier FOLDS
// that spine in ONCE and layers the specialization factor on top, so EchoService
// multiplies by AggregateHarvestMultiplier() INSTEAD OF GlobalHarvestMultiplier (never
// both). See EchoService.RatePerSecond.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>A small per-echo specialization readout for the roster/picker UI (WO-738).</summary>
    public struct EchoBonusReadout
    {
        /// <summary>The functional lane this echo is assigned to.</summary>
        public LaneType Lane;
        /// <summary>The echo's level (1..MaxLevel).</summary>
        public int Level;
        /// <summary>This echo's additive bonus to its assigned lane, as a PERCENT (e.g. 18f == +18%).
        /// Idle contributes nothing (0). Composed of the base-to-all floor + the element match
        /// bonus (when preferred) + the per-level increment.</summary>
        public float BonusPct;
        /// <summary>True when this echo's element/preferred-lane matches its assigned lane (earns the match bonus).</summary>
        public bool PreferredMatch;
    }

    /// <summary>
    /// The shared Echo specialization math (WO-738). Pure functions over the persisted
    /// assignment + the tunable balance + the fixed roster identity. See file header for
    /// the composition law (spine folded in ONCE by AggregateHarvestMultiplier).
    /// </summary>
    public static class EchoBonusCalculator
    {
        // =====================================================================
        //  HARVEST -- the applied total multiplier (folds the count spine).
        // =====================================================================

        /// <summary>
        /// The TOTAL multiplier applied to harvest income -- the value EchoService.RatePerSecond
        /// multiplies by IN PLACE OF GlobalHarvestMultiplier (do not apply both).
        ///
        /// FORMULA (owner-tunable via echoes-balance.json):
        ///   AggregateHarvestMultiplier
        ///     = EchoCount                                            // the WO-709 count-quadratic SPINE
        ///       x ( 1
        ///           + Σ over echoes ASSIGNED to Harvest of
        ///               [ BaseContributionPerEcho                    // "no echo wasted" floor
        ///                 + (PreferredLane == Harvest ? PreferredLaneMatchBonus : 0)   // element match
        ///                 + PerLevelBonus * (level - 1) ]            // level growth
        ///           + (all 6 owned ? SixSetBonusGlobalHarvest : 0) ) // the 6-of-6 set bonus
        ///
        /// Neutral (1.0) when no GameState/service exists. With defaults, one matched Lv1 Harvest
        /// echo adds 1 + 0.15 + 0.75 = 1.90x the specialization factor on top of the count spine.
        /// </summary>
        public static float AggregateHarvestMultiplier()
        {
            int count = OwnedCount();
            if (count <= 0) return 1f;   // no service -> neutral

            float specSum = 0f;
            for (int i = 0; i < count; i++)
            {
                if (LaneTypeOf(EchoAssignments.LaneOf(i)) != LaneType.Harvest) continue;
                specSum += LaneContribution(i, LaneType.Harvest);
            }

            if (AllOwned(count))
                specSum += EchoBalanceCatalog.SixSetBonusGlobalHarvest;

            return count * (1f + specSum);
        }

        /// <summary>
        /// Per-resource split WEIGHTS for DumpSilos (WO-738): each Harvest-assigned echo contributes
        /// its (BaseRateFor(id) * level) weight to its element's HarvestResource; a Harvest echo with
        /// a null HarvestResource spreads evenly across Wood/Iron/Food. Non-Harvest echoes contribute
        /// nothing. If the total is 0 (no harvest echoes), falls back to an even Wood/Iron/Food split
        /// so the caller never divides by zero. AetherCrystal is never weighted (premium currency).
        /// </summary>
        public static Dictionary<ResourceType, float> HarvestResourceWeights()
        {
            var weights = new Dictionary<ResourceType, float>
            {
                { ResourceType.Wood, 0f },
                { ResourceType.Iron, 0f },
                { ResourceType.Food, 0f },
            };

            int count = OwnedCount();
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                if (LaneTypeOf(EchoAssignments.LaneOf(i)) != LaneType.Harvest) continue;

                var entry = EchoRosterCatalog.ByIndex(i);
                if (entry == null) continue;

                int level = EchoAssignments.LevelOf(i);
                float w = Mathf.Max(0f, EchoBalanceCatalog.BaseRateFor(entry.Id)) * Mathf.Max(1, level);
                if (w <= 0f) continue;

                if (entry.HarvestResource.HasValue && weights.ContainsKey(entry.HarvestResource.Value))
                {
                    weights[entry.HarvestResource.Value] += w;
                }
                else
                {
                    // Null / non-spendable resource -> spread evenly across the three build harvestables.
                    float third = w / 3f;
                    weights[ResourceType.Wood] += third;
                    weights[ResourceType.Iron] += third;
                    weights[ResourceType.Food] += third;
                }
                total += w;
            }

            if (total <= 0f)
            {
                // No harvest echoes -> even split (never divide by zero downstream).
                weights[ResourceType.Wood] = 1f;
                weights[ResourceType.Iron] = 1f;
                weights[ResourceType.Food] = 1f;
            }

            return weights;
        }

        // =====================================================================
        //  NON-HARVEST lanes -- passive multipliers stored on EchoLaneBonuses.
        // =====================================================================

        /// <summary>
        /// The passive multiplier for a Crafting/Defense/Exploration lane (WO-738):
        ///   1 + Σ over echoes ASSIGNED to that lane of
        ///       [ BaseContributionPerEcho + (preferred+element match ? PreferredLaneMatchBonus : 0)
        ///         + PerLevelBonus * (level - 1) ].
        /// Harvest/Idle return 1.0 here (Harvest is handled by AggregateHarvestMultiplier; Idle is a no-op).
        /// </summary>
        public static float LaneMultiplier(LaneType lane)
        {
            if (lane == LaneType.Idle) return 1f;

            int count = OwnedCount();
            if (count <= 0) return 1f;

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                if (LaneTypeOf(EchoAssignments.LaneOf(i)) != lane) continue;
                sum += LaneContribution(i, lane);
            }
            return 1f + sum;
        }

        // =====================================================================
        //  UI readout.
        // =====================================================================

        /// <summary>A per-echo specialization readout (lane, level, bonus %, matched) for the roster/picker UI.</summary>
        public static EchoBonusReadout ReadoutFor(int echoIndex)
        {
            var lane = LaneTypeOf(EchoAssignments.LaneOf(echoIndex));
            int level = EchoAssignments.LevelOf(echoIndex);
            bool match = PreferredMatches(echoIndex, lane);
            float bonus = lane == LaneType.Idle ? 0f : LaneContribution(echoIndex, lane);
            return new EchoBonusReadout
            {
                Lane = lane,
                Level = level,
                BonusPct = bonus * 100f,   // fraction -> percent for display ("+18%")
                PreferredMatch = match,
            };
        }

        // =====================================================================
        //  Recompute -- the ONLY writer; pushes passive lane mults into Core.
        // =====================================================================

        /// <summary>
        /// Recompute the three passive lane multipliers + the harvest total and push them into the
        /// Core <see cref="EchoLaneBonuses"/> holder (Village writes, hosts read). Idempotent -- safe
        /// to call on every assignment/count change event. HarvestBonusMult mirrors the applied
        /// AggregateHarvestMultiplier so a HUD reading the contract sees the same number EchoService
        /// applies (EchoService itself reads AggregateHarvestMultiplier() LIVE -- no double-apply).
        /// [Flow:Echo].
        /// </summary>
        public static void Recompute()
        {
            EchoLaneBonuses.Reset();

            float harvest = AggregateHarvestMultiplier();
            float crafting = LaneMultiplier(LaneType.Crafting);
            float defense = LaneMultiplier(LaneType.Defense);
            float exploration = LaneMultiplier(LaneType.Exploration);

            EchoLaneBonuses.HarvestBonusMult = harvest;
            EchoLaneBonuses.CraftingMult = crafting;
            EchoLaneBonuses.DefenseMult = defense;
            EchoLaneBonuses.ExplorationMult = exploration;

            FlowTrace.Once("Echo", "bonus-recompute-first",
                "EchoBonusCalculator.Recompute: first pass -- passive lane multipliers populated onto EchoLaneBonuses (hosts read when they land).");
            FlowTrace.Step("Echo",
                $"Recompute: harvestx{harvest:0.###} (applied), craftx{crafting:0.###}, " +
                $"defx{defense:0.###}, explorationx{exploration:0.###} (owned {OwnedCount()}).");
        }

        // =====================================================================
        //  Internals.
        // =====================================================================

        /// <summary>The additive specialization contribution (FRACTION) of one echo to a given lane:
        /// BaseContributionPerEcho + (preferred match ? PreferredLaneMatchBonus : 0) + PerLevelBonus*(level-1).</summary>
        private static float LaneContribution(int echoIndex, LaneType lane)
        {
            int level = EchoAssignments.LevelOf(echoIndex);
            float c = EchoBalanceCatalog.BaseContributionPerEcho;
            if (PreferredMatches(echoIndex, lane)) c += EchoBalanceCatalog.PreferredLaneMatchBonus;
            c += EchoBalanceCatalog.PerLevelBonus * Mathf.Max(0, level - 1);
            return c;
        }

        /// <summary>True when the echo's identity PreferredLane matches the lane it's evaluated against.</summary>
        private static bool PreferredMatches(int echoIndex, LaneType lane)
        {
            if (lane == LaneType.Idle) return false;
            var entry = EchoRosterCatalog.ByIndex(echoIndex);
            return entry != null && entry.PreferredLane == lane;
        }

        /// <summary>Owned echo count. 0 when no GameState/service exists (=> neutral bonuses).</summary>
        private static int OwnedCount()
        {
            if (EchoService.Instance != null) return EchoService.Instance.EchoCount;
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return 0;
            return Mathf.Max(1, svc.State.EchoCount);
        }

        /// <summary>True when the full canonical roster is owned (the 6-of-6 set bonus condition).</summary>
        private static bool AllOwned(int count)
        {
            return count >= EchoRosterCatalog.Count;
        }

        /// <summary>Map an EchoAssignments lane token to the LaneType enum (unknown/legacy -> Idle-safe).</summary>
        private static LaneType LaneTypeOf(string laneToken)
        {
            if (string.IsNullOrEmpty(laneToken)) return LaneType.Idle;
            switch (laneToken)
            {
                case EchoAssignments.LaneHarvest:     return LaneType.Harvest;
                case EchoAssignments.LaneCrafting:    return LaneType.Crafting;
                case EchoAssignments.LaneDefense:     return LaneType.Defense;
                case EchoAssignments.LaneExploration: return LaneType.Exploration;
                default:                              return LaneType.Idle;
            }
        }
    }
}
