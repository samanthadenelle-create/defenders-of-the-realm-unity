# WORK ORDER 594 — Measure-driven castle base (bottom / top / perimeter)

**Status:** CAPTURED (idea) — not yet ready to implement
**Date:** 2026-07-01
**Priority:** P3 (architecture polish; follow-on to WO-593)
**Owner:** Samantha (idea) · Author: CLI
**Slot into:** world/environment lane, after WO-593 (castle island raise) is banked.

## The idea (owner, 2026-07-01)
Instead of any hardcoded castle dimensions, a **scripting editor tool** should be smart enough to
**MEASURE the base's bottom, top, and perimeter** from the actual geometry, and derive everything from it:
- **base = the footprint** (single source of truth, at the tunable `castle.liftY` height — WO-593).
- **walls / structures / tree build ON the base** (base + offset) — "move base, all moves."
- **walkable area = base perimeter − wall width** (inset by the wall thickness) → drives the nav floor.
- **bottom / top** = the base's vertical bounds → drives the terrain plateau (bottom) and the floor (top).

## Why it's the right direction
- It removes the last hardcoded extents (footprint half-widths, floor size, plateau radii) — the exact
  "hardcoding bites us" failure mode the owner called out. One measured base → everything derived.
- The codebase **already has this pattern**: `CastleWallsFromRecipe.CloseSouthWallSeams` *measures the
  real world-space renderer bounds — "never guess a length"* — to fill wall seams. This generalizes that.
- Data-driven + self-describing — fits "owner thinks in data structures."

## Sketch (not a spec yet)
- A `CastleBase` measurer: compute the AABB (bottom Y, top Y) + the XZ perimeter of the base footprint
  from the placed wall/floor renderer bounds (not from consts).
- Derive: nav-floor extent = perimeter inset by measured wall width; terrain-plateau half-extent = the
  perimeter; plateau falloff at the moat ring.
- Feed those into the WO-593 builders so the raise + walkable area + terrain all read the measured base.

## Prereq
WO-593 (castle island raise via the `CastleFootprintLiftY` base variable) landed + felt-verified first.
This WO replaces the remaining hardcoded extents with measured ones — do it once the raise is proven.
