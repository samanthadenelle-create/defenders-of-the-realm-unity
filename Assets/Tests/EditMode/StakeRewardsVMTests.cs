// =============================================================================
// StakeRewardsVMTests (EditMode) -- §2c lock for the stake-rewards VM.
// -----------------------------------------------------------------------------
// Hermetic projection over a fabricated StakeStanding + the KindTag mapping, plus a
// couple of resolver integration edges (un-staked vs staked). No scene needed.
// =============================================================================
using NUnit.Framework;
using DeNelle.Core.Platform;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class StakeRewardsVMTests
    {
        [Test]
        public void projects_a_staked_standing()
        {
            var reward = new StakeReward("Seeker Badge", "a badge", StakeRewardKind.Badge, "seeker", "Seeker");
            var tier = new StakeTier("seeker", "Seeker", 1, "You stake with the realm.", new[] { reward });
            var standing = new StakeStanding(500, "SKR", tier, null, new[] { reward }, new[] { tier });

            var vm = new StakeRewardsVM(standing);

            Assert.That(vm.HasStake, Is.True);
            Assert.That(vm.HasTier, Is.True);
            Assert.That(vm.TierName, Is.EqualTo("Seeker"));
            Assert.That(vm.TierTagline, Is.EqualTo("You stake with the realm."));
            Assert.That(vm.ActiveStakeText, Is.EqualTo("Active Stake:  500 SKR"));
            Assert.That(vm.Rewards.Count, Is.EqualTo(1));
            Assert.That(vm.Rewards[0].Label, Is.EqualTo("Seeker Badge"));
            Assert.That(vm.Rewards[0].KindTag, Is.EqualTo("BADGE"));
            Assert.That(vm.IsEmpty, Is.False);
        }

        [Test]
        public void projects_the_un_staked_standing()
        {
            var vm = new StakeRewardsVM(StakeRewardsResolver.Resolve(0));
            Assert.That(vm.HasStake, Is.False);
            Assert.That(vm.HasTier, Is.False);
            Assert.That(vm.IsEmpty, Is.True);
            Assert.That(vm.ActiveStakeText, Is.EqualTo("Active Stake:  0 SKR"));
        }

        [Test]
        public void resolver_stake_unlocks_at_least_one_reward()
        {
            // A stake at/above the first tier gate resolves to a staked standing with rewards.
            var vm = new StakeRewardsVM(StakeRewardsResolver.Resolve(1));
            Assert.That(vm.HasStake, Is.True);
            Assert.That(vm.HasTier, Is.True);
            Assert.That(vm.Rewards.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(vm.ActiveStakeText, Is.EqualTo("Active Stake:  1 SKR"));
        }

        [Test]
        public void kind_tag_maps_every_kind()
        {
            Assert.That(StakeRewardsVM.KindTag(StakeRewardKind.Badge), Is.EqualTo("BADGE"));
            Assert.That(StakeRewardsVM.KindTag(StakeRewardKind.Title), Is.EqualTo("TITLE"));
            Assert.That(StakeRewardsVM.KindTag(StakeRewardKind.Cosmetic), Is.EqualTo("COSMETIC"));
            Assert.That(StakeRewardsVM.KindTag(StakeRewardKind.Trickle), Is.EqualTo("TRICKLE"));
            Assert.That(StakeRewardsVM.KindTag(StakeRewardKind.Other), Is.EqualTo("PERK"));
        }

        [Test]
        public void close_invokes_the_injected_callback()
        {
            bool closed = false;
            var vm = new StakeRewardsVM(StakeRewardsResolver.Resolve(0), () => closed = true);
            vm.Close();
            Assert.That(closed, Is.True);
        }
    }
}
