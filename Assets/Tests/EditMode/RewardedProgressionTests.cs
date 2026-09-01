using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    public sealed class RewardedProgressionTests
    {
        [TestCase("wall_wood", 1, false)]
        [TestCase("wall_wood", 2, true)]
        [TestCase("WALL_WOOD", 2, true)]
        [TestCase("wall_stone", 2, false)]
        [TestCase("gate_stone", 2, false)]
        public void StoneGateTrigger_IsOnlyThePalisadeStoneTransition(string id, int level, bool expected)
        {
            Assert.That(RewardedProgression.IsStoneWallCreation(id, level), Is.EqualTo(expected));
        }

        [TestCase(6, false, false)]
        [TestCase(7, false, true)]
        [TestCase(8, false, true)]
        [TestCase(7, true, false)]
        [TestCase(99, true, false)]
        public void HealingPlans_AwardAtWaveSevenExactlyOnce(int waves, bool unlocked, bool expected)
        {
            Assert.That(RewardedProgression.ShouldAwardHealingCaravanPlans(waves, unlocked), Is.EqualTo(expected));
        }

        [Test]
        public void ProtectionCards_CarryTheRequiredLockedCopy()
        {
            Assert.That(RewardedProgression.LockReasonFor("gate_stone"),
                Is.EqualTo("Create a Stone Wall to unlock"));
            Assert.That(RewardedProgression.LockReasonFor("healing_caravan"),
                Is.EqualTo("Recover its plans after Wave 7"));
            Assert.That(RewardedProgression.LockReasonFor("wall_wood"), Is.Null);
        }
    }
}
