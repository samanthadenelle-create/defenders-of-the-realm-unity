# RESULT — WO-986 CoC non-square footprints

**Status:** IMPLEMENTED — 2026-08-15  
**PO felt-verify:** pack thin props / walls side-by-side; diagonal yaw still honest.

## Change

| Piece | What |
|-------|------|
| `StructureFactory.MeasureUprightFootprintXZ` | Mesh world AABB `(size.x, size.z)` — no max collapse |
| `MeasureClaimFootprintXZ` | Non-wall = measured XZ; **Wall** = authored footprint both axes (WO-972 one-cell tile) |
| `PlacementGrid.FootprintCells(Vector2)` / `(Vector2, yaw)` | Independent cells + rotated rectangle AABB |
| `BuildModeController` / `BaseLayoutLoader` / `StructureCardVM` | All claim via XZ path |

## Save safety

Prior square-of-max claims were **≥** new thin claims. Load **frees** phantom cells; never expands into occupied ground.

## Legacy

`FootprintCells(float)` square overload kept for StrategicPlacement / BuildEconomy regressions.
