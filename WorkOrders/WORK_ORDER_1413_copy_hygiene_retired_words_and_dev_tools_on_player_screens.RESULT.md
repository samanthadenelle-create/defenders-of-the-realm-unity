# WO-1413 RESULT (part 1) - copy hygiene on the file-disjoint screens

**Status:** PARTIAL - part 1 FIXED 2026-09-05 (Codex dev lane + s8.9 rework; gated by the CLI); part 2 (the halves that were fenced behind the 1418 overlay) unlocked at `ecf647b53` and re-dispatched to the dev lane (BATCH_STATE s8.11)

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
