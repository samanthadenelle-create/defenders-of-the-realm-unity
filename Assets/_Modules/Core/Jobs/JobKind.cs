// =============================================================================
// JobKind / ChannelId — the vocabulary of the common "Obsidian" work queue
// (WO-773, multi-channel per owner review 2026-07-26).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Jobs
//
// The Obsidian queue is ONE shared service with MULTIPLE independent CHANNELS —
// a builder queue must NOT compete for slots with a troop-training queue (CoC
// feel: builders and the barracks run in parallel). Each channel has its own
// concurrent ACTIVE slots + its own FIFO PENDING queue and NEVER shares slots
// with another channel.
//
//   JobKind  = WHAT the job does (drives the completion effect + duration curve).
//   ChannelId = WHICH parallel worker pool runs it (Builder / Train / Research).
//
// A JobKind maps to a DEFAULT channel (DefaultChannel below); a consumer may
// still target a channel explicitly. Adding a new job type = a new JobKind +
// (optionally) a channel mapping + an IJobEffect handler — the queue engine,
// slots, persistence and offline-fairness are untouched.
//
// PLAYER-FACING NAMING: never surface "Obsidian" in player copy — the player sees
// "Builders" / "Training queue". "Obsidian" is the internal code name only.
// =============================================================================

namespace DeNelle.Core.Jobs
{
    /// <summary>
    /// What a queued job does. Drives the completion effect (IJobEffect / the
    /// existing build-upgrade seams) and, for Builder jobs, the duration curve.
    /// Extensible: add a member + a handler; the queue never changes.
    /// </summary>
    public enum JobKind
    {
        /// <summary>Build a brand-new structure (Builder channel).</summary>
        Build = 0,
        /// <summary>Upgrade an existing structure/building to a target tier (Builder channel).</summary>
        Upgrade = 1,
        /// <summary>Repair a damaged/burning structure (Builder channel).</summary>
        Repair = 2,
        /// <summary>Unlock a building/tech tier (Research channel).</summary>
        UnlockTier = 3,
        /// <summary>Learn a new spell (Research channel).</summary>
        LearnMagic = 4,
        /// <summary>Train a troop (Train channel).</summary>
        TrainTroop = 5,
        /// <summary>Build a tower (Builder channel).</summary>
        TowerBuild = 6,
        /// <summary>Upgrade a tower (Builder channel).</summary>
        TowerUpgrade = 7,
        /// <summary>Upgrade a wall (Builder channel).</summary>
        WallUpgrade = 8,
        /// <summary>Upgrade the Barracks BUILDING to the next level — unlocks new troops (Builder channel, WO-771.9).</summary>
        BarracksUpgrade = 9,
        /// <summary>Upgrade a single TROOP's progression track — reach/strength/ability (Research channel, WO-771.9).</summary>
        TroopUpgrade = 10,
        /// <summary>
        /// Research ONE building perk from building-tiers.json (Research channel) — the WC3
        /// "research at the Blacksmith" pillar, now TIME-BASED (owner ruling 2026-08-07).
        /// Job id: <c>BuildingPerkService.ResearchJobPrefix + buildingId + ":" + perkId</c>.
        /// <para>
        /// ⚠ WHY THIS IS A NEW VALUE AND NOT A REUSE OF <see cref="UnlockTier"/> (=3, which has
        /// zero producers and would have been "free"): <c>UnlockTier</c> means "unlock a
        /// building/tech TIER" — the tier ladder, which is already served by <see cref="Upgrade"/>
        /// / <see cref="BarracksUpgrade"/> and is a DIFFERENT thing from a per-perk purchase off a
        /// tier's shelf. Squatting on it would (a) hand a future tier-unlock WO a JobKind whose
        /// registered IJobEffect silently applies a PERK, and (b) force both features to share one
        /// job-id namespace. <see cref="BuildJobData.Kind"/> persists as an int and this is an
        /// APPEND (11), so no existing saved job is renumbered and no migration is needed.
        /// </para>
        /// </summary>
        BuildingResearch = 11,

        /// <summary>
        /// Polish ONE rough stone recovered from a dungeon into a refined gem (Research channel,
        /// WO-1042). Job id: <c>JewelPolishService.PolishJobPrefix + &lt;stoneInstanceId&gt;</c>.
        /// <para>
        /// ⚠ THE ONLY <b>RANDOM-OUTCOME</b> JOB KIND. The refined gem's tier is rolled when the job
        /// LANDS (odds shaped by the WO-1040 run grade), not chosen at enqueue. That makes it the one
        /// kind <see cref="JobRushPolicy"/> excludes from the paid instant-finish verb: paying crystals
        /// to resolve an unknown outcome is mechanically a loot box (owner ruling 2026-08-16). Every
        /// other kind above is deterministic and keeps its rush untouched.
        /// </para>
        /// <para>
        /// APPEND-ONLY (12), like <see cref="BuildingResearch"/>: <see cref="BuildJobData.Kind"/>
        /// persists as an int, so no saved job is renumbered and no migration is needed.
        /// </para>
        /// </summary>
        JewelPolish = 12,
    }

    /// <summary>
    /// A parallel worker pool. Channels run independently — each fills its own
    /// slots from its own FIFO queue and never competes with another channel.
    /// Extensible (append a member; add its default-channel mapping).
    /// </summary>
    public enum ChannelId
    {
        /// <summary>Builders — build / repair / upgrade / tower / wall work.</summary>
        Builder = 0,
        /// <summary>The training queue — troop training.</summary>
        Train = 1,
        /// <summary>The research/lab queue — tier unlocks, learning magic.</summary>
        Research = 2,
    }

    /// <summary>
    /// ECON-SWEEP 2026-08-16 (defect 3) — which job kinds are paid for in a currency the WO-911
    /// refundable basket cannot carry.
    /// <para>
    /// <see cref="JobCost"/> has lanes for wood/food/iron/crystals/magic and NO COINS LANE, and
    /// <c>BuildTimerService.ToJobCost(Village.ResourceCost)</c> warns as much. Research is the only
    /// gold-priced job in the game, so cancelling one records an ALL-ZERO basket and the gold is
    /// gone. That is the current refund POLICY and this helper does not change it -- it exists so the
    /// player-facing cancel message can NAME the money instead of reporting "Nothing to refund.",
    /// which is a claim that no currency was taken and is simply false. Adding a coins lane
    /// (JobCost.Coins + BuildJobData.paidCoins, save schema v38) is the policy fix and is the
    /// owner's call; see the note at the top of BuildingPerkService.
    /// </para>
    /// </summary>
    public static class JobCurrency
    {
        /// <summary>
        /// True when a job of <paramref name="kind"/> was charged in COINS (gold), which the paid
        /// basket cannot record and a cancel therefore cannot return.
        /// </summary>
        public static bool SpendsUnrefundableCoins(JobKind kind) => kind == JobKind.BuildingResearch;

        /// <summary>ASCII, player-readable name of that currency, for the cancel notice.</summary>
        public static string UnrefundableCurrencyLabel(JobKind kind)
            => SpendsUnrefundableCoins(kind) ? "gold" : "";
    }

    /// <summary>Static helpers mapping a <see cref="JobKind"/> to its default <see cref="ChannelId"/>.</summary>
    public static class JobChannels
    {
        /// <summary>
        /// The channel a job of <paramref name="kind"/> runs on by default:
        /// TrainTroop → Train; UnlockTier/LearnMagic/TroopUpgrade/BuildingResearch → Research;
        /// everything else (Build/Repair/Upgrade/Tower*/Wall*) → Builder.
        /// </summary>
        public static ChannelId DefaultChannel(JobKind kind)
        {
            switch (kind)
            {
                case JobKind.TrainTroop:
                    return ChannelId.Train;
                case JobKind.UnlockTier:
                case JobKind.LearnMagic:
                case JobKind.TroopUpgrade:      // WO-771.9 — per-troop upgrade track runs on the research/lab queue
                case JobKind.BuildingResearch:  // 2026-08-07 — WC3 timed building-perk research, same lab queue
                case JobKind.JewelPolish:       // WO-1042 — the Jeweler's bench polishes a rough stone on the lab queue
                    return ChannelId.Research;
                default:                         // Build/Repair/Upgrade/Tower*/Wall*/BarracksUpgrade → Builder
                    return ChannelId.Builder;
            }
        }
    }
}
