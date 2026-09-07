# WO-1480 RESULT - the ninth hardcoded structure ceiling is gone; the divisor is derived

**Status:** IMPLEMENTED IN THE TREE, NOT YET GATED. Acceptance 4 is open.
**Commit:** none - uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files:** the ticket names `Assets/_Modules/Village/Buildings/WallSegment.cs`; **that path does not exist**.
The file is `Assets/_Modules/Village/Walls/WallSegment.cs` and that is what was edited.
- `WallSegment.cs:157` - `public static int MaxTier => RepoProps.MaxStructureLevel;` (new); `:181` -
  `SetTier` now clamps `1..MaxTier`, was the literal `1..3`; `:46` adds `using DeNelle.Core.Catalog;`.
- `WallSegment.cs:106` + `:165-169` - the tabled `s_tierToughness = { 1f, 1f, 1.6f, 2.56f }` is replaced by
  `TierToughnessStep = 1.6f` and `ToughnessFor(int tier)` = `Mathf.Pow(step, t-1)`.
- `WallSegment.cs:333-334` - the damage path clamps to `MaxTier` and divides by `ToughnessFor(t)`; `:203` -
  `ApplyTierBlockerHeight` dropped its own `Mathf.Clamp(_tier - 1, 0, 3)` literal.
- `Assets/Editor/Regression/BuildEconomyRegression.cs:1427-1520` - new `[wall-tier-ceiling]` case: fails if
  `WallSegment.cs` re-introduces a literal ceiling (`:1518`) or stops naming `RepoProps.MaxStructureLevel`
  (`:1519-1520`).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds (UI-MVVM violation on `BuildPreviewModal.cs:252-253`; hollow-pass at
`NightMarketNoWalletRegression.cs:761`) were fixed at source in `eb161dc98` (20:10), AFTER both logs. Neither
log postdates `eb161dc98` or the working tree, so the wave-two gate is owed. Measured:
`grep -c wall-tier-ceiling Builds/reg-quiet.log` returns **0** - the new case has never executed.

## What landed

The derived step reproduces the retired table exactly at the tiers that existed - x1 / x1.6 / x2.56 - so no
live wall changes behaviour today (`wall_wood` authors `maxLevel: 2`). Section 3 was honoured: the literal was
not raised from 3 to 6, it was removed. The height path now leans on `WallDefense.TargetHeight`'s own
authored clamp rather than restating a bound on this side.

## Acceptance

- [x] `SetTier` reads the ceiling from `RepoProps` - `WallSegment.cs:181` via `MaxTier` at `:157`.
- [x] Divisor defined for every admissible level - `ToughnessFor` at `:165-169` is continuous over `1..MaxTier`.
- [x] Literal-ceiling regression widened - `BuildEconomyRegression.cs:1427-1520`.
- [ ] RED proof stated - the case's own red path is documented in source, but **no red run is on file**; it
      has not executed once.
- [ ] `REGRESSION_OK n/n` on a fresh log - **not run** (see the gates line).

**Still needs a device capture:** none. This is a data-and-logic change; the wave-two regression run is the
whole remaining proof. A device capture only becomes relevant once a wall row authors `maxLevel` above 3.
