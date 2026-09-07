// =============================================================================
// WelcomeBackDoorsRegression -- WO-1408: the return screen has a NEXT DOOR.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Entry point: Run(out string reason)
//
// THE MEASURED RED (read at source on the pre-change tree, 2026-09-06):
//   * WelcomeBackPopup.cs -- the file built exactly ONE button, COLLECT, and drew
//     no row that led anywhere. `grep -c "BuildObsidianButton"` = 1.
//   * WelcomeBackDoorsVM did not exist; `PanelRouter` was not referenced anywhere
//     under Assets/_Modules/Village/Harvest/. The away report could name a finished
//     build and then leave the player on the HUD, where the loudest remaining
//     surface is the store card.
//   * OfflineHarvestResult carried no attack axis at all, so "was I attacked?" --
//     the one away-window question with a panel already built to answer it
//     (PanelId.DefenseReport, WO-1026) -- could not even be asked.
// Every case below fails on that tree: cases 1-6 because the type does not compile
// against it, cases 7-9 because the literals they grep are absent.
//
// WHAT THIS SUITE ASSERTS
//   1. [two-rows-with-doors]   one finished job + one recorded attack -> exactly TWO rows,
//      carrying the named words, each with a door (Manage / DefenseReport).
//   2. [nothing-means-nothing] an empty window -> ZERO rows and NO ready door. There is no
//      empty state and no disabled door; COLLECT stands alone.
//   3. [raid-door-retired]     an army-ready fixture -> NO ready door, NO army/Heartfire line,
//      ready='none'. OWNER REVERSAL 2026-09-07 01:13 ("no idea why raid is listed here") on
//      Logs/device/screens/owner-harvest-20260907-011321.png. This case previously asserted
//      the OPPOSITE ([raid-door-routes]); it is inverted rather than deleted so that restoring
//      WO-1408's spec fails here instead of reaching the owner a second time.
//   4. [raid-door-retired]     no posture combination brings the door back.
//   5. [manage-tab-is-real]    the tab string is one ManageScreenPanel.Open(string) actually
//      accepts, and TRAIN wins the mixed case.
//   6. [rows-are-ascii]        every produced string reaches a mobile font atlas.
//   7. [popup-routes-through-the-router]  WelcomeBackPopup reaches its destinations through
//      PanelRouter.Open + PanelManager.SetReturnDoor and NOWHERE ELSE -- no second navigation
//      mechanism, and still exactly ONE modal built in that file.
//   8. [collect-first-then-route]  a door tap performs the SAME collect verb the button does
//      (PerformCollect, which CollectAndDismiss also calls) before it routes.
//   9. [trace-line]            the ticket's one-per-open trace literal is emitted.
//
// ⛔ THE TICKET'S "START WAVE" DOOR IS NOT PINNED HERE, ON PURPOSE. WO-1408 sketches
// "Heartfire is full - a wave is ready" beside a START WAVE door. HeartfireCharges.cs
// states in its own header that Heartfire's only sink is MARCHING and that "RAID ORDERS
// is dead"; it buys no wave, and no wave-start door exists to route to. Pinning the
// sketch would pin a mechanism the tree does not have. Raised to the lead as a ruling.
//
// Cases 1-6 drive the real DeNelle.Village.UI.WelcomeBackDoorsVM with no canvas and no
// scene -- which is the whole reason the destination decisions live in a pure type.
// Cases 7-9 are SOURCE assertions, the right instrument for "the wiring is present":
// the modal cannot be constructed in editmode batchmode (no canvas/PanelSettings).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Core.UI;
using DeNelle.Village;
using DeNelle.Village.UI;

namespace DeNelle.Editor.Regression
{
    /// <summary>Headless oracle for the away summary's optional door rows and ready door.</summary>
    public static class WelcomeBackDoorsRegression
    {
        private const string PopupSrc = "Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs";
        private const string VmSrc = "Assets/_Modules/Village/Harvest/UI/WelcomeBackDoorsVM.cs";
        private const string ServiceSrc = "Assets/_Modules/Village/Harvest/OfflineHarvestService.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            try
            {
                // ── 1. THE ACCEPTANCE CASE: two rows, two doors ───────────────
                var busy = new OfflineHarvestResult
                {
                    AwaySeconds = 6.0 * 3600.0 + 12.0 * 60.0,
                    AttackCount = 1,
                    AttackBreachName = "North Gate",
                    AttackOutcomeWord = "BREACHED",
                };
                busy.CompletedJobs.Add(new OfflineHarvestResult.OfflineJobLine
                { Verb = "TRAIN", Label = "Footman x1", FinishedUnixMs = 1_000.0 });
                busy.CompletedJobs.Add(new OfflineHarvestResult.OfflineJobLine
                { Verb = "UPGRADE", Label = "Arcane Spire L2", FinishedUnixMs = 2_000.0 });

                var vm = WelcomeBackDoorsVM.Build(busy, raidCapable: false,
                    armyUsed: 0, armyCap: 0, heartfireLit: 0, heartfireMax: 3);

                if (vm.RowCount != 2)
                    failures.Add($"case1 [two-rows-with-doors] one finished job + one recorded attack produced " +
                                 $"{vm.RowCount} row(s), expected 2 -- the returning player is dropped on the HUD " +
                                 "with nothing to tap, which is the measured WO-1408 defect");
                else
                {
                    var finished = vm.Rows[0];
                    var attacked = vm.Rows[1];

                    if (finished.Label != "FINISHED WHILE AWAY")
                        failures.Add($"case1 [two-rows-with-doors] row 1 label = '{finished.Label}', expected " +
                                     "'FINISHED WHILE AWAY'");
                    if (finished.Detail.IndexOf("Footman x1", StringComparison.Ordinal) < 0 ||
                        finished.Detail.IndexOf("Arcane Spire L2", StringComparison.Ordinal) < 0)
                        failures.Add($"case1 [two-rows-with-doors] row 1 detail '{finished.Detail}' does not name both " +
                                     "finished jobs -- the row must say the queue card's own words");
                    if (finished.Door != PanelId.Manage)
                        failures.Add($"case1 [two-rows-with-doors] the finished row's door is {finished.Door}, " +
                                     "expected PanelId.Manage");
                    if (string.IsNullOrEmpty(finished.DoorText))
                        failures.Add("case1 [two-rows-with-doors] the finished row carries no door face");

                    if (attacked.Label != "ATTACKED")
                        failures.Add($"case1 [two-rows-with-doors] row 2 label = '{attacked.Label}', expected 'ATTACKED'");
                    if (attacked.Detail.IndexOf("North Gate", StringComparison.Ordinal) < 0 ||
                        attacked.Detail.IndexOf("1x", StringComparison.Ordinal) < 0)
                        failures.Add($"case1 [two-rows-with-doors] row 2 detail '{attacked.Detail}' does not read " +
                                     "'1x - North Gate breached'");
                    if (attacked.Door != PanelId.DefenseReport)
                        failures.Add($"case1 [two-rows-with-doors] the attacked row's door is {attacked.Door}, " +
                                     "expected PanelId.DefenseReport");
                }

                if (vm.TraceLine.IndexOf("finished=1", StringComparison.Ordinal) < 0 ||
                    vm.TraceLine.IndexOf("attacked=1", StringComparison.Ordinal) < 0)
                    failures.Add($"case1 [two-rows-with-doors] the trace line '{vm.TraceLine}' does not count both rows");

                // ── 2. an empty window says NOTHING ───────────────────────────
                var emptyVm = WelcomeBackDoorsVM.Build(new OfflineHarvestResult(), raidCapable: true,
                    armyUsed: 0, armyCap: 10, heartfireLit: 3, heartfireMax: 3);
                if (emptyVm.RowCount != 0)
                    failures.Add($"case2 [nothing-means-nothing] an empty window produced {emptyVm.RowCount} row(s) -- " +
                                 "there is no empty state and no 'nothing finished' row; a row the player cannot act " +
                                 "on teaches them the screen lies");
                if (emptyVm.HasReadyDoor)
                    failures.Add("case2 [nothing-means-nothing] a zero-army window offered the RAID door");
                if (emptyVm.ReadyKind != "none")
                    failures.Add($"case2 [nothing-means-nothing] ready kind = '{emptyVm.ReadyKind}', expected 'none'");

                var nullVm = WelcomeBackDoorsVM.Build(null, true, 3, 10, 3, 3);
                if (nullVm == null || nullVm.RowCount != 0 || nullVm.HasReadyDoor)
                    failures.Add("case2 [nothing-means-nothing] a NULL result did not yield an empty VM -- the popup " +
                                 "would blank rather than degrade");

                // -- 3. the RAID door is RETIRED (owner reversal 2026-09-07 01:13) --
                // WAS [raid-door-routes]: this case asserted the door EXISTED on an army-ready,
                // Heartfire-lit, raid-capable window. The owner read that door on her own device
                // frame (Logs/device/screens/owner-harvest-20260907-011321.png) and said "no idea
                // why raid is listed here". The welcome-back popup is about the HARVEST; COLLECT is
                // its single action. The case is INVERTED, not deleted, so a seat that restores the
                // ready band from WO-1408's spec fails here instead of shipping it to her twice.
                var ready = WelcomeBackDoorsVM.Build(new OfflineHarvestResult(), raidCapable: true,
                    armyUsed: 3, armyCap: 10, heartfireLit: 3, heartfireMax: 3);
                if (ready.HasReadyDoor)
                    failures.Add($"case3 [raid-door-retired] the fully-ready window still offered a '{ready.ReadyDoorText}' " +
                                 "door beside COLLECT -- the owner retired it 2026-09-07 ('no idea why raid is listed here'); " +
                                 "this popup's ONE action is COLLECT");
                if (!string.IsNullOrEmpty(ready.ReadyLine))
                    failures.Add($"case3 [raid-door-retired] the fully-ready window still drew the army/Heartfire line " +
                                 $"'{ready.ReadyLine}' -- it is chrome for the retired raid door and goes with it");
                if (ready.ReadyKind != "none")
                    failures.Add($"case3 [raid-door-retired] ready kind = '{ready.ReadyKind}', expected 'none'");

                // -- 4. NO posture combination may bring the door back ---------
                // WAS [ready-needs-all-three] (the door appears only when all three signals are
                // true). After the reversal the assertion is stronger and simpler: none of them,
                // in any combination, produces a door.
                if (WelcomeBackDoorsVM.Build(new OfflineHarvestResult(), false, 3, 10, 3, 3).HasReadyDoor ||
                    WelcomeBackDoorsVM.Build(new OfflineHarvestResult(), true, 0, 10, 3, 3).HasReadyDoor ||
                    WelcomeBackDoorsVM.Build(new OfflineHarvestResult(), true, 3, 10, 0, 3).HasReadyDoor ||
                    WelcomeBackDoorsVM.Build(new OfflineHarvestResult(), true, 10, 10, 3, 3).HasReadyDoor)
                    failures.Add("case4 [raid-door-retired] some posture combination still produced a ready door on the " +
                                 "welcome-back popup -- after the 2026-09-07 reversal there is no combination that may");

                // ── 5. the Manage tab is one the panel actually accepts ───────
                var trainOnly = new List<OfflineHarvestResult.OfflineJobLine>
                { new OfflineHarvestResult.OfflineJobLine { Verb = "TRAIN", Label = "Footman x1" } };
                var researchOnly = new List<OfflineHarvestResult.OfflineJobLine>
                { new OfflineHarvestResult.OfflineJobLine { Verb = "RESEARCH", Label = "Masonry" } };
                var buildOnly = new List<OfflineHarvestResult.OfflineJobLine>
                { new OfflineHarvestResult.OfflineJobLine { Verb = "BUILD", Label = "Barracks" } };
                var mixed = new List<OfflineHarvestResult.OfflineJobLine>
                {
                    new OfflineHarvestResult.OfflineJobLine { Verb = "BUILD", Label = "Barracks" },
                    new OfflineHarvestResult.OfflineJobLine { Verb = "TRAIN", Label = "Footman x1" },
                };
                CheckTab(failures, trainOnly, "Troops");
                CheckTab(failures, researchOnly, "Research");
                CheckTab(failures, buildOnly, "Buildings");
                CheckTab(failures, mixed, "Troops");

                string manage = ReadOrNull("Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs");
                if (manage == null) failures.Add("case5 could not read ManageScreenPanel.cs");
                else
                    foreach (var tab in new[] { "Troops", "Research", "Buildings" })
                        if (manage.IndexOf("\"" + tab + "\"", StringComparison.Ordinal) < 0)
                            failures.Add($"case5 [manage-tab-is-real] ManageScreenPanel.Open(string) does not accept " +
                                         $"'{tab}' -- the door would half-open (the panel ignores an unknown tab and " +
                                         "sits on the launcher)");

                // ── 6. ASCII only ────────────────────────────────────────────
                foreach (var s in Strings(vm)) if (!IsAscii(s))
                    failures.Add($"case6 [rows-are-ascii] produced string '{s}' is not ASCII -- it reaches a mobile " +
                                 "font atlas");
                foreach (var s in Strings(ready)) if (!IsAscii(s))
                    failures.Add($"case6 [rows-are-ascii] produced string '{s}' is not ASCII");

                // ── 7/8/9. the popup's wiring ─────────────────────────────────
                string popup = ReadOrNull(PopupSrc);
                if (popup == null) failures.Add($"case7 could not read {PopupSrc}");
                else
                {
                    if (popup.IndexOf("PanelRouter.Open(door)", StringComparison.Ordinal) < 0 ||
                        popup.IndexOf("PanelRouter.Open(door, context)", StringComparison.Ordinal) < 0)
                        failures.Add("case7 [popup-routes-through-the-router] the doors do not route through " +
                                     "PanelRouter.Open -- a second navigation mechanism is forbidden");
                    if (popup.IndexOf("PanelManager.SetReturnDoor", StringComparison.Ordinal) < 0)
                        failures.Add("case7 [popup-routes-through-the-router] the route is not armed on the existing " +
                                     "return-door arbiter. It must be: ResourceCollectorService.CollectAll opens a " +
                                     "HarvestOverflowModal on the SAME exclusive arbiter, so a synchronous open here " +
                                     "would swap the harvest result off screen the instant it appeared");
                    foreach (var forbidden in new[] { "FindAnyObjectByType<ManageScreenPanel>", "GetComponent<DefenseReportPanel>", "SendMessage(" })
                        if (popup.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
                            failures.Add($"case7 [popup-routes-through-the-router] '{forbidden}' is a second route out " +
                                         "of this screen -- doors go through PanelRouter and nothing else");
                    int modals = CountOf(popup, "BuildObsidianModal");
                    if (modals != 1)
                        failures.Add($"case7 [popup-routes-through-the-router] WelcomeBackPopup builds {modals} modals " +
                                     "-- this is the ONE return-time surface");

                    if (popup.IndexOf("private void PerformCollect()", StringComparison.Ordinal) < 0)
                        failures.Add("case8 [collect-first-then-route] the collect VERB was not extracted -- a door " +
                                     "that routes without collecting makes COLLECT the only way to bank, which is the " +
                                     "cul-de-sac with an extra step");
                    if (popup.IndexOf("Dismiss();        // arms the return door", StringComparison.Ordinal) < 0 ||
                        popup.IndexOf("PerformCollect(); // may open the harvest-result modal", StringComparison.Ordinal) < 0)
                        failures.Add("case8 [collect-first-then-route] CollectThenRoute does not arm-then-collect -- " +
                                     "the ordering IS the mechanism (see the block comment on that method)");
                    if (popup.IndexOf("PerformCollect();\r\n            Dismiss();", StringComparison.Ordinal) < 0 &&
                        popup.IndexOf("PerformCollect();\n            Dismiss();", StringComparison.Ordinal) < 0)
                        failures.Add("case8 [collect-first-then-route] CollectAndDismiss no longer calls the shared " +
                                     "PerformCollect -- the button and the doors have two collect implementations");

                    if (popup.IndexOf("FlowTrace.Step(\"WelcomeBack\", _doors.TraceLine)", StringComparison.Ordinal) < 0)
                        failures.Add("case9 [trace-line] the popup does not emit the one-per-open WO-1408 trace line " +
                                     "(FlowTrace.Step(\"WelcomeBack\", ...) with the rows/ready counts)");
                }

                string vmSrc = ReadOrNull(VmSrc);
                if (vmSrc == null) failures.Add($"case7 could not read {VmSrc}");
                else if (vmSrc.IndexOf("UnityEngine", StringComparison.Ordinal) >= 0)
                    failures.Add("case7 [popup-routes-through-the-router] WelcomeBackDoorsVM references UnityEngine -- " +
                                 "the destination decisions must stay a pure function so this oracle can drive them " +
                                 "with no canvas and no scene");

                string service = ReadOrNull(ServiceSrc);
                if (service == null) failures.Add($"case1 could not read {ServiceSrc}");
                else
                {
                    if (service.IndexOf("DefenseReportLedger.All()", StringComparison.Ordinal) < 0)
                        failures.Add("case1 [two-rows-with-doors] OfflineHarvestService never reads the defence ledger " +
                                     "-- the ATTACKED row can have no data");
                    foreach (var forbidden in new[] { "DefenseReportLedger.Append", "DefenseReportLedger.MarkRead" })
                        if (service.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
                            failures.Add($"case1 [two-rows-with-doors] OfflineHarvestService calls {forbidden} -- the " +
                                         "away summary REPORTS the defence record, it must never write it or consume " +
                                         "the unread badge the door exists to carry the player to");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "WELCOME BACK DOORS OK -- a finished job and a recorded attack each produce ONE row with ONE " +
                         "door (Manage / Defence Report); an empty window produces none; NO posture brings back the " +
                         "retired RAID door (owner reversal 2026-09-07); every door collects first and routes through " +
                         "PanelRouter on the existing return-door arbiter";
                return true;
            }
            reason = $"WELCOME BACK DOORS FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        private static void CheckTab(List<string> failures,
                                     IReadOnlyList<OfflineHarvestResult.OfflineJobLine> jobs,
                                     string expected)
        {
            string got = WelcomeBackDoorsVM.ManageTabFor(jobs);
            if (got != expected)
                failures.Add($"case5 [manage-tab-is-real] ManageTabFor -> '{got}', expected '{expected}'");
        }

        private static IEnumerable<string> Strings(WelcomeBackDoorsVM vm)
        {
            if (vm == null) yield break;
            for (int i = 0; i < vm.Rows.Count; i++)
            {
                var r = vm.Rows[i];
                if (r == null) continue;
                yield return r.Label;
                yield return r.Detail;
                yield return r.DoorText;
                yield return r.DoorContext;
            }
            yield return vm.ReadyLine;
            yield return vm.ReadyDoorText;
            yield return vm.TraceLine;
        }

        /// <summary>Read a repo-relative source file, or null (never throws -- an unreadable file
        /// is a NAMED failure above, not an exception that hides the other cases).</summary>
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
                    $"welcome-back-doors oracle could not read '{relativePath}': {ex.GetType().Name}: {ex.Message}");
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
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++) if (s[i] > 127) return false;
            return true;
        }
    }
}
