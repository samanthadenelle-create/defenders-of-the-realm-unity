// =============================================================================
// ManageQueueDrawerRegression [manage-queue-drawer]
// -----------------------------------------------------------------------------
// F8 2026-08-31: tower browsing leads; queue administration is opt-in.
//
// ⛔ RE-POINTED 2026-09-04 (WO-1368). THIS SUITE ENFORCED THE DEFECT.
//
// The 2026-08-31 ruling is REAL and is unchanged: inline queue rows made the browse
// list overflow at landscape height, so queue rows must not be built into the browse
// catalogue. But commit 486cd7b17 removed the ONLY call to ManageScreenPanel.AddQueueRow
// - the method that builds `Finish Now`, `Ad`, `Cancel` and `Move up` - and added, in
// the SAME change, a case here that FAILS THE BUILD if that call comes back:
//
//     if (panel.Contains("AddSectionHeader(\"IN QUEUE - \"") ||
//         panel.Contains("AddQueueRow(_vm.QueueRows"))
//         failures.Add("queue jobs are duplicated inline beneath the primary upgrade catalogue");
//
// The verbs were moved to "the explicit header Queue drawer", which contained only the
// display-only rail and the Buy-Builder offer. So for three days the crystal sink and
// the rewarded-ad surface had NO BUILD SITE ANYWHERE - and this suite guaranteed they
// could not be restored. It shipped to a production candidate with REGRESSION_OK green.
// (Owner, playing it: "i dont see the watch ad or pay crtystals to complete early stuff".)
//
// ⭐ WHAT CHANGED, AND WHY IT IS A RE-POINT AND NOT A DELETION (the WO-1159 precedent):
// when a ruling MOVES, the pin moves with it and gets STRICTER. The ban is now SCOPED to
// RenderList - the browse catalogue, which is what the ruling was ever about - and a new
// case REQUIRES the rows to exist in the drawer. Absence is no longer a passing state.
//
// ⚠ WHY THE ORIGINAL ACCEPTANCE CRITERION WAS WORTHLESS, recorded so it is not rewritten:
// the ticket first asked for an oracle asserting `queueRows > 0` while the queue is
// non-empty. `queueRows` is the VM's count and it tracked the real job count PERFECTLY
// all morning while not one verb rendered. Asserting the VM computed rows proves nothing.
// These cases assert the BUILD SITE is reached and the CONTROLS are constructed there.
//
// ⚠ THE HONEST LIMIT: DataRegression runs in editor batchmode with no play session, so
// this suite cannot instantiate the panel and count Buttons. It is a SOURCE sweep. What
// would catch the runtime half is the FlowTrace line RenderQueueDrawer now emits
// ("queue drawer BUILT n row(s) ... FinishNow=n Ad=n Cancel=n") read off a device or
// AutoPilot capture with a job queued - which is also the WO-1368 acceptance evidence.
//
// Cases:
//   1 [drawer-exists]     the drawer is constructed, collapsed by default, reachable by
//                         the QUEUE affordance, and spends no browse height.
//   2 [rows-not-inline]   RenderList (the browse catalogue) builds NO queue rows.
//   3 [rows-have-a-home]  AddQueueRow HAS a caller, and it is RenderQueueDrawer.
//   4 [verbs-exist]       Finish Now / Ad / Cancel / Move up are all still constructed,
//                         and each is wired to its VM command.
//   5 [drawer-rendered]   RenderQueueDrawer is actually invoked - on Render and on open.
//   6 [ad-comment-true]   the in-file claim about the ad flag / ad SDK is not the false
//                         one that shipped (it sent a reader chasing a flag already on).
//   7 [townsfolk-paths]   unchanged: early villagers teach the exact Build/Manage paths.
//
// Markers: MANAGE_QUEUE_DRAWER_OK / MANAGE_QUEUE_DRAWER_FAIL.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>F8 2026-08-31: tower browsing leads; queue administration is opt-in.
    /// WO-1368: opt-in means BEHIND the QUEUE affordance, never NOWHERE.</summary>
    public static class ManageQueueDrawerRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string panelPath = Path.Combine("Assets", "_Modules", "Village", "UI", "Manage", "ManageScreenPanel.cs");
            string panel = File.Exists(panelPath) ? File.ReadAllText(panelPath) : string.Empty;
            if (panel.Length == 0)
                failures.Add("[drawer-exists] ManageScreenPanel.cs is MISSING or empty - nothing below can be trusted");

            // ── 1 [drawer-exists] ────────────────────────────────────────────────────────
            if (!panel.Contains("BuildQueueDrawer(well)"))
                failures.Add("[drawer-exists] Manage does not construct the queue side drawer");
            if (!panel.Contains("_queueDrawer.SetActive(false)"))
                failures.Add("[drawer-exists] queue drawer is not collapsed by default");
            if (!panel.Contains("float fixedNoRail = stripCost + noticeCost"))
                failures.Add("[drawer-exists] rail/slot bands still consume default browse height");
            if (!panel.Contains("ManageHeaderActions") || !panel.Contains("TabsBandPx = 0f"))
                failures.Add("[drawer-exists] Queue is not seated in the title row or the redundant destination band returned");
            if (!panel.Contains("\"QUEUE\""))
                failures.Add("[drawer-exists] right-edge queue affordance is missing");

            // ── 2 [rows-not-inline] — the 2026-08-31 ruling, now SCOPED to the browse list ──
            // The ruling was always about the BROWSE CATALOGUE overflowing. Scoping the ban to
            // RenderList's body is what lets the verbs live in the drawer without weakening it.
            string renderList = Body(panel, "private void RenderList()", "private string FindSummary");
            if (renderList == null)
                failures.Add("[rows-not-inline] could not locate RenderList's body - the scoped ban cannot be evaluated, " +
                             "so it is reported as a FAILURE rather than passing vacuously");
            else
            {
                if (renderList.Contains("AddQueueRow"))
                    failures.Add("[rows-not-inline] RenderList builds queue rows inline beneath the primary upgrade " +
                                 "catalogue - the browse destination overflows at landscape height (F8 2026-08-31)");
                if (renderList.Contains("AddSectionHeader(\"IN QUEUE - \""))
                    failures.Add("[rows-not-inline] the IN QUEUE section header is back in the browse list");
            }

            // ── 3 [rows-have-a-home] — the WO-1368 defect, asserted directly ──────────────
            // A private method with zero callers is dead code that LOOKS like a shipped feature.
            // That is exactly what `Finish Now` and `Ad` were for three days.
            int defs = Count(panel, "private void AddQueueRow(");
            int calls = Count(panel, "AddQueueRow(") - defs;
            if (defs == 0)
                failures.Add("[rows-have-a-home] AddQueueRow is GONE - nothing builds Finish Now / Ad / Cancel / Move up");
            else if (calls == 0)
                failures.Add("[rows-have-a-home] ⛔ AddQueueRow has ZERO CALLERS. The crystal sink and the rewarded-ad " +
                             "surface are unreachable from every tab, every channel, at every queue depth - the exact " +
                             "WO-1368 defect that shipped to a production candidate with every marker green");
            // Bounded by RenderList, which follows it - so this scope holds the drawer renderer
            // ALONE and cannot be satisfied by something RenderList does.
            string drawerRender = Body(panel, "private void RenderQueueDrawer()", "private void RenderList()");
            if (drawerRender == null)
                failures.Add("[rows-have-a-home] RenderQueueDrawer is missing - the drawer has no row build site, which " +
                             "is the state the removal comment already claimed was not true");
            else if (!drawerRender.Contains("AddQueueRow(_vm.QueueRows[i])"))
                failures.Add("[rows-have-a-home] RenderQueueDrawer does not build the VM's queue rows - the drawer would " +
                             "again be a rail and an offer with no verbs");

            // ── 4 [verbs-exist] — each control AND its command ───────────────────────────
            RequirePair(panel, failures, "\"Finish Now\"", "FinishNow(channel, jobId)", "Finish Now (the crystal sink)");
            RequirePair(panel, failures, "\"Ad\"",         "WatchAd(channel, jobId)",   "Ad (the rewarded-ad surface)");
            RequirePair(panel, failures, "\"Cancel\"",     "Cancel(channel, jobId)",    "Cancel");
            RequirePair(panel, failures, "\"Move up\"",    "BumpUp(channel, jobId",     "Move up");

            // ── 5 [drawer-rendered] — built is not the same as rendered ──────────────────
            string render = Body(panel, "private void Render()", "private void ApplyOperationalMedievalSkin");
            if (render == null || !render.Contains("RenderQueueDrawer()"))
                failures.Add("[drawer-rendered] Render() does not refresh the open drawer - rows would be built once and " +
                             "then never track the queue");
            else if (render.IndexOf("RenderQueueDrawer()", StringComparison.Ordinal) <
                     render.IndexOf("RenderList()", StringComparison.Ordinal))
                failures.Add("[drawer-rendered] Render() builds the drawer BEFORE RenderList, which clears the tick and " +
                             "progress cells - the drawer's rows would keep their buttons and silently lose their " +
                             "countdowns");
            string toggle = Body(panel, "private void ToggleQueueDrawer()", "private void BuildNotice");
            if (toggle == null || !toggle.Contains("RenderQueueDrawer()"))
                failures.Add("[drawer-rendered] ToggleQueueDrawer does not render the drawer on open - the QUEUE " +
                             "affordance would reveal an empty panel");

            // ── 6 [ad-comment-true] — a comment that lies costs the next seat a session ──
            if (panel.Contains("no ad SDK is wired anywhere"))
                failures.Add("[ad-comment-true] the stale 2026-08-07 comment is back: it calls FeatureFlags.RewardedAdSkip " +
                             "OFF and claims no ad SDK is wired. Both are false (the flag is declared defaultOn:true and " +
                             "LevelPlay is integrated), and it sends a reader chasing a flag that is already on");

            // ── 7 [townsfolk-paths] — unchanged from the original suite ─────────────────
            string first = TownsfolkDialogue.BuildHelpFor(TownsfolkDialogue.Archetype.Trader, 0);
            if (!first.Contains("Tap Build") || !first.Contains("Defense") || !first.Contains("tower card"))
                failures.Add("[townsfolk-paths] townsfolk do not teach the exact Build > Defense > tower-card path");
            string second = TownsfolkDialogue.BuildHelpFor(TownsfolkDialogue.Archetype.Trader, 1);
            if (!second.Contains("Manage") || !second.Contains("Upgrade"))
                failures.Add("[townsfolk-paths] townsfolk do not teach the exact tower upgrade path");
            if (TownsfolkDialogue.ShouldOfferBuildHelp(3, TownsfolkDialogue.Archetype.Trader, 0))
                failures.Add("[townsfolk-paths] onboarding help continues after the opening waves");

            reason = failures.Count == 0
                ? "MANAGE_QUEUE_DRAWER_OK tower choices lead; queue administration is opt-in AND REACHABLE " +
                  "(Finish Now / Ad / Cancel / Move up are built in the drawer, never in the browse list); " +
                  "early villagers teach exact paths"
                : "MANAGE_QUEUE_DRAWER_FAIL: " + string.Join("; ", failures);
            return failures.Count == 0;
        }

        /// <summary>A control is only real when its FACE and its COMMAND are both present - a
        /// button wired to nothing and a command no button calls both read as "shipped".</summary>
        private static void RequirePair(string panel, List<string> failures, string face, string command, string what)
        {
            if (!panel.Contains(face))
                failures.Add("[verbs-exist] the " + what + " control face " + face + " is not constructed anywhere");
            else if (!panel.Contains(command))
                failures.Add("[verbs-exist] the " + what + " face exists but nothing invokes " + command +
                             " - the control would render and do nothing");
        }

        /// <summary>Source between <paramref name="from"/> and the next <paramref name="until"/>,
        /// or null when either marker is absent. Deliberately null-on-miss: a scoped assertion that
        /// cannot find its scope must FAIL, not pass silently on an empty string.</summary>
        private static string Body(string src, string from, string until)
        {
            int a = src.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return null;
            int b = src.IndexOf(until, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? null : src.Substring(a, b - a);
        }

        private static int Count(string src, string needle)
        {
            int n = 0, i = 0;
            while ((i = src.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }
    }
}
