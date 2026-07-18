// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// TowerUpgradeVMTests (EditMode) — MVVM Silo C §2c permission gate.
// -----------------------------------------------------------------------------
// Locks TowerUpgradeButton's economy/level/cost logic in TowerUpgradeVM: the
// label + interactable projection, the Wood-pool affordability gate, the maxed
// edge, and the Upgrade command (routes to the target's TryUpgrade + re-projects +
// raises Changed). Over a fake target + fake IEconomy (no scene Tower).
// =============================================================================

using NUnit.Framework;
using DeNelle.Village.UI;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class TowerUpgradeVMTests
    {
        [Test]
        public void no_target_reads_upgrade_and_is_disabled()
        {
            var vm = new TowerUpgradeVM(new FakeEconomy { Wood = 999 });
            Assert.That(vm.ButtonText, Is.EqualTo("Upgrade"));
            Assert.That(vm.Interactable, Is.False);
        }

        [Test]
        public void uninitialised_target_is_disabled()
        {
            var vm = new TowerUpgradeVM(new FakeEconomy { Wood = 999 });
            vm.SetTarget(new FakeTowerUpgradeTarget { HasData = false });
            Assert.That(vm.ButtonText, Is.EqualTo("Upgrade"));
            Assert.That(vm.Interactable, Is.False);
        }

        [Test]
        public void affordable_upgrade_projects_label_and_enables()
        {
            var vm = new TowerUpgradeVM(new FakeEconomy { Wood = 100 });
            vm.SetTarget(new FakeTowerUpgradeTarget { HasData = true, CurrentLevel = 1, NextUpgradeCost = 50 });
            Assert.That(vm.NextLevel, Is.EqualTo(2));
            Assert.That(vm.Cost, Is.EqualTo(50));
            Assert.That(vm.ButtonText, Is.EqualTo("Upgrade (L2)  50"));
            Assert.That(vm.Interactable, Is.True);
        }

        [Test]
        public void unaffordable_upgrade_is_disabled()
        {
            var vm = new TowerUpgradeVM(new FakeEconomy { Wood = 10 });
            vm.SetTarget(new FakeTowerUpgradeTarget { HasData = true, CurrentLevel = 1, NextUpgradeCost = 50 });
            Assert.That(vm.Interactable, Is.False, "Wood 10 < cost 50");
        }

        [Test]
        public void maxed_tower_shows_max_level_and_is_disabled()
        {
            var vm = new TowerUpgradeVM(new FakeEconomy { Wood = 999 });
            // CurrentLevel == Tower.MaxLevel (3) -> next would be 4 -> maxed.
            vm.SetTarget(new FakeTowerUpgradeTarget { HasData = true, CurrentLevel = 3, NextUpgradeCost = 50 });
            Assert.That(vm.ButtonText, Is.EqualTo("Max Level"));
            Assert.That(vm.Interactable, Is.False);
        }

        [Test]
        public void upgrade_command_calls_target_and_raises_changed()
        {
            var vm = new TowerUpgradeVM(new FakeEconomy { Wood = 100 });
            var target = new FakeTowerUpgradeTarget { HasData = true, CurrentLevel = 1, NextUpgradeCost = 50 };
            vm.SetTarget(target);

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Upgrade();

            Assert.That(target.UpgradeCalls, Is.EqualTo(1), "routed to the cost-enforced TryUpgrade");
            Assert.That(vm.NextLevel, Is.EqualTo(3), "re-projected after the level rose to 2");
            Assert.That(changed, Is.EqualTo(1));
        }
    }
}
