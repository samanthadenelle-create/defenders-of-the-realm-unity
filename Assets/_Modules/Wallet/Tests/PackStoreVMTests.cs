// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// PackStoreVMTests (EditMode) — WO-744 permission gate for the pack-store's
// game-state seam. Locks the ownership + entitlement grant MOVED out of PackStore
// (the View) into PackStoreVM, so the View no longer names GameStateService /
// FindFirstObjectOfType (ARCHITECTURE_PRINCIPLES.md §2c). Exercises the VM over a
// plain injected GameState — NO scene, NO GameStateService singleton.
//
// Asserts (money/reward path unchanged):
//   * ApplyPackContents tops up crystals/food/coins + records the pack SKU + its
//     cosmetic SKUs as owned.
//   * IsOwned reflects the grant; IsOwned is false for an unknown SKU.
//   * Ownership is idempotent (RecordOwned dedups) — the purchase re-grant guard.
//   * A null state does not throw (self-reports the lost entitlement).
// =============================================================================

using System;
using NUnit.Framework;
using DeNelle.Core.State;
using DeNelle.Wallet;

namespace DeNelle.Wallet.Tests
{
    [TestFixture]
    public class PackStoreVMTests
    {
        private static PackDef MakePack(string sku, int crystals, int food, int coins, params string[] cosmetics)
        {
            var pack = new PackDef
            {
                Sku = sku,
                Contents = new PackContents
                {
                    Economy = new PackEconomy { Crystals = crystals, Food = food, Coins = coins },
                }
            };
            if (cosmetics != null)
                foreach (var c in cosmetics) pack.Contents.Cosmetics.Add(c);
            return pack;
        }

        [Test]
        public void apply_pack_grants_economy_and_records_owned()
        {
            var state = new GameState();
            int crystals0 = state.Resources.Crystals;
            int food0 = state.Resources.Food;
            int coins0 = state.Resources.Coins;

            var vm = new PackStoreVM(() => state);
            var pack = MakePack("pack-x", crystals: 100, food: 50, coins: 25, "cos-a", "cos-b");

            vm.ApplyPackContents(pack);

            Assert.That(state.Resources.Crystals, Is.EqualTo(crystals0 + 100), "crystals topped up");
            Assert.That(state.Resources.Food, Is.EqualTo(food0 + 50), "food topped up");
            Assert.That(state.Resources.Coins, Is.EqualTo(coins0 + 25), "coins topped up");

            Assert.That(state.OwnedItemIds, Does.Contain("pack-x"), "pack SKU recorded owned");
            Assert.That(state.OwnedItemIds, Does.Contain("cos-a"), "cosmetic SKU recorded owned");
            Assert.That(state.OwnedItemIds, Does.Contain("cos-b"), "cosmetic SKU recorded owned");
            Assert.That(vm.IsOwned("pack-x"), Is.True, "IsOwned reflects the grant");
        }

        [Test]
        public void is_owned_is_false_for_unknown_sku()
        {
            var state = new GameState();
            var vm = new PackStoreVM(() => state);
            Assert.That(vm.IsOwned("never-bought"), Is.False);
        }

        [Test]
        public void ownership_grant_is_idempotent()
        {
            var state = new GameState();
            var vm = new PackStoreVM(() => state);
            var pack = MakePack("pack-x", crystals: 10, food: 0, coins: 0);

            vm.ApplyPackContents(pack);
            vm.ApplyPackContents(pack);   // re-grant

            int occurrences = state.OwnedItemIds.FindAll(s => s == "pack-x").Count;
            Assert.That(occurrences, Is.EqualTo(1),
                "RecordOwned must dedup — the SKU is recorded once even on a re-grant");
        }

        [Test]
        public void apply_with_null_state_does_not_throw()
        {
            var vm = new PackStoreVM(() => null);
            var pack = MakePack("pack-x", crystals: 10, food: 0, coins: 0);
            Assert.DoesNotThrow(() => vm.ApplyPackContents(pack),
                "no GameState must self-report, never throw (the payment already settled)");
        }
    }
}
