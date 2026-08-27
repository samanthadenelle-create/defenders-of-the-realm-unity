// =============================================================================
// WO1217EarlyEconomyLadderRegression [early-ladder] -- WO-1217 owner rulings,
// 2026-08-26, Seeker build 2026.08.26.341419.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Pins the three rulings of WO-1217 that had
// no oracle. (Slice C's TOP-tier half is already pinned by
// CastlePlansUnlockRegression [castle-plans]; this suite pins the LOWER-tier half
// it deliberately does not cover, plus Slices A and B.)
//
//   A. FIRST-STEP RULE -- repo.upgradeCost[0] (the L1->L2 rung) == round(repo.cost[r]
//      * 1.5) for EVERY resource r, on EVERY entry that authors an upgradeCost
//      array. Owner ruling verbatim: "would take hours just to level anything in
//      the start". It was a flat x3 on all four towers (Archer Tower L2 = 1080
//      wood / 480 iron, ~29 waves for one rung at WO-1216's faucet).
//      + THE GOOD PATH: the SECOND rung must still climb ABOVE 1.5x, so a future
//      "flatten the ladder" pass cannot leak past the first rung and quietly make
//      a maxed structure cheap. WO-1217: "Do NOT touch the second step."
//      + THE 11th maxLevel>1 ROW ('healing_caravan') authors NO array on purpose and
//        falls back to BuildModeController.UpgradeCostFor's scaler (build cost x the
//        level being LEFT = 1.0x for L1->L2), which is already BELOW the ceiling.
//        Asserted EXPLICITLY rather than skipped -- an unauthored row must stay
//        unauthored-and-cheap, not silently acquire a x3 table.
//
//   B. FOUNDING BUDGET -- a new game starts Gold 200 / Wood 0 / Iron 0. Owner
//      ruling verbatim: "so start gold at 200". The wood/iron arms pin the
//      2026-07-13 ZERO-seed ruling against accidental restoration (the per-id
//      free-first-build flags replaced that seed to prevent all-defense-no-town).
//      ⚠ These arms compare against LITERALS on purpose. CoreSaveRegression already
//      asserts ResetToNewGame == StartingBudget.*, which pins the WIRING but is
//      tautological about the VALUE: editing the constant moves that oracle with it.
//      This suite is the value pin; that one is the wiring pin. Both are needed.
//
//   C. CRYSTAL GATE -- tower_arcane_spire charges ZERO crystals at BUILD and at the
//      L1->L2 rung, and NONZERO at its final rung. Owner ruling verbatim: "i think
//      only tier 3 should cost crystals on the arcane tower". There is still no
//      crystal faucet in town (PROD-015, open), so a crystal-priced L2 was an
//      Upgrade button no amount of play could satisfy.
//      ⛔ NOT WEAKENED, MOVED: WO-947 keeps a magical row crystal-BASED -- now at its
//      FINAL rung. The nonzero-top arm is what holds that line, and it lives in
//      [castle-plans]; the zero-lower arms live here. If the basket ever moves again,
//      BOTH sites move.
//
// ⛔ DATA-ONLY. This suite reads the SHIPPING catalog JSON and the founding
// constants; it grants nothing, mutates nothing, and touches no save.
//
// Marker: EARLY_LADDER_OK / EARLY_LADDER_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "early-ladder suite", () => { if (!WO1217EarlyEconomyLadderRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[early-ladder] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class WO1217EarlyEconomyLadderRegression
    {
        private const string CatalogPath = "Data/Canonical/structures-catalog.json";
        private const string SpireId = "tower_arcane_spire";
        private const string UnauthoredLadderId = "healing_caravan";

        /// <summary>The owner's flattening multiplier for the FIRST rung only (WO-1217 Slice A).</summary>
        private const float FirstStepMultiplier = 1.5f;

        /// <summary>
        /// The ruled founding budget. LITERALS on purpose -- see the header: comparing
        /// against StartingBudget.* would move with any edit to it and pin nothing.
        /// </summary>
        private const int RuledGold = 200;
        private const int RuledWood = 0;
        private const int RuledIron = 0;

        private static readonly string[] CostKeys = { "wood", "food", "iron", "crystals" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- EARLY ECONOMY LADDER (WO-1217: flat first rung / gold seed / crystal gate) ---");

            JArray entries = null;
            string json = DeNelle.Core.CanonicalJson.Read(CatalogPath);
            if (string.IsNullOrEmpty(json))
            {
                // ⛔ NOT A SKIP. The catalog is a SHIPPING file that is always present; a read
                // failure here means the data this whole suite exists to pin is unreadable, and
                // returning green on that is the hollow pass the gate is built to catch.
                failures.Add("[read] " + CatalogPath + " missing or empty -- the shipping catalog must be readable");
            }
            else
            {
                try { entries = JObject.Parse(json)["entries"] as JArray; }
                catch (Exception ex) { failures.Add("[read] parse error: " + ex.Message); }
                if (entries == null)
                    failures.Add("[read] structures-catalog.json has no 'entries' array");
                else if (entries.Count == 0)
                    failures.Add("[read] structures-catalog.json 'entries' is EMPTY -- nothing to pin");
            }

            if (entries != null && entries.Count > 0)
            {
                CheckFirstStepRule(entries, failures, log);
                CheckSpireCrystalGate(entries, failures, log);
            }

            CheckFoundingBudget(failures, log);

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // ---- A: the first rung is a RULE, not a hand-authored row ----------------
        private static void CheckFirstStepRule(JArray entries, List<string> failures, StringBuilder log)
        {
            int multiRung = 0, authored = 0;
            bool sawUnauthoredRow = false;

            foreach (var tok in entries)
            {
                if (!(tok is JObject o)) continue;
                string id = o["id"]?.ToString();
                var repo = o["repo"] as JObject;
                if (repo == null) continue;
                int maxLevel = repo["maxLevel"]?.Value<int>() ?? 1;
                if (maxLevel <= 1) continue;
                multiRung++;

                var cost = repo["cost"] as JObject;
                var steps = repo["upgradeCost"] as JArray;

                if (steps == null || steps.Count == 0)
                {
                    // The deliberate exception. Assert it IS the known one -- a NEW unauthored
                    // row is a silent hole in the rule, not a pass.
                    if (!string.Equals(id, UnauthoredLadderId, StringComparison.OrdinalIgnoreCase))
                        failures.Add($"[first-step] '{id}' has maxLevel {maxLevel} but authors NO upgradeCost array. " +
                                     $"Only '{UnauthoredLadderId}' is the ruled exception (its UpgradeCostFor fallback is " +
                                     "1.0x for L1->L2, already below the ceiling). Either author the rung to the rule or " +
                                     "record a new exception -- an unpinned row is how the x3 wall comes back.");
                    else
                    {
                        sawUnauthoredRow = true;
                        log.AppendLine($"  '{id}' maxLevel {maxLevel}, no upgradeCost array -- ruled exception, UpgradeCostFor fallback is 1.0x for the first rung (below the {FirstStepMultiplier:0.##}x ceiling) OK");
                    }
                    continue;
                }

                authored++;
                if (cost == null)
                {
                    failures.Add($"[first-step] '{id}' authors an upgradeCost array but NO repo.cost -- the rule is expressed against the build cost, so it cannot be derived");
                    continue;
                }

                var first = steps[0] as JObject;
                if (first == null)
                {
                    failures.Add($"[first-step] '{id}' upgradeCost[0] is not an object");
                    continue;
                }

                foreach (string k in CostKeys)
                {
                    int build = cost[k]?.Value<int>() ?? 0;
                    int got = first[k]?.Value<int>() ?? 0;
                    int want = Mathf.RoundToInt(build * FirstStepMultiplier);
                    if (got != want)
                        failures.Add($"[first-step] '{id}' upgradeCost[0].{k} = {got}, expected {want} " +
                                     $"(= round(build {build} x {FirstStepMultiplier:0.##})). WO-1217 Slice A: the first rung is a " +
                                     "FORMULA across the catalog, never a hand-edited row -- re-tune the multiplier and re-derive.");
                }

                // ⭐ THE GOOD PATH. A failure-only oracle would pass a catalog someone had
                // flattened end to end. The second rung must still be a real commitment.
                if (steps.Count >= 2 && steps[1] is JObject second)
                {
                    bool anyResource = false, secondClimbs = false;
                    foreach (string k in CostKeys)
                    {
                        int build = cost[k]?.Value<int>() ?? 0;
                        if (build <= 0) continue;
                        anyResource = true;
                        int s2 = second[k]?.Value<int>() ?? 0;
                        if (s2 > Mathf.RoundToInt(build * FirstStepMultiplier)) secondClimbs = true;
                    }
                    if (anyResource && !secondClimbs)
                        failures.Add($"[ladder-steep] '{id}' upgradeCost[1] does NOT climb above the flattened first rung " +
                                     $"({FirstStepMultiplier:0.##}x build) in any resource -- WO-1217: \"Do NOT touch the second step. L3 stays " +
                                     "at its authored value everywhere.\" The flattening leaked past the first rung.");
                }
            }

            if (multiRung == 0)
                failures.Add("[first-step] NO entry with maxLevel > 1 exists in the catalog -- there is no upgrade ladder to pin, so a green here would prove nothing");
            else if (authored == 0)
                failures.Add($"[first-step] {multiRung} multi-rung entries exist but NONE authors an upgradeCost array -- the rule was checked against zero rows");
            else
                log.AppendLine($"  first-step rule holds: {authored} of {multiRung} maxLevel>1 rows author upgradeCost[0] == round(build x {FirstStepMultiplier:0.##}) on all of {string.Join("/", CostKeys)}; second rungs still climb");

            if (!sawUnauthoredRow)
                log.AppendLine($"  note: '{UnauthoredLadderId}' (the ruled unauthored exception) was not found as a maxLevel>1 row -- every multi-rung row now authors its ladder");
        }

        // ---- C: crystals gate the FINAL tier only --------------------------------
        private static void CheckSpireCrystalGate(JArray entries, List<string> failures, StringBuilder log)
        {
            JObject repo = null;
            foreach (var tok in entries)
                if (tok is JObject o && string.Equals(o["id"]?.ToString(), SpireId, StringComparison.OrdinalIgnoreCase))
                { repo = o["repo"] as JObject; break; }

            if (repo == null)
            {
                failures.Add($"[spire-gate] structures-catalog.json has no '{SpireId}' entry (or it carries no repo) -- " +
                             "the row WO-1217 Slice C rules on is gone; the gate cannot be pinned");
                return;
            }

            int buildCrystals = (repo["cost"] as JObject)?["crystals"]?.Value<int>() ?? 0;
            if (buildCrystals != 0)
                failures.Add($"[spire-gate] '{SpireId}' BUILD charges {buildCrystals} crystals, expected 0 -- owner ruling " +
                             "2026-08-26: \"only tier 3 should cost crystals\". There is no crystal faucet in town (PROD-015), " +
                             "so a crystal-priced lower tier cannot be satisfied by playing at all.");

            var steps = repo["upgradeCost"] as JArray;
            if (steps == null || steps.Count == 0)
            {
                failures.Add($"[spire-gate] '{SpireId}' authors NO upgradeCost ladder -- \"only tier 3\" needs a tier 3 to exist");
                return;
            }

            int l2Crystals = (steps[0] as JObject)?["crystals"]?.Value<int>() ?? 0;
            if (l2Crystals != 0)
                failures.Add($"[spire-gate] '{SpireId}' L1->L2 charges {l2Crystals} crystals, expected 0 -- this is the exact " +
                             "button the owner hit on device that no grind rate could satisfy.");

            // ⭐ THE GOOD PATH, and the WO-947 line: the rule MOVED to the top, it did not vanish.
            // (Also held by [castle-plans]; duplicated deliberately so neither site can drop it alone.)
            int topCrystals = (steps[steps.Count - 1] as JObject)?["crystals"]?.Value<int>() ?? 0;
            if (topCrystals <= 0)
                failures.Add($"[spire-gate] '{SpireId}' FINAL rung charges {topCrystals} crystals -- a magical row under WO-947 " +
                             "is crystal-BASED. The 2026-08-26 ruling moved that cost to the top tier; it did not remove it.");

            // The structure must still have a real price once crystals leave the lower rungs.
            int buildIron = (repo["cost"] as JObject)?["iron"]?.Value<int>() ?? 0;
            int buildWood = (repo["cost"] as JObject)?["wood"]?.Value<int>() ?? 0;
            if (buildIron + buildWood <= 0)
                failures.Add($"[spire-gate] '{SpireId}' BUILD is now FREE (wood 0 / iron 0 / crystals 0) -- WO-1217 says move the " +
                             "crystal value into Iron (and/or Wood) so the structure keeps a real price, not delete it.");

            log.AppendLine($"  '{SpireId}': build crystals={buildCrystals} (iron {buildIron} / wood {buildWood}), L1->L2 crystals={l2Crystals}, FINAL rung crystals={topCrystals}");
        }

        // ---- B: the founding budget ---------------------------------------------
        private static void CheckFoundingBudget(List<string> failures, StringBuilder log)
        {
            if (StartingBudget.StrategicGold != RuledGold)
                failures.Add($"[founding-budget] StartingBudget.StrategicGold = {StartingBudget.StrategicGold}, expected {RuledGold} " +
                             "-- owner ruling 2026-08-26, verbatim: \"so start gold at 200\".");
            if (StartingBudget.StrategicWood != RuledWood)
                failures.Add($"[founding-budget] StartingBudget.StrategicWood = {StartingBudget.StrategicWood}, expected {RuledWood} " +
                             "-- the 2026-07-13 ZERO-seed ruling: wood/iron founding freebies were replaced by the per-id " +
                             "free-first-build flags to prevent all-defense-no-town. A gold seed is a SEPARATE ruling and does not reopen this.");
            if (StartingBudget.StrategicIron != RuledIron)
                failures.Add($"[founding-budget] StartingBudget.StrategicIron = {StartingBudget.StrategicIron}, expected {RuledIron} " +
                             "-- see the wood arm; the 2026-07-13 zero-seed ruling covers both.");

            log.AppendLine($"  founding budget: gold {StartingBudget.StrategicGold} / wood {StartingBudget.StrategicWood} / iron {StartingBudget.StrategicIron}");
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "EARLY_LADDER_OK");
                return "EARLY LADDER OK -- first rung is round(build x1.5) on every authored ladder (second rungs still climb); " +
                       "arcane spire charges crystals only at its final tier; new game founds on gold 200 / wood 0 / iron 0";
            }
            string reason = "early-ladder: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "EARLY_LADDER_FAIL: " + reason);
            return reason;
        }
    }
}
