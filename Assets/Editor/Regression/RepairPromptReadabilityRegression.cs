using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class RepairPromptReadabilityRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string root = Directory.GetCurrentDirectory();
            string hud = File.ReadAllText(Path.Combine(root, "Assets/_Modules/HUD/Kit/HudKitController.cs"));
            string controller = File.ReadAllText(Path.Combine(root, "Assets/_Modules/Village/Walls/WallRepairController.cs"));
            Require(hud, "horizontalOverflow = HorizontalWrapMode.Wrap", failures);
            Require(hud, "verticalOverflow = VerticalWrapMode.Overflow", failures);
            Require(hud, "resizeTextForBestFit = true", failures);
            Require(hud, "Repair structure", failures);
            Require(hud, "Rebuild structure", failures);
            Require(controller, "Health: ", failures);
            Require(controller, "Damage: ", failures);
            Require(controller, " cost: ", failures);
            Require(controller, "Shortfall: ", failures);
            string sample = DeNelle.Village.WallRepairController.ComposePromptDetails(
                "Northwestern Reinforced Force-Field Gate", .63f,
                "12,500 wood, 4,750 iron", "2,500 wood, 750 iron", false, "Repair");
            if (sample.Contains("...") || sample.Contains("\u2026")) failures.Add("sample copy contains ellipsis");
            foreach (string full in new[] { "Northwestern Reinforced Force-Field Gate", "Health: 37%",
                         "Damage: 63%", "Repair cost: 12,500 wood, 4,750 iron", "Shortfall: 2,500 wood, 750 iron" })
                if (!sample.Contains(full)) failures.Add("full-copy path lost: " + full);
            reason = failures.Count == 0 ? "repair prompt wraps with complete name/health/damage/cost/shortfall/action copy"
                : string.Join(" | ", failures);
            return failures.Count == 0;
        }

        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("REPAIR_PROMPT_READABILITY_OK " + reason);
            else Debug.LogError("REPAIR_PROMPT_READABILITY_FAIL " + reason);
        }

        private static void Require(string source, string token, List<string> failures)
        { if (!source.Contains(token)) failures.Add("missing runtime contract: " + token); }
    }
}
