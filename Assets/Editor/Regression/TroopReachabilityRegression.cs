// =============================================================================
// TroopReachabilityRegression [troop-reachability] -- WO-2011 / owner ruling 21.
// -----------------------------------------------------------------------------
// EVERY TROOP MUST BE REACHABLE BY A PLAYER. The sibling suite
// ProgressionReachabilityRegression (WO-1423) guards the VILLAGE-tier axis; this one
// guards the TROOP axis, which is the axis that actually broke.
//
// WHAT BROKE (the class of bug, not one instance): troop unlocks were gated on
// GameState.BarracksLevel -- a field whose ONLY writer was the completion effect of a
// JobKind.BarracksUpgrade job, composed only by BarracksPanelVM, reachable only from
// BarracksPanel.ShowBarracksUI, which has ZERO CALLERS (source grep + a script-GUID
// search of every .unity/.prefab/.asset, 2026-09-06). So the field sat at its founding
// value of 1 forever and SEVEN OF NINE TROOPS -- Spearman, Field Cleric, Shieldguard,
// Outrider, Siege Catapult, Battlemage, Echo Legionnaire -- could not be trained by any
// player action, while the barracks BUILDING the player CAN upgrade did nothing for the
// army. Two numbers spelled the same way on different scales; identical in shape to the
// village-tier defect (WO-1423). Owner ruling 21, 2026-09-06: "Merge them - the building
// tier gates troops."
//
// 394 suites were green throughout, because every one of them asked "does this system do
// its job" and none asked "can a player get here at all". That is what this file adds.
//
// It deliberately asks through the CODE PATH -- BarracksProgression.EffectiveBarracksLevelOf
// and BarracksProgression.IsTroopUnlocked, the accessors the live gate uses -- so the
// ACCESSOR ITSELF is under test. A second, parallel re-walk of the JSON would stay green
// while the game read a different number, which is exactly how the defect hid.
//
// Marker: TROOP_REACHABILITY_OK / TROOP_REACHABILITY_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "troop-reachability suite", () => { if (!DeNelle.Editor.TroopReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[troop-reachability] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.State;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TroopReachabilityRegression
    {
        private const string BarracksId = "barracks";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== TroopReachabilityRegression (WO-2011 / ruling 21) ===\n");
            try
            {
                CheckUnlockCeiling(failures, log);
                CheckLaddersAgree(failures, log);
                CheckBuildingTierIsTheGate(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "TROOP_REACHABILITY_OK every authored troop unlocks at or below the barracks " +
                         "ladder ceiling (" + BarracksProgression.MaxBarracksBuildingTier + ") and the " +
                         "barracks BUILDING tier is the gate that opens them";
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "TROOP_REACHABILITY_FAIL " + string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // CASE 1  [troop-unlock-reachable]
        // No troop's unlock level may exceed the barracks ladder's max tier. Asked through
        // BarracksProgression.MaxBarracksBuildingTier + UnlockLevelFor -- the accessors introduced
        // by the merge -- so a regression in either turns this RED instead of hiding.
        //
        // REVERT RECIPE (RED): set any troop's "unlockBarracksTier" to 7 in troops.json (BOTH
        // canonical copies) -- the ladder tops out at 6 and this case names that troop.
        private static void CheckUnlockCeiling(List<string> failures, StringBuilder log)
        {
            int ceiling = BarracksProgression.MaxBarracksBuildingTier;
            if (ceiling <= 0)
            {
                failures.Add("[troop-unlock-reachable] building-tiers.json has no 'barracks' ladder (MaxTier=" +
                             ceiling + ") - after ruling 21 that ladder IS the troop gate, so a missing " +
                             "ceiling means no troop above tier 1 can ever unlock. FAIL, not a skip");
                return;
            }

            var troops = TroopCatalog.All;
            if (troops == null || troops.Count == 0)
            {
                failures.Add("[troop-unlock-reachable] troops.json loaded ZERO troops - the reachability pin " +
                             "cannot be scoped, so it cannot be trusted. FAIL, not a skip");
                return;
            }

            int checkedCount = 0;
            foreach (var t in troops)
            {
                if (t == null || string.IsNullOrEmpty(t.Id)) continue;
                checkedCount++;
                int need = BarracksProgression.UnlockLevelFor(t.Id);
                if (need > ceiling)
                    failures.Add("[troop-unlock-reachable] " + t.Id + " unlocks at barracks level " + need +
                                 " but the barracks ladder tops out at tier " + ceiling +
                                 " - no player action can raise the barracks that far, so this troop is " +
                                 "authored, priced, iconed and permanently untrainable");
            }
            log.AppendLine("ceiling=" + ceiling + " troops=" + checkedCount);
        }

        // CASE 2  [barracks-ladders-agree]
        // TWO ladders are spelled "barracks level": barracks.json levels[] and the barracks rows in
        // building-tiers.json. BarracksProgression.IsTroopUnlocked takes the UNION of the two
        // encodings, so a barracks.json level ABOVE the building ceiling would advertise an unlock
        // the player's building can never reach. The ladders must have the same height.
        //
        // REVERT RECIPE (RED): add a {"level": 7, ...} row to barracks.json (BOTH canonical copies).
        private static void CheckLaddersAgree(List<string> failures, StringBuilder log)
        {
            int catalogMax = BarracksProgression.MaxBarracksLevel;          // barracks.json
            int buildingMax = BarracksProgression.MaxBarracksBuildingTier;  // building-tiers.json
            if (catalogMax != buildingMax)
                failures.Add("[barracks-ladders-agree] barracks.json tops out at level " + catalogMax +
                             " but the barracks building ladder tops out at tier " + buildingMax +
                             ". After ruling 21 these are ONE number read two ways - a mismatch means " +
                             "either an unlock nobody can reach or a tier that grants nothing");
            log.AppendLine("barracks.json max=" + catalogMax + " building-tiers max=" + buildingMax);
        }

        // CASE 3  [building-tier-is-the-gate]  ** the case that would have caught the defect **
        // Drives the REAL merged accessor with a throwaway GameState that holds the legacy stored
        // BarracksLevel at its founding value of 1 and ONLY a barracks BUILDING tier, then asserts
        // that raising the BUILDING unlocks exactly the troops building-tiers.json advertises.
        //
        // REVERT RECIPE (RED): make BarracksProgression.EffectiveBarracksLevelOf return
        // BarracksLevelOf(state) alone (the pre-ruling-21 body) -- a state at building tier 6 then
        // reports level 1 and unlocks only Footman + Archer, and this case names all seven troops
        // the owner could not reach.
        private static void CheckBuildingTierIsTheGate(List<string> failures, StringBuilder log)
        {
            int ceiling = BarracksProgression.MaxBarracksBuildingTier;
            if (ceiling <= 0) return;   // already failed in CASE 1

            GameState fixtureState = null;
            try
            {
                fixtureState = ScriptableObject.CreateInstance<GameState>();
                fixtureState.BarracksLevel = 1;   // the founding value every real save actually holds
                if (fixtureState.BuildingTiers == null)
                    fixtureState.BuildingTiers = new Dictionary<string, int>();

                for (int tier = 1; tier <= ceiling; tier++)
                {
                    fixtureState.BuildingTiers[BarracksId] = tier;

                    int effective = BarracksProgression.EffectiveBarracksLevelOf(fixtureState);
                    if (effective != tier)
                    {
                        failures.Add("[building-tier-is-the-gate] BuildingTiers[\"barracks\"]=" + tier +
                                     " with the legacy stored level at 1 resolves to effective level " +
                                     effective + ". Ruling 21: the BUILDING tier gates troops");
                        continue;
                    }

                    foreach (var t in TroopCatalog.All)
                    {
                        if (t == null || string.IsNullOrEmpty(t.Id)) continue;
                        int need = BarracksProgression.UnlockLevelFor(t.Id);
                        bool unlocked = BarracksProgression.IsTroopUnlocked(t.Id, effective);
                        bool shouldBe = need <= tier;
                        if (unlocked != shouldBe)
                            failures.Add("[building-tier-is-the-gate] at barracks BUILDING tier " + tier +
                                         ", " + t.Id + " (unlocks at " + need + ") reports unlocked=" +
                                         unlocked + " - expected " + shouldBe);
                    }
                }

                // The whole roster must be reachable by the top of the ladder. This is the sentence
                // the owner would have wanted on 2026-09-06: is every troop gettable at all?
                fixtureState.BuildingTiers[BarracksId] = ceiling;
                int top = BarracksProgression.EffectiveBarracksLevelOf(fixtureState);
                int reachable = 0, total = 0;
                foreach (var t in TroopCatalog.All)
                {
                    if (t == null || string.IsNullOrEmpty(t.Id)) continue;
                    total++;
                    if (BarracksProgression.IsTroopUnlocked(t.Id, top)) reachable++;
                    else
                        failures.Add("[every-troop-is-reachable] " + t.Id + " is STILL locked at the top of the " +
                                     "barracks ladder (tier " + ceiling + ") - it can never be trained");
                }
                log.AppendLine("at top tier " + ceiling + ": " + reachable + "/" + total + " troops reachable");

                // And a fresh save (no barracks tier written at all) must still train the day-one
                // roster - the floor of 1 that predates the merge and must survive it.
                fixtureState.BuildingTiers.Remove(BarracksId);
                int fresh = BarracksProgression.EffectiveBarracksLevelOf(fixtureState);
                if (fresh != 1)
                    failures.Add("[fresh-save-floor] a state with no barracks tier resolves to level " + fresh +
                                 " - it must floor to 1 so the day-one Footman + Archer stay trainable");
            }
            finally
            {
                if (fixtureState != null) UnityEngine.Object.DestroyImmediate(fixtureState);
            }
        }
    }
}
