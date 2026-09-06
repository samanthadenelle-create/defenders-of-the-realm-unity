# WO-1404 RESULT - the Journey deck's two cards say what is waiting

**Status:** FIXED 2026-09-05 (Codex dev lane + s8.9 rework; gated by the CLI; device build after the owner's reboot; felt-test closes)

## What landed (worktree `D:\eoa-codex-1404`, base `003b64ce2`, applied three-way onto `ecf647b53`)
- `Assets/_Modules/Core/HudModel/JourneyDeckSubtitleVM.cs` (Core, pure VM): Quests = `<n> active . <m> ready to claim`
  from `QuestService.ActiveQuestIds()` + `DailyQuestService.Today.Quests`; Raids = `Army <n> / <cap> . <k> camp(s)
  open` from the army-fill seam + a new change-only `PostureSignals.SetRaidOpenCampCount`.
- `BuildTimerService` publishes the open-camp count: rebuilt only when victories / catalog fingerprint / scene
  provider change (no 1 Hz `RaidSelectionVM` construction, no per-second trace - the s8.9 finding); a camp counts as
  open only if `RaidSelectionVM.IsLocked(id)` is false AND `GarrisonCount(def) <= deployableBodies` (the `<=`
  predicate is newly authored here; `RaidSelectionVM.cs` byte-identical to base).
- `PlayerDeckWorkspace` (HUD, Core-only reference) binds both subtitles; exact trace per card.
- Capture fixture publishes `Army 0 / 10` for the Journey frame. New `JourneyDeckSubtitleRegression` (RED recipes),
  registered by the lead. The suite's first version imported `DeNelle.HUD`, which the regression assembly does not
  reference - fixed by moving the VM to Core (lead ruling s8.9 #1), no asmdef widened.

## Evidence
- Two read-only lead reviews (first: blocker + two design defects; rework recheck: all eight s8.9 items PASS).
- `COMPILE_GATE_OK` (`Builds/c11`, 23:13); `REGRESSION_OK 389/389` (`Builds/r12`, 23:15) with
  `JOURNEY_DECK_SUBTITLE_OK fixture='2 active . 1 ready to claim' / 'Army 3 / 10 . 1 camp open'`; `UI_CAPTURE_OK 91`
  (`Builds/cap6`, 23:16) - `JourneyWorkspace_2670x1200.png` opened.

## Advisory (not blocking)
- `JourneyRaidCatalogInput()` still walks the scene catalog once a second to fingerprint it (allocation-free);
  revisit if the catalog grows.
