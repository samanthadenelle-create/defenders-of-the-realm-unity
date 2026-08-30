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
            if (!vm.Contains("VisibleTabs.Add(ManageTab.Defense)") ||
                !vm.Contains("VisibleTabs.Add(ManageTab.Buildings)"))
                failures.Add("absent-before-placement / visible-after-placement category gate is missing");
            int upgrade = panel.IndexOf("UPGRADABLE TOWERS", StringComparison.Ordinal);
            int queue = panel.IndexOf("IN QUEUE - ", StringComparison.Ordinal);
            if (upgrade < 0 || queue < 0 || upgrade > queue)
                failures.Add("selected structure actions are not authored above queue history");
            if (!panel.Contains("Showing \" + (first + 1)") ||
                !panel.Contains("Previous page") || !panel.Contains("Next page"))
                failures.Add("overflow has no visible count and bidirectional paging affordance");
            if (!panel.Contains("Need something that is not placed here?") || !panel.Contains("Build new"))
                failures.Add("absent categories have no secondary Build-new route");

            reason = failures.Count == 0
                ? "Manage categories follow live placements; actions lead; overflow count/paging and Build-new route are visible."
                : "Manage progressive disclosure regression failed: " + string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
