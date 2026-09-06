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

            // ⛔ THE "NO LAUNCHER" HALF OF THIS CASE IS RETIRED - 2026-09-06, WO-1443.
            // It used to FAIL on `private void ShowLauncher(` existing at all. The owner's mockup
            // (docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png) panel 1 IS a hub - three cards,
            // BUILD / ARMY / RESEARCH, CLOSE beneath - and CAPTURE_LOOP_GOAL.md 3.0c item 2 states
            // in those words that this supersedes WO-2001's launcher retirement for that screen.
            // WHAT WO-2001 WAS ACTUALLY DEFENDING still holds and is still pinned below: a REQUIRED
            // chooser in front of narrow rails. It is a different object now - three cards the owner
            // drew, in front of full-width grids - and it is what lets the tab row leave the
            // workspace body and give the grid back 132px.
            // The case is not deleted: it still proves the chooser is not FOUR tiles and that
            // Manage renders through the one workspace renderer.
            if (panel.Contains("ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"))
                failures.Add(Tag + "[launcher-retired] the hub is back to FOUR cards including Defense. " +
                             "WO-2001 merged Defense into BUILD (ShowOperational maps both onto " +
                             "ManageTabId.Build), so a Defense card opens the same destination as the " +
                             "Build card - a fourth place to go that is not a fourth place");
            // RED PROOF: re-point the BACK button's callback at anything but the model's graph.
            //
            // ⚠ PIN MOVED 2026-09-06 (WO-1443, the mockup round), WITH THE RULING. It used to pin
            // the literal seat `new Vector2(0.205f, 0.965f), OnBackPressed` - a 0.035-0.205 word
            // slab reading "BACK". The owner's mockup
            // (docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png) draws a `<-` ARROW at top-LEFT on
            // all eight numbered panels, and CAPTURE_LOOP_GOAL.md 3.0b states it; the mockup wins
            // over a coordinate written before it existed. What this case actually defends - that
            // BACK is wired to the MODEL's screen graph and not to a destination the View picked -
            // is unchanged, so the pin now asserts the CONTROL and its WIRING, not its rectangle.
            // A pin on a rectangle forbids the next layout ruling; a pin on the wiring does not.
            // ⚠ PIN CORRECTED AGAIN 2026-09-06, AND THE MISTAKE WAS THIS CASE'S OWN.
            // The note above says "a pin on a rectangle forbids the next layout ruling; a pin on the
            // wiring does not" - and then the replacement pinned the arrow's PARENT
            // (`chrome.content.transform`). Round 7 moved the arrow into BuildTabs and parented it
            // to `_tabsHost`, because BuildTabs destroys every child of that host on entry and an
            // arrow built once at chrome time survived exactly one frame. The pin went red on the
            // parent token while BACK was, and is, correctly wired.
            // VERIFIED AT SOURCE THIS ROUND, not assumed: ManageScreenPanel.cs:2035-2037 builds the
            // "<-" face with OnBackPressed, and OnBackPressed (:991-996) closes the drawer first,
            // then calls _vm.Back(). The MODEL owns the graph, which is the whole of canon 9 here.
            // So the case now pins ONLY the wiring - the face exists, its callback is OnBackPressed,
            // and that method defers to the model. Never the parent, never the rectangle.
            if (!panel.Contains("\"<-\"") ||
                !panel.Contains("OnBackPressed") || !panel.Contains("_vm.Back()"))
                failures.Add(Tag + "[launcher-retired] the Manage BACK control is not wired to the MODEL's screen " +
                             "graph. Canon 9: the View does not decide destinations, and ruling 28 makes returns a " +
                             "destination decision");
            // RED PROOF: delete the ManageWorkspacePanel construction.
            //
            // ⚠ PIN MOVED 2026-09-06, WO-1443 section 1, WITH THE RULING THAT MOVED IT - not deleted
            // to go green. This case used to require the single expression
            // `_workspace.Bind(_vm.ComposeWorkspace())`. The owner's ruling ("remove the manage army
            // and sub line replace the manage top") makes the host bind the model's breadcrumb into
            // the PANEL TITLE, so RenderWorkspace must hold the composed VM in a local:
            //     var workspaceVm = _vm.ComposeWorkspace();
            //     ApplyWorkspaceTitle(workspaceVm.HeaderTitle);
            //     _workspace.Bind(workspaceVm);
            // The INVARIANT this case defends is unchanged and is now asserted in two halves: Manage
            // still COMPOSES through the model and still BINDS the one common renderer. Composing
            // twice per paint would also be wrong, so the local is the correct shape, not a
            // concession. The heading half is pinned separately by ManageOneHeadingRegression.
            if (!panel.Contains("new DeNelle.Core.Manage.ManageWorkspacePanel(_workspaceHost)") ||
                !panel.Contains("_vm.ComposeWorkspace()") ||
                !panel.Contains("_workspace.Bind(workspaceVm)"))
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
