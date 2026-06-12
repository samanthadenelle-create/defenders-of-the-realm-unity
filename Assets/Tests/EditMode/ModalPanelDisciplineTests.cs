// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// ModalPanelDisciplineTests (EditMode) — regression guard for the MainCastle_Hall
// "stuck Help menu" cluster (Close traps the player / Dev tools goes nowhere /
// a stale menu layers over the dialogue portrait).
// -----------------------------------------------------------------------------
// ROOT CAUSE the playtest hit: the gear -> Help menu (HelpMenu) and the
// Settings -> Dev tools overlay (AdminOverlay) were the only full-screen modal
// panels that did NOT route through the DEF-212 single-modal arbiter
// (DeNelle.Core.UI.PanelManager). Because their backdrops are pickingMode.Position
// (they eat input) and they never registered, they stacked over open content and
// an invisible-but-pickable backdrop could trap the player.
//
// This suite pins the fix on two axes:
//   1. BEHAVIOUR — PanelManager genuinely enforces "only one modal open at a time"
//      and invokes the displaced panel's Close callback. (If this invariant ever
//      breaks, every panel that relies on it regresses.)
//   2. STRUCTURE — HelpMenu.cs and AdminOverlay.cs actually call PanelManager
//      Register + NotifyOpened/NotifyClosed. This is the check that would have
//      FAILED before the fix: the original files never touched PanelManager, so a
//      future edit that drops the registration (re-introducing the stuck menu)
//      trips this test. The test asmdef can't reference DeNelle.HUD, so the
//      structural assertion reads the source files directly.
// =============================================================================

using System.IO;
using NUnit.Framework;
using DeNelle.Core.UI;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ModalPanelDisciplineTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clear any panel left "open" so tests don't leak arbiter state.
            PanelManager.CloseOpen();
        }

        // ── 1. BEHAVIOUR: single-modal invariant ────────────────────────────────

        [Test]
        public void OpeningSecondPanel_ClosesTheFirst()
        {
            bool firstClosed = false;
            bool firstOpen = true;
            var first = PanelManager.Register("First", () => { firstClosed = true; firstOpen = false; }, () => firstOpen);
            var second = PanelManager.Register("Second", () => { }, () => false);

            PanelManager.NotifyOpened(first);
            Assert.IsTrue(PanelManager.AnyOpen, "First panel should register as open.");
            Assert.AreEqual("First", PanelManager.OpenPanelName);

            // Opening a different panel MUST close the one already open — this is the
            // discipline the stuck-Help-menu fix depends on.
            PanelManager.NotifyOpened(second);
            Assert.IsTrue(firstClosed, "Opening the second panel must invoke the first panel's Close callback.");
            Assert.AreEqual("Second", PanelManager.OpenPanelName, "The arbiter should now track the second panel.");
        }

        [Test]
        public void ClosingOpenPanel_ClearsArbiterState()
        {
            bool open = true;
            var handle = PanelManager.Register("Solo", () => open = false, () => open);

            PanelManager.NotifyOpened(handle);
            Assert.IsTrue(PanelManager.AnyOpen);

            PanelManager.NotifyClosed(handle);
            Assert.IsFalse(PanelManager.AnyOpen,
                "After a panel closes, no modal must remain registered open — otherwise an " +
                "invisible backdrop lingers and traps input.");
            Assert.IsNull(PanelManager.OpenPanelName);
        }

        [Test]
        public void CloseOpen_InvokesCloseCallback()
        {
            bool closed = false;
            var handle = PanelManager.Register("Trapped", () => closed = true, () => true);
            PanelManager.NotifyOpened(handle);

            // A global "back"/ESC must be able to dismiss whatever modal is up.
            PanelManager.CloseOpen();
            Assert.IsTrue(closed, "CloseOpen must invoke the open panel's Close callback so it can never get stuck.");
            Assert.IsFalse(PanelManager.AnyOpen);
        }

        // ── 2. STRUCTURE: the menu panels participate in the arbiter ─────────────

        [Test]
        public void HelpMenu_RoutesThroughPanelManager()
        {
            string src = ReadModuleSource("HUD/HelpMenu.cs");
            Assert.IsTrue(src.Contains("PanelManager.Register"),
                "HelpMenu must register with PanelManager — without it the gear/Help menu stacks over " +
                "content and its pickable backdrop traps the player (the MainCastle_Hall bug).");
            Assert.IsTrue(src.Contains("PanelManager.NotifyOpened") && src.Contains("PanelManager.NotifyClosed"),
                "HelpMenu must announce open/close to PanelManager so opening it closes other panels and " +
                "closing it clears the modal slot.");
        }

        [Test]
        public void AdminOverlay_RoutesThroughPanelManager()
        {
            string src = ReadModuleSource("HUD/AdminOverlay.cs");
            // AdminOverlay aliases PanelManager as PanelMgr; accept either spelling.
            Assert.IsTrue(src.Contains("PanelManager.Register") || src.Contains("PanelMgr.Register"),
                "AdminOverlay (the Dev tools overlay) must register with PanelManager so it participates " +
                "in single-modal discipline.");
            Assert.IsTrue(
                (src.Contains("PanelManager.NotifyOpened") || src.Contains("PanelMgr.NotifyOpened")) &&
                (src.Contains("PanelManager.NotifyClosed") || src.Contains("PanelMgr.NotifyClosed")),
                "AdminOverlay must announce open/close to PanelManager.");
        }

        [Test]
        public void AdminOverlay_SortsAboveHelpMenu()
        {
            // "Dev tools goes nowhere" was the admin overlay opening BENEATH the Help panel
            // (HelpMenu sortingOrder 2700, AdminOverlay a stale 170). Guard the ordering so a
            // future edit can't bury the dev panel again.
            int help = ExtractSortingOrder(ReadModuleSource("HUD/HelpMenu.cs"));
            int admin = ExtractSortingOrder(ReadModuleSource("HUD/AdminOverlay.cs"));
            Assert.Greater(admin, help,
                $"AdminOverlay sortingOrder ({admin}) must exceed HelpMenu ({help}) so 'Dev tools' renders " +
                "on top instead of disappearing behind the Help panel.");
        }

        [Test]
        public void Inventory_RoutesThroughPanelManager()
        {
            string src = ReadModuleSource("Village/Hero/HeroInventoryController.cs");
            Assert.IsTrue(src.Contains("PanelManager.Register") || src.Contains("PanelMgr.Register"),
                "HeroInventoryController (the BAG inventory) must register with PanelManager so it joins " +
                "single-modal discipline (no stuck pickable backdrop in the hub).");
            Assert.IsTrue(
                (src.Contains("PanelManager.NotifyOpened") || src.Contains("PanelMgr.NotifyOpened")) &&
                (src.Contains("PanelManager.NotifyClosed") || src.Contains("PanelMgr.NotifyClosed")),
                "HeroInventoryController must announce open/close to PanelManager.");
        }

        [Test]
        public void BagWiring_SelfHeals_SoInventoryAlwaysOpens()
        {
            // The "BAG opens nothing" bug: HeroEquipHud wired the InventoryRequested event ONCE with no
            // retry, so in the castle (HUD spawned in the same scene-load, not ready at Start) the bind
            // silently no-op'd and BAG fired into the void. The fix re-attempts until the wire lands —
            // the same self-heal idiom the working BUILD/TALK bridges use. Guard the retry exists.
            string src = ReadModuleSource("Village/Hero/HeroEquipHud.cs");
            Assert.IsTrue(src.Contains("_wired"),
                "HeroEquipHud must track whether the inventory wire landed and RETRY until it does — " +
                "without the retry the BAG button opens nothing in the castle hub.");
            Assert.IsTrue(src.Contains("void Update"),
                "HeroEquipHud must re-attempt the wire each frame until bound (Update self-heal), " +
                "matching the working BUILD/TALK bridges.");
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static string ReadModuleSource(string relativePath)
        {
            // Application.dataPath -> <project>/Assets ; modules live under Assets/_Modules.
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Modules", relativePath);
            Assert.IsTrue(File.Exists(path), "Expected source file not found: " + path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Pulls the FIRST `_document.sortingOrder = N;` literal from a source file. Both
        /// HelpMenu and AdminOverlay set their document sortingOrder exactly once at build.
        /// </summary>
        private static int ExtractSortingOrder(string src)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                src, @"sortingOrder\s*=\s*(\d+)");
            Assert.IsTrue(m.Success, "Could not find a sortingOrder literal in source.");
            return int.Parse(m.Groups[1].Value);
        }
    }
}
