# WO-1499 RESULT - the header suffix names the away window, and the pin that defended the defect moved

**Status:** IMPLEMENTED, uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
Capture and Seeker felt-verify still owed.
**Commit:** none. `Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs` and
`Assets/Editor/Regression/AwaySummaryReportRegression.cs` are both `M` in `git status`;
`git log -S"AWAY LIMIT REACHED"` on the popup returns no commit, and the working diff carries the string twice.
**Files:** `Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs:696` - `AwayTextFor` now returns
`YOUR REALM WORKED FOR {span} (AWAY LIMIT REACHED)` on `wasCapped`, with the reasoning at `:186-197` and the
delegation from `AwayText` at `:678`. `Assets/Editor/Regression/AwaySummaryReportRegression.cs:243-260` -
case8's pin re-pointed, and `:260` adds a case that fails if `AwayText` ever stops delegating to `AwayTextFor`.

## What landed

The ticket's diagnosis is confirmed at source. `WasCapped` is `window.ExceedsCap(OfflineCapHours)`
(`OfflineHarvestService:385`), the 10-hour AWAY WINDOW ceiling, which says nothing about the bank. The header
suffix read `(STORAGE FULL)` off that same bit and now reads `(AWAY LIMIT REACHED)`.

The suffix is not deleted, per the ticket's constraint - the player still learns a ceiling bit, and now learns
which one. The pin at `AwaySummaryReportRegression.cs:243` moved onto the corrected wording with the reason
recorded in the suite, so the suite no longer defends the defect it was written to catch.

The genuinely-full state remains distinct and is not merged into this wording: the per-resource return row
carries its own destiny string `STORAGE FULL - STAYS PUT`, composed by
`OfflineHarvestService.ReturnRowDestiny` (`:887-890`) when `r.Banks <= 0`. Body line and header suffix now name
one subject each, and both are true.

This closes the half-move left by WO-1434, which corrected the BODY line's subject and left the header suffix
saying the wrong thing off the same bit. The attribution is written into the code at `WelcomeBackPopup.cs:194`.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds were a UI-MVVM violation on `BuildPreviewModal.cs:252-253` and a hollow-pass
marker at `NightMarketNoWalletRegression.cs:761`, both fixed at source and committed in `eb161dc98` (20:10),
AFTER both logs. Neither log postdates `eb161dc98` or the current working tree, and this lane's edits are
uncommitted, so `AwaySummaryReportRegression` has not been run against the re-pointed pin.

## Acceptance

- [x] Away-window cap and bank-full cap produce DIFFERENT strings, both live: `(AWAY LIMIT REACHED)` on the
      header (`WelcomeBackPopup.cs:696`) and `STORAGE FULL - STAYS PUT` per return row
      (`OfflineHarvestService.cs:890`).
- [x] `AwaySummaryReportRegression` case8 re-pointed (`:252-255`). RED proof is stated in the suite source, not
      measured - this lane held no Unity lock.
- [ ] Fresh `WelcomeBack` capture opened. Not re-captured.
- [ ] `REGRESSION_OK n/n` on a fresh log. The only run available is a `REGRESSION_FAIL` that predates this work.

Still owed: the wave-two compile and regression gate over this uncommitted work, a fresh `WelcomeBack` capture
opened, and a Seeker screencap of a return from an away window longer than ten hours.
