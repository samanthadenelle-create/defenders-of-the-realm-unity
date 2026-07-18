// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// BuildMenuVMTests (EditMode) — MVVM Silo C §2c permission gate.
// -----------------------------------------------------------------------------
// Locks BuildMenu's crystal-balance read in BuildMenuVM: Crystals sources the
// IEconomy.Crystals store, falls back to the standalone value when no economy,
// and the shared tower list VM is wired. Over a fake IEconomy (the injectable
// ctor — CreateDefault's service/scene resolution is not exercised here).
// =============================================================================

using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.UI;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class BuildMenuVMTests
    {
        private static PlacedTowerListVM EmptyTowers()
            => new PlacedTowerListVM(() => new Tower[0]);

        [Test]
        public void crystals_read_from_the_economy()
        {
            var vm = new BuildMenuVM(new FakeEconomy { Crystals = 42 }, EmptyTowers(), null, fallbackCrystals: 7, onClose: null);
            Assert.That(vm.Crystals, Is.EqualTo(42));
        }

        [Test]
        public void crystals_fall_back_when_no_economy()
        {
            var vm = new BuildMenuVM(null, EmptyTowers(), null, fallbackCrystals: 7, onClose: null);
            Assert.That(vm.Crystals, Is.EqualTo(7));
        }

        [Test]
        public void the_shared_tower_list_is_wired()
        {
            var towers = EmptyTowers();
            var vm = new BuildMenuVM(new FakeEconomy(), towers, null, 0, null);
            Assert.That(vm.Towers, Is.SameAs(towers));
        }

        [Test]
        public void dispose_disposes_the_tower_list_without_throwing()
        {
            var vm = new BuildMenuVM(new FakeEconomy(), EmptyTowers(), null, 0, null);
            Assert.DoesNotThrow(() => vm.Dispose());
        }
    }
}
