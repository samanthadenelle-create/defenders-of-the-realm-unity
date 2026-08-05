// =============================================================================
// CollectorIncomeRegression [collector-income]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Markers: COLLECTOR_INCOME_OK / COLLECTOR_INCOME_FAIL.
//
// THE DEFECT CLASS THIS PINS (measured 2026-08-04): PHANTOM COLLECTOR INCOME.
//
// ResourceBuildingHarvester.Update iterated all three
// ResourceBuildingProgression.OrderedIds (farm / lumbermill / forge)
// UNCONDITIONALLY. Its only guard was `GetLevel(id) < 1`, and
// ResourceBuildingState.GetLevel is `PlayerPrefs.GetInt(key, 1)` clamped to >= 1
// (ResourceBuildingState.cs:63-69) - it DEFAULTS TO 1 and never asks whether the
// building exists, so `level < 1` is unreachable. An EMPTY town therefore earned
// farm + lumbermill + forge income from t=0; and because no ResourceCollector was
// registered for a building that was never placed, the income fell through to a
// direct-grant fallback that banked it straight into the wallet - UNCAPPED,
// AUTO-BANKED, bypassing the capped pending pool, the manual Collect tap and the
// siege-loot risk that are the entire point of the WO-663 CoC collector spine.
// Post-WO-855 that free baseline is ~720 wood + 936 food + 432 iron per hour for a
// town with nothing in it, and PLACING the collector made income strictly WORSE.
//
// The fix gates the per-id tick on the building actually having been built, proven
// by the persisted WO-834 ledger GameState.EverBuiltStructureIds (save v36) or by a
// live registered ResourceCollector, and DELETES the direct-grant fallback so the
// capped pending pool is the only payout path.
//
// SIX CASES:
//
//   1 [gate-live]      Source: the harvester consults the existence rule BEFORE the
//                      level read, and reads the ever-built ledger. FAILS on the
//                      pre-fix tree (there was no gate at all).
//
//   2 [no-direct-grant] Source: the uncapped auto-banking payout is GONE - no
//                      EconomyService.Grant / ResourceLedger.Credit anywhere in the
//                      harvester, exactly one collector.Accrue payout, and the
//                      no-collector branch traces + withholds instead of paying.
//                      FAILS on the pre-fix tree.
//
//   3 [empty-town-zero] Behavioral truth table on the PURE rule
//                      ResourceBuildingHarvester.MayHarvest: an empty town (nothing
//                      ever built, no live collector) earns ZERO for every id; an
//                      unrelated ledger and a null ledger earn zero too.
//
//   4 [built-earns]    A built farm earns farm income and ONLY farm income (no bleed
//                      to lumbermill/forge), the match is case-insensitive (the
//                      MarkEverBuilt convention), and a live standing collector
//                      produces even with an empty ledger.
//
//   5 [catalog-map]    Data: each progression id resolves to EXACTLY ONE Collector
//                      catalog row via repo.collectorBuildingId, and the BARE id
//                      (lumbermill / forge also exist as GameplayBuilding storefront
//                      rows) is NOT a Collector row - so building the Forge
//                      STOREFRONT can never open the forge COLLECTOR's harvest gate.
//
//   6 [founding-flow]  THE SOFT-LOCK GUARD. Starting wood and iron are 0
//                      (StartingBudget), so the free-first-build flags ARE the
//                      starting budget. Every mandatory founding placement must be
//                      free (FoundingKit or a non-tower lane-3 first-of-each-id
//                      freebie), the founding collector must be a real Collector row
//                      whose placement OPENS the gate, and a freshly placed
//                      (level-1) collector must be able to accrue - ResourceCollector
//                      .IsActive must NOT require level > 1, or the zero-seed
//                      founding bootstrap dead-locks the moment the direct-grant
//                      fallback is removed.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.CollectorIncomeRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Editor.Regression
{
    public static class CollectorIncomeRegression
    {
        private const string HarvesterSrc = "Assets/_Modules/Village/Buildings/Progression/ResourceBuildingHarvester.cs";
        private const string CollectorSrc = "Assets/_Modules/Village/Buildings/Progression/ResourceCollector.cs";
        private const string BuildModeSrc = "Assets/_Modules/Village/BuildMode/BuildModeController.cs";
        private const string StructuresRes = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string StructuresSA = "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";
        private const string StepsRes = "Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json";

        // =====================================================================

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("COLLECTOR_INCOME_OK - " + reason);
            else Debug.LogError("COLLECTOR_INCOME_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "gate-live", () => Case1_GateLive(failures, notes));
            Case(failures, "no-direct-grant", () => Case2_NoDirectGrant(failures, notes));
            Case(failures, "empty-town-zero", () => Case3_EmptyTownZero(failures, notes));
            Case(failures, "built-earns", () => Case4_BuiltEarns(failures, notes));
            Case(failures, "catalog-map", () => Case5_CatalogMap(failures, notes));
            Case(failures, "founding-flow", () => Case6_FoundingFlow(failures, notes));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count > 0)
            {
                reason = "collector-income FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
                return false;
            }

            reason = "COLLECTOR INCOME OK - an empty town earns ZERO from every resource building, " +
                     "the existence gate runs before the level read and reads the WO-834 ever-built " +
                     "ledger, the uncapped auto-banking direct grant is gone (the capped pending pool " +
                     "is the only payout), a built farm earns farm income only, each progression id maps " +
                     "to exactly one Collector catalog row, and the zero-seed founding sequence still " +
                     "bootstraps (every demanded placement is free and a level-1 collector accrues)" + noteStr;
            return true;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the existence gate is live, and it runs BEFORE the level read
        // =====================================================================

        private static void Case1_GateLive(List<string> failures, List<string> notes)
        {
            string raw = ReadText(HarvesterSrc, failures);
            if (raw == null) return;
            string code = StripComments(raw);

            int update = code.IndexOf("private void Update", StringComparison.Ordinal);
            if (update < 0)
            {
                failures.Add("[gate-live] ResourceBuildingHarvester has no Update() - the harvest tick moved; re-point this oracle");
                return;
            }
            string body = code.Substring(update);

            int gate = body.IndexOf("MayHarvest", StringComparison.Ordinal);
            int levelRead = body.IndexOf("ResourceBuildingState.GetLevel", StringComparison.Ordinal);

            if (gate < 0)
            {
                failures.Add("[gate-live] the harvest tick does NOT consult the existence rule (MayHarvest) - every id " +
                             "ticks unconditionally again, so an EMPTY town earns free income from t=0 (the phantom-income defect)");
                return;
            }
            if (levelRead >= 0 && gate > levelRead)
                failures.Add("[gate-live] the existence gate runs AFTER ResourceBuildingState.GetLevel - GetLevel DEFAULTS TO 1 " +
                             "and can never report 'does not exist', so the level read must not be the first guard");

            if (code.IndexOf("EverBuilt", StringComparison.Ordinal) < 0)
                failures.Add("[gate-live] the harvester no longer reads the WO-834 EverBuiltStructureIds ledger - the persisted " +
                             "proof that a building was actually placed (save v36) is the whole basis of the gate");

            // The gate must also stop banking ELAPSED time for a building that does not
            // exist, or a town founded an hour in pays out an instant phantom backlog.
            if (!Regex.IsMatch(body, @"_elapsed\s*\[\s*i\s*\]\s*=\s*0f"))
                notes.Add("the closed-gate branch does not visibly zero _elapsed[i]; a never-built id may bank elapsed time");

            // Instrumentation (CLAUDE.md sec.12): a capture must name which ids ticked and
            // which were skipped for not existing.
            if (body.IndexOf("FlowTrace", StringComparison.Ordinal) < 0)
                failures.Add("[gate-live] the harvest tick writes no FlowTrace line - a capture cannot show which ids ticked " +
                             "and which were skipped for not existing (CLAUDE.md sec.12)");
        }

        // =====================================================================
        //  CASE 2 - the uncapped auto-banking direct grant is GONE
        // =====================================================================

        private static void Case2_NoDirectGrant(List<string> failures, List<string> notes)
        {
            string raw = ReadText(HarvesterSrc, failures);
            if (raw == null) return;
            string code = StripComments(raw);

            if (code.IndexOf("EconomyService", StringComparison.Ordinal) >= 0)
                failures.Add("[no-direct-grant] the harvester references EconomyService again - the direct grant banked harvest " +
                             "income straight into the wallet, UNCAPPED and with no Collect tap, which is the second half of the " +
                             "phantom-income defect (and it also paid full town income while the player was off in a dungeon)");

            if (Regex.IsMatch(code, @"ResourceLedger\s*\.\s*Credit\s*\("))
                failures.Add("[no-direct-grant] the harvester calls ResourceLedger.Credit again - harvest income must land in the " +
                             "collector's capped pending pool, never straight in the wallet");

            var accrue = Regex.Matches(code, @"\.\s*Accrue\s*\(");
            if (accrue.Count == 0)
                failures.Add("[no-direct-grant] the harvester never calls Accrue - the capped pending pool is the ONLY payout path; " +
                             "with it gone a built collector earns nothing at all");
            else if (accrue.Count > 1)
                failures.Add("[no-direct-grant] the harvester calls Accrue " + accrue.Count + " times in one tick - income would " +
                             "DOUBLE-COUNT for a building whose ResourceCollector is registered");

            // The withheld branch must be audible, not a silent skip.
            if (code.IndexOf("no-live-collector", StringComparison.Ordinal) < 0)
                notes.Add("the built-but-no-live-collector branch does not carry the 'no-live-collector' trace key; a wiring bug " +
                          "there would be invisible in a capture");
        }

        // =====================================================================
        //  CASE 3 - an empty town earns ZERO (the headline behavioral assertion)
        // =====================================================================

        private static void Case3_EmptyTownZero(List<string> failures, List<string> notes)
        {
            var empty = new List<string>();
            var unrelated = new List<string> { "workshop", "barracks", "pet-house", "lumberyard" };

            foreach (var id in ResourceBuildingProgression.OrderedIds)
            {
                var cat = ResourceBuildingHarvester.CatalogIdsForBuilding(id);
                if (cat == null || cat.Count == 0)
                {
                    failures.Add("[empty-town-zero] CatalogIdsForBuilding('" + id + "') resolved NOTHING - the gate would have " +
                                 "no id to test against the ledger");
                    continue;
                }

                if (ResourceBuildingHarvester.MayHarvest(cat, empty, false))
                    failures.Add("[empty-town-zero] '" + id + "' MAY HARVEST on an EMPTY town (nothing ever built, no live " +
                                 "collector) - that is the phantom-income defect: a town with nothing in it earning free, " +
                                 "uncapped, auto-banked income from t=0");

                if (ResourceBuildingHarvester.MayHarvest(cat, unrelated, false))
                    failures.Add("[empty-town-zero] '" + id + "' MAY HARVEST off a ledger holding only unrelated ids [" +
                                 string.Join(",", unrelated) + "] - building a workshop must not switch on the farm");

                if (ResourceBuildingHarvester.MayHarvest(cat, null, false))
                    failures.Add("[empty-town-zero] '" + id + "' MAY HARVEST off a NULL ledger - an absent/legacy list must read " +
                                 "as 'nothing built', never as 'everything built'");
            }
        }

        // =====================================================================
        //  CASE 4 - a built farm earns farm income (and nothing else does)
        // =====================================================================

        private static void Case4_BuiltEarns(List<string> failures, List<string> notes)
        {
            var farmIds = ResourceBuildingHarvester.CatalogIdsForBuilding(ResourceBuildingProgression.FarmId);
            var millIds = ResourceBuildingHarvester.CatalogIdsForBuilding(ResourceBuildingProgression.LumbermillId);
            var forgeIds = ResourceBuildingHarvester.CatalogIdsForBuilding(ResourceBuildingProgression.ForgeId);
            if (farmIds == null || farmIds.Count == 0)
            {
                failures.Add("[built-earns] the farm resolves to no collector catalog id - the gate can never open for it");
                return;
            }

            var builtFarm = new List<string>(farmIds);

            if (!ResourceBuildingHarvester.MayHarvest(farmIds, builtFarm, false))
                failures.Add("[built-earns] a town with a BUILT farm (its id in EverBuiltStructureIds) may NOT harvest - the gate " +
                             "is too tight and the player's own building earns nothing");

            if (ResourceBuildingHarvester.MayHarvest(millIds, builtFarm, false))
                failures.Add("[built-earns] building the FARM also switched on the LUMBERMILL - the gate must be per-id");
            if (ResourceBuildingHarvester.MayHarvest(forgeIds, builtFarm, false))
                failures.Add("[built-earns] building the FARM also switched on the FORGE - the gate must be per-id");

            // MarkEverBuilt records OrdinalIgnoreCase (GameState.cs:538-547); the gate must agree.
            var upper = new List<string>();
            foreach (var f in builtFarm) upper.Add(f.ToUpperInvariant());
            if (!ResourceBuildingHarvester.MayHarvest(farmIds, upper, false))
                failures.Add("[built-earns] the ledger match is case-SENSITIVE - GameState.MarkEverBuilt/HasEverBuilt are " +
                             "OrdinalIgnoreCase, so a case-variant id must still read as built");

            // A LIVE standing collector is the strongest existence proof there is: it exists
            // right now. It must produce even before/without a ledger entry (a migrated save
            // replays its BaseLayout collectors, and the ledger seed is a separate leg).
            if (!ResourceBuildingHarvester.MayHarvest(farmIds, new List<string>(), true))
                failures.Add("[built-earns] a LIVE registered ResourceCollector does not open the gate - a collector standing in " +
                             "the world is proof the building exists and must always be allowed to accrue");

            // No double-count: the payout routes to the collector and RETURNS. Proven at
            // source in case 2 (exactly one Accrue, no Grant/Credit after it); asserted here
            // as the rule half - a live collector never ALSO opens a second wallet path,
            // because the wallet path no longer exists.
            string raw = ReadText(HarvesterSrc, failures);
            if (raw == null) return;
            string code = StripComments(raw);
            int accrueAt = code.IndexOf(".Accrue(", StringComparison.Ordinal);
            if (accrueAt >= 0)
            {
                string after = code.Substring(accrueAt);
                if (Regex.IsMatch(after, @"\.\s*Grant\s*\(") || Regex.IsMatch(after, @"Credit\s*\("))
                    failures.Add("[built-earns] a wallet grant follows the collector Accrue in the same tick - a registered " +
                                 "collector would be paid TWICE (pending pool AND wallet)");
            }
        }

        // =====================================================================
        //  CASE 5 - the id map is unambiguous, and the storefront is not the collector
        // =====================================================================

        private static void Case5_CatalogMap(List<string> failures, List<string> notes)
        {
            string raw = ReadText(StructuresRes, failures);
            if (raw == null) return;

            string sa = File.Exists(StructuresSA) ? File.ReadAllText(StructuresSA) : null;
            if (sa != null && !string.Equals(raw, sa, StringComparison.Ordinal))
                notes.Add("structures-catalog.json Resources and StreamingAssets copies differ (tracked by the catalog suites)");

            var entries = JObject.Parse(raw)["entries"] as JArray;
            if (entries == null)
            {
                failures.Add("[catalog-map] structures-catalog.json has no 'entries' array");
                return;
            }

            var typeById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var collectorsFor = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                string id = (string)e["id"];
                if (string.IsNullOrEmpty(id)) continue;
                string type = (string)e["type"] ?? "";
                typeById[id] = type;
                if (!string.Equals(type, "Collector", StringComparison.OrdinalIgnoreCase)) continue;

                var repo = e["repo"];
                string bid = repo != null ? (string)repo["collectorBuildingId"] : null;
                if (string.IsNullOrEmpty(bid)) bid = id;
                if (!collectorsFor.TryGetValue(bid, out var list))
                {
                    list = new List<string>();
                    collectorsFor[bid] = list;
                }
                list.Add(id);
            }

            foreach (var id in ResourceBuildingProgression.OrderedIds)
            {
                if (!collectorsFor.TryGetValue(id, out var rows) || rows.Count == 0)
                {
                    failures.Add("[catalog-map] progression building '" + id + "' has NO Collector row in structures-catalog.json " +
                                 "(no repo.collectorBuildingId points at it) - it can never be built, so it must never earn");
                    continue;
                }
                if (rows.Count > 1)
                    failures.Add("[catalog-map] progression building '" + id + "' maps to " + rows.Count + " Collector rows (" +
                                 string.Join(",", rows) + ") - the existence gate cannot say which one proves it was built");

                // The BARE id must not itself be a Collector row: 'lumbermill' and 'forge'
                // are ALSO GameplayBuilding storefront rows, and accepting the bare id would
                // let building the storefront open the collector's harvest gate.
                if (typeById.TryGetValue(id, out var bareType) &&
                    string.Equals(bareType, "Collector", StringComparison.OrdinalIgnoreCase))
                    notes.Add("bare id '" + id + "' is itself a Collector row; the gate's bare-id exclusion is now moot for it");
            }
        }

        // =====================================================================
        //  CASE 6 - the zero-seed founding sequence still bootstraps (soft-lock guard)
        // =====================================================================

        private static void Case6_FoundingFlow(List<string> failures, List<string> notes)
        {
            // (a) The freebies ARE the starting budget - there is no resource seed to fall
            //     back on, so anything the FTUE demands must be placeable for nothing.
            if (DeNelle.Core.State.StartingBudget.StrategicWood != 0 ||
                DeNelle.Core.State.StartingBudget.StrategicIron != 0)
                notes.Add("StartingBudget is no longer 0/0 (wood " + DeNelle.Core.State.StartingBudget.StrategicWood +
                          ", iron " + DeNelle.Core.State.StartingBudget.StrategicIron + ") - the founding freebies are no " +
                          "longer the only starting budget; re-read this case's premise");

            // (b) A freshly PLACED collector is level 1. With the direct-grant fallback gone,
            //     the pending pool is the only income path, so a level-1 collector MUST be
            //     able to accrue or a zero-seed founding can never earn its first resource.
            string collectorRaw = ReadText(CollectorSrc, failures);
            if (collectorRaw != null)
            {
                string collectorCode = StripComments(collectorRaw);
                var isActive = Regex.Match(collectorCode, @"bool\s+IsActive\s*=>([^;]*);");
                if (!isActive.Success)
                    failures.Add("[founding-flow] ResourceCollector.IsActive not found - the accrual gate moved; re-point this oracle");
                else if (isActive.Groups[1].Value.IndexOf("GetLevel", StringComparison.Ordinal) >= 0)
                    failures.Add("[founding-flow] ResourceCollector.IsActive still gates accrual on the building LEVEL (" +
                                 isActive.Groups[1].Value.Trim() + ") - levels start at 1, so every freshly placed collector " +
                                 "would accrue ZERO until a paid upgrade the player cannot afford. With the phantom direct " +
                                 "grant removed this dead-locks the zero-seed founding bootstrap (owner 2026-07-13: LEVEL 1 PRODUCES)");
            }

            // (c) Every mandatory founding placement is FREE: either an explicit FoundingKit
            //     id, or a NON-TOWER catalog row (BuildModeController lane 3 - the first
            //     placement of each distinct non-tower id is free).
            string bmRaw = ReadText(BuildModeSrc, failures);
            string stepsRaw = ReadText(StepsRes, failures);
            string catRaw = ReadText(StructuresRes, failures);
            if (bmRaw == null || stepsRaw == null || catRaw == null) return;

            string bmCode = StripComments(bmRaw);
            int kit = bmCode.IndexOf("HashSet<string> FoundingKit", StringComparison.Ordinal);
            string kitBlock = "";
            if (kit < 0)
                notes.Add("BuildModeController.FoundingKit not found - founding affordability is verified only through the " +
                          "non-tower lane-3 freebie");
            else
            {
                int open = bmCode.IndexOf('{', kit);
                int close = open >= 0 ? bmCode.IndexOf('}', open) : -1;
                if (open >= 0 && close > open) kitBlock = bmCode.Substring(open, close - open);
            }

            var typeById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var catEntries = JObject.Parse(catRaw)["entries"] as JArray;
            if (catEntries != null)
                foreach (var e in catEntries)
                {
                    string id = (string)e["id"];
                    if (!string.IsNullOrEmpty(id)) typeById[id] = (string)e["type"] ?? "";
                }

            var steps = JObject.Parse(stepsRaw)["steps"] as JArray;
            if (steps == null)
            {
                failures.Add("[founding-flow] tutorial-steps.json has no 'steps' array");
                return;
            }

            const string Prefix = "build.structure_placed:";
            int walked = 0;
            foreach (var s in steps)
            {
                bool contextual = string.Equals((string)s["flowId"], "contextual", StringComparison.OrdinalIgnoreCase);
                if (contextual) continue;
                string signal = s["completion"] != null ? (string)s["completion"]["signal"] : null;
                if (string.IsNullOrEmpty(signal) || !signal.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string wantId = signal.Substring(Prefix.Length);
                if (string.IsNullOrEmpty(wantId)) continue;
                walked++;

                bool inKit = kitBlock.IndexOf("\"" + wantId + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
                typeById.TryGetValue(wantId, out var type);
                bool nonTower = !string.Equals(type, "Tower", StringComparison.OrdinalIgnoreCase);
                if (!inKit && !nonTower)
                    failures.Add("[founding-flow] founding step awaits placement of '" + wantId + "' (catalog type '" + type +
                                 "') which is neither in FoundingKit nor a non-tower lane-3 freebie - with wood/iron seeded at 0 " +
                                 "the player cannot afford the thing the FTUE forces them to place (soft-lock)");

                // And the demanded placement must actually TURN INCOME ON: for a collector
                // id, committing it writes that id into EverBuiltStructureIds
                // (BuildModeController.Place -> MarkEverBuilt), which is exactly what the
                // gate reads. Prove the round trip for every collector the FTUE demands.
                foreach (var bid in ResourceBuildingProgression.OrderedIds)
                {
                    var cat = ResourceBuildingHarvester.CatalogIdsForBuilding(bid);
                    if (cat == null || !cat.Contains(wantId)) continue;
                    if (!ResourceBuildingHarvester.MayHarvest(cat, new List<string> { wantId }, false))
                        failures.Add("[founding-flow] the FTUE forces the player to place '" + wantId + "' but committing it does " +
                                     "NOT open the harvest gate for '" + bid + "' - the founding collector would stand there " +
                                     "earning nothing and a zero-seed save could never buy anything");
                }
            }

            if (walked == 0)
                failures.Add("[founding-flow] no mandatory 'build.structure_placed:*' step found in tutorial-steps.json - the " +
                             "founding sequence this case walks is gone; re-point the oracle");
            else
                notes.Add("walked " + walked + " mandatory founding placement step(s)");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string ReadText(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] missing file: " + path);
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and block comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
