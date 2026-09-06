# WO-1407 RESULT - the town HUD tells a non-raid-capable player how to become one

**Status:** FIXED 2026-09-05 for the objective line, minutes, and idle-builders copy; two acceptance items carried to rulings (below). Gated; device build follows tonight.

## What landed
- New `Assets/_Modules/Core/HudModel/HudStateCopy.cs` - `HeartObjectiveCopy.Resolve(hostile, capable, lock, army, out troopsNeeded)`: the Heart plate's line 2 sentence ("Build a Barracks ...", "Train 3 more to unlock Raids", the wave line when capable) from Core state only.
- `Assets/_Modules/Core/HudModel/HudActionBarModel.cs` - `ArmySnapshot` republishes `RaidEntryGate.ArmyStatus` so the View never reads the gate (WO-835 View-purity pin).
- `Assets/_Modules/HUD/Kit/HudKitController.cs` - `RepaintHeartObjective` (change-detected on hostile/capable/lock/army.Version; one trace per transition) reading `(_barModel ?? HudActionBarModel.Shared).ArmySnapshot`; countdown reads `ElarionUi.Duration(...)` (minutes, not raw seconds); Builders chip copy "Builders idle 2".
- `Assets/_Modules/Core/UI/ElarionUi.cs` (`Duration`), `QueueRailView.cs`, `RaidEntryGate.cs` (army status incl. WO-823 `RequiredSlots`), `Village/Buildings/BuildTimerService.cs`, `Village/Waves/WaveCountdownUI.cs`.
- `Assets/Editor/Regression/HudLabelFitRegression.cs` Case13 `heart-objective-state`, Case14 `countdown-minutes` (`Duration(855) == "14m 15s"`), Case15 `builders-chip-idle`.

## Evidence
- Rebased onto `44d46128d` three-way (the WO-1415 plate block and this repaint both survive at `HudKitController.cs:4544-4622`); 9 files +537/-39; guid `9c4e1407b7d24f0aa3e5c6d8f1407a01` unique.
- First gate run (`Builds/r2`) RED on `HudActionBarRegression` ("HudKitController reads RaidEntryGate.ArmyStatus again") - fixed by the model accessor; `COMPILE_GATE_OK` (`Builds/c3`, 21:43); `REGRESSION_OK 385/385` (`Builds/r3`, 21:45).

## Carried to rulings (not built, on purpose)
- ASCII `[*]` flame pips -> icons: the owner ruled it tonight ("i hate those [*] items") - minted as **WO-1419** (Codex tail).
- "Builders chip tap opens Manage" contradicts CLAUDE.md s7 (chip = status glance; the Manage bar face is the ONE Queues door) - not implemented; ruling needed if she wants a second door.
- "Plate budgets four lines so `Heartfire is full` is never clipped" - mooted by WO-1415 (that string no longer exists).
- Chip visibility when idle: copy changed; the device-frame claim that the chip was absent is still unverified on the phone.
- Owner felt-test on the tester build closes it.
