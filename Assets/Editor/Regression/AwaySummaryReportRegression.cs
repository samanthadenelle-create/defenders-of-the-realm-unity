// =============================================================================
// AwaySummaryReportRegression -- LANE G: the returning session actually reports.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Entry point: Run(out string reason)
//
// THE ARROW THIS PINS: Repeat -> Collect. Economy map
// docs/PROGRAM_RAID_ECONOMY_2026-09-04.md sec.7 opens the ideal returning session on two
// beats:
//     BUILD COMPLETE            -> collect
//     Resources full            -> collect
//
// THE MEASURED RED (read at source on the pre-change tree, 2026-09-04):
//   * WelcomeBackPopup.cs:19 --
//         if (result == null || (result.Total <= 0 && !result.HasMendNews)) return;
//     so a player who finished three builds overnight and accrued NO node harvest was told
//     NOTHING. A collector-only town scored zero on both terms of that gate.
//   * WelcomeBackPopup.cs:74 -- the COLLECT button's onClick was `Dismiss`. A button
//     labelled with a verb that performed no verb.
//
// PRECISION, deliberately not overclaimed: an UPGRADE-kind job ALREADY applies its level on
// the offline sweep through CompletedUpgradeApplier, and where the structure is spawned the
// player does see it. The real holes are new-construction completions, upgrades whose
// structure has not spawned, and the total absence of an AGGREGATE.
//
// WHAT THIS SUITE ASSERTS
//   1. [gate-jobs-and-collectors]  a result with ZERO haul, NO mend, ONE finished job and a
//      pending collector reports HasSummaryContent == true. <- THE ACCEPTANCE CASE.
//   2. [gate-still-silent-when-empty]  a wholly empty result still reports false, so case 1
//      was not bought by making the gate always-true.
//   3. [gate-mend-only-survives]  the WO-1231 mend-only reveal is not regressed.
//   4. [popup-reads-the-one-gate]  WelcomeBackPopup.Show gates on HasSummaryContent and the
//      retired two-term expression is GONE (a second copy of the gate is the defect).
//   5. [collect-button-performs-its-verb]  the button is wired to CollectAndDismiss, which
//      carries the tap to the EXISTING CollectorStatusGate.RequestCollectAll -- and there is
//      still exactly ONE modal built in that file (no second return-time popup).
//   6. [service-records-and-never-banks]  OfflineHarvestService listens on the ONE completion
//      seam (BuildTimerService.JobCompleted), reads the collector registry, and never calls
//      Collect() -- the away claim must not become a second route to the wallet.
//   7. [rows-are-ascii]  every new player-facing row literal is ASCII.
//
// Cases 1-3 drive the real DeNelle.Village.OfflineHarvestResult type. Cases 4-7 are source
// assertions, which is the right instrument for "the wiring is present": the modal cannot be
// constructed in editmode batchmode (no canvas/PanelSettings), and asserting on a screenshot
// is not available to a data oracle.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>Headless oracle for the away summary's four-axis reveal gate + COLLECT wiring.</summary>
    public static class AwaySummaryReportRegression
    {
        private const string PopupSrc = "Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs";
        private const string ServiceSrc = "Assets/_Modules/Village/Harvest/OfflineHarvestService.cs";
        private const string ResultSrc = "Assets/_Modules/Village/Harvest/OfflineHarvestResult.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            try
            {
                // ── 1. THE ACCEPTANCE CASE (this is the red) ──────────────────
                // Zero node accrual, one finished build, one pending collector.
                var collectorOnly = new OfflineHarvestResult
                {
                    AwaySeconds = 8.0 * 3600.0,
                    PendingCollectorTotal = 1_240,
                    PendingCollectorCount = 1,
                };
                collectorOnly.CompletedJobs.Add(new OfflineHarvestResult.OfflineJobLine
                {
                    Verb = "BUILD",
                    Label = "Barracks",
                    FinishedUnixMs = 1_000_000.0,
                });

                if (collectorOnly.Total != 0)
                    failures.Add($"case1 setup is wrong: the acceptance case must have ZERO haul (Total={collectorOnly.Total})");
                if (collectorOnly.HasMendNews)
                    failures.Add("case1 setup is wrong: the acceptance case must carry NO mend news");
                if (!collectorOnly.HasJobNews)
                    failures.Add("case1 [gate-jobs-and-collectors] one finished job did not register as job news");
                if (!collectorOnly.HasCollectorNews)
                    failures.Add("case1 [gate-jobs-and-collectors] 1240 waiting in a collector did not register as collector news");
                if (!collectorOnly.HasSummaryContent)
                    failures.Add("case1 [gate-jobs-and-collectors] ZERO haul + one finished build + one pending collector " +
                                 "still reports HasSummaryContent=false -- the returning player is told NOTHING, which is the " +
                                 "measured defect (WelcomeBackPopup.cs:19, 'result.Total <= 0 && !result.HasMendNews')");

                // ── 2. the gate did not become always-true ────────────────────
                var empty = new OfflineHarvestResult();
                if (empty.HasSummaryContent)
                    failures.Add("case2 [gate-still-silent-when-empty] an empty window reports summary content -- the gate " +
                                 "is now always-true and would raise a popup that says nothing");

                // ── 3. mend-only still reveals (WO-1231 must not regress) ─────
                // EchoMendReport.HasContent is Repairs > 0 || HealthFraction > 0 || SpentTotal > 0
                // || Stalled (EchoMendCopy.cs:104), so one repair that cost 40 wood is content.
                var mendOnly = new OfflineHarvestResult
                {
                    Mend = new EchoMendReport { ClaimSequence = 7, Repairs = 1, SpentWood = 40 },
                };
                if (!mendOnly.HasMendNews)
                    failures.Add("case3 setup is wrong: a 1-repair / 40-wood mend report did not read as mend news");
                else if (!mendOnly.HasSummaryContent)
                    failures.Add("case3 [gate-mend-only-survives] a mend-only window no longer reveals -- WO-1231 regressed");
                if (mendOnly.Total != 0)
                    failures.Add("case3 setup is wrong: the mend-only case must carry no haul");

                // ── 4/5. the popup: one gate, one modal, a verb that acts ─────
                string popup = ReadOrNull(PopupSrc);
                if (popup == null) failures.Add($"case4 could not read {PopupSrc}");
                else
                {
                    if (popup.IndexOf("!result.HasSummaryContent", StringComparison.Ordinal) < 0)
                        failures.Add("case4 [popup-reads-the-one-gate] WelcomeBackPopup.Show does not gate on " +
                                     "result.HasSummaryContent -- the reveal gate has been re-derived at the call site");
                    if (popup.IndexOf("result.Total <= 0 && !result.HasMendNews", StringComparison.Ordinal) >= 0)
                        failures.Add("case4 [popup-reads-the-one-gate] the RETIRED two-term gate " +
                                     "'result.Total <= 0 && !result.HasMendNews' is still present -- a collector-only town " +
                                     "falls through it");

                    if (popup.IndexOf("new Vector2(0.63f, 0.155f), CollectAndDismiss", StringComparison.Ordinal) < 0)
                        failures.Add("case5 [collect-button-performs-its-verb] the COLLECT button is not wired to " +
                                     "CollectAndDismiss (it used to call Dismiss, i.e. it collected nothing)");
                    if (popup.IndexOf("CollectorStatusGate.RequestCollectAll", StringComparison.Ordinal) < 0)
                        failures.Add("case5 [collect-button-performs-its-verb] the popup never reaches " +
                                     "CollectorStatusGate.RequestCollectAll -- the tap does not carry to the existing " +
                                     "collect command");

                    int modals = CountOf(popup, "BuildObsidianModal");
                    if (modals != 1)
                        failures.Add($"case5 [collect-button-performs-its-verb] WelcomeBackPopup builds {modals} modals -- " +
                                     "this is the ONE return-time surface; a second popup is forbidden");

                    if (popup.IndexOf("AddCompletedJobRows", StringComparison.Ordinal) < 0)
                        failures.Add("case5 the finished-job rows are not rendered (AddCompletedJobRows absent)");
                    if (popup.IndexOf("AddCollectorRow", StringComparison.Ordinal) < 0)
                        failures.Add("case5 the pending-collector row is not rendered (AddCollectorRow absent)");

                    // ── 7. ASCII only in the player-facing row copy ───────────
                    foreach (var lit in new[] { "COMPLETE", "ALSO FINISHED", "COLLECTOR WAITING", "COLLECTORS WAITING" })
                    {
                        if (popup.IndexOf("\"" + lit, StringComparison.Ordinal) < 0 &&
                            popup.IndexOf(lit + "\"", StringComparison.Ordinal) < 0)
                            failures.Add($"case7 [rows-are-ascii] expected row literal '{lit}' is missing from the popup");
                        else if (!IsAscii(lit))
                            failures.Add($"case7 [rows-are-ascii] row literal '{lit}' is not ASCII");
                    }
                }

                // ── 6. the service records, and never banks ───────────────────
                string service = ReadOrNull(ServiceSrc);
                if (service == null) failures.Add($"case6 could not read {ServiceSrc}");
                else
                {
                    if (service.IndexOf("BuildTimerService.Instance", StringComparison.Ordinal) < 0 ||
                        service.IndexOf("JobCompleted += OnAnyJobCompleted", StringComparison.Ordinal) < 0)
                        failures.Add("case6 [service-records-and-never-banks] OfflineHarvestService does not attach to " +
                                     "BuildTimerService.JobCompleted -- finished jobs cannot reach the away summary");
                    if (service.IndexOf("ResourceCollectorRegistry.All", StringComparison.Ordinal) < 0)
                        failures.Add("case6 [service-records-and-never-banks] the pending-collector total is not read from " +
                                     "ResourceCollectorRegistry -- the 'waiting' row cannot be populated");
                    if (service.IndexOf(".Collect()", StringComparison.Ordinal) >= 0)
                        failures.Add("case6 [service-records-and-never-banks] OfflineHarvestService calls Collect() -- the " +
                                     "away claim must REPORT pending, never bank it; banking here is a second route to the " +
                                     "wallet for the same units");
                }

                string result = ReadOrNull(ResultSrc);
                if (result == null) failures.Add($"case1 could not read {ResultSrc}");
                else if (result.IndexOf("HasSummaryContent", StringComparison.Ordinal) < 0)
                    failures.Add("case1 OfflineHarvestResult carries no HasSummaryContent -- the one gate does not exist");
            }
            catch (Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "AWAY SUMMARY OK -- a zero-haul window with one finished job and a pending collector REVEALS; " +
                         "an empty window still stays silent; mend-only still reveals; the popup reads the one gate, " +
                         "builds one modal, and COLLECT carries to CollectorStatusGate.RequestCollectAll";
                return true;
            }
            reason = $"AWAY SUMMARY FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>Read a repo-relative source file, or null (never throws -- an unreadable
        /// file is a NAMED failure above, not an exception that hides the other cases).</summary>
        private static string ReadOrNull(string relativePath)
        {
            try
            {
                string full = Path.Combine(UnityEngine.Application.dataPath, "..", relativePath);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch (Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Regression",
                    $"away-summary oracle could not read '{relativePath}': {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static bool IsAscii(string s)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] > 127) return false;
            return true;
        }
    }
}
