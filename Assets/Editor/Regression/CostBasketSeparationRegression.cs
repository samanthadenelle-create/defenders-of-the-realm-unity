// =============================================================================
// CostBasketSeparationRegression [cost-basket] -- WO-947: cost baskets separate
// by the structure's NATURE.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only; references DeNelle.Core).
// Contract mirrors the other Run(out reason) oracles:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: COST_BASKET_OK (Debug.Log) / COST_BASKET_FAIL (LogError)
//
// THE RULING (owner, 2026-08-10, verbatim intent -- WO-947 section 1):
//   "The lens... is what are we building? If we are building regular structures,
//    then it makes sense that they only cost... iron and wood. However, if it's
//    magical based or ethereal based, then yes, it's crystal... Let's make a
//    separation. So it doesn't touch all three."
// Operationalized:
//   * REGULAR structures cost wood + iron (+/- food where already used). NEVER crystals.
//   * MAGICAL / ETHEREAL structures are CRYSTALS + IRON -- never wood.
//   * INVARIANT: no row's build cost or upgrade step holds wood AND iron AND crystals.
//
// THE PINS ARE ANSWERED (owner, 2026-08-14); catalog v18 applies the first four and v19 the last:
//   pin 1 "Crystals and Iron"        -> the magical pairing is crystals + iron.
//   pin 2 "yes AoE healing"          -> healing IS magical: tower_healer + healing_caravan
//                                       become crystals + iron (+ their existing food).
//   pin 3 "Crafting (can enbue       -> the jeweler is a CRAFTING shop, therefore REGULAR
//         preciouus sstones future      today. The owner flagged a FUTURE release may let it
//         release)"                     imbue precious stones -- a re-classification THEN.
//   pin 4 "thats a baliista          -> tower_wall_wizard is MECHANICAL, therefore REGULAR.
//         mechanical"                   The DATA reading beat the ID reading.
//   pin 5 "cathedral of magic  -> 'arcane-tower' ("Cathedral of Magic") is MAGICAL. It is the
//         is where all magic      ENGINE of magical progression, not a vendor that deals in
//         upgrades anre and       magic. Applied in catalog v19.
//         can unlock new
//         teirs of spells"
// So MagicalIds holds FOUR magical ids and PendingPins is EMPTY.
//
// WHY THE PENDING-PIN MECHANISM STAYS even at zero entries: when a row's side is
// an open OWNER question, guessing it is exactly the inference-fix CLAUDE.md
// section 12 forbids. Such a row is carried here as a DATED, CITED exemption
// instead of silently passing or silently failing -- and the list SELF-CLEANS,
// because case 3 FAILS if a listed row has stopped violating, so a pin that lands
// and gets applied forces the exemption to be deleted in the same change. It can
// only shrink. It is NOT a mute button for a gate failure.
//
// THE LAST PIN IS SPENT (owner, 2026-08-14): 'arcane-tower' ("Cathedral of Magic")
// was the one row left unclassified. It is MAGICAL -- crystals + iron -- and catalog
// v19 folds its wood:60 1:1 into CRYSTALS (basket total 120 unchanged).
// This OVERRULES the earlier reading that put it on the REGULAR side by the jeweler
// analogy ("a shop that DEALS IN magic is still a shop"), which cited behaviorId
// 'GameplayBuilding' and the row's _heightNote ("despite the id this is not a tower --
// it is the town's one civic landmark"). THE OWNER'S DISTINCTION, recorded here
// because the surface evidence genuinely points both ways and the next reader will
// hit the same fork: the jeweler SELLS things that happen to be precious; the
// Cathedral is WHERE MAGIC UPGRADES LIVE AND WHERE NEW SPELL TIERS UNLOCK -- the
// ENGINE of magical progression, not a vendor. 'behaviorId: GameplayBuilding'
// describes its BEHAVIOUR (it is not a firing tower), NOT its cost class.
// WO-947 is now FULLY APPLIED: no open classification questions remain.
//
// Cases:
//   1 [invariant]   no entry's build cost / upgrade step holds wood AND iron AND
//                   crystals -- except the dated pending-pin ids.
//   2 [regular]     crystals appear in NO entry outside MagicalIds -- except the
//                   dated pending-pin ids. Also FAILS a row whose authored basket
//                   is all-zero, because BuildModeController.CostFor's back-compat
//                   fallback then charges pure `buildCost` CRYSTALS -- a regular
//                   structure priced in crystals through the back door.
//   3 [pins]        every pending-pin id still EXISTS and still VIOLATES (else the
//                   exemption is stale and is hiding the next regression), and the
//                   pins are logged distinguishably for the owner call.
//   4 [applied]     every conversion already made (v17: tower_siege_tower; v18:
//                   tower_wall_wizard, jeweler, tower_arcane_spire, tower_healer,
//                   healing_caravan; v19: arcane-tower) stays on its ruled side -- regular rows carry
//                   zero crystals, magical rows carry zero wood and non-zero
//                   crystals -- and each basket TOTAL is unchanged from its
//                   pre-ruling value, because every fold was 1:1. A revert or a
//                   stealth re-balance both trip here.
//
// DELIBERATELY NOT DUPLICATED HERE: the structures-catalog dual-copy byte-equal
// check (BuildEconomyRegression.CheckDualCopy) and cost-slot sanity / tier
// monotonicity (BuildEconomyRegression checks 3 + 4). This oracle owns ONE thing:
// basket COMPOSITION.
//
// Reads the AUTHORED basket (repo.cost / repo.upgradeCost), NOT
// BuildModeController.CostFor -- CostFor folds in the buildCost-crystals
// back-compat fallback and the tower softcap multiplier, neither of which is a
// statement about what the row is MADE of. Composition is an authoring question.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "cost-basket suite", () => { if (!DeNelle.Editor.Regression.CostBasketSeparationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[cost-basket] " + r); });
//
// Standalone: run-unity-method DeNelle.Editor.Regression.CostBasketSeparationRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;

namespace DeNelle.Editor.Regression
{
    public static class CostBasketSeparationRegression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";

        /// <summary>
        /// The PINNED magical/ethereal set -- rows allowed to carry crystals.
        /// POPULATED 2026-08-14 by the OWNER's answers to WO-947 section 4:
        ///   pin 1 verbatim "Crystals and Iron"  -> the magical basket is crystals + iron, never wood.
        ///   pin 2 verbatim "yes AoE healing"    -> healing IS magical, so both healing rows are here.
        ///   pin 5 verbatim "cathedral of magic is where all magic upgrades anre and can unlock new
        ///                   teirs of spells"    -> 'arcane-tower' is the ENGINE of magical
        ///                   progression, not a vendor that deals in magic. Applied catalog v19.
        /// Adding an id here is an OWNER ruling, never an agent's inference.
        /// </summary>
        private static readonly HashSet<string> MagicalIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tower_arcane_spire",   // element Aether / behaviorId ArcaneTower / projectileStyle "spell"
                "tower_healer",         // owner 2026-08-14 pin 2: "yes AoE healing" -- healing is magical
                "healing_caravan",      // same ruling; moves with tower_healer
                "arcane-tower",         // owner 2026-08-14 pin 5: "cathedral of magic is where all magic
                                        // upgrades anre and can unlock new teirs of spells" -- the ENGINE of
                                        // magical progression. behaviorId 'GameplayBuilding' is its BEHAVIOUR
                                        // (not a firing tower), NOT its cost class. Overrules the jeweler-analogy
                                        // reading; see the header. Applied catalog v19.
            };

        /// <summary>
        /// Rows still on their PRE-RULING basket because their classification is an
        /// OPEN OWNER PIN. **EMPTY as of 2026-08-14** -- the owner answered all four
        /// WO-947 section 4 pins plus the section 6 id-vs-data pin plus the final
        /// 'arcane-tower' pin, and catalog v18 + v19
        /// apply every one of them, so there is nothing left to excuse. The
        /// mechanism stays (case 3 fails if a listed row has stopped violating) so a
        /// FUTURE pin can be carried the same dated, cited way instead of an agent
        /// guessing a side -- the inference-fix CLAUDE.md section 12 forbids.
        /// </summary>
        private static readonly Dictionary<string, string> PendingPins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // (empty -- every WO-947 pin was answered by the owner on 2026-08-14 and applied in
                //  structures-catalog.json v18 + v19. Do NOT re-add a row here to dodge a gate failure;
                //  an entry here is an OWNER question on record, not a mute button.)
            };

        /// <summary>
        /// Every row WO-947 has actually been converted (catalog v17 + v18 + v19). Each records the
        /// SIDE it was ruled onto and the basket TOTALS (build, then each upgrade step) that the
        /// conversion deliberately PRESERVED -- both folds were 1:1, so first-cost feel did not
        /// move. A revert, a dropped fold, or a stealth re-balance all trip case 4.
        /// </summary>
        private sealed class AppliedRow
        {
            public bool Magical;       // true -> crystals + iron, wood must be 0; false -> wood + iron, crystals must be 0
            public int[] Totals;       // build cost, then one per upgrade step
            public string Why;         // the owner's own words + the catalog evidence
        }

        private static readonly Dictionary<string, AppliedRow> AppliedRows =
            new Dictionary<string, AppliedRow>(StringComparer.OrdinalIgnoreCase)
            {
                { "tower_siege_tower", new AppliedRow {
                    Magical = false, Totals = new[] { 270, 324, 675 },
                    Why = "REGULAR on the catalog's own evidence (displayName 'Sky Ballista (Anti-Air)', " +
                          "'Wall-mounted spear thrower', element None, projectileStyle 'bolt', _heightCadence " +
                          "SIEGE ENGINE group). v17, 2026-08-14. Crystals folded 1:1 into IRON." } },
                { "tower_wall_wizard", new AppliedRow {
                    Magical = false, Totals = new[] { 160, 192, 400 },
                    Why = "REGULAR by OWNER ruling 2026-08-14, verbatim: \"thats a baliista mechanical\". The " +
                          "'wizard' in the id is stale naming; the row's data (displayName 'Ballista', element " +
                          "None, projectileStyle 'bolt', owner rename 2026-07-08) is what was ruled on. v18. " +
                          "Crystals (70/84/175) folded 1:1 into IRON." } },
                { "jeweler", new AppliedRow {
                    Magical = false, Totals = new[] { 120 },
                    Why = "REGULAR by OWNER ruling 2026-08-14, verbatim: \"Crafting (can enbue preciouus sstones " +
                          "future release)\" -- it is a crafting shop, it trades in gems rather than being built " +
                          "of magic. The owner flagged a FUTURE release may let it imbue precious stones; that is " +
                          "a re-classification THEN, not now. v18. Crystals (30) folded 1:1 into IRON." } },
                { "tower_arcane_spire", new AppliedRow {
                    Magical = true, Totals = new[] { 165, 198, 412 },
                    Why = "MAGICAL (element 'Aether', behaviorId 'ArcaneTower', projectileStyle 'spell'); the " +
                          "PAIRING came from OWNER pin 1, verbatim: \"Crystals and Iron\". v18. Wood (40/48/100) " +
                          "folded 1:1 into CRYSTALS, the crystal-BASED side of the ruling." } },
                { "tower_healer", new AppliedRow {
                    Magical = true, Totals = new[] { 250 },
                    Why = "MAGICAL by OWNER ruling 2026-08-14, verbatim: \"yes AoE healing\" -- healing IS magical. " +
                          "Basket is crystals + iron + the FOOD this row already used. v18. Wood (110) folded 1:1 " +
                          "into CRYSTALS." } },
                { "healing_caravan", new AppliedRow {
                    Magical = true, Totals = new[] { 350 },
                    Why = "MAGICAL by the same OWNER ruling as tower_healer (\"yes AoE healing\"); the two move " +
                          "together. Basket is crystals + iron + food. v18. Wood (150) folded 1:1 into CRYSTALS." } },
                { "arcane-tower", new AppliedRow {
                    Magical = true, Totals = new[] { 120 },
                    Why = "MAGICAL by OWNER ruling 2026-08-14, verbatim: \"cathedral of magic is where all magic " +
                          "upgrades anre and can unlock new teirs of spells\" -- it is the ENGINE of magical " +
                          "progression, not a vendor that deals in magic (the jeweler-analogy reading, which " +
                          "cited behaviorId 'GameplayBuilding' and the row's civic-landmark _heightNote, is " +
                          "OVERRULED: behaviorId describes BEHAVIOUR, not cost class). v19. Wood (60) folded " +
                          "1:1 into CRYSTALS, so the basket is crystals 60 + iron 60, total 120 unchanged. " +
                          "No upgrade ladder on this row (singleton, maxLevel unset), so ONE basket." } },
            };

        [Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("COST_BASKET_OK - " + reason);
            else Debug.LogError("COST_BASKET_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== CostBasketSeparationRegression [cost-basket] (WO-947: regular = wood+iron, magical = crystal-based) ===");

            try
            {
                var entries = ParseCatalog(failures, log);
                if (entries != null)
                {
                    CaseInvariantAndRegular(entries, failures, log);
                    CasePendingPinsStillStand(entries, failures, log);
                    CaseAppliedRowStaysConverted(entries, failures, log);
                }
            }
            catch (Exception ex)
            {
                failures.Add("[cost-basket] CostBasketSeparationRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "COST BASKET OK - no structure row mixes wood + iron + crystals and no row outside the " +
                         "pinned magical set (" + MagicalIds.Count + " row(s)) carries crystals; " +
                         PendingPins.Count + " dated WO-947 pending-pin row(s) remain (0 = every owner pin " +
                         "answered and applied); all " + AppliedRows.Count + " converted row(s) stay on their " +
                         "ruled side with their basket totals intact.";
                Debug.Log("COST_BASKET_OK\n" + log);
                return true;
            }
            reason = "cost-basket: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("COST_BASKET_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  Parse -- the SAME settings CatalogBootstrap.LoadFromJson uses.
        // =====================================================================
        private static List<CatalogEntry> ParseCatalog(List<string> failures, StringBuilder log)
        {
            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[cost-basket] " + CatalogRelPath + " unreadable (CanonicalJson.Read returned empty) - " +
                             "no basket is verifiable at all");
                return null;
            }
            StructuresFile file;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
            }
            catch (Exception ex)
            {
                failures.Add("[cost-basket] structures-catalog.json failed to parse: " + ex.Message);
                return null;
            }
            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[cost-basket] structures-catalog.json deserialized to 0 entries");
                return null;
            }
            log.AppendLine("  structures-catalog.json v" + file.Version + " -> " + file.Entries.Count + " row(s)");
            return file.Entries;
        }

        // =====================================================================
        //  CASES 1 + 2 -- the invariant, and crystals-only-where-pinned.
        // =====================================================================
        private static void CaseInvariantAndRegular(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            int clean = 0;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.repo == null) continue;
                bool pending = PendingPins.ContainsKey(e.id);
                bool magical = MagicalIds.Contains(e.id);
                bool dirty = false;

                // Every authored basket on the row: the build cost + each upgrade step.
                // ResourceCost is a STRUCT (Core/Catalog/RepoProps.cs) - never null; the
                // array itself is the only nullable part.
                var baskets = new List<KeyValuePair<string, ResourceCost>>
                {
                    new KeyValuePair<string, ResourceCost>("build cost", e.repo.cost),
                };
                if (e.repo.upgradeCost != null)
                    for (int i = 0; i < e.repo.upgradeCost.Length; i++)
                        baskets.Add(new KeyValuePair<string, ResourceCost>("upgrade L" + (i + 1) + "->L" + (i + 2),
                                                                           e.repo.upgradeCost[i]));

                foreach (var b in baskets)
                {
                    var c = b.Value;
                    string where = "'" + e.id + "' " + b.Key + " (w" + c.wood + " f" + c.food + " i" + c.iron + " c" + c.crystals + ")";

                    // -- CASE 1 [invariant] -- never all three.
                    if (c.wood > 0 && c.iron > 0 && c.crystals > 0)
                    {
                        dirty = true;
                        if (!pending)
                            failures.Add("[invariant] " + where + " holds wood AND iron AND crystals - the WO-947 " +
                                         "owner ruling (2026-08-10) forbids a basket that touches all three. Pick a " +
                                         "side: regular -> wood + iron, magical/ethereal -> crystal-based.");
                    }

                    // -- CASE 2 [regular] -- crystals only on the pinned magical set.
                    if (c.crystals > 0 && !magical)
                    {
                        dirty = true;
                        if (!pending)
                            failures.Add("[regular] " + where + " charges crystals but '" + e.id + "' is not on the " +
                                         "pinned MAGICAL set - WO-947: regular structures cost wood + iron (+/- food) " +
                                         "and NEVER crystals. If this row IS magical/ethereal it needs an OWNER pin " +
                                         "(WO-947 section 4), not a code-side classification.");
                    }
                }

                // -- CASE 2b -- the back-door crystal charge. An all-zero authored basket
                // makes BuildModeController.CostFor fall through to charging `buildCost`
                // in CRYSTALS, which prices a regular structure in crystals invisibly.
                bool authoredZero = e.repo.cost.IsZero;
                bool affordGated = e.repo.placement == null || e.repo.placement.checkAffordable;
                if (authoredZero && affordGated && e.repo.buildCost > 0 && !magical)
                {
                    dirty = true;
                    if (!pending)
                        failures.Add("[regular] '" + e.id + "' authors NO repo.cost (all-zero) but buildCost " +
                                     e.repo.buildCost + " - CostFor's back-compat fallback charges that in pure " +
                                     "CRYSTALS, so this regular structure is crystal-priced through the back door " +
                                     "(WO-947). Author a wood + iron basket.");
                }

                if (!dirty && !pending) clean++;
            }
            log.AppendLine("  [invariant]+[regular] " + clean + " row(s) conform outright; " +
                           PendingPins.Count + " carried as dated WO-947 pending pins; " +
                           MagicalIds.Count + " row(s) on the pinned MAGICAL set");
        }

        // =====================================================================
        //  CASE 3 [pins] -- the exemption list may only shrink, and every pin is
        //  surfaced distinguishably (no silent failures, CLAUDE.md section 12).
        // =====================================================================
        private static void CasePendingPinsStillStand(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            if (PendingPins.Count == 0)
            {
                log.AppendLine("  [pins] NONE OPEN - every WO-947 classification pin was answered by the owner on " +
                               "2026-08-14 and applied in structures-catalog.json v18 + v19. The exemption list is empty, " +
                               "so no row is excused from the ruling.");
                return;
            }

            var byId = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.id) && !byId.ContainsKey(e.id)) byId[e.id] = e;

            foreach (var pin in PendingPins)
            {
                CatalogEntry e;
                if (!byId.TryGetValue(pin.Key, out e) || e == null || e.repo == null)
                {
                    failures.Add("[pins] WO-947 pending-pin id '" + pin.Key + "' is no longer a catalog row - the " +
                                 "exemption is stale and would silently excuse whatever takes its place. Delete it " +
                                 "from PendingPins (or restore the row).");
                    continue;
                }

                bool stillViolates = Violates(e.repo.cost);
                if (e.repo.upgradeCost != null)
                    foreach (var step in e.repo.upgradeCost)
                        if (Violates(step)) stillViolates = true;

                if (!stillViolates)
                {
                    failures.Add("[pins] WO-947 pending-pin id '" + pin.Key + "' NO LONGER violates the cost-basket " +
                                 "ruling - its pin has evidently been answered and applied. REMOVE it from " +
                                 "PendingPins in the same change so the exemption cannot hide the next regression " +
                                 "(and add it to MagicalIds if the owner pinned it magical).");
                }
                else
                {
                    log.AppendLine("  [pins] OPEN - '" + pin.Key + "' still on its pre-ruling basket. " + pin.Value);
                }
            }
        }

        /// <summary>Does this basket breach the ruling for a row with no owner pin?</summary>
        private static bool Violates(ResourceCost c)
        {
            return c.crystals > 0;   // crystals outside the pinned magical set, incl. the all-three case
        }

        // =====================================================================
        //  CASE 4 [applied] -- the one conversion WO-947 has actually made.
        // =====================================================================
        private static void CaseAppliedRowStaysConverted(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            var byId = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.id) && !byId.ContainsKey(e.id)) byId[e.id] = e;

            foreach (var kv in AppliedRows)
            {
                string id = kv.Key;
                AppliedRow spec = kv.Value;

                CatalogEntry row;
                if (!byId.TryGetValue(id, out row) || row == null || row.repo == null)
                {
                    failures.Add("[applied] '" + id + "' is missing from the catalog - a row WO-947 has been " +
                                 "applied to cannot be verified. " + spec.Why);
                    continue;
                }

                var baskets = new List<ResourceCost> { row.repo.cost };
                if (row.repo.upgradeCost != null)
                    foreach (var step in row.repo.upgradeCost) baskets.Add(step);

                if (baskets.Count != spec.Totals.Length)
                {
                    failures.Add("[applied] '" + id + "' authors " + baskets.Count + " basket(s), expected " +
                                 spec.Totals.Length + " (build + upgrade steps) - the WO-947 conversion was " +
                                 "authored against that ladder shape. " + spec.Why);
                    continue;
                }

                for (int i = 0; i < baskets.Count; i++)
                {
                    var c = baskets[i];
                    string which = i == 0 ? "build cost" : "upgrade step " + i;

                    if (spec.Magical)
                    {
                        // Magical basket = CRYSTALS + IRON (owner pin 1, 2026-08-14). Wood is out.
                        if (c.wood != 0)
                            failures.Add("[applied] '" + id + "' " + which + " charges " + c.wood + " wood - WO-947 " +
                                         "converted this row to MAGICAL (crystals + iron, NEVER wood). It has been " +
                                         "reverted or re-authored. " + spec.Why);
                        if (c.crystals <= 0)
                            failures.Add("[applied] '" + id + "' " + which + " charges NO crystals - a magical row " +
                                         "under WO-947 is crystal-BASED. " + spec.Why);
                    }
                    else
                    {
                        // Regular basket = WOOD + IRON (+/- food). Crystals are out.
                        if (c.crystals != 0)
                            failures.Add("[applied] '" + id + "' " + which + " charges " + c.crystals + " crystals - " +
                                         "WO-947 converted this row to REGULAR (wood + iron, NEVER crystals). It has " +
                                         "been reverted. " + spec.Why);
                    }

                    int total = c.wood + c.food + c.iron + c.crystals;
                    if (total != spec.Totals[i])
                        failures.Add("[applied] '" + id + "' " + which + " totals " + total + ", expected " +
                                     spec.Totals[i] + " - every WO-947 conversion folded the dropped resource 1:1 " +
                                     "into the surviving side specifically so first-cost FEEL was unchanged. A " +
                                     "different total is a balance change riding on a composition ruling; if it is " +
                                     "intended, update AppliedRows with the owner's new numbers. " + spec.Why);
                }

                log.AppendLine("  [applied] '" + id + "' converted " + (spec.Magical ? "MAGICAL (crystals+iron)" : "REGULAR (wood+iron)") +
                               ", totals " + string.Join("/", Array.ConvertAll(spec.Totals, t => t.ToString())) + " preserved");
            }
        }
    }
}
