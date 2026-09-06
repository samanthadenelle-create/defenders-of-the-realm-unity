// =============================================================================
// ManageNavigationRegression - WO-2001. The Manage INFORMATION ARCHITECTURE, and
// the one rule that cannot be proved by reading the screen: owner ruling 28.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor.Regression
//
// ⛔ RULING 28, VERBATIM: "once you go back to the item you need to resolve, you
// learn how much resources it needs and the back flow should take you back there to
// see if you want to look at another locked type."
//
// The navigation is a TREE WITH CROSS-EDGES. Ordinary back walks the tree - a detail
// returns to its grid. A PREREQUISITE JUMP crosses branches - a locked Outrider sends
// the player to the Barracks BUILD card, which lives under BUILD, not ARMY - and back
// from THAT returns to the Outrider, the screen that sent them.
//
// ⛔ THE CASE IS DRIVEN IN BOTH DIRECTIONS ON PURPOSE, because ruling 28's own oracle
// sketch says so: "Both directions, or the case passes vacuously by always returning
// to the origin." A back stack that ALWAYS returns to an origin passes a jump-only
// test and silently breaks every ordinary back press in the game.
//
// LIVE half: drives the real DeNelle.Village.UI.ManageScreenVM screen graph. It needs
// no services - EnterTab's Rebuild is wrapped in Guard.Try and degrades to an empty
// model, which is exactly the pre-boot shape a batchmode gate runs in.
// SOURCE half: reads ManageScreenPanel.cs / ManageScreenVM.cs as text for the shapes
// a live test cannot see - the retired launcher, the single art loader, the BACK wire.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core.Manage;
using DeNelle.Village.UI;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-2001 - the Manage screen graph, its back stack, and the retired launcher.</summary>
    public static class ManageNavigationRegression
    {
        private const string Tag = "[manage-navigation]";
        private const string PanelPath = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";
        private const string VmPath = "Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs";
        private const string HeartPath = "Assets/_Modules/Village/UI/Manage/HeartPanel.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== ManageNavigationRegression (WO-2001) ===\n");
            try
            {
                CheckBackDistinguishesJumpFromBrowse(failures, log);
                CheckTabsAndDefaultEntry(failures, log);
                CheckSource(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add(Tag + " suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "MANAGE_NAV_OK back distinguishes a prerequisite JUMP from a BROWSE " +
                         "(ruling 28), Manage opens on a tab rather than a chooser, and the " +
                         "four-tile launcher can no longer be shown";
                Debug.Log(reason + "\n" + log);
                return true;
            }
            reason = "MANAGE_NAV_FAIL: " + string.Join("; ", failures.ToArray());
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // ── CASE [jump-returns-to-origin] ────────────────────────────────────
        // RED PROOF (jump half):   in ManageScreenVM.Back, delete the `_nav.Origin != null` arm.
        // RED PROOF (browse half): in ManageScreenVM.Back, return `_nav.Origin` unconditionally.
        private static void CheckBackDistinguishesJumpFromBrowse(List<string> failures, StringBuilder log)
        {
            var vm = new ManageScreenVM();
            bool closed = false;
            vm.CloseRequested = () => closed = true;

            vm.EnterTab(ManageTabId.Build);
            if (vm.Nav == null || vm.Nav.Kind != ManageScreenKind.Grid || vm.Nav.Tab != ManageTabId.Build)
            {
                failures.Add(Tag + "[jump-returns-to-origin] EnterTab(Build) did not land on the BUILD grid " +
                             "(nav=" + Describe(vm.Nav) + ") - nothing below can be trusted");
                return;
            }

            // 1. BROWSE: grid -> detail -> back lands on the GRID (the ordinary case, which must
            //    not regress just because the special case exists).
            vm.OpenDetail(ManageTabId.Build, "alpha", null, null);
            var browsedDetail = vm.Nav;
            if (browsedDetail == null || browsedDetail.Kind != ManageScreenKind.Detail)
            {
                failures.Add(Tag + "[jump-returns-to-origin] OpenDetail did not produce a Detail screen " +
                             "(nav=" + Describe(vm.Nav) + ")");
                return;
            }

            // 2. JUMP: from that detail, a prerequisite CTA sends the player to a DIFFERENT detail,
            //    carrying the screen that sent them as its ORIGIN.
            vm.OpenDetail(ManageTabId.Build, "beta", null, browsedDetail);
            if (vm.Nav == null || vm.Nav.Origin == null)
                failures.Add(Tag + "[jump-returns-to-origin] a jump did not record an ORIGIN - the back " +
                             "stack remembers WHERE but not WHY, so ruling 28 cannot hold");

            vm.Back();
            if (vm.Nav == null || vm.Nav.Kind != ManageScreenKind.Detail ||
                !string.Equals(vm.Nav.ItemId, "alpha", StringComparison.Ordinal))
                failures.Add(Tag + "[jump-returns-to-origin] BACK from a screen entered BY A JUMP landed on " +
                             Describe(vm.Nav) + " instead of the ORIGIN detail 'alpha'. Owner ruling 28: the " +
                             "locked item's screen is where the player shops for goals - returning them to the " +
                             "grid drops them in a different branch with no memory of what they were doing");

            // 3. BROWSE AGAIN, from the origin we just returned to: back must now walk the TREE.
            vm.Back();
            if (vm.Nav == null || vm.Nav.Kind != ManageScreenKind.Grid)
                failures.Add(Tag + "[jump-returns-to-origin] BACK from a BROWSED detail landed on " +
                             Describe(vm.Nav) + " instead of its grid. Both directions must hold, or the " +
                             "case passes vacuously by always returning to an origin (ruling 28's own oracle)");

            // 4. BACK from a root grid leaves Manage. It must NEVER route through the retired
            //    four-tile launcher (WO-2001 acceptance criteria).
            vm.Back();
            if (!closed)
                failures.Add(Tag + "[jump-returns-to-origin] BACK from a root grid did not raise " +
                             "CloseRequested - the only other place it could go is the retired launcher");

            log.AppendLine("[jump-returns-to-origin] jump -> origin, browse -> grid, root -> close");
        }

        // ── CASE [opens-on-a-tab] ────────────────────────────────────────────
        // RED PROOF: make OpenDefaultScreen leave Nav null, or persist a tab it did not validate.
        private static void CheckTabsAndDefaultEntry(List<string> failures, StringBuilder log)
        {
            // A tab this build does not offer must NOT survive as the opening screen
            // (WO-2001 "Do not persist a stale tab that is no longer available because of feature
            // gating"). Seed the pref with RESEARCH, which a bare fixture cannot offer.
            int savedPref = PlayerPrefs.GetInt(ManageScreenVM.LastTabPrefKey, (int)ManageTabId.Build);
            PlayerPrefs.SetInt(ManageScreenVM.LastTabPrefKey, (int)ManageTabId.Research);

            var vm = new ManageScreenVM();
            vm.OpenDefaultScreen();

            // The suite must not leave the player's own last-used tab rewritten. Restored before
            // any assertion can early-return past it.
            PlayerPrefs.SetInt(ManageScreenVM.LastTabPrefKey, savedPref);

            if (vm.Nav == null)
            {
                failures.Add(Tag + "[opens-on-a-tab] Manage opened with no screen at all - the four-tile " +
                             "chooser is retired, so a null screen is a blank panel");
                return;
            }
            if (vm.Nav.Kind != ManageScreenKind.Grid)
                failures.Add(Tag + "[opens-on-a-tab] Manage opened on " + vm.Nav.Kind + " rather than a tab " +
                             "grid. WO-2001: Manage opens directly to BUILD / ARMY / RESEARCH");
            if (vm.AvailableTabIds == null || vm.AvailableTabIds.Count == 0)
                failures.Add(Tag + "[opens-on-a-tab] the model offers NO tabs - the header would render empty");
            else if (!Contains(vm.AvailableTabIds, vm.Nav.Tab))
                failures.Add(Tag + "[opens-on-a-tab] Manage opened on " + vm.Nav.Tab + ", which is not in " +
                             "AvailableTabIds - a stale gated tab was persisted");
            if (!Contains(vm.AvailableTabIds, ManageTabId.Build))
                failures.Add(Tag + "[opens-on-a-tab] BUILD is not offered. It is unconditional: without it a " +
                             "gated build can present a Manage screen with nothing on it");

            log.AppendLine("[opens-on-a-tab] entry lands on " + Describe(vm.Nav) +
                           "; tabs=" + vm.AvailableTabIds.Count);
        }

        // ── CASE [launcher-retired] + [one-art-loader] ───────────────────────
        private static void CheckSource(List<string> failures, StringBuilder log)
        {
            string panel = Read(PanelPath, failures);
            string vm = Read(VmPath, failures);
            string heart = Read(HeartPath, failures);
            if (panel == null || vm == null || heart == null) return;

            // RED PROOF: restore `private void ShowLauncher()` to ManageScreenPanel.cs.
            if (panel.Contains("private void ShowLauncher("))
                failures.Add(Tag + "[launcher-retired] ManageScreenPanel still carries ShowLauncher. It was the " +
                             "ONLY path that put the four-tile chooser on screen and BACK was its only caller; " +
                             "WO-2001 retires the chooser and states BACK must never route through it");
            // RED PROOF: re-point the BACK button's callback at anything but the model's graph.
            if (!panel.Contains("new Vector2(0.205f, 0.965f), OnBackPressed") || !panel.Contains("_vm.Back()"))
                failures.Add(Tag + "[launcher-retired] the Manage BACK control is not wired to the MODEL's screen " +
                             "graph. Canon 9: the View does not decide destinations, and ruling 28 makes returns a " +
                             "destination decision");
            // RED PROOF: delete the ManageWorkspacePanel construction.
            if (!panel.Contains("new DeNelle.Core.Manage.ManageWorkspacePanel(_workspaceHost)") ||
                !panel.Contains("_workspace.Bind(_vm.ComposeWorkspace())"))
                failures.Add(Tag + "[launcher-retired] Manage does not render through the ONE common workspace " +
                             "renderer - canon 10 asks for one presentation path, not a second UI system");

            // RED PROOF: put a Resources.Load body back into LoadManageBuildingSpriteAt.
            if (!panel.Contains("=> DeNelle.Core.Manage.ManageArt.LoadSprite(resourceKey)"))
                failures.Add(Tag + "[one-art-loader] ManageScreenPanel.LoadManageBuildingSpriteAt is not a forwarder " +
                             "to ManageArt.LoadSprite. Two implementations of one loader is the duplicated state " +
                             "CLAUDE.md 2 / 5 / 16 records three times over");
            if (panel.Contains("ManageBuildingSpriteCache"))
                failures.Add(Tag + "[one-art-loader] the Village-side sprite cache is back beside ManageArt's - " +
                             "one key would then resolve twice with two different miss behaviours");
            if (!heart.Contains("DeNelle.Core.Manage.ManageArt.LoadSprite(key)"))
                failures.Add(Tag + "[one-art-loader] HeartPanel does not load through ManageArt - HeartPanel.cs " +
                             "already states the rule the Heart must not become the one art route with its own loader");

            // RED PROOF: delete the Origin field from ManageNavEntry.
            if (!vm.Contains("public ManageNavEntry Origin"))
                failures.Add(Tag + "[launcher-retired] ManageNavEntry carries no Origin - ruling 28's back stack " +
                             "would be a plain screen history, which returns the player to the grid every time");

            log.AppendLine("[launcher-retired]/[one-art-loader] source shapes present");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static bool Contains(IReadOnlyList<ManageTabId> list, ManageTabId id)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == id) return true;
            return false;
        }

        private static string Describe(ManageNavEntry nav)
        {
            if (nav == null) return "<null>";
            return nav.Kind + ":" + nav.Tab + ":" + (nav.ItemId ?? nav.SchoolId ?? "-") +
                   (nav.Origin != null ? " (from " + (nav.Origin.ItemId ?? "grid") + ")" : "");
        }

        private static string Read(string rel, List<string> failures)
        {
            string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(abs))
            {
                failures.Add(Tag + " source file missing: " + rel + " - the source half is reported as a " +
                             "FAILURE rather than passing vacuously");
                return null;
            }
            return File.ReadAllText(abs);
        }
    }
}
