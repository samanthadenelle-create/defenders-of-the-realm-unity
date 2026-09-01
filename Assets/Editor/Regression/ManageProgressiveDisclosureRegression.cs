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
            int upgrade = panel.IndexOf("UPGRADABLE TOWERS", StringComparison.Ordinal);
            if (upgrade < 0 || !panel.Contains("BuildQueueDrawer(well)") ||
                panel.Contains("AddSectionHeader(\"IN QUEUE - \""))
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
