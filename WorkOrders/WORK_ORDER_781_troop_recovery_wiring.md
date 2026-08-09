# WO-781 — Wire ArmyStorage.TickRecovery (wounded troops never heal)

**Status:** DONE (reconciled 2026-08-09 from the tree - commit `cd5a059c` wired TickRecovery via TroopRecoveryService, live and offline. NOT felt-verified; no `.RESULT.md`)

> ⚠ Renumbered 779→781 on 2026-07-26: WO-779 was reassigned by the owner to the UI spacing/layout conformance sweep. The implementation agent may reference the old "779" label — the WORK is unchanged.

**Status:** SHIPPED 2026-07-27 (cd5a059c — TroopRecoveryService wires TickRecovery live + offline).
**Minted:** 2026-07-26 (CLI, from gameplay-gap ledger — borderline P0)
**Lane:** Core/State army recovery (single lane). Dispatch on the clean committed base.

## Why (evidence)
`ArmyStorage.TickRecovery(...)` exists but has **ZERO callers repo-wide** (verified in `docs/qa/GAMEPLAY_GAPS_2026-07-26.md`). Wounded troops (set on raid retreat/defeat via `ArmyStorage.ReconcileAfterRaid`) therefore NEVER recover — the army silently degrades toward unwinnable. CoC-standard: wounded/healing troops return to available after a timer. This is the "soft-loss sting, not permadeath" the raid design promises.

## Scope
1. RCA the correct advance hook (§12 — locate, don't assume): find where offline/elapsed time is applied on load (likely `GameStateService.ApplyPersisted` offline catch-up, mirroring how `BuildTimerService`/`ObsidianQueueEngine` resolve elapsed jobs) and where a live per-session tick could run. Prefer reusing the SAME elapsed-time source the queue uses so recovery and job-resolve stay consistent offline.
2. Call `ArmyStorage.TickRecovery(elapsedSeconds)` (a) on load with the offline elapsed delta, and (b) on a lightweight live cadence (e.g. once/sec or on the same tick the queue polls). Null/empty-army safe; no-op when nothing is recovering.
3. Confirm the wounded→available transition reconciles with `OwnedTroopId`/veterancy accounting (don't resurrect a troop twice; don't heal a troop that was permanently lost if any such state exists).

## Acceptance (data-verified)
- EditMode oracle: seed a wounded troop with `recoverAt` in the past → run the load/offline-resolve path → assert it's available again; seed one with `recoverAt` in the future → assert still wounded; advance elapsed past it → assert recovers. Wire a marker (e.g. `TROOP_RECOVERY_OK`) into `DataRegression.RunAll`.
- Assert `TickRecovery` now has a live caller (a reachability check, like the queue-toggle gap).

## Do NOT touch
- The raid casualty/reconcile RULES (`ReconcileAfterRaid` — correct); only ADD the recovery advance.
- The WO-773 queue engine internals; reuse its elapsed-time source, don't fork it.
- WO-771.6 stakes (casualties-on-victory) — separate.
