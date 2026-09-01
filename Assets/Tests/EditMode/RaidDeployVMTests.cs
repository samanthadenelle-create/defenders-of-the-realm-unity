// =============================================================================
// RaidDeployVMTests (EditMode) — §2c permission gate for the pre-raid deploy MVVM
// slice. Locks the party roster + troop grouping + owned counts + POWER RATING +
// deployable count + deploy guard that MOVED out of RaidDeployScreen into the pure
// RaidDeployVM. Uses a FAKE army + a FAKE troop-info resolver (no scene, no
// GameState, no TroopCatalog).
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class RaidDeployVMTests
    {
        // Fake troop facts: footman = melee atk 10, archer = ranged atk 20 (both 1 slot).
        private static RaidDeployVM.TroopInfo Info(string id)
        {
            switch (id)
            {
                case "footman": return new RaidDeployVM.TroopInfo("Footman", 10f, false, 1);
                case "archer": return new RaidDeployVM.TroopInfo("Archer", 20f, true, 1);
                default: return new RaidDeployVM.TroopInfo(null, 10f, false, 1);
            }
        }

        private static ArmyStorage ArmyOf(params string[] troopDefIds)
        {
            var a = new ArmyStorage();
            int n = 1;
            foreach (var id in troopDefIds)
                a.Owned.Add(new PlayerTroop("troop-" + (n++), id));
            return a;
        }

        private static SceneConfigDef Def() => new SceneConfigDef
        {
            id = "raider_camp_small",
            displayName = "Raider Camp",
            difficulty = "Regular",
            sceneName = "RaidBase_raider_camp_small",
            recommendedClearTime = 90f,
            twoStarTime = 70f,
        };

        [Test]
        public void troops_group_by_type_with_owned_counts_in_first_seen_order()
        {
            var vm = new RaidDeployVM(Def(), ArmyOf("footman", "footman", "archer"),
                                      new List<string> { "Knight" }, Info, null);

            Assert.That(vm.Troops.Count, Is.EqualTo(2), "two distinct troop types");
            Assert.That(vm.Troops[0].Id, Is.EqualTo("footman"));
            Assert.That(vm.Troops[0].Price, Is.EqualTo(2), "owned count carried on Price");
            Assert.That(vm.Troops[0].Name, Is.EqualTo("Footman"));
            Assert.That(vm.Troops[1].Id, Is.EqualTo("archer"));
            Assert.That(vm.Troops[1].Price, Is.EqualTo(1));

            Assert.That(vm.IsRanged("archer"), Is.True);
            Assert.That(vm.IsRanged("footman"), Is.False);
        }

        [Test]
        public void deployable_count_and_power_rating_sum_deployable_attack()
        {
            var vm = new RaidDeployVM(Def(), ArmyOf("footman", "footman", "archer"),
                                      new List<string> { "Knight" }, Info, null);
            Assert.That(vm.DeployableCount, Is.EqualTo(3));
            // 10 + 10 + 20, each at rank-0 veterancy (x1.0) = 40.
            Assert.That(vm.PowerRating, Is.EqualTo(40));
        }

        [Test]
        public void empty_army_reads_zero_and_no_troop_rows()
        {
            var vm = new RaidDeployVM(Def(), ArmyOf(), new List<string> { "Knight" }, Info, null);
            Assert.That(vm.Troops.Count, Is.EqualTo(0));
            Assert.That(vm.DeployableCount, Is.EqualTo(0));
            Assert.That(vm.PowerRating, Is.EqualTo(0));
        }

        [Test]
        public void party_classes_and_companion_names_project()
        {
            var vm = new RaidDeployVM(Def(), ArmyOf(), new List<string> { "Knight", "Ranger" }, Info, null);
            Assert.That(vm.PartyClasses.Count, Is.EqualTo(2));
            Assert.That(vm.PartyClasses[0], Is.EqualTo("Knight"));
            Assert.That(vm.CompanionName("Knight"), Is.EqualTo("Grom"));
            Assert.That(vm.CompanionName("Ranger"), Is.EqualTo("Sylas"));
            Assert.That(vm.CompanionName("wizard"), Is.EqualTo("Thrain"));
        }

        [Test]
        public void empty_party_falls_back_to_a_hero_placeholder()
        {
            var vm = new RaidDeployVM(Def(), ArmyOf(), new List<string>(), Info, null);
            Assert.That(vm.PartyClasses.Count, Is.EqualTo(1));
            Assert.That(vm.PartyClasses[0], Is.EqualTo("Knight"));
        }

        [Test]
        public void deploy_guard_reflects_scene_presence()
        {
            var withScene = new RaidDeployVM(Def(), ArmyOf(), null, Info, null);
            Assert.That(withScene.CanDeploy, Is.True);
            Assert.That(withScene.RaidName, Is.EqualTo("Raider Camp"));
            Assert.That(withScene.EstClearTime, Is.EqualTo(70f), "2-star band drives the estimate");

            var noScene = new RaidDeployVM(new SceneConfigDef { id = "x", sceneName = "" }, ArmyOf(), null, Info, null);
            Assert.That(noScene.CanDeploy, Is.False);
        }

        [Test]
        public void army_cap_text_is_never_null()
        {
            var vm = new RaidDeployVM(Def(), ArmyOf("footman"), new List<string> { "Knight" }, Info, null);
            Assert.That(vm.ArmyCapText, Is.Not.Null);
            Assert.That(vm.ArmyCapText, Does.StartWith("Army:"));
        }
    }
}
