// =============================================================================
// ProgressionReachabilityRegression [progression-reachability] -- WO-1423.
// -----------------------------------------------------------------------------
// EVERY AUTHORED GATE MUST BE OPENABLE BY A PLAYER. For every building tier and
// every research perk in building-tiers.json, the VILLAGE tier it demands must be
// <= VillageTierService.MaxTier. A requirement above that ceiling can never be
// satisfied by any player action -- VillageTierService.TryUpgrade refuses at max
// (VillageTierService.cs:59) -- so the content is authored, priced, iconed, and
// permanently out of reach, with nothing on screen saying so.
//
// WHY THIS SUITE EXISTS (the class of bug, not one instance): the perk gate used to
// compare BuildingTierCatalog.PerkUnlockTier -- a BUILDING tier number -- against
// VillageTierService.Current. Under that reading lumber-ancient-sawmill (lumbermill
// tier 4) demanded village tier 4 while MaxTier is 3: unreachable forever. Every
// ladder's tier-1 perk was likewise village-locked on a fresh save although its own
// tier row authors requiresVillageTier: 0. The owner met it as "some items are
// locked till village level 1, which there is no way to trigger."
//
// The perk half deliberately asks through the CODE PATH
// (BuildingTierCatalog.PerkRequiredVillageTier), not by re-walking the JSON: the
// accessor is the thing under test, so a regression in it must turn this suite RED
// rather than hide behind a second, parallel reading of the same file.
//
// Marker: PROGRESSION_REACHABILITY_OK / PROGRESSION_REACHABILITY_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "progression-reachability suite", () => { if (!DeNelle.Editor.ProgressionReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[progression-reachability] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ProgressionReachabilityRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== ProgressionReachabilityRegression (WO-1423) ===\n");
            try
            {
                CheckEveryGateIsReachable(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "PROGRESSION_REACHABILITY_OK every authored building tier and research perk " +
                         "sits at or below VillageTierService.MaxTier=" + VillageTierService.MaxTier;
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "PROGRESSION_REACHABILITY_FAIL " + string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        private static void CheckEveryGateIsReachable(List<string> failures, StringBuilder log)
        {
            int ceiling = VillageTierService.MaxTier;
            var buildings = BuildingTierCatalog.All;
            if (buildings == null || buildings.Count == 0)
            {
                failures.Add("[progression-reachability] building-tiers.json loaded ZERO ladders - the reachability " +
                             "pin cannot be scoped, so it cannot be trusted. FAIL, not a skip");
                return;
            }

            int tiersChecked = 0, perksChecked = 0;

            // CASE 1  [tier-gate-reachable]
            // REVERT RECIPE (RED): lower VillageTierService.MaxTier to 2 -- every ladder's tier-4 row
            // authors requiresVillageTier 3 and this case names all six.
            foreach (var b in buildings)
            {
                if (b == null || b.Tiers == null) continue;
                foreach (var t in b.Tiers)
                {
                    if (t == null) continue;
                    tiersChecked++;
                    if (t.RequiresVillageTier > ceiling)
                        failures.Add("[tier-gate-reachable] " + b.Id + " tier " + t.Tier + " demands Village Tier " +
                                     t.RequiresVillageTier + " but VillageTierService.MaxTier is " + ceiling +
                                     " - TryUpgrade refuses at max, so this upgrade can never be bought by any " +
                                     "player action");
                }
            }

            // CASE 2  [perk-gate-reachable]  ** the case that would have caught WO-1423 **
            // Asked through BuildingTierCatalog.PerkRequiredVillageTier, the accessor the live gate
            // uses (BuildingPerkService.CanResearch), so the ORACLE and the GAME read the same number.
            // REVERT RECIPE (RED): make PerkRequiredVillageTier return `t.Tier` instead of
            // `t.RequiresVillageTier` -- lumber-ancient-sawmill then reports 4 against MaxTier 3 and
            // this case fires, which is exactly the defect the owner hit.
            foreach (var b in buildings)
            {
                if (b == null || b.Tiers == null) continue;
                foreach (var t in b.Tiers)
                {
                    if (t == null || t.Perks == null) continue;
                    foreach (var p in t.Perks)
                    {
                        if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                        perksChecked++;
                        int gate = BuildingTierCatalog.PerkRequiredVillageTier(b.Id, p.Id);
                        if (gate == int.MaxValue)
                        {
                            failures.Add("[perk-gate-reachable] " + b.Id + ":" + p.Id + " is authored in the catalog " +
                                         "but PerkRequiredVillageTier cannot find it - the research gate would " +
                                         "fail-closed and lock the perk forever");
                            continue;
                        }
                        if (gate > ceiling)
                            failures.Add("[perk-gate-reachable] " + b.Id + ":" + p.Id + " demands Village Tier " +
                                         gate + " but VillageTierService.MaxTier is " + ceiling + " - no player " +
                                         "action can ever raise the village that far, so this research is " +
                                         "unreachable content");
                        // A perk must also never out-gate the very tier row that carries it: the
                        // upgrade that unlocks the perk and the perk itself read the SAME authored
                        // number, which is the WO-1423 fix stated as an invariant.
                        // REVERT RECIPE (RED): same as above - returning t.Tier breaks this too.
                        if (gate != t.RequiresVillageTier)
                            failures.Add("[perk-gate-matches-its-tier-row] " + b.Id + ":" + p.Id + " gates on Village " +
                                         "Tier " + gate + " but its own tier row (" + b.Id + " tier " + t.Tier +
                                         ") authors " + t.RequiresVillageTier + ". Two scales spelled the same way " +
                                         "is the WO-1423 defect");
                    }
                }
            }

            log.AppendLine("MaxTier=" + ceiling + " ladders=" + buildings.Count +
                           " tiers=" + tiersChecked + " perks=" + perksChecked);
        }
    }
}
