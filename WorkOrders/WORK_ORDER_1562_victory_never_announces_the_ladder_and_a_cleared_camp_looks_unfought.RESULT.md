# WO-1562 RESULT - the ladder is announced, and a cleared camp says so

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane; no Unity run, no commit.
Line numbers re-read at source after the last edit (CLAUDE.md 11B).

## PART 1 - THE UNLOCK ANNOUNCEMENT

`RaidVictoryController.ResolveUnlockLine`
(`Assets/_Modules/Village/World/Camps/RaidVictoryController.cs:831`) no longer returns `null`
unconditionally. It calls `RaidSelectionVM.UnlockAnnouncementFor`
(`Assets/_Modules/Village/Hero/RaidSelectionVM.cs:539` -> `CampUnlockedAt` `:515`), which walks the
SAME `FlagshipRaidIds` + authored `unlockVictories` pair `ResolveLock` and `NextLockedCamp` already
consult. **One ladder, no second copy of the thresholds** (acceptance 2). The retired comment's
reasoning is honoured, not overturned: nothing is named in the victory file.

**"Crossed on THIS win" is exact.** `RecordVictory` increments `GameState.RaidVictories` by one per
settled win from the one `_handled` latch, and the counter is monotonic - so a count equals any given
threshold on exactly one win, ever. A **repeat clear still increments** (the counter is wins, not
claims) and still cannot re-announce, because it lands on a count already passed. Reasoning at
`RaidSelectionVM.cs:522-531`; no previous-count parameter is taken, because an extra input is one
more thing to keep in sync. **Both branches traced**, so a crossing stays distinguishable in a capture
from a non-crossing - the property `:810-812` was written to preserve. Guarded throughout.

## PART 2 - THE CLEARED MARKER

`RaidSelectionVM.ClaimedProvider` (`:144`) is wired **once**, in `RaidSelectionScreen.OpenInternal`
(`Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:474`), to `RaidClaimService.IsClaimed` - **never
a second claim predicate**. Resolved per row in `Rebuild` (`:776`), guarded; a fault or an unwired
provider reads NOT CLEARED, the forgiving direction. `ClearedWordFor` (`:430`) / `ClearedWord` (`:434`)
compose `CLEARED - repeats pay 25%`, and **the percentage is FORMATTED from
`RaidClaimService.RepeatClearLootMultiplier`, never typed** - the same constant `ApplyFirstClearGate`
pays through, so the row can never advertise a rate the settle does not pay (disclosure only).

### JUDGEMENT CALL, RECORDED RATHER THAN MADE SILENTLY

**The marker takes the CLOCK band's left column** (`RaidSelectionScreen.cs:945-961`), per this
ticket's own sanction. Every row reads the identical `Clock: 3:00` while difficulty, walls, defenders
and spoils all vary, so it is the one band that differentiates nothing; the clock returns the moment
the camp is un-cleared. **Words, not a tint or a glyph** - unchanged in greyscale. Swept
`Assets/Editor` for a pin on `"Clock` first: the only hit is an unrelated `HeartfireRegression`
message, so nothing reds.

## CONTRADICTION FOUND - reported, not resolved

This ticket and WO-1534 A6 both state the repeat rate is **60%** (attributed to WO-1461). The live
constant read **`RepeatClearLootMultiplier = 0.25f`** at
`Assets/_Modules/Village/World/Camps/RaidClaimService.cs:78` on 2026-09-06. **No number was typed
anywhere**, precisely so this row follows the constant whichever way WO-1461 lands. Recorded in-code
at `RaidSelectionVM.cs:425`.

## ORACLES - no `DataRegression.cs` edit; suite already registered

In `Assets/Editor/Regression/RaidSelectionSpoilsRegression.cs`:
- `CheckClearedMarker` (`:374`) - nothing claimed / provider unwired -> no marker anywhere; one camp
  claimed -> exactly that camp carries CLEARED with the **live** percentage read off the constant.
- `CheckUnlockAnnouncement` (`:423`) - seven non-crossing counts stay silent; a count landing on a rung
  the live catalog authors announces that camp with the one authored prefix. **`rungsChecked` (`:443`)
  fails the case at zero**, because every rung is skipped when the catalog does not resolve and a
  vacuous green would certify the very seam that was orphaned for a release.
- Case E source-lint (`:590`) - the screen must read `_vm.ClearedWordFor(` and must wire
  `ClaimedProvider`: "the model composed it and the renderer discarded it" is a defect this repo has
  already shipped once (WO-1534 B2).

## OWED

- `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs, judged by the marker.
- **RED-before-green owed, not claimed** - both cases cannot compile against the pre-change tree
  (`ClearedWordFor`, `UnlockAnnouncementFor`, the `claimed` ctor argument did not exist).
- Fresh captures: the victory screen with an unlock line, and the grid with a cleared camp.
- Owner felt-verify + close.

**Scope note:** this ticket's "leave the `LOCKED - needs Army N` word alone - WO-1542 blocked" line is
**superseded**; WO-1542 was ruled the same day and landed in this batch. Different bands, no collision.
