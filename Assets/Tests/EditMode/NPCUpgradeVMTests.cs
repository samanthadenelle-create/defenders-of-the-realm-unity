// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// NPCUpgradeVMTests (EditMode) — WO-744 permission gate for the world-space NPC
// upgrade station's economy seam. Locks the transaction MOVED out of
// NPCUpgradeStation (the View) into NPCUpgradeVM, so the View no longer names
// EconomyService.Instance (ARCHITECTURE_PRINCIPLES.md §2c). Uses a FAKE IEconomy
// so the VM is exercised with NO scene / NO EconomyService singleton.
//
// Asserts:
//   * TryPurchaseUpgrade spends atomically when affordable.
//   * TryPurchaseUpgrade returns false + does NOT spend when short.
//   * TryPurchaseUpgrade returns false with a null economy.
//   * GrantFirstHarvestBonus adds +5 Wood / +5 Food.
// =============================================================================

using System;
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class NPCUpgradeVMTests
    {
        private sealed class FakeEconomy : IEconomy
        {
            public int Coins { get; set; }
            public int Wood { get; set; }
            public int Iron { get; set; }
            public int Food { get; set; }
            public int Crystals { get; set; }

            public int SpendCalls;
            public int GrantCalls;

            public event Action<ResourceSnapshot> OnChanged;

            public bool CanAfford(ResourceCost cost) =>
                Coins >= cost.Coins && Wood >= cost.Wood && Iron >= cost.Iron &&
                Food >= cost.Food && Crystals >= cost.Crystals;

            public bool TrySpend(ResourceCost cost)
            {
                if (!CanAfford(cost)) return false;
                Coins -= cost.Coins; Wood -= cost.Wood; Iron -= cost.Iron;
                Food -= cost.Food; Crystals -= cost.Crystals;
                SpendCalls++;
                OnChanged?.Invoke(new ResourceSnapshot(Wood, Food, Iron, Crystals));
                return true;
            }

            public void Grant(ResourceCost amount)
            {
                Coins += amount.Coins; Wood += amount.Wood; Iron += amount.Iron;
                Food += amount.Food; Crystals += amount.Crystals;
                GrantCalls++;
                OnChanged?.Invoke(new ResourceSnapshot(Wood, Food, Iron, Crystals));
            }
        }

        [Test]
        public void try_purchase_spends_when_affordable()
        {
            var eco = new FakeEconomy { Wood = 100, Food = 100, Iron = 100 };
            var vm = new NPCUpgradeVM(eco);

            bool ok = vm.TryPurchaseUpgrade(new ResourceCost(wood: 30, food: 20, iron: 10));

            Assert.That(ok, Is.True, "an affordable upgrade must spend");
            Assert.That(eco.SpendCalls, Is.EqualTo(1));
            Assert.That(eco.Wood, Is.EqualTo(70));
            Assert.That(eco.Food, Is.EqualTo(80));
            Assert.That(eco.Iron, Is.EqualTo(90));
        }

        [Test]
        public void try_purchase_fails_when_short_no_spend()
        {
            var eco = new FakeEconomy { Wood = 5, Food = 5, Iron = 5 };
            var vm = new NPCUpgradeVM(eco);

            bool ok = vm.TryPurchaseUpgrade(new ResourceCost(wood: 30, food: 20, iron: 10));

            Assert.That(ok, Is.False, "an unaffordable upgrade must not spend");
            Assert.That(eco.SpendCalls, Is.EqualTo(0));
            Assert.That(eco.Wood, Is.EqualTo(5), "no Wood deducted on a failed spend");
        }

        [Test]
        public void try_purchase_with_null_economy_returns_false()
        {
            var vm = new NPCUpgradeVM(null);
            Assert.That(vm.TryPurchaseUpgrade(new ResourceCost(wood: 1)), Is.False,
                "a missing economy must fail closed (never mint a free upgrade)");
        }

        [Test]
        public void grant_first_harvest_bonus_adds_5_wood_5_food()
        {
            var eco = new FakeEconomy { Wood = 10, Food = 10 };
            var vm = new NPCUpgradeVM(eco);

            vm.GrantFirstHarvestBonus();

            Assert.That(eco.GrantCalls, Is.EqualTo(1));
            Assert.That(eco.Wood, Is.EqualTo(15), "+5 Wood");
            Assert.That(eco.Food, Is.EqualTo(15), "+5 Food");
        }
    }
}
