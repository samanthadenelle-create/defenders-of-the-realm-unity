// =============================================================================
// BuildTimerConfig — the tunable knobs for build/upgrade timers + ad-skip (WO-172).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog
//
// CoC-style time-sink tuning, kept OUT of logic (WO-172 constraint: "all durations
// in a tunable SO/constants, never hard-coded"). One asset drives:
//   • the HYBRID duration curve — short early (snappy onboarding), scaling
//     super-linearly to hours for high tiers (the endgame drag) via DurationForTier;
//   • the rewarded-ad skip chunk + daily cap (the opt-in monetization lever);
//   • the premium instant-finish crystal price;
//   • the free build-slot count (concurrency / scarcity → sellable slots later).
//
// Lives in DeNelle.Core (pure data, no Village ref) so both the Village
// BuildTimerService AND a future Core-side replay/validation can read the same
// numbers. BuildTimerService resolves an instance via Resources.Load (path below)
// and falls back to code defaults if the asset is absent — so the system works with
// zero scene/asset authoring and the owner can drop an authored asset to retune.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// Tunable durations / ad-skip / instant-finish / build-slot knobs for the
    /// WO-172 construction-timer system. Author one asset to retune; code defaults
    /// stand if none exists.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Economy/Build Timer Config", fileName = "BuildTimerConfig")]
    public sealed class BuildTimerConfig : ScriptableObject
    {
        /// <summary>Resources path BuildTimerService loads by default (no extension).</summary>
        public const string ResourcesPath = "Economy/BuildTimerConfig";

        [Header("Hybrid duration curve (tier 0 = first build)")]
        [Tooltip("Base seconds for a tier-0 build — keep onboarding snappy (seconds–minutes).")]
        [Min(0f)] public float baseBuildSeconds = 15f;

        [Tooltip("Per-tier multiplier — durations scale super-linearly: tierSeconds = base * pow(growth, tier). " +
                 ">1 makes high tiers the hours-long endgame drag that drives ad-watches/spend.")]
        [Min(1f)] public float tierGrowth = 3.0f;

        [Tooltip("Hard ceiling on any single job's duration (seconds). Default 48h — nothing drags past this.")]
        [Min(0f)] public float maxDurationSeconds = 48f * 3600f;

        [Tooltip("Upgrades multiply the same tier curve by this (upgrades a touch longer than a fresh build of the same tier).")]
        [Min(0f)] public float upgradeMultiplier = 1.25f;

        [Header("Rewarded-ad skip (opt-in, store-build only)")]
        [Tooltip("Seconds knocked off the remaining timer per rewarded-ad watch (the fixed chunk). Default 15 min.")]
        [Min(0f)] public float adSkipSeconds = 15f * 60f;

        [Tooltip("Max rewarded-ad skips a player may use per day across ALL jobs (the daily cap). 0 = unlimited.")]
        [Min(0)] public int adSkipsPerDay = 10;

        [Header("Premium instant-finish (convenience IAP, not power)")]
        [Tooltip("Aether-crystal price to instantly finish a job, scaled by remaining minutes. " +
                 "0 disables the paid skip (ad-skip still works).")]
        [Min(0)] public int instantFinishCrystalsPerMinute = 1;

        [Tooltip("Minimum crystal price for any instant-finish (so near-done jobs still cost something).")]
        [Min(0)] public int instantFinishMinCrystals = 5;

        [Header("Build slots (concurrency / scarcity)")]
        [Tooltip("How many jobs may run at once for free. CoC-style scarcity — extra slots are a future unlock/purchase.")]
        [Min(1)] public int freeBuildSlots = 2;

        /// <summary>
        /// Hybrid curve: duration (seconds) for a <paramref name="tier"/> job of
        /// <paramref name="type"/>. Super-linear via <see cref="tierGrowth"/>,
        /// clamped to <see cref="maxDurationSeconds"/>. Tier is the TARGET level
        /// (0 = first build / first upgrade step).
        /// </summary>
        public float DurationSecondsForTier(int tier, BuildJobKind type)
        {
            if (tier < 0) tier = 0;
            float seconds = baseBuildSeconds * Mathf.Pow(Mathf.Max(1f, tierGrowth), tier);
            if (type == BuildJobKind.Upgrade) seconds *= Mathf.Max(0f, upgradeMultiplier);
            return Mathf.Min(seconds, Mathf.Max(0f, maxDurationSeconds));
        }

        /// <summary>Crystal price to instant-finish a job with <paramref name="remainingSeconds"/> left.</summary>
        public int InstantFinishPrice(double remainingSeconds)
        {
            if (instantFinishCrystalsPerMinute <= 0) return 0;   // paid skip disabled
            double minutes = Mathf.Max(0f, (float)remainingSeconds) / 60.0;
            int price = Mathf.CeilToInt((float)minutes * instantFinishCrystalsPerMinute);
            return Mathf.Max(price, instantFinishMinCrystals);
        }

        // Code-default fallback so the system runs with no authored asset.
        private static BuildTimerConfig s_default;

        /// <summary>A code-built default config instance (used when no asset is authored).</summary>
        public static BuildTimerConfig CreateDefault()
        {
            if (s_default != null) return s_default;
            s_default = CreateInstance<BuildTimerConfig>();
            s_default.name = "BuildTimerConfig (code default)";
            return s_default;
        }
    }

    /// <summary>
    /// Duration-curve flavour. Mirrors <see cref="DeNelle.Core.State.BuildJobType"/>
    /// but kept local so this pure-data config has no dependency direction concern.
    /// </summary>
    public enum BuildJobKind
    {
        Build = 0,
        Upgrade = 1,
    }
}
