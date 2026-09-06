// =============================================================================
// DifficultyMath -- the ENTIRE dynamic-difficulty decision, as pure static code.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Adaptive
//
// NO UnityEngine TYPES. NO Mathf. NO MonoBehaviour. NO singleton. System.Math
// only -- the same deliberate choice PartyShopVM / InventoryVM made so their logic is
// unit-testable without a scene. That is what makes the headless oracle possible
// at all: the reference sketch put this arithmetic inside a DontDestroyOnLoad
// MonoBehaviour, where an EditMode test cannot inject a history, cannot advance a
// timer, and therefore cannot prove a single line of the owner's feel table.
// "Deterministic / AutoPilot / regression friendly" is only true if the history
// can be INJECTED, so it is.
//
// =============================================================================
//  THE MAPPING -- three broken shapes and why this one is not
// =============================================================================
// SHAPE 1 (could never make the game harder):
//     raw = deathFactor*0.55 + timeFactor*0.45          // convex blend of [0,1]
//     result = Clamp(raw, 0.75, 1.45)                   // therefore [0.75, 1.0]
// `raw` can never exceed 1.0, so maxMultiplier = 1.45 was DEAD CONFIG and the
// owner's "player stomping content -> 1.25-1.40" row was unreachable.
//
// SHAPE 2 (two-sided, but neutral in the wrong place):
//     BaseMultiplier = Lerp(min, max, performance)
// Reachable at both ends, but Lerp(0.75, 1.45, p) equals 1.0 only at
// p = 0.25/0.70 = 0.357, so a dead-average p=0.5 player already sits at 1.10 and
// the system quietly ratchets EVERYONE up. It also never read targetDeathRate or
// targetClearRatio, orphaning both as dead authored keys.
//
// SHAPE 3 (curve applied before the map -- makes it WORSE):
//     curved = SmoothStep(0, 1, linear); result = Lerp(min, max, curved)
// At the authored targets linear = 0.5987, SmoothStep(0.5987) = 0.64612, and
// Lerp(0.75, 1.45, 0.64612) = 1.202. An exactly-on-target player gets 1.20x --
// worse than Shape 2's 1.17x, because SmoothStep pushes everything above 0.5
// upward. THE GENERAL RULE, which is the load-bearing insight here:
//
//     ANY CURVE APPLIED TO A BLENDED 0..1 SCORE BEFORE THE MIN/MAX MAPPING MOVES
//     THE NEUTRAL POINT, UNLESS NEUTRAL HAPPENS TO SIT AT EXACTLY 0.5. OURS DOES
//     NOT. CURVE-THEN-MAP IS STRUCTURALLY WRONG FOR AN AUTHORED NEUTRAL.
//
// THE APPROVED SHAPE (verified independently, 2026-08-02). Seven steps:
//
//   1. Each signal -> an UNSIGNED [0,1] mastery score via SafeInverseLerp
//        deathScore = SafeInverseLerp(deathStruggleBound, deathMasteryBound, deathRate)
//        timeScore  = SafeInverseLerp(slowClearRatio,     fastClearRatio,    clearRatio)
//   2. Inputs guarded for NaN/Infinity BEFORE use, each falling back to ITS OWN
//      AUTHORED TARGET so a corrupt sample reads NEUTRAL rather than biased.
//   3. ONE shared PerformanceScore(dRate, cRatio) is used for BOTH the authored
//      target and the actual history. That sharing is what makes neutral EXACT:
//        targetScore = PerformanceScore(targetDeathRate, targetClearRatio)   // 0.5987
//        actualScore = PerformanceScore(observed, observed)
//   4. deviation = actualScore - targetScore
//   5. PER-SIDE normalization by each side's OWN available range -- no gain constant:
//        norm = deviation >= 0 ? deviation / (1 - targetScore) : deviation / targetScore
//   6. Curve the MAGNITUDE only, sign restored:  shaped = sign(norm) * SmoothStep(|norm|)
//   7. ASYMMETRIC map about true neutral:
//        shaped >= 0 -> Lerp(1, maxMultiplier,  shaped)
//        shaped <  0 -> Lerp(1, minMultiplier, -shaped)
//
// Steps 5 and 7 both exist because THE BAND IS NOT SYMMETRIC ABOUT 1.0: min 0.75 is
// 0.25 below neutral, max 1.45 is 0.45 above. Every earlier iteration failed by
// treating it as symmetric -- a midpoint, a single Lerp across the whole range, or
// one shared gain -- and every one of those silently moved neutral. Step 6 curves
// the magnitude rather than the blend because SmoothStep fixes both 0 and 1, so it
// eases the feel without touching neutral or making a rail unreachable.
//
// CONFIRMED ARITHMETIC (authored defaults: targets 0.22 / 0.65, bounds 0.08 / 0.40
// and 0.40 / 1.10, weights 0.55 / 0.45, rails 0.75 / 1.45 / 1.60, smoothing 1.0;
// targetScore = 0.55*0.5625 + 0.45*0.642857 = 0.5987):
//
//   AT TARGET             0.22 / 0.65  -> deviation 0                    -> 1.000 EXACTLY
//   DOMINATING THRESHOLD  0.12 / 0.55  -> norm  0.5884, SmoothStep 0.6312 -> 1.284
//   STRUGGLING THRESHOLD  0.35 / 0.95  -> norm -0.6954, SmoothStep 0.7782 -> 0.805
//   actualScore = 1.0                  -> norm  1.0                       -> 1.450 EXACTLY
//   actualScore = 0.0                  -> norm -1.0                       -> 0.750 EXACTLY
//
//   Both base rails BIND. COMPOSED WITH SPIKE: 1.45 * 1.18 = 1.711, clamped to the
//   authored 1.60 -- so the composed ceiling binds for every base above
//   1.60 / 1.18 = 1.3559, which is inside the reachable base band.
//
// =============================================================================
//  POOLED-BODY RULE -- READ THIS BEFORE INTEGRATING ANY LEVER
// =============================================================================
// NEVER write `enemy.MaxHp *= mult`. Enemy/EnemyBrain state SURVIVES a pool
// Release/Get in this codebase (proven 2026-08-02), so an in-place multiply on a
// body reused five times applies the multiplier FIVE TIMES, exponentially. The
// contract is: capture the AUTHORED base on pool Get (Enemy.SetBaseStats), then
// always compute `base * mult` (Enemy.ApplyDifficulty). Every multiplier this
// class returns is intended for the second form and for nothing else.
// =============================================================================

using System;

namespace DeNelle.Core.Adaptive
{
    /// <summary>The rolled-up view of a player's recent history that the mapping consumes.</summary>
    public readonly struct DifficultyAggregate
    {
        /// <summary>Number of samples in the window (0..sampleWindow).</summary>
        public readonly int SampleCount;

        /// <summary>Fraction of sampled encounters in which the player died (0..1).</summary>
        public readonly float DeathRate;

        /// <summary>Mean actual/expected duration across the window.</summary>
        public readonly float ClearRatio;

        /// <summary>Mean damageTaken/damageDealt across the window (large when the player
        /// dealt nothing). Consumed by the dominating veto in the pressure classifier.</summary>
        public readonly float DamageTakenRatio;

        public DifficultyAggregate(int sampleCount, float deathRate, float clearRatio, float damageTakenRatio)
        {
            SampleCount = sampleCount;
            DeathRate = deathRate;
            ClearRatio = clearRatio;
            DamageTakenRatio = damageTakenRatio;
        }
    }

    /// <summary>How a single encounter reads for the pressure system.</summary>
    public enum EncounterVerdict
    {
        /// <summary>Neither dominating nor struggling -- pressure decays at the normal rate.</summary>
        Normal = 0,
        /// <summary>Low deaths, fast clear, and not chewed up doing it -- pressure BUILDS.</summary>
        Dominating = 1,
        /// <summary>High deaths or slow clears -- pressure drops FASTER and the base falls.</summary>
        Struggling = 2,
    }

    /// <summary>
    /// The whole dynamic-difficulty decision as pure functions. Every method is TOTAL: it
    /// never throws, and every degenerate input (0, negative, NaN, Infinity, a collapsed
    /// authored range) is mapped to a defined result rather than propagating. That
    /// totality is load-bearing -- a single NaN entering this chain would flow straight
    /// into enemy max HP and silently produce unkillable or instantly-dead enemies with no
    /// log line anywhere.
    /// </summary>
    public static class DifficultyMath
    {
        /// <summary>The exact neutral multiplier. Returned as a literal below the sample gate
        /// so the early-game case is bit-exact 1.0 with no arithmetic performed at all.</summary>
        public const float Neutral = 1f;

        /// <summary>Below this, two ends of a normalize range count as the SAME value and the
        /// range is treated as collapsed. Ranges come from JSON, so this is a real risk, not
        /// a theoretical one.</summary>
        private const float RangeEpsilon = 1e-6f;

        // =====================================================================
        //  NORMALIZE -- explicit contract, written out rather than borrowed
        // =====================================================================

        /// <summary>
        /// Where <paramref name="value"/> sits between <paramref name="from"/> and
        /// <paramref name="to"/>, as a fraction. CONTRACT: the result is ALWAYS CLAMPED to
        /// [0, 1]. This header states what the code does -- a comment describing behaviour
        /// the code does not have is how a P0 shipped tonight.
        ///
        /// Written out here rather than calling Mathf.InverseLerp for two reasons:
        ///   1. Mathf is not unit-testable headless -- this whole class exists to keep the
        ///      decision drivable from an EditMode test with no Unity player loop.
        ///   2. Mathf.InverseLerp's clamping SURPRISES readers. It clamps internally, so a
        ///      Clamp01() wrapper around it is a redundant no-op, and it can NEVER
        ///      extrapolate outside [0,1] -- if extrapolation is ever wanted, the manual
        ///      form (value - a) / (b - a) is required and Mathf will not provide it. Both
        ///      of those have caused wrong code to be written and reviewed as correct.
        ///
        /// NaN / INFINITY value -> 0.5, the mid-scale "no information" reading.
        /// DEGENERATE RANGE (|to - from| below <see cref="RangeEpsilon"/>) -> a STEP:
        /// 1 once the value has reached the collapsed point, else 0. Ranges come from JSON,
        /// so an author collapsing one is a real risk, and the division would otherwise
        /// produce a NaN that flows silently all the way into enemy max HP. Oracle case
        /// [no-nan] pins this.
        /// </summary>
        public static float SafeInverseLerp(float from, float to, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0.5f;
            if (float.IsNaN(from) || float.IsInfinity(from)) return 0.5f;
            if (float.IsNaN(to) || float.IsInfinity(to)) return 0.5f;

            float span = to - from;
            float mag = span < 0f ? -span : span;
            if (mag < RangeEpsilon)
                return value >= to ? 1f : 0f;   // collapsed range degrades to a step

            return Clamp((value - from) / span, 0f, 1f);
        }

        // =====================================================================
        //  SIGNALS -- unsigned [0,1], "1 = mastery". ONE shared PerformanceScore
        //  is used for BOTH the authored target and the actual history; that
        //  sharing is what makes neutral EXACT.
        // =====================================================================

        /// <summary>The death signal in [0, 1]: 1 at <see cref="DifficultyProfile.DeathMasteryBound"/>,
        /// 0 at <see cref="DifficultyProfile.DeathStruggleBound"/>.</summary>
        public static float DeathScore(float deathRate, DifficultyProfile profile)
        {
            var p = Safe(profile);
            // Guard the INPUT before use, and fall back to the AUTHORED TARGET -- not to a
            // convenient literal. A corrupt sample must read as NEUTRAL, never as biased:
            // falling back to a hardcoded 1.0 clearRatio against a 0.65 target silently
            // dragged an otherwise on-target player to 0.921, ~8% easier, with no signal.
            float dr = Clamp(Sanitize(deathRate, p.TargetDeathRate), 0f, 1f);
            return SafeInverseLerp(p.DeathStruggleBound, p.DeathMasteryBound, dr);
        }

        /// <summary>The clear-time signal in [0, 1]: 1 at <see cref="DifficultyProfile.FastClearRatio"/>,
        /// 0 at <see cref="DifficultyProfile.SlowClearRatio"/>.</summary>
        public static float TimeScore(float clearRatio, DifficultyProfile profile)
        {
            var p = Safe(profile);
            // Same rule as DeathScore: the fallback is the AUTHORED TARGET, so a NaN or
            // Infinity sample lands on exactly the target score and contributes zero
            // deviation. Oracle case [nan-is-neutral].
            float cr = Sanitize(clearRatio, p.TargetClearRatio);
            if (cr < 0f) cr = 0f;
            return SafeInverseLerp(p.SlowClearRatio, p.FastClearRatio, cr);
        }

        /// <summary>
        /// The blended UNSIGNED performance score in [0, 1]. Weights are NORMALISED by their
        /// sum: an author who writes 0.6/0.6 gets the intended 50/50 split rather than a
        /// silently out-of-range score. (The authored 0.55 + 0.45 already sums to 1.0; the
        /// oracle asserts that, because a retune that breaks the sum looks harmless in review.)
        /// </summary>
        public static float PerformanceScore(float deathRate, float clearRatio, DifficultyProfile profile)
        {
            var p = Safe(profile);
            float wSum = p.DeathWeight + p.TimeWeight;
            if (wSum <= 0f) wSum = 1f;
            float score = (DeathScore(deathRate, p) * p.DeathWeight + TimeScore(clearRatio, p) * p.TimeWeight) / wSum;
            return Clamp(score, 0f, 1f);
        }

        /// <summary>
        /// The performance score produced by the profile's OWN authored targets -- `pTarget`.
        /// Computed FROM the profile through the SAME pipeline as the actual score, never
        /// hardcoded, so retuning <see cref="DifficultyProfile.TargetDeathRate"/> or
        /// <see cref="DifficultyProfile.TargetClearRatio"/> genuinely moves the neutral point.
        /// Oracle case [target-is-live] proves exactly that, which is what keeps both target
        /// fields from becoming decorative dead keys.
        /// </summary>
        public static float PerformanceScoreAtTarget(DifficultyProfile profile)
        {
            var p = Safe(profile);
            return PerformanceScore(p.TargetDeathRate, p.TargetClearRatio, p);
        }

        /// <summary>
        /// The signed, per-side-normalized deviation from target, in [-1, +1] and EXACTLY 0
        /// at the authored targets.
        ///
        /// PER-SIDE normalization, and why there is no gain constant: raw deviation spans
        /// [-targetScore, +(1 - targetScore)] -- with the authored defaults that is
        /// [-0.5987, +0.4013], so the negative side has half again more range than the
        /// positive. ONE shared scale factor cannot normalize both: an earlier revision used
        /// `* 1.8f`, which saturated the negative side at -0.5556 but capped the positive at
        /// 0.4013 * 1.8 = 0.722, leaving maxMultiplier permanently unreachable -- a dead
        /// rail. Dividing each side by ITS OWN available range makes both rails exactly
        /// reachable, deletes the magic constant, and AUTO-ADAPTS when a target is retuned.
        /// </summary>
        public static float Deviation(float deathRate, float clearRatio, DifficultyProfile profile)
        {
            var p = Safe(profile);
            float targetScore = PerformanceScoreAtTarget(p);
            float actualScore = PerformanceScore(deathRate, clearRatio, p);
            float deviation = actualScore - targetScore;

            float headroom = deviation >= 0f ? (1f - targetScore) : targetScore;
            if (headroom < RangeEpsilon) headroom = RangeEpsilon;
            return Clamp(deviation / headroom, -1f, 1f);
        }

        // =====================================================================
        //  SHAPING -- curve the MAGNITUDE, never the blend
        // =====================================================================

        /// <summary>
        /// SmoothStep on [0,1]: 3t^2 - 2t^3. Fixes BOTH endpoints (0 -> 0, 1 -> 1), which is
        /// exactly why it is safe to apply to a magnitude and catastrophic to apply to a
        /// blended score whose neutral is not 0.5.
        /// </summary>
        public static float SmoothStep01(float t)
        {
            float x = Clamp(Sanitize(t, 0f), 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// Shapes a signed deviation, preserving sign and both fixed points. Blends linear
        /// toward SmoothStep by <see cref="DifficultyProfile.ScoreSmoothing"/> so the eased
        /// feel is tunable without any risk of moving neutral: at deviation 0 every blend
        /// gives 0, and at |deviation| 1 every blend gives 1.
        /// </summary>
        public static float ShapeDeviation(float deviation, DifficultyProfile profile)
        {
            var p = Safe(profile);
            float d = Clamp(Sanitize(deviation, 0f), -1f, 1f);
            float mag = d < 0f ? -d : d;
            float s = Clamp(p.ScoreSmoothing, 0f, 1f);
            float shapedMag = mag * (1f - s) + SmoothStep01(mag) * s;
            // Sign restored explicitly. NOTE THE TRAP: UnityEngine.Mathf.Sign(0f) returns
            // +1f, NOT 0 -- harmless while it multiplies a zero magnitude, but a later
            // refactor that multiplies something non-zero would silently snap NEUTRAL to the
            // positive rail. Sign() below returns 0 for 0.
            return Sign(d) * shapedMag;
        }

        /// <summary>Sign that returns 0 for 0 -- unlike UnityEngine.Mathf.Sign, which returns +1.</summary>
        internal static float Sign(float v)
        {
            if (v > 0f) return 1f;
            if (v < 0f) return -1f;
            return 0f;
        }

        // =====================================================================
        //  THE EARLY-GAME GATE
        // =====================================================================

        /// <summary>
        /// How much AUTHORITY the system has, given the sample count.
        ///
        /// Below <see cref="DifficultyProfile.MinSamples"/> this is 0 and callers return a
        /// LITERAL 1.0 -- the owner's "early game stays near 1.0" row, made exact. The
        /// sketch had NO gate at all: its empty-history defaults (deathRate 0.2, clearRatio
        /// 0.7) fed straight into the formula and produced a non-1.0 multiplier on encounter
        /// ONE, before the game had learned anything about the player.
        ///
        /// From MinSamples up to <see cref="DifficultyProfile.SampleWindow"/> authority ramps
        /// in LINEARLY rather than switching on at a cliff, so the first sample past the gate
        /// nudges rather than lurches. Reaching SampleWindow gives full authority.
        /// </summary>
        public static float Confidence(int sampleCount, DifficultyProfile profile)
        {
            var p = Safe(profile);
            if (sampleCount < p.MinSamples) return 0f;
            int span = p.SampleWindow - p.MinSamples + 1;
            if (span < 1) span = 1;
            int got = sampleCount - p.MinSamples + 1;
            return Clamp((float)got / span, 0f, 1f);
        }

        // =====================================================================
        //  BASE MULTIPLIER (never includes the spike -- see Compose)
        // =====================================================================

        /// <summary>
        /// The BASE multiplier: history -> [minMultiplier, maxMultiplier], exactly 1.0 at
        /// the authored targets and exactly 1.0 below the sample gate. The spike is NEVER
        /// folded in here; a derived value is never stored in the field the base is computed
        /// into. (That conflation is what made the sketch's 45-second spike outlive its
        /// timer: the timer expired, nothing recomputed, and the spiked value stayed live
        /// until the next encounter ENDED.)
        /// </summary>
        public static float BaseMultiplier(float deathRate, float clearRatio, int sampleCount, DifficultyProfile profile)
        {
            var p = Safe(profile);

            // Bit-exact early game: no arithmetic, no rounding, no drift.
            if (sampleCount < p.MinSamples) return Neutral;

            float shaped = ShapeDeviation(Deviation(deathRate, clearRatio, p), p);

            // ASYMMETRIC map about TRUE neutral. NEVER a single Lerp across the full band:
            // the band is not symmetric (min is 0.25 below 1.0, max is 0.45 above), so
            // Lerp(min, max, (dev+1)*0.5) puts an on-target player at 1.10. Mapping the two
            // halves separately makes 1.0 exact BY CONSTRUCTION and keeps both rails
            // exactly reachable at +/-1.
            float raw = shaped >= 0f
                ? Neutral + shaped * (p.MaxMultiplier - Neutral)   // Lerp(1, max,  shaped)
                : Neutral + shaped * (Neutral - p.MinMultiplier);  // Lerp(1, min, -shaped)

            // Ramp the system's AUTHORITY in, not the multiplier's rails.
            float mult = Neutral + (raw - Neutral) * Confidence(sampleCount, p);
            return Clamp(mult, p.MinMultiplier, p.MaxMultiplier);
        }

        /// <summary>Convenience overload over a rolled-up window.</summary>
        public static float BaseMultiplier(DifficultyAggregate agg, DifficultyProfile profile)
        {
            return BaseMultiplier(agg.DeathRate, agg.ClearRatio, agg.SampleCount, profile);
        }

        // =====================================================================
        //  SPIKE COMPOSITION -- composed at READ time, always
        // =====================================================================

        /// <summary>
        /// Composes the live multiplier from the base and whether a spike is currently
        /// active. Called on EVERY read, so a spike's expiry is felt the instant it happens
        /// rather than at the next encounter boundary.
        ///
        /// Clamped BOTH ends: a Min-only clamp has no floor, and defensive symmetry costs
        /// nothing. While a spike is live the ceiling is the authored ABSOLUTE
        /// <see cref="DifficultyProfile.MaxMultiplierWithSpike"/> -- a number a human can
        /// read and reason about -- never a factor multiplied onto another rail.
        /// </summary>
        public static float Compose(float baseMultiplier, bool spikeActive, DifficultyProfile profile)
        {
            var p = Safe(profile);
            float b = Sanitize(baseMultiplier, Neutral);
            if (!p.PressureEnabled || !spikeActive)
                return Clamp(b, p.MinMultiplier, p.MaxMultiplier);
            return Clamp(b * p.SpikeMultiplier, p.MinMultiplier, p.MaxMultiplierWithSpike);
        }

        // =====================================================================
        //  LEVERS -- each toggleable, each derived from the profile
        // =====================================================================

        /// <summary>Enemy max-HP multiplier. Returns 1.0 when the lever is off.</summary>
        public static float EnemyHpMultiplier(float composed, DifficultyProfile profile)
        {
            var p = Safe(profile);
            return p.ScaleEnemyHp ? Sanitize(composed, Neutral) : Neutral;
        }

        /// <summary>
        /// Enemy damage multiplier. Honours <see cref="DifficultyProfile.EnemyDamageDownOnly"/>,
        /// which by default forbids the damage lever from ever making the player take MORE
        /// damage. Damage is the one lever players experience as unfair -- nothing on screen
        /// changes to explain a sudden one-shot -- while HP and count read as tension.
        /// </summary>
        public static float EnemyDamageMultiplier(float composed, DifficultyProfile profile)
        {
            var p = Safe(profile);
            if (!p.ScaleEnemyDamage) return Neutral;
            float m = Sanitize(composed, Neutral);
            if (p.EnemyDamageDownOnly && m > Neutral) return Neutral;
            return m;
        }

        /// <summary>
        /// Enemy COUNT multiplier: the composed multiplier's deviation from 1.0, scaled by
        /// <see cref="DifficultyProfile.CountScale"/>. The sketch hardcoded
        /// `(current - 0.75f) / 0.7f`, baking minMultiplier and the min/max span in as
        /// literals -- retuning the profile would have silently desynced the count lever from
        /// the HP lever. This form reads only the live multiplier and one authored scale, so
        /// it cannot desync.
        /// </summary>
        public static float EnemyCountMultiplier(float composed, DifficultyProfile profile)
        {
            var p = Safe(profile);
            if (!p.ScaleEnemyCount) return Neutral;
            float m = Sanitize(composed, Neutral);
            return Neutral + (m - Neutral) * p.CountScale;
        }

        /// <summary>
        /// Bends a multiplier onto the SOFTER boss curve: bosses keep only
        /// <see cref="DifficultyProfile.BossExcessRetained"/> of any increase, but
        /// <see cref="DifficultyProfile.BossReliefRetained"/> (1.0 by default) of any
        /// decrease. Deliberately asymmetric -- a boss is where players actually die, so
        /// damping the RELIEF as well would make "softer curve" a lie in the direction that
        /// matters most.
        /// </summary>
        public static float BossCurve(float composed, DifficultyProfile profile)
        {
            var p = Safe(profile);
            float m = Sanitize(composed, Neutral);
            float d = m - Neutral;
            float bent = Neutral + d * (d >= 0f ? p.BossExcessRetained : p.BossReliefRetained);
            return Clamp(bent, p.MinMultiplier, p.MaxMultiplierWithSpike);
        }

        /// <summary>Boss max-HP multiplier (softer curve). 1.0 when the lever is off.</summary>
        public static float BossHpMultiplier(float composed, DifficultyProfile profile)
        {
            var p = Safe(profile);
            return p.ScaleBossHp ? BossCurve(composed, p) : Neutral;
        }

        /// <summary>Boss damage multiplier (softer curve), with its own down-only guard --
        /// the same reasoning as trash damage, amplified: a boss one-shot ends a whole run.</summary>
        public static float BossDamageMultiplier(float composed, DifficultyProfile profile)
        {
            var p = Safe(profile);
            if (!p.ScaleBossDamage) return Neutral;
            float m = BossCurve(composed, p);
            if (p.BossDamageDownOnly && m > Neutral) return Neutral;
            return m;
        }

        // =====================================================================
        //  PRESSURE
        // =====================================================================

        /// <summary>
        /// Classifies ONE encounter for the pressure system. "Dominating" additionally
        /// requires that the player was not chewed up getting there
        /// (<see cref="DifficultyProfile.DominatingMaxDamageTakenRatio"/>) -- otherwise a
        /// desperate, nearly-fatal fast clear reads as mastery and gets answered with a
        /// spike, which is precisely the moment a spike feels arbitrary.
        /// </summary>
        public static EncounterVerdict Classify(EncounterSample sample, DifficultyProfile profile)
        {
            var p = Safe(profile);
            float clearRatio = sample.ClearRatio;
            float deathRate = sample.PlayerDied ? 1f : 0f;

            if (deathRate > p.StrugglingDeathRate || clearRatio > p.StrugglingClearRatio)
                return EncounterVerdict.Struggling;

            if (deathRate < p.DominatingDeathRate &&
                clearRatio < p.DominatingClearRatio &&
                sample.DamageTakenRatio <= p.DominatingMaxDamageTakenRatio)
                return EncounterVerdict.Dominating;

            return EncounterVerdict.Normal;
        }

        /// <summary>
        /// The pressure value after one encounter of the given verdict. Pure: current
        /// pressure in, next pressure out, always clamped to [0, 1].
        /// </summary>
        public static float NextPressure(float pressure, EncounterVerdict verdict, DifficultyProfile profile)
        {
            var p = Safe(profile);
            if (!p.PressureEnabled) return 0f;
            float cur = Clamp(Sanitize(pressure, 0f), 0f, 1f);
            switch (verdict)
            {
                case EncounterVerdict.Dominating:
                    return Clamp(cur + p.PressureBuildRate, 0f, 1f);
                case EncounterVerdict.Struggling:
                    return Clamp(cur - p.PressureDecayRate * p.StrugglingDecayFactor, 0f, 1f);
                default:
                    return Clamp(cur - p.PressureDecayRate, 0f, 1f);
            }
        }

        /// <summary>
        /// True when the given pressure should fire a spike. A spike can never fire while one
        /// is already live, and after firing the caller soft-resets pressure to
        /// <see cref="DifficultyProfile.SpikeResetPressure"/> (validated to sit BELOW the
        /// threshold), so an instant re-trigger is structurally impossible.
        /// </summary>
        public static bool ShouldSpike(float pressure, bool spikeAlreadyActive, DifficultyProfile profile)
        {
            var p = Safe(profile);
            if (!p.PressureEnabled || spikeAlreadyActive) return false;
            return Sanitize(pressure, 0f) >= p.SpikeThreshold;
        }

        /// <summary>
        /// Whether a spike is live at <paramref name="nowSeconds"/>, given its absolute expiry
        /// timestamp. An absolute timestamp compared at READ time -- rather than a countdown
        /// decremented in Update() -- is what lets the headless oracle prove the expiry
        /// (case [spike-expires]) with no play session and no MonoBehaviour tick. It also
        /// makes expiry exact rather than frame-quantised, and it removes the need for an
        /// Update() on a DontDestroyOnLoad singleton whose only job was one timer.
        /// </summary>
        public static bool IsSpikeActive(double nowSeconds, double spikeExpiresAtSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsNaN(spikeExpiresAtSeconds)) return false;
            return nowSeconds < spikeExpiresAtSeconds;
        }

        // =====================================================================
        //  HELPERS -- System.Math only (Math.Clamp is not on this netstandard)
        // =====================================================================

        private static DifficultyProfile Safe(DifficultyProfile profile)
        {
            return profile ?? new DifficultyProfile().Validate();
        }

        /// <summary>Maps NaN / Infinity to a caller-chosen sane default. Degenerate inputs are
        /// a certainty here (a wave cleared on the spawn frame, an unset expected duration),
        /// and a single NaN entering the running average would poison every later read --
        /// silently, straight into enemy max HP.</summary>
        private static float Sanitize(float v, float fallback)
        {
            return (float.IsNaN(v) || float.IsInfinity(v)) ? fallback : v;
        }

        internal static float Clamp(float v, float lo, float hi)
        {
            if (float.IsNaN(v)) return lo;
            if (lo > hi) { float t = lo; lo = hi; hi = t; }
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
