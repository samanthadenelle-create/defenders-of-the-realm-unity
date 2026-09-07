# WO-1478 RESULT - the ghost pill reads the live catalog row, and a ninth oracle case forbids the next literal

**Status:** FIXED in the working tree. All six files scrubbed, and the durable half - an oracle that
fails an editor-authored cost - is built.
**Commit:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate. The
`UICaptureLaunch.cs` change is entirely uncommitted: `eb161dc98` (20:10) also touched that file
(+79/-7) but for the WO-1411 and WO-1408 lanes, not for this one. Measured by diffing the commit and
the working tree separately; only the working-tree diff carries the `SetPlacingLabel` change.
**Files:**
- `Assets/Editor/UICaptureLaunch.cs:4580-4619` - the literal
  `SetPlacingLabel("Arcane Spire", "88 wood, 88 iron, 187 crystals")` is deleted. The pill now hydrates
  the catalog (`HydrateCatalogForCapture`), fetches `CatalogRegistry.Get("tower_arcane_spire")`, and
  formats through `StructureCardVM.PlacementSummaryFor(ghostEntry).CostWords` - the same seam the ghost
  paints with in play. An absent row LOGS AN ERROR and skips the case rather than inventing a price
  (`:4600-4605`); an EMPTY price warns and proceeds, because a costless pill is the authored
  first-build freebie (WO-1010 D20), not a broken capture (`:4610-4615`).
- `Assets/Editor/Regression/CostBasketSeparationRegression.cs:334-357,581-708` - CASE 8
  `[capture-basket]`: no `SetPlacingLabel(` call in `Assets/Editor/UICaptureLaunch.cs` may pass an
  authored cost string. Comments are stripped first, so the historical note left in the harness does
  not trip it.
- Scrubbed: `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs` docstring;
  `WorkOrders/WORK_ORDER_1010_build_ui_carousel_minimize.md`;
  `WorkOrders/WORK_ORDER_1411_build_never_says_what_you_can_afford.md`. The two review documents got a
  one-line CORRECTION banner with the body left frozen per CLAUDE.md section 15 -
  `docs/qa/UI_REVIEW_2026-09-05/REVIEW_A_independent.md` and `REVIEW_MERGED.md` (row 10's quoted
  confirm line is now `<live cost words> . <build time> . Builder free`).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed. Case 8 has therefore NEVER RUN.

## Acceptance

- [x] Zero hits for `88 wood` repo-wide. Measured this session across `Assets/`, `WorkOrders/` and
      `docs/`: the only two hits are inside this ticket's own WO file, at `:14` (the quoted evidence)
      and `:46` (the acceptance line).
- [ ] The capture paints the authored row; a fresh Build PNG opened showing the iron-only cost. NOT
      CAPTURED - `UI_CAPTURE_OK` has not been run since the change, and no PNG has been opened.
- [x] `CostBasketSeparationRegression` covers editor-authored cost strings - case 8 at `:581`.
- [ ] `REGRESSION_OK n/n` on a fresh log. Owed with the wave-two gate; the RED proof for case 8 is also
      unstated.

Needs no device capture. It needs one `UI_CAPTURE_OK` run with the Build ghost PNG opened, and the
wave-two regression gate so case 8 executes for the first time.
