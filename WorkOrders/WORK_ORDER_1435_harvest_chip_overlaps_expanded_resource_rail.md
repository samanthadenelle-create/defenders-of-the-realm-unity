# WO-1435: the Harvest chip covers a resource row when the resource window is open

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:52:24, build 2026.09.07.358574). PRIOR STATUS: FIXED - ON THE SEEKER 2026.09.07.358574 - landed in `5bc5025f5` (see RESULT); the measured-rects criterion is a recorded deviation awaiting the owner's word
**Silo:** HUD only (`Assets/_Modules/HUD/Kit/HudKitController.cs` + its pinned suites). File-disjoint from
the Manage 2000-block, from WO-1434 (Village/Harvest + popup) and from WO-1432 (new feedback panel).
**Source:** owner felt-test 2026-09-06 on build **2026.09.06.358161**, verbatim:
> *"can we move harvest down when someone opens the resource window"* / *"so it doesnt overlap"*

**Evidence: `adb screencap` pulled from her device this session**, saved at
`Logs/device/screens/` (harvest-overlap). Not a report, not an inference - the pixels.

---

## 1. WHAT THE CAPTURE SHOWS

The right-hand rail, top to bottom: coins **371**, wood **10k**, iron **4000**, then a row whose value is
**covered by the `Harvest` button**, then crystals **78**.

Rows 1, 2 and 4 each render their number. **Row 3 renders a button on top of its number.** The obscured
row is **STONE**.

**Why that particular row matters, and it is not a coincidence worth ignoring:** WO-1434 proved from the
same device that stone is capped at **2,000** while production runs at **~7,050/hour** - the entire bank
fills in 17 minutes. The one number that would have told her this at a glance is the one number the
button covers. **This is not only a layout defect; it is the reason a real economy problem stayed
invisible.** Say so in the RESULT.

## 2. THE SEAM - read these, they are the whole mechanism

All in `Assets/_Modules/HUD/Kit/HudKitController.cs`:

| Line | Fact |
|---|---|
| `:1834` | `BuildRailChip(rrt, "CollectorsChip", "Collectors", 0f, ...)` - the Harvest chip is pinned at a **fixed** `yFromTopPx`. |
| `:1940` | `rt.anchoredPosition = new Vector2(0f, -yFromTopPx)` - fixed-pixel band, top-anchored. |
| `:1738` | `ResRowHeightPx = 56f` |
| `:2754` | `float panelH = kinds.Length * ResRowHeightPx + (kinds.Length - 1) * ResRowGapPx;` |
| `:2775` | `rowRt.anchoredPosition = new Vector2(0f, -(i * (ResRowHeightPx + ResRowGapPx)))` |

**The resource panel's height is a FUNCTION of its row count and grows downward. The Harvest chip's
position is a CONSTANT.** Two things sharing one gutter, one of them variable, and nothing reconciles
them. That is the same species this whole program keeps finding: two things that should be one.

## 3. WHAT TO BUILD

**The chip's offset must DERIVE from the panel's actual height, not be a second hand-maintained number.**
A new constant that happens to clear today's four rows is the identical bug one row later - and the row
count is not fixed (`kinds.Length`). Read the laid-out height and place the chip beneath it.

Also required: **the Builders chip at `:1758` sits in the same gutter at the same fixed `0f`.** Fix both
or prove the second cannot collide. Do not fix only the one she saw.

## 4. THE CONSTRAINTS THAT WILL BITE - all canon, all verifiable in-file

- ⛔ **The chip is 220 x 112 ref px and that is CANON** (`:1720-1722`). `RailChipWidthPx = 220f` because
  three rail chips share one right edge and must match `EchoUnlockFeedback.EchoChipWidthPx`. **Do not
  narrow, do not shorten, do not shrink the font** - `ElarionUiKit.FontFloor` is a FLOOR. The in-file
  comment at `:1866-1878` records a captured fleet failure where this chip read `"Tap to collec"`, sliced
  mid-glyph in all 8 runs, and states the fix is fewer characters, never a smaller box.
- ⛔ **Fixed pixels only in rail chrome (WO-841), never fractions.** `:1928-1930` explains why: a fraction
  band can resolve under `MinTouchPx`, and `ClampMinTouch` then grows it about its centre **into its
  neighbour** - which would recreate this exact overlap by a different route.
- **Never the word "Storage"** on this chip (WO-900 section 4 copy law, cited at `:1864`).
- **The owner is red/green colourblind.** State carries in words and count, never tint. Greyscale is the gate.
- `HudLabelFitRegression` and `SessionShapeRegression` pin HUD geometry. If a case pins the colliding
  offset, **move it deliberately with the ruling recorded in-file** - a pin that requires the defect is a
  pin that forbids the fix. Do not delete a case to go green.

## 5. ACCEPTANCE

- [ ] A regression that MEASURES the laid-out rects of the resource panel and both rail chips and asserts
      **zero overlap**, at **three different row counts** (so it cannot pass by matching today's four).
      It must FAIL against today's build - state its RED proof in-file.
- [ ] Headless capture with the resource window OPEN, **PNG opened and looked at** (memory:
      `headless-screenshot-verify-ui-before-build`; compile-green never proves a panel looks right).
- [ ] The stone row's number is legible in that capture.
- [ ] `REGRESSION_OK n/n`.
