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

        private const string StructuresPath   = "Data/Canonical/structures-catalog.json";
        private const string TroopsPath       = "Data/Canonical/troops.json";
        private const string BarracksPath     = "Data/Canonical/barracks.json";
        private const string BuildingTiersPath= "Data/Canonical/building-tiers.json";
        private const string GearLevelsPath   = "Data/Canonical/gear-levels.json";

        /// <summary>Max bankable for a capped resource with ONE container at <paramref name="level"/> (0 = none).</summary>
        private static int MaxBankable(int level)
        {
            if (level <= 0) return BaseCap;
            int idx = Mathf.Clamp(level, 1, LevelMultipliers.Length) - 1;
            return BaseCap + Mathf.RoundToInt(ContainerUnit * LevelMultipliers[idx]);
        }

        /// <summary>The absolute ceiling: one container at the single ceiling RepoProps.MaxStructureLevel.</summary>
        private static int AbsoluteMax => MaxBankable(RepoProps.MaxStructureLevel);

        /// <summary>Lowest container level whose cap holds <paramref name="amount"/> within the safety margin; -1 = never.</summary>
        private static int RequiredLevel(int amount)
        {
            for (int l = 0; l <= RepoProps.MaxStructureLevel; l++)
                if (amount <= SafetyFraction * MaxBankable(l)) return l;
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
            log.AppendLine("=== EconomySinkCapRegression [sink-cap] (no authored cost exceeds max bankable) ===");
            log.AppendLine("  ceiling = base " + BaseCap + " + one container at L" + RepoProps.MaxStructureLevel +
                           " = " + AbsoluteMax + "; safety margin " + (SafetyFraction * 100f) + "%");
            log.AppendLine("  crystals + coins are UNCAPPED BY DESIGN (TownBankCapacity.UncappableResources) - not scanned");

            try
            {
                CaseCeiling(failures, log, ref scanned);
                CaseStorageLadderSelfAffordable(failures, log);
                CaseTroopsUngated(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("[sink-cap] EconomySinkCapRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "SINK CAP OK - " + scanned + " authored cost value(s) across 5 catalog(s) all fit the " +
                         "max bankable amount for their resource (ceiling " + AbsoluteMax + " at container L" +
                         RepoProps.MaxStructureLevel + "); the 3 storage ladders are self-affordable at every " +
                         "step; troop training stays affordable at ZERO storage.";
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

            log.AppendLine("  [ceiling] " + scanned + " authored cost value(s) scanned against " + AbsoluteMax);
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
