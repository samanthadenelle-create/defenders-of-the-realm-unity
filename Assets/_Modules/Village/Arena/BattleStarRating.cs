// =============================================================================
// BattleStarRating — WO-505 "battle closing": the duration -> 1..3 star rating
// and the star -> reward-multiplier mapping. Pure, static, no Unity types so the
// headless DataRegression harness (DeNelle.Editor) can assert the tiers without a
// scene. The THRESHOLDS + the MULTIPLIERS are owner-tunable named consts (the felt
// tuning is the owner's later pass; this is the gate-provable wiring).
//
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
// =============================================================================

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Maps a battle DURATION to a 1..3 star rating and a star count to a reward
    /// multiplier. All thresholds are named constants — a playtest tuning is a
    /// one-line edit, no logic change (owner felt-tunes the numbers).
    /// </summary>
    public static class BattleStarRating
    {
        // ── Tunable thresholds (seconds) — owner felt-tunes these later ────────
        /// <summary>At or under this many seconds = 3 stars (a fast, clean win).</summary>
        public const float ThreeStarSeconds = 90f;
        /// <summary>At or under this many seconds = 2 stars; anything slower = 1 star.</summary>
        public const float TwoStarSeconds = 120f;

        // ── Tunable reward multipliers per star count ─────────────────────────
        /// <summary>1-star reward multiplier (baseline, the slow win).</summary>
        public const float OneStarMultiplier = 1.00f;
        /// <summary>2-star reward multiplier.</summary>
        public const float TwoStarMultiplier = 1.25f;
        /// <summary>3-star reward multiplier (the fast, clean win pays the most).</summary>
        public const float ThreeStarMultiplier = 1.50f;

        /// <summary>The maximum stars a battle can earn.</summary>
        public const int MaxStars = 3;

        /// <summary>
        /// The 1..3 star rating for a battle that took <paramref name="durationSeconds"/>.
        /// Faster = more stars: &lt;= <see cref="ThreeStarSeconds"/> -&gt; 3,
        /// &lt;= <see cref="TwoStarSeconds"/> -&gt; 2, else 1. A win always earns at
        /// least 1 star (the rating is only computed on a WIN). Negative/zero durations
        /// clamp to the top tier.
        /// </summary>
        public static int StarsForDuration(float durationSeconds)
        {
            if (durationSeconds <= ThreeStarSeconds) return 3;
            if (durationSeconds <= TwoStarSeconds) return 2;
            return 1;
        }

        /// <summary>
        /// The reward multiplier for a star count (1 -&gt; 1.00x, 2 -&gt; 1.25x,
        /// 3 -&gt; 1.50x). 0 stars (a loss) maps to 1.00x — callers grant no reward on a
        /// loss anyway, so the floor is harmless. Out-of-range clamps to the nearest tier.
        /// </summary>
        public static float MultiplierForStars(int stars)
        {
            if (stars >= 3) return ThreeStarMultiplier;
            if (stars == 2) return TwoStarMultiplier;
            return OneStarMultiplier;
        }
    }
}
