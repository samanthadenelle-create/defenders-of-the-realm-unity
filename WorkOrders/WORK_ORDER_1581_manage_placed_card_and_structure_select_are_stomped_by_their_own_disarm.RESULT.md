# WO-1581 RESULT - the door fired every time; something re-covered it a frame later

**Status:** IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate

## Trace evidence (adb logcat -d -s Unity, owner device, build 2026.09.07.359076)

The card was never dead. It was overwritten in the same frame, twice (00.191 and 00.972):

```
08:28:00.191  Manage Placed card TAPPED - closing the browser ...
08:28:00.193  closed workspace 'Build Collections' to world
08:28:00.194  WorldHold ACQUIRE 'obsidian-navigation-workspace:Build Collections' -> timeScale 0.00
08:28:00.194  PanelManager: 'Build Collections' opened and verified visible (IsOpen=true)
08:28:00.210  kit toast -> 'Tap a building or wall to move, upgrade or sell it.'
```

The same stomp is the tester's missing MOVE - selection works, then is buried:

```
08:28:02.424  tap-select: hit - SELECTS 'lumberyard' (web pointer path)
08:28:02.429  PanelManager: 'Build Collections' opened and verified visible (IsOpen=true)
```

## Cause

WO-1273 redefined `BuildPaletteUI.Expand()` (`:875`) to `Show()` the MODAL browser; its inverse
`Collapse()` (`:631`) was not updated and never touched `_collectionBrowser`. `CancelArmed()`
(`BuildModeController.cs:2396`) calls `Expand()` on every disarm, so both EDIT entries re-opened the
catalog over themselves - `BeginManagePlaced` (`:2436`, no Collapse at all) and `SelectStructure`
(`:2504`, whose Collapse no longer won). The MOVE chain itself is correct and unchanged (WO §2).

## Fix

1. `Collapse` closes the browser, before the `_canvas == null` guard, guarded on `IsOpen`.
2. `BeginManagePlaced` collapses AFTER `CancelArmed`/`ClearSelection` (both kept; C5c stays green).
3. MOVE chain instrumented (`SELECTED`, `MOVE BEGUN`, `MOVE COMMITTED`, `MOVE CANCELLED`, plus a
   `FlowTrace.Fail` on the catalog-miss refusal that was a bare `Debug.LogWarning`).

Card NOT re-pointed to Manage - ruling ss25 lands it on the existing select loop. Footer link proven
working and untouched (`08:28:01.569 footer link TAPPED` -> `Manage -> Defense` 02.421).

## Pin

`PlacedStructureDoorRegression` C6a (Collapse touches `_collectionBrowser` before the `_canvas`
guard) + C6b (`BeginManagePlaced` Collapses after `CancelArmed`, by index). Header records the
lesson: C1..C5 were green and true while the feature was dead to the player.

## Unproven from here

No Unity was run: `COMPILE_GATE_OK` and `PLACED_DOOR_OK` are NOT captured. The headless MOVE fixture
already exists - `AutoPilotDriver.AssertBuildMoveChain` (`:2797`, phase run at `:324`): arm -> cancel
-> tap-select -> `ProbeBeginMoveSelected` -> commit at a new cell -> assert the layout record moved.
**It would have passed with this bug present** - its SELECT link asserts `BuildSelectionUI` is
`activeInHierarchy`, which cannot see a second canvas drawn over it. That blind spot is the follow-up.
Brace/NUL passed on all three `.cs` (149/149, 547/547, 22/22; zero NULs); `git diff --numstat` =
38/79/75 changed lines, so the two python-assisted rewrites did not touch line endings.
