// =============================================================================
// DynamicDifficultyTests -- EditMode proof that the difficulty logic is drivable
// without Unity at all.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Tests.EditMode
//
// These tests exist as much to PROVE THE ARCHITECTURE as to check the numbers: not
// one of them constructs a GameObject, enters play mode, or waits a frame. That is
// only possible because DifficultyMath is pure static System.Math code and
// DynamicDifficultyState takes its clock as a PARAMETER. The reference sketch put
// the same logic in two DontDestroyOnLoad MonoBehaviour singletons, where none of
// this could be written.
//
// The headless oracle (Assets/Editor/Regression/DynamicDifficultyRegression.cs) is
// the gate-facing suite and covers the same ground against the SHIPPED JSON. This
// file drives the math directly against explicit profiles, so a failure here points
// at the arithmetic rather than at the tuning.
// =============================================================================

using NUnit.Framework;
using DeNelle.Core.Adaptive;

namespace DeNelle.Tests.EditMode
{
    public class DynamicDifficultyTests
    {
        private const float Eps = 1e-4f;

        /// <summary>The authored shipping tuning, built in code so these tests do not
        /// silently change meaning when someone retunes the JSON.</summary>
        private static DifficultyProfile Shipping()
        {
            return new DifficultyProfile
            {
                Version = 1,
                SampleWindow = 10, MinSamples = 3,
                TargetDeathRate = 0.22f, DeathMasteryBound = 0.08f, DeathStruggleBound = 0.40f,
                TargetClearRatio = 0.65f, FastClearRatio = 0.40f, SlowClearRatio = 1.10f,
                DeathWeight = 0.55f, TimeWeight = 0.45f, ScoreSmoothing = 1.0f,
                MinMultiplier = 0.75f, MaxMultiplier = 1.45f, MaxMultiplierWithSpike = 1.60f,
                BossExcessRetained = 0.55f, BossReliefRetained = 1.00f,
                ScaleEnemyHp = true, ScaleEnemyDamage = true, EnemyDamageDownOnly = true,
                ScaleEnemyCount = true, CountScale = 0.50f,
                ScaleBossHp = true, ScaleBossDamage = true, BossDamageDownOnly = true,
                PressureEnabled = true,
                PressureBuildRate = 0.08f, PressureDecayRate = 0.04f, StrugglingDecayFactor = 2.0f,
                SpikeThreshold = 0.75f, SpikeMultiplier = 1.18f,
                SpikeDurationSeconds = 45f, SpikeResetPressure = 0.35f,
                DominatingDeathRate = 0.12f, DominatingClearRatio = 0.55f,
                DominatingMaxDamageTakenRatio = 0.35f,
                StrugglingDeathRate = 0.35f, StrugglingClearRatio = 0.95f,
            }.Validate();
        }

        // =====================================================================
        //  THE MAPPING -- the five approved numbers
        // =====================================================================

        [Test]
        public void TargetScore_MatchesTheApprovedValue()
        {
            // 0.55 * InverseLerp(0.40, 0.08, 0.22) + 0.45 * InverseLerp(1.10, 0.40, 0.65)
            //   = 0.55 * 0.5625 + 0.45 * 0.642857 = 0.5987
            Assert.AreEqual(0.5987f, DifficultyMath.PerformanceScoreAtTarget(Shipping()), 1e-3f);
        }

        [Test]
        public void AtTheAuthoredTargets_MultiplierIsExactlyOne()
        {
            var p = Shipping();
            Assert.AreEqual(1f, DifficultyMath.BaseMultiplier(p.TargetDeathRate, p.TargetClearRatio, p.SampleWindow, p), Eps,
                "Neutral must land on the authored target BY CONSTRUCTION. Three iterations of this " +
                "formula put an on-target player at 1.17, 1.20 and 1.10 respectively.");
            Assert.AreEqual(0f, DifficultyMath.Deviation(p.TargetDeathRate, p.TargetClearRatio, p), Eps);
        }

        [Test]
        public void DominatingThreshold_LandsInTheOwnersBand()
        {
            var p = Shipping();
            float m = DifficultyMath.BaseMultiplier(0.12f, 0.55f, p.SampleWindow, p);
            Assert.AreEqual(1.284f, m, 2e-3f);
            Assert.That(m, Is.InRange(1.25f, 1.40f));
        }

        [Test]
        public void StrugglingThreshold_LandsInTheOwnersBand()
        {
            var p = Shipping();
            float m = DifficultyMath.BaseMultiplier(0.35f, 0.95f, p.SampleWindow, p);
            Assert.AreEqual(0.805f, m, 2e-3f);
            Assert.That(m, Is.InRange(0.75f, 0.85f));
        }

        [Test]
        public void BothRails_AreExactlyReachable()
        {
            var p = Shipping();
            // actualScore 1.0 -- never died, cleared faster than the mastery bound.
            Assert.AreEqual(1.45f, DifficultyMath.BaseMultiplier(0f, 0.30f, p.SampleWindow, p), Eps);
            // actualScore 0.0 -- died more than the struggle bound, slower than the slow bound.
            Assert.AreEqual(0.75f, DifficultyMath.BaseMultiplier(0.50f, 1.30f, p.SampleWindow, p), Eps);
        }

        [Test]
        public void TheComposedCeiling_ActuallyBinds()
        {
            var p = Shipping();
            // 1.45 * 1.18 = 1.711, above the authored 1.60 -- so the rail engages inside the
            // reachable range instead of being decoration that reads as protection.
            Assert.AreEqual(1.60f, DifficultyMath.Compose(1.45f, true, p), Eps);
            Assert.That(1.45f * p.SpikeMultiplier, Is.GreaterThan(p.MaxMultiplierWithSpike));
        }

        // =====================================================================
        //  THE EARLY-GAME GATE
        // =====================================================================

        [Test]
        public void BelowTheSampleGate_MultiplierIsBitExactlyOne()
        {
            var p = Shipping();
            for (int n = 0; n < p.MinSamples; n++)
            {
                Assert.AreEqual(1f, DifficultyMath.BaseMultiplier(0f, 0.1f, n, p),
                    "A dominating history below the gate must be EXACTLY 1.0, not merely close.");
                Assert.AreEqual(1f, DifficultyMath.BaseMultiplier(1f, 3f, n, p),
                    "A disastrous history below the gate must be EXACTLY 1.0.");
            }
        }

        [Test]
        public void PastTheGate_AuthorityRampsInRatherThanJumping()
        {
            var p = Shipping();
            float atGate = DifficultyMath.BaseMultiplier(0f, 0.1f, p.MinSamples, p);
            float mid = DifficultyMath.BaseMultiplier(0f, 0.1f, (p.MinSamples + p.SampleWindow) / 2, p);
            float full = DifficultyMath.BaseMultiplier(0f, 0.1f, p.SampleWindow, p);

            Assert.That(atGate, Is.LessThan(mid));
            Assert.That(mid, Is.LessThan(full));
            Assert.AreEqual(1.45f, full, Eps);
            Assert.AreEqual(1f, DifficultyMath.Confidence(p.SampleWindow, p), Eps);
        }

        // =====================================================================
        //  TOTALITY -- no input, however degenerate, yields NaN or Infinity
        // =====================================================================

        [Test]
        public void DegenerateInputs_NeverProduceNaNOrInfinity()
        {
            var p = Shipping();
            float[] vals = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1e30f, 0f, 1e30f };
            foreach (var dr in vals)
            foreach (var cr in vals)
            {
                float b = DifficultyMath.BaseMultiplier(dr, cr, 10, p);
                Assert.IsFalse(float.IsNaN(b) || float.IsInfinity(b), "base was " + b);
                float c = DifficultyMath.Compose(b, true, p);
                Assert.IsFalse(float.IsNaN(c) || float.IsInfinity(c), "composed was " + c);
                Assert.That(c, Is.InRange(p.MinMultiplier - Eps, p.MaxMultiplierWithSpike + Eps));
            }
        }

        [Test]
        public void ACollapsedProfileRange_DegradesToAStepRatherThanNaN()
        {
            Assert.AreEqual(1f, DifficultyMath.SafeInverseLerp(0.5f, 0.5f, 0.9f), Eps);
            Assert.AreEqual(0f, DifficultyMath.SafeInverseLerp(0.5f, 0.5f, 0.1f), Eps);
            Assert.AreEqual(0.5f, DifficultyMath.SafeInverseLerp(0f, 1f, float.NaN), Eps);
        }

        [Test]
        public void ACorruptSample_ReadsNeutralRatherThanBiased()
        {
            var p = Shipping();
            // A NaN clearRatio must fall back to the AUTHORED TARGET. Falling back to a
            // hardcoded 1.0 against a 0.65 target quietly drags an on-target player to 0.921.
            Assert.AreEqual(1f, DifficultyMath.BaseMultiplier(p.TargetDeathRate, float.NaN, p.SampleWindow, p), Eps);
            Assert.AreEqual(1f, DifficultyMath.BaseMultiplier(float.NaN, p.TargetClearRatio, p.SampleWindow, p), Eps);
        }

        [Test]
        public void EncounterSample_MapsEveryDegenerateDurationToNeutral()
        {
            Assert.AreEqual(1f, new EncounterSample(30f, 0f, false, 0f, 0f, false).ClearRatio, Eps);
            Assert.AreEqual(1f, new EncounterSample(30f, -5f, false, 0f, 0f, false).ClearRatio, Eps);
            Assert.AreEqual(1f, new EncounterSample(float.NaN, 60f, false, 0f, 0f, false).ClearRatio, Eps);
            Assert.AreEqual(0.5f, new EncounterSample(30f, 60f, false, 0f, 0f, false).ClearRatio, Eps);
        }

        [Test]
        public void SignHelper_ReturnsZeroForZero()
        {
            // UnityEngine.Mathf.Sign(0f) returns +1f. A refactor that multiplied something
            // non-zero by it would snap NEUTRAL to the positive rail.
            var p = Shipping();
            Assert.AreEqual(0f, DifficultyMath.ShapeDeviation(0f, p), Eps);
        }

        // =====================================================================
        //  LEVERS
        // =====================================================================

        [Test]
        public void DamageLevers_AreDownOnlyByDefault()
        {
            var p = Shipping();
            Assert.AreEqual(1f, DifficultyMath.EnemyDamageMultiplier(1.45f, p), Eps,
                "Damage is the lever players read as unfair; by default it may never scale up.");
            Assert.AreEqual(1f, DifficultyMath.BossDamageMultiplier(1.45f, p), Eps);

            // Downward relief must still flow through.
            Assert.That(DifficultyMath.EnemyDamageMultiplier(0.80f, p), Is.LessThan(1f));
            Assert.That(DifficultyMath.BossDamageMultiplier(0.80f, p), Is.LessThan(1f));
        }

        [Test]
        public void CountLever_IsDerivedFromTheProfileNotBakedLiterals()
        {
            var p = Shipping();
            // 1 + (1.40 - 1) * 0.50 = 1.20
            Assert.AreEqual(1.20f, DifficultyMath.EnemyCountMultiplier(1.40f, p), Eps);

            // Retuning countScale must move it -- the sketch baked (m - 0.75) / 0.7 as literals.
            var p2 = Shipping();
            p2.CountScale = 1.0f;
            Assert.AreEqual(1.40f, DifficultyMath.EnemyCountMultiplier(1.40f, p2), Eps);
        }

        [Test]
        public void BossCurve_IsSofterUpwardAndFullReliefDownward()
        {
            var p = Shipping();
            Assert.AreEqual(1.22f, DifficultyMath.BossCurve(1.40f, p), 1e-3f);   // 1 + 0.40*0.55
            Assert.AreEqual(0.80f, DifficultyMath.BossCurve(0.80f, p), 1e-3f);   // 1 - 0.20*1.00
            Assert.That(DifficultyMath.BossCurve(1.40f, p), Is.LessThan(1.40f));
        }

        [Test]
        public void EveryLeverCanBeTurnedOffIndividually()
        {
            var p = Shipping();
            p.ScaleEnemyHp = false; p.ScaleEnemyCount = false;
            p.ScaleBossHp = false; p.ScaleBossDamage = false; p.ScaleEnemyDamage = false;
            Assert.AreEqual(1f, DifficultyMath.EnemyHpMultiplier(1.45f, p), Eps);
            Assert.AreEqual(1f, DifficultyMath.EnemyCountMultiplier(1.45f, p), Eps);
            Assert.AreEqual(1f, DifficultyMath.EnemyDamageMultiplier(0.75f, p), Eps);
            Assert.AreEqual(1f, DifficultyMath.BossHpMultiplier(1.45f, p), Eps);
            Assert.AreEqual(1f, DifficultyMath.BossDamageMultiplier(0.75f, p), Eps);
        }

        // =====================================================================
        //  PRESSURE + SPIKE
        // =====================================================================

        private static EncounterSample Dominating()
        {
            return new EncounterSample(18f, 60f, false, 80f, 1000f, false);   // ratio 0.30, dmg 0.08
        }

        private static EncounterSample Struggling()
        {
            return new EncounterSample(84f, 60f, true, 900f, 300f, false);     // ratio 1.40
        }

        [Test]
        public void Classification_MatchesTheOwnersRows()
        {
            var p = Shipping();
            Assert.AreEqual(EncounterVerdict.Dominating, DifficultyMath.Classify(Dominating(), p));
            Assert.AreEqual(EncounterVerdict.Struggling, DifficultyMath.Classify(Struggling(), p));

            // A fast, death-free clear where the player was nearly killed is NOT mastery.
            var pyrrhic = new EncounterSample(18f, 60f, false, 950f, 1000f, false);
            Assert.AreNotEqual(EncounterVerdict.Dominating, DifficultyMath.Classify(pyrrhic, p),
                "This veto is the only consumer of the tracked damageTaken/damageDealt numbers.");
        }

        [Test]
        public void PressureBuildsOnDominatingAndDecaysFasterWhenStruggling()
        {
            var p = Shipping();
            float up = DifficultyMath.NextPressure(0.5f, EncounterVerdict.Dominating, p);
            float normal = DifficultyMath.NextPressure(0.5f, EncounterVerdict.Normal, p);
            float struggling = DifficultyMath.NextPressure(0.5f, EncounterVerdict.Struggling, p);

            Assert.That(up, Is.GreaterThan(0.5f));
            Assert.That(normal, Is.LessThan(0.5f));
            Assert.That(struggling, Is.LessThan(normal), "The owner's 'struggling -> pressure drops FASTER' row.");
            Assert.AreEqual(0f, DifficultyMath.NextPressure(0.01f, EncounterVerdict.Struggling, p), Eps);
            Assert.AreEqual(1f, DifficultyMath.NextPressure(0.99f, EncounterVerdict.Dominating, p), Eps);
        }

        [Test]
        public void SpikeFiresAtTheThresholdAndCannotInstantlyReFire()
        {
            var p = Shipping();
            var s = new DynamicDifficultyState(p);

            int fires = 0;
            double t = 0d;
            for (int i = 0; i < 40; i++) { if (s.Record(Dominating(), t)) fires++; t += 1d; }

            Assert.AreEqual(1, fires,
                "40 dominating encounters one second apart must fire exactly once: the soft reset to " +
                "spikeResetPressure plus the already-active guard make an instant re-trigger impossible.");
            Assert.That(p.SpikeResetPressure, Is.LessThan(p.SpikeThreshold));
        }

        [Test]
        public void TheSpikeExpiresExactlyOnTime_AndIsFeltAtReadTime()
        {
            // THE headline regression. In the reference sketch the 45s timer lived in
            // Update() and only controlled a log line: the spiked value was written back
            // into the base field, so it stayed live until the NEXT encounter ended.
            var p = Shipping();
            var s = new DynamicDifficultyState(p);

            double t = 1000d;
            double fired = -1d;
            for (int i = 0; i < 40 && fired < 0d; i++) { if (s.Record(Dominating(), t)) fired = t; t += 1d; }
            Assert.That(fired, Is.GreaterThan(0d), "no spike fired, so expiry cannot be tested");

            Assert.IsTrue(s.IsSpikeActive(fired + 0.001d));
            Assert.IsTrue(s.IsSpikeActive(fired + p.SpikeDurationSeconds - 0.01d));
            Assert.IsFalse(s.IsSpikeActive(fired + p.SpikeDurationSeconds + 0.01d),
                "The spike must be dead the instant its authored duration elapses.");

            float during = s.CurrentMultiplier(fired + 1d);
            float after = s.CurrentMultiplier(fired + p.SpikeDurationSeconds + 0.01d);
            float muchLater = s.CurrentMultiplier(fired + p.SpikeDurationSeconds + 3600d);

            Assert.That(during, Is.GreaterThan(after), "expiry must be felt without a new encounter");
            Assert.AreEqual(DifficultyMath.Compose(s.BaseMultiplier, false, p), after, Eps,
                "After expiry the value must be exactly the un-spiked base -- proof no derived " +
                "value was stored back into the base field.");
            Assert.AreEqual(after, muchLater, Eps, "nothing may drift with no encounters recorded");
            Assert.That(during, Is.LessThanOrEqualTo(p.MaxMultiplierWithSpike + Eps));
        }

        // =====================================================================
        //  DETERMINISM + HISTORY
        // =====================================================================

        [Test]
        public void SameHistoryIn_SameMultiplierOut()
        {
            var p = Shipping();
            var samples = new EncounterSample[24];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = new EncounterSample(30f + (i % 7) * 4f, 60f, (i % 5) == 0,
                                                 100f + i * 3f, 900f - i * 5f, (i % 4) == 0);

            float RunOnce()
            {
                var s = new DynamicDifficultyState(p);
                double t = 500d;
                foreach (var e in samples) { s.Record(e, t); t += 90d; }
                return s.CurrentMultiplier(t);
            }

            Assert.AreEqual(RunOnce(), RunOnce());
        }

        [Test]
        public void TheHistoryWindowRollsAtSampleWindow()
        {
            var p = Shipping();
            var s = new DynamicDifficultyState(p);
            for (int i = 0; i < p.SampleWindow * 3; i++) s.Record(Dominating(), i);

            Assert.AreEqual(p.SampleWindow, s.SampleCount);
            Assert.AreEqual(p.SampleWindow * 3, s.TotalRecorded);
        }

        [Test]
        public void AWindowOfStrugglingHistory_LowersTheBaseMultiplier()
        {
            var p = Shipping();
            var s = new DynamicDifficultyState(p);
            for (int i = 0; i < p.SampleWindow; i++) s.Record(Struggling(), i);
            Assert.That(s.BaseMultiplier, Is.LessThan(1f));
            Assert.That(s.BaseMultiplier, Is.GreaterThanOrEqualTo(p.MinMultiplier));
        }

        [Test]
        public void ResetReturnsTheSystemToExactlyNeutral()
        {
            var p = Shipping();
            var s = new DynamicDifficultyState(p);
            for (int i = 0; i < 30; i++) s.Record(Dominating(), i);
            s.Reset();

            Assert.AreEqual(0, s.SampleCount);
            Assert.AreEqual(0f, s.Pressure, Eps);
            Assert.IsFalse(s.IsSpikeActive(0d));
            Assert.AreEqual(1f, s.BaseMultiplier, "a reset state is bit-exactly neutral");
        }

        [Test]
        public void BossHistory_DrivesTheBossMultiplierOnceEnoughBossesAreSeen()
        {
            // This is what makes EncounterSample.WasBoss load-bearing rather than
            // recorded-and-ignored: boss difficulty follows boss performance.
            var p = Shipping();
            var s = new DynamicDifficultyState(p);

            // Trash: dominated. Bosses: struggled. INTERLEAVED, and the boss aggregate is filled to
            // the full SampleWindow rather than just MinSamples, because of the confidence ramp:
            // at exactly MinSamples, Confidence() is (3-3+1)/(10-3+1) = 0.125, so the boss
            // multiplier is pinned to 1 + (raw-1)*0.125 = 0.96875 no matter how badly the player
            // did. Seeding only MinSamples therefore measured the RAMP, not the boss performance,
            // and the comparison inverted. Interleaving also keeps dominating trash inside the
            // overall rolling window, so the two aggregates genuinely differ.
            for (int i = 0; i < p.SampleWindow; i++)
            {
                s.Record(Dominating(), i * 2);
                s.Record(new EncounterSample(84f, 60f, true, 900f, 300f, true), i * 2 + 1);
            }

            Assert.That(s.BossAggregate.SampleCount, Is.GreaterThanOrEqualTo(p.MinSamples));

            // INTENT: WasBoss must be load-bearing - boss difficulty follows BOSS performance, and a
            // player who keeps dying to bosses gets boss relief. Asserted against NEUTRAL, not
            // against the trash multiplier.
            //
            // WHY NOT `boss < trash`: the two are different populations with different confidence.
            // BossAggregate caps its sample count well below SampleWindow, so Confidence() for the
            // boss track is structurally bounded (~0.375) while the overall track reaches 1.0. The
            // boss multiplier therefore CANNOT travel as far from neutral as the overall one - which
            // is exactly the "bosses ride a softer curve" ruling working as designed. Asserting
            // boss < trash was asserting something the design deliberately forbids, and it happened
            // to pass before only because the boss track was pinned near 1.0 by the ramp.
            Assert.That(s.CurrentBossMultiplier(1000d), Is.LessThan(1f),
                "Bosses the player keeps losing to must get relief - if WasBoss were recorded-and-ignored this would sit at neutral.");
            Assert.That(s.CurrentBossMultiplier(1000d), Is.GreaterThanOrEqualTo(p.MinMultiplier),
                "boss relief must still respect the authored floor");
        }
    }
}
