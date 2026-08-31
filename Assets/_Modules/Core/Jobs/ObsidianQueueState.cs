// =============================================================================
// ObsidianQueueState / ChannelState — the persisted multi-channel Obsidian queue
// (WO-773, multi-channel per owner review 2026-07-26).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Jobs
//
// The single home for ALL timed work, split into independent CHANNELS. Lands in
// GameState.ObsidianQueue and round-trips through SaveSchema (v35). The v34→v35
// migration folds the legacy GameState.BuildJobs / PendingBuilds / BuildingCooldowns
// into the BUILDER channel (see SaveMigrator.MigrateToV35) — no in-flight work lost.
//
// THE JOB RECORD is <see cref="DeNelle.Core.State.BuildJobData"/> (the WO-172
// persisted, offline-fair timer record, now carrying a Kind + Channel). It IS the
// "ObsidianJob" the design refers to — generalized in place, not reinvented.
//
// SlotCount is DERIVED at runtime (BuildTimerConfig.freeBuildSlots + BoughtSlots),
// not persisted — only the purchase count (BoughtSlots) is durable. Channels never
// share slots: each ChannelState fills its own ActiveJobs (len ≤ SlotCount) from
// its own FIFO PendingQueue.
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json;
using DeNelle.Core.State;

namespace DeNelle.Core.Jobs
{
    /// <summary>
    /// One channel's worker pool: purchased extra slots + the ACTIVE jobs (running,
    /// length ≤ derived SlotCount) + the FIFO PENDING queue (waiting for a free slot).
    /// </summary>
    [System.Serializable]
    public sealed class ChannelState
    {
        /// <summary>Purchased extra slots on top of the config free slots (IAP/premium). Clamped ≥0.</summary>
        [JsonProperty("boughtSlots")] public int BoughtSlots;

        /// <summary>
        /// Wall-clock end of the one temporary worker taste, in Unix milliseconds. Zero means none.
        /// This is deliberately separate from <see cref="BoughtSlots"/>: expiry must never turn a
        /// temporary worker into permanent progression or affect permanent-slot gates.
        /// </summary>
        [JsonProperty("temporarySlotEndsAtUnixMs")] public double TemporarySlotEndsAtUnixMs;

        /// <summary>Durable one-time guard. This taste cannot be reclaimed after it expires.</summary>
        [JsonProperty("temporarySlotClaimed")] public bool TemporarySlotClaimed;

        /// <summary>Running jobs — one per occupied slot. Length ≤ SlotCount. Each has StartMs &gt; 0.</summary>
        [JsonProperty("active")] public List<BuildJobData> ActiveJobs = new List<BuildJobData>();

        /// <summary>Waiting jobs in FIFO order (head pulled first). Pending jobs carry StartMs = 0.</summary>
        [JsonProperty("pending")] public List<BuildJobData> PendingQueue = new List<BuildJobData>();

        /// <summary>Total jobs (active + pending) — for HUD summary chips.</summary>
        [JsonIgnore] public int Count =>
            (ActiveJobs != null ? ActiveJobs.Count : 0) + (PendingQueue != null ? PendingQueue.Count : 0);

        /// <summary>Null-guards the two lists (a JsonUtility/partial-save load can leave them null).</summary>
        public void EnsureLists()
        {
            if (ActiveJobs == null) ActiveJobs = new List<BuildJobData>();
            if (PendingQueue == null) PendingQueue = new List<BuildJobData>();
        }
    }

    /// <summary>
    /// The whole multi-channel queue: one <see cref="ChannelState"/> per
    /// <see cref="ChannelId"/>. Channels are created on demand via <see cref="Channel"/>.
    /// </summary>
    [System.Serializable]
    public sealed class ObsidianQueueState
    {
        /// <summary>Per-channel worker pools, keyed by <see cref="ChannelId"/> (serialized as the enum NAME).</summary>
        [JsonProperty("channels")] public Dictionary<ChannelId, ChannelState> Channels
            = new Dictionary<ChannelId, ChannelState>();

        /// <summary>Get (creating if absent) the <see cref="ChannelState"/> for <paramref name="id"/>. Never null.</summary>
        public ChannelState Channel(ChannelId id)
        {
            if (Channels == null) Channels = new Dictionary<ChannelId, ChannelState>();
            if (!Channels.TryGetValue(id, out var ch) || ch == null)
            {
                ch = new ChannelState();
                Channels[id] = ch;
            }
            ch.EnsureLists();
            return ch;
        }

        /// <summary>A fresh empty queue with all three canonical channels present.</summary>
        public static ObsidianQueueState Empty()
        {
            var q = new ObsidianQueueState();
            q.Channel(ChannelId.Builder);
            q.Channel(ChannelId.Train);
            q.Channel(ChannelId.Research);
            return q;
        }
    }
}
