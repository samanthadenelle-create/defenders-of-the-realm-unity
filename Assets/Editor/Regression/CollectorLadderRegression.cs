// =============================================================================
// CollectorLadderRegression — a placed COLLECTOR's upgrade ladder lives on a
// DIFFERENT row than the collector, and that row can be hidden from the palette.
// Deleting it must fail the gate, not the game.
// -----------------------------------------------------------------------------
// THE INDIRECTION. Three catalog rows are resource collectors, and none of them
// owns its own progression:
//
//     collector_farm       --repo.collectorBuildingId-->  farm
//     collector_lumbermill --repo.collectorBuildingId-->  lumbermill
//     collector_forge      --repo.collectorBuildingId-->  forge
//
// CatalogRegistry.ResolveUpgradeId performs exactly this hop, and
// BuildingUpgradeVM's constructor runs it before deciding which ladder to draw —
// so the tier tree the player sees on a placed Lumber Mill is authored under
// "lumbermill" in building-tiers.json, NOT under "collector_lumbermill".
//
// WHY THAT IS DANGEROUS. `lumbermill` is listed in the Town verb's `lockedIds` in
// build-categories.json — it is RETIRED FROM THE PALETTE. It therefore reads, to
// anyone skimming the data, as dead content: a row no card is ever drawn for, in a
// list of rows no card is ever drawn for. It is not dead. It is the sole home of
// the live Lumber Mill's four-tier "Restore the Mill" tree. A tidy-up pass that
// deletes retired rows would take the live building's entire progression with it.
//
// AND IT WOULD NOT CRASH — WHICH IS THE WHOLE PROBLEM. BuildingUpgradeVM picks its
// family in order: `_isCity = BuildingTierCatalog.IsUpgradable(id)` FIRST, then
// `_isResource = ResourceBuildingProgression.IsResourceBuilding(id)`. All three
// collector targets satisfy BOTH — farm/lumbermill/forge are the hardcoded trio in
// ResourceBuildingProgression as well as rows in building-tiers.json. So deleting
// the building-tiers row throws no exception and blanks no screen: the panel
// silently falls through to the LEGACY five-level yield ladder and draws a
// perfectly plausible, entirely different progression. No log line, no gate, no
// symptom a playtester would report as a bug. That silent-substitution is exactly
// what this oracle exists to make loud.
//
// READS THE SHIPPED JSON, NOT THE SHARED STATICS. Same rule BuildCardArtRegression
// keeps: CatalogRegistry and BuildingTierCatalog are process-wide caches that other
// suites register fixtures into and that hold whatever the last loader left behind.
// A gate whose verdict depends on suite ORDER is not a gate. Both sides of the link
// are deserialized here from Data/Canonical/*.json through DeNelle.Core.CanonicalJson
// — the same files that ship — into the same typed models the game uses.
//
// SCOPE NOTE — "still exists in the catalog" is NOT the invariant. WO-936 asked for
// an assertion that every collectorBuildingId target still exists as a catalog row.
// That assertion is false on today's green tree: `farm` is NOT a row in
// structures-catalog.json (only `collector_farm` is), and the Farm works fine,
// because a target needs a LADDER, not a catalog row. Asserting catalog existence
// would have failed honest data and taught the next reader the wrong dependency.
// The real load-bearing edge is the building-tiers row, so that is what is pinned.
//
// Registered in DataRegression.RunAll (covenant style).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class CollectorLadderRegression
    {
        private const string CatalogPath    = "Data/Canonical/structures-catalog.json";
        private const string TiersPath      = "Data/Canonical/building-tiers.json";
        private const string CategoriesPath = "Data/Canonical/build-categories.json";

        /// <summary>Shape of Data/Canonical/structures-catalog.json.</summary>
        private sealed class CatalogFile
        {
            [Newtonsoft.Json.JsonProperty("version")] public int Version;
            [Newtonsoft.Json.JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        /// <summary>Shape of Data/Canonical/build-categories.json (only the parts this oracle reads).</summary>
        private sealed class CategoriesFile
        {
            [Newtonsoft.Json.JsonProperty("categories")] public List<CategoryRow> Categories = new List<CategoryRow>();
        }

        private sealed class CategoryRow
        {
            [Newtonsoft.Json.JsonProperty("buildType")] public string BuildType;
            [Newtonsoft.Json.JsonProperty("lockedIds")] public List<string> LockedIds = new List<string>();
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // ── Load both sides of the link from the SHIPPED files ────────────────
            CatalogFile catalog;
            if (!TryReadJson(CatalogPath, out catalog, out string catErr))
            {
                reason = "FAIL: " + catErr;
                return false;
            }

            BuildingTierCatalogData tiers;
            if (!TryReadJson(TiersPath, out tiers, out string tierErr))
            {
                reason = "FAIL: " + tierErr;
                return false;
            }

            var entries = catalog != null && catalog.Entries != null
                ? catalog.Entries
                : new List<CatalogEntry>();
            if (entries.Count == 0)
            {
                reason = "FAIL: " + CatalogPath + " deserialized to ZERO entries — this oracle " +
                         "checked no collector links at all. A silent zero-check is how coverage disappears.";
                return false;
            }

            var tierRows = new Dictionary<string, BuildingUpgradeDef>(StringComparer.Ordinal);
            if (tiers != null && tiers.Buildings != null)
                foreach (var b in tiers.Buildings)
                    if (b != null && !string.IsNullOrEmpty(b.Id)) tierRows[b.Id] = b;
            if (tierRows.Count == 0)
            {
                reason = "FAIL: " + TiersPath + " deserialized to ZERO building rows — every collector " +
                         "ladder would silently fall through to the legacy level curve.";
                return false;
            }

            // Which ids does the catalog itself define, and which rows are collectors?
            // Both are needed to catch a CHAINED indirection (a collector pointing at a
            // collector), which ResolveUpgradeId does NOT follow — it hops exactly once.
            var catalogIds  = new HashSet<string>(StringComparer.Ordinal);
            var collectorOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                catalogIds.Add(e.id);
                string target = e.repo != null ? e.repo.collectorBuildingId : null;
                if (!string.IsNullOrEmpty(target)) collectorOf[e.id] = target;
            }

            if (collectorOf.Count == 0)
            {
                reason = "FAIL: NOT ONE catalog row declares repo.collectorBuildingId. Either every " +
                         "collector was deleted, or the field was renamed and this oracle now checks " +
                         "nothing while still reporting green.";
                return false;
            }

            // Palette visibility — report-only. A target being hidden is the EXPECTED state
            // (that is the hazard this suite exists for), never a failure on its own.
            var lockedIds = ReadLockedIds();

            var notes    = new List<string>();
            var retired  = new List<string>();

            foreach (var kv in collectorOf)
            {
                string collectorId = kv.Key;
                string target      = kv.Value;
                string link        = "'" + collectorId + "' -> '" + target + "'";

                // ── [no-chained-indirection] ─────────────────────────────────────
                // CatalogRegistry.ResolveUpgradeId hops exactly ONCE. A target that is
                // itself a collector would resolve to a row that owns no ladder, and the
                // panel would draw an empty grid with no error.
                if (collectorOf.ContainsKey(target))
                {
                    failures.Add("[no-chained-indirection] " + link + " — the TARGET is itself a collector " +
                                 "(it declares collectorBuildingId '" + collectorOf[target] + "'). " +
                                 "CatalogRegistry.ResolveUpgradeId hops ONCE, so this resolves to a row " +
                                 "with no ladder and the upgrade panel renders empty.");
                    continue;
                }

                // ── [target-owns-tier-rows] — THE LOAD-BEARING ASSERTION ─────────
                BuildingUpgradeDef row;
                if (!tierRows.TryGetValue(target, out row) || row.Tiers == null || row.Tiers.Count == 0)
                {
                    failures.Add("[target-owns-tier-rows] " + link + " — building-tiers.json has NO tier rows " +
                                 "for '" + target + "'. This does NOT crash: BuildingUpgradeVM falls through to " +
                                 "ResourceBuildingProgression's legacy level curve and silently draws a " +
                                 "DIFFERENT progression than the one that was authored. If '" + target + "' was " +
                                 "deleted as a 'retired' row, it was load-bearing — restore it. Retiring an id " +
                                 "from the palette (build-categories lockedIds) is not the same as deleting it.");
                    continue;
                }

                // ── [tier-ladder-contiguous] ─────────────────────────────────────
                // A whole-row delete is the loud case; dropping ONE tier out of the middle
                // is the quiet one. BuildingUpgradeVM walks CurrentTier + 1, so a gap stalls
                // the ladder permanently at the tier below the hole with no message.
                var seen = new HashSet<int>();
                int expected = 1;
                bool ladderOk = true;
                foreach (var t in row.Tiers)
                {
                    if (t == null) continue;
                    if (!seen.Add(t.Tier))
                    {
                        failures.Add("[tier-ladder-contiguous] " + link + " — '" + target + "' authors tier " +
                                     t.Tier + " TWICE. BuildingTierCatalog.TierOf returns the first match, so " +
                                     "the duplicate's cost and effect are unreachable.");
                        ladderOk = false;
                        break;
                    }
                    if (t.Tier != expected)
                    {
                        failures.Add("[tier-ladder-contiguous] " + link + " — '" + target + "' jumps from tier " +
                                     (expected - 1) + " to tier " + t.Tier + ". BuildingUpgradeVM only ever offers " +
                                     "CurrentTier + 1, so the ladder DEAD-ENDS at tier " + (expected - 1) +
                                     " and every tier above the gap is unreachable — with no error and no " +
                                     "player-visible reason.");
                        ladderOk = false;
                        break;
                    }
                    expected++;
                }
                if (!ladderOk) continue;

                // ── Green-path reporting: name the hazard this suite guards ───────
                bool hidden = lockedIds.Contains(target);
                bool inCatalog = catalogIds.Contains(target);
                if (hidden) retired.Add(target);
                notes.Add(link + " ladder=" + (expected - 1) + " tier(s)"
                          + (hidden ? ", PALETTE-RETIRED" : "")
                          + (inCatalog ? "" : ", no catalog row"));
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("FAIL: " + failures.Count + " collector upgrade-ladder link(s) are broken — a placed " +
                              "collector's upgrades are authored on the row its repo.collectorBuildingId points at:");
                foreach (var f in failures) sb.AppendLine("    - " + f);
                reason = sb.ToString().TrimEnd();
                return false;
            }

            var ok = new StringBuilder();
            ok.Append("OK — " + collectorOf.Count + " collector(s) resolve to a target that still owns a " +
                      "contiguous tier ladder in building-tiers.json: " + string.Join("; ", notes.ToArray()) + ".");
            if (retired.Count > 0)
                ok.Append(Environment.NewLine + "    LOAD-BEARING BUT PALETTE-RETIRED: " +
                          string.Join(", ", retired.ToArray()) + " — hidden from the build palette by " +
                          "build-categories lockedIds, yet the sole home of a live collector's progression. " +
                          "Do NOT delete " + (retired.Count == 1 ? "this row" : "these rows") +
                          " in a retired-content cleanup; this suite is the tripwire.");
            reason = ok.ToString();
            return true;
        }

        /// <summary>Every id filtered out of any build verb's palette (build-categories lockedIds).
        /// Read from the shipped file rather than BuildCategoryRegistry so the verdict does not
        /// depend on whether some earlier suite has loaded the registry yet.</summary>
        private static HashSet<string> ReadLockedIds()
        {
            var locked = new HashSet<string>(StringComparer.Ordinal);
            CategoriesFile cats;
            if (!TryReadJson(CategoriesPath, out cats, out _)) return locked;
            if (cats == null || cats.Categories == null) return locked;
            foreach (var c in cats.Categories)
            {
                if (c == null || c.LockedIds == null) continue;
                foreach (var id in c.LockedIds)
                    if (!string.IsNullOrEmpty(id)) locked.Add(id);
            }
            return locked;
        }

        private static bool TryReadJson<T>(string relativePath, out T value, out string error) where T : class
        {
            value = null;
            error = null;
            string json = DeNelle.Core.CanonicalJson.Read(relativePath);
            if (string.IsNullOrEmpty(json))
            {
                error = relativePath + " unreadable — this oracle checked ZERO collector links.";
                return false;
            }
            try
            {
                value = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json,
                    new Newtonsoft.Json.JsonSerializerSettings
                    {
                        Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                        MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore,
                    });
            }
            catch (Exception ex)
            {
                error = relativePath + " failed to parse: " + ex.Message;
                return false;
            }
            if (value == null)
            {
                error = relativePath + " deserialized to null.";
                return false;
            }
            return true;
        }
    }
}
