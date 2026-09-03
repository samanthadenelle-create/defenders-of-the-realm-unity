# WORK ORDER 1310 - RESULT

**Status:** FIXED (edit-only lane; NOT gated, NOT committed - the lead owns both)
**Date:** 2026-09-02
**Silo:** UI / Talents
**Closes only on a fresh screenshot a human OPENED.** Headless gates cannot see layout; the
`UI_CAPTURE_OK` marker proves pixels were written, not that they read correctly. See "What proves it".

---

## Files changed

| File | What changed |
|---|---|
| `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` | axis rotation, lattice solver, content extents, rest scroll, node plate labels, layout constants, two stale headers |
| `Assets/Editor/Regression/TalentTreeShapeRegression.cs` | new rule 6 `[viewport]` - the measurement AND the assertion the header falsely claimed already existed |

Nothing else was touched. In particular `hero-talents.json` and its StreamingAssets twin are
untouched (the ticket is right - the authoring is sound), rules 1-5 of the shape oracle are
untouched, and the quick-swap binding is untouched.

---

## The defect

Two independent root causes produced the four reported symptoms.

**1. The layout threw the authored lattice away twice over.**

The authored data is a TIER GRID: `y` is the tier (a handful of values, base at the largest `y`),
`x` is the lane within the tier (five per tier). `RebuildTracks` rotated that into
`(progressX, trackY)` - progression onto the COLUMN axis, lanes onto the ROW axis. That is
backwards for a `1695 x 493` landscape well: the many axis (a dozen lanes) became rows and the few
axis (2-4 visible tiers) became columns, so every board resolved as a tall narrow corridor.
Measured on the live data, a full mage board came out **7.2 viewports tall against 1.9 wide**.

`SolveGraphLatticePx` then made it worse: it used the norms for **sort order only**. Rows were
clustered, and each row was laid out **independently and centred**, so a plate's x was its *index
inside its own row* and nothing else. A one-node row landed at the exact horizontal centre no
matter where it sat in the progression - the owner's *"one is middle of a skill tree"* - and the
no-prerequisite base nodes, which deliberately share one identical authored `y`, scattered across
different rows at different x. Finally `contentW = maxX + NodeFocusPx*0.5 + pad` measured from the
rightmost **centre**, so the rect kept the solver's LEFT centring margin and truncated the matching
right one, and a board narrower than the well rested flush left against a top-left pivot. That is
the dead black right-hand half, verbatim.

**2. The type badge could not physically fit its own band.**

`BuildNodeTypeBadge` sat over the skill art at x `0.02-0.68` - about 90 ref px, 82 after insets -
and called `FitSingleLine(label, 0f, FontMicro)`. `minSize: 0` does **not** mean "shrink freely":
`FitSingleLine` substitutes `ElarionUiKit.FontFloor` (**30**), not `FontHardFloor` (20). "PASSIVE"
at 30 px bold needs roughly 115 px, so the word ellipsised to three or four glyphs on every plate.
The rank pip was pinned across x `0.12-0.88` of the same band, overlapping the badge across
`0.12-0.68`, so a glyph of `0/1` painted through the truncated word. `AC1...` is `ACTIVE`
ellipsised with a rank digit on top of it - not a duplicated name. The RCA addendum's warning was
right: deleting it would have deleted the ACTIVE/PASSIVE/SLOT-N state, which is the colourblind-law
carrier.

---

## The fix

**Rotation** (`RebuildTracks`) - one line of substance: emit `(lane, progress)` instead of
`(progress, lane)`. Lanes now run across the WIDE axis and progression down the short one.
`progress` stays inverted, so the base rank is **row 0** - the rest position and the entry point
the owner was looking for. Deeper tiers scroll down under it.

**Solver** (`SolveGraphLatticePx`) - it now consumes the norm MAGNITUDES. Norm index 0 clusters
board-wide into COLUMNS and index 1 into ROWS, so two nodes with the same reading share a column
(or a row) *everywhere on the board*: the base rank lands on one level by construction. A column
may not seat two nodes on one row, so a collision takes the next free row - which is what makes the
WO-1021 Chebyshev pitch law hold by construction rather than by luck. The solver is deliberately
**axis-neutral**; the caller's rotation decides which semantic rides which axis, so the two existing
oracles that feed it raw authored x/y still get a legal solve.

**Clearance** - new `PlateClearPx = NodeFocusPx * 1.30` (218.4). Every inset is now half of THAT,
not half of `NodeFocusPx`: a focus plate also carries a `BuildOuterRing(0.10)` glow and a hung
nameplate, so an 84 px inset clips them at the `RectMask2D`. New `MinRowPitchPx` (302.4) and
`MinColPitchPx` (268.8) reserve room for the hung nameplate on both axes. **Both are strictly
greater than `MinNodePitchPx` (226.8) - this tightens the pitch law, it does not weaken it.**

**Extents** - `contentW/H` mirror the leading margin (`maxX + minX`) instead of truncating it, and
are floored at the well size so a small board centres in the viewport instead of resting flush left.

**Rest scroll** - a kept scroll is honoured only for a within-board tap; the first draw of a board
(`_lastLayoutSig == null`, i.e. a fresh Open or a different tree) snaps to the top-left entry
corner. Never mid-content.

**Labels** - the type word moved OUT of the plate's top band and onto the **second line of the
nameplate**, which is 1.56 plate-widths (about 212 ref px) and already hangs below the art. One
name per node, one type word per node, neither over the icon. `BuildNodeTypeBadge` is retained as
the builder (the token is a `SkillsPanelLayoutRegression` source law, and the information is the
colourblind carrier) - it was re-pointed, not deleted. Both it and the rank now pass
`ElarionUiKit.FontHardFloor` explicitly, and the rank owns the whole top band alone at x
`0.24-0.76`, so nothing overlaps anything.

**Stale canon corrected in place:** the `RebuildTracks` header claimed "No name labels under plates
(detail column owns the copy)" while building one under every plate against a body that has no
detail column; and `NodeLabelBandPx = NodeLabelGapPx = 0` read as "the pitch law reserves zero
height for the nameplate". Both now say what is true and point at `NamePlateHangFrac`.

---

## The gate hole (acceptance 5)

The ticket says the oracle "measures the content height and deliberately does not fail on it". The
RCA addendum is correct that it is worse than that: `TalentTreeShapeRegression` contained **no
reference to `MinNodePitchPx`, to any pitch, or to the 493 px well anywhere in the file**. The
header sentence claiming the fit question "is answered by a number on every gate run" was **false
documentation**. That sentence is now replaced by the thing it described.

New **rule 6 `[viewport]`**: for each class board (class nodes + the shared pool) it runs the
view's own rotation and the view's own public `SolveGraphLatticePx` against the reference
`1695 x 493` well, then FAILS on

- any plate closer to the content origin than `PlateClearPx * 0.5` (the top-clip), and
- a board past `MaxScrollWide = 3.0` / `MaxScrollTall = 6.0` viewports.

Both numbers are also written to `notes`, pass or fail, so drift is visible before it is a failure.

**The new rule fails the shipped defect**, which is the only proof that it is a gate and not
decoration: the retired rotation resolved mage/ranger at 7.19 viewports tall (budget 6.0) and the
retired solver inset every plate at 84 px against a 109.2 px clearance. Both conditions fire.

---

## What proves it

**Arithmetic replay of the shipped solver** over the real `hero-talents.json`, at the reference
well, checking the Chebyshev pitch law and the clearance inset on every pair:

| board | cols x rows | tightest pitch | law | inset | content | viewports |
|---|---|---|---|---|---|---|
| full knight | 13 x 8 | 268.8 | 226.8 | 109/109 | 3528 x 2447 | 2.08 x 4.96 |
| full ranger | 12 x 7 | 268.8 | 226.8 | 109/109 | 3259 x 2145 | 1.92 x 4.35 |
| full mage | 12 x 7 | 268.8 | 226.8 | 109/109 | 3259 x 2145 | 1.92 x 4.35 |
| day-1 frontier mage | 6 x 2 | 268.8 | 226.8 | 109/109 | 1695 x 633 | **1.00** x 1.28 |
| day-1 frontier ranger | 7 x 3 | 268.8 | 226.8 | 109/109 | 1915 x 935 | 1.13 x 1.90 |
| day-1 frontier knight | 9 x 2 | 268.8 | 226.8 | 109/109 | 2453 x 633 | 1.45 x 1.28 |
| `TalentFocusSingletonRegression` Case 3 fixture (7x5 + a duplicated point) | 5 x 8 | 302.4 | 226.8 | 109/109 | - | - |
| `SkillsPanelLayoutRegression` Case 3 fixture (all 83 authored, raw x/y) | 12 x 11 | 268.8 | 226.8 | 109/109 | - | - |

The day-1 mage frontier - what a new player actually opens - now consumes **exactly the full well
width**, against a board that previously squeezed into a centre-left column.

**Sec.12 instrumentation, permanent:** `RebuildTracks` now emits a change-gated
`[Flow:SkillTree] graph insets: topLeft=.../... bottomRight=.../... vs clearance ...` line beside
the existing spacing probe, and `FlowTrace.Fail`s it as `CLIPPED:` when any margin is short. The
clip question is answered by a number on every draw instead of by the owner's eyes.

**Not run here (lead owns them):** `CompileGate`, `DataRegression`, `UI_CAPTURE_OK`. Brace and NUL
checks passed on both files (`BALANCED clean`).

**STILL REQUIRED TO CLOSE: a fresh capture PNG that a human OPENED** on each of the three trees.
Every claim above is geometry; only a screenshot proves the badge word renders unellipsised, that
the nameplate does not collide with the row below at the shipped font, and that the board reads as
a tree. Acceptance 6 is explicit that `UI_CAPTURE_OK` alone is not that proof.

---

## Deliberately NOT touched

- `Assets/Resources/Data/Canonical/hero-talents.json` and its StreamingAssets twin.
- `TalentTreeShapeRegression` rules 1-5 (`[authoring]`, `[base]`, `[widen]`, `[graph]`, `[hidden]`).
  Rule 6 is additive.
- `MinNodePitchPx` / `MaxPitchSpreadMul` / `NodeFocusPx` / `NodeSizePx` - the WO-1021 separation
  law. The new pitch floors sit above it.
- Any shared `ElarionUiKit` file. `FitSingleLine`'s `FontFloor` default is arguably the wider bug
  (a `minSize: 0` caller silently gets 30, not the hard floor of 20, and ellipsises), but it is a
  shared kit file other lanes touch. **Flagged for the lead, not edited here.** Every call site in
  this panel now passes its floor explicitly instead.
- The quick-swap binding (Thunderbolt -> slot 1) and `BuildNextTrackMarker`.
- `TalentFocusSingletonRegression` and `SkillsPanelLayoutRegression`; both were read and both still
  hold against the new solver (see the table).
