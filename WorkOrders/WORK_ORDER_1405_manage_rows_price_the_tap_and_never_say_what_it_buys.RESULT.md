# WO-1405 RESULT - Manage rows name what the upgrade buys, and the grid coordinate is retired

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 3 (BUILDING DETAIL) not yet passed (2026-09-07); code landed the Defense half 2026-09-06; the rest uncommitted. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED AND SUITE-GREEN IN A PRE-COMMIT RUN. Capture and Seeker felt-verify still owed.)*
**Commit:** `eb161dc98` (2026-09-06 20:10), the seven-gated-lanes commit. No uncommitted remainder belongs to
this ticket - `ManageScreenVM.cs` and `ManageScreenPanel.cs` DO carry uncommitted edits, but their markers are
WO-1488 / WO-1517 / WO-1518 / WO-1516 / WO-1387, not WO-1405.
**Files:** `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs` (+105; the developer coordinate retired at
`:1084-1099`, the replacement `CompassSideOf` helper at `:1122`, the trace at `:1636`), new suite
`Assets/Editor/Regression/ManageRowBenefitRegression.cs` (+477), registered in
`Assets/Editor/Regression/DataRegression.cs:1657` as `[manage-row-benefit]`.

## What landed

`ManageScreenVM.BuildDefenseBrowse` no longer concatenates `placed.cellX` / `placed.cellZ` into the row label.
It calls `CompassSideOf(placed.cellX, placed.cellZ)` (`ManageScreenVM.cs:1098`), which reads the axes off
`PlacementGrid` rather than assuming them and falls back to the shipped defaults headlessly, so a row can never
lose its location clause. The cell is still the row IDENTITY - it is composed into `jobKey` above, which is what
makes the CTA land on that instance - it is simply never spoken to the player. The new suite carries five cases:
`[no-developer-coordinate]`, `[location-is-words]`, `[coordinate-literal-retired]`,
`[defense-row-names-a-benefit]` / `[building-row-names-a-benefit]` / `[research-row-names-a-benefit]` and
`[troop-upgrade-names-an-effect]`. The Buildings and Troops halves landed earlier in `3c677027e` / `949e848a0`.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. Neither red was in this lane: one was a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` (the WO-1411 lane) and one a hollow-pass marker
at `Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761` (the WO-1409 lane); both were fixed at source
and committed in `eb161dc98` at 20:10, which is AFTER both gate logs. So neither log postdates `eb161dc98` or
the current working tree, and the wave-two gate is owed before any of this may be called green.

## Acceptance

- [x] RED-first suite exists and PASSED in the 20:07 run: `[manage-row-benefit] MANAGE_ROW_BENEFIT_OK every
      Defense/Buildings/Research row names what the upgrade buys, the Troops upgrade line names its effect, and
      no row string carries a grid coordinate` (`Builds/reg-quiet.log`). The RED proof is stated in the suite
      header rather than measured: reverting `ManageScreenVM.cs:1098` fires two cases.
- [ ] Headless: `RunManageOperationalCaptureHeadless` -> `MANAGE_OPERATIONAL_CAPTURE_OK 12/12` with the four
      Manage PNGs opened. Not run in this wave.
- [ ] Device: Manage > each tab, rows reading a benefit and a compass side.

Still owed: a re-run of the regression gate at HEAD, the Manage operational capture with the PNGs opened, and a
Seeker screencap of the Defense tab showing a compass side where `grid 5, 16` used to read.
