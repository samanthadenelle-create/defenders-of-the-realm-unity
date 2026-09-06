// =============================================================================
// EconomySinkCapRegression [sink-cap] -- NO AUTHORED COST MAY EXCEED THE MAXIMUM
// BANKABLE AMOUNT OF THAT RESOURCE.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only; references DeNelle.Core).
// Contract mirrors the other Run(out reason) oracles:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: SINK_CAP_OK (Debug.Log) / SINK_CAP_FAIL (LogError)
//
// THE DEFECT CLASS THIS EXISTS TO CATCH (owner economy pass, 2026-08-21):
//   A cost of 3,000 wood under a 2,000 wood cap is UNCOMPLETABLE. The player can
//   never HOLD enough at once, the button never lights, and NOTHING in the game
//   explains why -- there is no "your storage is too small" copy anywhere. It is
//   the same shape as the day-1 daily quest that force-returned forever because it
//   could never tick: not a crash, not an error, just a piece of content that is
//   silently, permanently unreachable. A gate is the only detector, because the
//   symptom is the ABSENCE of an event.
//
// THE CAP LADDER (verified at source 2026-08-21, do not restate from memory):
//   * Base store per capped resource      -- storage-caps.json baseCap = 2000
//     (wood/iron/food). TownBankCapacity.BaseCapOf floors any answer at
//     AbsoluteMinBaseCap = 1000, so a corrupt file cannot soft-lock a save.
//   * One container adds structures-catalog storageCapacity (1000) x
//     levelCapacityMultipliers[level-1] = [1,2,4,8,16,32] -- i.e. 1k/2k/4k/8k/
//     16k/32k AT levels 1..6. Containers: lumberyard=wood, foundry=iron, silo=food.
//   * So max bankable with ONE container = 2000 + {0,1k,2k,4k,8k,16k,32k}
//     = 2000 / 3000 / 4000 / 6000 / 10000 / 18000 / 34000 at container level 0..6.
//   * RepoProps.MaxStructureLevel = 6 is the SINGLE ceiling. This oracle reads it
//     rather than re-hardcoding a 6 -- re-hardcoding a level ceiling is banned.
//
// ⛔ CRYSTALS AND COINS ARE UNCAPPED BY DESIGN and are therefore OUT OF SCOPE here
// (owner ruling 2026-08-04, CoC-gems precedent). The exemption lives in C# --
// TownBankCapacity.UncappableResources -- and storage-caps.json IGNORES a crystals
// key with a warn rather than honouring it. A future seat reading only the JSON
// would conclude crystals are capped at whatever it finds; they are not. This is
// WHY the crystal-priced endgame ladders below are allowed to run to five figures.
//
// WHY THE BOUND IS **ONE** CONTAINER, not the true maximum: nothing caps how many
// lumberyards a player may place, so the theoretical bank is unbounded. Bounding at
// one container is the CONSERVATIVE reading -- it guarantees a single-container
// player can complete every cost. Loosening it to "build a second lumberyard" would
// make the gate pass costs that are completable only via an undiscoverable
// workaround, which is precisely the silent wall the gate exists to forbid.
//
// THE SAFETY MARGIN (SafetyFraction = 0.85): a cost is judged against 85% of the
// cap, not 100%. A cost that needs 100.0% of the bank is technically completable
// and practically miserable -- it demands a perfectly empty store and one exact
// collection. The margin is what makes "completable" mean "reachable in play".
//
// WHAT EACH CASE PINS
//   1 [ceiling]  no authored cost exceeds the ABSOLUTE max bankable (container L6).
//                This is the hard uncompletable-forever check and it covers EVERY
//                capped resource in EVERY scanned file.
//   2 [storage-ladder]  the storage rows are SELF-AFFORDABLE: the cost to upgrade a
//                container from level N to N+1 must fit inside the cap the player
//                actually has AT LEVEL N. This is the tightest constraint in the
//                economy and the easiest to break by scaling costs uniformly -- a
//                flat multiplier on a ladder that already doubles per step walks the
//                cost off the end of the cap ladder it is gated behind.
//   3 [troops-ungated]  troop TRAINING stays affordable at ZERO storage. Training is
//                the recurring loop; gating the recurring loop behind a storage
//                upgrade would stall the whole game, not pace it.
//   4 [cap-copy-reachable]  (WO-1425) the affordability REFUSAL PATH consults
//                TownBankCapacity at all, and the helper behind it produces a sentence
//                naming the container level -- and stays SILENT for a cost that fits and
//                for the uncapped currencies.
//   5 [cap-copy-ladder]  (WO-1425) the container level the player is TOLD matches the
//                level this gate derives, for every authored cost above the base store.
//
// ⚠ WHAT CASES 1-3 DO NOT PROVE, and why 4-5 exist. On 2026-09-06 the owner reported
// "some items you cannot upgrade cause you can not get enough resources to use because
// of ceilings" against a build on which THIS ORACLE PASSED. It was not wrong: every cost
// is completable with a maxed container. It was measuring the wrong thing. The failure
// was DISCOVERABILITY -- 'tower_ground_archer' L2->L3 costs 3150 wood, a level-1
// lumberyard tops the bank out at 3000, and the refusal said only "Not enough Wood
// (3150)" while the bar sat full at 3000/3000. Proving a cost is payable EVENTUALLY,
// while never proving the player can be TOLD what to do, is how a silent wall ships
// behind a green gate. Cases 4-5 pin the telling.
//
// THE GATE IS THE MODEL'S ONLY MEMORY. The 2026-08-21 sink pass chose its
// multipliers by computing required container levels; those levels are re-derived
// here from the data every run, so a later cost edit cannot silently invalidate the
// pass's arithmetic.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.Catalog;

namespace DeNelle.Editor.Regression
{
    public static class EconomySinkCapRegression
    {
        // --- the capped resources. Crystals/coins are UNCAPPED and deliberately absent.
        private const int BaseCap = 2000;                 // storage-caps.json baseCap (wood/iron/food)
        private const int ContainerUnit = 1000;           // structures-catalog storageCapacity on the 3 container rows
        private static readonly float[] LevelMultipliers = { 1f, 2f, 4f, 8f, 16f, 32f };
        private const float SafetyFraction = 0.85f;

        /// <summary>The three container rows, and which resource each one's capacity backs.</summary>
        private static readonly Dictionary<string, string> StorageRows =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "lumberyard", "wood" },
                { "foundry",    "iron" },
                { "silo",       "food" },
            };

        private static readonly string[] CappedResources = { "wood", "iron", "food" };

        /// <summary>WO-1425 -- one authored cost that exceeds the container-less base store, i.e. one
        /// the player must own storage to pay. These are the costs whose refusal MUST say so.</summary>
        private readonly struct OverBaseCost
        {
            public readonly string Where;
            public readonly string Resource;
            public readonly int Amount;
            public OverBaseCost(string where, string resource, int amount)
            { Where = where; Resource = resource; Amount = amount; }
        }

        private static readonly List<OverBaseCost> _overBase = new List<OverBaseCost>();

        private const string StructuresPath   = "Data/Canonical/structures-catalog.json";
        private const string TroopsPath       = "Data/Canonical/troops.json";
        private const string BarracksPath     = "Data/Canonical/barracks.json";
        private const string BuildingTiersPath= "Data/Canonical/building-tiers.json";
        private const string GearLevelsPath   = "Data/Canonical/gear-levels.json";
        // WO-1425 -- both author capped-resource costs and were OUTSIDE the scan, so the
        // [ceiling] guarantee simply did not cover them. Verified at source 2026-09-06: each
        // recipe row carries a flat `cost` object keyed { wood, food, iron, crystals }.
        private const string GearRecipesPath    = "Data/Canonical/gear-recipes.json";
        private const string JewelerRecipesPath = "Data/Canonical/jeweler-recipes.json";

        /// <summary>Every canonical file this oracle scans -- the count in the pass reason is DERIVED
        /// from this array, never restated (a hand-maintained "5 catalog(s)" is duplicated state).</summary>
        private static readonly string[] ScannedPaths =
        {
            StructuresPath, TroopsPath, BarracksPath, BuildingTiersPath, GearLevelsPath,
            GearRecipesPath, JewelerRecipesPath,
        };

        /// <summary>
        /// Max bankable for a capped resource with ONE container at <paramref name="level"/> (0 = none).
        ///
        /// ⛔ "ONE container" IS NOW A RULE, NOT A CONVENIENCE. Do not "generalise" this back to a
        /// sum over N containers. OWNER RULING 23 (2026-09-06, WO-2005), verbatim: *"also cap only
        /// one of each storage type, the idea is they should level them"* / *"if we decide one day
        /// we need more space we add another level easy."* `lumberyard`, `foundry` and `silo` carry
        /// <c>repo.singleton = true</c> in structures-catalog.json as of that ruling, so at most one
        /// of each can be placed and this arithmetic is exact rather than merely a lower bound.
        ///
        /// Before the ruling <c>TownBankCapacity.MaxOf</c> (:435-442) summed capacity over EVERY
        /// built container of a resource, so a second Lumberyard bought another full container's
        /// worth of wood ceiling for less than the L5-to-L6 rung cost - an undiscoverable second
        /// axis that made the level ladder pointless. Capacity now has ONE axis of growth: LEVEL.
        /// Raising the ceiling later is a DATA edit (a rung, or storage-caps.json's
        /// levelCapacityMultipliers), never a second building.
        ///
        /// ⚠ MaxOf's SUM is deliberately unchanged: existing saves that already hold two containers
        /// are GRANDFATHERED and keep their summed ceiling. Nothing is destroyed and no capacity is
        /// clamped - a clamp would delete resources the player paid for. The singleton flag only
        /// refuses the NEXT placement, and that is PROVEN, not assumed: every branch of
        /// <c>StructureSingleton.EnforceInternal</c> (:279-325) acts solely on <c>repo.bakedTwins</c>
        /// (<c>StandDownBakedTwins</c> :401-410 early-returns when a row authors none), and these
        /// three rows author none - so the hub-load sweep cannot touch a placed body or a BaseLayout
        /// record. So this arithmetic is the ceiling for every NEW town and a LOWER BOUND for a
        /// grandfathered one, which is the safe direction for a cap oracle.
        /// </summary>
        private static int MaxBankable(int level)
        {
            if (level <= 0) return BaseCap;
            int idx = Mathf.Clamp(level, 1, LevelMultipliers.Length) - 1;
            return BaseCap + Mathf.RoundToInt(ContainerUnit * LevelMultipliers[idx]);
        }

        /// <summary>The absolute ceiling: one container at the single ceiling RepoProps.MaxStructureLevel.</summary>
        private static int AbsoluteMax => MaxBankable(RepoProps.MaxStructureLevel);

        /// <summary>Lowest container level whose cap holds <paramref name="amount"/> within the safety margin; -1 = never.</summary>
        private static int RequiredLevel(int amount) => RequiredLevelAt(amount, SafetyFraction);

        /// <summary>Lowest container level whose cap holds <paramref name="amount"/> at an EXPLICIT
        /// margin; -1 = never. WO-1425 needs margin 1.0 to compare against the player-facing helper,
        /// which has no safety fraction (the sentence must name the level that ACTUALLY holds the
        /// cost, not the level that holds it comfortably).</summary>
        private static int RequiredLevelAt(int amount, float fraction)
        {
            for (int l = 0; l <= RepoProps.MaxStructureLevel; l++)
                if (amount <= fraction * MaxBankable(l)) return l;
            return -1;
        }

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SINK_CAP_OK - " + reason);
            else Debug.LogError("SINK_CAP_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            int scanned = 0;
            _overBase.Clear();   // WO-1425 -- the table is per-run, never cumulative across domain reloads
            log.AppendLine("=== EconomySinkCapRegression [sink-cap] (no authored cost exceeds max bankable) ===");
            log.AppendLine("  ceiling = base " + BaseCap + " + one container at L" + RepoProps.MaxStructureLevel +
                           " = " + AbsoluteMax + "; safety margin " + (SafetyFraction * 100f) + "%");
            log.AppendLine("  crystals + coins are UNCAPPED BY DESIGN (TownBankCapacity.UncappableResources) - not scanned");

            try
            {
                CaseCeiling(failures, log, ref scanned);
                CaseStorageLadderSelfAffordable(failures, log);
                CaseTroopsUngated(failures, log);
                CaseCapCopyReachable(failures, log);     // WO-1425
                CaseCapCopyLadder(failures, log);        // WO-1425 -- must run AFTER CaseCeiling (fills _overBase)
            }
            catch (Exception ex)
            {
                failures.Add("[sink-cap] EconomySinkCapRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "SINK CAP OK - " + scanned + " authored cost value(s) across " + ScannedPaths.Length +
                         " catalog(s) all fit the max bankable amount for their resource (ceiling " + AbsoluteMax +
                         " at container L" + RepoProps.MaxStructureLevel + "); the 3 storage ladders are " +
                         "self-affordable at every step; troop training stays affordable at ZERO storage; " +
                         _overBase.Count + " cost(s) exceed the container-less base store (table above), " +
                         "the refusal path is WIRED to TownBankCapacity [cap-copy-reachable] and the " +
                         "container level it would name matches this gate's ladder for every one of them " +
                         "[cap-copy-ladder] (WO-1425).";
                Debug.Log("SINK_CAP_OK\n" + log);
                return true;
            }
            reason = "sink-cap: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("SINK_CAP_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  Read one canonical JSON file as a JToken. Returns null (and records a
        //  failure) when unreadable -- an unverifiable catalog is a FAILURE, not a
        //  pass: silence is exactly the condition this oracle refuses to accept.
        // =====================================================================
        private static JToken Load(string relPath, List<string> failures, StringBuilder log)
        {
            string json = DeNelle.Core.CanonicalJson.Read(relPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[sink-cap] " + relPath + " unreadable (CanonicalJson.Read returned empty) - " +
                             "its costs cannot be verified against the bank cap at all");
                return null;
            }
            try
            {
                var tok = JToken.Parse(json);
                log.AppendLine("  loaded " + relPath);
                return tok;
            }
            catch (Exception ex)
            {
                failures.Add("[sink-cap] " + relPath + " failed to parse: " + ex.Message);
                return null;
            }
        }

        private static int IntAt(JToken t, string prop)
        {
            var v = t?[prop];
            return v != null && v.Type == JTokenType.Integer ? v.Value<int>() : 0;
        }

        /// <summary>Record one authored cost against the absolute ceiling.</summary>
        private static void Check(string where, string resource, int amount,
                                  List<string> failures, ref int scanned)
        {
            if (amount <= 0) return;
            scanned++;

            // WO-1425 -- every cost that a player CANNOT HOLD without a storage container is
            // recorded here, so SINK_CAP_OK prints the required-container-level table on every
            // build and the [cap-copy-ladder] case can re-derive it through the player-facing
            // helper. Recording is not judging: [ceiling] below is still the only pass/fail.
            if (amount > BaseCap) _overBase.Add(new OverBaseCost(where, resource, amount));

            if (amount > AbsoluteMax)
            {
                failures.Add("[ceiling] " + where + " costs " + amount + " " + resource + " but the MAXIMUM " +
                             "BANKABLE " + resource + " is " + AbsoluteMax + " (base " + BaseCap + " + one " +
                             "container at the RepoProps.MaxStructureLevel ceiling of " + RepoProps.MaxStructureLevel +
                             "). The player can NEVER HOLD enough at once, so this cost is UNCOMPLETABLE FOREVER " +
                             "and nothing in the game explains why. Lower the cost, or raise the container ladder " +
                             "in storage-caps.json - do NOT rely on the player building a second container.");
            }
        }

        // =====================================================================
        //  CASE 1 -- the ceiling, across every scanned catalog.
        // =====================================================================
        private static void CaseCeiling(List<string> failures, StringBuilder log, ref int scanned)
        {
            // --- structures-catalog.json: repo.cost + repo.upgradeCost[]
            var structures = Load(StructuresPath, failures, log);
            var entries = structures?["entries"] as JArray;
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    string id = (string)e["id"] ?? "?";
                    var repo = e["repo"];
                    if (repo == null) continue;
                    foreach (string r in CappedResources)
                        Check("structures-catalog '" + id + "' build cost", r, IntAt(repo["cost"], r), failures, ref scanned);
                    if (repo["upgradeCost"] is JArray ups)
                        for (int i = 0; i < ups.Count; i++)
                            foreach (string r in CappedResources)
                                Check("structures-catalog '" + id + "' upgrade step " + (i + 1), r,
                                      IntAt(ups[i], r), failures, ref scanned);
                }
            }

            // --- troops.json: costWood / costIron / costFood
            var troops = Load(TroopsPath, failures, log);
            foreach (var t in EnumerateTroops(troops))
            {
                string id = (string)t["id"] ?? "?";
                Check("troops '" + id + "' training", "wood", IntAt(t, "costWood"), failures, ref scanned);
                Check("troops '" + id + "' training", "iron", IntAt(t, "costIron"), failures, ref scanned);
                Check("troops '" + id + "' training", "food", IntAt(t, "costFood"), failures, ref scanned);
            }

            // --- barracks.json: levels[].cost { wood, iron, food, ... }
            var barracks = Load(BarracksPath, failures, log);
            if (barracks?["levels"] is JArray levels)
                foreach (var lv in levels)
                    foreach (string r in CappedResources)
                        Check("barracks level " + (lv["level"] ?? "?"), r, IntAt(lv["cost"], r), failures, ref scanned);

            // --- building-tiers.json: buildings[].tiers[].costWood / costFood
            //     (costCrystal is UNCAPPED and deliberately not checked)
            var tiers = Load(BuildingTiersPath, failures, log);
            if (tiers?["buildings"] is JArray buildings)
                foreach (var b in buildings)
                {
                    string bid = (string)b["id"] ?? "?";
                    if (!(b["tiers"] is JArray ts)) continue;
                    foreach (var ti in ts)
                    {
                        string where = "building-tiers '" + bid + "' tier " + (ti["tier"] ?? "?");
                        Check(where, "wood", IntAt(ti, "costWood"), failures, ref scanned);
                        Check(where, "food", IntAt(ti, "costFood"), failures, ref scanned);
                    }
                }

            // --- gear-levels.json: bands[].costWood[] / costIron[]
            var gear = Load(GearLevelsPath, failures, log);
            if (gear?["bands"] is JArray bands)
                foreach (var bd in bands)
                {
                    string rarity = (string)bd["rarity"] ?? "?";
                    CheckArray("gear-levels band '" + rarity + "'", "wood", bd["costWood"] as JArray, failures, ref scanned);
                    CheckArray("gear-levels band '" + rarity + "'", "iron", bd["costIron"] as JArray, failures, ref scanned);
                }

            // --- WO-1425: gear-recipes.json + jeweler-recipes.json: recipes[].cost { wood, iron, food, ... }
            //     Both were outside the scan entirely, so their capped-resource costs carried NO
            //     ceiling guarantee at all. Shape verified at source 2026-09-06 -- a flat `cost`
            //     object per recipe row, exactly like barracks levels. crystals is UNCAPPED and
            //     deliberately not checked.
            foreach (string recipePath in new[] { GearRecipesPath, JewelerRecipesPath })
            {
                var doc = Load(recipePath, failures, log);
                if (!(doc?["recipes"] is JArray recipes)) continue;
                foreach (var rec in recipes)
                {
                    string rid = (string)rec["id"] ?? "?";
                    foreach (string r in CappedResources)
                        Check(recipePath + " recipe '" + rid + "'", r, IntAt(rec["cost"], r), failures, ref scanned);
                }
            }

            log.AppendLine("  [ceiling] " + scanned + " authored cost value(s) scanned against " + AbsoluteMax);
        }

        // =====================================================================
        //  CASE 4 [cap-copy-reachable] -- WO-1425. THE REFUSAL PATH MUST CONSULT
        //  TownBankCapacity AT ALL.
        // ---------------------------------------------------------------------
        //  WHY THIS CASE EXISTS, stated plainly: this oracle PASSED on the build the
        //  owner played (2026.09.06.357599) and reported the experience as acceptable.
        //  It was right about the arithmetic -- no cost is uncompletable -- and blind to
        //  the thing that actually broke: 'tower_ground_archer' L2->L3 costs 3150 wood,
        //  a save with a level-1 lumberyard tops out at 3000, and the ONLY thing the
        //  player was told was "Not enough Wood (3150)" while their bar sat full. Proving
        //  "completable eventually" and never "the player can be told what to do" is how
        //  a silent wall ships with a green gate.
        //
        //  Case 4a is a SOURCE SCAN. That is deliberately crude and deliberately narrow:
        //  it asserts the ONE structural link -- that BuildModeController's shortfall copy
        //  names TownBankCapacity -- because the absence of that link is the entire defect
        //  and a link is the one thing a source scan can prove without a scene, a save or
        //  an EconomyService. Case 4b then proves the helper behind it actually SPEAKS.
        //
        //  REVERT RECIPE for 4a (prove it RED): in BuildModeController.ShortfallMessage
        //  (:3296 as written) REPLACE the line `string capBlock = CapBlockMessage(cost);` with
        //  `string capBlock = "";`. (Deleting it outright leaves `capBlock` undeclared on
        //  the next line -- that is a COMPILE ERROR, not a red gate.)
        //  REVERT RECIPE for 4b: make TownBankCapacity.TryDescribeStorageBlock return
        //  false on its first line. (Do NOT gut only DescribeStorageBlock's
        //  reachable-level branch: with an unloaded CatalogRegistry the message comes from
        //  the no-container branch instead, which still satisfies the assertion, so that
        //  edit can leave the case GREEN.)
        // =====================================================================
        private static void CaseCapCopyReachable(List<string> failures, StringBuilder log)
        {
            // --- 4a: the link exists in the refusal path -------------------------------
            // Application.dataPath, not a repo-relative literal: the batch CWD is not guaranteed and
            // the repo ROOT is machine-dependent (C:\eoa on one seat, D:\eoa on another) -- a
            // hardcoded root is exactly how a doc-followed path lands somewhere that does not exist.
            string controllerPath = System.IO.Path.Combine(Application.dataPath,
                "_Modules/Village/BuildMode/BuildModeController.cs");
            string src = null;
            try { src = System.IO.File.ReadAllText(controllerPath); }
            catch (Exception ex)
            {
                failures.Add("[cap-copy-reachable] could not read " + controllerPath + " (" + ex.GetType().Name +
                             "): the one structural link between a refusal and the storage cap cannot be " +
                             "verified, and an unverifiable link is a FAILURE, not a pass.");
            }

            if (src != null)
            {
                bool hasHelper = src.Contains("public static string CapBlockMessage(");
                bool wired     = src.Contains("string capBlock = CapBlockMessage(cost);");
                bool readsCap  = src.Contains("TownBankCapacity.TryDescribeStorageBlock(");

                if (!hasHelper || !wired || !readsCap)
                    failures.Add("[cap-copy-reachable] BuildModeController's affordability refusal does NOT " +
                                 "consult TownBankCapacity (helper=" + hasHelper + ", wired=" + wired +
                                 ", readsCap=" + readsCap + "). A cost the bank can NEVER HOLD then reads " +
                                 "exactly like a cost the player has not saved up for yet -- the owner's " +
                                 "2026-09-06 report, and the defect this oracle certified as acceptable " +
                                 "because it only ever judged arithmetic. The refusal must name the " +
                                 "container, the level and the capacity that unblocks the cost.");
                else
                    log.AppendLine("  [cap-copy-reachable] BuildModeController.ShortfallMessage consults " +
                                   "TownBankCapacity via CapBlockMessage - the link is present");
            }

            // --- 4b: the helper actually produces the sentence --------------------------
            // Driven at the CONTAINER-LESS state (no GameStateService in an editor batch, so
            // MaxOf resolves to baseCap): 3150 wood -- the exact cost from the owner's report.
            const int archerL3Wood = 3150;
            string msg = DeNelle.Core.Economy.TownBankCapacity.StorageBlockMessage(
                DeNelle.Core.Economy.BankResource.Wood, archerL3Wood);

            if (string.IsNullOrEmpty(msg))
            {
                failures.Add("[cap-copy-reachable] TownBankCapacity.StorageBlockMessage(Wood, " + archerL3Wood +
                             ") returned NOTHING against a bank ceiling of " +
                             DeNelle.Core.Economy.TownBankCapacity.MaxOf(DeNelle.Core.Economy.BankResource.Wood) +
                             ". The player-facing sentence for a cap block does not exist, so the refusal " +
                             "falls back to the generic shortfall line and the wall stays invisible.");
            }
            else if (msg.IndexOf("storage", StringComparison.OrdinalIgnoreCase) < 0 &&
                     msg.IndexOf("level", StringComparison.OrdinalIgnoreCase) < 0)
            {
                failures.Add("[cap-copy-reachable] the cap-block sentence names neither a storage level nor " +
                             "storage at all: \"" + msg + "\". The copy must state the WAY OUT, not just " +
                             "that something is wrong.");
            }
            else
            {
                log.AppendLine("  [cap-copy-reachable] cap-block copy at zero storage: \"" + msg + "\"");
            }

            // A cost that FITS must stay silent, or every ordinary shortfall grows a false wall.
            string quiet = DeNelle.Core.Economy.TownBankCapacity.StorageBlockMessage(
                DeNelle.Core.Economy.BankResource.Wood, 100);
            if (!string.IsNullOrEmpty(quiet))
                failures.Add("[cap-copy-reachable] a 100-wood cost produced a cap-block sentence (\"" + quiet +
                             "\"). The cap story must appear ONLY when the bank genuinely cannot hold the " +
                             "cost - otherwise it becomes noise on every ordinary shortfall and stops being read.");

            // Crystals are UNCAPPED by design -- a cap sentence for them would be a fabricated wall.
            string crystal = DeNelle.Core.Economy.TownBankCapacity.StorageBlockMessage(
                DeNelle.Core.Economy.BankResource.Crystals, 999999);
            if (!string.IsNullOrEmpty(crystal))
                failures.Add("[cap-copy-reachable] a CRYSTAL cost produced a cap sentence (\"" + crystal +
                             "\"). Crystals and coins are UNCAPPED by design (owner ruling 2026-08-04, " +
                             "TownBankCapacity.UncappableResources) - telling the player to build storage " +
                             "for them invents a wall that does not exist.");
        }

        // =====================================================================
        //  CASE 5 [cap-copy-ladder] -- WO-1425. WHAT THE PLAYER IS TOLD MUST AGREE
        //  WITH WHAT THIS GATE PROVED.
        // ---------------------------------------------------------------------
        //  The sentence names a container LEVEL. This case re-derives that level for
        //  EVERY authored cost above the container-less base store, through the
        //  player-facing helper, and requires it to match this oracle's own ladder at
        //  margin 1.0. Two ladders that disagree is the duplicated-state failure this
        //  repo pays for over and over -- here it would mean the game tells the player
        //  to reach a level that does not hold the cost.
        //
        //  It also uses RequiredLevel(), which was defined and NEVER CALLED: the gate
        //  computed the levels the 2026-08-21 sink pass reasoned about and then threw
        //  them away, which is why nothing noticed the levels were never surfaced.
        //
        //  REVERT RECIPE (prove it RED): change LevelMultipliers in this file to
        //  { 1f, 2f, 3f, 8f, 16f, 32f } -- the L3 rung drops from 6000 to 5000, so
        //  'tower_siege_tower' L2->L3 (5600 wood, MEASURED) resolves to L3 through the
        //  player-facing helper and L4 through this oracle, and the case fails.
        //  (Do NOT tamper with the L6 rung: no authored cost currently lands above the
        //  L5 rung of 18000, so a top-rung edit changes nothing and leaves this GREEN.)
        // =====================================================================
        private static void CaseCapCopyLadder(List<string> failures, StringBuilder log)
        {
            // The two ladders must start from the SAME base, or every comparison below is
            // measuring the wrong thing. Named explicitly so a divergence says WHICH side moved.
            int coreBaseWood = DeNelle.Core.Economy.TownBankCapacity.BaseCapOf(DeNelle.Core.Economy.BankResource.Wood);
            if (coreBaseWood != BaseCap)
            {
                failures.Add("[cap-copy-ladder] TownBankCapacity.BaseCapOf(Wood) = " + coreBaseWood +
                             " but this oracle's BaseCap constant is " + BaseCap + ". Either storage-caps.json " +
                             "moved (update the constant AND the header ladder above) or the file failed to " +
                             "load in this batch (BaseCapOf floors at AbsoluteMinBaseCap " +
                             DeNelle.Core.Economy.TownBankCapacity.AbsoluteMinBaseCap + "). Until they agree, " +
                             "the level the player is told and the level this gate proved are not comparable.");
                return;
            }

            int mismatches = 0;
            var table = new StringBuilder();
            table.AppendLine("  [cap-copy-ladder] REQUIRED CONTAINER LEVEL per authored cost above the " +
                             "container-less base store of " + BaseCap + ":");

            foreach (var c in _overBase)
            {
                if (!DeNelle.Core.Economy.TownBankCapacity.TryParseResource(c.Resource, out var br)) continue;

                int coreLevel = DeNelle.Core.Economy.TownBankCapacity.RequiredContainerLevel(
                    br, c.Amount, ContainerUnit, RepoProps.MaxStructureLevel, out int coreCap);
                int oracleLevel = RequiredLevelAt(c.Amount, 1f);

                table.AppendLine("    " + c.Amount.ToString().PadLeft(6) + " " + c.Resource.PadRight(5) +
                                 " -> container L" + coreLevel + " (bank holds " + coreCap + ")   " + c.Where);

                if (coreLevel != oracleLevel)
                {
                    mismatches++;
                    failures.Add("[cap-copy-ladder] " + c.Where + " costs " + c.Amount + " " + c.Resource +
                                 ": the PLAYER-FACING helper says container level " + coreLevel +
                                 " but this gate's ladder says " + oracleLevel + ". The sentence the player " +
                                 "reads would send them to a level that does not hold the cost. One ladder, " +
                                 "one answer - TownBankCapacity owns it; this oracle only cross-checks it.");
                }
            }

            table.AppendLine("  [cap-copy-ladder] " + _overBase.Count + " over-base cost(s), " +
                             mismatches + " ladder mismatch(es)");
            log.Append(table);
        }

        private static void CheckArray(string where, string resource, JArray arr,
                                       List<string> failures, ref int scanned)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Count; i++)
                if (arr[i].Type == JTokenType.Integer)
                    Check(where + " step " + i, resource, arr[i].Value<int>(), failures, ref scanned);
        }

        private static IEnumerable<JToken> EnumerateTroops(JToken troops)
        {
            var arr = (troops?["troops"] as JArray) ?? (troops?["entries"] as JArray);
            if (arr == null) yield break;
            foreach (var t in arr) yield return t;
        }

        // =====================================================================
        //  CASE 2 -- the storage ladder must be SELF-AFFORDABLE.
        //  Upgrading a container from level N to N+1 is paid while the player is
        //  STILL AT LEVEL N, so upgradeCost[N-1] is judged against MaxBankable(N)
        //  for the resource that container backs. The cross-resource half (a
        //  lumberyard upgrade also costs iron, capped by the FOUNDRY) is judged at
        //  the MATCHED level -- the honest assumption that a player raises their
        //  containers roughly in step. Both halves must hold.
        // =====================================================================
        private static void CaseStorageLadderSelfAffordable(List<string> failures, StringBuilder log)
        {
            var structures = Load(StructuresPath, failures, log);
            if (!(structures?["entries"] is JArray entries)) return;

            int checkedSteps = 0;
            foreach (var e in entries)
            {
                string id = (string)e["id"] ?? "?";
                if (!StorageRows.TryGetValue(id, out string ownResource)) continue;
                var repo = e["repo"];
                if (repo == null) continue;

                // The BUILD cost is paid with NO container of that kind yet -> level 0.
                foreach (string r in CappedResources)
                {
                    int amount = IntAt(repo["cost"], r);
                    if (amount <= 0) continue;
                    checkedSteps++;
                    int budget = Mathf.RoundToInt(SafetyFraction * MaxBankable(0));
                    if (amount > budget)
                        failures.Add("[storage-ladder] '" + id + "' BUILD costs " + amount + " " + r +
                                     " but is paid before any container exists - budget is " + budget +
                                     " (" + (SafetyFraction * 100f) + "% of " + MaxBankable(0) + "). The first " +
                                     "container would be unaffordable, which locks the ENTIRE storage ladder " +
                                     "and every cost gated behind it.");
                }

                if (!(repo["upgradeCost"] is JArray ups)) continue;
                for (int i = 0; i < ups.Count; i++)
                {
                    int atLevel = i + 1;                       // paying to reach level i+2, while still at i+1
                    foreach (string r in CappedResources)
                    {
                        int amount = IntAt(ups[i], r);
                        if (amount <= 0) continue;
                        checkedSteps++;
                        int budget = Mathf.RoundToInt(SafetyFraction * MaxBankable(atLevel));
                        if (amount > budget)
                        {
                            bool own = string.Equals(r, ownResource, StringComparison.OrdinalIgnoreCase);
                            failures.Add("[storage-ladder] '" + id + "' upgrade L" + atLevel + "->L" + (atLevel + 1) +
                                         " costs " + amount + " " + r + ", over the " + budget + " budget a player " +
                                         "AT LEVEL " + atLevel + " can hold (" + (SafetyFraction * 100f) + "% of " +
                                         MaxBankable(atLevel) + "). " +
                                         (own ? "This is the container's OWN resource, so the upgrade pays for the " +
                                                "very capacity it needs - a hard self-lock: the ladder can never " +
                                                "advance past this rung."
                                              : "This is the CROSS resource (capped by a different container), judged " +
                                                "at the matched level on the assumption containers rise in step.") +
                                         " A uniform multiplier on a ladder that already doubles per step is how " +
                                         "this breaks - the cost outruns the cap ladder it is gated behind.");
                        }
                    }
                }
            }
            log.AppendLine("  [storage-ladder] " + checkedSteps + " step(s) across " + StorageRows.Count +
                           " container row(s) verified self-affordable");
        }

        // =====================================================================
        //  CASE 3 -- troop TRAINING stays affordable at ZERO storage.
        //  Training is the recurring loop the whole economy drains through. A
        //  storage prerequisite on a one-time upgrade PACES the game; the same
        //  prerequisite on the recurring loop STALLS it -- the player cannot train
        //  the army that earns the resources that buy the storage.
        // =====================================================================
        private static void CaseTroopsUngated(List<string> failures, StringBuilder log)
        {
            var troops = Load(TroopsPath, failures, log);
            int budget = Mathf.RoundToInt(SafetyFraction * MaxBankable(0));
            int n = 0;
            foreach (var t in EnumerateTroops(troops))
            {
                string id = (string)t["id"] ?? "?";
                n++;
                foreach (var pair in new[] { new[] { "wood", "costWood" }, new[] { "iron", "costIron" }, new[] { "food", "costFood" } })
                {
                    int amount = IntAt(t, pair[1]);
                    if (amount > budget)
                        failures.Add("[troops-ungated] troop '" + id + "' costs " + amount + " " + pair[0] +
                                     " to train, over the " + budget + " a player with NO storage container can " +
                                     "hold (" + (SafetyFraction * 100f) + "% of " + MaxBankable(0) + "). Troop " +
                                     "training is the RECURRING loop - gating it behind a storage upgrade stalls " +
                                     "the game rather than pacing it. Pace with one-time costs and build TIME, " +
                                     "never with the recurring loop.");
                }
            }
            log.AppendLine("  [troops-ungated] " + n + " troop(s) verified affordable at zero storage (budget " + budget + ")");
        }
    }
}
