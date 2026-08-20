// =============================================================================
// ContentPackingRegression — pins the owner's 2026-08-20 packing ruling:
//   "this means I want this broken down to each family of enemy"
//   "i want the structures one at a time"
// -----------------------------------------------------------------------------
// WHAT IT PROTECTS, AND WHY A GATE RATHER THAN A COMMENT.
// Both art groups used to be PackTogether, so the built filenames read
// "enemy_art_assets_all_<hash>.bundle" (64.5 MB) and
// "structure_art_assets_all_<hash>.bundle" (19.7 MB). "assets_all" IS the
// PackTogether name: with it, the first Hollow Skirmisher a player meets drags the
// entire 64 MB enemy catalogue over the CDN, and the first hut drags all 19.7 MB.
//
// The fix is a SETTINGS value — one enum on a group schema. That is exactly the kind
// of change that gets silently reverted: by a merge, by the Addressables Groups
// window (two clicks, no diff review), or by a seat who "cleaned up" labels. Nothing
// would break loudly. The game would still run, still resolve every address, still
// gate green — and quietly go back to one 64 MB blob. This suite is the only thing
// that turns that regression into a FAILURE instead of a slow phone.
//
// ⛔ THE ADDRESS CHECK IS THE OTHER HALF, AND IT IS THE DANGEROUS ONE.
// Re-packing must change PACKING ONLY. Addresses are the contract: structures-catalog
// .json authors them verbatim as repo.visualPrefabPath / repo.upgradeVisualPath, and
// StructureAssetLoader resolves that exact string. This project has ALREADY shipped
// that failure once - visualPrefabPath "Structures/WizardTower_1" outlived its art and
// put a wizard tower where the Ballista belonged, in front of real players. So this
// suite re-walks every catalog art path and proves it still names a REGISTERED
// address, every run, rather than trusting that a packing edit "obviously" left
// addressing alone.
//
// BOTH DIRECTIONS ARE ASSERTED (a test that cannot fail proves nothing). Besides
// checking the live settings, PredicateSelfTest() feeds the same accept/reject
// predicates the REVERTED values - PackTogether for either group, a single family
// bucket - and fails if any of them is accepted. A future refactor that neuters the
// check into `return true` therefore fails here first.
//
// Marker: CONTENT_PACKING_OK / CONTENT_PACKING_FAIL. Read the MARKER, not the exit
// code (CLAUDE.md §8).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Proves the per-family / per-asset content packing intent still holds.</summary>
    public static class ContentPackingRegression
    {
        public const string OkMarker   = "CONTENT_PACKING_OK";
        public const string FailMarker = "CONTENT_PACKING_FAIL";

        private const string EnemyGroupName     = "Enemy_Art";
        private const string StructureGroupName = "Structure_Art";
        private const string FamilyLabelPrefix  = "enemyfam-";

        // Both copies are checked. They are meant to be identical, and when they drift
        // the RESOURCES copy is what a player build actually reads - so a suite that
        // only looked at one could pass while the shipped one was broken.
        private static readonly string[] CatalogPaths =
        {
            "Assets/Resources/Data/Canonical/structures-catalog.json",
            "Assets/StreamingAssets/Data/Canonical/structures-catalog.json",
        };

        [MenuItem("Defenders/Regression/Verify content packing")]
        public static void RunMenu() => RunAll();

        /// <summary>Batchmode entry point. Emits the marker and exits 1 on failure.</summary>
        public static void RunAll()
        {
            if (Run(out string reason))
            {
                Debug.Log($"{OkMarker} :: {reason}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            Debug.LogError($"{FailMarker} :: {reason}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>
        /// True when the packing intent holds. <paramref name="reason"/> always carries a
        /// §1.4b-grade explanation - on failure it names WHAT was wrong, WHY it matters
        /// and HOW to fix it, so the next reader never has to re-derive the ruling.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes    = new List<string>();

            // ── 0. The predicates must still be able to say NO. ───────────────
            PredicateSelfTest(failures, notes);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                reason = "no AddressableAssetSettings asset — Addressables is not configured, so the " +
                         "per-family / per-asset packing this suite pins cannot exist. FIX: restore " +
                         "Assets/AddressableAssetsData/AddressableAssetSettings.asset.";
                return false;
            }

            CheckStructuresPackedPerAsset(settings, failures, notes);
            CheckEnemiesPackedPerFamily(settings, failures, notes);
            CheckCatalogArtPathsResolve(settings, failures, notes);

            reason = failures.Count == 0
                   ? string.Join("; ", notes)
                   : string.Join(" | ", failures);
            return failures.Count == 0;
        }

        // =====================================================================
        // 1. STRUCTURES — not one blob
        // =====================================================================

        private static void CheckStructuresPackedPerAsset(
            AddressableAssetSettings settings, List<string> failures, List<string> notes)
        {
            var group = settings.FindGroup(StructureGroupName);
            if (group == null)
            {
                failures.Add($"[structures] group '{StructureGroupName}' does not exist. Every building's art " +
                             "is registered there; without it nothing in town can resolve. FIX: restore " +
                             $"Assets/AddressableAssetsData/AssetGroups/{StructureGroupName}.asset.");
                return;
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                failures.Add($"[structures] group '{StructureGroupName}' has no BundledAssetGroupSchema, so it " +
                             "declares no BundleMode at all and the build falls back to defaults. FIX: add the " +
                             "schema and set BundleMode = PackSeparately.");
                return;
            }

            if (!AcceptsStructureMode(schema.BundleMode))
            {
                failures.Add($"[structures] BundleMode is '{schema.BundleMode}', not PackSeparately. " +
                             "PackTogether packs all 35 buildings into ONE structure_art_assets_all bundle " +
                             "(19.7 MB), so placing the very first hut downloads every building in the game. " +
                             "Owner ruling 2026-08-20: \"i want the structures one at a time\". " +
                             "FIX: run Defenders > Addressables > Apply per-family + per-asset packing, or set " +
                             "BundleMode = PackSeparately on " +
                             $"Assets/AddressableAssetsData/AssetGroups/Schemas/{StructureGroupName}_BundledAssetGroupSchema.asset.");
                return;
            }

            int n = group.entries.Count;
            if (n < 2)
            {
                failures.Add($"[structures] '{StructureGroupName}' holds {n} entr(y/ies). PackSeparately on a " +
                             "one-entry group is indistinguishable from PackTogether — the group has been " +
                             "emptied behind this gate. FIX: re-register the structure art.");
                return;
            }
            notes.Add($"[structures] PackSeparately over {n} entries");
        }

        // =====================================================================
        // 2. ENEMIES — not one blob, and split on FAMILY
        // =====================================================================

        private static void CheckEnemiesPackedPerFamily(
            AddressableAssetSettings settings, List<string> failures, List<string> notes)
        {
            var group = settings.FindGroup(EnemyGroupName);
            if (group == null)
            {
                failures.Add($"[enemies] group '{EnemyGroupName}' does not exist. FIX: restore " +
                             $"Assets/AddressableAssetsData/AssetGroups/{EnemyGroupName}.asset.");
                return;
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                failures.Add($"[enemies] group '{EnemyGroupName}' has no BundledAssetGroupSchema. " +
                             "FIX: add it and set BundleMode = PackTogetherByLabel.");
                return;
            }

            if (!AcceptsEnemyMode(schema.BundleMode))
            {
                failures.Add($"[enemies] BundleMode is '{schema.BundleMode}', not PackTogetherByLabel. " +
                             "PackTogether packs every enemy in the game into ONE " +
                             "enemy_art_assets_all bundle (64.5 MB), so meeting a single Hollow Skirmisher " +
                             "pulls the orcs, the trolls and the dragon too. Owner ruling 2026-08-20: " +
                             "\"I want this broken down to each family of enemy\". " +
                             "FIX: run Defenders > Addressables > Apply per-family + per-asset packing.");
                return;
            }

            // Every entry needs EXACTLY ONE family label. Zero => it joins the unlabelled
            // catch-all bundle; two => Addressables buckets by the label SET and silently
            // mints a THIRD bundle for that pair, duplicating nothing but confusing
            // everything. Both are counted and named.
            var buckets   = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var unlabelled = new List<string>();
            var multi      = new List<string>();

            foreach (var entry in group.entries)
            {
                if (entry == null) continue;
                var fam = entry.labels == null
                        ? new List<string>()
                        : entry.labels.Where(l => l != null &&
                              l.StartsWith(FamilyLabelPrefix, StringComparison.Ordinal)).ToList();

                if (fam.Count == 0) { unlabelled.Add(entry.address); continue; }
                if (fam.Count > 1)  { multi.Add($"{entry.address}[{string.Join(",", fam)}]"); continue; }

                buckets.TryGetValue(fam[0], out int c);
                buckets[fam[0]] = c + 1;
            }

            if (unlabelled.Count > 0)
            {
                failures.Add($"[enemies] {unlabelled.Count} entr(ies) carry NO '{FamilyLabelPrefix}*' label, so " +
                             "PackTogetherByLabel drops them all into one unlabelled bundle — a second blob, " +
                             "which is the defect this ruling removes. First few: " +
                             string.Join(", ", unlabelled.Take(6)) +
                             ". FIX: re-run the packing tool; it derives the family from enemies.json.");
            }

            if (multi.Count > 0)
            {
                failures.Add($"[enemies] {multi.Count} entr(ies) carry MORE THAN ONE family label. Addressables " +
                             "buckets by the whole label SET, so each extra combination mints its own bundle and " +
                             "the family grain stops being one-bundle-per-family: " +
                             string.Join(", ", multi.Take(6)) +
                             ". FIX: re-run the packing tool (it strips stale family labels before applying).");
            }

            if (!AcceptsFamilyBucketCount(buckets.Count))
            {
                failures.Add($"[enemies] only {buckets.Count} distinct family bucket(s) " +
                             $"({string.Join(", ", buckets.Keys)}). With one bucket, PackTogetherByLabel " +
                             "produces exactly ONE bundle and is functionally identical to the PackTogether " +
                             "this change replaced. FIX: check that enemies.json still carries more than one " +
                             "'family' token and re-run the packing tool.");
            }

            if (failures.Count == 0 || buckets.Count > 0)
                notes.Add($"[enemies] PackTogetherByLabel over {buckets.Count} families (" +
                          string.Join(", ", buckets.Select(kv => kv.Key + ":" + kv.Value)) + ")");
        }

        // =====================================================================
        // 3. ADDRESSES — every catalog art path still resolves
        // =====================================================================

        private static void CheckCatalogArtPathsResolve(
            AddressableAssetSettings settings, List<string> failures, List<string> notes)
        {
            // Build the live address set ONCE, from every group. A structure address is
            // allowed to live in any group - the check is that the STRING resolves, not
            // that it sits where we expect.
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in settings.groups)
            {
                if (g == null) continue;
                foreach (var e in g.entries)
                    if (e != null && !string.IsNullOrEmpty(e.address)) addresses.Add(e.address);
            }

            int checkedPaths = 0;
            foreach (string catalogPath in CatalogPaths)
            {
                if (!File.Exists(catalogPath))
                {
                    failures.Add($"[addresses] catalog '{catalogPath}' is missing — the structure art contract " +
                                 "cannot be verified. FIX: restore the file, or drop it from CatalogPaths here " +
                                 "if it was retired on purpose.");
                    continue;
                }

                string json;
                try { json = File.ReadAllText(catalogPath); }
                catch (Exception e)
                {
                    failures.Add($"[addresses] could not read '{catalogPath}': {e.GetType().Name}: {e.Message}");
                    continue;
                }

                // Text-scan, not a typed deserialize: a schema addition to the catalog must
                // never be able to break this gate (same reasoning as
                // AssetMoveManifestRegression.ParseEntries).
                var missing = new List<string>();
                foreach (Match m in Regex.Matches(
                             json, "\"(visualPrefabPath|upgradeVisualPath)\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string field = m.Groups[1].Value;
                    string addr  = m.Groups[2].Value;
                    checkedPaths++;
                    if (!addresses.Contains(addr))
                        missing.Add($"{field}='{addr}'");
                }

                if (missing.Count > 0)
                {
                    failures.Add($"[addresses] {missing.Count} art path(s) in '{catalogPath}' name an address " +
                                 "that is NOT registered with Addressables. The loader resolves that exact " +
                                 "string, so each one is an INVISIBLE BUILDING in a live build (the " +
                                 "\"WizardTower_1\" class of defect). Re-packing must change PACKING ONLY, never " +
                                 "an address — if this appeared after a re-pack, the re-pack renamed something. " +
                                 "Offenders: " + string.Join(", ", missing.Take(8)) +
                                 ". FIX: restore the address on the entry, or correct the catalog row.");
                }
            }

            if (checkedPaths == 0)
            {
                failures.Add("[addresses] ZERO art paths were checked. A check that inspects nothing passes " +
                             "for free and proves nothing — the catalog field names changed, or the files are " +
                             "empty. FIX: confirm structures-catalog.json still authors " +
                             "repo.visualPrefabPath / repo.upgradeVisualPath.");
            }
            else
            {
                notes.Add($"[addresses] {checkedPaths} catalog art path(s) resolve against " +
                          $"{addresses.Count} registered addresses");
            }
        }

        // =====================================================================
        // 4. THE PREDICATES + their self-test (assert BOTH directions)
        // =====================================================================

        /// <summary>Structures pass ONLY on PackSeparately — "one at a time".</summary>
        public static bool AcceptsStructureMode(BundledAssetGroupSchema.BundlePackingMode mode) =>
            mode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately;

        /// <summary>Enemies pass ONLY on PackTogetherByLabel — the family grain.
        /// PackSeparately is rejected too: it would be ~78 bundles and would split an
        /// Orc body from its own normal map, which is finer than the ruling asked for.</summary>
        public static bool AcceptsEnemyMode(BundledAssetGroupSchema.BundlePackingMode mode) =>
            mode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;

        /// <summary>A family split needs at least two buckets; one bucket IS PackTogether.</summary>
        public static bool AcceptsFamilyBucketCount(int bucketCount) => bucketCount >= 2;

        /// <summary>
        /// Feeds the predicates the REVERTED values and fails if any is accepted. Without
        /// this, a predicate quietly rewritten to `return true` would make every check
        /// above pass forever while the content went back to two giant blobs.
        /// </summary>
        private static void PredicateSelfTest(List<string> failures, List<string> notes)
        {
            var must = new List<string>();

            if (AcceptsStructureMode(BundledAssetGroupSchema.BundlePackingMode.PackTogether))
                must.Add("AcceptsStructureMode ACCEPTED PackTogether");
            if (AcceptsStructureMode(BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel))
                must.Add("AcceptsStructureMode ACCEPTED PackTogetherByLabel");
            if (!AcceptsStructureMode(BundledAssetGroupSchema.BundlePackingMode.PackSeparately))
                must.Add("AcceptsStructureMode REJECTED the required PackSeparately");

            if (AcceptsEnemyMode(BundledAssetGroupSchema.BundlePackingMode.PackTogether))
                must.Add("AcceptsEnemyMode ACCEPTED PackTogether");
            if (!AcceptsEnemyMode(BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel))
                must.Add("AcceptsEnemyMode REJECTED the required PackTogetherByLabel");

            if (AcceptsFamilyBucketCount(0) || AcceptsFamilyBucketCount(1))
                must.Add("AcceptsFamilyBucketCount ACCEPTED a single-bucket (= one blob) split");
            if (!AcceptsFamilyBucketCount(2))
                must.Add("AcceptsFamilyBucketCount REJECTED a legitimate two-family split");

            if (must.Count > 0)
            {
                failures.Add("[self-test] the packing predicates no longer reject the reverted state, so every " +
                             "check in this suite is vacuous: " + string.Join(", ", must) +
                             ". FIX: restore the predicates — they must accept ONLY the owner's packing shape.");
            }
            else
            {
                notes.Add("[self-test] predicates reject PackTogether in both lanes");
            }
        }

        /// <summary>Human-readable summary, for a caller that wants the detail inline.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return "no AddressableAssetSettings";
            foreach (var g in settings.groups)
            {
                if (g == null) continue;
                var s = g.GetSchema<BundledAssetGroupSchema>();
                sb.AppendLine($"{g.Name}: {(s == null ? "no schema" : s.BundleMode.ToString())} " +
                              $"({g.entries.Count} entries)");
            }
            return sb.ToString();
        }
    }
}
