# WO-1488 RESULT - rows derive from the plate, the X moved, the timer fits; the flow map is still RED

**Status:** FIXED AT SOURCE, UNGATED AND UNPROVEN. Uncommitted in the working tree as of 2026-09-06
21:00, awaiting the wave-two gate. The `MANAGE_FLOW_MAP_FAIL` acceptance is still open on the evidence
that exists.
**Commit:** none - working tree only.
**Files:**
- `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:431` - the row band is DERIVED from the plate
  rect; `:488` measures the live plate rather than assuming it; `:595` bounds the list by plate then
  tabs; `:2028` puts the X top-right in the title overlay, out of the tab strip; `:1942` and `:2202`
  hold the one shared pair of constants; `:5268` fits the timer line to its own floor.
- `Assets/Editor/Regression/ManageQueueDrawerRegression.cs:480` - new case 11
  `[rows-inside-the-plate]`, measured; `:259` and `:389` retire and re-point two older reads with the
  ruling kept in place rather than deleted.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and
committed in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the
current working tree. The wave-two gate is owed.

## 1. What landed

The recorded cause is that the rows and the art were two different rects. Rows were seated on the
drawer rect at `_drawerListY0 = gap` while the frame draws with a 96px 9-slice border
(`content-panel.png.meta` spriteBorder 96,96,96,96), so the visible interior floor sits about 96px
higher. Two whole rows, roughly 84px of them, painted outside the frame - which is why the older
whole-row trim case passed: it measured the list against itself, and the list was never wrong.

The new case names five RED mutations that each fail it, including restoring `_drawerListY0 = gap` and
restoring `FitSingleLine(state, 0f, QueueLineFontPx)`, the 30px kit floor that ellipsised
"11m 0s left (0% do...". Its reference height 579px is measured off the r24 line
`MANAGE_QUEUE_BANDS drawer=475px` divided by the 0.82 span, not assumed.

## 2. Acceptance

- [ ] `MANAGE_FLOW_MAP_OK` on a fresh flowmap log. OPEN. The newest is `Builds/flowmap-r24.log`
      (2026-09-06 18:39) and it still reads `MANAGE_FLOW_MAP_FAIL`. That log PREDATES this lane's
      edits, so it neither proves nor disproves the fix. Section 3 of the WO is respected here: the
      "all nine screens match" claim is NOT repeated.
- [x] Measured drawer case with RED proof stated. `ManageQueueDrawerRegression.cs:480`.
- [ ] Fresh `ManageFlow_BUILD_queue` PNG. OPEN - the only one on disk is the ticket's own evidence
      image at 2026-09-06 18:39, pre-fix.
- [ ] `REGRESSION_OK n/n` on a fresh log. OPEN - see the gates line.

Row thumbnails, asked for in section 2 of the WO, are NOT in this lane's edits and remain open.
Still owed: a flowmap round and a `ManageFlow_BUILD_queue` capture, both after the wave-two gate.
