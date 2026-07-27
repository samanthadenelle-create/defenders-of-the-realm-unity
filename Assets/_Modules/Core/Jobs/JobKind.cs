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

    /// <summary>Static helpers mapping a <see cref="JobKind"/> to its default <see cref="ChannelId"/>.</summary>
    public static class JobChannels
    {
        /// <summary>
        /// The channel a job of <paramref name="kind"/> runs on by default:
        /// TrainTroop → Train; UnlockTier/LearnMagic → Research; everything else
        /// (Build/Repair/Upgrade/Tower*/Wall*) → Builder.
        /// </summary>
        public static ChannelId DefaultChannel(JobKind kind)
        {
            switch (kind)
            {
                case JobKind.TrainTroop:
                    return ChannelId.Train;
                case JobKind.UnlockTier:
                case JobKind.LearnMagic:
                    return ChannelId.Research;
                default:
                    return ChannelId.Builder;
            }
        }
    }
}
