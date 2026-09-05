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

            // [research-locked-visible] ADDED 2026-09-04 (WO-1390, owner on the Seeker, build 355905:
            // "under manage research it shows nothing, should it show Tier one and show locked with a
            // link to upgrade the prerequisite"). Device log: `research browse ... 6 with a tier
            // ladder -> 0 perk row(s)`. The cause was ONE line in BuildResearchBrowse -
            // `if (!can) continue;` - which dropped every tier-locked perk and discarded the
            // CanResearch reason as `_`. The Manage rule the Troops tab already follows ("Build a
            // Barracks to unlock" + BuildLockBadge) is now the Research rule too: a locked perk is a
            // LOCKED row whose StateText is the CanResearch reason verbatim and whose CTA is the
            // DOOR to the prerequisite (OpenUpgradePanel -> PanelId.BuildingUpgrade, the one existing
            // start path), never a dead button.
            //
            // This suite is a SOURCE oracle (no VM fixture), so the pin is scoped to the method body.
            // RED PROOF: restore `if (!can) continue;` in place of the locked-row block (the exact
            // pre-WO-1390 text) -> "Research drops locked perks" fires; drop the `Locked = true`
            // assignment -> the row-builder check fires; drop `r.Locked` from BuildBrowseRowContent
            // -> the panel check fires.
            int rbStart = vm.IndexOf("private void BuildResearchBrowse()", StringComparison.Ordinal);
            int rbEnd   = rbStart >= 0 ? vm.IndexOf("        private ", rbStart + 32, StringComparison.Ordinal) : -1;
            string researchBody = (rbStart >= 0 && rbEnd > rbStart) ? vm.Substring(rbStart, rbEnd - rbStart) : "";
            if (rbStart < 0)
                failures.Add("[research-locked-visible] BuildResearchBrowse not found - the locked-row pin cannot be scoped");
            if (researchBody.Contains("if (!can) continue;"))
                failures.Add("[research-locked-visible] Research drops locked perks (`if (!can) continue;` is back) - the owner sees an empty tab again");
            if (!researchBody.Contains("CanResearch(bId, pId, out string reason)") ||
                !researchBody.Contains("Locked = true,") ||
                !researchBody.Contains("StateText = gate") ||
                !researchBody.Contains("? \"Locked.\" : reason)") ||
                !researchBody.Contains("OpenUpgradePanel(bId)") ||
                !researchBody.Contains("\"UPGRADE THE HEART\"") ||
                !researchBody.Contains(" locked)."))
                failures.Add("[research-locked-visible] the locked research row is not built from the CanResearch reason with the upgrade-page door and the (M locked) trace");
            int bbStart = panel.IndexOf("private void BuildBrowseRowContent(", StringComparison.Ordinal);
            int bbEnd   = bbStart >= 0 ? panel.IndexOf("        private ", bbStart + 36, StringComparison.Ordinal) : -1;
            string browseRowBody = (bbStart >= 0 && bbEnd > bbStart) ? panel.Substring(bbStart, bbEnd - bbStart) : "";
            if (!browseRowBody.Contains("r.Locked") || !browseRowBody.Contains("BuildLockBadge("))
                failures.Add("[research-locked-visible] a locked browse row does not dim + seat BuildLockBadge like the Troops rail");

            reason = failures.Count == 0
                ? "Manage keeps four stable worded cards, derives availability from live placements, renders after VM population, and preserves actions/paging/Build-new."
                : "Manage progressive disclosure regression failed: " + string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
