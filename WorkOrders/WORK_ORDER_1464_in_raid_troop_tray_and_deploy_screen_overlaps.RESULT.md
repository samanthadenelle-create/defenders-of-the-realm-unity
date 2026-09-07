# WO-1464 RESULT - the in-raid half

**Status:** IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate.
The deploy-screen half landed earlier under WO-1519; this covers the three in-raid defects.

## BASELINE (measured, not inferred)
`Logs/device/screens/owner-screen-20260907-004502.png` - build 358872, 2670x1200, mid-raid at 1:13,
SPIRE 20% / Razed 72%. Opened. It shows all three defects in one frame: FOOTMAN / ARCHER dark-on-dark,
the tray slab over the movement stick's ring, and "1:13" over the hero nameplate with "1/3" and
"Troops 10/10" over the compass.

## WHAT CHANGED, AND AT WHICH CAUSE

**1. Unreadable tray (fixed by REMOVING an override, not by picking a colour).**
`RefreshTiles` was overwriting the kit's label colour with `ElarionUi.Ink` (near-black) on the obsidian
tile's dark face. The kit already paints these labels `ObsidianButtonLabelColor` -> `ElarionUi.Parchment`,
which is exactly why Rally ON and RETREAT read perfectly in the same capture. The override is gone; the
count badge moves off the corner filigree into the tile interior in `Gilt`, bold, `FitSingleLine`. The
armed cue was hue-only (`Affordable` green vs `Ink`) - it is now a SHAPE marker (`[ FOOTMAN ]`) with
brightness as a secondary, never sole, signal. Same defect class as `HelpMenuEntryRegression` D2.

**2. Tray over the joystick.** The stick's mount moved into `HudLayoutBands.MoveClusterMount` (the only
copy; `HudAreasHost` reads it) and the tray's left edge is now `HudLayoutBands.BottomOverlayLeftX`
(stick right edge + the shared gap) = 0.280. WO-1436 lifted the bar clear of the ability row's Y band
and that fix stays - but the stick reaches y 0.330, more than twice the row's 0.150 ceiling, so
clearing the row was never the same as clearing the stick.

**3. Raid readout over the nameplate and compass.** Seat is now
`HudLayoutBands.RaidReadoutBand` (x 0.780-0.995, y 0.510-0.870) and the readout reflows top-to-bottom
as a right-hand column: timer / SPIRE / Razed / stars / troops. There is NO free full-width top strip
on this HUD - Vitals reaches y 0.983, Status 0.990, System 0.985, and below them TargetInfo and the
Heart plate close the next row. The right column is lawful because `hud-areas.json` lists `actionRail`
with an empty widget array in BOTH hostile postures and does not list `queueStatus` at all, and WO-1436
makes a raid hostile end to end. That conflict is STATED in `HudLayoutBands`, not hidden.

## MEASURED, ZERO OVERLAPS
bar (0.280-0.980 x, 0.160-0.310 y), status (0.280-0.980, 0.320-0.360), readout (0.780-0.995,
0.510-0.870) vs stick / hero plate / Status / System / TargetInfo / Heart / ability row: no intersection
at `HudLayoutBands.Epsilon`.

## THE TRAP THIS FIX NEARLY WALKED INTO ([seat])
Making a label legible in a band too thin to seat it makes it render BLANK, not small - TMP Ellipsis
culls the line, and the headless capture the acceptance PNG comes from has no relax guard. Measured at
`RaidSelectionScreen.NeedPx(FontFloor 30) = 38.58` ref px on the owner's 965.4-unit canvas:
the count badge at its first seat was **29.7** px (would have culled) - grown to 0.48 of the tile,
**44.5**; the deploy status line's band is **38.62** against 38.58, a 0.04 px margin, so that one label
is fitted at `FontHardFloor` (NeedPx 26.8) and the oracle asserts the floor the code actually uses;
the readout's thinnest row is **41.7**. All three are now pinned by a `[seat]` assertion.

## ORACLE
`RaidHudThumbBandRegression` extended (NOT a new sibling) with cases 5-7 plus `[seat]`. Case 5 is a
FIXTURE case: it instantiates a live `HudAreasHost`, reads the mounts the game builds, and reds on any
raid band intruding. Cases 6-7 keep the numbers from being retyped, including
`HudAreasHost.ActionBarMinX == HudLayoutBands.MoveClusterMount.xMax`. Red proof against build 358872
is written on the method. **No new registration needed** - `DataRegression.cs:463`
(`[raid-thumb-band]`). `NightMarketUiRegression` was also edited (its `[stick]` case now reads the Core
constant instead of regexing the HudAreasHost literal that moved) - `DataRegression.cs:1518`.

## FILES (+/-)
`Assets/_Modules/Core/UI/HudLayoutBands.cs` +75/-0 - `MoveClusterMount`, `BottomOverlayLeftX`, `RaidReadoutBand`
`Assets/_Modules/HUD/Kit/HudAreasHost.cs` +10/-1 - mounts the stick from the table
`Assets/_Modules/Village/Troops/RaidDeployController.cs` +96/-13 - tray left edge, legibility, badge seat
`Assets/_Modules/Village/Troops/RaidHudController.cs` +96/-41 - readout reseated + reflowed
`Assets/Editor/Regression/NightMarketUiRegression.cs` +24/-5 - `[stick]` reads the Core constant
`Assets/Editor/Regression/RaidHudThumbBandRegression.cs` +272/-2 - cases 5-7 + `[seat]`
Brace balance and NUL scan passed on all six.

## OWED
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on a fresh log. **No Unity run happened in this lane, so
      the suites are NOT proven green.**
- [ ] A fresh in-raid capture of the SAME view as the baseline.
- [ ] Owner felt-verify. The readout moving from a top strip to a right column is a presentation
      change she has not seen; it is the only lawful seat, but it is her call.

## CONTRADICTIONS FOUND
- The ticket sites the top band in `RaidDeployController.cs`; it is `RaidHudController.cs:119-121`.
- The badge cite `:1141-1153` is stale - it was `:1447-1452`.
- `RaidHudController`'s own header claimed the two raid surfaces "sit on complementary edges and never
  overlap". True of each other; neither was ever checked against the HUD beneath. Header corrected.
