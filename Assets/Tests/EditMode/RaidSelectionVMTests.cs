// =============================================================================
// RaidSelectionVMTests (EditMode) — §2c permission gate for the raid-grid MVVM
// slice. Locks the SceneConfigDef projection + per-card helpers that MOVED out of
// RaidSelectionScreen into the pure RaidSelectionVM. Uses a FAKE def list (no scene,
// no SceneConfigCatalog / scene-configs.json).
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class RaidSelectionVMTests
    {
        private static List<SceneConfigDef> Defs() => new List<SceneConfigDef>
        {
            new SceneConfigDef { id = "raider_camp_small", displayName = "Raider Camp", difficulty = "Regular",
                                 sceneName = "Raid_RaiderCamp", recommendedClearTime = 90f, rewardMultiplier = 1.5f, shardDropChance = 0.25f },
            new SceneConfigDef { id = "mage_enclave", displayName = "", difficulty = "Extreme",
                                 sceneName = "Raid_MageEnclave", recommendedClearTime = 240f, rewardMultiplier = 2f, shardDropChance = 0f },
        };

        [Test]
        public void raids_project_ids_and_raw_display_names()
        {
            using var vm = new RaidSelectionVM(Defs(), null);
            Assert.That(vm.Raids.Count, Is.EqualTo(2));
            Assert.That(vm.Raids[0].Id, Is.EqualTo("raider_camp_small"));
            Assert.That(vm.Raids[0].Name, Is.EqualTo("Raider Camp"));
            // A missing displayName stays EMPTY so the View can apply the kit spacer to the id.
            Assert.That(vm.Raids[1].Name, Is.EqualTo(string.Empty));
        }

        [Test]
        public void per_card_helpers_read_the_backing_def()
        {
            using var vm = new RaidSelectionVM(Defs(), null);
            Assert.That(vm.DifficultyFor("raider_camp_small"), Is.EqualTo("Regular"));
            Assert.That(vm.TargetTimeFor("raider_camp_small"), Is.EqualTo(90f));
            Assert.That(vm.RewardMultiplierFor("raider_camp_small"), Is.EqualTo(1.5f));
            Assert.That(vm.ShardChanceFor("raider_camp_small"), Is.EqualTo(0.25f));

            Assert.That(vm.DifficultyFor("mage_enclave"), Is.EqualTo("Extreme"));
            Assert.That(vm.ShardChanceFor("mage_enclave"), Is.EqualTo(0f));
        }

        [Test]
        public void def_for_returns_the_backing_def_and_null_for_unknown()
        {
            using var vm = new RaidSelectionVM(Defs(), null);
            Assert.That(vm.DefFor("mage_enclave"), Is.Not.Null);
            Assert.That(vm.DefFor("mage_enclave").sceneName, Is.EqualTo("Raid_MageEnclave"));
            Assert.That(vm.DefFor("nope"), Is.Null);
        }

        [Test]
        public void empty_catalog_yields_empty_grid()
        {
            using var vm = new RaidSelectionVM(new List<SceneConfigDef>(), null);
            Assert.That(vm.Raids.Count, Is.EqualTo(0));
        }
    }
}
