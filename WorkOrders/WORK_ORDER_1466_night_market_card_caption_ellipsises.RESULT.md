# WO-1466 RESULT - the oracle now measures the glyphs that are drawn, and the plate carries them

**Status:** IMPLEMENTED, uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
Capture and Seeker felt-verify still owed.
**Commit:** none. `Assets/_Modules/HUD/Kit/HudKitController.cs` and
`Assets/Editor/Regression/HudLabelFitRegression.cs` are both `M` in `git status`, and `git diff` on the pair
shows four `WO-1466` markers.
**Files:** `Assets/_Modules/HUD/Kit/HudKitController.cs:1140-1153` - the reasoning, and `:1153`
`NightMarketLabelPlateX0` moved from `0.30f` to `0.20f`.
`Assets/Editor/Regression/HudLabelFitRegression.cs:1655-1672` - case 11c now measures the upper-case form;
`:1793` does the same for case 12e.

## What landed

The ticket asked for the caption box to be ADDED to `HudLabelFitRegression`. The tree shows the box was
already there, in cases `[night-market-standout]` (11) and `[night-market-aurora]` (12) registered at
`HudLabelFitRegression.cs:179-180`, and that the oracle was measuring the WRONG STRING. It measured the
mixed-case `storeWordmark` that canon-strings authors, `The Night Market`, and reported the upper-case width
only as a note. The frame paints `THE NIGHT MA...` in capitals, and the same frame proves the transform is not
this card's: `AddDockTab` passes `Leaderboard` / `Music` / `Settings` / `Realm` / `Pause` and the HUD paints
MUSIC / SETTINGS / REALM / PAUSE, so every obsidian face in this HUD renders upper-case.

That is why the cut survived two builds, a felt-test and the very case meant to catch it. Upper case is now
the binding measurement in both cases, with the authored casing kept as the note. The plate then had to widen
to carry it: `(0.97 - 0.20) * 320 * 0.92 = 226.7` reference px against `197.2` before.

Both "what not to do" constraints held. The CARD is not widened, only the label plate inside it, and the
authored copy is not retyped - canon-strings is the owner's call and a second copy of the name is forbidden by
`StoreNameSingleSourceRegression`.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds were a UI-MVVM violation on `BuildPreviewModal.cs:252-253` and a hollow-pass
marker at `NightMarketNoWalletRegression.cs:761`, both fixed at source and committed in `eb161dc98` (20:10),
AFTER both logs. Neither log postdates `eb161dc98` or the current working tree, and this lane's edits are
uncommitted, so `HudLabelFitRegression` has NOT been run against the corrected measurement.

## Acceptance

- [x] Caption box present in `HudLabelFitRegression` - it was already present, and the real defect was the
      string it measured (`:1655-1672`). RED proof is stated in the source header, not measured.
- [ ] Caption renders complete in a fresh headless capture, opened. Not re-captured.
- [ ] `REGRESSION_OK n/n` on a fresh log. The only run available is a `REGRESSION_FAIL` that predates this work.

Still owed: the wave-two compile and regression gate over this uncommitted work, a fresh
`AdaptiveHudGearOpen_2670x1200.png` opened to confirm the whole caption renders, and a Seeker screencap of the
HUD card.

Note for the board: the ticket's premise that the caption "sits outside the label-fit oracle entirely" is
contradicted by the tree. It was inside the oracle and being measured wrongly, which is a worse failure and the
one the fix addresses.
