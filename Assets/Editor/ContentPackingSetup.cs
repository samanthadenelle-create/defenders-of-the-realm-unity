// =============================================================================
// ContentPackingSetup — WO owner ruling 2026-08-20: "this means I want this broken
// down to each family of enemy" / "i want the structures one at a time".
// -----------------------------------------------------------------------------
// THE MEASURED PROBLEM. Both art groups shipped as PackTogether, so the built
// filenames literally said so:
//     enemy_art_assets_all_<hash>.bundle       64.5 MB
//     structure_art_assets_all_<hash>.bundle   19.7 MB
// "assets_all" is the PackTogether name. There is NO way to fetch one enemy or one
// building: the first Hollow Skirmisher a player meets pulls all 64 MB over the CDN
// before it can be drawn, and the first hut pulls all 19.7 MB. That is the whole of
// the defect — nothing about the ADDRESSES is wrong, only the packing.
//
// ⛔ THIS FILE CHANGES PACKING ONLY. NEVER AN ADDRESS.
// Addresses are the contract: structures-catalog.json authors them verbatim as
// repo.visualPrefabPath / repo.upgradeVisualPath, and StructureAssetLoader /
// EnemyAssetLoader resolve by that exact string. Move an address and every catalog
// row pointing at it resolves to nothing — an invisible building, in a live build
// (the exact failure class WO-1124 and the "WizardTower_1" incident already cost
// this project). So this script only ever writes BundleMode and LABELS; the
// address, the GUID and the asset path of every entry are left untouched, and
// DumpAddresses() exists so that claim is PROVEN by diff rather than asserted.
//
// WHY LABELS FOR ENEMIES AND PackSeparately FOR STRUCTURES.
//   • Structures: the owner asked for "one at a time", which is exactly
//     BundlePackingMode.PackSeparately — one bundle per entry, natively supported by
//     Addressables 2.9.1, no new group assets, no new schema files, and no address
//     churn. Making 35 one-entry GROUPS would achieve the identical bundle shape
//     while adding 70 new .asset files to maintain and 35 more places for a build
//     path to drift. Same result, more moving parts: rejected.
//   • Enemies: the owner asked for a FAMILY grain, which is coarser than per-entry
//     and finer than per-group — that is precisely BundlePackingMode
//     .PackTogetherByLabel. Addressables buckets entries by their LABEL SET, so
//     tagging each entry with exactly one family label yields exactly one bundle per
//     family. PackSeparately here would give ~78 bundles and split a single Orc body
//     from its own normal map, costing an extra request per texture for no benefit.
//
// ⛔ THE FAMILY LIST IS DERIVED FROM DATA, NOT TYPED HERE (CLAUDE.md §0/§2 lesson:
// a list copied into a second file is the bug, even when it is right the day it is
// written — the stale WO number block and the hardcoded repo root were both this).
// FamilyMap() reads Assets/Resources/Data/Canonical/enemies.json and takes the
// modelKey -> family pairing from the rows themselves. Add an enemy family to that
// JSON and this grouper picks it up with no edit here. The keyword fallback below
// only catches art that has NO enemies.json row at all (the WO-481 orc bodies, the
// misc Demon/Dragon), and it is logged per-address so a wrong bucket is visible in
// the dump rather than silent.
//
// READ THE MARKER, NOT THE EXIT CODE (CLAUDE.md §8).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Re-packs the Enemy_Art / Structure_Art Addressable groups to a per-family /
    /// per-asset grain, and dumps the evidence (address inventory + built bundle
    /// inventory) that proves the change was packing-only.
    /// </summary>
    public static class ContentPackingSetup
    {
        // ── Names this file is allowed to touch. Everything else (Gear, Dungeon, the
        // hero group, localization) is explicitly OUT OF SCOPE and must not be read
        // for modification — those lanes belong to other work. ────────────────────
        public const string EnemyGroupName     = "Enemy_Art";
        public static readonly string[] EnemyFamilyGroupNames =
            { "Enemy_Models", "Enemy_Controllers", "Enemy_Textures" };
        public const string StructureGroupName = "Structure_Art";
        private const string RemoteBuildPathId = "ad0e68328bd7fd54ea79f0a9ab1dd9b1";
        private const string RemoteLoadPathId  = "cf151d4962873af43b9302d323a9d707";

        /// <summary>Prefix every family label carries, so the labels this tool owns are
        /// distinguishable from any hand-authored label at a glance and in the catalog.</summary>
        public const string FamilyLabelPrefix = "enemyfam-";

        /// <summary>Bucket for enemy art that belongs to NO single family — the generic
        /// animator controllers and the shared materials. It is ONE bundle, so a shared
        /// asset is fetched once and never duplicated into each family bundle.</summary>
        public const string SharedFamily = "shared";

        private const string EnemiesJsonPath = "Assets/Resources/Data/Canonical/enemies.json";
        private const string LogDir          = "logs/addressables";

        public const string MarkerApplied     = "CONTENT_PACKING_APPLIED";
        public const string MarkerApplyFail   = "CONTENT_PACKING_APPLY_FAIL";
        public const string MarkerDump        = "ADDR_DUMP_OK";
        public const string MarkerDumpFail    = "ADDR_DUMP_FAIL";
        public const string MarkerBuild       = "CONTENT_BUILD_REPORT_OK";
        public const string MarkerBuildFail   = "CONTENT_BUILD_REPORT_FAIL";

        // =====================================================================
        // ENTRY POINTS (batchmode)
        // =====================================================================

        /// <summary>Write the CURRENT address inventory to logs/addressables/addresses-before.txt.</summary>
        public static void DumpAddressesBefore() => DumpAddresses("before");

        /// <summary>Write the address inventory to logs/addressables/addresses-after.txt.</summary>
        public static void DumpAddressesAfter() => DumpAddresses("after");

        /// <summary>Build content and report the bundle inventory as .../bundles-before.txt.</summary>
        public static void BuildAndReportBefore() => BuildAndReport("before");

        /// <summary>Build content and report the bundle inventory as .../bundles-after.txt.</summary>
        public static void BuildAndReportAfter() => BuildAndReport("after");

        [MenuItem("Defenders/Addressables/Apply per-family + per-asset packing")]
        public static void ApplyPackingMenu() => ApplyPacking();

        /// <summary>Batchmode wrapper — Unity's -executeMethod wants a void, no-arg static.</summary>
        public static void ApplyPackingBatch() => ApplyPacking();

        /// <summary>ONE batchmode session: address inventory + a content build, tagged "before".
        /// Combined because each Unity batchmode launch costs minutes and the two artefacts
        /// must describe the SAME tree — capturing them in separate sessions would let an
        /// edit land between them and make the diff lie.</summary>
        public static void CaptureBefore()
        {
            DumpAddresses("before");
            BuildAndReport("before");
        }

        /// <summary>The mirror of <see cref="CaptureBefore"/>, tagged "after".</summary>
        public static void CaptureAfter()
        {
            DumpAddresses("after");
            BuildAndReport("after");
        }

        /// <summary>
        /// Same build, with the Addressables BUILD LAYOUT report switched on.
        ///
        /// WHY IT IS A SEPARATE ENTRY POINT. Splitting a group PackSeparately makes every
        /// bundle carry its own copy of any implicit (non-addressable) dependency it
        /// touches — shared materials, shaders, meshes. That duplication is the entire
        /// cost side of the owner's "one at a time" ruling, and it CANNOT be read off the
        /// bundle sizes: a bigger total is equally consistent with per-bundle archive
        /// overhead. The layout report is the only artefact that itemises which asset was
        /// copied into how many bundles, so the size argument is measured rather than
        /// reasoned. Left OFF by default because it costs build time on every build.
        /// Output: Library/com.unity.addressables/buildReports/buildlayout_*.json.
        /// </summary>
        public static void BuildWithLayoutReport()
        {
            // RESTORED in a finally. GenerateBuildLayout is a STICKY editor preference, not a
            // per-build argument: leaving it on silently taxes every subsequent content build
            // on this machine, and because it lives outside the repo no diff would ever show
            // why builds got slower.
            bool previous = ProjectConfigData.GenerateBuildLayout;
            try
            {
                ProjectConfigData.GenerateBuildLayout = true;
                BuildAndReport("after-layout");
            }
            finally
            {
                ProjectConfigData.GenerateBuildLayout = previous;
            }
        }

        /// <summary>Force the sticky build-layout preference back off (recovery entry point
        /// for a session that was interrupted between enabling it and the finally above).</summary>
        public static void DisableBuildLayoutReport()
        {
            ProjectConfigData.GenerateBuildLayout = false;
            Debug.Log($"{MarkerApplied} :: build-layout report preference is OFF " +
                      $"(GenerateBuildLayout={ProjectConfigData.GenerateBuildLayout}).");
        }

        // =====================================================================
        // 1. ADDRESS INVENTORY — the proof that packing changed and addressing did not
        // =====================================================================

        /// <summary>
        /// Dumps every entry of EVERY group as "group|address|guid|assetPath|bytes",
        /// sorted. Diffing the before/after files is the ONLY acceptable evidence that
        /// re-packing did not move an address — asserting it from the settings asset
        /// would be exactly the inference-fix §12 forbids.
        /// </summary>
        public static void DumpAddresses(string tag)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"{MarkerDumpFail} :: no AddressableAssetSettings asset — " +
                               "Addressables is not configured in this project, so there is nothing to dump.");
                return;
            }

            var lines = new List<string>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;
                    long bytes = FileBytes(entry.AssetPath);
                    lines.Add(string.Join("|", group.Name, entry.address, entry.guid,
                                          entry.AssetPath ?? "", bytes.ToString(CultureInfo.InvariantCulture)));
                }
            }
            lines.Sort(StringComparer.Ordinal);

            string path = WriteLog($"addresses-{tag}.txt", lines);
            Debug.Log($"{MarkerDump} {lines.Count} entries -> {path}");
        }

        // =====================================================================
        // 2. THE RE-PACK
        // =====================================================================

        /// <summary>
        /// Applies the owner's packing shape. Returns false with a NAMED reason rather
        /// than throwing, so a caller can gate on it.
        /// </summary>
        public static bool ApplyPacking()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"{MarkerApplyFail} :: no AddressableAssetSettings asset.");
                return false;
            }

            var report = new StringBuilder();
            var failures = new List<string>();

            // ── STRUCTURES: one bundle per asset ──────────────────────────────
            var structures = settings.FindGroup(StructureGroupName);
            if (structures == null)
            {
                failures.Add($"group '{StructureGroupName}' not found — it was renamed or deleted; " +
                             "re-packing cannot proceed without knowing which group holds the building art.");
            }
            else
            {
                var schema = structures.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    failures.Add($"group '{StructureGroupName}' carries no BundledAssetGroupSchema, " +
                                 "so it has no BundleMode to set.");
                }
                else
                {
                    schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                    EditorUtility.SetDirty(schema);
                    EditorUtility.SetDirty(structures);
                    report.AppendLine($"[structures] {StructureGroupName}.BundleMode = PackSeparately " +
                                      $"({structures.entries.Count} entries -> one bundle each).");
                }
            }

            // ── ENEMIES: one bundle per family ────────────────────────────────
            var enemies = settings.FindGroup(EnemyGroupName);
            if (enemies == null)
            {
                failures.Add($"group '{EnemyGroupName}' not found — it was renamed or deleted.");
            }
            else
            {
                var schema = enemies.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    failures.Add($"group '{EnemyGroupName}' carries no BundledAssetGroupSchema.");
                }
                else
                {
                    var familyMap = FamilyMap(out string mapSource);
                    report.AppendLine($"[enemies] family map derived from {mapSource}: " +
                                      string.Join(", ", familyMap.Select(kv => kv.Key + "->" + kv.Value)));

                    var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    foreach (var entry in enemies.entries.ToList())
                    {
                        if (entry == null) continue;
                        string family = FamilyForAddress(entry.address, familyMap);
                        string label  = FamilyLabelPrefix + family;

                        settings.AddLabel(label, false);

                        // Strip any family label this tool previously applied, so a re-run is
                        // idempotent and a re-classified asset does not end up double-labelled
                        // (two labels = a THIRD bundle for that pair, silently).
                        foreach (var old in entry.labels.Where(l => l != null &&
                                     l.StartsWith(FamilyLabelPrefix, StringComparison.Ordinal)).ToList())
                        {
                            if (old != label) entry.SetLabel(old, false, false, false);
                        }
                        entry.SetLabel(label, true, false, false);

                        counts.TryGetValue(family, out int n);
                        counts[family] = n + 1;
                        report.AppendLine($"    {entry.address} -> {label}");
                    }

                    if (counts.Count < 2)
                    {
                        failures.Add($"every enemy entry landed in a single family bucket " +
                                     $"({(counts.Count == 0 ? "none" : counts.Keys.First())}) — " +
                                     "PackTogetherByLabel would then produce ONE bundle again, i.e. the " +
                                     "defect this change exists to remove. The family derivation is wrong.");
                    }

                    schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
                    EditorUtility.SetDirty(schema);
                    EditorUtility.SetDirty(enemies);
                    report.AppendLine($"[enemies] {EnemyGroupName}.BundleMode = PackTogetherByLabel; buckets = " +
                                      string.Join(", ", counts.Select(kv => kv.Key + ":" + kv.Value)));
                }
            }

            // The meshes/controllers/textures are the payload the runtime actually fetches.
            // Keeping these Local+PackTogether made Enemy_Art's family split cosmetic.
            var payloadFamilyMap = FamilyMap(out string payloadMapSource);
            foreach (string groupName in EnemyFamilyGroupNames)
                ConfigureEnemyFamilyGroup(settings, groupName, payloadFamilyMap, payloadMapSource,
                    report, failures);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteLog("packing-apply.txt", new[] { report.ToString() });
            Debug.Log(report.ToString());

            if (failures.Count > 0)
            {
                Debug.LogError($"{MarkerApplyFail} :: " + string.Join(" | ", failures));
                return false;
            }
            Debug.Log(MarkerApplied);
            return true;
        }

        private static void ConfigureEnemyFamilyGroup(AddressableAssetSettings settings, string groupName,
            Dictionary<string, string> familyMap, string mapSource, StringBuilder report, List<string> failures)
        {
            var group = settings.FindGroup(groupName);
            if (group == null) { failures.Add($"group '{groupName}' not found."); return; }
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) { failures.Add($"group '{groupName}' carries no BundledAssetGroupSchema."); return; }

            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in group.entries.ToList())
            {
                if (entry == null) continue;
                string family = FamilyForAddress(entry.address, familyMap);
                string label = FamilyLabelPrefix + family;
                settings.AddLabel(label, false);
                foreach (string old in entry.labels.Where(l => l != null &&
                             l.StartsWith(FamilyLabelPrefix, StringComparison.Ordinal)).ToList())
                    if (old != label) entry.SetLabel(old, false, false, false);
                entry.SetLabel(label, true, false, false);
                counts.TryGetValue(family, out int n);
                counts[family] = n + 1;
            }

            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
            schema.BuildPath.SetVariableById(settings, RemoteBuildPathId);
            schema.LoadPath.SetVariableById(settings, RemoteLoadPathId);
            schema.RetryCount = 2;
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);
            report.AppendLine($"[enemies] {groupName}: Remote PackTogetherByLabel, retry=2, " +
                              $"map={mapSource}, buckets=" +
                              string.Join(", ", counts.Select(kv => kv.Key + ":" + kv.Value)));
        }

        // =====================================================================
        // 3. FAMILY DERIVATION (data-first)
        // =====================================================================

        /// <summary>
        /// modelKey -> family, read from enemies.json. This is the DATA the owner's
        /// "each family of enemy" grain is defined by; nothing here invents a family.
        /// </summary>
        public static Dictionary<string, string> FamilyMap(out string source)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            source = EnemiesJsonPath;

            string json;
            try { json = File.ReadAllText(EnemiesJsonPath); }
            catch (Exception e)
            {
                source = $"{EnemiesJsonPath} (UNREADABLE: {e.GetType().Name}) — keyword fallback only";
                return map;
            }

            // ⛔ SCOPE THE SCAN TO THE "enemies" ARRAY FIRST. This is not tidiness — it is the
            // fix for a real mis-parse this tool shipped on its first run. enemies.json carries
            // a sibling "_schemaNotes" OBJECT whose KEYS are the schema's own field names
            // ("id", "family", "modelKey", ...) and whose VALUES are paragraphs of prose. A
            // whole-file row scan therefore reads the documentation as if it were an enemy and
            // injects a garbage `<paragraph> -> <paragraph>` pair into the family map, which
            // the longest-prefix matcher below then has to be LUCKY to ignore. Filtering on the
            // presence of "id" does NOT help: the notes block documents "id" too. Only the
            // array boundary distinguishes data from documentation.
            string rows = ExtractEnemiesArray(json);
            if (rows == null)
            {
                source = $"{EnemiesJsonPath} (no \"enemies\" array found) — keyword fallback only";
                return map;
            }

            // Text-scan rather than a typed deserialize (same reasoning as
            // AssetMoveManifestRegression.ParseEntries): a schema addition must not be
            // able to break the grouper. Each row object is matched whole, then its
            // fields are pulled out, so a row missing one is simply skipped instead of
            // pairing a modelKey with the NEXT row's family.
            foreach (Match row in Regex.Matches(rows, "\\{[^{}]*\\}", RegexOptions.Singleline))
            {
                var fam = Regex.Match(row.Value, "\"family\"\\s*:\\s*\"([^\"]+)\"");
                var mk  = Regex.Match(row.Value, "\"modelKey\"\\s*:\\s*\"([^\"]+)\"");
                if (!fam.Success || !mk.Success) continue;

                string key = mk.Groups[1].Value;
                // Belt and braces: a mesh key is a FILENAME and can never contain whitespace.
                // If prose ever reaches here again it is dropped loudly rather than silently
                // poisoning the prefix matcher.
                if (key.Any(char.IsWhiteSpace))
                {
                    Debug.LogWarning($"[ContentPackingSetup] ignoring an enemies.json modelKey that looks like " +
                                     $"prose, not a mesh filename: \"{Truncate(key, 60)}\".");
                    continue;
                }
                map[key] = Normalize(fam.Groups[1].Value);
            }
            return map;
        }

        /// <summary>
        /// The family bucket for one enemy ADDRESS. Pure and static so the regression can
        /// exercise it without an Addressables settings object.
        ///
        /// Satellite art follows its model by construction, not by a hand list:
        ///   "Enemies/Orc_Mage.fbm/..."   -> the ".fbm" sidecar folder NAMES its model
        ///   "Enemies/TripoTex/Troll_normal" / "Enemies/OrcTex/Orc_Tank_basecolor"
        ///                                -> the texture pools name the model in the LEAF
        ///   "Enemies/Materials/skeleton" -> same leaf rule; a leaf that names no model
        ///                                   (Glow, Material_Pbr) falls to <see cref="SharedFamily"/>.
        /// </summary>
        public static string FamilyForAddress(string address, Dictionary<string, string> familyMap)
        {
            if (string.IsNullOrEmpty(address)) return SharedFamily;

            string t = address.StartsWith("Enemies/", StringComparison.Ordinal)
                     ? address.Substring("Enemies/".Length) : address;

            string[] seg = t.Split('/');
            string first = seg[0];
            string leaf  = seg[seg.Length - 1];

            if (seg.Length > 1 && first.EndsWith(".fbm", StringComparison.OrdinalIgnoreCase))
                return FamilyForModelName(first.Substring(0, first.Length - 4), familyMap);

            return FamilyForModelName(leaf, familyMap);
        }

        /// <summary>
        /// family for a bare art name. Exact data match wins; then LONGEST modelKey prefix
        /// (so "Orc_Necromancer_basecolor" cannot be stolen by a shorter "Orc" key); then a
        /// keyword fallback for the committed bodies that legitimately carry no
        /// enemies.json row (the WO-481 orc warband, the misc Demon / Boss_Dragon).
        /// </summary>
        public static string FamilyForModelName(string name, Dictionary<string, string> familyMap)
        {
            if (string.IsNullOrEmpty(name)) return SharedFamily;

            if (familyMap != null)
            {
                if (familyMap.TryGetValue(name, out string exact)) return exact;

                string bestKey = null;
                foreach (var kv in familyMap)
                {
                    if (name.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) &&
                        (bestKey == null || kv.Key.Length > bestKey.Length))
                        bestKey = kv.Key;
                }
                if (bestKey != null) return familyMap[bestKey];
            }

            string n = name.ToLowerInvariant();
            if (n.StartsWith("skeleton", StringComparison.Ordinal) ||
                (n.Contains("necromancer") && !n.StartsWith("orc", StringComparison.Ordinal)))
                return "hollow";
            if (n.StartsWith("orc", StringComparison.Ordinal))   return "orc";
            if (n.StartsWith("troll", StringComparison.Ordinal)) return "troll";
            if (n.StartsWith("demon", StringComparison.Ordinal) ||
                n.StartsWith("boss", StringComparison.Ordinal)  ||
                n.Contains("dragon"))
                return "bosses";

            // Generic rigs (HumanoidEnemy / LargeEnemy / LargeHumanoid) and the shared
            // materials (Glow / Material_Pbr) land here ON PURPOSE. They are referenced
            // from more than one family, and because they stay REGISTERED addresses in
            // one shared bundle, Addressables points every family bundle at them rather
            // than copying them — so "shared" costs one extra small fetch, never a
            // duplicated payload.
            return SharedFamily;
        }

        /// <summary>
        /// The text between the brackets of the top-level <c>"enemies": [ ... ]</c> array, or
        /// null when there is none. Bracket-DEPTH counted rather than regex'd, because the
        /// rows contain nested braces and a lazy `\[.*?\]` would stop at the first inner
        /// bracket and silently return a partial roster.
        /// </summary>
        public static string ExtractEnemiesArray(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            int key = json.IndexOf("\"enemies\"", StringComparison.Ordinal);
            if (key < 0) return null;

            int open = json.IndexOf('[', key);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0) return json.Substring(open + 1, i - open - 1);
                }
            }
            return null;   // unterminated array — malformed JSON, say nothing rather than guess
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";

        private static string Normalize(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Trim().ToLowerInvariant().Replace('_', '-');

        // =====================================================================
        // 4. BUILD + BUNDLE INVENTORY (the proof; settings alone prove nothing)
        // =====================================================================

        /// <summary>
        /// Builds player content and writes the resulting FILE REGISTRY with per-file
        /// sizes and per-prefix totals. The registry comes from the build result itself,
        /// not from a directory listing — ServerData/ accumulates bundles from previous
        /// days, and listing the folder would silently mix them into the total.
        /// </summary>
        public static bool BuildAndReport(string tag)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"{MarkerBuildFail} :: no AddressableAssetSettings asset.");
                return false;
            }

            AddressablesPlayerBuildResult result = null;
            try
            {
                AddressableAssetSettings.BuildPlayerContent(out result);
            }
            catch (Exception e)
            {
                Debug.LogError($"{MarkerBuildFail} :: BuildPlayerContent THREW {e.GetType().Name}: {e.Message}");
                return false;
            }

            if (result == null)
            {
                Debug.LogError($"{MarkerBuildFail} :: BuildPlayerContent returned no result object.");
                return false;
            }
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"{MarkerBuildFail} :: {result.Error}");
                return false;
            }
            if (result.LocationCount <= 0)
            {
                Debug.LogError($"{MarkerBuildFail} :: content built with ZERO locations — " +
                               "an empty catalog resolves nothing at runtime.");
                return false;
            }

            var lines = new List<string>();
            var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var countsByPrefix = new SortedDictionary<string, int>(StringComparer.Ordinal);
            long grand = 0;
            int files = 0;

            var paths = new List<string>();
            try { paths.AddRange(result.FileRegistry.GetFilePaths()); }
            catch (Exception e)
            {
                Debug.LogError($"{MarkerBuildFail} :: could not read the build FileRegistry " +
                               $"({e.GetType().Name}: {e.Message}) — the bundle inventory is the proof " +
                               "this change asked for, so a build without it is not a pass.");
                return false;
            }

            foreach (var p in paths.OrderBy(x => x, StringComparer.Ordinal))
            {
                long bytes = FileBytes(p);
                string name = Path.GetFileName(p);
                lines.Add($"{bytes,12}  {name}");
                grand += bytes;
                files++;

                string prefix = PrefixOf(name);
                totals.TryGetValue(prefix, out long t);
                totals[prefix] = t + bytes;
                countsByPrefix.TryGetValue(prefix, out int c);
                countsByPrefix[prefix] = c + 1;
            }

            lines.Sort(StringComparer.Ordinal);
            var header = new List<string>
            {
                $"# content build [{tag}]  target={EditorUserBuildSettings.activeBuildTarget}  " +
                $"locations={result.LocationCount}  duration={result.Duration:0.0}s",
                $"# outputPath={result.OutputPath}",
                "# ---- per-prefix totals ----",
            };
            foreach (var kv in totals)
                header.Add($"# {kv.Key,-28} {countsByPrefix[kv.Key],4} file(s) {kv.Value,12} bytes " +
                           $"({kv.Value / 1048576.0:0.00} MiB)");
            header.Add($"# TOTAL {files} file(s) {grand} bytes ({grand / 1048576.0:0.00} MiB)");
            header.Add("# ---- files ----");
            header.AddRange(lines);

            string path = WriteLog($"bundles-{tag}.txt", header);
            Debug.Log(string.Join("\n", header));
            Debug.Log($"{MarkerBuild} {files} files {grand} bytes " +
                      $"target={EditorUserBuildSettings.activeBuildTarget} -> {path}");
            return true;
        }

        /// <summary>Group a built filename by the bundle family it belongs to
        /// ("enemy_art", "structure_art", "gear", "catalog", ...), so the report says
        /// where the bytes went instead of only how many there were.</summary>
        private static string PrefixOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            if (name.StartsWith("catalog", StringComparison.OrdinalIgnoreCase)) return "catalog";
            if (name.StartsWith("enemy_art", StringComparison.OrdinalIgnoreCase)) return "enemy_art";
            if (name.StartsWith("enemy_models", StringComparison.OrdinalIgnoreCase)) return "enemy_models";
            if (name.StartsWith("enemy_controllers", StringComparison.OrdinalIgnoreCase)) return "enemy_controllers";
            if (name.StartsWith("enemy_textures", StringComparison.OrdinalIgnoreCase)) return "enemy_textures";
            if (name.StartsWith("structure_art", StringComparison.OrdinalIgnoreCase)) return "structure_art";
            if (name.StartsWith("gear", StringComparison.OrdinalIgnoreCase)) return "gear";
            if (name.StartsWith("dungeon", StringComparison.OrdinalIgnoreCase)) return "dungeon";
            if (name.StartsWith("localization", StringComparison.OrdinalIgnoreCase)) return "localization";
            int us = name.IndexOf('_');
            return us > 0 ? name.Substring(0, us) : name;
        }

        // =====================================================================
        // helpers
        // =====================================================================

        private static long FileBytes(string assetOrFilePath)
        {
            if (string.IsNullOrEmpty(assetOrFilePath)) return 0;
            try
            {
                var fi = new FileInfo(assetOrFilePath);
                return fi.Exists ? fi.Length : 0;
            }
            catch { return 0; }
        }

        private static string WriteLog(string fileName, IEnumerable<string> lines)
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), LogDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllLines(path, lines.ToArray());
            return path.Replace('\\', '/');
        }
    }
}
