# WO-1402 RESULT - Raid Selection rows say what a raid PAYS

**Status:** FIXED 2026-09-05 (gated; device build follows tonight)

## What landed
- `Assets/_Modules/Village/Troops/RaidScoring.cs` - `EstimateSpoils` (the ONE spoils estimate, shared with WO-1403's deploy screen).
- `Assets/_Modules/Village/Hero/RaidSelectionVM.cs` - `EstimateSpoils` / `FormatSpoils` / `SpoilsPrefix`: each camp row carries a spoils line.
- `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs` - paints the VM's spoils line on the card.
- New `Assets/Editor/Regression/RaidSelectionSpoilsRegression.cs` (registered in `DataRegression.cs`).

## Capture finding and fix (instrumented, not guessed)
The first capture (`Builds/cap2`) showed NO spoils line on any card although `cap2:13574/13909` proved the VM produced
it and the View built it: the card bands were too short (22.7 px for a 22 pt line) and `FitSingleLine` cannot shrink
below the label's own size, so TextMeshPro culled the whole line - and with it the escalation lock sentence on every
locked camp (the WORD that carries the state for a red/green colourblind owner). Fix: `CardHeightPx` 142 -> 178, five
named bands, spoils on its own right-aligned row, clock 28 -> 22 pt; `RaidSelectionSpoilsRegression` case F replays
the live `CardBands` table (`HavePx >= NeedsPx`, RED at 142). Recapture (`Builds/cap3`, 22:18):
`RaidSelection_2670x1200.png` opened - spoils line + lock sentence visible on every card.

## Evidence
- Lane patch (previous session, 08:24) applied clean to `44d46128d`; 5 files, +768/-21.
- `COMPILE_GATE_OK` (`Builds/c3`, 21:43); `REGRESSION_OK 385/385` (`Builds/r3`, 21:45) with `RAID_SELECTION_SPOILS_OK`.
- Owner felt-test on the tester build closes it.
