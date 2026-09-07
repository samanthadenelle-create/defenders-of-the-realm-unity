# WO-1465 RESULT - the gear dock sorts above its neighbouring mounts, and the drawer clears the stick

**Status:** IMPLEMENTED, uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
Capture and Seeker felt-verify still owed.
**Commit:** none. `Assets/_Modules/HUD/Kit/HudKitController.cs` and
`Assets/Editor/Regression/HudUiRegression.cs` are both `M` in `git status`; `git diff` on the controller shows
11 `WO-1465` markers and `git log -S"WO-1465"` on that file returns no commit.
**Files:** `Assets/_Modules/HUD/Kit/HudKitController.cs` - the drawer-clears-the-stick block at `:4167-4193`,
`RaiseDockAboveNeighbourMounts` at `:4277-4310` with its trace at `:4307`, the `DockSortingStep` constant at
`:4313`, and the drawer band arithmetic at `:4320-4418`.
`Assets/Editor/Regression/HudUiRegression.cs:249` calls `CheckGearDrawerClearsNeighbours`, the measured case,
tagged `GEAR DRAWER (WO-1465)` at `:1754`.

## What landed

The cause was proven from source plus the capture rather than inferred, and the proof is recorded in the code
at `HudKitController.cs:4279-4287`: the two widgets sit in DIFFERENT area mounts. `hud-areas.json` puts
`chatDock` in `dock` and `nightMarketCard` in `minimap`, and `HudAreasHost.Build` adds Dock before Minimap, so
the Minimap mount is the later sibling and uGUI paints it last, on top. That rules out the ticket's implied fix
- no sibling shuffle INSIDE the dock could have worked, and shuffling the MOUNTS would fix one pair and rebreak
the next while fighting the occupancy table that owns mount order.

The fix instead gives the dock root its own sorting `Canvas`, one derived step above the host canvas, so it
stays under the battle overlay band (5000) and far under the modal band (30000+). A nested Canvas registers its
graphics to itself, so a `GraphicRaycaster` is added with it - without that every menu row would go dead. The
PAUSE-on-joystick half is handled at `:4167-4193`, which the code itself describes as belt and braces rather
than one fix standing in for the other.

The ticket's "what not to do" was respected: the Night Market card is not hidden while the menu is open.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds were a UI-MVVM violation on `BuildPreviewModal.cs:252-253` and a hollow-pass
marker at `NightMarketNoWalletRegression.cs:761`, both fixed at source and committed in `eb161dc98` (20:10),
AFTER both logs. Neither log postdates `eb161dc98` or the current working tree, so this lane has NOT been
gated at all: its edits are uncommitted and were not necessarily present in either run.

## Acceptance

- [ ] Fresh `AdaptiveHudGearOpen` capture opened with LEADERBOARD fully legible. Not re-captured.
- [x] Measured overlap case exists: `HudUiRegression.cs:249` `CheckGearDrawerClearsNeighbours`. Its RED proof
      is stated in the source header, not measured - this lane held no Unity lock.
- [ ] `REGRESSION_OK n/n` on a fresh log. The only run available is a `REGRESSION_FAIL`.

Still owed: the wave-two compile and regression gate over this uncommitted work, a fresh
`AdaptiveHudGearOpen_2670x1200.png` opened, and a Seeker screencap with the gear menu open over the Night
Market card and the PAUSE face clear of the joystick ring.
