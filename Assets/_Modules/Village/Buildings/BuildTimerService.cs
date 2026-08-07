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
using System.Globalization;   // ad-skip window stamp: culture-invariant round-trip parse
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;

// WO-911 refund plumbing refers to the resource ledger as `Ledger.*` (ResourceCost,
// HarvestResource, ResourceLedger). There is no namespace by that name - those types
// live in DeNelle.Village.Buildings.Progression. One alias resolves every reference
// (:632, :642-645, :1070+) rather than rewriting each call site.
using Ledger = DeNelle.Village.Buildings.Progression;

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
        private bool _statusSeeded;   // first-frame flash fix (review 2026-08-01), see below
        private void Update()
        {
            // FIRST-FRAME FLASH FIX (review 2026-08-01): RaidEntryGate.ArmyStatus defaults
            // READY pre-publish, so the HUD Raids button could flash bright for up to 1s on
            // hub load (the 1 Hz tick below may land BEFORE GameState exists, publishing the
            // null-state READY snapshot, then wait a full second). Seed ONE publish through
            // the same path the moment GameState is first available, off the tick cadence.
            if (!_statusSeeded && State != null)
            {
                _statusSeeded = true;
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    "army status seed publish (first Update with GameState, pre-heartbeat).");
                PublishStatus();
            }

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

        /// <summary>WO-911 — extra slots PURCHASED on <paramref name="id"/> (0 for an untouched channel).</summary>
        public int BoughtSlotsOf(ChannelId id)
        {
            var ch = GetChannel(id);
            return ch != null ? Mathf.Max(0, ch.BoughtSlots) : 0;
        }

        /// <summary>
        /// Grant an extra slot on <paramref name="id"/> WITHOUT charging or gating. Persists.
        /// </summary>
        /// <remarks>
        /// ⚠ WO-911 — this is the raw GRANT. It was the whole of B3 (unlimited free parallel
        /// workers, which also erodes the waiting pain the crystal sink monetizes). Player-facing
        /// callers MUST use <see cref="TryBuySlot"/>, which applies the owner's Echo gate and the
        /// crystal charge. This entry point survives for grants that are legitimately free
        /// (milestone/dev/test) and is now explicitly named as such.
        /// </remarks>
        public void GrantSlot(ChannelId id)
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

        /// <summary>Back-compat alias for <see cref="GrantSlot"/> — see its remarks before calling.</summary>
        [Obsolete("WO-911: use TryBuySlot for any player-facing purchase (it applies the Echo gate + crystal charge). GrantSlot is the free grant.")]
        public void BuySlot(ChannelId id) => GrantSlot(id);

        // =====================================================================
        //  WO-911 — THE EXTRA-SLOT SINK: ECHO-GATED, CRYSTAL-PRICED (Q6 / M11)
        //  -------------------------------------------------------------------
        //  Owner ruling Q6: "each Echo above 2 unlocks the OPTION to purchase one
        //  extra queue slot with crystals." A TWO-STEP gate — the Echo count
        //  unlocks the RIGHT to buy; crystals complete it. NEITHER STEP ALONE
        //  GRANTS THE SLOT. Not the account-level milestone dial, not a building
        //  level. Convenience only: a slot buys parallelism, never combat power.
        //
        //  Q7 permits three sinks (a pack, a direct crystal buy, and a TEMPORARY
        //  unlock after watching X ads). Only the DIRECT CRYSTAL BUY is built
        //  here. The temporary one is deliberately NOT built: an EXPIRING slot
        //  needs a persisted per-channel expiry timestamp, ChannelState.BoughtSlots
        //  is a permanent int with no expiry concept, and that lands in the same
        //  SaveSchema territory as WO-912's rolling-window ad state — which the WO
        //  requires be designed JOINTLY, in ONE coordinated bump, rather than
        //  producing two competing rollover mechanisms in one file.
        // =====================================================================

        /// <summary>
        /// WO-911 (Q6) — how many extra slots the player's Echo count entitles them to BUY on a
        /// channel. "Each Echo above 2": <c>EchoCount - extraSlotEchoFloor</c>, floored at 0.
        /// </summary>
        /// <remarks>
        /// Reads <see cref="DeNelle.Core.State.GameState.EchoCount"/> — the PERSISTED authority in
        /// <c>DeNelle.Core</c> — deliberately, NOT <c>EchoService.Instance</c>. EchoService lives in
        /// DeNelle.Village and floors its getter at 1, and reading the Core field keeps this gate
        /// working headlessly and in any assembly that can already see GameState. Six Echoes exist,
        /// so the lever tops out at four extra slots per channel.
        /// </remarks>
        public int EchoEntitledSlots()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return 0;
            int floor = Config != null ? Mathf.Max(0, Config.extraSlotEchoFloor) : 2;
            return Mathf.Max(0, state.EchoCount - floor);
        }

        /// <summary>
        /// WO-911 (Q6) — crystal price of the NEXT extra slot on <paramref name="id"/>. Rises with
        /// the slots already bought on that channel so the second one is not the same trivial spend
        /// as the first. 0 when the sink is disabled in config.
        /// </summary>
        public int NextSlotPrice(ChannelId id)
        {
            int baseCost = Config != null ? Mathf.Max(0, Config.extraSlotBaseCrystals) : 0;
            if (baseCost <= 0) return 0;
            var ch = GetChannel(id);
            int bought = ch != null ? Mathf.Max(0, ch.BoughtSlots) : 0;
            return baseCost * (1 + bought);
        }

        /// <summary>
        /// WO-911 (Q6) — the owner's TWO-STEP purchase: the Echo count must unlock the right to buy
        /// AND the crystals must be spent. Charges the one GameState wallet, then widens both the
        /// worker pool and the line (see <see cref="QueueDepthLimit"/>).
        /// </summary>
        /// <param name="failure">
        /// Player-readable ASCII reason on a false return, null on success. State is carried by TEXT
        /// (the owner is red/green colourblind) and the broke case is prefixed with
        /// <see cref="InsufficientCrystalsPrefix"/> so the caller can route to the crystal store.
        /// </param>
        public bool TryBuySlot(ChannelId id, out string failure)
        {
            failure = null;
            var ch = GetChannel(id);
            if (ch == null) { failure = "Save not loaded."; return false; }

            int entitled = EchoEntitledSlots();
            int bought = Mathf.Max(0, ch.BoughtSlots);
            if (bought >= entitled)
            {
                // STEP ONE failed. Say WHAT unlocks it — an unexplained locked button is the bug.
                failure = entitled <= 0
                    ? "Locked. Awaken a 3rd Echo to unlock extra queue slots."
                    : $"Locked. You have used all {entitled} slot(s) your Echoes unlock - awaken another Echo.";
                DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                    $"slot buy on {id} refused — Echo gate ({bought} bought / {entitled} entitled).");
                return false;
            }

            int price = NextSlotPrice(id);
            if (price <= 0) { failure = "Extra slots are not for sale right now."; return false; }

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null) { failure = "Save not loaded."; return false; }
            if (state.Resources.Crystals < price)
            {
                // STEP TWO failed. Stay visible + route to the faucet; never a silent no-op.
                failure = InsufficientCrystalsPrefix + $"{price} needed, {state.Resources.Crystals} held.";
                DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                    $"slot buy on {id} declined — broke ({state.Resources.Crystals}/{price}).");
                return false;
            }

            svc.AddCrystals(-price);
            GrantSlot(id);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                $"extra slot bought on {id} for {price} crystals (now {SlotCount(id)} slots, " +
                $"line depth {QueueDepthLimit(id)}).");
            return true;
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
        public BuildJobData? StartBuild(string structureId, int tier = 0, bool firstEverBuild = false)
            => StartBuilderJob(structureId, BuildJobType.Build, JobKind.Build, tier,
                               firstEverBuild: firstEverBuild);

        /// <summary>
        /// WO-911 (M2) — <see cref="StartBuild(string,int,bool)"/> that RECORDS what the caller
        /// charged, so a later cancel can refund exactly 100% of it (owner ruling Q1). Pass the
        /// basket that was actually debited — <c>default</c> for a free build. Prefer this overload
        /// at every charging site; the un-costed one exists only for callers that genuinely pay
        /// nothing.
        /// </summary>
        public BuildJobData? StartBuild(string structureId, int tier, bool firstEverBuild, JobCost paid)
            => StartBuilderJob(structureId, BuildJobType.Build, JobKind.Build, tier,
                               firstEverBuild: firstEverBuild, paid: paid);

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
        /// WO-911 (M2) — <see cref="StartUpgrade(string,int)"/> that RECORDS the charged basket for
        /// the 100%-flat cancel refund (ruling Q1).
        /// </summary>
        public BuildJobData? StartUpgrade(string structureId, int targetLevel, JobCost paid)
            => StartBuilderJob(structureId, BuildJobType.Upgrade, JobKind.Upgrade,
                               Mathf.Max(0, targetLevel - 2), targetLevel, paid: paid);

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

        private BuildJobData? StartBuilderJob(string structureId, BuildJobType type, JobKind kind, int tier,
                                              int targetLevel = 0, bool firstEverBuild = false,
                                              JobCost paid = default)
        {
            if (string.IsNullOrEmpty(structureId)) return null;
            var ch = Builder;
            if (ch == null) return null;

            // One job per structure id (across active AND pending).
            if (IndexInChannel(ch, structureId) >= 0) return null;

            var curveKind = type == BuildJobType.Upgrade ? BuildJobKind.Upgrade : BuildJobKind.Build;
            double durationMs = Config.DurationSecondsForTier(tier, curveKind) * 1000.0;

            // OWNER RULING 2026-08-06 -- first-build grace. The FIRST time the player places a
            // given structure it builds in firstBuildSeconds (5s) instead of the tier curve, so
            // onboarding never stalls on a timer. Storage containers (the "pallets" -- lumberyard
            // / foundry / silo) are EXCLUDED by her explicit carve-out; the caller decides that,
            // because only it knows the catalog id (structureId here is the JOB key, which for a
            // placement is UnderConstructionVisual.KeyFor(data), NOT the catalog id).
            //
            // Upgrades never qualify: "first build" means the first BUILD. An upgrade of a
            // structure you have never owned is not reachable anyway.
            if (firstEverBuild && type != BuildJobType.Upgrade && Config.firstBuildSeconds > 0f)
            {
                double graceMs = Config.firstBuildSeconds * 1000.0;
                // Only ever SHORTEN. A tier-0 curve already under the grace (or a retuned config)
                // must not be made slower by this rule.
                if (graceMs < durationMs)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Step("BuildTimer",
                        $"FIRST-BUILD grace on '{structureId}': {durationMs / 1000.0:0.#}s -> " +
                        $"{Config.firstBuildSeconds:0.#}s (tier {tier} curve bypassed).");
                    durationMs = graceMs;
                }
            }

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
                Paid = paid,                      // WO-911 M2 — the refund basket rides the job
            };

            bool started = ObsidianQueueEngine.Enqueue(ch, BuilderSlots, job, TimeSource.NowUnixMs(),
                                                       QueueDepthLimit(ChannelId.Builder), out bool accepted);
            if (!accepted)
            {
                // WO-911 (Q4) — the line is at its DEPTH cap. Refuse LOUDLY: the caller has usually
                // already charged, so a silent null here would eat the player's resources.
                _lastEnqueueFailure = DepthRefusalMessage(ChannelId.Builder);
                DeNelle.Core.Diagnostics.FlowTrace.Warn("BuildTimer",
                    $"StartBuilderJob REFUSED for '{structureId}' — {_lastEnqueueFailure} " +
                    "(caller must refund whatever it charged).");
                return null;
            }
            _lastEnqueueFailure = null;
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

        /// <summary>
        /// WO-911 (M2) — default-channel enqueue that RECORDS the charged basket for the 100%-flat
        /// cancel refund (ruling Q1).
        /// </summary>
        public BuildJobData? Enqueue(JobKind kind, string targetId, double durationSeconds, int targetTier, JobCost paid)
            => Enqueue(kind, JobChannels.DefaultChannel(kind), targetId, durationSeconds, targetTier, paid);

        /// <summary>Enqueue a job onto an explicit <paramref name="channel"/> (see the default-channel overload).</summary>
        public BuildJobData? Enqueue(JobKind kind, ChannelId channel, string targetId, double durationSeconds, int targetTier = 0)
            => Enqueue(kind, channel, targetId, durationSeconds, targetTier, default);

        /// <summary>
        /// Enqueue onto an explicit <paramref name="channel"/>, recording the basket the caller
        /// charged (WO-911 M2). Returns null when the id is already in flight OR when the line is at
        /// its DEPTH cap — check <see cref="LastEnqueueFailure"/> to tell those apart and to get a
        /// player-readable reason.
        /// </summary>
        public BuildJobData? Enqueue(JobKind kind, ChannelId channel, string targetId, double durationSeconds,
                                     int targetTier, JobCost paid)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            var ch = GetChannel(channel);
            if (ch == null) return null;
            if (IndexInChannel(ch, targetId) >= 0)
            {
                _lastEnqueueFailure = "Already in the queue.";
                return null;
            }

            var job = new BuildJobData
            {
                StructureId = targetId,
                JobType = kind == JobKind.Upgrade || kind == JobKind.TowerUpgrade || kind == JobKind.WallUpgrade
                    ? (int)BuildJobType.Upgrade : (int)BuildJobType.Build,
                Kind = (int)kind,
                Channel = (int)channel,
                DurationMs = Math.Max(0.0, durationSeconds) * 1000.0,
                TargetTier = targetTier,
                Paid = paid,                      // WO-911 M2 — the refund basket rides the job
            };

            bool started = ObsidianQueueEngine.Enqueue(ch, SlotCount(channel), job, TimeSource.NowUnixMs(),
                                                       QueueDepthLimit(channel), out bool accepted);
            if (!accepted)
            {
                _lastEnqueueFailure = DepthRefusalMessage(channel);
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Obsidian",
                    $"job '{kind}' -> '{targetId}' REFUSED on {channel} — {_lastEnqueueFailure} " +
                    "(caller must refund whatever it charged).");
                return null;
            }
            _lastEnqueueFailure = null;
            Persist();
            DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                $"job '{kind}' -> '{targetId}' {(started ? "started" : "QUEUED")} on {channel} ({durationSeconds:0}s).");
            if (started) JobStarted?.Invoke(job);
            RaiseQueueChanged();
            if (started && job.DurationMs <= 0) CompleteChannelJob(channel, targetId);
            return job;
        }

        // =====================================================================
        //  WO-911 (M1) — QUEUE DEPTH: 5 TOTAL PER LINE (owner ruling Q4)
        // =====================================================================

        private string _lastEnqueueFailure;

        /// <summary>
        /// WO-911 — why the LAST enqueue attempt was refused, in player-readable ASCII, or null if
        /// the last attempt succeeded. Callers surface this instead of failing silently (§12).
        /// </summary>
        public string LastEnqueueFailure => _lastEnqueueFailure;

        /// <summary>
        /// WO-911 (Q4) — the DEPTH cap for <paramref name="id"/>: how many items may be lined up on
        /// that ONE channel (active + pending). Authored data
        /// (<see cref="BuildTimerConfig.queueDepthPerLine"/>, 5) plus the purchased slots on this
        /// channel, so an Echo-gated slot buy widens the line as well as the worker pool. 0 when the
        /// config disables the cap.
        /// </summary>
        /// <remarks>
        /// ⚠ Per-CHANNEL, never global (ruling Q4): Builder at 5 must not block Research or Train.
        /// And this is NOT <see cref="BuildTimerConfig.freeBuildSlots"/> — see WO-911 section 2d.
        /// </remarks>
        public int QueueDepthLimit(ChannelId id)
        {
            int authored = Config != null ? Config.queueDepthPerLine : 0;
            if (authored <= 0) return 0;                      // uncapped by config
            var ch = GetChannel(id);
            int bought = ch != null ? Mathf.Max(0, ch.BoughtSlots) : 0;
            return authored + bought;
        }

        /// <summary>WO-911 — true when <paramref name="id"/> can accept no more work right now.</summary>
        public bool IsLineFull(ChannelId id) => ObsidianQueueEngine.IsFull(GetChannel(id), QueueDepthLimit(id));

        /// <summary>WO-911 — items currently lined up on <paramref name="id"/> (active + pending).</summary>
        public int QueueDepth(ChannelId id)
        {
            var ch = GetChannel(id);
            return ch != null ? ch.Count : 0;
        }

        /// <summary>
        /// WO-911 — the ASCII, colour-independent reason a line refused work. State is carried by
        /// TEXT because the owner is red/green colourblind; never encode "full" by tint alone.
        /// </summary>
        private string DepthRefusalMessage(ChannelId id)
            => $"{ChannelWord(id)} queue is full ({QueueDepth(id)}/{QueueDepthLimit(id)}). Cancel or finish an item first.";

        // =====================================================================
        //  WO-911 (M2) — COST ADAPTERS
        //  -------------------------------------------------------------------
        //  The tree carries THREE unrelated ResourceCost types (Core.Catalog's
        //  lowercase struct, EconomyService's PascalCase struct with Coins, and
        //  the Ledger's one-line-per-resource readonly struct). JobCost is a
        //  plain 5-int value in Core so BuildJobData depends on none of them;
        //  these adapters are the ONLY place the shapes meet, so a charge site
        //  records its basket in one call and cannot mis-map a field.
        // =====================================================================

        /// <summary>WO-911 — the catalog cost shape (structures-catalog / upgradeCost) as a paid basket.</summary>
        public static JobCost ToJobCost(DeNelle.Core.Catalog.ResourceCost c)
            => new JobCost(c.wood, c.food, c.iron, c.crystals);

        /// <summary>WO-911 — the EconomyService cost shape as a paid basket. Coins are NOT refundable here.</summary>
        /// <remarks>
        /// Fully qualified on purpose: this file carries <c>using DeNelle.Core.Catalog;</c>, so a bare
        /// <c>ResourceCost</c> here would silently mean the ENCLOSING namespace's type and read as the
        /// same overload as the one above. Naming both explicitly makes the pair unmistakable.
        /// </remarks>
        public static JobCost ToJobCost(DeNelle.Village.ResourceCost c)
        {
            if (c.Coins > 0)
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Obsidian",
                    $"job cost carries {c.Coins} coins — coins are not part of the refundable basket " +
                    "(no ledger lane); a cancel will not return them.");
            return new JobCost(c.Wood, c.Food, c.Iron, c.Crystals);
        }

        /// <summary>WO-911 — a ledger cost-line list (+ optional magic) as a paid basket.</summary>
        public static JobCost ToJobCost(System.Collections.Generic.IReadOnlyList<Ledger.ResourceCost> lines, int magic = 0)
        {
            var jc = new JobCost(0, 0, 0, 0, Mathf.Max(0, magic));
            if (lines == null) return jc;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Amount <= 0) continue;
                switch (line.Resource)
                {
                    case Ledger.HarvestResource.Wood: jc.Wood += line.Amount; break;
                    case Ledger.HarvestResource.Food: jc.Food += line.Amount; break;
                    case Ledger.HarvestResource.Iron: jc.Iron += line.Amount; break;
                    case Ledger.HarvestResource.Crystals: jc.Crystals += line.Amount; break;
                }
            }
            return jc;
        }

        /// <summary>ASCII display word for a channel ("Builders" / "Training" / "Research").</summary>
        public static string ChannelWord(ChannelId id)
        {
            switch (id)
            {
                case ChannelId.Train: return "Training";
                case ChannelId.Research: return "Research";
                default: return "Builders";
            }
        }

        // =====================================================================
        //  Query — Builder channel (WO-172 API, back-compat)
        // =====================================================================

        /// <summary>True if a job (running OR queued) is in flight for this structure id on the Builder channel.</summary>
        public bool IsBuilding(string structureId) => Builder != null && IndexInChannel(Builder, structureId) >= 0;

        /// <summary>Seconds remaining for the Builder job on <paramref name="structureId"/> (full duration while queued; 0 if none).</summary>
        public double RemainingSeconds(string structureId) => RemainingSeconds(ChannelId.Builder, structureId);

        /// <summary>
        /// WO-911 — seconds remaining for a job on ANY channel. A QUEUED job (StartMs = 0) reports
        /// its FULL duration: it has not started, so nothing has elapsed. That is the pricing input
        /// ruling Q5 needs, and it feeds the EXISTING curve
        /// (<see cref="BuildTimerConfig.InstantFinishPrice"/>) rather than a second one.
        /// </summary>
        public double RemainingSeconds(ChannelId channel, string structureId)
        {
            var job = FindInChannel(GetChannel(channel), structureId, out bool _);
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

        /// <summary>Crystal price to instant-finish this Builder job right now (0 = none in flight / paid skip disabled).</summary>
        public int InstantFinishPrice(string structureId) => InstantFinishPrice(ChannelId.Builder, structureId);

        // =====================================================================
        //  WO-911 — SPEEDUPS ON EVERY CHANNEL, INCLUDING QUEUED JOBS
        //  -------------------------------------------------------------------
        //  Owner's monetization spine: "Finish on ALL channels", "always show
        //  Finish while a job runs", "price from remaining time with a minimum".
        //  Ruling Q5 extends it to a job that has NOT started.
        //
        //  Before this WO these three wrappers hard-resolved the Builder channel
        //  and early-returned on StartMs <= 0, so Train/Research showed no CTA at
        //  all and 3 of a 5-deep queue offered nothing. The COMPLETION machinery
        //  was ALREADY channel-generic (CompleteChannelJob), so this is a
        //  generalization of the wrappers -- NOT new machinery, and NOT a second
        //  pricing curve. BuildTimerConfig.InstantFinishPrice stays the only
        //  curve; all that changed is what "remaining" means for a queued job
        //  (its full duration -- see RemainingSeconds above).
        // =====================================================================

        /// <summary>
        /// WO-911 — crystal price to Complete Now on ANY channel, for a RUNNING **or QUEUED** job
        /// (ruling Q5). 0 only when no such job exists or the paid skip is disabled in config.
        /// Priced from remaining time through the existing curve, with its authored minimum, so a
        /// near-done job is never free.
        /// </summary>
        public int InstantFinishPrice(ChannelId channel, string structureId)
        {
            var job = FindInChannel(GetChannel(channel), structureId, out bool _);
            if (!job.HasValue) return 0;
            return Config.InstantFinishPrice(RemainingSeconds(channel, structureId));
        }

        /// <summary>True when a rewarded-ad skip is allowed right now (cooldown clear AND daily cap not hit).</summary>
        public bool CanWatchAdToSkip(string structureId) => CanWatchAdToSkip(ChannelId.Builder, structureId);

        /// <summary>
        /// WO-911 — ad-skip eligibility on ANY channel. Still RUNNING-ONLY: the ad grants a fixed
        /// -N minutes off a countdown, and a queued job has no countdown to shorten yet (pulling its
        /// StartMs back would silently start it out of FIFO order). A queued item's speed-up is
        /// Complete Now, which is explicit and priced.
        /// </summary>
        public bool CanWatchAdToSkip(ChannelId channel, string structureId)
        {
            // RELEASE BLOCKER GATE (2026-08-07): OFF by default, so every ad CTA is ABSENT until a
            // real ad SDK + WO-912 server-side ad-window validation land. See
            // FeatureFlags.RewardedAdSkip for the two hard prerequisites.
            if (!DeNelle.Core.FeatureFlags.RewardedAdSkip)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Once("Obsidian", "adskip-flagoff",
                    "Ad-skip CTA suppressed on every channel: ff.rewardedadskip is OFF (no ad SDK " +
                    "is wired, so the reward would be granted for free). Not a missing button.");
                return false;
            }

            var job = FindInChannel(GetChannel(channel), structureId, out bool isActive);
            if (!job.HasValue || !isActive || job.Value.StartMs <= 0) return false;
            if (UnderDailyAdCap() == false) return false;
            var mgr = RewardedAdManager.Instance;
            return mgr != null && mgr.IsAdReady;
        }

        /// <summary>
        /// Watch a rewarded ad to knock a fixed chunk (Config.adSkipSeconds) off the remaining
        /// timer. Opt-in, store-build only, capped per day. The timer always finishes on its own.
        /// </summary>
        public bool WatchAdToSkip(string structureId) => WatchAdToSkip(ChannelId.Builder, structureId);

        /// <summary>WO-911 — ad-skip on ANY channel (running jobs only; see <see cref="CanWatchAdToSkip(ChannelId,string)"/>).</summary>
        public bool WatchAdToSkip(ChannelId channel, string structureId)
        {
            // Same gate as CanWatchAdToSkip — the ACT is refused too, not just the affordance, so a
            // stale UI reference or a direct caller can never reach the reward while the flag is OFF.
            if (!DeNelle.Core.FeatureFlags.RewardedAdSkip)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Obsidian",
                    $"WatchAdToSkip('{structureId}' on {channel}) REFUSED: ff.rewardedadskip is OFF. " +
                    "No ad SDK is wired, so granting the skip would be a free reward with no ad shown. " +
                    "No time was skipped and no window allowance was consumed.");
                return false;
            }

            var job = FindInChannel(GetChannel(channel), structureId, out bool isActive);
            if (!job.HasValue || !isActive || job.Value.StartMs <= 0) return false;
            if (!UnderDailyAdCap()) return false;

            var mgr = RewardedAdManager.Instance;
            if (mgr == null) return false;

            return mgr.TryShowAd(() =>
            {
                RecordAdSkipUsed();
                ApplySkipSeconds(channel, structureId, Config.adSkipSeconds);
            });
        }

        /// <summary>
        /// Premium instant-finish: spend crystals (single GameState wallet) to complete the
        /// Builder job now. No-op if the price is 0 (disabled) or unaffordable.
        /// </summary>
        public bool TryInstantFinish(string structureId) => TryInstantFinish(ChannelId.Builder, structureId);

        /// <summary>
        /// WO-911 — Complete Now on ANY channel, for a RUNNING or QUEUED job (ruling Q5), spending
        /// crystals from the one GameState wallet. Acts ONLY on the id passed in (ruling Q11: never
        /// a game-wide pass, never an ambiguous aggregate).
        /// </summary>
        /// <param name="failure">
        /// Player-readable ASCII reason on a false return, or null on success. NEVER a silent
        /// no-op: today's UI taps a visible button and nothing happens (WO-911 section 2c #5).
        /// "Broke" is reported distinctly so the caller can route to the crystal store.
        /// </param>
        public bool TryInstantFinish(ChannelId channel, string structureId, out string failure)
        {
            failure = null;
            int price = InstantFinishPrice(channel, structureId);
            if (price <= 0)
            {
                failure = "Nothing to finish here.";
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Obsidian",
                    $"TryInstantFinish('{structureId}' on {channel}) — no priced job (price {price}).");
                return false;
            }

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                failure = "Save not loaded.";
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Obsidian", "TryInstantFinish with no GameState.");
                return false;
            }
            if (state.Resources.Crystals < price)
            {
                // Owner's rule: the button STAYS VISIBLE when broke and offers a route to buy.
                // The caller reads InsufficientCrystalsPrefix to decide to open the store.
                failure = InsufficientCrystalsPrefix + $"{price} needed, {state.Resources.Crystals} held.";
                DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                    $"TryInstantFinish('{structureId}') declined — broke ({state.Resources.Crystals}/{price}).");
                return false;
            }

            svc.AddCrystals(-price);
            CompleteAnyJob(channel, structureId);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                $"Complete Now: '{structureId}' on {channel} for {price} crystals.");
            return true;
        }

        /// <summary>WO-911 — <see cref="TryInstantFinish(ChannelId,string,out string)"/> without the reason.</summary>
        public bool TryInstantFinish(ChannelId channel, string structureId)
            => TryInstantFinish(channel, structureId, out _);

        /// <summary>
        /// WO-911 — marker that prefixes the "cannot afford" failure so a caller can tell the broke
        /// case (route to the crystal store) from a structural one, without parsing prose.
        /// </summary>
        public const string InsufficientCrystalsPrefix = "Not enough crystals: ";

        // Apply a time skip by pulling StartMs back by `seconds`; if that finishes it, complete it.
        private void ApplySkipSeconds(ChannelId channel, string structureId, float seconds)
        {
            var ch = GetChannel(channel);
            if (ch == null || seconds <= 0f) return;
            int i = ActiveIndexInChannel(ch, structureId);
            if (i < 0) return;

            var j = ch.ActiveJobs[i];
            if (j.StartMs <= 0) return;             // queued — nothing to skip yet
            j.StartMs -= seconds * 1000.0;          // earlier start → earlier finish
            ch.ActiveJobs[i] = j;

            if (j.FinishMs <= TimeSource.NowUnixMs())
            {
                CompleteChannelJob(channel, structureId);
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

        /// <summary>
        /// WO-911 (ruling Q5) — force-complete a job on <paramref name="channel"/> whether it is
        /// RUNNING or still QUEUED, then cascade the channel.
        /// -------------------------------------------------------------------------------------
        /// <see cref="CompleteChannelJob"/> is deliberately ACTIVE-ONLY: it scans ActiveJobs and
        /// silently no-ops on a pending id (which is exactly why the zero-duration auto-complete at
        /// the enqueue seams is gated on <c>started</c>). Ruling Q5 requires a player to be able to
        /// pay to finish an item that has not started, so this wrapper lifts the pending job out of
        /// the FIFO first and completes it directly — it does NOT promote it into a slot, because
        /// that would evict FIFO order for the items ahead of it. The items behind it close the gap
        /// through the normal pull, so no hole is left (the same guarantee cancel gives).
        /// </summary>
        public void CompleteAnyJob(ChannelId channel, string structureId)
        {
            var ch = GetChannel(channel);
            if (ch == null) return;

            if (ActiveIndexInChannel(ch, structureId) >= 0)
            {
                CompleteChannelJob(channel, structureId);
                return;
            }

            int p = PendingIndexInChannel(ch, structureId);
            if (p < 0)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Obsidian",
                    $"CompleteAnyJob('{structureId}' on {channel}) — no such job, active or pending.");
                return;
            }

            var job = ch.PendingQueue[p];
            ch.PendingQueue.RemoveAt(p);            // the rest shift up; pending jobs hold no slot
            DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                $"Complete Now on a QUEUED job '{structureId}' ({channel}) — lifted from position {p} of the line.");
            OnJobCompleted(job);

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

        // =====================================================================
        //  WO-911 — CANCEL WITH A 100% REFUND (owner ruling Q1)
        // =====================================================================

        /// <summary>
        /// WO-911 (ruling Q1) — cancel ONE job and refund <b>100% of what was paid for it</b>,
        /// FLAT, regardless of how much time has elapsed. An ACTIVE job at 90% refunds exactly the
        /// same as an untouched pending one.
        /// -------------------------------------------------------------------------------------
        /// The owner took the Finish-Now interaction knowingly: yes, a full refund on a nearly-done
        /// job is a free alternative to paying crystals — <b>the player still loses the elapsed
        /// TIME, and the time is the real cost</b>. Do NOT "protect" the sink with a partial refund
        /// and do NOT add an elapsed-time scaling curve.
        ///
        /// The refund is the basket the job CARRIES (v37 <see cref="BuildJobData.Paid"/>), never a
        /// re-derivation: the placement path charges against the LIVE tower count and a first-build
        /// freebie charges nothing, so re-deriving would refund a number the player never paid.
        /// A pre-v37 job carries no basket and refunds ZERO — traced, never silent.
        ///
        /// Credited through <see cref="Ledger.ResourceLedger"/>, which writes the SAME GameState
        /// fields every charge site debits (EconomyService.TrySpend reads/writes those very fields),
        /// and is UNCAPPED — an EconomyService "earned income" grant would silently evaporate
        /// against the town bank cap and eat the refund.
        ///
        /// Cancelling an ACTIVE job frees its slot and the next pending job starts immediately;
        /// cancelling a PENDING one closes the gap. Both are existing engine behaviour.
        /// </summary>
        /// <param name="refunded">What was actually credited back (all-zero on failure or a legacy job).</param>
        /// <returns>True if a job was found and cancelled.</returns>
        public bool CancelChannelJobWithRefund(ChannelId channel, string structureId, out JobCost refunded)
        {
            refunded = default;
            var ch = GetChannel(channel);
            if (ch == null) return false;

            // Read the basket BEFORE the job is removed — the cancel destroys the record.
            var found = FindInChannel(ch, structureId, out bool wasActive);
            if (!found.HasValue)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Obsidian",
                    $"cancel+refund: no job '{structureId}' on {channel}.");
                return false;
            }
            var paid = found.Value.Paid;

            if (!CancelChannelJob(channel, structureId)) return false;

            if (paid.IsZero)
            {
                // Either a genuinely free build or a pre-v37 save. Both are legitimate; neither is
                // silent (§12) — a zero refund the player did not expect must be explainable.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Obsidian",
                    $"cancelled '{structureId}' on {channel} with a ZERO refund — the job carries no " +
                    "paid basket (free build, or an in-flight job from a pre-v37 save).");
                return true;
            }

            DeNelle.Core.Diagnostics.Guard.Try("Obsidian", "refund cancelled job", () =>
            {
                Ledger.ResourceLedger.Credit(Ledger.HarvestResource.Wood, paid.Wood);
                Ledger.ResourceLedger.Credit(Ledger.HarvestResource.Food, paid.Food);
                Ledger.ResourceLedger.Credit(Ledger.HarvestResource.Iron, paid.Iron);
                Ledger.ResourceLedger.Credit(Ledger.HarvestResource.Crystals, paid.Crystals);
                if (paid.Magic > 0) Ledger.ResourceLedger.CreditMagic(paid.Magic);
            });

            refunded = paid;
            DeNelle.Core.Diagnostics.FlowTrace.Step("Obsidian",
                $"cancelled {(wasActive ? "ACTIVE" : "queued")} '{structureId}' on {channel} — " +
                $"refunded 100% ({paid.Describe()}).");
            return true;
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
        //  Ad-skip cap - a ROLLING WINDOW anchored on first use (owner, 2026-08-06)
        // =====================================================================
        //  Was a calendar-day cap. Her ruling: "when the first ad comes in, we mark a
        //  timer, and that's their four hour rolling from there."
        //
        //  WHY FIXED-FROM-FIRST-USE AND NOT A SLIDING WINDOW - do not "improve" this:
        //  a sliding window drips the allowance back one at a time, so a free player
        //  limps along indefinitely. This one lets them burn the allowance and then hit
        //  a HARD WALL AT ZERO for the rest of the window. The wall IS the conversion
        //  trigger - the cap exists to create the spend moment, not to limit ad revenue.
        //  A day-reset was also worse for a different reason: it punishes an evening
        //  player who spent their allowance that morning. Rolling always offers a way back.
        //
        //  NO SCHEMA BUMP. The two persisted fields already existed (v13/WO-172) and only
        //  change MEANING: AdSkipDayKey now holds the window-start instant (round-trip
        //  "o" format), AdSkipsUsedToday the count within it. The names are now misleading
        //  and worth renaming on the next schema touch.
        //
        //  KNOWN LIMIT - the window start is a DEVICE clock (UtcNow). Moving the device
        //  clock forward past the window grants a fresh allowance. That is not just free
        //  skips: once a real ad SDK is behind this it is FABRICATED IMPRESSIONS against
        //  the ad account, which is what networks ban for. A trustworthy version needs the
        //  window stamped/validated server-side where the save already round-trips. Tracked
        //  in WO-912; deliberately not solved here because no ad SDK is wired yet.

        private bool UnderDailyAdCap()
        {
            int cap = Config.adSkipsPerWindow;
            if (cap <= 0) return true;              // 0 = unlimited
            RollWindowIfNeeded();
            var state = State;
            return state != null && state.AdSkipsUsedToday < cap;
        }

        private void RecordAdSkipUsed()
        {
            RollWindowIfNeeded();
            var state = State;
            if (state == null) return;
            // The FIRST watch of a window is what starts the clock, so stamp it here
            // rather than on the check - merely opening a screen must not burn the window.
            if (string.IsNullOrEmpty(state.AdSkipDayKey))
                state.AdSkipDayKey = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            state.AdSkipsUsedToday++;
            Persist();
        }

        private void RollWindowIfNeeded()
        {
            var state = State;
            if (state == null) return;

            float windowSeconds = Config.adSkipWindowSeconds;
            if (windowSeconds <= 0f) return;        // no window configured => never rolls

            if (string.IsNullOrEmpty(state.AdSkipDayKey)) return;   // no window open yet

            DateTime start;
            if (!DateTime.TryParse(state.AdSkipDayKey,
                                   CultureInfo.InvariantCulture,
                                   DateTimeStyles.RoundtripKind,
                                   out start))
            {
                // A legacy "yyyy-MM-dd" day key (or anything unparseable) lands here.
                // Treat it as a closed window rather than trusting it: the player gets a
                // fresh allowance once, on upgrade, which is the forgiving direction.
                state.AdSkipDayKey = null;
                state.AdSkipsUsedToday = 0;
                return;
            }

            double elapsed = (DateTime.UtcNow - start.ToUniversalTime()).TotalSeconds;
            // A NEGATIVE elapsed means the clock moved backwards since the stamp - treat it
            // as tampering and close the window rather than leaving it open forever.
            if (elapsed < 0d || elapsed >= windowSeconds)
            {
                state.AdSkipDayKey = null;
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

                // CARD-RAIL VIEW (WO-864; supersedes the WO-778 text rows): every channel
                // publishes its jobs active-first, then the waiting line in order. The
                // extra card fields (verb / icon / stack) are resolved HERE, once per
                // publish, so the always-on HUD rail, the Work Queue modal and any future
                // Manage screen render the SAME card from the SAME data and can never
                // disagree. Still pure presentation marshalling — no timer/economy logic.
                s.Entries = BuildEntries(ChannelId.Builder, now);
                s.TrainEntries = BuildEntries(ChannelId.Train, now);
                s.ResearchEntries = BuildEntries(ChannelId.Research, now);
            }
            DeNelle.Core.UI.ObsidianQueueGate.PublishStatus(s);
            PublishArmyStatus();
        }

        // WO-864: one channel's card list — ACTIVE jobs first (each owns a slot and a live
        // countdown), then the FIFO waiting line. Identical PENDING troop trains collapse to
        // ONE card carrying an "xN" badge (the CoC "Barbarian x5" read) instead of N cards
        // that say the same word. Active jobs never collapse — each is a real running slot.
        // FREE slots are NOT published here: the view derives them from SlotCount, so a
        // channel that grows a slot needs no publisher change.
        private const int MaxPublishedCards = 24;   // sanity bound; the rail tails the rest

        private System.Collections.Generic.List<DeNelle.Core.UI.ObsidianQueueGate.QueueEntry> _entryBuf
            = new System.Collections.Generic.List<DeNelle.Core.UI.ObsidianQueueGate.QueueEntry>();

        private DeNelle.Core.UI.ObsidianQueueGate.QueueEntry[] BuildEntries(ChannelId channel, double now)
        {
            var act = ActiveJobsOf(channel);
            var pend = PendingJobsOf(channel);
            _entryBuf.Clear();

            for (int i = 0; i < act.Count && _entryBuf.Count < MaxPublishedCards; i++)
            {
                var e = MakeEntry(act[i]);
                e.RemainingSec = (int)System.Math.Max(0.0, (act[i].FinishMs - now) / 1000.0);
                e.Queued = false;
                _entryBuf.Add(e);
            }

            int firstPending = _entryBuf.Count;
            for (int i = 0; i < pend.Count && _entryBuf.Count < MaxPublishedCards; i++)
            {
                var e = MakeEntry(pend[i]);
                e.RemainingSec = -1;
                e.Queued = true;

                // Collapse into an earlier PENDING card of the same stack key, if any.
                string key = StackKey(pend[i].StructureId);
                int merged = -1;
                if (key != null)
                    for (int j = firstPending; j < _entryBuf.Count; j++)
                        if (string.Equals(StackKey(_entryBuf[j].JobId), key, System.StringComparison.Ordinal))
                        { merged = j; break; }

                if (merged >= 0)
                {
                    var m = _entryBuf[merged];
                    m.StackCount++;
                    _entryBuf[merged] = m;
                }
                else _entryBuf.Add(e);
            }

            return _entryBuf.ToArray();
        }

        // A card record for one job: player-facing name + the VERB (owner ruling 2026-08-03 —
        // the card is built on the verb; art is the enhancement) + the icon route.
        private static DeNelle.Core.UI.ObsidianQueueGate.QueueEntry MakeEntry(BuildJobData job)
        {
            string label = ObsidianQueueHud.FormatJobTarget(job);
            if (string.IsNullOrEmpty(label)) label = PrettyJobLabel(job.StructureId);
            // The stack badge carries the count, so drop the "x1" the label suffixes onto trains.
            if (label.EndsWith(" x1", System.StringComparison.Ordinal))
                label = label.Substring(0, label.Length - 3);

            var e = new DeNelle.Core.UI.ObsidianQueueGate.QueueEntry
            {
                Label = label,
                Verb = CardVerb(job.JobKind),
                JobId = job.StructureId,
                TargetTier = job.TargetTier,
                StackCount = 1,
                Free = false,
            };

            // Troops have NO portraits on disk (measured 0/7) but every TroopDef carries an
            // iconId that DOES exist under RpgUi/icons — that route is the only thing that
            // gets a troop card any art at all.
            string troopId = TroopIdOfJob(job);
            if (!string.IsNullOrEmpty(troopId))
            {
                var def = TroopCatalog.Find(troopId);
                if (def != null && !string.IsNullOrEmpty(def.IconId))
                {
                    e.IconRole = DeNelle.Core.UI.RpgUiCatalog.RoleIcons;
                    e.IconKey = def.IconId;
                }
            }
            return e;
        }

        /// <summary>ASCII uppercase card verb. Never a raw enum name.</summary>
        private static string CardVerb(JobKind kind)
        {
            switch (kind)
            {
                case JobKind.Build:
                case JobKind.TowerBuild:      return "BUILD";
                case JobKind.Repair:          return "REPAIR";
                case JobKind.TrainTroop:      return "TRAIN";
                case JobKind.UnlockTier:      return "UNLOCK";
                case JobKind.LearnMagic:      return "LEARN";
                case JobKind.Upgrade:
                case JobKind.TowerUpgrade:
                case JobKind.WallUpgrade:
                case JobKind.BarracksUpgrade:
                case JobKind.TroopUpgrade:    return "UPGRADE";
                default:                      return "WORK";
            }
        }

        // The troop a job targets ("barracks-train:troop-footman:9f2c41ab" -> "troop-footman"),
        // or null when the job is not troop-shaped.
        private static string TroopIdOfJob(BuildJobData job)
        {
            string id = job.StructureId ?? "";
            if (id.StartsWith(BarracksService.TrainPrefix, System.StringComparison.Ordinal))
            {
                var parts = id.Split(':');
                return parts.Length >= 2 ? parts[1] : null;
            }
            if (id.StartsWith(BarracksService.TroopUpgradePrefix, System.StringComparison.Ordinal))
                return id.Substring(BarracksService.TroopUpgradePrefix.Length);
            return null;
        }

        // Jobs that are INTERCHANGEABLE collapse onto one card. Only troop trains qualify:
        // every structure job is unique by its "@cell" suffix, so collapsing those would hide
        // real work. Returns null for anything that must stay its own card.
        private static string StackKey(string structureId)
        {
            if (string.IsNullOrEmpty(structureId)) return null;
            if (!structureId.StartsWith(BarracksService.TrainPrefix, System.StringComparison.Ordinal)) return null;
            var parts = structureId.Split(':');
            return parts.Length >= 2 ? (parts[0] + ":" + parts[1]) : null;
        }

        // Full-army gate (owner ruling: HUD Raids button greys unless the army is full
        // counting ready + queued troops): publish the Raids-button army snapshot on the
        // SAME cadence as the chip snapshot (QueueChanged edges + the 1s heartbeat).
        // RaidEntryGate bumps its Version only on a value change, so the 1 Hz republish
        // is repaint-free for the HUD. The MATH lives in ArmyReadiness.Compute — the one
        // readiness formula (owner review 2026-08-01); this method only relays it to the
        // Core seam. Null GameState/Army -> Compute returns READY so headless/AutoPilot
        // never false-dims (mirrors RaidSelectionScreen's bypass).
        private void PublishArmyStatus()
        {
            var s = ArmyReadiness.Compute(State);
            DeNelle.Core.UI.RaidEntryGate.PublishArmyStatus(
                s.Ready, s.DeployableSlots, s.QueuedSlots, s.CapSlots);
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
