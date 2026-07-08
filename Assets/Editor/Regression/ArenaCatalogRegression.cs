// =============================================================================
// ArenaCatalogRegression — headless oracle for the seeded Arena data spines:
// ArenaCatalog (3 opponents) + ArenaDefenseCatalog (6 defenders + point pool).
// -----------------------------------------------------------------------------
// Pure data + logic — loads the REAL static catalogs (lazy Build()) and asserts the
// invariants the Arena raid/setup loops depend on:
//   OPPONENTS (ArenaCatalog.All):
//     1. exactly 3, unique non-empty ids, resolvable via Get(id).
//     2. Wager ascends (50/100/200) and WinPurse == Wager*2 (the stake-doubled purse).
//     3. Tier + Threat + GuardCount are positive and non-decreasing with wager.
//     4. every BaseRecipe realizes to a NON-EMPTY fort (an empty recipe = no defender
//        base = the instant-win / empty-Arena class of bug).
//   DEFENDERS (ArenaDefenseCatalog.All):
//     5. exactly 6, unique non-empty ids, PointCost > 0, resolvable via Get(id).
//     6. Unit defenders carry a UnitClass; Structure defenders carry a BehaviorId.
//     7. DefensePointPool == 50 and the CHEAPEST defender is affordable on an empty
//        layout (a day-one defender can always be placed).
//
// NO PlayMode, NO GameState. Mirrors MonetizationCovenantRegression:
// public static bool Run(out string reason).
// =============================================================================
using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Village.Arena;

namespace DeNelle.Editor
{
    public static class ArenaCatalogRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // ---- OPPONENTS ----------------------------------------------------
            var opps = ArenaCatalog.All;
            if (opps == null || opps.Count == 0)
            { reason = "ARENA CATALOG FAIL: ArenaCatalog.All is empty (seed build broke)"; return false; }
            if (opps.Count != 3)
                failures.Add($"expected 3 seeded opponents, found {opps.Count}");

            var oppIds = new HashSet<string>();
            long prevWager = long.MinValue;
            int prevThreat = int.MinValue;
            foreach (var o in opps)
            {
                if (o == null) { failures.Add("null opponent entry"); continue; }
                if (string.IsNullOrEmpty(o.Id)) { failures.Add("opponent with null/empty id"); }
                else if (!oppIds.Add(o.Id)) failures.Add($"duplicate opponent id '{o.Id}'");
                else if (ArenaCatalog.Get(o.Id) != o) failures.Add($"ArenaCatalog.Get('{o.Id}') did not round-trip");

                if (o.WinPurse != o.Wager * 2L)
                    failures.Add($"opponent '{o.Id}' WinPurse {o.WinPurse} != Wager*2 ({o.Wager * 2L})");
                if (o.Wager <= prevWager) failures.Add($"opponent '{o.Id}' wager {o.Wager} did not ascend (prev {prevWager})");
                prevWager = o.Wager;
                if (o.Threat < prevThreat) failures.Add($"opponent '{o.Id}' threat {o.Threat} decreased (prev {prevThreat})");
                prevThreat = o.Threat;
                if (o.Tier <= 0 || o.Threat <= 0 || o.GuardCount <= 0)
                    failures.Add($"opponent '{o.Id}' has non-positive tier/threat/guards ({o.Tier}/{o.Threat}/{o.GuardCount})");

                int recipeCount = o.BaseRecipe != null ? o.BaseRecipe.Count : 0;
                if (recipeCount == 0)
                    failures.Add($"opponent '{o.Id}' BaseRecipe is EMPTY — no defender fort (instant-win / empty-Arena bug class)");
            }

            // ---- DEFENDERS ----------------------------------------------------
            var defs = ArenaDefenseCatalog.All;
            if (defs == null || defs.Count == 0)
            { failures.Add("ArenaDefenseCatalog.All is empty (seed build broke)"); }
            else
            {
                if (defs.Count != 6)
                    failures.Add($"expected 6 seeded defenders, found {defs.Count}");

                var defIds = new HashSet<string>();
                ArenaDefenseDef cheapest = null;
                foreach (var d in defs)
                {
                    if (d == null) { failures.Add("null defender entry"); continue; }
                    if (string.IsNullOrEmpty(d.Id)) failures.Add("defender with null/empty id");
                    else if (!defIds.Add(d.Id)) failures.Add($"duplicate defender id '{d.Id}'");
                    else if (ArenaDefenseCatalog.Get(d.Id) != d) failures.Add($"ArenaDefenseCatalog.Get('{d.Id}') did not round-trip");

                    if (d.PointCost <= 0) failures.Add($"defender '{d.Id}' PointCost {d.PointCost} <= 0");
                    if (d.Kind == DefenderKind.Unit && !d.UnitClass.HasValue)
                        failures.Add($"unit defender '{d.Id}' has no UnitClass (no body to spawn)");
                    if (d.Kind == DefenderKind.Structure && string.IsNullOrEmpty(d.BehaviorId))
                        failures.Add($"structure defender '{d.Id}' has no BehaviorId (no behavior to attach)");

                    if (cheapest == null || d.PointCost < cheapest.PointCost) cheapest = d;
                }

                if (ArenaDefenseCatalog.DefensePointPool != 50)
                    failures.Add($"DefensePointPool is {ArenaDefenseCatalog.DefensePointPool}, expected 50");
                if (cheapest != null &&
                    !ArenaDefenseCatalog.CanAfford(new List<PlacedDefenderData>(), cheapest.Id))
                    failures.Add($"cheapest defender '{cheapest.Id}' ({cheapest.PointCost}pt) is NOT affordable on an empty pool (day-one place broken)");
            }

            if (failures.Count == 0)
            {
                reason = $"ARENA CATALOG OK — 3 opponents (purse=2x wager, forts non-empty) + 6 defenders (pool {ArenaDefenseCatalog.DefensePointPool}, day-one affordable)";
                return true;
            }
            reason = $"ARENA CATALOG FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }
    }
}
