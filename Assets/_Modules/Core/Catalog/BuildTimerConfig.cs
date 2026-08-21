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
//   base 45s | tierGrowth 3.2 | upgradeMultiplier 1.25 | freeBuildSlots 2
//   tier ladder: 45s | 2.4m | 7.7m | 24.6m | 1.3h | 4.2h
// First-time discoveries receive the separate 15s grace below. Repeat builds and
// advanced upgrades pay the real curve, preserving early momentum without letting
// the construction catalog evaporate in one sitting.
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
        [Min(0f)] public float baseBuildSeconds = 45f;

        [Tooltip("Per-tier multiplier — durations scale super-linearly: tierSeconds = base * pow(growth, tier). " +
                 ">1 makes high tiers the hours-long endgame drag that drives ad-watches/spend.")]
        [Min(1f)] public float tierGrowth = 3.2f;

        [Tooltip("Hard ceiling on any single job's duration (seconds). Default 24h — no multi-day lockouts.")]
        [Min(0f)] public float maxDurationSeconds = 24f * 3600f;

        [Tooltip("Upgrades multiply the same tier curve by this (upgrades a touch longer than a fresh build of the same tier).")]
        [Min(0f)] public float upgradeMultiplier = 1.25f;

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
        // point and it is easy to get backwards. The retention-first 2026-08-21 pass limits
        // this to three voluntary watches per four hours: enough to rescue a play session,
        // not enough to turn construction into an ad playlist or pressure a purchase.
        //
        // The numbers land on purpose: a 20-minute troop clears in 2 watches (feels free),
        // A ten-minute chunk clears a short wait and dents a long one. Late progression
        // remains paced by planning, offline time and optional crystal use.
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
        [Min(0)] public int adSkipsPerWindow = 3;

        [Tooltip("Length of the ROLLING window the skip allowance is counted over. Default 4 hours.")]
        [Min(0f)] public float adSkipWindowSeconds = 4f * 60f * 60f;

        [Header("Premium instant-finish (convenience IAP, not power)")]
        [Tooltip("Aether-crystal price to instantly finish a job, scaled by remaining minutes. " +
                 "0 disables the paid skip (ad-skip still works).")]
        [Min(0)] public int instantFinishCrystalsPerMinute = 1;

        [Tooltip("Minimum crystal price for any instant-finish (so near-done jobs still cost something).")]
        [Min(0)] public int instantFinishMinCrystals = 3;

        // Originally 5 seconds. The retention-first 2026-08-21 pass keeps the first-build
        // grace but raises it to 15 seconds so discovery stays quick without feeling disposable.
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
        [Min(0f)] public float firstBuildSeconds = 15f;

        [Header("Build slots (concurrency / scarcity)")]
        [Tooltip("How many jobs may run at once for free. CoC-style scarcity — extra slots are a future unlock/purchase.")]
        [Min(1)] public int freeBuildSlots = 2;

        // ─────────────────────────────────────────────────────────────────────
        //  WO-911 (M1) — QUEUE DEPTH. A DIFFERENT AXIS FROM freeBuildSlots.
        //  -------------------------------------------------------------------
        //  Owner: "max of five things in the queue, maybe upgradable later"
        //  (ruled Q4: 5 TOTAL PER LINE — per channel, NOT global).
        //
        //  ⚠ DEPTH is how many items may be LINED UP on one channel (active +
        //  pending). CONCURRENCY (freeBuildSlots, above) is how many run AT ONCE.
        //  DO NOT implement the cap of 5 by raising freeBuildSlots: that would
        //  hand the player five simultaneous builders, collapse the CoC scarcity
        //  the timer economy rests on, and delete the waiting pain the crystal
        //  Finish-Now sink exists to monetize. freeBuildSlots is also oracle-
        //  pinned at 2 (BuildEconomyRegression). See WO-911 section 2d.
        //
        //  Authored as DATA so "upgradable later" is a data change, not a code
        //  change. The per-player lever on top of this is the Echo-gated
        //  purchased slot (BuildTimerService.TryBuySlot, ruling Q6), which
        //  raises DEPTH and CONCURRENCY together via ChannelState.BoughtSlots.
        // ─────────────────────────────────────────────────────────────────────
        [Header("Queue depth (WO-911 — line length, NOT concurrency)")]
        [Tooltip("Max items lined up on ONE channel (active + pending). Owner ruling Q4: 5 per line. 0 = uncapped.")]
        [Min(0)] public int queueDepthPerLine = 5;

        [Header("Extra queue slot (Echo-gated crystal sink, WO-911 Q6/Q7)")]
        [Tooltip("Crystals for the FIRST purchased slot on a channel. Each further slot on that channel costs this x (1 + slots already bought).")]
        [Min(0)] public int extraSlotBaseCrystals = 250;

        [Tooltip("Echoes the player must own BEFORE the first extra slot may be bought. Ruling Q6: 'each Echo above 2'.")]
        [Min(0)] public int extraSlotEchoFloor = 2;

        // ─────────────────────────────────────────────────────────────────────
        //  BUILDING-PERK RESEARCH — the WC3 timed-research curve (owner ruling
        //  2026-08-07: "building perk research must be TIME-BASED, like WC3").
        //  -------------------------------------------------------------------
        //  WHAT THE DATA ACTUALLY CARRIES (checked at source before authoring a
        //  number, not assumed): BuildingPerkDef has id/name/effect/goldCost/
        //  iconId/isSignature/modifiers and NOTHING ELSE - there is no
        //  researchSeconds field in building-tiers.json, in either copy
        //  (Assets/Resources/... and Assets/StreamingAssets/...). BuildingTierDef
        //  carries no duration either. So the ONLY per-perk signal that already
        //  exists is goldCost, and the curve is derived from it here rather than
        //  hard-coded at the call site (WO-172 constraint: "all durations in a
        //  tunable SO/constants, never hard-coded").
        //
        //  Authoring a researchSeconds field per perk is the RIGHT long-term
        //  home and is deliberately NOT done here: it is a content pass across
        //  16 perks x 2 json copies plus a catalog-shape change, i.e. an owner
        //  decision, not a side effect of making research timed. When it lands,
        //  BuildingPerkService.ResearchSeconds prefers the authored value and
        //  this curve becomes the fallback - one call site to change.
        //
        //  THE BAND THIS PRODUCES against the LIVE catalog (goldCost 250..2000):
        //    250g -> 3m 30s | 300g -> 4m | 600g -> 7m | 800g -> 9m
        //    1200g -> 13m   | 1600g -> 17m | 2000g -> 21m
        //  Early perks are a coffee break; the tier-3 signature capstones are a
        //  real session-length wait, which is what makes the crystal Finish-Now
        //  sink (1 crystal/minute) and the 10-minute ad chunk meaningful here.
        //  Deliberately SHORTER than a building upgrade of comparable price:
        //  research is a parallel Research-channel line the player runs
        //  alongside their builders, not the main-line time sink.
        // ─────────────────────────────────────────────────────────────────────
        [Header("Building-perk research (WC3 timed research)")]
        [Tooltip("Flat floor added to every building-perk research, in seconds. Guarantees research is " +
                 "never felt as instant even for the cheapest perk. 0 = no floor.")]
        [Min(0f)] public float researchBaseSeconds = 60f;

        [Tooltip("Seconds of research time per 1 gold of the perk's authored goldCost. The whole per-perk " +
                 "curve: seconds = researchBaseSeconds + goldCost * this, clamped to maxDurationSeconds.")]
        [Min(0f)] public float researchSecondsPerGold = 0.6f;

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

        /// <summary>
        /// Wall-clock seconds one building-perk research takes, derived from the perk's authored
        /// <c>goldCost</c> (the only per-perk signal building-tiers.json carries — see the block
        /// comment above). <c>researchBaseSeconds + goldCost * researchSecondsPerGold</c>, clamped
        /// to <see cref="maxDurationSeconds"/>. A negative/zero gold cost still pays the base floor,
        /// so a free perk is a short wait rather than an instant grant.
        /// </summary>
        public float ResearchSecondsForGold(int goldCost)
        {
            float seconds = Mathf.Max(0f, researchBaseSeconds)
                          + Mathf.Max(0, goldCost) * Mathf.Max(0f, researchSecondsPerGold);
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
