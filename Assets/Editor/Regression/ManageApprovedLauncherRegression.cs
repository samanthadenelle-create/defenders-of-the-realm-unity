using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Source oracle for the approved 2026-08-31 four-card Manage launcher.</summary>
    public static class ManageApprovedLauncherRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            const string path = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";
            string panel = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            foreach (string copy in new[] { "Choose a path", "Towers, walls & gates",
                         "Town structures & upgrades", "Build a Barracks to unlock",
                         "Discover realm advancements" })
                if (!panel.Contains(copy)) failures.Add("missing approved copy: " + copy);

            if (!panel.Contains("ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"))
                failures.Add("four-card order drifted");
            if (panel.Contains("QueueBadgePlate_") || panel.Contains("0/5 queued\";"))
                failures.Add("launcher reintroduced non-actionable queue-depth clutter");
            if (!panel.Contains("MedievalUiSkin.ApplyShell(chrome)"))
                failures.Add("Manage no longer consumes the shared medieval shell/medallion contract");
            if (!panel.Contains("BarracksUnlock.IsUnlocked") || !panel.Contains("BuildLockBadge") ||
                !panel.Contains("UI/ElarionMedieval/badges/lock-badge"))
                failures.Add("Troops lock is not worded + visual + source-authoritative");
            foreach (string card in new[] { "cards/defense", "cards/buildings", "cards/troops-locked", "cards/research" })
                if (!panel.Contains(card)) failures.Add("approved layered card art missing: " + card);
            if (!panel.Contains("Build a Barracks to unlock Troops."))
                failures.Add("locked-card tap has no feedback");
            if (!panel.Contains("_categoryNavigationCommitted") ||
                !panel.Contains("if (_categoryNavigationCommitted) return"))
                failures.Add("rapid category taps are not guarded");
            if (!panel.Contains("card.transition = Selectable.Transition.ColorTint"))
                failures.Add("pressed/focused visual state is absent");
            if (!panel.Contains("ApplyOperationalMedievalSkin()") ||
                !panel.Contains("MedievalUiSkin.ApplyButton(button, primary)"))
                failures.Add("operational destinations still bypass the shared medieval button family");
            if (!panel.Contains("string.Equals(objectName, \"Scrim\"") ||
                !panel.Contains("string.Equals(objectName, \"CloseButton\""))
                failures.Add("bulk operational styling can repaint the modal scrim or shared Close");
            if (!panel.Contains("\"Build defense\""))
                failures.Add("Defense empty-state CTA is not mobile-readable");

            reason = failures.Count == 0
                ? "MANAGE_APPROVED_LAUNCHER_OK four-card hierarchy, lock feedback, clean summaries, and rapid-tap guard"
                : "MANAGE_APPROVED_LAUNCHER_FAIL: " + string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
