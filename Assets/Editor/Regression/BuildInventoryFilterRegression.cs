// =============================================================================
// BuildInventoryFilterRegression — WO-2005. Guards the BUILD inventory's SHAPE,
// never its size.
// -----------------------------------------------------------------------------
// Owner ruling 20 / Manage redesign canon §3: "Do not lock acceptance tests to a
// guessed total. The model must expose the authoritative live list."
//
// ⛔ SO THERE IS NO EXPECTED COUNT IN THIS FILE. Not 28, not 26, not 23. Every case
// below is an INVARIANT that stays true as the catalog grows: every offered row
// carries a filter, every filter token is legal, no two rows claim one art tile,
// the three storage containers are singleton. Pinning a number would make adding a
// building a two-file edit and would go stale exactly like CLAUDE.md §2's WO block
// and §7's MaxVisibleFaces line — the very defects this program exists to stop
// repeating.
//
// Reads the canonical JSON DIRECTLY (both copies) rather than CatalogRegistry: this
// runs in batchmode where no scene has booted, and reading the file is also what
// lets case 5 prove the two copies agree.
//
// ⚠ REVERT RECIPE (if this suite blocks a lane and must come out in a hurry):
//   1. delete Assets/Editor/Regression/BuildInventoryFilterRegression.cs (+ .meta)
//   2. delete the single registration line in Assets/Editor/Regression/DataRegression.cs
//      (search "build-inventory-filters")
//   Nothing else references it. It is pure verification: no runtime code path, no
//   data, no asset. Removing it loses the guard and changes no behaviour.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using DeNelle.Core.Catalog;

namespace DeNelle.Editor.Regression
{
    public static class BuildInventoryFilterRegression
    {
        private const string CatalogResources = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string CatalogStreaming = "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";
        private const string CollectionsPath  = "Assets/Resources/Data/Canonical/card-collections.json";

        /// <summary>
        /// The three storage containers, by the role each one CLAIMS — never by id.
        /// Owner ruling 23 (2026-09-06): "cap only one of each storage type, the idea is they
        /// should level them." Capacity has ONE axis of growth and it is LEVEL.
        /// </summary>
        /// ⚠ "stone_store" IS A LITERAL HERE ON PURPOSE, AND IT IS A FINDING. The named constant
        /// <c>StructureRole.FoodStore</c> is <c>"food_store"</c> - a role **no catalog row claims**
        /// (measured 2026-09-06: the silo authors <c>"role": "stone_store"</c>). That is the exact
        /// trap WO-1416 fixed on the producer axis, where <c>StructureRole.FoodProducer</c> named a
        /// role nothing claimed and every call site resolved to null in silence. Using the constant
        /// here would have made this case match ZERO rows and pass green while guarding nothing.
        /// The constant should be retired to <c>StoneStore = "stone_store"</c> the way FoodProducer
        /// was - handed back to the CLI seat, not done inside this lane.
        private static readonly string[] StorageRoles =
        {
            StructureRole.WoodStore, StructureRole.IronStore, "stone_store",
        };

        /// <summary>
        /// DATED, CITED, SELF-CLEANING exemption list — the same mechanism
        /// CostBasketSeparationRegression uses for its owner pins, and for the same reason:
        /// guessing a row's side is the inference-fix CLAUDE.md §12 forbids.
        ///
        /// These ids are advertised in a card collection, are HIDDEN, and NOTHING in the
        /// shipped game names the step that would reveal them. Measured 2026-09-06 and recorded
        /// in docs/PREREQUISITE_REGISTRY_2026-09-06.md §2.1:
        ///   gate_stone          hard-coded hide that precedes every unlock check
        ///                       (BuildCollectionBrowser.HiddenUntilFinishedArtId, applied :357).
        ///                       Its unlock DOES flip and never matters.
        ///   jeweler             Town lockedIds; no writer for its unlock. Strands all 6
        ///                       jeweler-recipes.json recipes with it.
        ///   tower_catapult      Defense lockedIds; no writer.
        ///   tower_siege_tower   Defense lockedIds; no writer.
        ///   tower_arcane_spire  DIFFERENT, and listed for honesty: a writer DOES exist
        ///                       (CastleDefensePlansPickup at WavesCompleted >= 3), but nothing
        ///                       names the step to the player, so it is indistinguishable from
        ///                       the four above from inside the game.
        ///
        /// Case 6 FAILS when this list stops matching what the data says, in EITHER direction —
        /// so an id that becomes reachable forces the exemption out, and a NEW dead tile cannot
        /// slip in unnoticed. **Never add an id here to make the suite green.** An id earns a
        /// line only with the measurement that put it there.
        /// </summary>
        private static readonly HashSet<string> UnnamedLockIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "gate_stone", "jeweler", "tower_catapult", "tower_siege_tower", "tower_arcane_spire",
            };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            JObject catalog = null;

            try
            {
                catalog = JObject.Parse(File.ReadAllText(CatalogResources));
            }
            catch (Exception ex)
            {
                reason = "[build-inventory-filters] could not read/parse " + CatalogResources + ": " + ex.Message;
                return false;
            }

            var entries = catalog["entries"] as JArray;
            if (entries == null || entries.Count == 0)
            {
                reason = "[build-inventory-filters] structures-catalog.json holds no entries[]";
                return false;
            }

            var offered = OfferedIds(failures);
            var artKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var filtered = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in BuildFilter.Membership) counts[f] = 0;

            foreach (var e in entries)
            {
                string id = (string)e["id"] ?? "?";
                var mf = e["manageFilters"] as JArray;
                var tokens = mf?.Select(t => (string)t).Where(t => !string.IsNullOrEmpty(t)).ToList()
                             ?? new List<string>();

                // 1 [legal-token] every authored token is one of the five memberships, and ALL is
                //   never authored (it is the unfiltered list, not a membership).
                foreach (var t in tokens)
                {
                    if (string.Equals(t, BuildFilter.All, StringComparison.OrdinalIgnoreCase))
                        failures.Add("[build-inventory-filters] '" + id + "' authors \"ALL\". ALL is the " +
                                     "UNFILTERED list and must never be a membership - authoring it means the " +
                                     "day a row is added and the token is forgotten, ALL silently stops " +
                                     "meaning all. Remove it; BuildFilter.Matches already answers ALL.");
                    else if (!BuildFilter.IsLegalMembership(t))
                        failures.Add("[build-inventory-filters] '" + id + "' authors manageFilters token \"" + t +
                                     "\", which is not one of " + string.Join("/", BuildFilter.Membership) +
                                     ". The six chips are an owner ruling (OWNER_RULINGS_LOCKED.md #5); a typo " +
                                     "here hides a building behind a chip that does not exist.");
                    else counts[t]++;
                }
                if (tokens.Count > 0) filtered.Add(id);

                // 2 [offered-has-filter] every row the browser OFFERS belongs to at least one
                //   non-ALL filter (canon §3). This is the whole point of the filter pass.
                if (offered.Contains(id) && tokens.Count == 0)
                    failures.Add("[build-inventory-filters] '" + id + "' is offered by a card collection but " +
                                 "authors NO manageFilters. Manage redesign canon section 3: every structure must " +
                                 "belong to at least one filter besides ALL, or it is reachable only through " +
                                 "ALL - a hidden \"Other\" bucket, which the numeric acceptance criteria forbid.");

                // 3 [art-key-unique] two rows may not claim one art tile - the tile would render
                //   for whichever row drew last and the other would look like a duplicate.
                string art = (string)e["manageArtKey"];
                if (!string.IsNullOrEmpty(art))
                {
                    if (artKeys.TryGetValue(art, out string first))
                        failures.Add("[build-inventory-filters] art tile \"" + art + "\" is claimed by BOTH '" +
                                     first + "' and '" + id + "'. The art-name-to-id join is one-to-one; two " +
                                     "claimants means one building is wearing another's portrait. Resolve in " +
                                     "structures-catalog.json, never in a name-parsing resolver.");
                    else artKeys[art] = id;
                }

                // 4 [storage-singleton] owner ruling 23 - one of each storage type.
                string role = (string)e["role"];
                if (!string.IsNullOrEmpty(role) && Array.IndexOf(StorageRoles, role) >= 0)
                {
                    bool singleton = (bool?)e["repo"]?["singleton"] ?? false;
                    if (!singleton)
                        failures.Add("[build-inventory-filters] '" + id + "' claims storage role \"" + role +
                                     "\" but repo.singleton is not true. OWNER RULING 23 (2026-09-06): capacity " +
                                     "grows by LEVEL, never by COUNT. Without the flag a second container is " +
                                     "cheaper than the next rung and the whole ladder is pointless.");
                    if (!tokens.Any(t => string.Equals(t, BuildFilter.Storage, StringComparison.OrdinalIgnoreCase)))
                        failures.Add("[build-inventory-filters] container '" + id + "' is not in the STORAGE " +
                                     "filter (authors: " + string.Join(",", tokens) + ").");
                }
            }

            // 5 [copy-parity] the two canonical copies must stay byte-identical - a parity oracle
            //   exists precisely because a text-mode rewrite has flattened one of them before.
            try
            {
                byte[] a = File.ReadAllBytes(CatalogResources);
                byte[] b = File.ReadAllBytes(CatalogStreaming);
                if (a.Length != b.Length || !a.SequenceEqual(b))
                    failures.Add("[build-inventory-filters] the two canonical copies of structures-catalog.json " +
                                 "are NOT byte-identical (" + a.Length + " vs " + b.Length + " bytes). Patch from " +
                                 "one buffer and write both; never edit one copy by hand.");
            }
            catch (Exception ex)
            {
                failures.Add("[build-inventory-filters] could not compare the canonical copies: " + ex.Message);
            }

            // 6 [unnamed-locks] the dated exemption list still matches reality, in BOTH directions.
            var derivedUnnamed = DerivedUnnamedLocks(entries, offered);
            foreach (string id in UnnamedLockIds)
                if (!derivedUnnamed.Contains(id))
                    failures.Add("[build-inventory-filters] '" + id + "' is still listed in UnnamedLockIds but the " +
                                 "data no longer puts it there - either it became reachable (good: delete the line " +
                                 "and the registry note with it) or it left its collection. A stale exemption hides " +
                                 "the next regression.");
            foreach (string id in derivedUnnamed)
                if (!UnnamedLockIds.Contains(id))
                    failures.Add("[build-inventory-filters] NEW DEAD TILE: '" + id + "' is advertised in a card " +
                                 "collection, is hidden by build-categories lockedIds/visibleLockedIds, and nothing " +
                                 "names the step that reveals it. An item the player can see and no action can " +
                                 "unlock is the defect class WO-2005 exists to kill. Either wire an unlock writer, " +
                                 "pull it from the collection, or add it here WITH the measurement that proves it.");

            if (failures.Count == 0)
                reason = "[build-inventory-filters] " + filtered.Count + " catalog row(s) carry a filter across " +
                         string.Join(" / ", BuildFilter.Membership.Select(f => f + "=" + counts[f])) +
                         "; " + offered.Count + " offered row(s) all filtered; " + artKeys.Count +
                         " art tile(s) uniquely claimed; the three storage containers are singleton; both " +
                         "canonical copies byte-identical; " + UnnamedLockIds.Count + " dated unnamed-lock " +
                         "exemption(s) still accurate. (Counts REPORTED, never asserted - canon section 3.)";
            else
                reason = string.Join("\n", failures);
            return failures.Count == 0;
        }

        /// <summary>Every id any card collection points at. Read from data, never listed here.</summary>
        private static HashSet<string> OfferedIds(List<string> failures)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var doc = JObject.Parse(File.ReadAllText(CollectionsPath));
                if (doc["collections"] is JArray cols)
                    foreach (var c in cols)
                        if (c["items"] is JArray items)
                            foreach (var i in items)
                            {
                                string id = (string)i["itemId"];
                                if (!string.IsNullOrEmpty(id)) set.Add(id);
                            }
            }
            catch (Exception ex)
            {
                failures.Add("[build-inventory-filters] could not read " + CollectionsPath + ": " + ex.Message);
            }
            return set;
        }

        /// <summary>
        /// Ids that are OFFERED by a collection, gated by build-categories (lockedIds or
        /// visibleLockedIds), and for which <c>RewardedProgression.LockReasonFor</c> supplies no
        /// player-facing step. Derived every run so the exemption list cannot drift from the data.
        /// </summary>
        private static HashSet<string> DerivedUnnamedLocks(JArray entries, HashSet<string> offered)
        {
            var gated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var cats = JObject.Parse(File.ReadAllText("Assets/Resources/Data/Canonical/build-categories.json"));
                if (cats["categories"] is JArray list)
                    foreach (var c in list)
                    {
                        if (c["lockedIds"] is JArray locked)
                            foreach (var id in locked) { var s = (string)id; if (!string.IsNullOrEmpty(s)) gated.Add(s); }
                        if (c["visibleLockedIds"] is JObject vis)
                            foreach (var p in vis.Properties()) gated.Add(p.Name);
                    }
            }
            catch (Exception ex)
            {
                // A read failure here would silently EMPTY the derived set and turn case 6 into a
                // pile of misleading "stale exemption" failures, so it goes straight into the
                // RESULT (not into `gated`, where the offered-only filter below would drop it) and
                // surfaces as one honest, unmatchable id. Never degrade quietly.
                result.Add("<build-categories.json unreadable: " + ex.Message + ">");
                return result;
            }

            foreach (string id in gated)
            {
                if (!offered.Contains(id)) continue;
                // The two ids the game DOES name a step for (RewardedProgression.LockReasonFor).
                // Read through the same constants the runtime uses so a copy cannot drift.
                if (string.Equals(id, DeNelle.Village.RewardedProgression.HealingCaravanId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(id, DeNelle.Village.RewardedProgression.StoneGateId, StringComparison.OrdinalIgnoreCase))
                {
                    // gate_stone IS named ("Create a Stone Wall to unlock") - and is then hidden
                    // anyway by the unfinished-art constant, which runs first. Named but dead, so
                    // it belongs in the derived set.
                    result.Add(id);
                    continue;
                }
                result.Add(id);
            }
            return result;
        }
    }
}
