// =============================================================================
// DynamicDifficultyState -- the injectable, Unity-free difficulty tracker.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Adaptive
//
// Holds the rolling encounter history, the pressure value and the spike expiry
// TIMESTAMP -- and nothing else. All arithmetic is delegated to DifficultyMath.
// No UnityEngine types, no MonoBehaviour, no singleton: an EditMode test or the
// headless oracle constructs one of these, feeds it a synthetic history, passes
// its own clock, and reads exactly what the shipped game would read.
//
// THE CLOCK IS A PARAMETER, NOT A DEPENDENCY. Every method that cares about time
// takes `nowSeconds`. That is what makes "the spike expires after 45 seconds"
// a headless assertion instead of a claim -- and it is why there is no Update()
// here ticking a countdown. A countdown in Update() was the mechanism of the
// headline bug in the reference sketch: the timer expired, nothing recomputed the
// multiplier, and the spiked value stayed live until the next encounter ENDED --
// which can be minutes after the 45 seconds are up. With an absolute expiry
// timestamp compared at read time, expiry is exact and cannot be missed.
//
// THE BASE IS NEVER OVERWRITTEN WITH A COMPOSED VALUE. `BaseMultiplier` is
// recomputed from history; the spike is applied on top at read time by
// `CurrentMultiplier(now)`. Storing a derived value back into the field the base
// lives in is the shape of that same bug and is structurally impossible here.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Adaptive
{
    /// <summary>
    /// Rolling difficulty state for one player session. Construct, Record() finished
    /// encounters, then read the multipliers. Fully deterministic: the same sequence of
    /// samples and clock values always produces the same outputs.
    /// </summary>
    public sealed class DynamicDifficultyState
    {
        private readonly DifficultyProfile _profile;
        private readonly List<EncounterSample> _history = new List<EncounterSample>();

        private float _pressure;
        private double _spikeExpiresAtSeconds = double.NegativeInfinity;

        /// <summary>The tuning table this state was built against (never null).</summary>
        public DifficultyProfile Profile { get { return _profile; } }

        /// <summary>Current pressure, 0..1. Builds on dominating encounters, decays otherwise.</summary>
        public float Pressure { get { return _pressure; } }

        /// <summary>Absolute clock value at which the live spike expires. NegativeInfinity
        /// when no spike has ever fired.</summary>
        public double SpikeExpiresAtSeconds { get { return _spikeExpiresAtSeconds; } }

        /// <summary>How many samples are in the window right now.</summary>
        public int SampleCount { get { return _history.Count; } }

        /// <summary>Total encounters recorded since construction, including ones that have
        /// aged out of the window. Diagnostics only.</summary>
        public int TotalRecorded { get; private set; }

        public DynamicDifficultyState(DifficultyProfile profile)
        {
            _profile = (profile ?? new DifficultyProfile()).Validate();
        }

        // =====================================================================
        //  RECORDING
        // =====================================================================

        /// <summary>
        /// Records one finished encounter and advances the pressure system. Returns true if
        /// this encounter FIRED a spike, so the caller can log/telegraph the moment.
        /// </summary>
        /// <param name="sample">The finished encounter.</param>
        /// <param name="nowSeconds">The caller's clock (unscaled seconds). Injected, never read
        /// from UnityEngine.Time here -- that is what keeps this headless-testable.</param>
        public bool Record(EncounterSample sample, double nowSeconds)
        {
            _history.Add(sample);
            TotalRecorded++;
            while (_history.Count > _profile.SampleWindow) _history.RemoveAt(0);

            if (!_profile.PressureEnabled)
            {
                _pressure = 0f;
                return false;
            }

            var verdict = DifficultyMath.Classify(sample, _profile);
            _pressure = DifficultyMath.NextPressure(_pressure, verdict, _profile);

            bool spikeLive = DifficultyMath.IsSpikeActive(nowSeconds, _spikeExpiresAtSeconds);
            if (!DifficultyMath.ShouldSpike(_pressure, spikeLive, _profile)) return false;

            _spikeExpiresAtSeconds = nowSeconds + _profile.SpikeDurationSeconds;
            // SOFT reset, not zero: the ramp back is quicker than the first climb, but
            // because SpikeResetPressure is validated to sit strictly below SpikeThreshold,
            // a spike can never instantly re-fire.
            _pressure = _profile.SpikeResetPressure;
            return true;
        }

        /// <summary>Clears history, pressure and any live spike. Used on a new game / new run.</summary>
        public void Reset()
        {
            _history.Clear();
            _pressure = 0f;
            _spikeExpiresAtSeconds = double.NegativeInfinity;
            TotalRecorded = 0;
        }

        // =====================================================================
        //  AGGREGATION
        // =====================================================================

        /// <summary>The rolled-up window over ALL recorded encounters.</summary>
        public DifficultyAggregate Aggregate { get { return Roll(false); } }

        /// <summary>
        /// The rolled-up window over BOSS encounters only. This is what makes
        /// <see cref="EncounterSample.WasBoss"/> load-bearing rather than recorded-and-ignored:
        /// once enough boss fights have been seen, boss difficulty adapts to how the player
        /// performs against BOSSES, not to how they clear trash. Below that threshold the
        /// combined history is used, so a player who has fought one boss is not judged on it.
        /// </summary>
        public DifficultyAggregate BossAggregate
        {
            get
            {
                var boss = Roll(true);
                return boss.SampleCount >= _profile.MinSamples ? boss : Roll(false);
            }
        }

        private DifficultyAggregate Roll(bool bossOnly)
        {
            int n = 0, deaths = 0;
            float ratioSum = 0f, dmgSum = 0f;
            int dmgCount = 0;

            for (int i = 0; i < _history.Count; i++)
            {
                var s = _history[i];
                if (bossOnly && !s.WasBoss) continue;
                n++;
                if (s.PlayerDied) deaths++;
                ratioSum += s.ClearRatio;
                float dr = s.DamageTakenRatio;
                // float.MaxValue is the "dealt nothing" sentinel; averaging it in would
                // overflow the sum. Count it as a large-but-finite 10x instead.
                if (dr >= float.MaxValue) dr = 10f;
                dmgSum += dr;
                dmgCount++;
            }

            if (n == 0) return new DifficultyAggregate(0, _profile.TargetDeathRate, _profile.TargetClearRatio, 0f);

            float deathRate = (float)deaths / n;
            float clearRatio = ratioSum / n;
            float dmgRatio = dmgCount > 0 ? dmgSum / dmgCount : 0f;
            return new DifficultyAggregate(n, deathRate, clearRatio, dmgRatio);
        }

        // =====================================================================
        //  READS -- the spike is ALWAYS composed here, never stored
        // =====================================================================

        /// <summary>
        /// The base multiplier from history alone. NEVER includes the spike, and is never
        /// written back to from a composed value.
        /// </summary>
        public float BaseMultiplier { get { return DifficultyMath.BaseMultiplier(Aggregate, _profile); } }

        /// <summary>True while a pressure spike is live at the given clock value.</summary>
        public bool IsSpikeActive(double nowSeconds)
        {
            return _profile.PressureEnabled && DifficultyMath.IsSpikeActive(nowSeconds, _spikeExpiresAtSeconds);
        }

        /// <summary>Seconds of spike remaining at the given clock value (0 when none).</summary>
        public double SpikeRemainingSeconds(double nowSeconds)
        {
            if (!IsSpikeActive(nowSeconds)) return 0d;
            double left = _spikeExpiresAtSeconds - nowSeconds;
            return left > 0d ? left : 0d;
        }

        /// <summary>
        /// THE live multiplier: base composed with the spike at READ time. Every lever getter
        /// below routes through this, so a spike expiring is felt on the very next read.
        /// </summary>
        public float CurrentMultiplier(double nowSeconds)
        {
            return DifficultyMath.Compose(BaseMultiplier, IsSpikeActive(nowSeconds), _profile);
        }

        /// <summary>The live multiplier for BOSS encounters: the boss-only history bent onto
        /// the softer boss curve, then composed with any live spike.</summary>
        public float CurrentBossMultiplier(double nowSeconds)
        {
            float bossBase = DifficultyMath.BaseMultiplier(BossAggregate, _profile);
            return DifficultyMath.Compose(bossBase, IsSpikeActive(nowSeconds), _profile);
        }

        // ---- Levers (thin, so integration sites never re-derive anything) ----

        /// <summary>Enemy max-HP multiplier to apply as <c>baseHp * mult</c> on spawn.</summary>
        public float EnemyHpMultiplier(double now) { return DifficultyMath.EnemyHpMultiplier(CurrentMultiplier(now), _profile); }

        /// <summary>Enemy damage multiplier to apply as <c>baseDamage * mult</c> on spawn.</summary>
        public float EnemyDamageMultiplier(double now) { return DifficultyMath.EnemyDamageMultiplier(CurrentMultiplier(now), _profile); }

        /// <summary>Wave enemy-count multiplier to apply as <c>baseCount * mult</c>.</summary>
        public float EnemyCountMultiplier(double now) { return DifficultyMath.EnemyCountMultiplier(CurrentMultiplier(now), _profile); }

        /// <summary>Boss max-HP multiplier to apply as <c>baseHp * mult</c> on spawn.</summary>
        public float BossHpMultiplier(double now) { return DifficultyMath.BossHpMultiplier(CurrentBossMultiplier(now), _profile); }

        /// <summary>Boss damage multiplier to apply as <c>baseDamage * mult</c> on spawn.</summary>
        public float BossDamageMultiplier(double now) { return DifficultyMath.BossDamageMultiplier(CurrentBossMultiplier(now), _profile); }

        /// <summary>One-line diagnostic for FlowTrace / the F8 harness.</summary>
        public string Describe(double nowSeconds)
        {
            var a = Aggregate;
            return "n=" + a.SampleCount + "/" + _profile.SampleWindow +
                   " deathRate=" + a.DeathRate.ToString("0.###") +
                   " clearRatio=" + a.ClearRatio.ToString("0.###") +
                   " conf=" + DifficultyMath.Confidence(a.SampleCount, _profile).ToString("0.##") +
                   " base=" + BaseMultiplier.ToString("0.###") +
                   " pressure=" + _pressure.ToString("0.##") +
                   " spike=" + (IsSpikeActive(nowSeconds) ? SpikeRemainingSeconds(nowSeconds).ToString("0.#") + "s" : "off") +
                   " current=" + CurrentMultiplier(nowSeconds).ToString("0.###");
        }
    }
}
