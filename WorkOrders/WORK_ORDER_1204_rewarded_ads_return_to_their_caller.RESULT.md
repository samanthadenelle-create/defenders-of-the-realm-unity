# WORK ORDER 1204 - RESULT: rewarded ads preserve their caller

**Status:** DONE - CLOSED BY THE OWNER 2026-08-25 on a Seeker felt-test of build `2026.08.25.341262`. Owner verbatim: *"ad plays"* then *"good and clsoed to screen was loaded from"* - the rewarded ad returned to its invoking screen, which is this ticket's entire acceptance. ⭐ Closing it surfaced a gap in its OWN oracle and that gap is now closed too: `AdGateAndArenaReturnRegression` proved only the SUPPRESSION half (an ad does not open Pause) and asserted NOTHING about suppression ENDING, so a leaked scope would have silently retired ordinary backgrounding's Pause - strictly worse than the defect this ticket fixed - while the suite stayed green. The inverse is now pinned (no scope active -> backgrounding MUST auto-open Pause), verified `AD_GATE_ARENA_OK` inside `REGRESSION_OK 283/283`. A refusal test is not acceptance. *(Prior line:)* **Status:** IMPLEMENTED 2026-08-25 - code and automated gates complete; awaiting owner Seeker felt-test.
**Code commit:** `76ba97a9ded97c0853ff6fed40dcc30eb6caefcd`

## Owner report

"After an ad plays, the game returns to Pause/Settings instead of the screen/context that invoked the ad."

## Root cause

LevelPlay's native Android full-screen activity caused Unity to call
`PauseController.OnApplicationPause(true)`. That method unconditionally opened Pause through
`PanelManager`, swap-closing the actual invoking panel. The ad callbacks themselves did not route to
Settings; the forced application-pause path replaced the caller before the ad closed.

## Landed

- `Assets/_Modules/Core/UI/PauseGate.cs` - scoped external-presentation suppression with no navigation ownership.
- `Assets/_Modules/Settings/PauseController.cs` - application backgrounding opens Pause only when the external-presentation scope is inactive.
- `Assets/_Modules/Village/Monetization/Providers/LevelPlayInitializer.cs` - scope acquired immediately before `ShowAd()` and released across close, display failure, synchronous show failure, and destroy.
- `Assets/Editor/Regression/AdGateAndArenaReturnRegression.cs` - pins Daily Chest and Manage as two distinct callers that remain open through native presentation.
- `docs/MASTER_CATALOG/core.md` and `docs/MASTER_CATALOG/devtools-settings-onboarding.md` - record the navigation and pause contract.

Reward, dismiss, unavailable, and completion callback semantics remain with their existing LevelPlay
callbacks. The new scope does not reopen any panel, so `PanelManager` remains the sole navigation
authority.

## Fresh evidence

- `COMPILE_GATE_OK` - fresh Unity 6000.4.8f1 compile log, 2026-08-25 13:51:49.
- `AD_GATE_ARENA_OK` - fresh focused oracle log, 2026-08-25 13:52:35.
- Focused proof: Daily Chest and Manage remain their own callers across the native presentation.
- `git diff --check` was clean before landing.

## Still open - owner felt-test

- [ ] On the Seeker, play a rewarded ad from Daily Chest and confirm it returns to Daily Chest.
- [ ] On the Seeker, play a rewarded ad from Manage or another non-Settings caller and confirm it returns to that caller.
- [ ] Confirm earned, dismissed, and unavailable outcomes still feel correct on device.

No further code action is presently identified. Do not mark owner-felt-closed until the device run is reported.
