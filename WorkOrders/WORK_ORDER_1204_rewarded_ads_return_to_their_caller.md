# WORK ORDER 1204 - rewarded ads return to their caller

**Status:** DONE - CLOSED BY THE OWNER 2026-08-25 on a Seeker felt-test of build `2026.08.25.341262`. Owner verbatim: *"ad plays"* then *"good and clsoed to screen was loaded from"* - the rewarded ad returned to its invoking screen, which is this ticket's entire acceptance. ⭐ Closing it surfaced a gap in its OWN oracle and that gap is now closed too: `AdGateAndArenaReturnRegression` proved only the SUPPRESSION half (an ad does not open Pause) and asserted NOTHING about suppression ENDING, so a leaked scope would have silently retired ordinary backgrounding's Pause - strictly worse than the defect this ticket fixed - while the suite stayed green. The inverse is now pinned (no scope active -> backgrounding MUST auto-open Pause), verified `AD_GATE_ARENA_OK` inside `REGRESSION_OK 283/283`. A refusal test is not acceptance. *(Prior line:)* **Status:** IMPLEMENTED 2026-08-25 in `76ba97a9d` - compile and focused oracle green; awaiting owner Seeker felt-test from at least two invoking screens.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1204 -> 1205 in the same edit)
**Silo:** Monetization / navigation
**Origin:** Owner-reported live behavior on 2026-08-25: "after an ad plays, the game returns to Pause/Settings instead of the screen/context that invoked the ad."

## Defect

A rewarded ad launches a native Android full-screen activity. Unity reports that transition through
`OnApplicationPause(true)`. `PauseController` treated every such background transition as a reason
to call `Pause()`, and `Pause()` opened the Pause panel through `PanelManager`. Because
`PanelManager` is the existing single navigation authority, opening Pause swap-closed whichever
screen actually invoked the ad. Settings was therefore not the caller contract; it was one visible
form of the incorrect forced destination.

## Implemented contract

Commit `76ba97a9ded97c0853ff6fed40dcc30eb6caefcd`:

1. `PauseGate.BeginExternalPresentation(...)` creates a scoped suppression around the native
   rewarded-ad presentation.
2. `PauseController.OnApplicationPause` auto-pauses only when no external presentation is active.
3. `LevelPlayInitializer` acquires the scope immediately before `ShowAd()` and releases it on
   display failure, close, synchronous `ShowAd` failure, or provider destruction.
4. The scope never reopens a panel and owns no return destination. The caller's existing
   `PanelManager` handle remains the sole navigation authority.
5. Existing completion authority is preserved: reward comes from the rewarded callback; close
   settles dismissed only when not already settled; display/show failure settles unavailable.

## Acceptance

- [x] Source root cause traced through the real LevelPlay entry, completion, close, and failure flow.
- [x] Native-presentation suppression is acquired before `ShowAd()` and released on every terminal path.
- [x] No second navigation or caller-reopen authority introduced.
- [x] Focused regression pins two distinct callers: Daily Chest and Manage.
- [x] Fresh `COMPILE_GATE_OK`.
- [x] Fresh `AD_GATE_ARENA_OK`.
- [ ] Owner Seeker felt-test: invoke a rewarded ad from Daily Chest and confirm return to Daily Chest.
- [ ] Owner Seeker felt-test: invoke a rewarded ad from Manage (or another non-Settings caller) and confirm return to that caller.

## Result

See `WorkOrders/WORK_ORDER_1204_rewarded_ads_return_to_their_caller.RESULT.md`.
