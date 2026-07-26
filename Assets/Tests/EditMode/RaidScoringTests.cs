// =============================================================================
// RaidScoringTests (EditMode) — locks the PURE V1 raid star + loot math
// (RaidScoring.ComputeStars / ComputeLoot, WO-771.6 teleport/deploy loop). No
// scene, no GameState, no catalog — just the deterministic-enough formulas.
// =============================================================================

using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class RaidScoringTests
    {
        private const float Clock = 180f;

        // ── Stars from inputs (design B5: cleared / boss / % / clock) ──────────

        [Test]
        public void Stars_Retreat_UnderHalf_Razed_IsZero()
        {
            Assert.AreEqual(0, RaidScoring.ComputeStars(false, false, 0.20f, 10f, Clock));
        }

        [Test]
        public void Stars_Retreat_HalfRazed_IsOne()
        {
            Assert.AreEqual(1, RaidScoring.ComputeStars(false, false, 0.50f, 10f, Clock));
        }

        [Test]
        public void Stars_BossDown_IsTwo()
        {
            Assert.AreEqual(2, RaidScoring.ComputeStars(false, true, 0.60f, 10f, Clock));
        }

        [Test]
        public void Stars_FullClear_OverTheClock_IsTwo()
        {
            Assert.AreEqual(2, RaidScoring.ComputeStars(true, true, 1f, 240f, Clock));
        }

        [Test]
        public void Stars_FullClear_UnderTheClock_IsThree()
        {
            Assert.AreEqual(3, RaidScoring.ComputeStars(true, true, 1f, 120f, Clock));
        }

        [Test]
        public void Stars_AlwaysWithinRange()
        {
            for (int di = 0; di <= 10; di++)
            {
                float d = di / 10f;
                foreach (var cleared in new[] { false, true })
                foreach (var boss in new[] { false, true })
                foreach (var t in new[] { 30f, 180f, 400f })
                {
                    int s = RaidScoring.ComputeStars(cleared, boss, d, t, Clock);
                    Assert.GreaterOrEqual(s, 0);
                    Assert.LessOrEqual(s, 3);
                }
            }
        }

        // ── Loot scales with stars + destruction ──────────────────────────────

        [Test]
        public void Loot_NothingRaided_PaysNothing()
        {
            var loot = RaidScoring.ComputeLoot(0, 0f, 40, 60, 15, 20);
            Assert.AreEqual(0, loot.Crystals);
            Assert.AreEqual(0, loot.Food);
        }

        [Test]
        public void Loot_ScalesMonotonically_WithStarsAndDestruction()
        {
            var none = RaidScoring.ComputeLoot(0, 0f, 40, 60, 15, 20);
            var half = RaidScoring.ComputeLoot(1, 0.5f, 40, 60, 15, 20);
            var full = RaidScoring.ComputeLoot(3, 1f, 40, 60, 15, 20);

            Assert.Greater(half.Crystals, none.Crystals);
            Assert.Greater(full.Crystals, half.Crystals);
            Assert.Greater(half.Food, none.Food);
            Assert.Greater(full.Food, half.Food);
        }

        [Test]
        public void Loot_FullThreeStar_MatchesBasePlusPerStar()
        {
            // 100% destruction => full base; +3x per-star bonus.
            var full = RaidScoring.ComputeLoot(3, 1f, 40, 60, 15, 20);
            Assert.AreEqual(40 + 3 * 15, full.Crystals);   // 85
            Assert.AreEqual(60 + 3 * 20, full.Food);        // 120
        }
    }
}
