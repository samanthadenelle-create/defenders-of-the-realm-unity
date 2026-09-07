// =============================================================================
// AssetMoveManifestRegression — proves the generated move-manifest still
// describes reality.
// -----------------------------------------------------------------------------
// OWNER DESIGN (2026-08-17): "all we do is tag a file with what we moved as a
// json so we store its path". The manifest replaces ~14 hardcoded
// DeNelle.Core.AssetRoots.StructureContent strings with one lookup, and makes the move
// reversible and auditable.
//
// ⛔ THIS SUITE IS THE ENTIRE REASON THE MANIFEST IS SAFE TO RELY ON.
// A path list that nothing verifies is a SECOND SOURCE OF TRUTH, and this project
// was burned by exactly that class FOUR TIMES IN A SINGLE DAY:
//   • .tripo-extracted markers that outlived the FBX they described, so replaced
//     models silently skipped extraction;
//   • PetForwardYaw = -90, correct for the mesh it was written for and wrong the
//     moment the body changed;
//   • a WO-number banner copied into a second doc and left to rot;
//   • visualPrefabPath "Structures/WizardTower_1" — an art path that outlived its
//     art and shipped a wizard tower as the Ballista to real players.
// Every one was true when written. The manifest will go stale the same way — the
// difference is that this suite makes it FAIL A GATE instead of blanking a
// building in front of a live player.
//
// GUID-KEYED, NOT PATH-KEYED, on purpose: AssetDatabase.MoveAsset preserves GUIDs,
// so if an asset is moved again behind the manifest's back the GUID still resolves
// — to a DIFFERENT path. That mismatch is detectable. A path-keyed check would
// simply fail to find the file and could not tell "moved" from "deleted".
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>Verifies every entry in the generated asset-move manifest still resolves.</summary>
    public static class AssetMoveManifestRegression
    {
        // Editor-only data, deliberately NOT under Resources — see the note in
        // StructureAddressablesMigrator. Keeping it out keeps it out of every player build.
        private const string ManifestPath = "Assets/AddressableAssetsData/asset-move-manifest.json";
        private const string OkMarker     = "MOVE_MANIFEST_OK";

        [MenuItem("Defenders/Regression/Verify asset-move manifest")]
        public static void RunMenu() => Run();

        /// <summary>Batchmode entry. Emits MOVE_MANIFEST_OK or MOVE_MANIFEST_FAIL.</summary>
        public static void Run()
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            Verify(failures, log);

            Debug.Log(log.ToString());
            if (failures.Count == 0)
            {
                Debug.Log($"{OkMarker}");
                return;
            }
            foreach (var f in failures) Debug.LogError("[move-manifest] " + f);
            Debug.LogError($"MOVE_MANIFEST_FAIL: {failures.Count} failure(s)");
        }

        /// <summary>
        /// Registered-suite entry point (DataRegression.RunAll, WO-1496). Verify() used to be
        /// called directly, and from ABOVE the START FENCE: it ran, but uncounted — its
        /// [move-manifest] line landed in the pre-fence baseline and no `.Run(out` call-site
        /// existed for RegressionMarkerRegression's denominator to pin. This overload gives it
        /// the same shape as every other registered suite, so it is counted like one.
        /// </summary>
        public static bool Run(out string reason)
        {
            // WO-1496: the whole-suite stand-down is decided BEFORE Verify, on the one condition
            // that makes it stand down. Reporting green because a fresh clone has no manifest is
            // the exact arithmetic this project's third state exists to end.
            if (!System.IO.File.Exists(ManifestPath))
                return RegressionOutcome.Skip(out reason, "MOVE MANIFEST",
                    "no manifest at " + ManifestPath + " — no Addressables migration has run in this tree");

            var failures = new List<string>();
            var log = new StringBuilder();
            Verify(failures, log);

            if (failures.Count == 0)
            {
                reason = "MOVE MANIFEST OK — " + log.ToString().Replace("\r", " ").Replace("\n", " ").Trim();
                return true;
            }
            reason = "MOVE MANIFEST: " + failures.Count + " failure(s): " + string.Join(" | ", failures.ToArray());
            return false;
        }

        /// <summary>
        /// The verification body, shared by the standalone marker path and the registered
        /// Run(out string) above.
        /// </summary>
        public static void Verify(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[move-manifest] generated asset-move manifest:");

            if (!System.IO.File.Exists(ManifestPath))
            {
                // ABSENT IS NOT A FAILURE. No migration has run yet on a fresh clone, and failing
                // here would make every developer's first gate red for a file they never needed.
                // WO-1496: it is not a PASS either, and until now it read as one — a bare `return`
                // out of a void section, into a caller whose only channel is a bool. DECLARED as a
                // stand-down instead, so the run says it asserted nothing rather than implying it did.
                log.AppendLine("  " + RegressionOutcome.PartialSkip(
                    "[move-manifest] manifest verification",
                    "manifest absent at " + ManifestPath + " — no migration has run (a fresh clone is " +
                    "the normal case); nothing to verify, and nothing verified"));
                return;
            }

            string json = System.IO.File.ReadAllText(ManifestPath);

            // Hand-edit tripwire: the writer always stamps _generated. Its absence means someone
            // edited this by hand, which is precisely what the design forbids.
            if (!json.Contains("\"_generated\""))
            {
                failures.Add("manifest has no '_generated' stamp — it looks HAND-EDITED. This file is " +
                             "written by StructureAddressablesMigrator and must never be edited by hand; " +
                             "regenerate it instead.");
                return;
            }

            // FOLDER MODE: the migration was performed as a single AssetDatabase folder move, so
            // there is one fact to verify — the tree is at the new root and NOT at the old one.
            // Per-GUID checks do not apply (a folder record carries no asset GUID), and inventing
            // per-file entries for a move that was not performed per-file would be exactly the
            // second-source-of-truth problem this suite exists to catch.
            if (json.Contains("LIVE_FOLDER_MOVE"))
            {
                string from = Extract(json, "\"from\"\\s*:\\s*\"([^\"]+)\"");
                string to   = Extract(json, "\"to\"\\s*:\\s*\"([^\"]+)\"");

                if (!string.IsNullOrEmpty(to) && !AssetDatabase.IsValidFolder(to))
                    failures.Add($"folder move target '{to}' does not exist — the migrated art is gone.");

                // The one that actually matters: anything back under Resources is FORCE-INCLUDED in
                // every build again, silently undoing the migration while it still looks done.
                if (!string.IsNullOrEmpty(from) && AssetDatabase.IsValidFolder(from))
                    failures.Add($"'{from}' EXISTS AGAIN — structure art is back under Resources and " +
                                 "force-included in every build. An importer with a stale destination " +
                                 "is the usual cause; repoint it rather than deleting the folder.");

                if (failures.Count == 0)
                    log.AppendLine($"  folder move verified: '{from}' -> '{to}', nothing back under Resources.");
                return;
            }

            bool dryRun = json.Contains("DRY_RUN");
            var entries = ParseEntries(json);
            if (entries.Count == 0)
            {
                failures.Add("manifest parsed to ZERO entries but the file exists — the writer or this " +
                             "parser is broken; either way the manifest cannot be trusted.");
                return;
            }

            int okCount = 0, resourcesStill = 0;
            foreach (var e in entries)
            {
                string resolved = AssetDatabase.GUIDToAssetPath(e.guid);

                // (a) GUID no longer resolves -> the asset was DELETED.
                if (string.IsNullOrEmpty(resolved))
                {
                    failures.Add($"'{e.address}' guid {e.guid} resolves to NOTHING — the asset was deleted " +
                                 $"after the manifest was written (expected at '{e.to}').");
                    continue;
                }

                if (dryRun)
                {
                    // In DRY RUN nothing moved, so the asset must still be at 'from'. Verifying the
                    // pre-state matters: it proves the manifest describes assets that actually exist
                    // before anyone runs the live move against it.
                    if (!PathsEqual(resolved, e.from))
                        failures.Add($"[dry-run] '{e.address}' guid {e.guid} is at '{resolved}' but the manifest " +
                                     $"recorded it at '{e.from}'. The manifest is already stale — REGENERATE " +
                                     "before running the live move.");
                    else okCount++;

                    if (resolved.Contains("/Resources/")) resourcesStill++;
                    continue;
                }

                // (b) LIVE: the asset must be where the manifest says it went.
                if (!PathsEqual(resolved, e.to))
                {
                    failures.Add($"'{e.address}' guid {e.guid} is at '{resolved}' but the manifest says '{e.to}' — " +
                                 "the asset was moved again behind the manifest's back. Regenerate it.");
                    continue;
                }

                // (c) THE ONE THAT MATTERS MOST: it came back into Resources. Anything under a
                // Resources folder is force-included in every build, so this silently re-inflates
                // the payload while the migration still looks done.
                if (resolved.Contains("/Resources/"))
                {
                    failures.Add($"'{e.address}' is back under Resources at '{resolved}' — it is FORCE-INCLUDED " +
                                 "in every build again. An importer with a stale destination is the usual cause.");
                    continue;
                }

                okCount++;
            }

            log.AppendLine($"  {entries.Count} entr(ies), {okCount} verified" +
                           (dryRun ? $", DRY-RUN (still in Resources: {resourcesStill})" : ""));

            if (failures.Count == 0)
                log.AppendLine($"  {OkMarker} {entries.Count} entries");
        }

        /// <summary>First regex capture, or empty. Keeps the folder-mode check dependency-free.</summary>
        private static string Extract(string text, string pattern)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, pattern);
            return m.Success ? m.Groups[1].Value : "";
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(a?.Replace('\\', '/'), b?.Replace('\\', '/'),
                                 System.StringComparison.OrdinalIgnoreCase);
        }

        private struct Entry { public string address, from, to, guid; }

        /// <summary>
        /// Text-scan rather than a typed deserialize, so a schema addition cannot break the gate.
        /// Same reasoning as TripoStructureMaterialAudit.VerifyCatalogArt.
        /// </summary>
        private static List<Entry> ParseEntries(string json)
        {
            var list = new List<Entry>();
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(
                         json, "\\{\\s*\"address\"\\s*:\\s*\"([^\"]+)\".*?\"from\"\\s*:\\s*\"([^\"]+)\"" +
                               ".*?\"to\"\\s*:\\s*\"([^\"]+)\".*?\"guid\"\\s*:\\s*\"([^\"]*)\"",
                         System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                list.Add(new Entry
                {
                    address = m.Groups[1].Value,
                    from    = m.Groups[2].Value,
                    to      = m.Groups[3].Value,
                    guid    = m.Groups[4].Value,
                });
            }
            return list;
        }
    }
}
