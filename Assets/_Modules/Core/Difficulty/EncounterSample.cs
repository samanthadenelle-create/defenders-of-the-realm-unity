// =============================================================================
// EncounterSample -- one finished encounter, as the difficulty tracker sees it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Adaptive
//
// A readonly struct with NO Unity dependency, so an EditMode test (or the
// headless oracle) can synthesise an entire player history in three lines and
// drive the real math. That injectability is the whole reason the difficulty
// logic is split this way: two DontDestroyOnLoad MonoBehaviour singletons -- the
// shape of the reference sketch -- cannot be driven from a test at all, so
// "deterministic / regression friendly" would have been an unbacked claim.
//
// Every field here is CONSUMED:
//   DurationSeconds / ExpectedDurationSeconds -> ClearRatio -> the time signal
//   PlayerDied                                -> the death signal
//   DamageTaken / DamageDealt                 -> DamageTakenRatio, the veto that
//                                                stops "fast but nearly dead"
//                                                counting as dominating
//   WasBoss                                   -> selects the softer boss curve
// =============================================================================

namespace DeNelle.Core.Adaptive
{
    /// <summary>One completed encounter (a wave, a raid, or a boss fight).</summary>
    public readonly struct EncounterSample
    {
        /// <summary>How long the encounter actually took, in seconds.</summary>
        public readonly float DurationSeconds;

        /// <summary>How long the encounter was DESIGNED to take, in seconds. The caller
        /// supplies this from wave/boss authoring; a value &lt;= 0 makes the time signal
        /// unusable and the sample falls back to a neutral clear ratio rather than
        /// dividing by zero.</summary>
        public readonly float ExpectedDurationSeconds;

        /// <summary>True if the player died at least once during this encounter.</summary>
        public readonly bool PlayerDied;

        /// <summary>Total damage the player took during the encounter.</summary>
        public readonly float DamageTaken;

        /// <summary>Total damage the player dealt during the encounter.</summary>
        public readonly float DamageDealt;

        /// <summary>True for a boss encounter -- selects the softer boss curve downstream.</summary>
        public readonly bool WasBoss;

        public EncounterSample(
            float durationSeconds,
            float expectedDurationSeconds,
            bool playerDied,
            float damageTaken,
            float damageDealt,
            bool wasBoss)
        {
            DurationSeconds = durationSeconds;
            ExpectedDurationSeconds = expectedDurationSeconds;
            PlayerDied = playerDied;
            DamageTaken = damageTaken;
            DamageDealt = damageDealt;
            WasBoss = wasBoss;
        }

        /// <summary>
        /// actual / expected, with EVERY degenerate input mapped to the neutral 1.0 rather
        /// than to an infinity or a NaN that would poison the running average. Zero,
        /// negative, NaN and Infinity are all real possibilities here: a wave can be
        /// cleared on the spawn frame, and a caller can pass an unset expected duration.
        /// </summary>
        public float ClearRatio
        {
            get
            {
                float actual = DurationSeconds;
                float expected = ExpectedDurationSeconds;
                if (!IsFinitePositive(expected)) return 1f;
                if (float.IsNaN(actual) || float.IsInfinity(actual) || actual < 0f) return 1f;
                float r = actual / expected;
                if (float.IsNaN(r) || float.IsInfinity(r)) return 1f;
                return r;
            }
        }

        /// <summary>
        /// damageTaken / damageDealt. Returns a LARGE value (not zero) when the player dealt
        /// no damage, because "dealt nothing" is the opposite of dominating and must never
        /// pass the dominating veto by accident.
        /// </summary>
        public float DamageTakenRatio
        {
            get
            {
                float dealt = DamageDealt;
                float taken = DamageTaken;
                if (!IsFinitePositive(dealt)) return float.MaxValue;
                if (float.IsNaN(taken) || float.IsInfinity(taken) || taken < 0f) return float.MaxValue;
                float r = taken / dealt;
                if (float.IsNaN(r) || float.IsInfinity(r)) return float.MaxValue;
                return r;
            }
        }

        private static bool IsFinitePositive(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
        }

        public override string ToString()
        {
            return "EncounterSample(dur=" + DurationSeconds.ToString("0.##") +
                   "/exp=" + ExpectedDurationSeconds.ToString("0.##") +
                   " ratio=" + ClearRatio.ToString("0.###") +
                   " died=" + PlayerDied +
                   " dmgRatio=" + (DamageTakenRatio >= float.MaxValue ? "inf" : DamageTakenRatio.ToString("0.###")) +
                   " boss=" + WasBoss + ")";
        }
    }
}
