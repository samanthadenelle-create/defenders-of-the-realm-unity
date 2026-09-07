# WO-1414 RESULT - a new game claims nothing, and a fixture now says so

**Status:** IMPLEMENTED - 2026-09-07, UNCOMMITTED, awaiting gate. Edit-only lane: no Unity, no gate,
no git. Device felt-test on START NEW is OWED and is the closing evidence.

## What was ALREADY right, read at source this session (not assumed)
- `GameStateService.ResetToNewGame` zeroes the away clock (`GameStateService.cs:1304`) and clears the
  ledger (`:1354`). The ticket's own status note already corrected its premise for B.
- The LIVE halves exist: `OfflineHarvestService.OnNewGameStarted` drops any parked reveal and the
  held share; `ResourceBuildingHarvester.InstallNewGameHook` / `OnNewGameStarted`
  (`ResourceBuildingHarvester.cs:134-158`) clear the per-id owed intervals AND the cached gate
  verdicts, so the new town's first gate evaluation prints.
- **The ticket's step 1 (instrument the ANCHOR + its provenance) is DONE** -
  `OfflineClaimCoordinator.cs:281-286` computes `anchorMs` + a `provenance` word
  (`fresh` / `zero` / `resume-edge` / `state`) and `TraceWindowProvenance` (`:364-368`) prints it.
  A seat looking for this to write should read it first.

## What this lane added
**`Assets/Editor/Regression/OfflineHarvestRegression.cs` - case 6 `[new-game-claims-nothing]`.**
The fresh-save fixture the ticket's acceptance asks for, on the SAME defaults a new save is built
from: `new GameState()` must carry `LastHarvestClaimMs == 0` (the value the coordinator's
fresh-clock arm keys off, `OfflineClaimCoordinator.cs:281-293`), an EMPTY `EverBuiltStructureIds`,
and neither `farm` nor `lumbermill` ever-built - the two exact ids the owner's device logged HELD
every 10 s. Plus: an empty `OfflineHarvestResult` must NOT open the reveal gate
(`HasSummaryContent`), which is the one term the popup and `OnClaimCompleted` share, so one
assertion covers both surfaces.

RED PROOF (stated, not run - no Unity in this lane): restoring a non-zero `LastHarvestClaimMs`
default, seeding the ledger on a blank founding, or adding a term to `HasSummaryContent` that is
true at zero each fail this case. Case 1 above it already proves the coordinator's arm; case 6
proves the VALUE it reads.

## Registration line (DataRegression.cs NOT edited)
None needed - `OfflineHarvestRegression` is already registered; case 6 is inside its existing `Run`.

## Still NOT fixed, and still needing an owner ruling (unchanged by this lane)
1. **Default Town founding** marks `collector_lumbermill` / `collector_farm` ever-built while a
   `ResourceCollector` attaches only on PLACEMENT, so the baked twins are ledger ids with no
   collector. A *Default Town* new game can therefore still log HELD lines. This is a founding-path
   ruling, not a reset bug, and it is the one thing that could surprise a fresh-game felt-test.
2. **Evidence D** - the sub-minute welcome-back re-fire needs a window THRESHOLD. Not invented here.

## Gate evidence in-lane
Braces balanced (90/90), zero NUL bytes, zero non-ASCII added, LF preserved, FlowTrace kept.
