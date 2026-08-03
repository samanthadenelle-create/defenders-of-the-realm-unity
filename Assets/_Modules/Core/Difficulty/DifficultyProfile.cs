// =============================================================================
// DifficultyProfile -- the authored tuning table for Dynamic Difficulty Scaling.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Adaptive
//
// PLAIN DATA ONLY. Not a ScriptableObject, not a MonoBehaviour -- the reference
// sketch declared this [Serializable] and then called
// ScriptableObject.CreateInstance<DynamicDifficultyProfile>() on it, which is a
// type error that cannot compile. There is exactly ONE way this type is built:
// DifficultyProfileCatalog parses Data/Canonical/difficulty-profile.json through
// DeNelle.Core.CanonicalJson (Resources dual-copy first, StreamingAssets
// fallback) -- the same idiom every other catalog uses. No second config
// mechanism is introduced.
//
// EVERY FIELD BELOW HAS A REAL CONSUMER. The authored-key/dead-key contract is
// enforced headless by DynamicDifficultyRegression case [dead-keys]: any key in
// the JSON that does not bind to a field here, and any field here that nothing
// outside this file reads, FAILS the gate. That rule exists because dead
// authored keys have bitten this project four times in one week (Cathedral mage
// keys, canHitAir, centralBuilding, elementBias).
//
// DELIBERATELY ABSENT: `scaleAggressiveTactics`. See the ruling in
// DifficultyMath's header -- the only surface it could drive
// (EnemyBrain.KiterTactics et al.) is a STATIC SHARED archetype, so writing to
// it would corrupt every enemy for the whole session. An unimplementable key is
// a dead key; it is not authored.
//
// RELATIONSHIP TO DeNelle.Core.State.DifficultyTuning: they are DISJOINT and
// compose by multiplication on different quantities. DifficultyTuning is the
// PLAYER-CHOSEN Easy/Normal/Hard setting and governs exactly one thing -- the
// length of the BETWEEN-WAVE BUILD WINDOW (a countdown multiplier). This profile
// governs enemy STAT/COUNT scaling inside an encounter, and never touches a
// countdown. Neither reads the other, so they cannot disagree; a player on Easy
// gets a longer build window AND a dynamic stat multiplier derived from how that
// player is actually performing. If they are ever merged, merge them here -- not
// by adding a second countdown dial.
// =============================================================================

using Newtonsoft.Json;

namespace DeNelle.Core.Adaptive
{
    /// <summary>
    /// The authored tuning table for dynamic difficulty. Field initialisers ARE the
    /// built-in fallback used when difficulty-profile.json is missing or unparseable,
    /// so the system degrades to a sane, shipped-tested table rather than to zeros.
    /// </summary>
    [System.Serializable]
    public sealed class DifficultyProfile
    {
        /// <summary>Schema version. Read by the catalog loader, which Warns on a mismatch.</summary>
        [JsonProperty("version")] public int Version = 1;

        // =====================================================================
        //  SAMPLING / EARLY-GAME GATE
        // =====================================================================

        /// <summary>How many recent encounters the rolling history keeps, and the sample
        /// count at which the system reaches FULL authority. Also the ring capacity.</summary>
        [JsonProperty("sampleWindow")] public int SampleWindow = 10;

        /// <summary>Below this many samples the multiplier is EXACTLY 1.0 -- returned as a
        /// literal, with no arithmetic performed, so a brand-new player can never be
        /// scaled off one unlucky death. Between this and <see cref="SampleWindow"/> the
        /// system ramps in gradually (see DifficultyMath.Confidence).</summary>
        [JsonProperty("minSamples")] public int MinSamples = 3;

        // =====================================================================
        //  THE TWO SIGNALS -- anchored so that "performing at target" == score 0
        // =====================================================================

        /// <summary>The death rate the curve treats as NEUTRAL. This is the PIVOT of the death
        /// signal: a history sitting exactly here scores 0, and a score of 0 maps to EXACTLY
        /// 1.0 by construction. This field is therefore load-bearing, not decorative -- the
        /// oracle case [target-is-live] proves that moving it moves the neutral point.</summary>
        [JsonProperty("targetDeathRate")] public float TargetDeathRate = 0.22f;

        /// <summary>The death rate at which the death signal scores 1.0 (total mastery).
        /// The signal normalizes from <see cref="DeathStruggleBound"/> up to this.</summary>
        [JsonProperty("deathMasteryBound")] public float DeathMasteryBound = 0.08f;

        /// <summary>The death rate at which the death signal scores 0.0 (fully struggling).</summary>
        [JsonProperty("deathStruggleBound")] public float DeathStruggleBound = 0.40f;

        /// <summary>clearRatio = actualDuration / expectedDuration. The PIVOT of the time
        /// signal: a history sitting exactly here scores 0 -> multiplier exactly 1.0. The
        /// owner's authored par is 0.65 (clearing in 65% of the designed time is "on target").</summary>
        [JsonProperty("targetClearRatio")] public float TargetClearRatio = 0.65f;

        /// <summary>The clear ratio at which the time signal scores 1.0 (total mastery).</summary>
        [JsonProperty("fastClearRatio")] public float FastClearRatio = 0.40f;

        /// <summary>The clear ratio at which the time signal scores 0.0 (fully struggling).</summary>
        [JsonProperty("slowClearRatio")] public float SlowClearRatio = 1.10f;

        /// <summary>Blend weight on the death signal (the owner's ~55/45 split).</summary>
        [JsonProperty("deathWeight")] public float DeathWeight = 0.55f;

        /// <summary>Blend weight on the clear-time signal (the owner's ~55/45 split).</summary>
        [JsonProperty("timeWeight")] public float TimeWeight = 0.45f;

        /// <summary>
        /// How much the deviation MAGNITUDE is eased: 0 = purely linear, 1 = full SmoothStep.
        /// Applied to |deviation| with the sign preserved, NEVER to the blended score --
        /// SmoothStep fixes both 0 and 1, so curving the magnitude gives the eased "difficulty
        /// drifts rather than steps" feel while leaving neutral at exactly 1.0 and both rails
        /// exactly reachable. Curving the blend BEFORE the map is what put an on-target player
        /// at 1.20x in an earlier iteration.
        /// </summary>
        [JsonProperty("scoreSmoothing")] public float ScoreSmoothing = 1.0f;

        // =====================================================================
        //  SAFETY RAILS
        // =====================================================================

        /// <summary>Hard floor on the composed multiplier. The score -> multiplier mapping
        /// is DERIVED from this (never a baked literal), so retuning it retunes the curve.</summary>
        [JsonProperty("minMultiplier")] public float MinMultiplier = 0.75f;

        /// <summary>Hard ceiling on the BASE multiplier (no spike active).</summary>
        [JsonProperty("maxMultiplier")] public float MaxMultiplier = 1.45f;

        /// <summary>
        /// ABSOLUTE hard ceiling on the COMPOSED multiplier (base x spike). A real number a
        /// human can read, NOT a factor multiplied onto another rail.
        ///
        /// Two earlier shapes of this rail were broken and are pinned against by the oracle:
        ///   - `maxMultiplier * 1.15f` = 1.667: a hidden 15% the spike was silently allowed
        ///     to exceed the stated ceiling by.
        ///   - `maxMultiplier * maxSpikeMultiplier` = 1.45 * 1.25 = 1.8125: HIGHER than the
        ///     rail it replaced, and DEAD -- the largest composable value is
        ///     maxMultiplier * spikeMultiplier = 1.45 * 1.18 = 1.711, which never reaches
        ///     1.8125, so the "rail" was decoration that reads as protection during review.
        ///
        /// 1.60 BINDS: it engages for every base multiplier above 1.60/1.18 = 1.3559, which is
        /// inside the reachable base band (base tops out at 1.45). Oracle case
        /// [rails-reachable] fails the suite if any authored rail becomes unreachable again.
        /// </summary>
        [JsonProperty("maxMultiplierWithSpike")] public float MaxMultiplierWithSpike = 1.60f;

        // =====================================================================
        //  BOSS CURVE -- "slightly softer than trash", asymmetrically
        // =====================================================================

        /// <summary>Fraction of the ABOVE-1.0 excess a boss keeps. 0.55 means a trash
        /// multiplier of 1.40 becomes a boss multiplier of 1 + 0.40*0.55 = 1.22 -- the boss
        /// ramps up more gently than trash, which is the owner's "softer curve".</summary>
        [JsonProperty("bossExcessRetained")] public float BossExcessRetained = 0.55f;

        /// <summary>Fraction of the BELOW-1.0 relief a boss keeps. 1.0 = a struggling player
        /// gets the FULL easing on bosses. Deliberately asymmetric with
        /// <see cref="BossExcessRetained"/>: bosses are where players actually die, so
        /// damping the relief too would make the softer curve a lie in the direction that
        /// matters most.</summary>
        [JsonProperty("bossReliefRetained")] public float BossReliefRetained = 1.00f;

        // =====================================================================
        //  LEVERS -- each individually toggleable, per the owner's brief
        // =====================================================================

        /// <summary>Scale enemy max HP. Recommended ON: HP reads as tension, not unfairness.</summary>
        [JsonProperty("scaleEnemyHp")] public bool ScaleEnemyHp = true;

        /// <summary>Scale enemy contact/ranged damage. Recommended ON only in company with
        /// <see cref="EnemyDamageDownOnly"/> -- see that field.</summary>
        [JsonProperty("scaleEnemyDamage")] public bool ScaleEnemyDamage = true;

        /// <summary>When true, the enemy damage lever may only ever make the player take LESS
        /// damage (multiplier clamped to &lt;= 1.0); upward pressure is expressed through HP and
        /// count instead. Damage is the one lever players read as unfair ("I got one-shot for
        /// no reason") because nothing on screen changed to explain it. Defaults TRUE.</summary>
        [JsonProperty("enemyDamageDownOnly")] public bool EnemyDamageDownOnly = true;

        /// <summary>Scale the number of enemies in a wave, through <see cref="CountScale"/>.</summary>
        [JsonProperty("scaleEnemyCount")] public bool ScaleEnemyCount = true;

        /// <summary>How much of the multiplier's deviation from 1.0 the enemy COUNT lever
        /// takes. 0.5 means a 1.40 multiplier becomes 1.20 on count -- count changes are the
        /// most visible and the most expensive (pathing, perf), so they move at half rate.
        /// Derived from the live multiplier, never from baked min/max literals.</summary>
        [JsonProperty("countScale")] public float CountScale = 0.50f;

        /// <summary>Scale boss max HP (through the softer boss curve).</summary>
        [JsonProperty("scaleBossHp")] public bool ScaleBossHp = true;

        /// <summary>Scale boss damage (through the softer boss curve).</summary>
        [JsonProperty("scaleBossDamage")] public bool ScaleBossDamage = true;

        /// <summary>Down-only guard for the boss damage lever, for the same reason as
        /// <see cref="EnemyDamageDownOnly"/> -- amplified, because a boss one-shot ends a run.</summary>
        [JsonProperty("bossDamageDownOnly")] public bool BossDamageDownOnly = true;

        // =====================================================================
        //  PRESSURE SYSTEM -- temporary spikes that answer mastery
        // =====================================================================

        /// <summary>Master toggle for the pressure/spike half. When false the composed
        /// multiplier is exactly the base multiplier and no spike can ever fire.</summary>
        [JsonProperty("pressureEnabled")] public bool PressureEnabled = true;

        /// <summary>Pressure added per DOMINATING encounter.</summary>
        [JsonProperty("pressureBuildRate")] public float PressureBuildRate = 0.08f;

        /// <summary>Pressure removed per NON-dominating encounter (the slow default decay).</summary>
        [JsonProperty("pressureDecayRate")] public float PressureDecayRate = 0.04f;

        /// <summary>Multiplier on <see cref="PressureDecayRate"/> for a STRUGGLING encounter --
        /// the owner's "pressure drops FASTER" row. 2.0 = twice the normal bleed.</summary>
        [JsonProperty("strugglingDecayFactor")] public float StrugglingDecayFactor = 2.0f;

        /// <summary>Pressure (0..1) at or above which a spike fires.</summary>
        [JsonProperty("spikeThreshold")] public float SpikeThreshold = 0.75f;

        /// <summary>Multiplier applied on top of the base multiplier while a spike is live.</summary>
        [JsonProperty("spikeMultiplier")] public float SpikeMultiplier = 1.18f;

        /// <summary>How long a spike lasts, in seconds. Enforced by an absolute expiry
        /// TIMESTAMP compared at read time, not by an Update() countdown -- so the spike ends
        /// the instant it is due, even if no encounter finishes in between.</summary>
        [JsonProperty("spikeDurationSeconds")] public float SpikeDurationSeconds = 45f;

        /// <summary>Pressure is soft-reset to this on a spike (not to zero), so the ramp back
        /// is quicker than the first climb but a spike still cannot instantly re-fire.</summary>
        [JsonProperty("spikeResetPressure")] public float SpikeResetPressure = 0.35f;

        /// <summary>Death rate STRICTLY BELOW which an encounter may count as dominating.</summary>
        [JsonProperty("dominatingDeathRate")] public float DominatingDeathRate = 0.12f;

        /// <summary>Clear ratio STRICTLY BELOW which an encounter may count as dominating.</summary>
        [JsonProperty("dominatingClearRatio")] public float DominatingClearRatio = 0.55f;

        /// <summary>damageTaken/damageDealt must be at or below this for an encounter to count
        /// as dominating. This is what stops a player who barely survived a fast clear from
        /// being told they are dominating -- and it is the consumer that makes the owner's
        /// damageTaken/damageDealt tracking real rather than recorded-and-ignored.</summary>
        [JsonProperty("dominatingMaxDamageTakenRatio")] public float DominatingMaxDamageTakenRatio = 0.35f;

        /// <summary>Death rate STRICTLY ABOVE which an encounter counts as struggling.</summary>
        [JsonProperty("strugglingDeathRate")] public float StrugglingDeathRate = 0.35f;

        /// <summary>Clear ratio STRICTLY ABOVE which an encounter counts as struggling.</summary>
        [JsonProperty("strugglingClearRatio")] public float StrugglingClearRatio = 0.95f;

        // =====================================================================
        //  REPAIR
        // =====================================================================

        /// <summary>
        /// Repairs any authored value that would make the mapping degenerate (a zero span,
        /// an inverted rail, a negative weight). Called once by the catalog after parse and
        /// by the pure math layer defensively, so NO downstream code has to defend against
        /// a divide-by-zero the tuner introduced. Idempotent.
        /// </summary>
        public DifficultyProfile Validate()
        {
            if (SampleWindow < 1) SampleWindow = 1;
            if (MinSamples < 1) MinSamples = 1;
            if (MinSamples > SampleWindow) MinSamples = SampleWindow;

            // NOTE: the bounds are NOT force-separated from the targets here. A collapsed
            // range is handled TOTALLY downstream by DifficultyMath.SafeInverseLerp (which
            // degrades to a step rather than dividing by zero), and silently nudging an
            // author's numbers would hide the mistake instead of surviving it. What IS
            // repaired is anything that would invert the meaning of a rail.
            if (TargetDeathRate < 0f) TargetDeathRate = 0f;
            if (TargetClearRatio < 0f) TargetClearRatio = 0f;

            if (DeathWeight < 0f) DeathWeight = 0f;
            if (TimeWeight < 0f) TimeWeight = 0f;
            if (DeathWeight + TimeWeight <= 0f) { DeathWeight = 0.55f; TimeWeight = 0.45f; }

            if (ScoreSmoothing < 0f) ScoreSmoothing = 0f;
            if (ScoreSmoothing > 1f) ScoreSmoothing = 1f;

            if (MinMultiplier <= 0f) MinMultiplier = 0.0001f;
            if (MinMultiplier > 1f) MinMultiplier = 1f;
            if (MaxMultiplier < 1f) MaxMultiplier = 1f;
            if (MaxMultiplierWithSpike < MaxMultiplier) MaxMultiplierWithSpike = MaxMultiplier;

            if (BossExcessRetained < 0f) BossExcessRetained = 0f;
            if (BossExcessRetained > 1f) BossExcessRetained = 1f;
            if (BossReliefRetained < 0f) BossReliefRetained = 0f;
            if (BossReliefRetained > 1f) BossReliefRetained = 1f;

            if (CountScale < 0f) CountScale = 0f;
            if (CountScale > 1f) CountScale = 1f;

            if (PressureBuildRate < 0f) PressureBuildRate = 0f;
            if (PressureDecayRate < 0f) PressureDecayRate = 0f;
            if (StrugglingDecayFactor < 0f) StrugglingDecayFactor = 0f;
            if (SpikeThreshold <= 0f) SpikeThreshold = 0.0001f;
            if (SpikeThreshold > 1f) SpikeThreshold = 1f;
            if (SpikeMultiplier < 1f) SpikeMultiplier = 1f;
            if (SpikeDurationSeconds < 0f) SpikeDurationSeconds = 0f;
            if (SpikeResetPressure < 0f) SpikeResetPressure = 0f;
            if (SpikeResetPressure >= SpikeThreshold) SpikeResetPressure = SpikeThreshold * 0.5f;

            return this;
        }
    }
}
