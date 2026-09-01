using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>F8 2026-08-31: tower browsing leads; queue administration is opt-in.</summary>
    public static class ManageQueueDrawerRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string panelPath = Path.Combine("Assets", "_Modules", "Village", "UI", "Manage", "ManageScreenPanel.cs");
            string panel = File.Exists(panelPath) ? File.ReadAllText(panelPath) : string.Empty;

            if (!panel.Contains("BuildQueueDrawer(well)"))
                failures.Add("Manage does not construct the queue side drawer");
            if (!panel.Contains("_queueDrawer.SetActive(false)"))
                failures.Add("queue drawer is not collapsed by default");
            if (!panel.Contains("float fixedNoRail = stripCost + noticeCost"))
                failures.Add("rail/slot bands still consume default browse height");
            if (!panel.Contains("ManageHeaderActions") || !panel.Contains("TabsBandPx = 0f"))
                failures.Add("Queue is not seated in the title row or the redundant destination band returned");
            if (!panel.Contains("\"QUEUE\""))
                failures.Add("right-edge queue affordance is missing");
            if (panel.Contains("AddSectionHeader(\"IN QUEUE - \"") ||
                panel.Contains("AddQueueRow(_vm.QueueRows"))
                failures.Add("queue jobs are duplicated inline beneath the primary upgrade catalogue");

            string first = TownsfolkDialogue.BuildHelpFor(TownsfolkDialogue.Archetype.Trader, 0);
            if (!first.Contains("Tap Build") || !first.Contains("Defense") || !first.Contains("tower card"))
                failures.Add("townsfolk do not teach the exact Build > Defense > tower-card path");
            string second = TownsfolkDialogue.BuildHelpFor(TownsfolkDialogue.Archetype.Trader, 1);
            if (!second.Contains("Manage") || !second.Contains("Upgrade"))
                failures.Add("townsfolk do not teach the exact tower upgrade path");
            if (TownsfolkDialogue.ShouldOfferBuildHelp(3, TownsfolkDialogue.Archetype.Trader, 0))
                failures.Add("onboarding help continues after the opening waves");

            reason = failures.Count == 0
                ? "MANAGE_QUEUE_DRAWER_OK tower choices lead; queue opt-in; early villagers teach exact paths"
                : "MANAGE_QUEUE_DRAWER_FAIL: " + string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
