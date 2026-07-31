// =============================================================================
// BuildTimerService — the common "Obsidian" multi-channel work queue (WO-172 +
// WO-773). CoC-style timed jobs + rewarded-ad/instant speedups, now GENERALIZED.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ONE shared service, MULTIPLE independent CHANNELS (Builder / Train / Research).
// Every timed action in the game flows through it — build, repair, upgrade,
// tier-unlock, learn-magic, troop-training, towers, walls — but a channel NEVER
// shares slots with another channel, so a troop training and a wall upgrade run in
// PARALLEL (CoC feel: builders and the barracks don't compete). Each channel has:
//   • N concurrent ACTIVE slots (BuildTimerConfig.freeBuildSlots + purchased slots)
//   • one FIFO PENDING queue — a job past the slot count QUEUES (it does NOT reject);
//     on completion the freed slot AUTO-PULLS the next pending job (cascades offline).
//
// The queue MATH is the pure DeNelle.Core.Jobs.ObsidianQueueEngine (headlessly
// testable); this MonoBehaviour is the thin wrapper that owns the GameState-backed
// channels (GameState.ObsidianQueue), feeds the engine TimeSource.NowUnixMs(), and
// dispatches the completion EFFECT (the existing Build/Upgrade seams + the IJobEffect
// registry for the extensible kinds).
//
// PERSISTENCE: every job lives in GameState.ObsidianQueue (per-channel active +
// pending lists of BuildJobData) and the clock is TimeSource.NowUnixMs() — wall-clock
// unix-ms. So timers keep counting while the app is closed: on load this service
// sweeps every channel, completes jobs whose FinishMs passed, and cascades pending
// pulls (offline-fair). Tick() only drives UI/visuals while open.
//
// BACK-COMPAT: the WO-172 legacy GameState.BuildJobs was folded into the Builder
// channel by the v34→v35 migration and is no longer read here. The public Builder-
// facing API (StartBuild/StartUpgrade/IsBuilding/RemainingSeconds/Progress/HasFreeSlot/
// ActiveJobs/skips/CompleteJob/CancelJob/RepointJob) is unchanged for its callers
// (BuildModeController / UnderConstructionVisual) — it now operates on the Builder
// channel and QUEUES instead of rejecting when full.
//
// AD SEAM / ASMDEF notes are unchanged from WO-172 (see history).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// The common Obsidian work queue (WO-773): N concurrent active slots + a FIFO
    /// pending queue PER CHANNEL (Builder/Train/Research), offline-fair via TimeSource,
    /// with rewarded-ad / premium speedups on the Builder channel. Persisted in
    /// GameState.ObsidianQueue. Self-bootstrapping singleton (mirrors OfflineHarvestService).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildTimerService : MonoBehaviour
    {
        public static BuildTimerService Instance { get; private set; }

        /// <summary>Raised when a job finishes (timer elapsed, ad/instant skip, or offline catch-up). Arg = the completed job.</summary>
        public event Action<BuildJobData> JobCompleted;

        /// <summary>Raised when a job starts running (immediate start OR auto-pulled from the queue). Arg = the started job.</summary>
        public event Action<BuildJobData> JobStarted;

        /// <summary>Raised when a job's remaining time changes via a skip (ad / instant). Arg = the updated job.</summary>
        public event Action<BuildJobData> JobSkipped;

        /// <summary>WO-773 — raised whenever ANY channel's active/pending set changes (for the queue HUD).</summary>
        public event Action QueueChanged;

        private BuildTimerConfig _config;

        /// <summary>The tunable curve/ad/slot knobs. Loaded from Resources, code-default if absent.</summary>
        public BuildTimerConfig Config => _config ??= LoadConfig();

        // ── Bootstrap / lifecycle ─────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("BuildTimerService");
            DontDestroyOnLoad(go);
            go.AddComponent<BuildTimerService>();
        }

        private void Awake()
        {
            // Destroy(this) not Destroy(gameObject) — may share a host (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            // Offline catch-up: any job that finished while the app was closed completes now
            // (one frame's slack so structures/registries have awoken).
            StartCoroutine(SweepNextFrame());
        }

        private System.Collections.IEnumerator SweepNextFrame()
        {
            yield return null;
            SweepAllChannels();
        }

        // While open, complete jobs the moment they expire so the UI/visual flips without
        // waiting for the next load. Cheap: a handful of jobs across channels, checked ~1/s.
        private float _nextTick;
        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            SweepAllChannels();
            PublishStatus();   // WO-778: 1s heartbeat keeps the HUD chip countdown live
        }

        // =====================================================================
        //  Slots / channels
        // =====================================================================

        /// <summary>The GameState-backed multi-channel queue (never null while a state exists).</summary>
        private static ObsidianQueueState Queue
        {
            get
            {
                var s = State;
                if (s == null) return null;
                return s.ObsidianQueue ??= ObsidianQueueState.Empty();
            }
        }

        /// <summary>The <see cref="ChannelState"/> for <paramref name="id"/> (null only when no state exists).</summary>
        private static ChannelState GetChannel(ChannelId id)
        {
            var q = Queue;
            return q?.Channel(id);
        }

        /// <summary>
        /// Derived slot count for <paramref name="id"/>: the config free slots + this channel's
        /// purchased slots. (Owner-design milestone unlocks — +1 slot at account L10/L20 — layer
        /// on top here as a future tuning dial; the queue mechanism already honours any count.)
        /// </summary>
        public int SlotCount(ChannelId id)
        {
            int free = Mathf.Max(1, Config.freeBuildSlots);
            var ch = GetChannel(id);
            int bought = ch != null ? Mathf.Max(0, ch.BoughtSlots) : 0;
            return free + bought;
        }

        /// <summary>Purchase an extra slot on <paramref name="id"/> (premium currency handled by caller). Persists.</summary>
        public void BuySlot(ChannelId id)
        {
            var ch = GetChannel(id);
            if (ch == null) return;
            ch.BoughtSlots = Mathf.Max(0, ch.BoughtSlots) + 1;
            Persist();
            // A newly-freed slot may immediately pull a pending job.
            ObsidianQueueEngine.PullIntoFreeSlots(ch, SlotCount(id), TimeSource.NowUnixMs());
            Persist();
            RaiseQueueChanged();
        }

        // ── Builder-channel convenience (the WO-172 callers) ──────────────────
        private static ChannelState Builder => GetChannel(ChannelId.Builder);
        private int BuilderSlots => SlotCount(ChannelId.Builder);

        /// <summary>A read-only snapshot of the Builder channel's running jobs (WO-172 API).</summary>
        public IReadOnlyList<BuildJobData> ActiveJobs => Builder != null ? Builder.ActiveJobs : System.Array.Empty<BuildJobData>();

        /// <summary>A read-only snapshot of a channel's running jobs (WO-773).</summary>
        public IReadOnlyList<BuildJobData> ActiveJobsOf(ChannelId id)
        {
            var ch = GetChannel(id);
            return ch != null ? ch.ActiveJobs : System.Array.Empty<BuildJobData>();
        }

        /// <summary>A read-only snapshot of a channel's FIFO pending queue (WO-773).</summary>
        public IReadOnlyList<BuildJobData> PendingJobsOf(ChannelId id)
        {
            var ch = GetChannel(id);
            return ch != null ? ch.PendingQueue : System.Array.Empty<BuildJobData>();
        }

        // =====================================================================
        //  Start a job — Builder channel (WO-108/WO-151 build + upgrade)
        // =====================================================================

        // ─────────────────────────────────────────────────────────────────────
        //  ★ WO-108 INTEGRATION POINT ★  (unchanged for callers)
        //  Placement commit calls StartBuild(structureId, tier); upgrades call
        //  StartUpgrade(structureId, targetTier). A full slot set now QUEUES the job
        //  (returns the pending job) instead of rejecting — the freed slot auto-pulls it.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Start (or QUEUE) a BUILD job for <paramref name="structureId"/> at
        /// <paramref name="tier"/> on the Builder channel. Returns the job (running or pending),
        /// or null if one already runs/queues for this id. Caller charges cost BEFORE calling.
        /// </summary>
        public BuildJobData? StartBuild(string structureId, int tier = 0)
            => StartBuilderJob(structureId, BuildJobType.Build, JobKind.Build, tier);

        /// <summary>
        /// Start (or QUEUE) an UPGRADE job for <paramref name="structureId"/> to
        /// <paramref name="targetLevel"/> on the Builder channel (F8-51 — level applies at
        /// completion via CompletedUpgradeApplier). Returns the job (running or pending), or
        /// null if one already runs/queues for this id.
        /// </summary>
        public BuildJobData? StartUpgrade(string structureId, int targetLevel)
            => StartBuilderJob(structureId, BuildJobType.Upgrade, JobKind.Upgrade,
                               Mathf.Max(0, targetLevel - 2), targetLevel);

        /// <summary>
        /// True when a NEW Builder job would start immediately (a free Builder slot exists).
        /// With the queue a full slot no longer rejects — it queues — so callers may skip this
        /// check; it remains for UI ("all builders busy → will queue").
        /// </summary>
        public bool HasFreeSlot
        {
            get
            {
                var ch = Builder;
                return ch != null && ch.ActiveJobs.Count < BuilderSlots;
            }
        }

        private BuildJobData? StartBuilderJob(string structureId, BuildJobType type, JobKind kind, int tier, int targetLevel = 0)
        {
            if (string.IsNullOrEmpty(structureId)) return null;
            var ch = Builder;
            if (ch == null) return null;

            // One job per structure id (across active AND pending).
            if (IndexInChannel(ch, structureId) >= 0) return null;

            var curveKind = type == BuildJobType.Upgrade ? BuildJobKind.Upgrade : BuildJobKind.Build;
            double durationMs = Config.DurationSecondsForTier(tier, curveKind) * 1000.0;

            // WO-676 STEWARD (Foreman's Pace): ONE HeroTalentModifiers read shortens every
            // build/upgrade timer at job start. StatSum is null-safe (0 with no service/tree)
            // and the sum is clamped so a mis-authored node can never make timers negative.
            float haste = Mathf.Clamp01(DeNelle.Village.Talents.HeroTalentModifiers.StatSum(
                HeroTalentClassReader.Slug(), "buildTime"));
            if (haste > 0f)
            {
                durationMs *= 1.0 - haste;
                DeNelle.Core.Diagnostics.FlowTrace.Once("Talent", "buildTime",
                    $"buildTime -{haste:P0} applied to build/upgrade timer duration (WO-676 Foreman's Pace).");
            }

            var job = new BuildJobData
            {
                StructureId = structureId,
                JobType = (int)type,
                Kind = (int)kind,
                Channel = (int)ChannelId.Builder,
                DurationMs = durationMs,
                TargetTier = targetLevel,
            };

            bool started = ObsidianQueueEngine.Enqueue(ch, BuilderSlots, job, TimeSource.NowUnixMs());
            Persist();

            if (type == BuildJobType.Upgrade)
                DeNelle.Core.Diagnostics.FlowTrace.Step("BuildTimer",
                    $"upgrade '{structureId}' {(started ? "started" : "QUEUED")} ({durationMs / 1000.0:0}s, " +
                    $"tier {Mathf.Max(1, targetLevel - 1)}->{targetLevel})");

            if (started) JobStarted?.Invoke(job);
            RaiseQueueChanged();

            // A zero-duration RUNNING job completes immediately (queued ones start later).
            if (started && durationMs <= 0) CompleteJob(structureId);
            return job;
        }

        // =====================================================================
        //  Generic enqueue — the "everything flows through it" seam (WO-773)
        // =====================================================================

        /// <summary>
        /// Enqueue a job of any <paramref name="kind"/> onto its default channel (Build/Repair/
        /// Upgrade/Tower*/Wall* → Builder; TrainTroop → Train; UnlockTier/LearnMagic → Research).
        /// Starts immediately if a slot is free on that channel, else queues. The effect on
        /// completion is the IJobEffect registered for the kind. Returns the job, or null if one
        /// already runs/queues for <paramref name="targetId"/> on that channel.
        /// </summary>
        public BuildJobData? Enqueue(JobKind kind, string targetId, double durationSeconds, int targetTier = 0)
            => Enqueue(kind, JobChannels.DefaultChannel(kind), targetId, durationSeconds, targetTier);

        /// <summary>Enqueue a job onto an explicit <paramref name="channel"/> (see the default-channel overload).</summary>
        public BuildJobData? Enqueue(JobKind kind, ChannelId channel, string targetId, double durationSeconds, int targetTier = 0)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            var ch = GetChannel(channel);
            if (ch == null) return null;
            if (IndexInChannel(ch, targetId) >= 0) return null;

            var job = new BuildJobData
            {
                StructureId = targetId,
                JobType = kind == JobKind.Upgrade || kind == JobKind.TowerUpgrade || kind == JobKind.WallUpgrade
                    ? (int)BuildJobType.Upgrade : (int)BuildJobType.Build,
                Kind = (int)kind,
                Channel = (int)channel,
                DurationMs = Math.Max(0.0, durationSeconds) * 1000.0,
                TargetTier = targetTier,
            };

            bool started = ObsidianQueueEngine.Enqueue(ch, SlotCount(channel), job, TimeSource.NowUnixMs());
            Persist();
            DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                $"job '{kind}' -> '{targetId}' {(started ? "started" : "QUEUED")} on {channel} ({durationSeconds:0}s).");
            if (started) JobStarted?.Invoke(job);
            RaiseQueueChanged();
            if (started && job.DurationMs <= 0) CompleteChannelJob(channel, targetId);
            return job;
        }

        // =====================================================================
        //  Query — Builder channel (WO-172 API, back-compat)
        // =====================================================================

        /// <summary>True if a job (running OR queued) is in flight for this structure id on the Builder channel.</summary>
        public bool IsBuilding(string structureId) => Builder != null && IndexInChannel(Builder, structureId) >= 0;

        /// <summary>Seconds remaining for the Builder job on <paramref name="structureId"/> (full duration while queued; 0 if none).</summary>
        public double RemainingSeconds(string structureId)
        {
            var job = FindInChannel(Builder, structureId, out bool _);
            if (!job.HasValue) return 0;
            var j = job.Value;
            if (j.StartMs <= 0) return j.DurationMs / 1000.0;   // queued — not started yet
            double remMs = j.FinishMs - TimeSource.NowUnixMs();
            return remMs > 0 ? remMs / 1000.0 : 0;
        }

        /// <summary>0..1 progress for the Builder job on <paramref name="structureId"/> (0 while queued; 1 if none/done).</summary>
        public float Progress(string structureId)
        {
            var job = FindInChannel(Builder, structureId, out bool _);
            if (!job.HasValue) return 1f;
            var j = job.Value;
            if (j.StartMs <= 0) return 0f;      // queued — not started yet
            if (j.DurationMs <= 0) return 1f;
            double elapsed = TimeSource.NowUnixMs() - j.StartMs;
            return Mathf.Clamp01((float)(elapsed / j.DurationMs));
        }

        /// <summary>Crystal price to instant-finish this Builder job right now (0 = unavailable / queued / paid skip disabled).</summary>
        public int InstantFinishPrice(string structureId)
        {
            var job = FindInChannel(Builder, structureId, out bool isActive);
            if (!job.HasValue || !isActive || job.Value.StartMs <= 0) return 0;
            return Config.InstantFinishPrice(RemainingSeconds(structureId));
        }

        // =====================================================================
        //  Speedups — rewarded ad (opt-in, capped) + premium instant-finish (Builder)
        // =====================================================================

        /// <summary>True when a rewarded-ad skip is allowed right now (cooldown clear AND daily cap not hit).</summary>
        public bool CanWatchAdToSkip(string structureId)
        {
            var job = FindInChannel(Builder, structureId, out bool isActive);
            if (!job.HasValue || !isActive || job.Value.StartMs <= 0) return false;
            if (UnderDailyAdCap() == false) return false;
            var mgr = RewardedAdManager.Instance;
            return mgr != null && mgr.IsAdReady;
        }

        /// <summary>
        /// Watch a rewarded ad to knock a fixed chunk (Config.adSkipSeconds) off the remaining
        /// timer. Opt-in, store-build only, capped per day. The timer always finishes on its own.
        /// </summary>
        public bool WatchAdToSkip(string structureId)
        {
            var job = FindInChannel(Builder, structureId, out bool isActive);
            if (!job.HasValue || !isActive || job.Value.StartMs <= 0) return false;
            if (!UnderDailyAdCap()) return false;

            var mgr = RewardedAdManager.Instance;
            if (mgr == null) return false;

            return mgr.TryShowAd(() =>
            {
                RecordAdSkipUsed();
                ApplySkipSeconds(structureId, Config.adSkipSeconds);
            });
        }

        /// <summary>
        /// Premium instant-finish: spend crystals (single GameState wallet) to complete the
        /// Builder job now. No-op if the price is 0 (disabled/queued) or unaffordable.
        /// </summary>
        public bool TryInstantFinish(string structureId)
        {
            int price = InstantFinishPrice(structureId);
            if (price <= 0) return false;

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || state.Resources.Crystals < price) return false;

            svc.AddCrystals(-price);
            CompleteJob(structureId);
            return true;
        }

        // Apply a time skip by pulling StartMs back by `seconds`; if that finishes it, complete it.
        private void ApplySkipSeconds(string structureId, float seconds)
        {
            var ch = Builder;
            if (ch == null || seconds <= 0f) return;
            int i = ActiveIndexInChannel(ch, structureId);
            if (i < 0) return;

            var j = ch.ActiveJobs[i];
            if (j.StartMs <= 0) return;             // queued — nothing to skip yet
            j.StartMs -= seconds * 1000.0;          // earlier start → earlier finish
            ch.ActiveJobs[i] = j;

            if (j.FinishMs <= TimeSource.NowUnixMs())
            {
                CompleteJob(structureId);
            }
            else
            {
                Persist();
                JobSkipped?.Invoke(j);
                RaiseQueueChanged();
            }
        }

        // =====================================================================
        //  Completion / cancel
        // =====================================================================

        /// <summary>Force-complete a Builder job now (skips/instant/offline). Removes + applies effect + auto-pulls the next queued job.</summary>
        public void CompleteJob(string structureId) => CompleteChannelJob(ChannelId.Builder, structureId);

        /// <summary>Force-complete a specific job on <paramref name="channel"/> now, then cascade the channel's queue.</summary>
        public void CompleteChannelJob(ChannelId channel, string structureId)
        {
            var ch = GetChannel(channel);
            if (ch == null) return;
            int i = ActiveIndexInChannel(ch, structureId);
            if (i < 0) return;

            var job = ch.ActiveJobs[i];
            ch.ActiveJobs.RemoveAt(i);
            OnJobCompleted(job);

            // The freed slot pulls the next queued job; then resolve any newly-due (cascade).
            ObsidianQueueEngine.PullIntoFreeSlots(ch, SlotCount(channel), TimeSource.NowUnixMs());
            ObsidianQueueEngine.Resolve(ch, SlotCount(channel), TimeSource.NowUnixMs(), OnJobCompleted);
            Persist();
            RaiseQueueChanged();
        }

        // The ONE completion seam — live expiry, ad/instant skip AND the offline-fair sweep all
        // route here so the effect lands identically. Order: (1) the F8-51 Upgrade level apply
        // (proven CompletedUpgradeApplier seam) for Upgrade jobs; (2) the IJobEffect registry for
        // the extensible kinds (Repair/TrainTroop/UnlockTier/LearnMagic/…) — a no-op for Build/
        // Upgrade so they never double-apply; (3) the JobCompleted event (UnderConstructionVisual
        // reveal etc.). Guarded so one bad apply logs + never blocks the cascade.
        private void OnJobCompleted(BuildJobData job)
        {
            if (job.Type == BuildJobType.Upgrade && job.TargetTier > 0)
                DeNelle.Core.Diagnostics.Guard.Try("BuildTimer", "apply completed upgrade",
                    () => Buildings.Progression.CompletedUpgradeApplier.Apply(job));

            JobEffectRegistry.Apply(job);
            JobCompleted?.Invoke(job);
        }

        /// <summary>
        /// Re-key an in-flight Builder job (F8-51): a placed structure MOVED mid-timer changes its
        /// cell-derived key. Timer progress is untouched. Handles both active + queued jobs.
        /// </summary>
        public void RepointJob(string oldStructureId, string newStructureId)
        {
            if (string.IsNullOrEmpty(newStructureId) || oldStructureId == newStructureId) return;
            var ch = Builder;
            if (ch == null) return;

            int a = ActiveIndexInChannel(ch, oldStructureId);
            if (a >= 0)
            {
                var j = ch.ActiveJobs[a];
                j.StructureId = newStructureId;
                ch.ActiveJobs[a] = j;
            }
            else
            {
                int p = PendingIndexInChannel(ch, oldStructureId);
                if (p < 0) return;
                var j = ch.PendingQueue[p];
                j.StructureId = newStructureId;
                ch.PendingQueue[p] = j;
            }
            Persist();
            RaiseQueueChanged();
            DeNelle.Core.Diagnostics.FlowTrace.Step("BuildTimer",
                $"job re-keyed '{oldStructureId}' -> '{newStructureId}' (structure moved mid-timer)");
        }

        /// <summary>
        /// Cancel a Builder job WITHOUT completing it (e.g. the player sells the structure
        /// mid-build). Removes it from active OR the pending queue; caller owns any refund.
        /// A cancelled active slot auto-pulls the next queued job.
        /// </summary>
        public bool CancelJob(string structureId) => CancelChannelJob(ChannelId.Builder, structureId);

        /// <summary>Cancel a job on <paramref name="channel"/> without completing it. Auto-pulls the queue on success.</summary>
        public bool CancelChannelJob(ChannelId channel, string structureId)
        {
            var ch = GetChannel(channel);
            if (ch == null) return false;

            int a = ActiveIndexInChannel(ch, structureId);
            if (a >= 0)
            {
                ch.ActiveJobs.RemoveAt(a);
                ObsidianQueueEngine.PullIntoFreeSlots(ch, SlotCount(channel), TimeSource.NowUnixMs());
                Persist();
                RaiseQueueChanged();
                return true;
            }
            int p = PendingIndexInChannel(ch, structureId);
            if (p >= 0)
            {
                ch.PendingQueue.RemoveAt(p);
                Persist();
                RaiseQueueChanged();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reorder a PENDING job to <paramref name="index"/> within its channel's FIFO (drag-reorder).
        /// No-op for a running job or an out-of-range move. Persists + raises QueueChanged.
        /// </summary>
        public bool ReorderPending(ChannelId channel, string targetId, int index)
        {
            var ch = GetChannel(channel);
            if (ch == null) return false;
            int p = PendingIndexInChannel(ch, targetId);
            if (p < 0) return false;
            index = Mathf.Clamp(index, 0, ch.PendingQueue.Count - 1);
            if (index == p) return true;
            var job = ch.PendingQueue[p];
            ch.PendingQueue.RemoveAt(p);
            ch.PendingQueue.Insert(index, job);
            Persist();
            RaiseQueueChanged();
            return true;
        }

        // Sweep EVERY channel: complete due jobs + cascade pending pulls (open OR offline).
        private void SweepAllChannels()
        {
            var q = Queue;
            if (q == null || q.Channels == null) return;
            double now = TimeSource.NowUnixMs();
            int totalCompleted = 0;
            // Copy the keys — Resolve mutates the channel lists + fires listeners that may re-enter.
            var ids = new List<ChannelId>(q.Channels.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                var ch = q.Channel(ids[i]);
                totalCompleted += ObsidianQueueEngine.Resolve(ch, SlotCount(ids[i]), now, OnJobCompleted);
            }
            if (totalCompleted > 0)
            {
                Persist();
                RaiseQueueChanged();
            }
        }

        // =====================================================================
        //  Daily ad-skip cap (unchanged)
        // =====================================================================

        private bool UnderDailyAdCap()
        {
            int cap = Config.adSkipsPerDay;
            if (cap <= 0) return true;
            RollDayIfNeeded();
            var state = State;
            return state != null && state.AdSkipsUsedToday < cap;
        }

        private void RecordAdSkipUsed()
        {
            RollDayIfNeeded();
            var state = State;
            if (state == null) return;
            state.AdSkipsUsedToday++;
            Persist();
        }

        private void RollDayIfNeeded()
        {
            var state = State;
            if (state == null) return;
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (state.AdSkipDayKey != today)
            {
                state.AdSkipDayKey = today;
                state.AdSkipsUsedToday = 0;
            }
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        private static GameState State => GameStateService.Instance != null ? GameStateService.Instance.State : null;

        private void RaiseQueueChanged()
        {
            DeNelle.Core.Diagnostics.Guard.Try("Obsidian", "raise QueueChanged", () => QueueChanged?.Invoke());
            PublishStatus();   // WO-778: chip snapshot tracks every queue mutation
        }

        // WO-778: presentation-ready summary for the persistent HUD chip (Core seam).
        // Remaining time is computed HERE (TimeSource is Village-side) so the HUD never
        // clocks; the HUD polls DeNelle.Core.UI.ObsidianQueueGate.Status only.
        private void PublishStatus()
        {
            var s = new DeNelle.Core.UI.ObsidianQueueGate.WorkQueueStatus();
            if (State != null)
            {
                s.Available = true;
                double now = TimeSource.NowUnixMs();
                double soonest = double.MaxValue;

                void Fill(ChannelId id, out int busy, out int slots, out int queued)
                {
                    var act = ActiveJobsOf(id);
                    busy = act.Count;
                    slots = SlotCount(id);
                    queued = PendingJobsOf(id).Count;
                    for (int i = 0; i < act.Count; i++)
                        if (act[i].StartMs > 0 && act[i].FinishMs < soonest)
                            soonest = act[i].FinishMs;
                }

                Fill(ChannelId.Builder,  out s.BuilderBusy,  out s.BuilderSlots,  out s.BuilderQueued);
                Fill(ChannelId.Train,    out s.TrainBusy,    out s.TrainSlots,    out s.TrainQueued);
                Fill(ChannelId.Research, out s.ResearchBusy, out s.ResearchSlots, out s.ResearchQueued);

                s.SoonestRemainingSec = soonest == double.MaxValue
                    ? -1
                    : (int)System.Math.Max(0.0, (soonest - now) / 1000.0);

                // WC3 QUEUE VIEW (owner 2026-07-30 "show like 5 deep Queued"): the Builder
                // channel's jobs by name — active first (with countdown), then the waiting
                // line in order. Capped at 7 (2 crews + 5 visible queued); the chip renders 5.
                var bAct = ActiveJobsOf(ChannelId.Builder);
                var bPend = PendingJobsOf(ChannelId.Builder);
                int n = System.Math.Min(7, bAct.Count + bPend.Count);
                if (n > 0)
                {
                    s.Entries = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry[n];
                    int w = 0;
                    for (int i = 0; i < bAct.Count && w < n; i++, w++)
                        s.Entries[w] = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
                        {
                            Label = PrettyJobLabel(bAct[i].StructureId),
                            RemainingSec = (int)System.Math.Max(0.0, (bAct[i].FinishMs - now) / 1000.0),
                            Queued = false
                        };
                    for (int i = 0; i < bPend.Count && w < n; i++, w++)
                        s.Entries[w] = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
                        {
                            Label = PrettyJobLabel(bPend[i].StructureId),
                            RemainingSec = -1,
                            Queued = true
                        };
                }
            }
            DeNelle.Core.UI.ObsidianQueueGate.PublishStatus(s);
        }

        // Player-facing label for a queue row: strip the placement suffix ("@15_7"), then
        // title-case the id's tokens ("tower_arcane_spire" -> "Tower Arcane Spire",
        // "barracks-upgrade" -> "Barracks Upgrade"). Pure string work — no catalog lookup,
        // so the chip stays Core-safe and never blocks on data readiness.
        private static string PrettyJobLabel(string structureId)
        {
            if (string.IsNullOrEmpty(structureId)) return "Job";
            string id = structureId;
            int at = id.IndexOf('@');
            if (at > 0) id = id.Substring(0, at);
            var parts = id.Split('-', '_', ':');
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.Length > 0 ? sb.ToString() : "Job";
        }

        // Index of a job (active OR pending) in a channel by structure id; -1 if none.
        private static int IndexInChannel(ChannelState ch, string id)
        {
            if (ActiveIndexInChannel(ch, id) >= 0) return 0;
            if (PendingIndexInChannel(ch, id) >= 0) return 0;
            return -1;
        }

        private static int ActiveIndexInChannel(ChannelState ch, string id)
        {
            if (ch == null || ch.ActiveJobs == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < ch.ActiveJobs.Count; i++)
                if (ch.ActiveJobs[i].StructureId == id) return i;
            return -1;
        }

        private static int PendingIndexInChannel(ChannelState ch, string id)
        {
            if (ch == null || ch.PendingQueue == null || string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < ch.PendingQueue.Count; i++)
                if (ch.PendingQueue[i].StructureId == id) return i;
            return -1;
        }

        // Find a job (active first, then pending) in a channel; isActive reports which list.
        private static BuildJobData? FindInChannel(ChannelState ch, string id, out bool isActive)
        {
            isActive = false;
            int a = ActiveIndexInChannel(ch, id);
            if (a >= 0) { isActive = true; return ch.ActiveJobs[a]; }
            int p = PendingIndexInChannel(ch, id);
            if (p >= 0) return ch.PendingQueue[p];
            return null;
        }

        private static void Persist() => GameStateService.Instance?.Save();

        private static BuildTimerConfig LoadConfig()
        {
            var cfg = Resources.Load<BuildTimerConfig>(BuildTimerConfig.ResourcesPath);
            return cfg != null ? cfg : BuildTimerConfig.CreateDefault();
        }
    }
}
