# WORK ORDER 172 — RESULT (Phase 1: standalone timer service + ad-speedup seam)

**Status:** CODE COMPLETE (brace/paren-gated) — pending CLI build-verify.
**Date:** 2026-05-31
**Lane:** economy/build (NO VillageSceneBuilder, NO Village.unity, NO bake).

---

## What was built

A **standalone, persisted, offline-counting** build/upgrade timer service with a
rewarded-ad + premium speedup seam — decoupled from WO-108 (player build mode, NOT
built yet), which attaches to it later at one flagged call site.

### Files created
- `Assets/_Modules/Core/State/BuildJobData.cs` — persisted job struct
  `{ structureId, jobType(build/upgrade), startMs, durationMs }`, `FinishMs` = start+duration.
  Unix-ms clock (same unit as `LastInboxSyncAt`/`LastHarvestClaimMs`/`PendingTowerBuild.FinishAt`).
- `Assets/_Modules/Core/Catalog/BuildTimerConfig.cs` — tunable SO (no magic numbers in logic):
  hybrid duration curve (`DurationSecondsForTier` = `base * pow(growth, tier)`, upgrade multiplier,
  hard cap), ad-skip chunk + daily cap, instant-finish crystal price, free build-slot count.
  `CreateDefault()` code-fallback so it runs with zero asset authoring.
- `Assets/_Modules/Village/Buildings/BuildTimerService.cs` — the service: `StartBuild`/`StartUpgrade`,
  `RemainingSeconds`/`Progress`/`IsBuilding`/`ActiveJobs`, `WatchAdToSkip`, `TryInstantFinish`,
  `CompleteJob`/`CancelJob`. Self-bootstrapping singleton (mirrors OfflineHarvestService).

### Files edited (additive persistence round-trip, schema v12 → v13)
- `Assets/_Modules/Core/State/GameState.cs` — added `BuildJobs` (List), `AdSkipsUsedToday`,
  `AdSkipDayKey` at the END (older saves stay loadable).
- `Assets/_Modules/Core/State/SaveSchema.cs` — `CurrentVersion` 12→13; nullable `buildJobs` /
  `adSkipsUsedToday` / `adSkipDayKey` on `PersistedState`; finiteInt/nonNegInt validation.
  **Additive-default-on-read — NO migration step** (same pattern as v12's `lastHarvestClaimMs`).
- `Assets/_Modules/Core/State/GameStateService.cs` — `Snapshot()` + `ApplyPersisted()` round-trip
  the 3 fields; `Reset()` clears them.

---

## WO-108 integration point (flagged)

In `BuildTimerService.cs`, marked `★ WO-108 INTEGRATION POINT ★`:
When build-mode lands, `BuildModeController.ConfirmPlace` (AFTER the WO-131 wallet charge) calls
`BuildTimerService.Instance?.StartBuild(structureId, tier)` and treats the structure as
under-construction (scaffold + countdown bar) until `JobCompleted` fires for that id, then reveals
it. WO-151 upgrades call `StartUpgrade(structureId, targetTier)` identically. `structureId` =
the placed structure's unique id (PlacedStructureData key). The service references no WO-108 types,
so it compiles + ships standalone today.

---

## Ad-stub reconciliation (no greenfield)

Reused the existing **`RewardedAdManager`** (DeNelle.Village, DEF-69) — `TryShowAd(Action onReward)`
with its built-in cooldown + virtual `ShowAdInternal` SDK seam. `WatchAdToSkip` calls it and applies
the skip in the reward callback (genuine completion only). A per-day cap (`adSkipsPerDay`) layers on
top of the manager's per-view cooldown. **No new ad provider was created.** Crystal instant-finish
spends through the single GameState wallet via `GameStateService.AddCrystals` (WO-131), never a
second balance.

---

## Persistence approach

Jobs live in `GameState.BuildJobs` and round-trip through the standard SaveSchema layer (PlayerPrefs
`dotr-save`). The clock is `TimeSource.NowUnixMs()` (the WO-115 seam — swappable to server time later
with no math change), so **timers count down across app close / offline**: on `Start()` the service
sweeps any job whose `FinishMs` already passed and completes it (offline-fair catch-up); while open,
a ~1 Hz `Update` sweep flips finished jobs without waiting for the next load. Skips pull `StartMs`
back (remaining shrinks). Negative deltas are inherently safe (a job only ever completes EARLIER).

---

## Design decisions (the WO's open questions — defaulted, owner-tunable)

- **Build slots:** 2 free, scarcity-style (CoC). Tunable `freeBuildSlots`; extra slots = future unlock.
- **Ad-skip:** fixed 15-min chunk, 10/day cap. Tunable `adSkipSeconds` / `adSkipsPerDay` (0 = unlimited).
- **Scope:** buildings + upgrades now; the job model is generic (`structureId` + `jobType`) so
  crafting/refining can reuse it later with no shape change.

---

## Risks / notes

- **Under-construction VISUAL + finish flourish (AC #2):** deferred to WO-108 — the visual state lives
  on the placed structure, which doesn't exist until build-mode does. The service raises
  `JobStarted`/`JobCompleted`/`JobSkipped` events for that layer to drive scaffold/bar/VFX/SFX. The
  TIMER half (persisted, offline, skip) is complete now.
- **Store-build-only ad gating:** `RewardedAdManager` is the single ad gate; the two-build (store vs
  crypto) split is enforced where that manager is compiled/stubbed, not re-implemented here.
- **No bake, no scene edits, no commit/build** (per constraints). `BuildTimerService` self-bootstraps,
  so no Village.unity authoring is required.
- `BuildTimerConfig` asset path `Resources/Economy/BuildTimerConfig` is optional — owner can author
  one to retune; code defaults stand otherwise.

---

## Test steps (CLI / editor)

1. Compile (verify v13 round-trip + no dup types).
2. Play, call `BuildTimerService.Instance.StartBuild("test-1", 0)` (15s default) → `IsBuilding`
   true, `RemainingSeconds` counts down, `JobCompleted` fires at ~15s.
3. Start a long job (tier 3), quit + relaunch within the duration → job still in flight, remaining
   reduced by the offline gap; let it pass finish offline → completes on next load.
4. `WatchAdToSkip("...")` → stub ad grants, remaining drops by 15 min, daily counter increments;
   exceed `adSkipsPerDay` → `CanWatchAdToSkip` false.
5. `TryInstantFinish("...")` with enough crystals → job completes, crystals deducted via the one wallet.

## Done checklist
- [x] Persisted real-time build/upgrade timers (offline countdown)
- [x] Hybrid tunable duration curve (SO); build-slot concurrency (2 free)
- [x] Watch-ad skip (opt-in, daily-capped) via the existing RewardedAdManager seam; never a wall;
      premium crystal instant-finish alongside
- [x] Standalone — WO-108 placement + WO-151 upgrade call sites flagged; generic for crafting later
- [x] Brace/paren balance verified on every `.cs` touched; Village → Core only; single-wallet spend
- [ ] Under-construction visual + finish flourish — deferred to WO-108 (event seam provided)
