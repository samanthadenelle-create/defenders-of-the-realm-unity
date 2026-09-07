# WO-1571 RESULT - Manage BUILD door now opens the ghost for its own id

**Status:** IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate. Edit-only: no Unity, no git.

## ROUTE

BEFORE - id dropped at the first hop: `ManageScreenVM.cs:4188`
`Invoke = () => OpenTownBuilderRequested?.Invoke()` -> `ManageScreenPanel.cs:830` ->
`OpenTownBuilder` (`:5317`) -> `BuildModeController.EnterBuildMode(Town)` (`:463`) ->
`Enter()` (`:539`) -> `:661-662` palette Configure/Show -> `BuildPaletteUI.cs:345` ->
`BuildCollectionBrowser.Show` (`:94`) `Open(Root())` -> dead end.

AFTER - id carried through: `ManageScreenVM.cs:4198` `Invoke = () => RequestPlacement(rowId)` ->
`PlaceStructureRequested` -> `ManageScreenPanel.cs:831` -> `OpenPlacementFor` (`:5330`) ->
`BuildModeController.EnterBuildModeForStructure` (`:498`) -> `BuildPaletteUI.PlaceById` (`:356`) ->
`BuildCollectionBrowser.PlaceById` (`:508`) -> the existing private `Place(entry)` ->
`Done(...)` -> `OnEntrySelected` -> `Arm` -> ghost armed.

`Enter` still shows the palette, so `opened workspace 'Build Collections' at root` STILL prints
once, immediately followed in the same frame by `Manage direct BUILD door -> place '<id>'` and
`Armed placement for '<id>'`. A trace that STOPS at the root line is the failure.

## SEAM AND GATES

- Seam: the browser's own `Place`/`Done` pick path, not `Close()` + `ArmById` - `Done`
  (`ObsidianNavigationWorkspace.cs:97-107`) also releases the browsing pause.
- First-use guide: `Advance` (`BuildFirstUseGuide.cs:78-81`) is a silent no-op off-step, so `Done`
  cannot refuse; and WO-1411's `GhostArmed()` (`:59-63`, called from `BuildHudController.cs:1023`)
  names "a Manage door" and moves the guide to MoveGhost. Nothing to sequence.
- Offer/unlock gate: `IsCollectionItemVisible` (`BuildCollectionBrowser.cs:564`), asked BEFORE
  anything closes; carries WO-1379 + `build-categories.json` lockedIds. Singleton stays in `Arm`.
  Affordability deliberately not a door test - the ghost opens and the why-band explains.

## FILES TOUCHED

`ManageScreenVM.cs`, `ManageScreenPanel.cs`, `BuildModeController.cs`, `BuildPaletteUI.cs`,
`BuildCollectionBrowser.cs`, `Assets/Editor/Regression/ManageBuildDoorRegression.cs` (NEW),
`DataRegression.cs`, `CLI_LANES_WO_NUMBERS.md` (1571 -> 1572 same edit), `WORK_ORDER_1540_*.md`
(section 5). Braces balanced, NUL-clean on all seven `.cs`. Not gated - no Unity in this lane.
