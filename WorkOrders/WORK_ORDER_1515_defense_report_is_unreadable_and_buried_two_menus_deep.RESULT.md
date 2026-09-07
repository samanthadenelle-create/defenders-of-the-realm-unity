# WO-1515 RESULT - the tan slab and the row overlap are fixed with a new measured suite; the HUD chip was specced, not built

**Status:** PANEL HALF DONE, DOOR HALF SPECCED - uncommitted in the working tree as of 2026-09-06 21:45, awaiting
the wave-two gate.
**Commit:** none. Edit-only lane.
**Files:** `Village/UI/Defense/DefenseReportPanel.cs` (`M`, +100/-16),
`Assets/Editor/Regression/DefenseReportLayoutRegression.cs` (NEW, untracked), registered at
`Assets/Editor/Regression/DataRegression.cs:644` as the `defense-report-layout suite`.
`HUD/Kit/HudKitController.cs` deliberately UNTOUCHED - the WO-1465/1466/1468 lane owns it tonight.
**Gates:** none cover this. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and the owner's door ruling arrived
20:05, so the gate predates the whole lane. `Builds/cg-aab.log` (20:54) is RED: 42x `CS0103`, first two
`ManageTroopsTrainDoorRegression.cs(247,17)`, `ManageProgressiveDisclosureRegression.cs(228,41)`.

## 1. The RCA that changed the fix (ticket sec.2C, proven at source)

The panel never authored a tan surface. `StyleObsidianWell` built ONE `Image`, seeded it `ElarionUiKit.ObsidianFill`,
then overwrote it with `img.sprite = frame; img.color = Color.white;` - and `card-frame-empty` is a hollow bezel
with a transparent centre. The kit's `TwoToneParchmentFill` RGB(0.827, 0.760, 0.576) read straight through the
hole, under inks chosen for a DARK surface. Measured: `ParchmentDim` on that tan is **1.05:1**; on the obsidian
plate **10.96:1**. The left well took the identical call and looked right because its backing is the dark
`TwoToneWellFill` - one code path, two surfaces. The overlap was independent: the row label carried a hard `\n`,
and `FitSingleLine` is a WIDTH fit (NoWrap + Ellipsis + autosize), so the break survived and never shrank.

## 2. What landed

Plate and bezel are now TWO images (opaque `WellFill` plate, bezel a later sibling). The row band is DERIVED -
`Mathf.Max(ElarionUiKit.MinTouchPx, RowFontMax * RowLineBoxMul + RowPadPx)` = 112px, gap 10, pitch 122 - not a
fixed offset. The row label is one line with `FitSingleLine(caption, 30f, 44f)` armed explicitly; scroll padding
clears the bezel (22 list / 28 detail). `_onParchment` stays false and the panel keeps its light inks; the lane
FLAGGED sec.2's "obsidian plate AND `ElarionUi.Ink`" as self-contradictory rather than silently picking one
(CLAUDE.md sec.11B.B). `DefenseReportLayoutRegression` - markers `DEFENSE_REPORT_LAYOUT_OK`/`_FAIL` - carries
`[derived-pitch]` at 1920x1080 / 2340x1080 / **2670x1200** (`:99`, the owner's device), `[dark-plate]` asserting
every detail ink clears 4.5:1 against the plate with the shipped tan as a negative fixture that must stay under
the floor, and `[source-laws]` pinning the plate/bezel split, no `img.sprite = frame` on the fill, no `\n` in the
row label, `FitSingleLine` armed, NUL-free, braces balanced.

## 3. Acceptance

- [x] Detail pane on the kit plate; no row overlap; measured case at 2670x1200 with a contrast assert - sec.2.
- [ ] One measured case that the chip exists ONLY while an unread report exists - **OPEN**. Grep for
      `ATTACK REPORT` and `DefenseReportLedger` in `HudKitController.cs` returns nothing; the chip is a written
      spec in the ticket's sec.2D, handed to the lane that owns that file.
- [ ] A headless PNG with the chip on screen, opened - **OPEN**, blocked by the chip.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 4. Owed

The wave-two gate; the chip lane against sec.2D; a headless `DefenseReport_*` capture opened; and one Seeker frame
showing the chip present when a report is unread and absent when it is not.

## 5. Second pass - 2026-09-06 (door lane): the chip is BUILT

The owed half of sec.2B/2D is in the tree, uncommitted. `DefenseReportChipModel` (NEW,
`Core/HudModel/`) decides it: `Compose(unreadCount, newestUnreadOutcome)` is PURE - visible only above
zero unread, caption `"ATTACK REPORT
<HELD|BREACHED|OVERRUN>"`, empty and Key 0 otherwise - and
`Current` is the one ledger read. `HudKitController.BuildDefenseReportChip` relays it through the SAME
`BuildRailChip` the Builders/Collectors chips use, inheriting the 220x112 face, `MinTouchPx` and the
shared rail gutter; its band takes an `HudRailClearance` sourced from the gold chip and both siblings
(WO-1435) and starts INACTIVE. `TickDefenseReportChip` polls every 0.5 s and repaints on `Key` -
throttled because the ledger publishes no Version and `Current` walks the ring.
**Three things the spec did not say, all found at source:** (1) `BuildRailChip` arms `FitSingleLine`, a
WIDTH fit - the caption's second half is the outcome word, i.e. exactly what would ellipsize away, so the
label is re-fitted with `FitBlock(22,30)` and `[chip-gate]` MEASURES every caption line against the
220x0.92 label rect at that 22 px floor (the WO-1144 "Tap to collec" lesson, same chip family).
(2) `MarkRead` had ONE caller, `Select` (the row tap), so the chip's own door could never clear the chip:
`DefenseReportPanel.Open` now marks the LANDING report read and re-selects the newest when a newer one is
unread. (3) `_defenseChipKey` is reset in the builder - without it a HUD rebuild leaves the band inactive
while the key still matches, and the tick early-outs forever on an unread report.

**Files:** `Core/HudModel/DefenseReportChipModel.cs` (NEW 124 lines + `.meta`), `HudKitController.cs`
(chip build + tap + tick + fields), `DefenseReportPanel.cs` (Open marks read; `OutcomeWord` delegates to
Core - one switch, three surfaces), `hud-areas.json` in BOTH canonical copies (`defenseReportChip` on
calm(town) queueStatus - town only, LF preserved, both parse), `DefenseReportLayoutRegression.cs`
(`[chip-gate]`, suite now 4 cases). **No new registration** - it extends the suite already registered as
the `defense-report-layout suite`; `DataRegression.cs` untouched. **Still open:** the wave-two gate; a
headless PNG with the chip on screen, opened; one Seeker frame showing it present when unread, absent
when not. Noted, not fixed here: `Open` marks read BEFORE `PanelManager.NotifyOpened` can reject, so a
mid-battle rejection would clear the chip for a report never seen (town-only posture makes it unlikely).
