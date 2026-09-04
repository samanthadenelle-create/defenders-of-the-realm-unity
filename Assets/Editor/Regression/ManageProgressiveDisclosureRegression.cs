using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor
{
    /// <summary>Source oracle for the phone Manage progressive-disclosure contract.</summary>
    public static class ManageProgressiveDisclosureRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string vm = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs");
            string panel = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs");

            if (!vm.Contains("CountPlacedThisTown()") || !vm.Contains("BuildVisibleTabs()"))
                failures.Add("categories are not derived from authoritative current-town placements");
            if (!panel.Contains("ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"))
                failures.Add("the stable four-card Manage launcher is missing or reordered");
            if (!panel.Contains("BarracksUnlock.IsUnlocked") ||
                !panel.Contains("Build a Barracks to unlock") ||
                !panel.Contains("ActivateLauncherCard"))
                failures.Add("locked Troops card is not sourced from BarracksUnlock with explicit feedback");
            int rebuild = panel.IndexOf("_vm.Rebuild();", StringComparison.Ordinal);
            int launcher = panel.IndexOf("RenderLauncherCards();", rebuild, StringComparison.Ordinal);
            if (rebuild < 0 || launcher < rebuild)
                failures.Add("launcher cards are not rendered after the VM populates availability");
            // ⭐ RE-POINTED 2026-09-04 (lead), NOT relaxed - the WO-1159 precedent: a ruling moved,
            // so the pin moves with it and gets STRICTER about the thing the ruling actually meant.
            //
            // THE RULING (F8 2026-08-31) IS "upgrade browsing LEADS; queue administration is OPT-IN".
            // The old check enforced it by banning the string `AddSectionHeader("IN QUEUE - "`
            // ANYWHERE in the panel. That was a fair proxy while the queue had no home at all - but
            // WO-1368 gave the queue verbs a home INSIDE the opt-in drawer, which is precisely what
            // the ruling asks for, and the global ban failed it. A header inside the drawer does not
            // put queue history in the browse catalogue; it labels the opt-in surface.
            //
            // ⛔ So the ban is SCOPED TO RenderList's BODY - the browse catalogue - exactly as its
            // sibling ManageQueueDrawerRegression was re-pointed in the same wave. Banning the string
            // globally would now forbid the fix for a P1 money-path defect (WO-1368: the Finish Now
            // and Ad verbs had NO build site for three days and shipped in the production candidate).
            int upgrade = panel.IndexOf("UPGRADABLE TOWERS", StringComparison.Ordinal);
            int rlStart = panel.IndexOf("private void RenderList(", StringComparison.Ordinal);
            int rlEnd   = rlStart >= 0 ? panel.IndexOf("        private ", rlStart + 24, StringComparison.Ordinal) : -1;
            string renderListBody = (rlStart >= 0 && rlEnd > rlStart) ? panel.Substring(rlStart, rlEnd - rlStart) : "";
            if (rlStart < 0)
                failures.Add("RenderList not found - the browse-leads pin cannot be scoped, so it cannot be trusted");
            if (upgrade < 0 || !panel.Contains("BuildQueueDrawer(well)") ||
                renderListBody.Contains("AddSectionHeader(\"IN QUEUE - \""))
                failures.Add("upgrade browsing does not lead cleanly with queue history isolated in the opt-in drawer");
            if (!panel.Contains("Showing \" + (first + 1)") ||
                !panel.Contains("Previous page") || !panel.Contains("Next page"))
                failures.Add("overflow has no visible count and bidirectional paging affordance");
            if (!panel.Contains("Need another town structure?") ||
                !panel.Contains("\"Open build\", OpenTownBuilder") ||
                !panel.Contains("EnterBuildMode(DeNelle.Core.Catalog.BuildType.Town)"))
                failures.Add("absent building categories have no real secondary Town-build route");

            reason = failures.Count == 0
                ? "Manage keeps four stable worded cards, derives availability from live placements, renders after VM population, and preserves actions/paging/Build-new."
                : "Manage progressive disclosure regression failed: " + string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
