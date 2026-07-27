# WORK ORDER 773 — Common "Obsidian" Job Queue (unified timed-work system)

**Status:** SPEC. The single queue every timed job flows through — **buildings, repairs,
upgrades, tier-unlocks, magic-learning, troop training, towers, everything.**
**Date:** 2026-07-26
**Author:** systems design pass.
**Owner ask (2026-07-26):** "design a common obsidian so buildings, upgrades, troops,
towers, everything can flow through it as needed."

> **Naming note (flag for the owner — do NOT rename in code).** "Obsidian" is overloaded across the
> project: (1) the **Blink UI pack**, (2) a **wall tier** (Stone/Obsidian in raid base configs), and
> (3) **this job queue** (`ObsidianQueueService`). Recommendation: keep `ObsidianQueueService` as the
> **internal** name, but use a **player-facing "Builders" / "Training queue"** label in any HUD/UI so
> players never see the overloaded term. This is an owner call — no code rename without the owner's
> ruling. (Same note carried in `docs/RAID_NORTHSTAR.md` §4.)

## Context — what exists here vs the target
This branch has only an **ad-hoc** timed-build substrate: `GameState.BuildingCooldowns`
(`SerializableDict<string,double>`, `GameState.cs:78`) and `GameState.PendingBuilds`
(`List<PendingTowerBuild>`, `:80`). There is **no** `BuildTimerService`, no queue, no
slot model, no queue HUD in this tree (grep-confirmed). CLI's more-advanced tree has a
2-slot `BuildTimerService` (reject-when-full, no queue, no dynamic slots) and a banked
spec (their "WO-762") for a 4-slot Obsidian queue. **This work order is the concrete,
implementable design of that common queue for this codebase** — one system that
supersedes the ad-hoc timers and that raids/dungeons/village all enqueue into.

## Model — "Obsidian" = the concurrent-worker + shared-queue system (COC builder-huts analog)

- **Obsidian slots** = concurrent workers. `SlotCount` grows: N at start, +1 at account
  L10, +1 at L20, plus buyable slots (premium currency). A slot runs **one** job at a time.
- **One shared FIFO pending queue.** When a job is enqueued: if a slot is free it **starts
  immediately**; else it lands at the tail of `PendingQueue`. On a job's completion the
  freed slot **auto-pulls** the next pending job. This is the "put it in and it auto-flows"
  behavior across every job type.
- **Offline-fair.** Timing is wall-clock; on load the service resolves all jobs whose
  `startedAtUnix + durationSeconds ≤ now`, applies effects, and cascades auto-pulls until no
  further completions — mirroring the existing offline-fair build/wallet handling.

## Data model (new — `_Modules/Core/Jobs/`, persisted in `GameState`)

```
enum JobKind { Build, Repair, Upgrade, UnlockTier, LearnMagic, TrainTroop, TowerBuild,
               TowerUpgrade, WallUpgrade /* extensible — add without touching the queue */ }

[Serializable] class ObsidianJob {
  string   Id;
  JobKind  Kind;
  string   TargetId;          // building/tower/troop/tier id the effect applies to
  long     StartedAtUnix;     // 0 while still pending
  int      DurationSeconds;
  string   PayloadJson;       // kind-specific extra data (e.g. troopId, targetTier)
}

[Serializable] class ObsidianQueueState {
  int              SlotCount;         // derived: unlocked-by-level + bought
  int              BoughtSlots;       // persisted purchases
  List<ObsidianJob> ActiveJobs;       // length ≤ SlotCount
  List<ObsidianJob> PendingQueue;     // FIFO
}
```
`ObsidianQueueState` lands in `GameState` via the **WO-771.1b consolidated migration**
(one schema bump), and `PendingTowerBuild`/`BuildingCooldowns` are migrated into
`ObsidianJob`s (kind `TowerBuild`) so nothing is lost.

## Service + effect handlers (the "everything flows through it" seam)

`ObsidianQueueService` (Core):
- `Enqueue(JobKind, targetId, durationSeconds, payload) → ObsidianJob` — start-now-or-queue.
- `Resolve(nowUnix)` — offline-fair completion + cascade auto-pull; idempotent.
- `SlotCount` from account level (start/L10/L20) + `BoughtSlots`; `BuySlot()` (premium currency).
- `Cancel(jobId)` (refund policy), `Reorder(jobId, index)` (pending only), optional `SpeedUp(jobId)`.
- Raises `QueueChanged` for the HUD.

**Effect strategy** — each `JobKind` has an `IJobEffect` handler applied on completion, so
systems own their *effect* and the queue owns *timing/slots/persistence*:
- `TrainTroopEffect` → increment `GameState.TroopRoster` (**replaces WO-771.8 §4's inline timer**).
- `UpgradeEffect`/`TowerUpgradeEffect` → bump tier (**replaces WO-771.9's inline `BuildingCooldowns` use**).
- `BuildEffect`/`RepairEffect`/`WallUpgradeEffect`/`UnlockTierEffect`/`LearnMagicEffect` →
  village building/tower/wall/tier/magic systems.
Adding a new job type = a new `JobKind` + handler; the queue, HUD, persistence, and
offline-fairness are untouched.

## HUD — the "Obsidian bar" (UI Toolkit)

Shows each slot (active job icon + countdown + progress ring), the pending queue (FIFO,
with cancel + drag-reorder), and a **buy-slot** button. Subscribes to `QueueChanged`. Lives
in the village HUD; a compact variant can surface in the raid/deploy screens.

## Integration — route existing/new timers through the queue
- **WO-771.8 (troop training):** replace the inline `BuildingCooldowns`/`PendingBuilds` timer
  with `ObsidianQueueService.Enqueue(TrainTroop, troopId, dur, …)`.
- **WO-771.9 (Barracks/troop upgrades):** enqueue `Upgrade`/`TowerUpgrade` jobs instead of a
  private timer.
- **Village towers/walls/buildings:** migrate `PendingBuilds` → `Enqueue(TowerBuild/…)`.
- **WO-772/dungeon:** any timed unlock (tiers, magic) enqueues here too.

## Acceptance

1. Every timed action (build/repair/upgrade/unlock-tier/learn-magic/train-troop/tower) is
   created via `ObsidianQueueService.Enqueue` — no system starts a private timer.
2. A free slot starts a job immediately; a full slot queues it; on completion the next
   pending job auto-pulls into the freed slot (unit test the state machine).
3. **Offline-fair:** save with jobs mid-flight, advance the clock, load → the correct jobs
   complete and cascade, effects applied exactly once (test).
4. `SlotCount` = start + L10 + L20 unlocks + `BoughtSlots`; `BuySlot` spends premium currency
   and persists.
5. The Obsidian bar shows slots + queue; cancel and reorder mutate `PendingQueue` correctly.
6. Migration converts existing `PendingBuilds`/`BuildingCooldowns` into `ObsidianJob`s with no
   lost progress; state round-trips through save/load.
7. WO-771.8 and WO-771.9 route through the queue (no separate timer code remains).
8. `WORK_ORDER_773_*.RESULT.md`.

## Key files
`_Modules/Core/Jobs/ObsidianQueueService.cs`, `ObsidianJob.cs`, `ObsidianQueueState.cs`,
`IJobEffect.cs` + per-kind handlers; `GameState.cs`/`SaveMigrator.cs` (via WO-771.1b);
`_Modules/*/UI/ObsidianBar.uxml/.uss` + controller. Migrates from `GameState.cs:78,80`
(`BuildingCooldowns`/`PendingBuilds`). Consumed by WO-771.8, WO-771.9, village build/tower/wall.

> **Cross-tree note:** if CLI's `BuildTimerService`/WO-762 lands in this branch first, this
> WO becomes "generalize `BuildTimerService` into the slotted Obsidian queue + handlers +
> HUD" rather than a from-scratch build — the data model and acceptance above still hold.
