# WORK ORDER 612 — Wire BuildTimerService into placement (owner-ratified 2026-07-06)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**WO number 612 PROVISIONAL** (authority = MASTER_PIPELINES_BACKLOG; confirm on mint).
**Lane:** Village / BuildMode (file-disjoint from HUD + world lanes).

## Owner decision (2026-07-06, BINDING)
Build timers = **option 2 now, option 3 later**: wire the existing WO-172 `BuildTimerService`
into placement with SHORT timers (code-default config: 15 s first build, 2 free slots), and the
growth path to monetized timers is **"free income, no real cost to user"** — rewarded-ad skips
(player pays attention, ad revenue = income), crystal instant-finish stays a convenience. The
timer ALWAYS completes on its own (NORTH_STAR ad discipline — already encoded in the service).
**No monetization UI surfaces in this WO** — the ad/crystal hooks stay dormant switches.

## What exists (reuse, DO NOT rebuild)
- `BuildTimerService` (WO-172): persisted offline-fair jobs (`GameState.BuildJobs`), events
  (`JobStarted/JobCompleted/JobSkipped`), ad-skip + instant-finish, self-bootstrapping singleton.
- `BuildTimerConfig`: code-default (no asset) = base 15 s, tierGrowth 3.0, upgrade ×1.25,
  freeBuildSlots 2, adSkip 15 min ×10/day, instant-finish 1 crystal/min (min 5).
- The documented "WO-108 INTEGRATION POINT" comment in the service names the exact seam.

## Changes
1. `FeatureFlags.BuildTimers` — `ff.buildtimers`, **default ON** (owner picked the behavior;
   flag = the off-switch).
2. `BuildModeController.Place()` — after the WO-131 charge + BaseLayout append:
   `StartBuild(key, 0)`; on a real job, attach `UnderConstructionVisual`. **Null job (slots
   full / service absent) = instant completion — placement is NEVER blocked** (degrade, don't wall).
3. NEW `UnderConstructionVisual.cs` (BuildMode/): scaffold state = dimmed renderers +
   `DefenseTower` disabled (no firing mid-construction) + world-space countdown text; reveals on
   `JobCompleted`; self-heals via `IsBuilding()` poll (covers offline sweep + missed events).
   `KeyFor(PlacedStructureData)` = the job key (`itemId@x_z`).
4. `BaseLayoutLoader.Spawn()` — on load, a structure whose key `IsBuilding()` re-arms its
   scaffold (offline-fair: the service sweep has already completed overdue jobs before this).

## Scope cuts (deliberate, polish later)
- **Upgrades stay instant** (deferred-upgrade jobs touch the save/apply flow; placement timers
  deliver the felt rhythm first).
- No build-queue UI, no slot-purchase, no ad button surfacing.

## Acceptance
- [ ] Place a tower → dimmed + countdown ~15 s → pops to full color and starts firing.
- [ ] Save/quit mid-build → relaunch → timer continued (or completed) offline-fair.
- [ ] Third simultaneous placement (slots=2) completes instantly, never blocks.
- [ ] `ff.buildtimers=0` restores instant builds.
- [ ] `COMPILE_GATE_OK`, brace/NUL clean, `[Flow:Build]` traces on start/reveal.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
