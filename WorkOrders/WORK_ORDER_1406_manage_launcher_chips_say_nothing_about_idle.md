# WO-1406: Manage launcher chips say nothing about IDLE, the Troops header has no army total, and the locked Troops card is a wall, not a door

**Status:** IN PROGRESS - ABSORBED INTO WO-1418 (Codex batch, BATCH_STATE PART 8 / 8.5 ruling 1: all three chips activate their tab); lands and flips with 1418. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

## Evidence
- `docs/qa/UI_REVIEW_2026-09-05/03-manage-launcher.png` (device, build 355952) - SEEN (`REVIEW_MERGED.md` row 5):
  chips `Builders 0/2 . Training 0/2 . Research 0/2` under `Choose a path`; nothing says a line is idle or which
  path has something waiting.
- `07-manage-troops.png` (device): the Troops card reads `Available` under `LEVEL 3` with no referent; no `Army N / M`.
- `Builds/ui-capture/ManageWorkspace_2670x1200.png` (09-05 07:02, Troops locked): the card reads
  `Build a Barracks to unlock` with a lock glyph and is not tappable.
- Both reviewers: `REVIEW_A_independent.md` A-2 / A-3 / A-8, `REVIEW_B_independent.md` A1 / A3.
- CODE: `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:674` `Choose a path`; `:722` the locked-card copy;
  `:857` the tap only toasts `Build a Barracks to unlock Troops.`. The Defense tab already owns a Build door
  (`BUILD DEFENSE` -> Close + EnterBuildMode). Army numbers: `PostureSignals.SetArmyFill` (`Core/HudModel/PostureSignals.cs:321`)
  from `BuildTimerService.PublishArmyStatus` (`Village/Buildings/BuildTimerService.cs:2199`).

## What the player experiences
Three chips that read as scores, a card that says a number with no name, and a locked card whose only response
is a toast repeating its own label. The launcher never says "go here, something is waiting".

## Fix shape (one mechanism)
- Chips read idleness in words and are doors: `Builders idle - 2 free` / `Builders 1/2 . next free 3m 59s`; a tap
  activates that tab (`ActivateLauncherCard`, which exists).
- Troops header band: `Army 3 / 10 - The Forsaken Camp fields 9` from the `PublishArmyStatus` producer plus the
  cheapest open camp from `RaidSelectionVM` (WO-1402's predicate). `Available` -> `3 in your army`.
- Locked Troops card becomes tappable and reads `BUILD A BARRACKS`, routing through the Defense tab's existing
  `Close + EnterBuildMode(...)` seam, armed on the Barracks entry. The toast at `:857` is retired.

```
Choose a path        Builders idle - 2 free   Training idle   Research idle
[ DEFENSE ] [ BUILDINGS ] [ RESEARCH ] [ TROOPS  BUILD A BARRACKS ]
```
Trace: `FlowTrace.Step("Manage", "launcher chip=<line> idle=<bool> free=<n>")`, `FlowTrace.Step("Manage",
"troops locked door -> build mode barracks")`.

## Acceptance
- [ ] RED first: `ManageLauncherIdleRegression` - fixture with 0 busy builders: the Builders chip text contains
      `idle`; chip tap activates the Defense tab (trace); Troops-locked fixture: the card is a button whose label
      is `BUILD A BARRACKS` and whose tap enters build mode (trace), no toast. Fails on the current tree.
- [ ] Headless: `ManageWorkspace_2670x1200.png` + `ManageTroops_2670x1200.png` regenerated
      (`MANAGE_OPERATIONAL_CAPTURE_OK 12/12`), opened: chip words, army header line, door label.
- [ ] Device: Manage launcher on a fresh save and on the owner's save; screencaps read.

## Not in scope
Row benefit lines (WO-1405); the queue drawer; the HUD Builders chip (WO-1407); Barracks cost or placement rules.

## Owner ruling
None from section 2 - the ticket reuses rulings already made (WO-1389 army status; Defense tab door pattern).
