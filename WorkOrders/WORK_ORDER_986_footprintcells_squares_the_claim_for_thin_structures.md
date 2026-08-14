# WORK ORDER 986 — `PlacementGrid.FootprintCells` squares the grid claim, so every THIN structure over-claims on its narrow axis

**Status:** SPEC — NEEDS AN OWNER CALL (do not implement without one)
**Minted:** 2026-08-14 (CLI)
**Silo:** Build mode / placement grid
**Surfaced by:** verification of WO-972 (walls cannot be built beside each other)

---

## What was found

While confirming WO-972's fix was really in the tree, the grid's claim maths was read at source:

```csharp
// PlacementGrid.cs:235-238
int cells = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, m) / cellSize));
return new Vector2Int(cells, cells);          // <-- ONE scalar, squared
```

```csharp
// StructureFactory.cs:693
result = Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z));   // <-- depth discarded
```

The mesh is collapsed to a **single scalar** (the larger of x and z), and the grid then claims a
**square** of that size. A wall's real 1.42 m depth is thrown away before the grid ever sees it.

## What WO-972 actually fixed — and what it did not

WO-972 made walls placeable side-by-side by feeding the claim a **different metric**: the authored
catalog footprint (`wall_wood` / `wall_stone` `fp = 2.1`), so `Ceil(2.1 / 3) = 1` cell. Verified in
both catalog copies. It also holds at diagonal yaw — the `|sin| + |cos|` inflation at
`PlacementGrid.cs:262-264` gives `2.1 x 1.414 = 2.97 m < 3`, still 1x1.

**It did not fix the squaring.** It routed around it, for `CatalogType.Wall` rows only.

Consequence: **any other structure whose mesh overshoots one axis by even 1% still claims a square
block on its thin axis.** Fences, banners, market stalls, signage, any long-and-narrow prop — each
one silently reserves ground it does not occupy, and the player experiences it as "why can't I put
these next to each other", which is the exact complaint WO-972 came from.

## Why this is a SPEC and not a READY ticket

A proper fix means threading a **non-square `(x, z)` footprint** through:

- `PlacementGrid.FootprintCells` and every caller,
- the occupancy map,
- the yaw-inflation path (`:262-264`) — a non-square footprint rotates into a *different* non-square
  footprint, which the current scalar maths cannot express,
- **and every saved layout's occupancy replay.**

That last one is the expensive part: existing player saves replay their layout through the grid. A
footprint change alters what those layouts claim on load, so it is not a pure code change — it has a
migration shape. It touches every placeable structure in the game.

**The question for the owner is therefore not "is the squaring wrong" (it is), but "does it hurt in
play anywhere other than walls?"** If the only thin structures players place adjacent are walls, WO-972
already bought the whole benefit and this should stay unimplemented on purpose. If fences/stalls/banners
are meant to line up too, this is worth the migration.

Decide that before anyone pays for it.

## What NOT to do

- ⛔ Do **not** extend the WO-972 workaround by hand-authoring `fp` values for more catalog rows.
  That is a second copy of the real footprint, kept in sync by hand, and it will drift — the same
  duplicated-state failure as the stale WO-number block and the hardcoded repo root (CLAUDE.md §0/§2).
  ARCHITECTURE_PRINCIPLES §4: derive, don't hand-author.
- ⛔ Do not change `FootprintCells` "just for a few more types". A partial non-square path is worse
  than a consistent square one, because the yaw inflation stops being correct for the mixed set.
- ⛔ Do not touch `OutpostFoundationGenerator.cs:322`. It uses the raw mesh measure to size a
  **NavMesh carve**, not a grid claim — that is correct as-is, and shrinking a carve is explicitly
  forbidden by WO-972's constraints.

## Files that would be in scope (if approved)

- `Assets/_Modules/Village/BuildMode/PlacementGrid.cs` (`FootprintCells`, yaw inflation, occupancy)
- `Assets/_Modules/Village/BuildMode/StructureFactory.cs` (`:693` — stop collapsing to one scalar)
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` (claim/preview consumers)
- `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs` (**saved-layout occupancy replay — the
  migration risk lives here**)
- `Assets/Editor/Regression/WallAdjacencyRegression.cs` (extend beyond walls)

## Related, already fixed

`StructureCardVM.FootprintFor` was the last reporter still reading the *old* metric, so a wall's info
card said **"2x2 cells" while placement claimed 1x1** — the UI contradicting the grid. Fixed
2026-08-14 by deriving from the single claim authority (`MeasureClaimFootprintMetres`). That fix is
independent of this ticket and does not depend on the decision above.
