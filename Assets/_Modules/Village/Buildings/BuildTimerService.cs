// =============================================================================
// BuildTimerService — CoC-style build/upgrade timers + rewarded-ad speedup (WO-172).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Placing a building / buying an upgrade is NOT instant: it starts a timed
// construction job that completes after a real-time duration. The wait is the
// idle/retention hook; a rewarded ad (opt-in) skips a fixed chunk; a premium
// crystal spend can instant-finish. The timer ALWAYS completes on its own — the
// ad is a shortcut, never a wall (NORTH_STAR ad discipline).
//
// STANDALONE BY DESIGN. WO-108 (player build mode) is NOT built yet, so this is a
// self-contained service the build flow attaches to, rather than hard-coupled to an
// unbuilt system. See the §"WO-108 INTEGRATION POINT" block below for the exact seam.
//
// PERSISTENCE: every job lives in GameState.BuildJobs (a List<BuildJobData>) and the
// clock is TimeSource.NowUnixMs() — wall-clock unix-ms (the WO-115 seam). So a timer
// keeps counting while the app is closed: on load this service sweeps jobs whose
// FinishMs already passed and completes them (offline-fair). No frame state, no
// Update() loop required for correctness — Tick() only drives UI/visuals while open.
//
// AD SEAM: reuses the existing RewardedAdManager (DEF-69) gate — TryShowAd(onReward)
// with its cooldown. We do NOT greenfield an ad provider (CLAUDE.md §8 monetization
// is ~70% built). A per-day cap (BuildTimerConfig.adSkipsPerDay) layers on top of the
// manager's per-view cooldown.
//
// ASMDEF: DeNelle.Village → DeNelle.Core only. Resource/crystal spend routes through
// the single GameState wallet via GameStateService (WO-131), never a second balance.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Owns the set of in-flight construction/upgrade timers (WO-172). Start a job,
    /// query remaining time, complete it on expiry, and offer rewarded-ad / premium
    /// speedups. Persisted (GameState.BuildJobs) + offline-counting (TimeSource).
    /// Self-bootstrapping singleton (mirrors OfflineHarvestService / RewardedAdManager).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildTimerService : MonoBehaviour
    {
        public static BuildTimerService Instance { get; private set; }

        /// <summary>Raised when a job finishes (timer elapsed, ad/instant skip, or offline catch-up). Arg = the completed job.</summary>
        public event Action<BuildJobData> JobCompleted;

        /// <summary>Raised when a job is started/enqueued. Arg = the new job.</summary>
        public event Action<BuildJobData> JobStarted;

        /// <summary>Raised when a job's remaining time changes via a skip (ad / instant). Arg = the updated job.</summary>
        public event Action<BuildJobData> JobSkipped;

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
            // Offline catch-up: any job that finished while the app was closed
            // completes now (one frame's slack so structures/registries have awoken).
            StartCoroutine(SweepNextFrame());
        }

        private System.Collections.IEnumerator SweepNextFrame()
        {
            yield return null;
            SweepCompleted();
        }

        // While open, complete jobs the moment they expire so the UI/visual flips
        // without waiting for the next load. Cheap: a handful of jobs, checked ~1/s.
        private float _nextTick;
        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            SweepCompleted();
        }

        // =====================================================================
        //  Start a job
        // =====================================================================

        // ─────────────────────────────────────────────────────────────────────
        //  ★ WO-108 INTEGRATION POINT ★
        //  When player build-mode (WO-108) lands, its placement commit
        //  (BuildModeController.ConfirmPlace, AFTER the WO-131 wallet charge) calls:
        //
        //      BuildTimerService.Instance?.StartBuild(structureId, tier);
        //
        //  and treats the structure as UNDER CONSTRUCTION (scaffold + countdown bar)
        //  until JobCompleted fires for that structureId — then reveal/enable it.
        //  WO-151 upgrades call StartUpgrade(structureId, targetTier) the same way.
        //  `structureId` = the placed structure's unique id (PlacedStructureData key).
        //  Nothing here references WO-108 types, so this compiles + ships standalone.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Start a BUILD job for <paramref name="structureId"/> at <paramref name="tier"/>
        /// (0 = first build). Duration comes from the hybrid curve. Returns the job, or
        /// null if a slot isn't free or one already runs for this id. Caller charges the
        /// resource cost BEFORE calling (WO-131 single-spend); this only times the wait.
        /// </summary>
        public BuildJobData? StartBuild(string structureId, int tier = 0)
            => StartJob(structureId, BuildJobType.Build, tier);

        /// <summary>
        /// Start an UPGRADE job for an existing structure/building to <paramref name="targetLevel"/>
        /// (F8-51). The caller charges the cost at commit; the LEVEL applies at completion (the
        /// CompletedUpgradeApplier seam in <see cref="CompleteJob"/>, offline-fair like builds).
        /// Duration = the WO-172 config curve keyed by the 0-based upgrade STEP (targetLevel-2,
        /// so the first upgrade of a level-1 structure ≈ baseBuildSeconds × upgradeMultiplier
        /// ≈ 19s at defaults, ×tierGrowth per further tier). Returns null when a job already
        /// runs for this id or no build slot is free.
        /// </summary>
        public BuildJobData? StartUpgrade(string structureId, int targetLevel)
            => StartJob(structureId, BuildJobType.Upgrade,
                        Mathf.Max(0, targetLevel - 2), targetLevel);

        /// <summary>
        /// True when a new job could start right now (a free CoC build slot exists). F8-51:
        /// upgrade entry points check this BEFORE charging so a slot-full state rejects
        /// cleanly instead of degrading to an instant upgrade (placement, which has already
        /// charged by the time it starts its job, keeps its never-block instant fallback).
        /// </summary>
        public bool HasFreeSlot
        {
            get
            {
                var jobs = Jobs;
                return jobs != null && jobs.Count < Mathf.Max(1, Config.freeBuildSlots);
            }
        }

        private BuildJobData? StartJob(string structureId, BuildJobType type, int tier, int targetLevel = 0)
        {
            if (string.IsNullOrEmpty(structureId)) return null;
            var jobs = Jobs;
            if (jobs == null) return null;

            // One job per structure id.
            if (IndexOf(structureId) >= 0) return null;
            // Build-slot scarcity (CoC-style; extra slots are a future unlock).
            if (jobs.Count >= Mathf.Max(1, Config.freeBuildSlots)) return null;

            var kind = type == BuildJobType.Upgrade ? BuildJobKind.Upgrade : BuildJobKind.Build;
            double durationMs = Config.DurationSecondsForTier(tier, kind) * 1000.0;

            // WO-676 STEWARD (Foreman's Pace): ONE HeroTalentModifiers read at this existing
            // duration calc — `buildTime` shortens every build/upgrade timer at job start.
            // StatSum is internally null-safe (0 with no service/tree/nodes) and the sum is
            // clamped so a mis-authored node can never make timers negative. Identity at 0.
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
                StartMs = TimeSource.NowUnixMs(),
                DurationMs = durationMs,
                TargetTier = targetLevel,
            };
            jobs.Add(job);
            Persist();

            // F8-51 §12 trace — every upgrade timer names itself when it starts.
            if (type == BuildJobType.Upgrade)
                DeNelle.Core.Diagnostics.FlowTrace.Step("BuildTimer",
                    $"upgrade '{structureId}' started ({durationMs / 1000.0:0}s, " +
                    $"tier {Mathf.Max(1, targetLevel - 1)}->{targetLevel})");

            JobStarted?.Invoke(job);

            // A zero-duration job (e.g. tier 0 with base 0) completes immediately.
            if (durationMs <= 0) CompleteJob(structureId);
            return job;
        }

        // =====================================================================
        //  Query
        // =====================================================================

        /// <summary>True if a construction/upgrade job is in flight for this structure id.</summary>
        public bool IsBuilding(string structureId) => IndexOf(structureId) >= 0;

        /// <summary>Seconds remaining for the job on <paramref name="structureId"/> (0 if none / already done).</summary>
        public double RemainingSeconds(string structureId)
        {
            int i = IndexOf(structureId);
            if (i < 0) return 0;
            double remMs = Jobs[i].FinishMs - TimeSource.NowUnixMs();
            return remMs > 0 ? remMs / 1000.0 : 0;
        }

        /// <summary>0..1 progress for the job on <paramref name="structureId"/> (1 if none / done).</summary>
        public float Progress(string structureId)
        {
            int i = IndexOf(structureId);
            if (i < 0) return 1f;
            var j = Jobs[i];
            if (j.DurationMs <= 0) return 1f;
            double elapsed = TimeSource.NowUnixMs() - j.StartMs;
            return Mathf.Clamp01((float)(elapsed / j.DurationMs));
        }

        /// <summary>A read-only snapshot of all in-flight jobs (for a build-queue HUD).</summary>
        public IReadOnlyList<BuildJobData> ActiveJobs => Jobs;

        /// <summary>Crystal price to instant-finish this job right now (0 = unavailable / paid skip disabled).</summary>
        public int InstantFinishPrice(string structureId)
        {
            if (IndexOf(structureId) < 0) return 0;
            return Config.InstantFinishPrice(RemainingSeconds(structureId));
        }

        // =====================================================================
        //  Speedups — rewarded ad (opt-in, capped) + premium instant-finish
        // =====================================================================

        /// <summary>True when a rewarded-ad skip is allowed right now (cooldown clear AND daily cap not hit).</summary>
        public bool CanWatchAdToSkip(string structureId)
        {
            if (IndexOf(structureId) < 0) return false;
            if (UnderDailyAdCap() == false) return false;
            var mgr = RewardedAdManager.Instance;
            return mgr != null && mgr.IsAdReady;
        }

        /// <summary>
        /// Watch a rewarded ad to knock a fixed chunk (Config.adSkipSeconds) off the
        /// remaining timer. Opt-in, store-build only, capped per day. Returns true if
        /// the ad was dispatched. The skip applies in the reward callback (on genuine
        /// completion). The timer always finishes on its own — this is a shortcut.
        /// </summary>
        public bool WatchAdToSkip(string structureId)
        {
            if (IndexOf(structureId) < 0) return false;
            if (!UnderDailyAdCap()) return false;

            var mgr = RewardedAdManager.Instance;
            if (mgr == null) return false;

            // RewardedAdManager.TryShowAd handles its own cooldown gate + (stubbed)
            // ad presentation, invoking onReward only on completion.
            return mgr.TryShowAd(() =>
            {
                RecordAdSkipUsed();
                ApplySkipSeconds(structureId, Config.adSkipSeconds);
            });
        }

        /// <summary>
        /// Premium instant-finish: spend crystals (from the single GameState wallet)
        /// to complete the job now. Returns true on success. Convenience, not power
        /// (NS "flex not power"). No-op if the price is 0 (disabled) or unaffordable.
        /// </summary>
        public bool TryInstantFinish(string structureId)
        {
            int i = IndexOf(structureId);
            if (i < 0) return false;

            int price = InstantFinishPrice(structureId);
            if (price <= 0) return false;

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || state.Resources.Crystals < price) return false;

            svc.AddCrystals(-price);     // single source of truth (WO-131); persisted + HUD-synced
            CompleteJob(structureId);
            return true;
        }

        // Apply a time skip by pulling StartMs back by `seconds` (so remaining shrinks);
        // if that finishes the job, complete it.
        private void ApplySkipSeconds(string structureId, float seconds)
        {
            int i = IndexOf(structureId);
            if (i < 0 || seconds <= 0f) return;

            var jobs = Jobs;
            var j = jobs[i];
            j.StartMs -= seconds * 1000.0;          // earlier start → earlier finish
            jobs[i] = j;

            if (j.FinishMs <= TimeSource.NowUnixMs())
            {
                CompleteJob(structureId);
            }
            else
            {
                Persist();
                JobSkipped?.Invoke(j);
            }
        }

        // =====================================================================
        //  Completion / cancel
        // =====================================================================

        /// <summary>Force-complete a job now (used by skips and offline catch-up). Removes + raises JobCompleted.</summary>
        public void CompleteJob(string structureId)
        {
            int i = IndexOf(structureId);
            if (i < 0) return;
            var job = Jobs[i];
            Jobs.RemoveAt(i);
            Persist();

            // F8-51 — UPGRADE jobs apply their deferred level HERE, at the one completion
            // seam (same seam placement reveals through), so live-expiry, ad/instant skips
            // AND the offline-fair sweep all land the level identically. Called directly
            // (not via the event) so the apply can never be missed by a late subscriber;
            // Guard.Try so one bad apply logs + never blocks the JobCompleted reveal.
            if (job.Type == BuildJobType.Upgrade && job.TargetTier > 0)
                DeNelle.Core.Diagnostics.Guard.Try("BuildTimer", "apply completed upgrade",
                    () => Buildings.Progression.CompletedUpgradeApplier.Apply(job));

            JobCompleted?.Invoke(job);
        }

        /// <summary>
        /// Re-key an in-flight job (F8-51): a placed structure MOVED mid-timer changes its
        /// cell-derived job key; without this the finished job would find no structure to
        /// apply to. Timer progress is untouched. No-op when no job runs for the old key.
        /// </summary>
        public void RepointJob(string oldStructureId, string newStructureId)
        {
            if (string.IsNullOrEmpty(newStructureId) || oldStructureId == newStructureId) return;
            int i = IndexOf(oldStructureId);
            if (i < 0) return;
            var jobs = Jobs;
            var j = jobs[i];
            j.StructureId = newStructureId;
            jobs[i] = j;
            Persist();
            DeNelle.Core.Diagnostics.FlowTrace.Step("BuildTimer",
                $"job re-keyed '{oldStructureId}' -> '{newStructureId}' (structure moved mid-timer)");
        }

        /// <summary>
        /// Cancel a job WITHOUT completing it (e.g. the player sells the structure
        /// mid-build). Removes it; the caller owns any refund (WO-108 sell flow).
        /// </summary>
        public bool CancelJob(string structureId)
        {
            int i = IndexOf(structureId);
            if (i < 0) return false;
            Jobs.RemoveAt(i);
            Persist();
            return true;
        }

        // Complete every job whose finish time has passed (open OR offline). Iterate a
        // copy because CompleteJob mutates the list + fires listeners that may re-enter.
        private void SweepCompleted()
        {
            var jobs = Jobs;
            if (jobs == null || jobs.Count == 0) return;
            double now = TimeSource.NowUnixMs();

            List<string> due = null;
            for (int i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].FinishMs <= now)
                    (due ??= new List<string>()).Add(jobs[i].StructureId);
            }
            if (due == null) return;
            for (int i = 0; i < due.Count; i++) CompleteJob(due[i]);
        }

        // =====================================================================
        //  Daily ad-skip cap
        // =====================================================================

        // 0 in config = unlimited.
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

        // Reset the per-day counter when the local day changes. Device-local date —
        // the cap is a soft retention dial, not an anti-cheat boundary, so device clock
        // is fine here (the persisted job timers are the integrity-sensitive part and
        // those use the same monotonic-forward TimeSource as offline harvest).
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

        private static List<BuildJobData> Jobs
        {
            get
            {
                var s = State;
                if (s == null) return null;
                return s.BuildJobs ??= new List<BuildJobData>();
            }
        }

        private int IndexOf(string structureId)
        {
            var jobs = Jobs;
            if (jobs == null || string.IsNullOrEmpty(structureId)) return -1;
            for (int i = 0; i < jobs.Count; i++)
                if (jobs[i].StructureId == structureId) return i;
            return -1;
        }

        private static void Persist() => GameStateService.Instance?.Save();

        private static BuildTimerConfig LoadConfig()
        {
            var cfg = Resources.Load<BuildTimerConfig>(BuildTimerConfig.ResourcesPath);
            return cfg != null ? cfg : BuildTimerConfig.CreateDefault();
        }
    }
}
