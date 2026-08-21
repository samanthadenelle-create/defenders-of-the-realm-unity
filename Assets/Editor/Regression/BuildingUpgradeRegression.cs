// =============================================================================
// BuildingUpgradeRegression — headless oracle for the resource-building upgrade
// tables (Farm / Lumbermill / Forge) and the DEF-121 Magic-gated Arcane Forge tier.
// -----------------------------------------------------------------------------
// "Real object in -> assert -> one marker": loads the REAL ResourceBuildingProgression
// catalog through the same lazy/Guarded path the game uses (ById -> Build()) and asserts
// the leveling invariants the upgrade flow (ResourceBuildingState.TryUpgrade) depends on:
//   1. The catalog builds all three resource buildings (no type-init poison / empty fallback).
//   2. Each building has MaxLevel >= 2, ascending YieldPerTick, and a MONOTONICALLY
//      non-increasing HarvestInterval (an upgrade must never tick slower).
//   3. Every NON-max level carries a non-empty UpgradeCost; the max level carries none.
//   4. The Forge is the only building with a Magic-gated tier: its top HARVEST level
//      (level 5) is IsMagicGated with MagicCost>0 and UnlocksTechNode == arcane_forge,
//      and the appended arcane level (6) is the true IsMaxLevel with a size multiplier > 1.
//   5. Farm/Lumbermill are 5 flat levels with NO magic gate anywhere.
//
// WO-855 Phase 5 (2026-08-03) added the FAUCET BALANCE cases. Phase 0 measured a level-5
// lumbermill at 61,875 wood/hour BEFORE the xEchoCount and production-perk multipliers -
// ~196 wood/sec at a full roster, which made every sink in the game free. These cases pin
// the faucet so a future "just bump the yield" edit fails loudly instead of silently
// re-breaking the economy:
//   6. [gather-faucet] Income-per-HOUR at three representative states (fresh / mid / maxed)
//      stays inside a target band, computed off the REAL catalog through the same
//      yield x sizeMult x interval math ResourceBuildingHarvester ticks. Includes the
//      early-game FLOOR - a fresh town must still earn enough to buy its first structure
//      in minutes, so a grind pass can never deadlock the bootstrap.
//   7. [perk-stack] For EVERY building in BOTH copies of building-tiers.json, the compounded
//      production multiplier (top tier x every owned perk - ModifierService.Compute multiplies
//      the CURRENT tier def by all owned perks) stays under WO-855 section 4.8's +80% cap.
//   8. [echo-scaling] The Echo faucet's total scaling from 1 -> 6 Echoes stays inside a band.
//      This deliberately PINS the WO-709 owner ruling that echo income is quadratic-in-count
//      (EchoService.RatePerSecond multiplies EchoCount by AggregateHarvestMultiplier, which
//      folds the count spine in a second time - x4 total at 2 Echoes, exactly as WO-709
//      specifies). It is INTENDED, reaffirmed by docs/design/ECONOMY_PROGRESSION_THESIS_
//      2026-08-02.md ("12-15x at full roster is NOT a bug - it is the milestone"), so this
//      case fails BOTH if someone linearises it and if someone inflates it - either way the
//      change must be deliberate and owner-ruled, never incidental.
//
// WO-856 (2026-08-04) added the GENERIC producer guard, sibling to case 9
// [upgrader-reaches-receiver] and born of the same lesson:
//   10. [yield-reachable-at-founding] For EVERY catalog row authoring a per-wave /
//      per-tick yield curve: the FIRST rung must be > 0 (no structure may deliver
//      nothing at its founding level), Clamp(repo.maxLevel,1,RepoProps.MaxStructureLevel)
//      must cover the whole
//      curve (no rungs the upgrade verb cannot reach), and a multi-rung curve must
//      have some way up (repo.upgradeCost, or a BuildingTierCatalog /
//      ResourceBuildingProgression ladder). The Crystal Mine failed all three at once
//      and had never paid a single crystal.
//
// Pure data + logic — NO PlayMode, NO GameState — so it runs inside DataRegression.RunAll.
// Mirrors MonetizationCovenantRegression: public static bool Run(out string reason).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village.Buildings.Progression;
// Alias rather than `using DeNelle.Village;` - that namespace declares Entry / ResourceCost /
// UpgradeResult, all of which collide with types in Buildings.Progression above.
using EchoBalanceCatalog = DeNelle.Village.EchoBalanceCatalog;
using EchoBonusCalculator = DeNelle.Village.EchoBonusCalculator;
using HarvestTarget = DeNelle.Village.HarvestTarget;

namespace DeNelle.Editor
{
    public static class BuildingUpgradeRegression
    {
        // WO-855 section 4.8: "cap stacked mults so total production does not exceed
        // ~+50-80% over base from perks alone". 1.80 == the top of that band.
        private const float MaxProductionStack = 1.80f;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            var farm = ResourceBuildingProgression.Find(ResourceBuildingProgression.FarmId);
            var mill = ResourceBuildingProgression.Find(ResourceBuildingProgression.LumbermillId);
            var forge = ResourceBuildingProgression.Find(ResourceBuildingProgression.ForgeId);

            if (farm == null) failures.Add("Farm curve missing (catalog build poisoned/empty fallback)");
            if (mill == null) failures.Add("Lumbermill curve missing (catalog build poisoned/empty fallback)");
            if (forge == null) failures.Add("Forge curve missing (catalog build poisoned/empty fallback)");

            // 1..3 general shape on every building that resolved.
            foreach (var def in new[] { farm, mill, forge })
            {
                if (def == null) continue;
                if (def.Levels == null || def.MaxLevel < 2)
                { failures.Add($"'{def.BuildingId}' has < 2 levels ({def.MaxLevel}) — not an upgrade curve"); continue; }

                int prevYield = int.MinValue;
                float prevInterval = float.MaxValue;
                for (int i = 0; i < def.Levels.Length; i++)
                {
                    var lvl = def.Levels[i];
                    if (lvl == null) { failures.Add($"'{def.BuildingId}' level index {i} is null"); continue; }

                    if (lvl.YieldPerTick <= prevYield)
                        failures.Add($"'{def.BuildingId}' level {lvl.Level} yield {lvl.YieldPerTick} did not ascend (prev {prevYield})");
                    prevYield = lvl.YieldPerTick;

                    // Speed ladder: never SLOWER than the level below (equal allowed at the arcane cap).
                    if (lvl.HarvestInterval > prevInterval + 0.0001f)
                        failures.Add($"'{def.BuildingId}' level {lvl.Level} interval {lvl.HarvestInterval}s is SLOWER than the level below ({prevInterval}s)");
                    prevInterval = lvl.HarvestInterval;

                    bool isMax = lvl.IsMaxLevel;
                    int costLines = lvl.UpgradeCost != null ? lvl.UpgradeCost.Count : 0;
                    if (isMax && (costLines > 0 || lvl.MagicCost > 0))
                        failures.Add($"'{def.BuildingId}' max level {lvl.Level} still carries an upgrade cost (should be terminal)");
                    if (!isMax && costLines == 0 && lvl.MagicCost <= 0)
                        failures.Add($"'{def.BuildingId}' non-max level {lvl.Level} has NO upgrade cost (dead-end mid-curve)");
                }
            }

            // 4. Forge — the DEF-121 Magic gate + arcane tier.
            if (forge != null)
            {
                if (forge.MaxLevel != 6)
                    failures.Add($"Forge should be 5 harvest levels + 1 arcane tier (6), found {forge.MaxLevel}");

                var topHarvest = forge.LevelDef(5);
                if (topHarvest == null || !topHarvest.IsMagicGated || topHarvest.MagicCost <= 0)
                    failures.Add("Forge level 5 (top harvest) is not Magic-gated (DEF-121 Arcane Forge gate missing)");
                else if (topHarvest.UnlocksTechNode != TechTree.ArcaneForgeNodeId)
                    failures.Add($"Forge level 5 unlocks '{topHarvest.UnlocksTechNode ?? "<null>"}', expected '{TechTree.ArcaneForgeNodeId}'");

                var arcane = forge.LevelDef(6);
                if (arcane == null || !arcane.IsMaxLevel)
                    failures.Add("Forge level 6 (arcane) is not the terminal max level");
                else if (arcane.YieldSizeMultiplier <= 1f)
                    failures.Add($"Forge arcane tier size multiplier {arcane.YieldSizeMultiplier} should exceed 1.0 (bigger haul)");
            }

            // 5. Farm/Lumbermill must be flat 5-level curves with NO magic gate.
            foreach (var def in new[] { farm, mill })
            {
                if (def == null) continue;
                if (def.MaxLevel != 5)
                    failures.Add($"'{def.BuildingId}' should be 5 flat levels (no magic tier), found {def.MaxLevel}");
                for (int lv = 1; lv <= def.MaxLevel; lv++)
                {
                    var d = def.LevelDef(lv);
                    if (d != null && d.IsMagicGated)
                        failures.Add($"'{def.BuildingId}' level {lv} is Magic-gated — only the Forge carries the tech axis (DEF-121)");
                }
            }

            // 6/7/8 - WO-855 Phase 5 faucet balance. Each is Guarded so a throw inside one
            // case is reported as a failure rather than taking the whole suite (and therefore
            // DataRegression.RunAll) down.
            Case(failures, "gather-faucet", () => CheckGatherFaucet(farm, mill, forge, failures));
            Case(failures, "perk-stack", () => CheckProductionStacks(failures));
            Case(failures, "echo-scaling", () => CheckEchoScaling(failures));
            Case(failures, "upgrader-reaches-receiver", () => CheckUpgraderReachesReceiver(failures));
            Case(failures, "yield-reachable-at-founding", () => CheckYieldReachableAtFounding(failures));

            if (failures.Count == 0)
            {
                reason = "BUILDING UPGRADE OK — Farm/Lumbermill (5 lvl) + Forge (5 + arcane) curves ascend, " +
                         "speeds monotone, costs terminal at max, only Forge Magic-gated (unlocks arcane_forge); " +
                         "WO-855 faucet bands hold (early/mid/late income, production stacks under +80%, " +
                         "echo 1->6 scaling inside the WO-709 quadratic band); every authored yield curve " +
                         "pays at its founding level and stays inside a reachable upgrade ladder";
                return true;
            }
            reason = $"BUILDING UPGRADE FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        // =====================================================================
        //  Case 6 - [gather-faucet] income per HOUR at representative states.
        // =====================================================================

        /// <summary>Ticks per hour at a level - the cadence ResourceBuildingHarvester actually runs
        /// (it clamps the interval to 0.5s minimum via ResourceBuildingState.CurrentHarvestInterval).</summary>
        private static float TicksPerHour(ResourceLevelDef d) => 3600f / Mathf.Max(0.5f, d.HarvestInterval);

        /// <summary>Resources per HOUR from one collector, replicating the live tick math:
        /// ResourceBuildingState.CurrentEffectiveYield (RoundToInt(YieldPerTick x YieldSizeMultiplier
        /// x ModifierService.ProductionMultFor)) x ticks/hour x the WO-709 xEchoCount multiplier
        /// ResourceBuildingHarvester applies. Talent harvestRate is deliberately excluded (0 on a
        /// fresh hero and it is a separate opt-in axis).</summary>
        private static float PerHour(ResourceBuildingDef def, int level, float prodMult, int echoes)
        {
            var d = def?.LevelDef(level);
            if (d == null) return 0f;
            int perTick = Mathf.Max(0, Mathf.RoundToInt(
                d.YieldPerTick * Mathf.Max(0f, d.YieldSizeMultiplier) * Mathf.Max(0f, prodMult)));
            return perTick * TicksPerHour(d) * Mathf.Max(1, echoes);
        }

        /// <summary>WO-855 section 4 resource basket: wood + 1.5*iron + 1.0*food (+2.0*crystals,
        /// which no collector produces, so it is absent here).</summary>
        private static float Basket(float wood, float iron, float food) => wood + 1.5f * iron + food;

        private static void Band(List<string> failures, string label, float value, float lo, float hi)
        {
            if (value < lo || value > hi)
                failures.Add($"[gather-faucet] {label} = {F(value)}/hr, outside the target band [{F(lo)}..{F(hi)}]");
        }

        private static void CheckGatherFaucet(
            ResourceBuildingDef farm, ResourceBuildingDef mill, ResourceBuildingDef forge, List<string> failures)
        {
            if (farm == null || mill == null || forge == null) return;   // already reported above

            // -- EARLY: everything level 1, the founding Echo only, no tiers/perks bought. --
            float eWood = PerHour(mill, 1, 1f, 1);
            float eIron = PerHour(forge, 1, 1f, 1);
            float eFood = PerHour(farm, 1, 1f, 1);
            Band(failures, "EARLY basket (all collectors L1, x1 echo, no perks)",
                 Basket(eWood, eIron, eFood), 1200f, 5000f);

            // The early-game FLOOR + CEILING, called out on its own because "do not break the
            // early game" is a hard constraint: wood is what almost every first structure costs.
            // 500/hr => a 100-wood first purchase in ~12 min and a 500-wood one in ~60 min;
            // 3000/hr => the grind pass has quietly undone itself.
            Band(failures, "EARLY wood (bootstrap floor - a fresh save must still get moving)",
                 eWood, 500f, 3000f);

            // -- MID: level 3 collectors, 3 Echoes, no production perks assumed (conservative). --
            float mWood = PerHour(mill, 3, 1f, 3);
            float mIron = PerHour(forge, 3, 1f, 3);
            float mFood = PerHour(farm, 3, 1f, 3);
            Band(failures, "MID basket (all collectors L3, x3 echoes, no perks)",
                 Basket(mWood, mIron, mFood), 8000f, 30000f);

            // -- LATE: maxed collectors (forge on its arcane tier), full 6-Echo roster, the
            //    MAXIMUM production stack the tier/perk data actually allows. --
            float woodStack = MaxStackFor("lumbermill", "woodProductionMult");
            float ironStack = MaxStackFor("forge", "resourceEfficiencyMult");
            float lWood = PerHour(mill, mill.MaxLevel, woodStack, 6);
            float lIron = PerHour(forge, forge.MaxLevel, ironStack, 6);
            float lFood = PerHour(farm, farm.MaxLevel, 1f, 6);   // ProductionMultFor("farm") is 1.0 - see report
            Band(failures, "LATE basket (maxed collectors, x6 echoes, full perk stack)",
                 Basket(lWood, lIron, lFood), 50000f, 170000f);

            // THE headline number Phase 0 measured at ~196 wood/sec. Keep it in single digits
            // to low double digits per second or the sink side can never catch up.
            float lateWoodPerSec = lWood / 3600f;
            if (lateWoodPerSec > 20f)
                failures.Add($"[gather-faucet] LATE wood = {F(lateWoodPerSec)}/sec (max {F(20f)}/sec) - " +
                             "the faucet is outrunning every sink in the game again (WO-855 Phase 0 measured 196/sec)");

            // Ordering law: iron is the scarce harvestable, it must never out-produce wood.
            if (lIron > lWood)
                failures.Add($"[gather-faucet] LATE iron {F(lIron)}/hr exceeds LATE wood {F(lWood)}/hr - " +
                             "iron is the scarce harvestable by design");
        }

        // =====================================================================
        //  Case 9 - [upgrader-reaches-receiver] every collector can be buffed.
        //
        //  The design (owner, 2026-08-04) is UPGRADER -> RECEIVER: you level a
        //  building, and its perks raise a DIFFERENT building's output. That link
        //  is a plain string lookup in ModifierService.ProductionMultFor, so it
        //  breaks silently whenever the two ids are spelled differently.
        //
        //  It HAD broken. Wood worked only by coincidence - ladder and collector
        //  are both "lumbermill", so the lookup collided and the perk landed. Food
        //  was authored under "windmill" while the collector is "farm", so every
        //  food perk (up to +45%, and paid for) resolved to the 1.0 default and did
        //  nothing at all. Owner ruling 2026-08-04: the ladder MOVED to "farm" and
        //  "windmill" is retired - the windmill is the Farm's secondary prop, not a
        //  building of its own (VillageSceneBuilder.Content.cs, WO-101).
        //
        //  The law: EVERY id the harvester actually ticks must resolve to a real
        //  multiplier. A collector whose lookup returns the default is orphaned
        //  from its upgrader - the player can buy a perk that cannot reach it.
        //  This fails for any FUTURE collector added without wiring, which is the
        //  whole point of pinning it here rather than just fixing the one case.
        // =====================================================================

        private static void CheckUpgraderReachesReceiver(List<string> failures)
        {
            // Neutral baseline: with no tiers/perks owned every mult is 1.0, so a
            // wired id and an orphaned id both read 1.0 and the test would pass
            // vacuously. Seed a distinctive value per kind and assert it arrives.
            // Active is READ-ONLY (=> _override ?? Compute()); SetOverride is the seam.
            // Capture whether an override was already in force so the restore below
            // cannot INSTALL one that did not exist before this case ran.
            bool hadOverride = DeNelle.Core.State.ModifierService.HasOverride;
            var saved = hadOverride ? DeNelle.Core.State.ModifierService.Active : null;
            try
            {
                DeNelle.Core.State.ModifierService.SetOverride(new DeNelle.Core.State.GameModifiers
                {
                    WoodProductionMult     = 1.31f,
                    FoodProductionMult     = 1.37f,
                    ResourceEfficiencyMult = 1.43f,
                });

                foreach (string id in ResourceBuildingProgression.OrderedIds)
                {
                    float mult = DeNelle.Core.State.ModifierService.ProductionMultFor(id);
                    if (Mathf.Approximately(mult, 1f))
                    {
                        failures.Add(
                            $"[upgrader-reaches-receiver] collector '{id}' resolves to the 1.0 DEFAULT in " +
                            "ModifierService.ProductionMultFor, so no upgrader's perk can ever reach it. " +
                            "The harvester ticks this id every frame; a tier ladder authored for it (or for " +
                            "its upgrader under a different spelling) is resources the player spends for " +
                            "nothing. Add a case mapping this id to the mult its upgrader grants.");
                    }
                }

                // The owner's 2026-08-04 ruling: the food ladder MOVED onto the farm, and the
                // dead "windmill" id must stay dead. If someone re-adds it as an alias, a future
                // ladder authored under the retired id would appear to work while the building
                // it names does not exist - the exact silent failure this ruling removed.
                if (!Mathf.Approximately(
                        DeNelle.Core.State.ModifierService.ProductionMultFor("windmill"), 1f))
                {
                    failures.Add(
                        "[upgrader-reaches-receiver] 'windmill' resolves to a real multiplier again. " +
                        "That id was RETIRED (owner, 2026-08-04): the windmill is the Farm's secondary " +
                        "prop, not a building, so the food ladder lives on 'farm'. Re-aliasing it lets a " +
                        "ladder be authored under a building that does not exist and still look wired.");
                }

                // ...and the ladder must actually be filed under the id the harvester ticks.
                foreach (string path in TierPaths)
                {
                    string full = Path.Combine(Application.dataPath, path);
                    if (!File.Exists(full)) continue;
                    string raw = File.ReadAllText(full);
                    if (raw.Contains("\"id\": \"windmill\""))
                    {
                        failures.Add(
                            $"[upgrader-reaches-receiver] {path} still authors a 'windmill' tier ladder. " +
                            "It was moved to 'farm' - a ladder under the retired id is bought with real " +
                            "resources and reaches nothing.");
                    }
                }
            }
            finally
            {
                if (hadOverride) DeNelle.Core.State.ModifierService.SetOverride(saved);
                else             DeNelle.Core.State.ModifierService.ClearOverride();
            }
        }

        // =====================================================================
        //  Case 10 - [yield-reachable-at-founding] -- the GENERIC guard (WO-856).
        //
        //  Same family as [upgrader-reaches-receiver] above: AUTHORED DATA WITH NO
        //  REACHABLE CONSUMER IS RESOURCES THE PLAYER SPENDS FOR NOTHING.
        //
        //  The Crystal Mine shipped with a payout that only fired at level 3, on a
        //  level nothing could raise, priced at 80 wood + 50 iron. It had never paid
        //  a crystal. Three laws, swept over EVERY catalog row that authors a
        //  per-wave / per-tick yield curve, so the next producer cannot repeat it:
        //
        //    1. The FIRST rung must be > 0. No structure may deliver nothing at its
        //       founding level - the player buys it before any upgrade exists.
        //    2. Clamp(repo.maxLevel, 1, RepoProps.MaxStructureLevel) >= curve.Length. No
        //       structure may author rungs it cannot reach (BuildModeController.MaxLevelFor
        //       clamps to that same named ceiling -- 6 since WO-966, was a hardcoded 3;
        //       a rung above it is decoration).
        //    3. A multi-rung curve needs SOME upgrade path: repo.upgradeCost with
        //       curve.Length - 1 entries, or membership in BuildingTierCatalog /
        //       ResourceBuildingProgression. A ladder with no way up is the same bug
        //       wearing different clothes.
        // =====================================================================

        private static readonly string[] BuildingPaths =
        {
            "Resources/Data/Canonical/buildings.json",
            "StreamingAssets/Data/Canonical/buildings.json",
        };

        private static readonly string[] CatalogPaths =
        {
            "Resources/Data/Canonical/structures-catalog.json",
            "StreamingAssets/Data/Canonical/structures-catalog.json",
        };

        /// <summary>The per-wave / per-tick yield keys a producer row may author. Any row
        /// carrying one of these is a FAUCET and falls under the three laws above.</summary>
        private static readonly string[] YieldCurveKeys =
        {
            "crystalsPerWave", "resourcesPerWave", "yieldPerWave", "yieldPerTick",
        };

        private static void CheckYieldReachableAtFounding(List<string> failures)
        {
            foreach (var rel in BuildingPaths)
            {
                string path = Path.Combine(Application.dataPath, rel);
                string tag = rel.StartsWith("StreamingAssets") ? "StreamingAssets" : "Resources";
                if (!File.Exists(path))
                {
                    failures.Add($"[yield-reachable-at-founding] buildings.json missing at '{rel}' (dual-copy broken)");
                    continue;
                }

                var buildings = JObject.Parse(File.ReadAllText(path))["buildings"] as JArray;
                if (buildings == null)
                {
                    failures.Add($"[yield-reachable-at-founding] [{tag}] buildings.json has no buildings[]");
                    continue;
                }

                foreach (var b in buildings)
                {
                    var row = b as JObject;
                    if (row == null) continue;
                    string id = (string)row["id"] ?? "<no-id>";

                    foreach (var key in YieldCurveKeys)
                    {
                        int[] curve = CurveFrom(row[key]);
                        if (curve == null) continue;   // key absent / not a curve

                        // LAW 1 - a producer must produce at the level it is founded on.
                        if (curve.Length == 0 || curve[0] <= 0)
                        {
                            failures.Add(
                                $"[yield-reachable-at-founding] [{tag}] '{id}' authors {key} with a first rung of " +
                                $"{(curve.Length == 0 ? "nothing" : curve[0].ToString(CultureInfo.InvariantCulture))}. " +
                                "NO STRUCTURE MAY DELIVER NOTHING AT ITS FOUNDING LEVEL - the player pays the build " +
                                "cost before any upgrade exists, so a zero first rung is a purchase that does nothing " +
                                "(WO-856: the Crystal Mine shipped exactly this way and had never paid out).");
                            continue;
                        }

                        // The structures-catalog row that carries the ladder. buildings.json links
                        // to it through "model" (crystal-mine -> mine_crystal); fall back to the id.
                        string catalogId = (string)row["model"];
                        JObject repo = FindRepo(catalogId) ?? FindRepo(id);
                        if (repo == null)
                        {
                            // Not a placeable structure - no ladder to reach, laws 2/3 do not apply.
                            continue;
                        }
                        string shownId = !string.IsNullOrEmpty(catalogId) ? catalogId : id;

                        // LAW 2 - never author rungs the upgrade verb cannot reach.
                        var maxTok = repo["maxLevel"];
                        int ceiling = DeNelle.Core.Catalog.RepoProps.MaxStructureLevel;
                        int maxLevel = Mathf.Clamp(maxTok != null ? (int)maxTok : 1, 1, ceiling);
                        if (maxLevel < curve.Length)
                        {
                            failures.Add(
                                $"[yield-reachable-at-founding] [{tag}] '{id}' authors {curve.Length} {key} rungs but " +
                                $"'{shownId}' reaches level {maxLevel} (repo.maxLevel " +
                                $"{(maxTok == null ? "not authored, defaults to 1" : maxTok.ToString())}, clamped 1..{ceiling} by " +
                                "BuildModeController.MaxLevelFor). Rungs above the ceiling are yields no player can " +
                                "ever collect - and a maxLevel of 1 makes the upgrade verb answer 'Max tier reached.' " +
                                "on a freshly-built structure.");
                        }

                        // LAW 3 - a multi-rung curve must have SOME way up.
                        if (curve.Length <= 1) continue;
                        var costs = repo["upgradeCost"] as JArray;
                        bool hasCostTable = costs != null && costs.Count >= curve.Length - 1;
                        bool hasLadder = DeNelle.Core.State.BuildingTierCatalog.IsUpgradable(id)
                                      || ResourceBuildingProgression.IsResourceBuilding(id);
                        if (!hasCostTable && !hasLadder)
                        {
                            failures.Add(
                                $"[yield-reachable-at-founding] [{tag}] '{id}' authors a {curve.Length}-rung {key} " +
                                $"curve but '{shownId}' has no upgrade path to climb it: repo.upgradeCost carries " +
                                $"{(costs == null ? 0 : costs.Count)} of the {curve.Length - 1} steps needed, and the id " +
                                "is in neither BuildingTierCatalog nor ResourceBuildingProgression. Authored data with " +
                                "no reachable consumer is resources the player spends for nothing.");
                        }
                    }
                }
            }
        }

        /// <summary>An authored yield curve: an ARRAY verbatim, or a bare SCALAR read-migrated
        /// to a one-rung flat curve (mirrors CrystalMine.ParseCurve). Null when the token is
        /// absent or is neither shape.</summary>
        private static int[] CurveFrom(JToken token)
        {
            if (token == null) return null;
            if (token is JArray rungs)
            {
                var curve = new int[rungs.Count];
                for (int i = 0; i < rungs.Count; i++) curve[i] = (int)rungs[i];
                return curve;
            }
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                return new[] { (int)token };
            return null;
        }

        /// <summary>The repo block of a structures-catalog row, from the first copy that has it.</summary>
        private static JObject FindRepo(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId)) return null;
            foreach (var rel in CatalogPaths)
            {
                string path = Path.Combine(Application.dataPath, rel);
                if (!File.Exists(path)) continue;
                var entries = JObject.Parse(File.ReadAllText(path))["entries"] as JArray;
                if (entries == null) continue;
                foreach (var e in entries)
                    if ((string)e["id"] == catalogId) return e["repo"] as JObject;
            }
            return null;
        }

        // =====================================================================
        //  Case 7 - [perk-stack] compounded production multipliers vs the cap.
        // =====================================================================

        // ModifierService.ProductionMultFor maps lumbermill->wood, windmill->food,
        // forge->efficiency. These are the only keys that scale a resource faucet.
        private static readonly string[] ProductionKeys =
            { "woodProductionMult", "foodProductionMult", "resourceEfficiencyMult" };

        private static readonly string[] TierPaths =
        {
            "Resources/Data/Canonical/building-tiers.json",
            "StreamingAssets/Data/Canonical/building-tiers.json",
        };

        /// <summary>The worst-case compounded multiplier for one building/key: the highest value any
        /// tier grants (tier ladders are cumulative-absolute, so this is the top tier) times EVERY
        /// perk that grants the same key - exactly how ModifierService.Compute + Apply aggregate.
        /// Returns 1.0 when the file/building/key is absent (never a phantom multiplier).</summary>
        private static float MaxStackFor(string buildingId, string key)
        {
            foreach (var rel in TierPaths)
            {
                string path = Path.Combine(Application.dataPath, rel);
                if (!File.Exists(path)) continue;
                var root = JObject.Parse(File.ReadAllText(path));
                var buildings = root["buildings"] as JArray;
                if (buildings == null) continue;
                foreach (var b in buildings)
                {
                    if ((string)b["id"] != buildingId) continue;
                    return StackOf(b["tiers"] as JArray, key, out _, out _);
                }
            }
            return 1f;
        }

        private static float StackOf(JArray tiers, string key, out float maxTier, out float perkProduct)
        {
            maxTier = 1f;
            perkProduct = 1f;
            if (tiers == null) return 1f;
            foreach (var t in tiers)
            {
                var mods = t["modifiers"] as JObject;
                var tok = mods != null ? mods[key] : null;
                if (tok != null)
                {
                    float v = (float)tok;
                    if (v > maxTier) maxTier = v;
                }

                var perks = t["perks"] as JArray;
                if (perks == null) continue;
                foreach (var p in perks)
                {
                    var pm = p["modifiers"] as JObject;
                    var pt = pm != null ? pm[key] : null;
                    if (pt == null) continue;
                    float pv = (float)pt;
                    if (pv > 0f) perkProduct *= pv;
                }
            }
            return maxTier * perkProduct;
        }

        private static void CheckProductionStacks(List<string> failures)
        {
            foreach (var rel in TierPaths)
            {
                string path = Path.Combine(Application.dataPath, rel);
                string tag = rel.StartsWith("StreamingAssets") ? "StreamingAssets" : "Resources";
                if (!File.Exists(path))
                {
                    failures.Add($"[perk-stack] building-tiers.json missing at '{rel}' (dual-copy broken)");
                    continue;
                }

                var root = JObject.Parse(File.ReadAllText(path));
                var buildings = root["buildings"] as JArray;
                if (buildings == null)
                {
                    failures.Add($"[perk-stack] [{tag}] building-tiers.json has no buildings[]");
                    continue;
                }

                foreach (var b in buildings)
                {
                    string id = (string)b["id"] ?? "<no-id>";
                    var tiers = b["tiers"] as JArray;
                    if (tiers == null) continue;

                    foreach (var key in ProductionKeys)
                    {
                        float stack = StackOf(tiers, key, out float maxTier, out float perkProduct);
                        if (maxTier <= 1f && perkProduct <= 1f) continue;   // key not authored here
                        if (stack > MaxProductionStack + 0.0001f)
                            failures.Add(
                                $"[perk-stack] [{tag}] '{id}' {key} compounds to x{F(stack)} " +
                                $"(top tier x{F(maxTier)} * perks x{F(perkProduct)}) - over the WO-855 " +
                                $"section 4.8 cap of x{F(MaxProductionStack)} (+80%)");
                    }
                }
            }
        }

        // =====================================================================
        //  Case 8 - [echo-scaling] the WO-709 quadratic band.
        // =====================================================================

        private static void CheckEchoScaling(List<string> failures)
        {
            // Owner re-balance 2026-08-21: predictable linear worker cadence replaces
            // the old quadratic faucet. Common materials pay 5/5s, Gold is slower,
            // and only final-Echo crystals pay exactly 1/15m at every level.
            if (Mathf.Abs(EchoBonusCalculator.HarvestRatePerHour(HarvestTarget.Wood, 1) - 3600f) > 0.01f ||
                Mathf.Abs(EchoBonusCalculator.HarvestRatePerHour(HarvestTarget.Iron, 1) - 3600f) > 0.01f ||
                Mathf.Abs(EchoBonusCalculator.HarvestRatePerHour(HarvestTarget.Food, 1) - 3600f) > 0.01f)
                failures.Add("[echo-scaling] Wood/Iron/Food must each produce 3600/hour (5 every 5 seconds) at level 1");
            if (Mathf.Abs(EchoBonusCalculator.HarvestRatePerHour(HarvestTarget.Gold, 1) - 900f) > 0.01f)
                failures.Add("[echo-scaling] Gold must remain slower than common materials (900/hour)");
            if (Mathf.Abs(EchoBonusCalculator.HarvestRatePerHour(HarvestTarget.Crystals, 1) - 4f) > 0.01f ||
                Mathf.Abs(EchoBonusCalculator.HarvestRatePerHour(HarvestTarget.Crystals, EchoBalanceCatalog.MaxLevel) - 4f) > 0.01f)
                failures.Add("[echo-scaling] Crystals must stay fixed at 4/hour (1 every 15 minutes), unaffected by level");
            return;

#pragma warning disable CS0162
            float baseC = EchoBalanceCatalog.BaseContributionPerEcho;
            float match = EchoBalanceCatalog.PreferredLaneMatchBonus;
            float perLv = EchoBalanceCatalog.PerLevelBonus;
            float six = EchoBalanceCatalog.SixSetBonusGlobalHarvest;
            float tri = EchoBalanceCatalog.HiddenTriSynergyBonus;
            int maxLevel = Mathf.Max(1, EchoBalanceCatalog.MaxLevel);

            float pairSum = 0f;
            var pairs = EchoBalanceCatalog.CrossBonuses;
            if (pairs != null)
                foreach (var p in pairs)
                    if (p != null) pairSum += Mathf.Max(0f, p.Bonus);

            // EchoService.RatePerSecond = EchoCount x (BaseRatePerHour/3600)
            //                             x EchoBonusCalculator.AggregateHarvestMultiplier()
            //   where AggregateHarvestMultiplier = count x (1 + specSum).
            // BaseRatePerHour cancels in every ratio below, so this needs no MonoBehaviour.
            float Total(int n, int echoLevel, bool synergiesRunning)
            {
                float spec = n * (baseC + match + perLv * Mathf.Max(0, echoLevel - 1));
                if (synergiesRunning) spec += pairSum + six + tri;
                return n * n * (1f + spec);
            }

            float solo = Total(1, 1, false);
            float roster = Total(6, 1, true);
            float rosterMaxed = Total(6, maxLevel, true);

            // (a) 1 -> 6 Echoes. The x36 floor is the WO-709 count spine itself; the rest is the
            //     specialization/synergy block. Band chosen around the measured x70 so a
            //     linearisation (~x12) AND a synergy-knob inflation both fail.
            float rosterRatio = roster / Mathf.Max(0.0001f, solo);
            if (rosterRatio < 40f || rosterRatio > 90f)
                failures.Add(
                    $"[echo-scaling] total echo income scales x{F(rosterRatio)} from 1 to 6 Echoes, outside " +
                    "the band [40..90]. The WO-709 quadratic count spine (x36 of that) is an OWNER RULING " +
                    "reaffirmed by docs/design/ECONOMY_PROGRESSION_THESIS_2026-08-02.md - changing it, in " +
                    "either direction, needs an owner ruling, not a balance pass");

            // (b) The 2-Echo checkpoint WO-709 states verbatim ("2 = x2 each, x4 total").
            float twoRatio = Total(2, 1, false) / Mathf.Max(0.0001f, solo);
            if (twoRatio < 3.5f || twoRatio > 4.6f)
                failures.Add(
                    $"[echo-scaling] 2 Echoes scale x{F(twoRatio)} over 1 (WO-709 specifies ~x4) - " +
                    "the count-quadratic spine has been altered");

            // (c) The thesis's structural fact: OWNING dominates LEVELLING. Levelling all six
            //     from 1 to max must stay a modest top-up, never a second power spike.
            float levelRatio = rosterMaxed / Mathf.Max(0.0001f, roster);
            if (levelRatio > 1.5f)
                failures.Add(
                    $"[echo-scaling] levelling a full roster 1 -> {maxLevel} multiplies income x{F(levelRatio)} " +
                    "(max x1.5) - the power spike must stay RECRUITMENT, not the level curve " +
                    "(ECONOMY_PROGRESSION_THESIS_2026-08-02.md section 2)");

            // (d) Crystals are the only real crystal faucet and the slowest by design (WO-830
            //     Sec.3b). Guard the LANE here too: the two crystal Echoes' combined share of a
            //     dumped silo must stay a meaningful minority - not zero (the crystal-priced towers
            //     must stay reachable: Ballista 70C / Arcane Spire 85C in structures-catalog.json
            //     as of this pass) and not a majority (the WO-830 monetization guard).
            float crystalShare = EchoBalanceCatalog.BaseRateFor("echo-stormcoil-serpent")
                               + EchoBalanceCatalog.BaseRateFor("echo-ember-phoenix");
            float allShare = crystalShare
                           + EchoBalanceCatalog.BaseRateFor("echo-frosthowl")
                           + EchoBalanceCatalog.BaseRateFor("echo-verdant-stag")
                           + EchoBalanceCatalog.BaseRateFor("echo-voidwing-raven")
                           + EchoBalanceCatalog.BaseRateFor("echo-stonewarden-bear");
            float crystalPct = allShare > 0f ? crystalShare / allShare : 0f;
            if (crystalPct < 0.10f || crystalPct > 0.25f)
                failures.Add(
                    $"[echo-scaling] crystals take {F(crystalPct * 100f)}% of a dumped silo at a full " +
                    "all-matched roster, outside [10%..25%] - below 10% the crystal-priced towers " +
                    "and every crystal-costed building tier stop being reachable; above 25% the " +
                    "WO-830 monetization guard (crystals = the slowest faucet) is gone");
            #pragma warning restore CS0162
        }
    }
}
