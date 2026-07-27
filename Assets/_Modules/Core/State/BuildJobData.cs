// =============================================================================
// BuildJobData — one in-flight construction/upgrade timer (WO-172). Pure data.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// A CoC-style timed construction job: placing a building or buying an upgrade
// enqueues one of these, and the structure completes at StartMs + DurationMs.
// PERSISTED in GameState.BuildJobs so the timer counts down in REAL time across
// app close / offline — exactly like the WO-115 offline-harvest clock. The clock
// is unix-ms (TimeSource.NowUnixMs), the same unit as GameState.LastInboxSyncAt /
// LastHarvestClaimMs and PendingTowerBuild.FinishAt, so "remaining" is a plain
// subtraction with no per-frame state.
//
// DESIGN: keyed by StructureId (the player-placed structure / catalog instance the
// job belongs to). WO-108 (player build mode, NOT built yet) will mint a unique id
// per placed structure (PlacedStructureData.itemId + a cell key) and pass it here;
// WO-151 upgrades reuse the same id for the existing structure. The id is opaque to
// this layer — it only ticks the clock and reports done. See BuildTimerService for
// the start/remaining/skip API and the §"WO-108 integration point" note there.
//
// This struct mirrors PendingTowerBuild's shape + persistence convention (a small
// [Serializable] struct in a List<> that round-trips through SaveSchema). It is
// SEPARATE from PendingTowerBuild (an in-session pet-assisted tower build) and from
// TowerConstruction (a seconds-long in-session visual raise) — those are not
// persisted CoC timers; this is the durable, offline-counting one.
// =============================================================================

using System;
using Newtonsoft.Json;
using DeNelle.Core.Jobs;

namespace DeNelle.Core.State
{
    /// <summary>What kind of work a <see cref="BuildJobData"/> represents.</summary>
    public enum BuildJobType
    {
        /// <summary>A brand-new structure being constructed (WO-108 placement).</summary>
        Build = 0,
        /// <summary>An existing structure being upgraded to the next tier (WO-151).</summary>
        Upgrade = 1,
    }

    /// <summary>
    /// One persisted, real-time construction/upgrade timer. Completes at
    /// <see cref="FinishMs"/> = <see cref="StartMs"/> + <see cref="DurationMs"/>.
    /// Counts down offline because the clock is wall-clock unix-ms, not frame time.
    /// </summary>
    [Serializable]
    public struct BuildJobData
    {
        /// <summary>
        /// Opaque id of the structure this job builds/upgrades. WO-108 mints it per
        /// placed structure; WO-151 reuses the structure's id for its upgrade. One
        /// in-flight job per id (BuildTimerService enforces).
        /// </summary>
        [JsonProperty("structureId")] public string StructureId;

        /// <summary>Build vs upgrade — drives the duration curve + completion behaviour.</summary>
        [JsonProperty("jobType")] public int JobType;

        /// <summary>
        /// WO-773 — the richer job kind (Build/Upgrade/Repair/TrainTroop/UnlockTier/LearnMagic/
        /// Tower*/Wall*). This is the "ObsidianJob" kind axis: the completion router uses it to
        /// pick the IJobEffect handler. Additive default-on-read: absent on a pre-WO-773 (v34)
        /// save → 0 = Build; the v34→v35 migration backfills it from <see cref="JobType"/> (an
        /// Upgrade JobType becomes JobKind.Upgrade). See <see cref="JobKind"/>.
        /// </summary>
        [JsonProperty("kind")] public int Kind;

        /// <summary>
        /// WO-773 — the channel this job runs on (Builder/Train/Research). Channels never share
        /// slots, so a training job and a build job run in parallel. Additive default-on-read:
        /// absent → 0 = Builder (the legacy build/upgrade channel); the v34→v35 migration stamps
        /// folded legacy jobs onto the Builder channel. See <see cref="ChannelId"/>.
        /// </summary>
        [JsonProperty("channel")] public int Channel;

        /// <summary>Unix-ms the job started (TimeSource.NowUnixMs at enqueue).</summary>
        [JsonProperty("startMs")] public double StartMs;

        /// <summary>Total job duration in ms. May be reduced by ad-skips / instant-finish (StartMs is pulled back).</summary>
        [JsonProperty("durationMs")] public double DurationMs;

        /// <summary>
        /// F8-51: the LEVEL/TIER this job applies when it completes. For an Upgrade job this is
        /// the target level the structure/building reaches at completion (costs were charged at
        /// commit; the level applies at the completion seam — never before). 0 for Build jobs and
        /// for pre-F8-51 saves (JSON default), which the completion router treats as "nothing to
        /// apply" — additive, back-compatible.
        /// </summary>
        [JsonProperty("targetTier")] public int TargetTier;

        /// <summary>Unix-ms the job completes. Convenience = StartMs + DurationMs (not stored).</summary>
        [JsonIgnore] public double FinishMs => StartMs + DurationMs;

        /// <summary>Strongly-typed view of <see cref="JobType"/>.</summary>
        [JsonIgnore]
        public BuildJobType Type
        {
            get => (BuildJobType)JobType;
            set => JobType = (int)value;
        }

        /// <summary>Strongly-typed view of <see cref="Kind"/> (WO-773 — the ObsidianJob kind axis).</summary>
        [JsonIgnore]
        public JobKind JobKind
        {
            get => (JobKind)Kind;
            set => Kind = (int)value;
        }

        /// <summary>Strongly-typed view of <see cref="Channel"/> (WO-773 — the worker-pool channel).</summary>
        [JsonIgnore]
        public ChannelId ChannelId
        {
            get => (ChannelId)Channel;
            set => Channel = (int)value;
        }
    }
}
