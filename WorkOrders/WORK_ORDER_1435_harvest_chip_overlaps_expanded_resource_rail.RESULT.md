# WO-1435 RESULT - the Harvest and Builders chips clear the expanded resource panel by a derived offset

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify (open the resource window; the stone row must read under no chip) and a headless capture with the
panel open.
**Commit:** `5bc5025f5` (carried alongside the raid lifecycle work; the WO Status was never flipped - this RESULT
closes the gap after a read-only re-verification at source on 2026-09-06).
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (18:48), `REGRESSION_OK 414/414` (18:50).

## What is in the tree, per acceptance criterion
- **Derived offset, not a constant** - `Assets/_Modules/HUD/Kit/HudKitController.cs:5071` `HudRailClearance`:
  `want = max(BaseYFromTopPx, (mountTop - lowest) + GapPx)` in canvas-local reference px; sources measured via
  `UnionBottom` over active descendants (the expanded stack is a child of `_resGoldOnly.root`, `:2843`). Unsettled
  rects retry, never verdict; the canvas-bottom clamp emits `FlowTrace.Warn`. `RailGapPx` made `internal` (`:1744`)
  so there is one gap.
- **Both chips** - Collectors `:1886`, Builders `:1788`; each registers the other regardless of build order.
  Builders' non-collision is pinned byte-exact by `Editor/Regression/SessionShapeRegression.cs:309`.
- **Re-derive on toggle** - `SetResourcePanelOpen` marks both dirty (`:3797-3800`), because a descendant
  `SetActive` raises no `OnRectTransformDimensionsChange` on the chip's band.
- **Canon respected** - `RailChipWidthPx = 220f` (`:1739`), `RailChipHeightPx = ElarionUiKit.MinTouchPx` (`:1737`).
- **Regression** - `Editor/Regression/HudUiRegression.cs:1579` `CheckHarvestChipClearsResourcePanel`, registered at
  `DataRegression.cs:458`; three row counts (3/4/6), zero-overlap plus on-canvas assertions.

## Open, stated honestly
- [ ] **Criterion 1 is met in substance, not as written.** The ticket asked for a regression that measures laid-out
      rects; the shipped check is authored-anchor arithmetic (`HudUiRegression.cs:1559-1565` says so, because
      `DeNelle.EditorRegression.asmdef` does not reference `DeNelle.HUD` and batchmode runs no layout pass). A
      recorded deviation without a ruling - the owner decides whether it closes the box.
- [ ] The RED proof (84.2 / 112.0 / 112.0 ref px overlaps pre-fix) is stated in-file, not cited to a run log.
- [ ] The ticket's primary evidence (`Logs/device/screens/` harvest-overlap PNG) is not in the tree; the only
      candidate, `owner-screen-144143.png`, is the Manage/Build screen. Evidence link broken.
- [ ] Headless capture with the resource window open, opened and read.

Stale line cites in the ticket (pre-fix): `:1834`->1886, `:1758`->1788, `:1738`->1758, `:1720-1722`->1735-1739,
`:2754`->2842, `:2775`->2863.
