// =============================================================================
// ShopVMTests (EditMode) — WO-431 permission gate for the shop MVVM slice.
// -----------------------------------------------------------------------------
// Locks the behavior that was MOVED out of ShopPanel (the View) into the pure
// ShopVM, so the View swap is safe only while these stay green
// (ARCHITECTURE_PRINCIPLES.md §2c). Uses a FAKE IEconomy so the VM is exercised
// with NO scene, NO EconomyService singleton, NO GameState.
//
// Asserts:
//   • Buy items are non-empty and each gear row's Price matches the catalog cost.
//   • Affordable flips with the fake wallet balance.
//   • Buy on an affordable selection spends + raises Changed.
//   • Buy on an unaffordable selection does NOT spend + sets the can't-afford status.
//   • SetMode swaps the Items list and resets Selection.
//   • Dispose() unsubscribes (no callback after dispose).
//
// EditMode never runs Awake(), so VillageInventory.Instance may be null; the Buy
// path null-guards it, so a successful buy still spends through the fake economy.
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ShopVMTests
    {
        // ── Fake economy: a controllable coin balance + transaction bookkeeping ──
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

            public ResourceCost Grant(ResourceCost amount)
            {
                Coins += amount.Coins; Wood += amount.Wood; Iron += amount.Iron;
                Food += amount.Food; Crystals += amount.Crystals;
                GrantCalls++;
                OnChanged?.Invoke(new ResourceSnapshot(Wood, Food, Iron, Crystals));
                // Uncapped fake: every requested unit lands, so the applied basket IS the request.
                return amount;
            }
        }

        private static ShopVM NewVm(FakeEconomy eco, string vendor = "")
        {
            // Unknown/empty vendor -> general default (Weapon|Armor|Potion), so Buy has the
            // widest stock and a never-empty list regardless of the catalog's contents.
            return new ShopVM(vendor, eco);
        }

        [Test]
        public void buy_items_are_non_empty_and_prices_match_catalog()
        {
            var eco = new FakeEconomy { Coins = 100000 };
            using var vm = NewVm(eco);

            Assert.That(vm.Mode, Is.EqualTo(ShopMode.Buy));
            Assert.That(vm.Items.Count, Is.GreaterThan(0), "Buy list must never be empty (potions are always stocked).");

            foreach (var item in vm.Items)
            {
                var w = GearCatalog.FindWeapon(item.Id);
                if (w != null)
                {
                    Assert.That(item.Price, Is.EqualTo(GearCatalog.GetBuyCost(w).Coins),
                        $"weapon '{item.Id}' price must match catalog buy cost");
                    continue;
                }
                var a = GearCatalog.FindArmor(item.Id);
                if (a != null)
                {
                    Assert.That(item.Price, Is.EqualTo(GearCatalog.GetBuyCost(a).Coins),
                        $"armor '{item.Id}' price must match catalog buy cost");
                }
                // potions: price is the fixed 8/12 gold the VM stocks (asserted indirectly by affordability tests).
            }
        }

        [Test]
        public void affordable_flips_with_fake_wallet_balance()
        {
            // Rich wallet: at least one row affordable.
            var rich = new FakeEconomy { Coins = 1000000 };
            using (var vmRich = NewVm(rich))
            {
                bool anyAffordable = false;
                foreach (var i in vmRich.Items) if (i.Affordable) { anyAffordable = true; break; }
                Assert.That(anyAffordable, Is.True, "with a huge balance, at least one row must read affordable");
            }

            // Broke wallet: nothing priced > 0 is affordable.
            var broke = new FakeEconomy { Coins = 0 };
            using (var vmBroke = NewVm(broke))
            {
                foreach (var i in vmBroke.Items)
                    if (i.Price > 0)
                        Assert.That(i.Affordable, Is.False, $"with 0 gold, priced row '{i.Id}' must read unaffordable");
            }
        }

        [Test]
        public void buy_on_affordable_spends_and_raises_changed()
        {
            var eco = new FakeEconomy { Coins = 1000000 };
            using var vm = NewVm(eco);

            // Pick an affordable, priced row.
            string id = null;
            foreach (var i in vm.Items) if (i.Price > 0 && i.Affordable) { id = i.Id; break; }
            Assert.That(id, Is.Not.Null, "test needs at least one affordable priced row");

            int changed = 0;
            vm.Changed += () => changed++;

            vm.Select(id);
            int spendsBefore = eco.SpendCalls;
            vm.Buy();

            Assert.That(eco.SpendCalls, Is.EqualTo(spendsBefore + 1), "an affordable Buy must spend exactly once");
            Assert.That(changed, Is.GreaterThan(0), "Buy must raise Changed so the View re-renders");
        }

        [Test]
        public void buy_on_unaffordable_does_not_spend_and_sets_status()
        {
            var eco = new FakeEconomy { Coins = 0 };
            using var vm = NewVm(eco);

            // Pick a priced (therefore unaffordable at 0 gold) row.
            string id = null;
            foreach (var i in vm.Items) if (i.Price > 0) { id = i.Id; break; }
            Assert.That(id, Is.Not.Null, "test needs at least one priced row");

            vm.Select(id);
            vm.Buy();

            Assert.That(eco.SpendCalls, Is.EqualTo(0), "an unaffordable Buy must not spend");
            Assert.That(vm.Status, Does.Contain("Not enough"),
                "an unaffordable Buy must set the can't-afford status");
        }

        [Test]
        public void buy_with_nothing_selected_sets_select_first_status()
        {
            var eco = new FakeEconomy { Coins = 1000000 };
            using var vm = NewVm(eco);
            // No Select() call.
            vm.Buy();
            Assert.That(vm.Status, Does.Contain("Select an item"),
                "Buy with no selection must prompt to select an item first");
        }

        [Test]
        public void set_mode_swaps_items_and_resets_selection()
        {
            var eco = new FakeEconomy { Coins = 1000000 };
            using var vm = NewVm(eco);

            // Select a Buy row.
            string id = null;
            foreach (var i in vm.Items) { id = i.Id; break; }
            Assert.That(id, Is.Not.Null);
            vm.Select(id);
            Assert.That(vm.SelectedId, Is.EqualTo(id));

            // Switch to SELL: selection resets, list is the (empty in EditMode) sell list, label flips.
            vm.SetMode(ShopMode.Sell);
            Assert.That(vm.Mode, Is.EqualTo(ShopMode.Sell));
            Assert.That(vm.SelectedId, Is.Null, "SetMode must reset selection");
            Assert.That(vm.ActionLabel, Is.EqualTo("Sell"));

            // Switch back to BUY: list repopulates.
            vm.SetMode(ShopMode.Buy);
            Assert.That(vm.Mode, Is.EqualTo(ShopMode.Buy));
            Assert.That(vm.ActionLabel, Is.EqualTo("Purchase"));
            Assert.That(vm.Items.Count, Is.GreaterThan(0), "BUY list must repopulate after SetMode");
        }

        [Test]
        public void create_default_builds_the_same_buy_projection()
        {
            // DI-in-Open hoist (UI_MVVM_MIGRATION_PLAN §1): ShopVM.CreateDefault resolves
            // EconomyService.Instance ITSELF (null in EditMode -> empty wallet), so its projection
            // matches a null-economy direct construction. Locks the factory so the View can drop the
            // direct `EconomyService.Instance` read. Also asserts the icon role/id mapping is intact.
            using var direct = new ShopVM("", null);
            using var viaFactory = ShopVM.CreateDefault("");

            Assert.That(viaFactory.Items.Count, Is.EqualTo(direct.Items.Count),
                "CreateDefault must build the same Buy list as the direct constructor");
            Assert.That(viaFactory.Items.Count, Is.GreaterThan(0));

            foreach (var it in viaFactory.Items)
            {
                bool knownRole = it.IconRole == ShopVM.IconRoleWeapon
                              || it.IconRole == ShopVM.IconRoleArmor
                              || it.IconRole == ShopVM.IconRolePotion
                              || it.IconRole == ShopVM.IconRoleAccessory;
                Assert.That(knownRole, Is.True, $"row '{it.Id}' must carry a known icon role for the seam");
                Assert.That(it.IconName, Is.EqualTo(it.Id), "the icon-name key is the item id (seam contract)");
            }
        }

        [Test]
        public void dispose_unsubscribes_no_callback_after_dispose()
        {
            var eco = new FakeEconomy { Coins = 1000 };
            var vm = NewVm(eco);

            int changed = 0;
            vm.Changed += () => changed++;
            vm.Dispose();

            int before = changed;
            // Economy change after Dispose must NOT reach the VM's (now-cleared) Changed subscribers.
            eco.Grant(new ResourceCost(coins: 50));
            Assert.That(changed, Is.EqualTo(before),
                "after Dispose the VM must not raise Changed from economy events (handler unsubscribed)");
        }
    }
}
