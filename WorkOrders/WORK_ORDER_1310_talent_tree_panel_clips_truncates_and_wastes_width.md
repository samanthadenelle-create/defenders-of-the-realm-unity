# WORK ORDER 1310 — The talent tree panel clips its content, truncates node names, and wastes half its width

**Status:** READY TO IMPLEMENT
**Silo:** UI / Talents
**Minted:** 2026-09-02 (CLI) from an owner screenshot, felt-test of the 03:33 Windows build.
**Severity:** P1 — this is the screen the retention work funnels players into.

## Owner report

> **"tree looks wrong."** (with screenshot), and earlier the same session:
> **"im having an issue in skills tree. THere should be a 'starting point' where these are the ones i
> can learn first, but they should visually be in same level to denote that. one is middle of a skill
> tree"** — she was on the MAGE tree for that one.

⭐ **THE DATA IS NOT THE PROBLEM — DO NOT "FIX" hero-talents.json.** That was checked at source:
every tree's no-prerequisite base nodes already share one identical `y` (knight 0.68, ranger 0.64,
mage 0.64, shared 0.98), and `TalentTreeShapeRegression` rules 1-5 pass. The authored lattice is
sound. **This is the VIEW.**

## What the screenshot shows (four distinct defects)

1. **Content CLIPPED at the top.** `AETHER BOND` and `GUARDIAN STANCE` are sliced through by the
   panel's top edge — half a node visible, its icon cut.
2. **Node names TRUNCATE**: `SLI…`, `AC1…`, `N…`.
3. **Labels OVERLAP their icons.** A truncated name is drawn ON TOP of the node art while the full
   name repeats below it (visible on Thunderbolt: `SLI…` over the icon, `THUNDERBOLT` beneath).
4. **Half the panel is empty.** The lattice is squeezed into a narrow centre-left column while the
   entire right-hand side is dead black space. The layout is not using the width it has been given.

⭐ WORKING, do not regress: Thunderbolt is the base node, purchasable with the first Wisdom point, and
it correctly lands in **quick-swap slot 1** ("Thunderbolt -> quick-swap 1."). That is the WO-1305-era
retention fix behaving exactly as intended.

## Why the gate did not catch it

`Assets/Editor/Regression/TalentTreeShapeRegression.cs` — its own header: *"WHAT IS MEASURED AND
LOGGED, never failed: the row census per tree and the implied content height at
`HeroSkillTreePanelMvvm.MinNodePitchPx`, so the 'does the tree fit' question..."*

**The oracle computes the content height and deliberately does not fail on it.** A tree that cannot
fit its viewport therefore passes. That is the gap this ticket must close, not just the pixels.

## The seam

`Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs`:
- `NodeFocusPx`, `MinNodePitchPx = NodeFocusPx * 1.35f` (~226.8), `MinLatticeWpx` / `MinLatticeHpx` (`:202-211`)
- `ResolveGraphNorms(seats, norm)` (`:500`) — the authored 0..1 x/y -> seat mapping
- the pitch check at `:692-693` (`pitchBroken = minGapPx < MinNodePitchPx - 0.5f`)

Establish AT SOURCE and report: does the lattice map x across the FULL available width, or into a
fixed/again-fractional sub-rect? Is the viewport scrollable, and if so is it starting scrolled
mid-content (which would explain the top clip)? Is the on-icon label a separate element from the
under-icon label, i.e. is one of them redundant?

## Acceptance criteria

1. No node is clipped by the panel edge at any captured resolution. If content legitimately exceeds
   the viewport, it SCROLLS and starts at the base row (the entry point the owner is looking for) —
   never mid-tree.
2. No node name ellipsises. Give the label room or reflow it; do not shrink to the font floor.
3. One name per node. Remove whichever of the overlapping labels is redundant.
4. The lattice uses the panel's width. Empty right-hand space is a layout bug, not a style.
5. **Close the gate hole:** `TalentTreeShapeRegression` must FAIL when the implied content height
   exceeds the viewport at the panel's own constants, instead of measuring and shrugging. A measured-
   and-never-failed value is how "fits" and "does not fit" read identically at the gate.
6. Verified by a fresh capture PNG that a human OPENED. `UI_CAPTURE_OK` proves pixels were written;
   the same marker was green over a wave-clear panel carrying four visible defects the same night.

## What NOT to touch

- ⛔ `Assets/Resources/Data/Canonical/hero-talents.json` and its StreamingAssets twin. The authoring is
  correct and was re-verified today; the base rows already share a y and all shape rules pass. A view
  bug fixed in the data is a data bug shipped.
- ⛔ Do NOT weaken `TalentTreeShapeRegression` rules 1-5 (`[authoring]`, `[base]`, `[widen]`, `[graph]`,
  `[hidden]`). Rule 2 `[base]` caught a real violation today and the owner explicitly declined to relax
  it. You are ADDING a viewport assertion, not loosening the shape law.
- ⛔ Do not change the quick-swap binding — it works.
- ⛔ Colour alone must never carry meaning; the owner is red/green colourblind.

---

# RCA ADDENDUM — 2026-09-02 (read-only agent). The gate does not "measure without failing" — it NEVER MEASURES.

The screen is `HeroSkillTreePanelMvvm` (code-built uGUI via `ElarionUiKit.BuildModalCanvas`,
`BuildChrome` `:1640+`). `TalentTreePanel.cs` is a separate legacy surface and is NOT this screen.

## Root cause of the wasted width: THE SOLVER THROWS AWAY THE AUTHORED POSITIONS

`SolveGraphLatticePx` (`HeroSkillTreePanelMvvm.cs:867-943`) uses the authored norms **only for sort
order and row clustering**. Placement is then per-row and independent:
```
colPitch = clamp((boxW - NodeFocusPx)/(k-1), MinNodePitchPx, MinNodePitchPx*1.9)   (:930-935)
xLeft    = half + max(0, (boxW - NodeFocusPx - blockW) * 0.5f)
```
Each row is laid out **independently and centred**, so a node's x is its **index within its own row**
and nothing else. A one-node row lands at the exact horizontal centre regardless of progression -
which is the owner's "one is middle of a skill tree".

That interacts fatally with the axis rotation at `:502-527`: `trackY = normalised authored x` and
`progressX = 1 - normalised authored y`. Rows are therefore clustered on **authored x**
(`RowClusterNorm = 0.055`, `:213`) - so the base nodes that deliberately share one identical authored
`y` (the exact property `TalentTreeShapeRegression` rule 2 `[base]` enforces, and which this ticket
correctly says is sound) get **scattered into different rows** because their x differs. `progressX` is
then discarded entirely. Many rows of 1-2 centred nodes = a narrow vertical column.

**Why the dead half is on the RIGHT rather than symmetric:** `:571-573` computes
`contentW = maxX + NodeFocusPx*0.5 + pad` where `maxX` is the rightmost **centre** - so the content
rect includes the solver's LEFT centring margin but truncates the right one. Content is top-left
anchored/pivoted (`:1999-2000`) and rested flush left (`:650-656`). `GraphColumnX1`/`DetailColumnX0`
(`:251`,`:253`) are DEAD constants - the graph well is full body width (`:1693-1697`), so the black
right side is not a detail column.

**Secondary:** the first `RebuildTracks` runs from `Bind -> Render` before layout, so `wellRt.rect` is
0 and it solves against the `GraphUnitWpx 1180 / GraphUnitHpx 780` fallback (`:536-544`), warning to
FlowTrace. `RebuildTracks` only re-runs on `_vm.Changed`, so the board can stay solved against the
fallback box for the panel's whole life. Check the felt-test log for "graph well not laid out yet".

## ⚠ Defect 3 is MISDIAGNOSED — do NOT delete "the redundant label"

The over-icon text is **not** a duplicate name. Three things are built in the plate's top band:
- `BuildNodeTypeBadge` (`:1320-1337`): x `0.02-0.68`, y `0.72-0.98`, text `SLOT N`/`ACTIVE`/`PASSIVE`
- the rank pip (`:1156-1159`): x `0.12-0.88`, y `0.72-0.96` - **it OVERLAPS the badge across x 0.12-0.68**
- `BuildNodeNamePlate` (`:1339-1359`): the full name, hung BELOW the plate

So `AC1...` is the badge word `ACTIVE` ellipsised **plus a glyph of the rank pip `0/1` sitting on top
of it**. Truncation source: `FitSingleLine` sets `overflowMode = Ellipsis` with `fontSizeMin` clamped
up to `FontHardFloor = 20` (`ElarionUiKitObsidian.cs:3054-3070`), and the badge band is only
`0.66 x 136 ~= 90` ref px - too narrow for `PASSIVE`/`SLOT 1` at 20px bold.

**Deleting the badge would delete the ACTIVE/PASSIVE/SLOT-N information, which is itself the
colourblind-law carrier** ("survives greyscale", `:1325-1326`). The fix is the band width and the
rank/badge collision - not a deletion.

Two stale-comment traps: `:445` still claims "No name labels under plates (detail column owns the
copy)" while `:1165` builds one; and `NodeLabelBandPx = NodeLabelGapPx = 0f` (`:236-237`) means the
pitch law reserves **zero** height for that nameplate.

## Top clipping — NOT deterministically provable from source

The panel is destroyed and rebuilt on every `Open` (`:340-342`, `:2119-2123`), so scroll starts at 0.
Candidates in order:
1. Solver top inset `half = NodeFocusPx*0.5 = 84` (`:923`) exactly equals the focus plate's
   half-height, so a focus plate in row 0 sits flush with the content top and its
   `BuildOuterRing(0.10f)` glow (`:1091-1093`) is clipped by the `RectMask2D`.
2. `contentH = maxY + half + pad + RankBandPx` (`:572`) adds the 28px rank allowance to the **BOTTOM**,
   while the rank band sits on the **TOP** of every plate (`:227-228`). The reservation is on the
   wrong side.
3. A within-session scroll preserved across a `Select` rebuild (`keptScroll` `:451`, `:650-656`).

Settle with a fresh screenshot or the FlowTrace "sparse graph drawn: ... content WxH px" (`:661-666`).

## ⛔ ACCEPTANCE 5 IS BIGGER THAN THIS TICKET SCOPES

This ticket (and my own earlier note) says the gate "measures content height but never fails on it".
**That is wrong. It never measures at all.** `Assets/Editor/Regression/TalentTreeShapeRegression.cs`
contains no reference to `MinNodePitchPx`, to any pitch, or to the 493px well anywhere in its 319
lines. The only `notes.Add` is the row census at `:262-263`. The header claim at `:44-46` - that "the
implied content height at `HeroSkillTreePanelMvvm.MinNodePitchPx` ... [is] answered by a number on
every gate run" - is **FALSE DOCUMENTATION**. You are adding the measurement AND the assertion.

The file only reads `x/y/prereqs/cost/hidden` and delegates pixel geometry to
`SkillsPanelLayoutRegression [grid]` (`:49-52`). Since `SolveGraphLatticePx` is `public static` and
Unity-free (`:867`), the viewport assertion belongs there, or in a new oracle that also exercises the
**rotation at `:502-527`** - which no oracle touches today, and which is where the authored-lattice
guarantee is destroyed.

## Files a fix would touch

`HeroSkillTreePanelMvvm.cs` `:502-527` (rotation vs authored rows), `:867-943` (consume norm
magnitudes, not just order), `:571-573` (symmetric extents), `:1156-1159` + `:1320-1337` (rank/badge
collision + band width), `:236-237` + `:1165` (reserve pitch for the nameplate or drop it); and
`Assets/Editor/Regression/TalentTreeShapeRegression.cs` (add the measurement its header already claims).
