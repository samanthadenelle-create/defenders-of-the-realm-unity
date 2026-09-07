# WO-1411 RESULT - Build says what is affordable, the ghost rail says words, and confirm prices the tap

**Status:** IMPLEMENTED AND SUITE-GREEN IN A PRE-COMMIT RUN. This lane's own file carried one of the two reds
in that run; the fix is in the same commit, so the run must be repeated before this reads green.
**Commit:** `eb161dc98` (2026-09-06 20:10), the seven-gated-lanes commit.
**Files:** `Assets/_Modules/Village/BuildMode/BuildHudController.cs` (+303; `PlaceVerbWord` at `:255`, the
ROTATE and CANCEL word verbs at `:710` / `:713`), new `StructureCardVM.cs` affordability counter (+161, the
count at `:206-220`, the colourblind words note at `:329`), `BuildCollectionBrowser.cs` (+105; the footer text
link `Already built? Manage defenses >` at `:226`, route unchanged, noted at `:250`), `BuildPreviewModal.cs`
(+59; the confirm price-and-wait block at `:137`, the painter at `:166`), `BuildFirstUseGuide.cs` (+31; the
placement phase copy at `:31`, the phase-owns-the-banner rule at `:45`), `card-collections.json` in both
canonical copies, new suite `Assets/Editor/Regression/BuildAffordabilityWordsRegression.cs` (+164), registered
in `Assets/Editor/Regression/DataRegression.cs:530` as `[build-affordability-words]`.

## What landed

Collection card subtitles carry an affordability count computed in the VM and rendered by the card. The ghost
rail replaced three unlabelled glyphs with PLACE / ROTATE / CANCEL, and the place verb reads BLOCKED when the
placement is refused. The confirm modal now prices the last tap before spending, using the real graced duration
and crew rather than a literal. The eighth `Upgrade Defenses` card left the grid and became a footer text link
on the same route. The ruling #13 renames landed in both canonical copies of the collections data, and the
first-use banner takes the placement phase from any door rather than persisting the Category step copy.

WO-1478's correction to this ticket held: the cost baskets quoted in the evidence were harness stubs from
`UICaptureLaunch.cs`, not game data, so no literal was carried into the fix - the words come from the same
`CostFormat` seam the card and ghost pill use.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. One of the two reds is THIS lane's own file: `UI-MVVM CONFORMANCE VIOLATION x1 - NEW View(s)
reading game state without a ViewModel: Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs -> 252:
GameStateService ; 253:GameStateService`. The other was a hollow-pass marker at
`NightMarketNoWalletRegression.cs:761` (the WO-1409 lane). Both were fixed at source and committed in
`eb161dc98` at 20:10, AFTER both gate logs - `BuildPreviewModal.cs` now names no `GameStateService` outside a
comment at `:171`. Neither gate log postdates `eb161dc98` or the current working tree; the wave-two gate is owed.

## Acceptance

- [x] The suite exists, is registered and PASSED in the 20:07 run: `[build-affordability-words]
      BUILD_AFFORDABILITY_WORDS_OK: collection subtitles carry the VM's affordability count, the ghost rail
      says PLACE / ROTATE / CANCEL (BLOCKED when refused), the confirm modal prices the tap with the real
      graced duration and crew, the 8th 'Upgrade Defenses' card is a footer link, ruling #13 renames landed in
      both canonical copies, and the banner takes the placement phase from any door` (`Builds/reg-quiet.log`).
- [ ] The whole run passes. It did not at 20:07, on this lane's own MVVM violation, fixed in `eb161dc98`.
- [ ] Headless: `BuildCollections`, `BuildGhostChips_blocked`, `BuildPreview` regenerated and opened;
      `HudLabelFitRegression` green; the stale palette dock re-captured.
- [ ] Device: BUILD, pick a category, place, and read the three words and the confirm line.

Still owed: the wave-two regression run at HEAD, three fresh captures opened, and a Seeker screencap of the
ghost rail and the confirm line.
