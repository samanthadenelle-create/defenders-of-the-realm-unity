# WO-1413 RESULT (part 1) - copy hygiene on the file-disjoint screens

**Status:** FIXED 2026-09-05 - part 1 (`458baf57f`) + part 2 (Codex, s8.11 hand-back at batch_results_state.md:745, gated by the CLI); device build after the owner's reboot; felt-test closes

## Part 2 (landed on top)
- `UICaptureLaunch.cs`: the retired dialogue-fixture option is replaced by the live "Show me the rumor board." plus the
  required "Gather resources"; the two synthetic watch rumors read "Part 1 of 2" / "Part 2 of 2". The canonical dialogue
  twins were inspected and left untouched (neither carries the retired line - fixture-only defect; twins byte-identical).
- `HudKitController.cs`: the three adaptive combat skill faces seed as EMPTY; an equipped slot paints its live
  AbilitySlotRecord name / icon / cooldown - no more numbered placeholder faces.
- New `CopyHygieneRegression` (RED recipes), registered. Lead correction at gate: its `[retired-pet]` scan was a
  case-insensitive "& PET" search that matched ordinary C# (`&& pet.Id == id`) in three files outside the lane and
  went RED on code, not copy; now a case-sensitive "& Pet"/"& PET" phrase match that ignores `&&` (zero hits
  repo-wide, RED recipe: put "Reset Hero & Pet" back in HelpMenu).
- Gates: `COMPILE_GATE_OK` (`Builds/c13` 23:51), `REGRESSION_OK 390/390` (`Builds/r14` 23:54) incl.
  `[copy-hygiene] COPY_HYGIENE_OK`.

## What landed (worktree `D:\eoa-codex-1413`, base `44d46128d`, applied three-way onto `ecf647b53`)
- Help: `Reset Hero & Echoes`, behind the shared danger confirmation (`ElarionUiKit.BuildConfirmModal`, Danger kind);
  developer rows stay compile-guarded.
- Echo workforce copy: `Echoes N/M - harvest +P% together` from the calculator's DISCLOSED additive spec-sum
  (`DisclosedHarvestBonusFraction` reproduces the original term order; numeric identity verified twice; hidden
  tri-synergy excluded on purpose). `Assets/Tests/EditMode/EchoWorkforceVMTests.cs` updated to the new shape.
- Settings keeps the Music slider, drops the duplicate toggle. Daily Chest hides the rewarded-ad CTA until the
  placement is ready (reward path untouched).
- Pause: the s8.9 revert - the "Resume" write was inert under the medieval shell (the baked ornate plate reads
  CLOSE) and `PauseMedievalSkinRegression:20` pins the primary face; PauseController.cs is byte-identical to base.
  "Pause: RESUME only" goes to the owner's rulings list.
- Stale prose: `HelpMenu.cs`, `HelpMenuVM.cs`, `SettingsController.cs`, `EchoWorkforceVM.cs` doc; the lead fixed the
  Core comment `PanelRouter.cs:146` ("Reset Hero and Echoes").

## Not done (part 2, unlocked now)
- Dialogue fixture + Rumor duplicate-card fixture (`UICaptureLaunch.cs`), combat skill faces (`HudKitController.cs`),
  the dialogue JSON twins, and `CopyHygieneRegression` (RED-first) - fenced behind the 1418 lanes at the time.

## Evidence
- Rework recheck: all s8.9 items PASS, no oracle red. `COMPILE_GATE_OK` (`Builds/c11`, 23:13);
  `REGRESSION_OK 389/389` (`Builds/r12`, 23:15) incl. `[pause-medieval-skin]` green; `UI_CAPTURE_OK 91`
  (`Builds/cap6`, 23:16) - `HelpMenu_2670x1200.png` opened.
