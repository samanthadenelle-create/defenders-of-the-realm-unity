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
// WO-855 Phase 4 (2026-08-03) -- the curve is now actually REACHABLE. The placement
// path used to pass a hard-coded literal tier 0, so every structure built in exactly
// baseBuildSeconds (15s) and tierGrowth was dead tuning. The tier is now derived from
// the structure's authored cost basket (TierForCost), and the defaults were retuned to
// the WO-855 sec.4.6 mobile bands -- snappy early, hours-long endgame:
//   base 15s -> 30s | upgradeMultiplier 1.25 -> 1.35 | freeBuildSlots stays 2 (scarcity)
//   tier ladder at growth 3.0: 30s | 1.5m | 4.5m | 13.5m | 40.5m | ~2h
// Against the live catalog that reads: founding + collectors + walls 30s, starter
// towers/shops 1.5m, barracks + heavy towers 4.5m, fountain 13.5m; the top two bands
// are headroom the Phase 2/3 cost retune grows into.
// There is NO Resources/Economy/BuildTimerConfig.asset in the tree -- these C# defaults
// ARE the live numbers. Author an asset only to override them.
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
        [Min(0f)] public float baseBuildSeconds = 30f;

        [Tooltip("Per-tier multiplier — durations scale super-linearly: tierSeconds = base * pow(growth, tier). " +
                 ">1 makes high tiers the hours-long endgame drag that drives ad-watches/spend.")]
        [Min(1f)] public float tierGrowth = 3.0f;

        [Tooltip("Hard ceiling on any single job's duration (seconds). Default 48h — nothing drags past this.")]
        [Min(0f)] public float maxDurationSeconds = 48f * 3600f;

        [Tooltip("Upgrades multiply the same tier curve by this (upgrades a touch longer than a fresh build of the same tier).")]
        [Min(0f)] public float upgradeMultiplier = 1.35f;

        [Header("Cost -> tier bands (WO-855 Phase 4)")]
        [Tooltip("Ascending resource-basket thresholds. A structure whose authored cost basket reaches " +
                 "thresholds[i] builds at tier i+1. Below thresholds[0] = tier 0 (the snappy early game).")]
        // CALIBRATED against the live structures-catalog basket spread (5 .. 440 as of
        // 2026-08-03), NOT invented: band 0 deliberately swallows EVERY founding piece and
        // every collector/wall (pet-house 125, collector_lumbermill 105, lumberyard 80,
        // farm 90, wall_stone 120, gate_stone 135) so the first ten minutes of a new save
        // are never gated -- the owner's hard constraint. Band 1 takes the starter towers
        // and shops (150-245), band 2 the barracks/heavy towers (270-335), band 3 the
        // fountain (440). Bands 4-5 are HEADROOM: nothing reaches them today, and they are
        // what turns the ladder into the hours-long endgame as the Phase 2/3 cost retune
        // pushes late rows up. Re-check this table whenever catalog costs move.
        public float[] tierCostThresholds = { 140f, 260f, 420f, 900f, 2000f };

        // OWNER RULING 2026-08-06. Crystals END a wait (immediate finish); an ad only
        // DENTS it. Two different products, not one product at two speeds.
        //
        // THE CAP IS A CONVERSION TRIGGER, NOT A LIMIT ON REVENUE - that is the whole
        // point and it is easy to get backwards. Her words: "if they've watched their ten
        // videos within four hours and they're still playing, they're gonna have to spend."
        // An impression pays cents; a crystal purchase pays dollars. Running out of free
        // skips WHILE STILL PLAYING is the best moment in the session to show a price.
        //
        // The numbers land on purpose: a 20-minute troop clears in 2 watches (feels free),
        // a 2-HOUR build needs 12 and the cap stops them at 10 - within sight of done and
        // 20 minutes short. That near-miss is the sell. An 8h upgrade takes 100 minutes off
        // and still leaves 6h20m, so late game leans on crystals by construction.
        [Header("Rewarded-ad skip (opt-in, store-build only)")]
        [Tooltip("Seconds knocked off the remaining timer per rewarded-ad watch.")]
        [Min(0f)] public float adSkipSeconds = 10f * 60f;

        // ROLLING WINDOW, not a calendar day. A day-reset punishes an evening player who
        // spent their allowance that morning; rolling always offers a way back in.
        //
        // WARNING FOR WHOEVER WIRES THIS: the persisted ledger is DAY-SHAPED
        // (SaveSchema AdSkipsUsedToday + AdSkipDayKey, since v13/WO-172) and CANNOT express
        // a rolling window. Implementing this needs the timestamps of recent watches (or a
        // decaying counter), i.e. a schema addition - not a config tweak. Reusing the day
        // fields would silently ship day-reset behaviour and quietly lose the ruling.
        [Tooltip("Max rewarded-ad skips allowed within the rolling window below. 0 = unlimited.")]
        [Min(0)] public int adSkipsPerWindow = 10;

        [Tooltip("Length of the ROLLING window the skip allowance is counted over. Default 4 hours.")]
        [Min(0f)] public float adSkipWindowSeconds = 4f * 60f * 60f;

        [Header("Premium instant-finish (convenience IAP, not power)")]
        [Tooltip("Aether-crystal price to instantly finish a job, scaled by remaining minutes. " +
                 "0 disables the paid skip (ad-skip still works).")]
        [Min(0)] public int instantFinishCrystalsPerMinute = 1;

        [Tooltip("Minimum crystal price for any instant-finish (so near-done jobs still cost something).")]
        [Min(0)] public int instantFinishMinCrystals = 5;

        // OWNER RULING 2026-08-06: "for the free ones (first time builds, other than the pallets),
        // can we make if free then timer is 5 seconds?"
        //
        // WHAT THE CATALOG ACTUALLY SAYS (checked before implementing, not assumed): NOTHING in the
        // game is free. All 29 costed structures-catalog entries have a non-zero basket -- the
        // cheapest is deco_torch at 5 wood, then wall_wood at 20. A literal "if cost == 0" rule
        // would fire on ZERO structures. So the rule is keyed to what she actually meant: the FIRST
        // time the player places a given structure, which the v36 ever-built ledger already tracks.
        //
        // This is deliberately NOT the same idea as BuildModeController's note that "a freebie does
        // not make a build instant" (it keys the tier off CostFor, not EffectiveCostFor). That guard
        // is about DISCOUNTS not shortening timers, and it stands. This is about ONBOARDING PACE:
        // the first of anything is snappy, every one after it pays the real curve.
        [Header("First-build grace (onboarding pace)")]
        [Tooltip("Seconds for the FIRST build of a structure id the player has never built before. " +
                 "0 disables the grace (every build pays the normal tier curve).")]
        [Min(0f)] public float firstBuildSeconds = 5f;

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

        /// <summary>
        /// WO-855 sec.4 resource BASKET -- the one weighted scalar the work order defines for
        /// comparing costs across the four axes: <c>wood + 1.5*iron + 1.0*food + 2.0*crystals</c>.
        /// Pure; used as the structure's economic WEIGHT when deriving its build tier.
        /// </summary>
        public static float CostBasket(ResourceCost cost)
            => cost.wood + 1.5f * cost.iron + 1.0f * cost.food + 2.0f * cost.crystals;

        /// <summary>
        /// WO-855 Phase 4 -- the BUILD TIER for a structure of <paramref name="cost"/>.
        /// -----------------------------------------------------------------------------
        /// Before WO-855 the placement path passed a hard-coded literal 0 here, so every
        /// structure in the game built in exactly <see cref="baseBuildSeconds"/> and the whole
        /// <see cref="tierGrowth"/> curve was unreachable dead tuning. There is NO
        /// <c>repo.buildSeconds</c> / <c>repo.tier</c> field on RepoProps (checked at source --
        /// adding one is forbidden by WO-855's "check before adding fields"), so the tier is
        /// derived from the structure's own AUTHORED COST BASKET: the cheap founding shed is
        /// tier 0 (snappy), the expensive endgame building is a high tier (the hours-long drag),
        /// and the whole ladder re-tunes itself for free when the Phase 2/3 JSON cost pass lands.
        /// Returns 0..<c>tierCostThresholds.Length</c>; a null/empty threshold table degrades to
        /// tier 0 (the pre-WO-855 behaviour) rather than throwing.
        /// </summary>
        public int TierForCost(ResourceCost cost)
        {
            var bands = tierCostThresholds;
            if (bands == null || bands.Length == 0) return 0;
            float basket = CostBasket(cost);
            int tier = 0;
            for (int i = 0; i < bands.Length; i++)
                if (basket >= bands[i]) tier = i + 1;
                else break;                       // ascending table -- first miss ends the climb
            return tier;
        }

        /// <summary>
        /// The highest tier <see cref="TierForCost"/> can ever return (= the threshold count).
        /// The oracle asserts the <see cref="maxDurationSeconds"/> clamp holds HERE, at the top
        /// of the REACHABLE ladder, not at an arbitrary tier number.
        /// </summary>
        public int MaxReachableTier => tierCostThresholds != null ? tierCostThresholds.Length : 0;

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
