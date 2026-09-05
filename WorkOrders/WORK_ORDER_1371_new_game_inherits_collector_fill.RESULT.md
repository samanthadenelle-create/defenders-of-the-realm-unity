# RESULT - WORK ORDER 1371 - A NEW GAME inherits the previous save's collector fill: 14,089 free resources

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block + the `Diagnosis 2026-09-04 (read-only)` section another seat appended tonight)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).
**Caveat:** the zero-fill vs seeded starting value is the OWNER's ruling and was never asked on record - see Gaps.

## What shipped

- Commit `f6540db88` (2026-09-04 12:47) - ancestor of HEAD and of `32af7767c` (base of build 2026.09.05.355872).
  Body: "WO-1371 - a new game inherited 14,089 resources because collector fill lives in PlayerPrefs OUTSIDE the
  save envelope. ClearHarvestPrefs added; ResourceBuildingState.ResetAll() finally has a caller - its first ever...
  The 13+ other out-of-envelope stores the RCA found are recorded as ledger rows with reasons rather than silently fixed."
- Prefs half: `Assets/_Modules/Core/State/GameStateService.cs:1296` `ClearHarvestPrefs(); // WO-1371`; `:1443-1465`
  the ONE authority for the three collector PlayerPrefs prefixes (`:1452 CollectorPendingPrefPrefix =
  "dotr.collector.pending."`, `:1453 ...hp.`, `:1454 ...lastaccrual.`) plus the building-id index; body `:1567-1592`
  deletes them for every id in `KnownCollectorIds()` (`:1500-1529`).
- Live half: `Assets/_Modules/Village/Buildings/Progression/ResourceCollector.cs:22-26` aliases the prefixes;
  `:747-793` `OnNewGameStarted` -> `ResetForNewGame` (`_pending = 0` at `:784`, HP full, stamp = now, `SaveState`)
  on every collector, then `ResourceBuildingState.ResetAll()`.
- Storage question (WO item 1) answered: PlayerPrefs, keyed by building id (`farm` / `lumbermill` / `forge`),
  outside the `dotr-save` envelope - which is exactly why the fill outlived `LastHarvestClaimMs = 0`.

## Suites that pin it

- `[newgame-pref-sweep]` (`Assets/Editor/Regression/NewGamePrefStoreSweepRegression.cs`: `:12` cites this WO, `:43`
  "RED against the pre-WO-1371 tree", `:52` markers `NEWGAME_PREF_SWEEP_OK/FAIL`). Registered `DataRegression.cs:667`
  beside `reset-full-clear` (`:663-664`, the GameState-FIELDS axis).
- `Builds/regression.log` (2026-09-04 22:44) line 113715: `REGRESSION_OK 377/377 suites`; the diagnosis section
  quotes `[newgame-pref-sweep] NEW GAME PREF SWEEP OK ... [notes: 16 KNOWN GAP(s) ...]` from the same run.

## Device build evidence

- Build 2026.09.05.355872 on the Seeker (installed 22:22); base `32af7767c` has `f6540db88` as an ancestor.
- The 22:29 welcome-back popup (`collectorsPending=16716 across 3 collector(s)`, `window 0s`) is NOT evidence of a
  defect on the fixed build: 16716 = 7500 + 5760 + 3456 (the three caps, identical to the pre-fix 07:42 figure at
  `freeze-20260904-095249.log:379210`), and `window 0s` is what every cold load reports (the resume claim seeds the
  clock first). It is the reading that no START NEW had been pressed on the fixed build yet.
- No post-fix device capture quoting `pending=0` exists.

## Owner felt-test (verbatim from the appended diagnosis)

1. On build 355872+ tap **START NEW**.
2. Read logcat for `ResetToNewGame: cleared N stale harvest PlayerPrefs key(s) across M collector id(s)`
   (`GameStateService.cs:1587-1591`) followed by `New Game: zeroed N live collector(s)` (`ResourceCollector.cs:771-774`).
3. The first `collector status ->` after `ResetToNewGame: EXIT` must read `pending=0`.
4. If `across 0 collector id(s)` appears, that is the residual hole (`KnownCollectorIds` on a pre-index device -
   the catalog-union path, which the oracle's Case 3 does not exercise).
5. Play a minute in the new town and confirm the collectors start empty, not at 7500 / 5760 / 3456.

## Gaps the RCA block names

- **Owner ruling open:** zero fill vs a seeded founding fill on New Game. The commit chose CLEAR (zero); the owner
  was not asked on record. The number lives in exactly one line: `ResourceCollector.cs:784` `_pending = 0.0;`
  (an absent pref key already reads as 0 at `:676`). Confirm or re-rule.
- Oracle gaps (observations): Case 3 proves the index path only, never the catalog-union path; Case 5 is a
  source-text grep, not a live `ResourceCollector` through the reset; `ResetForNewGame` writes stamp = now
  (`:787`) while the ledger row `GameStateService.cs:1653-1655` says "deleted, not stamped". The diagnosis section
  of the WO spells out a Case 7 `[live-collector-zero]` the CLI can add.
- The 16 `NotYetCleared` pref stores (`GameStateService.cs:1680-1722`, including `dotr-harvest-last-active`) remain
  inherited by design of that pass; each is a candidate ticket, not part of this one.
