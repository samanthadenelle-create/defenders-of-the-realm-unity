# WORK ORDER 612 — RESULT (2026-07-07)

**Status: IMPLEMENTED + HEADLESS-VERIFIED. Committed `3c75e8cb`, pushed (owner-authorized).**

## Delivered
- `FeatureFlags.BuildTimers` (`ff.buildtimers`, default ON).
- `BuildModeController.Place()` → `BuildTimerService.StartBuild(key, 0)` post-charge (the service's
  own documented WO-108 seam); null job (slots full / service absent) = instant, never blocks.
- NEW `UnderConstructionVisual` — dimmed renderers + DefenseTower disabled + world-space gold
  countdown; reveals on `JobCompleted`; self-heals via `IsBuilding()` poll (event-order-proof).
- `BaseLayoutLoader.Spawn()` re-arms the scaffold on load for genuinely unfinished jobs
  (service's offline-fair sweep completes overdue ones first).
- Job key = `itemId@cellX_cellZ` (`UnderConstructionVisual.KeyFor`).

## Verified
- `COMPILE_GATE_OK` + `REGRESSION_OK` + 4-bot fleet green (no new errors vs the pre-existing three).
- Brace/NUL clean on all touched files. Felt-pass = owner demo session (pending close).

## Scope cuts (as specced)
Upgrades instant; no queue UI; ad/crystal hooks dormant. Growth path (owner-ratified): option-3
"free income" — rewarded-ad skip revenue, timer always completes, never a wall.
