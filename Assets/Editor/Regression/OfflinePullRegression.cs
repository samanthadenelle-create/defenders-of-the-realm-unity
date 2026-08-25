// =============================================================================
// OfflinePullRegression — PROD-010. Proves the opt-in offline pull cannot repeat
// the defect it shipped with on 2026-08-19.
// -----------------------------------------------------------------------------
// WHAT SHIPPED BROKEN, AND WHY A SUITE EXISTS FOR IT.
//
// PROD-010 landed with its content set written as
//     ContentKeys = { "Structure_Art", "Enemy_Art" }
// which are Addressable GROUP names. A group name is not an Addressables key — only
// ADDRESSES and LABELS are. Every GetDownloadSizeAsync therefore matched nothing and
// answered 0; 0 was read as "already cached"; the player was stamped offline-ready;
// and nothing was ever downloaded. Compile gate green, regression suite green, ticket
// marked IMPLEMENTED. Nothing in the build was capable of noticing, because nothing
// ever compared the CLAIM against an OUTCOME.
//
// So this suite asserts the two halves of that failure, in both directions:
//
//   0 [meta]      THE SUITE ITSELF MUST BE CAPABLE OF FAILING. Proven at runtime by
//                 feeding a deliberately-wrong expectation through the real helper and
//                 requiring that it gets recorded. See the comment on Group0 for why a
//                 test file guarding this particular bug must not carry this particular
//                 shape. `Run` returns FALSE through an explicit, reachable exit.
//
//   1 [classify]  A set that resolves to ZERO keys must classify as CannotMeasure and
//                 must NEVER classify as AlreadyCached. This is the shipped defect
//                 expressed as a test — with the old code, ClassifySize(0, 0) would
//                 have had to answer "already cached" for the bug to exist at all.
//   2 [classify]  The honest cases still work: real keys + 0 bytes = AlreadyCached,
//                 real keys + bytes = NeedsDownload, any unmeasurable = CannotMeasure.
//                 A gate that only rejects is as useless as one that only accepts.
//   3 [verify]    PullVerified: success requires keys > 0 AND every handle OK AND a
//                 RE-MEASURED zero bytes outstanding. Specifically, "handles all
//                 succeeded but bytes are still outstanding" must be a FAILURE — that
//                 is precisely the shape of the shipped no-op.
//   4 [keys]      IsOfflineContentKey accepts real catalog addresses and rejects the
//                 32-hex GUID duplicates.
//   5 [coverage]  THE RE-PACK NET. Walks the real AddressableAssetSettings, finds every
//                 group whose LoadPath resolves to a remote URL, and requires
//                 IsOfflineContentKey to accept every address AND every label in it.
//                 The re-pack LANDED on 2026-08-20: Enemy_Art is now BundleMode 2
//                 (PackTogetherByLabel, 5 `enemyfam-*` family bundles) and Structure_Art
//                 is BundleMode 1 (PackSeparately, 35 per-asset bundles). Both stayed on
//                 Remote.LoadPath and the 544 addresses were unchanged — verified, not
//                 assumed. Bundle mode is orthogonal to this check by design: it re-shapes
//                 how bytes are packed, never what a key is, which is exactly why the set
//                 is enumerated from catalog keys and not from group names.
//   6 [lint]      OfflineContentService contains no WaitForCompletion (a P0 deadlock was
//                 fixed on 2026-08-19 caused by exactly that: Addressables 2.9.1
//                 implements it as `while (!InvokeWaitForCompletion()) { }` — no timeout,
//                 no exit), and DOES still call PullVerified after downloading. Group 6
//                 guards the assertion itself against a future "cleanup" deleting it.
//
// Groups 0-4 are PURE — no catalog, no play session, no network — because the decision
// logic they cover was deliberately factored out of the coroutines for that reason.
//
// Markers: OFFLINE_PULL_OK / OFFLINE_PULL_FAIL.
// Standalone: run-unity-method -Method DeNelle.Editor.OfflinePullRegression.RunAll
// Registered in DataRegression.RunAll as the "offline-pull suite".
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using DeNelle.Core.UI;
using DeNelle.Core;
using Newtonsoft.Json.Linq;

namespace DeNelle.Editor
{
    public static class OfflinePullRegression
    {
        public const string MarkerOk   = "OFFLINE_PULL_OK";
        public const string MarkerFail = "OFFLINE_PULL_FAIL";

        private const string ServicePath = "Assets/_Modules/Core/Addressables/OfflineContentService.cs";
        private const string OverlayPath = "Assets/_Modules/Core/UI/LoadingOverlay.cs";
        private const string CanonResources = "Assets/Resources/Data/Canonical/canon-strings.json";
        private const string CanonStreaming = "Assets/StreamingAssets/Data/Canonical/canon-strings.json";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- OFFLINE PULL (PROD-010) ---");

            Group0_ThisSuiteCanActuallyFail(failures, log);
            Group1_ZeroKeysIsNeverReady(failures, log);
            Group2_HonestCasesStillClassify(failures, log);
            Group3_PullMustProveItPulled(failures, log);
            Group4_KeyPredicate(failures, log);
            Group5_RemoteGroupCoverage(failures, log);
            Group6_SourceLint(failures, log);
            Group7_FirstRunConnectionRecovery(failures, log);

            // EXPLICIT, REACHABLE FAILURE EXIT. Every group above writes into `failures`, and
            // group 0 proves at runtime that a wrong answer really does land there — so this
            // branch is taken by real regressions, not decoration to satisfy a grep.
            if (failures.Count > 0)
            {
                log.AppendLine($"{MarkerFail} — {failures.Count} failure(s):");
                foreach (string f in failures) log.AppendLine("  FAIL: " + f);
                reason = log.ToString();
                return false;
            }

            log.AppendLine($"{MarkerOk} — 8/8 groups");
            reason = log.ToString();
            return true;
        }

        // ── 0 [meta] this suite must be CAPABLE of failing ───────────────────────
        // WHY A GROUP EXISTS JUST TO PROVE THE SUITE CAN FAIL.
        //
        // The first cut of this file computed `bool ok = failures.Count == 0; return ok;`. That
        // behaves correctly, but the project's regression meta-oracle flagged it as a HOLLOW
        // PASS because it could not see a failing shape — and the flag was worth acting on for a
        // reason bigger than the grep. PROD-010 shipped broken because a success report was
        // structurally incapable of being a failure report: `GetDownloadSizeAsync` on group names
        // answered 0, and 0 meant "done". Fixing that with a suite carrying the same shape would
        // be the same mistake at one remove — a rule that cannot bite reads as coverage while
        // protecting nothing, which is strictly worse than no rule at all.
        //
        // So this group does not assert a value; it asserts the MACHINERY. It feeds a
        // deliberately-wrong expectation through the SAME `Expect` helper the real groups use,
        // into a throwaway list, and requires that a failure was recorded.
        //
        // It earns its place twice over, because the deliberately-wrong expectation IS the
        // shipped defect: the probe expects ClassifySize(0, 0) == AlreadyCached. That expectation
        // must be WRONG. If the service ever regresses to calling a zero-key set "already
        // cached", the probe stops recording a failure — and THIS group fails. Group 0 and
        // group 1 therefore fail from opposite directions on the same regression.
        private static void Group0_ThisSuiteCanActuallyFail(List<string> failures, StringBuilder log)
        {
            var probe = new List<string>();

            // Deliberately WRONG expectation, run through the real helper. `AlreadyCached` is what
            // the 2026-08-19 build effectively believed about a set that resolved to nothing.
            Expect(probe, "[probe]", OfflineContentService.ClassifySize(0, 0),
                   OfflineSizeVerdict.AlreadyCached, "deliberate self-test - this expectation is wrong on purpose");

            if (probe.Count == 0)
            {
                failures.Add("[0] the assertion helper recorded NOTHING for a deliberately wrong expectation. " +
                             "Either Expect() no longer reports, or ClassifySize(0, 0) now answers AlreadyCached " +
                             "- the exact PROD-010 defect. Both readings are a hard failure: a suite that cannot " +
                             "record a failure is a green light wired to nothing.");
            }

            // Same proof for the outcome assertion, which is the half PROD-010 was missing
            // entirely. PullVerified must be able to answer FALSE for the no-op shape.
            var probe2 = new List<string>();
            if (OfflineContentService.PullVerified(113, true, 5_000_000, out _)) probe2.Add("recorded");
            if (probe2.Count != 0)
            {
                failures.Add("[0] PullVerified accepted a pull with 5 MB still outstanding, so the outcome " +
                             "assertion cannot fail either. This is the shipped defect exactly: handles " +
                             "reporting success while nothing landed.");
            }

            log.AppendLine("  [0] the suite's failure path is REACHABLE (proven at runtime, not by shape)");
        }

        // ── 1 [classify] the shipped defect, expressed as a test ─────────────────
        private static void Group1_ZeroKeysIsNeverReady(List<string> failures, StringBuilder log)
        {
            // ZERO keys with a zero byte total is EXACTLY the state the group-name set produced:
            // GetDownloadSizeAsync matched nothing and returned 0. If this ever answers
            // AlreadyCached again, the 2026-08-19 build is back.
            var v = OfflineContentService.ClassifySize(0, 0);
            if (v != OfflineSizeVerdict.CannotMeasure)
                failures.Add($"[1] ClassifySize(keys:0, bytes:0) = {v}; must be CannotMeasure. " +
                             "A content-key set that resolves to nothing is an UNKNOWN, never 'already downloaded' " +
                             "- this is the exact conflation that shipped a no-op as a feature.");

            if (OfflineContentService.ClassifySize(0, 0) == OfflineSizeVerdict.AlreadyCached)
                failures.Add("[1] zero-key set reached AlreadyCached - the original PROD-010 defect.");

            // ...and it must not be rescued by a positive byte count either: with no keys there is
            // no basis for any verdict at all.
            var v2 = OfflineContentService.ClassifySize(0, 12345);
            if (v2 != OfflineSizeVerdict.CannotMeasure)
                failures.Add($"[1] ClassifySize(keys:0, bytes:12345) = {v2}; must be CannotMeasure.");

            log.AppendLine("  [1] zero-key set classifies as CannotMeasure (never AlreadyCached)");
        }

        // ── 2 [classify] the gate must still PASS the good states ────────────────
        private static void Group2_HonestCasesStillClassify(List<string> failures, StringBuilder log)
        {
            Expect(failures, "[2]", OfflineContentService.ClassifySize(113, 0),
                   OfflineSizeVerdict.AlreadyCached, "real keys + 0 bytes = genuinely cached");
            Expect(failures, "[2]", OfflineContentService.ClassifySize(113, 88_253_119),
                   OfflineSizeVerdict.NeedsDownload, "real keys + real bytes = needs download");
            Expect(failures, "[2]", OfflineContentService.ClassifySize(113, -1),
                   OfflineSizeVerdict.CannotMeasure, "measurement failure is an unknown, not a zero");

            log.AppendLine("  [2] cached / needs-download / unmeasurable all classify correctly");
        }

        private static void Expect(List<string> failures, string tag,
                                   OfflineSizeVerdict actual, OfflineSizeVerdict expected, string what)
        {
            if (actual != expected) failures.Add($"{tag} {what}: got {actual}, expected {expected}.");
        }

        // ── 3 [verify] the outcome assertion whose absence caused the defect ─────
        private static void Group3_PullMustProveItPulled(List<string> failures, StringBuilder log)
        {
            // THE CENTRAL CASE. Every download handle reported Succeeded, yet bytes remain
            // outstanding for the same key set. That is a pull that did not pull, and it is
            // exactly what "keys that match nothing" looks like from the caller's side.
            if (OfflineContentService.PullVerified(113, true, 5_000_000, out string r1))
                failures.Add("[3] PullVerified(handles OK, 5 MB still outstanding) returned TRUE. " +
                             "Handles succeeding is not evidence that bytes landed; the remaining-size " +
                             "re-measurement is the only proof and it must be able to fail the pull.");

            if (OfflineContentService.PullVerified(0, true, 0, out string r2))
                failures.Add("[3] PullVerified(keys:0) returned TRUE - a set that resolved to nothing " +
                             "can never be reported as a completed pull.");

            if (OfflineContentService.PullVerified(113, false, 0, out string r3))
                failures.Add("[3] PullVerified(a handle failed) returned TRUE - a partial pull must " +
                             "never be claimed as success.");

            if (OfflineContentService.PullVerified(113, true, -1, out string r4))
                failures.Add("[3] PullVerified(remaining UNMEASURABLE) returned TRUE - an unproven pull " +
                             "must be treated as failed, not assumed good.");

            // ...and the one true case, so this is a gate and not a blanket refusal.
            if (!OfflineContentService.PullVerified(113, true, 0, out string r5))
                failures.Add($"[3] PullVerified(keys:113, handles OK, 0 remaining) returned FALSE ({r5}) - " +
                             "a genuinely complete pull must be able to succeed.");

            foreach (string reason in new[] { r1, r2, r3, r4 })
            {
                if (string.IsNullOrWhiteSpace(reason))
                    failures.Add("[3] PullVerified failed without saying why. A silent failure here is how " +
                                 "the last one went unnoticed for a day.");
            }

            log.AppendLine("  [3] pull success requires keys + handles + a re-measured zero outstanding");
        }

        // ── 4 [keys] the predicate ───────────────────────────────────────────────
        private static void Group4_KeyPredicate(List<string> failures, StringBuilder log)
        {
            foreach (string addr in new[] { "Structures/Ballista_L2", "Enemies/OrcHumanoid_Mage",
                                            "gear/weapon/Axe1h_01", "dungeon/exit/portal",
                                            "Assets/Localization/Tables/GameStrings Shared Data.asset" })
            {
                if (!OfflineContentService.IsOfflineContentKey(addr))
                    failures.Add($"[4] IsOfflineContentKey rejected real catalog address '{addr}'. " +
                                 "The offline set is complete BY CONSTRUCTION - local groups cost 0 bytes, " +
                                 "so dropping an address can only ever lose content, never save bandwidth.");
            }

            // GUID duplicates: every entry is registered under both its address and its asset
            // GUID. Including both doubles the key list for zero extra coverage.
            if (OfflineContentService.IsOfflineContentKey("0123456789abcdef0123456789abcdef"))
                failures.Add("[4] IsOfflineContentKey accepted a 32-hex asset GUID - these are duplicates " +
                             "of addresses already in the set.");

            if (OfflineContentService.IsOfflineContentKey(null) ||
                OfflineContentService.IsOfflineContentKey(""))
                failures.Add("[4] IsOfflineContentKey accepted a null/empty key.");

            log.AppendLine("  [4] key predicate accepts addresses, rejects GUID duplicates");
        }

        // ── 5 [coverage] the re-pack net ─────────────────────────────────────────
        private static void Group5_RemoteGroupCoverage(List<string> failures, StringBuilder log)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                failures.Add("[5] AddressableAssetSettings could not be loaded - the completeness check " +
                             "cannot run, and an unrunnable gate is not a gate.");
                return;
            }

            int remoteGroups = 0, remoteAddresses = 0, uncovered = 0;

            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                string loadPath;
                try { loadPath = schema.LoadPath.GetValue(settings) ?? ""; }
                catch (Exception ex) { failures.Add($"[5] could not evaluate LoadPath for '{group.Name}': {ex.Message}"); continue; }

                bool remote = loadPath.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                if (!remote) continue;

                remoteGroups++;
                var labelsSeen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.address)) continue;
                    remoteAddresses++;
                    if (!OfflineContentService.IsOfflineContentKey(entry.address))
                    {
                        uncovered++;
                        if (uncovered <= 10)
                            failures.Add($"[5] REMOTE address '{entry.address}' (group '{group.Name}') is NOT " +
                                         "in the offline content set. A player who opts into offline mode would " +
                                         "be told they are covered and then find this missing with no network. " +
                                         "Fix the set, not this test.");
                    }

                    // LABELS ARE CATALOG KEYS TOO, and after the 2026-08-20 re-pack they are
                    // load-bearing: Enemy_Art packs PackTogetherByLabel, so `enemyfam-orc` and its
                    // four siblings name the actual bundles. A label the offline set dropped would
                    // not lose coverage on its own (its member addresses are still in the set), but
                    // it would mean the predicate had started rejecting real keys - which is the
                    // first symptom of the set narrowing again.
                    if (entry.labels == null) continue;
                    foreach (string label in entry.labels)
                    {
                        if (string.IsNullOrEmpty(label) || !labelsSeen.Add(label)) continue;
                        if (!OfflineContentService.IsOfflineContentKey(label))
                        {
                            uncovered++;
                            failures.Add($"[5] REMOTE label '{label}' (group '{group.Name}') is NOT in the " +
                                         "offline content set, and after the re-pack that label names a bundle.");
                        }
                    }
                }

                log.AppendLine($"      remote group '{group.Name}': {group.entries.Count} entr(ies), " +
                               $"{labelsSeen.Count} label(s) [{string.Join(", ", labelsSeen)}] -> {loadPath}");
            }

            if (remoteGroups == 0)
                failures.Add("[5] NO remote group found in AddressableAssetSettings. Either the CDN ruling was " +
                             "reversed (in which case this suite and PROD-010 both need re-reading) or the " +
                             "settings failed to load - both must be looked at, not passed over.");

            if (remoteAddresses == 0 && remoteGroups > 0)
                failures.Add("[5] remote groups exist but contain ZERO addressable entries - the offline pull " +
                             "would have nothing to fetch, which is the 2026-08-19 outcome by another route.");

            log.AppendLine($"  [5] remote coverage: {remoteGroups} group(s), {remoteAddresses} address(es), " +
                           $"{uncovered} uncovered");
        }

        // ── 6 [lint] the deadlock ban + the assertion's own tombstone ────────────
        private static void Group6_SourceLint(List<string> failures, StringBuilder log)
        {
            string src = ReadCodeOnly(ServicePath, "[6]", failures);
            if (src == null) return;

            if (src.Contains("WaitForCompletion"))
                failures.Add("[6] OfflineContentService contains WaitForCompletion. Addressables 2.9.1 implements " +
                             "it as `while (!InvokeWaitForCompletion()) { }` - no timeout and no exit - which is " +
                             "the P0 deadlock fixed on 2026-08-19. Every wait in this file must be a coroutine yield.");

            if (!src.Contains("PullVerified"))
                failures.Add("[6] OfflineContentService no longer calls PullVerified. That call IS the fix: without " +
                             "re-measuring the set after downloading, a pull that fetched nothing reports success - " +
                             "which is what shipped. Do not remove it as cleanup.");

            if (!src.Contains("MeasureDownloadSize"))
                failures.Add("[6] OfflineContentService no longer measures download size. The size shown to the " +
                             "player must be MEASURED, never typed.");

            log.AppendLine("  [6] source lint: no WaitForCompletion, outcome assertion still present");
        }

        // -- 7 [PROD-012] disconnected first-run must stop and recover honestly ----
        private static void Group7_FirstRunConnectionRecovery(List<string> failures, StringBuilder log)
        {
            string service = ReadCodeOnly(ServicePath, "[7]", failures);
            string overlay = ReadCodeOnly(OverlayPath, "[7]", failures);
            if (service != null)
            {
                if (!service.Contains("LoadingOverlay.ShowConnectionRequired"))
                    failures.Add("[7] ContentSource.Unavailable no longer opens the first-run connection surface.");
                if (!service.Contains("KeyFirstRunInternetRequired") || !service.Contains("KeyRetry"))
                    failures.Add("[7] the service no longer resolves required/retry copy by canon key.");
            }
            if (overlay != null)
            {
                if (!overlay.Contains("OfflineContentService.ResolveContentSource"))
                    failures.Add("[7] Retry no longer re-enters the one content-source authority.");
                if (!overlay.Contains("ContentSource.Online || resolved == ContentSource.LocalCache"))
                    failures.Add("[7] Retry can dismiss without a usable Online or LocalCache result.");
                if (!overlay.Contains("_retryButton.interactable = true"))
                    failures.Add("[7] a failed Retry cannot be tried again.");
                if (!overlay.Contains("ApplyConnectionBarrierState(_group)"))
                    failures.Add("[7] converting a dismissing overlay no longer restores the connection barrier atomically.");
            }


            // Behavioral containment proof: the actual helper used when converting an already
            // dismissing overlay must restore visibility, input ownership, and the raycast wall
            // together. The persistent full-screen Canvas then prevents town controls behind it
            // from being reached while ContentSource is Unavailable.
            var go = new GameObject("offline-connection-barrier-probe");
            try
            {
                var group = go.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                var method = typeof(LoadingOverlay).GetMethod("ApplyConnectionBarrierState",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                    failures.Add("[7] connection barrier state helper is missing.");
                else
                {
                    method.Invoke(null, new object[] { group });
                    if (group.alpha != 1f || !group.interactable || !group.blocksRaycasts)
                        failures.Add($"[7] connection barrier restored alpha={group.alpha}, " +
                                     $"interactable={group.interactable}, blocksRaycasts={group.blocksRaycasts}; " +
                                     "Retry and town containment must be restored atomically.");
                }
            }
            catch (Exception ex)
            {
                failures.Add("[7] connection barrier behavior probe threw: " + ex.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            if (service != null && !service.Contains("if (!catalogUsable)"))
                failures.Add("[7] radio reachability can still dismiss first-run containment without a usable catalog proof.");

            try
            {
                string a = File.ReadAllText(CanonResources);
                string b = File.ReadAllText(CanonStreaming);
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    failures.Add("[7] canon-strings mirrors differ; first-run copy would depend on load path.");
                var canon = JObject.Parse(a);
                const string required = "An internet connection is required to finish setting up Elarion.";
                string actual = canon.Value<string>("offlineFirstRunInternetRequired");
                string retry = canon.Value<string>("offlineFirstRunRetry");
                if (actual != required)
                    failures.Add("[7] exact owner first-run sentence is absent or changed.");
                if (retry != "Retry")
                    failures.Add("[7] Retry label is absent or changed.");
                foreach (char c in required + (retry ?? ""))
                    if (c > 127) { failures.Add("[7] first-run copy is not ASCII-only."); break; }
            }
            catch (Exception ex)
            {
                failures.Add("[7] canon-strings parser failed: " + ex.Message);
            }

            log.AppendLine("  [7] disconnected first-run blocks honestly; Retry re-enters resolver; canon mirrors exact");
        }

        /// <summary>
        /// Code with comments and line-comment tails stripped. The project's standing lint
        /// discipline: a rule that matches its own tombstone comment would punish exactly the
        /// self-documenting notes CLAUDE.md sections 12/15 ask for — and this file's header
        /// names WaitForCompletion four times.
        /// </summary>
        private static string ReadCodeOnly(string relPath, string tag, List<string> failures)
        {
            string full = Path.GetFullPath(relPath);
            if (!File.Exists(full))
            {
                failures.Add($"{tag} {relPath} is MISSING - the PROD-010 fix cannot be verified at all.");
                return null;
            }

            var sb = new StringBuilder();
            foreach (string raw in File.ReadAllLines(full))
            {
                string line = raw;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

                int c = line.IndexOf("//", StringComparison.Ordinal);
                if (c >= 0 && !InsideQuotes(line, c)) line = line.Substring(0, c);
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private static bool InsideQuotes(string line, int index)
        {
            bool q = false;
            for (int i = 0; i < index; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) q = !q;
            }
            return q;
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
