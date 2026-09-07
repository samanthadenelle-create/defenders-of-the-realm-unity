# WO-1408 RESULT - the welcome-back popup grew doors, and COLLECT is no longer the only exit

**Status:** IMPLEMENTED AND SUITE-GREEN IN A PRE-COMMIT RUN. Capture and Seeker felt-verify still owed.
One ruling recorded rather than invented: a Heartfire wave-start door does not exist in canon.
**Commit:** `eb161dc98` (2026-09-06 20:10), the seven-gated-lanes commit.
**Files:** new `Assets/_Modules/Village/Harvest/UI/WelcomeBackDoorsVM.cs` (+263; the VM at `:82`, its row list
at `:89`, the second small door at `:94`, the Manage door at `:156`), `WelcomeBackPopup.cs` (+228),
`Assets/_Modules/Village/Harvest/OfflineHarvestResult.cs` (+36),
`Assets/_Modules/Village/Harvest/OfflineHarvestService.cs` (+56), new suite
`Assets/Editor/Regression/WelcomeBackDoorsRegression.cs` (+348), registered in
`Assets/Editor/Regression/DataRegression.cs:1462` as `[welcome-back-doors]`.

## What landed

`WelcomeBackDoorsVM` composes an optional row list, each row carrying a label, a `PanelId` door and a trace
kind. A finished job produces one row onto `PanelId.Manage`; a recorded attack produces one onto the Defence
Report; an army-ready window offers a second small `RAID` door beside COLLECT. An empty away window produces
zero rows, so COLLECT stands alone rather than under empty scaffolding. Every door collects first and then
routes through `PanelRouter` on the existing return-door arbiter, which keeps one path rather than two.

The ticket's third bullet asked for a `START WAVE` door on a full Heartfire. That door was NOT built, and the
reason is recorded in the VM header (`WelcomeBackDoorsVM.cs:36-40`): canon says RAID ORDERS is dead and MARCH
survives as the verb, Heartfire is the RAID charge and buys no wave, so there is no wave-start door to open.
The ready door routes to `PanelId.JourneyDeck` instead. That is a deviation from the fix shape, named here
rather than shipped silently.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. Neither red was in this lane: one was a UI-MVVM violation on `BuildPreviewModal.cs:252-253`
(the WO-1411 lane), one a hollow-pass marker at `NightMarketNoWalletRegression.cs:761` (the WO-1409 lane).
Both were fixed at source and committed in `eb161dc98` at 20:10, AFTER both gate logs, so neither log
postdates `eb161dc98` or the current working tree. The wave-two gate is owed.

## Acceptance

- [x] RED-first suite exists and PASSED in the 20:07 run: `[welcome-back-doors] WELCOME BACK DOORS OK -- a
      finished job and a recorded attack each produce ONE row with ONE door (Manage / Defence Report); an empty
      window produces none; an army-ready window offers RAID onto the Journey deck; every door collects first
      and routes through PanelRouter on the existing return-door arbiter` (`Builds/reg-quiet.log`).
- [ ] Headless: `WelcomeBack_2670x1200.png` regenerated on both fixtures and opened. Not run in this wave.
- [ ] Device: relaunch after a queued job completes offline, and confirm the row lands on Manage.

Still owed: the regression gate re-run at HEAD, `WELCOME_BACK_CAPTURE 6/6` with the frames opened, and a Seeker
capture of the popup after an offline completion. The Heartfire wave-start door is an owner ruling, not work.


---

# OWNER REVERSAL 2026-09-07 01:13 - the RAID door is retired from this popup

On her own frame (`Logs/device/screens/owner-harvest-20260907-011321.png`, build 358872) the owner
said, verbatim: **"no idea why raid is listed here"**.

The RAID door and the "Army 10 / 10 ready - Heartfire 3 / 3 lit - a camp awaits" line are REMOVED
from `WelcomeBackDoorsVM.Build`. This ticket's reasoning was sound - COLLECT did drop the player on
a HUD whose loudest control was the store card - but the PLACEMENT was wrong: the welcome-back popup
answers one question (what happened to my town, and what do I collect), and a raid invitation beside
COLLECT reads as a second competing primary. She did not recognise it as an offer at all.

**What survives, deliberately:** the ATTACKED row. That is a door onto a REPORT of something that
happened to her town, drawn only when such a report exists - not a raid invitation. This ticket's
actual invariant ("a row exists only when it is true") is unchanged.

The `ReadyLine` / `ReadyDoorText` / `ReadyDoor` fields and the four posture parameters are KEPT and
always empty, and the retired gate is recorded in prose at the removal site, so a reversal of the
reversal is a three-line edit. `WelcomeBackDoorsRegression` cases 3 and 4 are INVERTED to
`[raid-door-retired]` rather than deleted - a seat restoring this ticket's spec fails in editmode
instead of shipping it to her a second time.
