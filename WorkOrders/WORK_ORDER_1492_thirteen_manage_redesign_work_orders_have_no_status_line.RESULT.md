# WO-1492 RESULT - all seventeen ManageRedesign tickets carry a Status line, and the board now names a missing one

**Status:** FIXED in the working tree. Both halves landed: the seventeen files, and the durable sweep
that stops a whole program going invisible again.
**Commit:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files:**
- `WorkOrders/ManageRedesign/WO-2001` through `WO-2017` - thirteen files gained a `**Status:**` line
  (2001, 2002, 2004, 2006, 2007, 2008, 2009, 2010, 2012, 2013, 2014, 2015, 2016); the other four
  already carried one. Verified by reading the first `**Status:**` line out of each of the seventeen
  this session. The audit's PARTIAL rows read `IN PROGRESS - PARTIAL: <what is outstanding>`, and
  nothing was marked DONE on the audit's word alone: 2004 and 2015 read `READY TO IMPLEMENT`, the
  LANDED rows read `DONE - landed in <sha> (verified 2026-09-06)`.
- `tools/board_build.py:634-680` - `is_work_order_file` plus `missing_status_sweep()`, a RECURSIVE
  walk of `WorkOrders/` at any depth. It accepts both filename shapes, `WORK_ORDER_*.md` and
  `WO-<n>_*.md`, which is the second half of why the ManageRedesign program was invisible twice over:
  wrong directory AND wrong filename shape. `*.RESULT.md` and companion files are excluded.
- `tools/board_build.py:1765-1772` - prints `MISSING_STATUS_LINE <n> ...` and NAMES each file rather
  than counting them, so the output is the to-do list.
- `tools/board_build.py:1790-1797` - a missing status line is folded into `problems`, so the run ends
  `BOARD_CHECK_FAIL ... n missing status line(s)` instead of `BOARD_CHECK_OK`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed. This lane is Python and markdown, so no Unity suite covers
it; its own gate is `BOARD_CHECK_OK` from `python tools/board_build.py`.

## What landed

The sweep is deliberately NARROW. It answers only "is a `**Status:**` line PRESENT", never what the
status means - bucketing stays with `classify_status` on the rendered rows, so the sweep cannot
reclassify a single existing row. It can only add a named defect. Top-level files the row parser
already owns are skipped so a missing status is reported once, as Unlabeled, rather than twice.

## Acceptance

- [x] All seventeen carry a Status line - read at source this session.
- [ ] `python tools/board_build.py` shows them on `BOARD.html`. NOT RUN since the change. The last
      recorded board run is `b30e551ce` (20:12) which printed `BOARD_CHECK_OK`, and that predates
      these edits.
- [x] `board_build.py` fails on a WO file with no Status line - the code path is
      `missing_status_sweep()` feeding `problems` at `:1792`. The RED proof by removing one locally is
      NOT stated.

Needs no device capture. It needs one `python tools/board_build.py` run to confirm the seventeen rows
render and the marker is still `BOARD_CHECK_OK`, plus the deliberate remove-a-status RED proof.
