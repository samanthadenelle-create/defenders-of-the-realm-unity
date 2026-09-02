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
