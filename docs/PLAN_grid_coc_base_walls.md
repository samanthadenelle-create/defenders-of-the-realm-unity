# PLAN — Grid-based CoC base + Wood/Iron/Steel wall ladder (Phase 1)

**Status:** DRAFT for owner review (2026-06-13). Reproducible-from-script, same as the castle today.
**North star:** Clash-of-Clans-style single-level base on a grid — segment walls, footprint buildings,
tiered wall upgrades. Phase 1 = builder-generated + data-driven; Phase 2 (post-grant) = player places/upgrades.

---

## Why (the wins)
- **Mesh simplicity:** one wall-segment mesh per tier, tiled one-per-cell — retires the bespoke wall-run
  scaling (`CloseSouthWallSeams`), the mirror-and-rotate, and the per-gate bake exclusions.
- **Perf:** identical segments are GPU-instanced (compounds the WO-410 work).
- **Deterministic nav:** each cell is walkable/blocked → clean navmesh; kills the "can't exit / gate seam" bug class.
- **Data-driven:** a wall cell stores only `{cell, tier}`; upgrade = swap mesh + bump HP. Same table pattern as the Forge upgrades.
- **Rails for player base-building** (Phase 2) — players lay the same cells the builder does.

## The grid
- **Cell size:** 4m (proposed; configurable). Castle footprint ~±65m → ~32 cells across — coarse enough to be
  CoC-readable + thumb-placeable on mobile, fine enough for wall runs. (2m = more granular but 4× the segments.)
- **Origin/orientation:** world origin, axis-aligned. Cell (col,row) → world `(col*4 - halfW, 0, row*4 - halfH)`.
- **Cell state:** Empty | Wall | Building-footprint | Gate-gap | Core(Heart). Single layer, all y≈0.

## Wall segments + the Wood → Iron → Reinforced Steel ladder
- **3 tiers, one segment mesh + material each:**
  | Tier | Mesh/look | HP (proposed) | Upgrade cost |
  |---|---|---|---|
  | 1 Wood | wood-plank segment | 300 | Wood |
  | 2 Iron | iron-banded segment | 800 | Iron |
  | 3 Reinforced Steel | tempered steel segment | 2000 | Iron + Crystals |
  - HP/cost are first-pass, tuned in one table. No Stone resource — steel = refined iron; the "reinforced" top
    tier leans on the Iron + Crystal (magic-temper) arc. Stays on the existing 4 resources.
- **Wall = a structure** with HP; enemies attack it and it blocks pathing until destroyed (CoC). Driven by a
  `WallProgression` table mirroring `ResourceBuildingProgression` (tier → HP, mesh id, upgrade cost).
- **Gates** = cells deliberately left as `Gate-gap` (no segment) → enemies path through. No bake exclusion needed.

## Buildings on footprints
- Each building occupies an NxN cell footprint (reuse `BuildingCatalog.footprint`: small=1×1, medium=2×2, large=3×3).
- The builder snaps the existing 8 storefronts + Heart/Tree to their footprint cells.

## Recipe → grid-map migration
- The castle recipe JSON becomes a **grid-map**: `{ cellSize, wallCells:[{col,row,tier}], buildings:[{id,col,row}], gates:[...], core:{col,row} }`.
- `CastleHubBuilder` reads the grid-map and instantiates: wall segments per `wallCells` (mesh by tier), buildings
  by footprint, Heart/Tree at `core`. Replaces the recipe-mirror + bounds-scale path.
- Migration is mechanical: convert the current castle's world positions → nearest cells once (a one-time
  `BlueprintToGridMap` editor step, confidence-gated per the spatial-blueprint pattern), then author from the grid-map.

## NavMesh
- Bake from the grid: walkable = Empty/Gate-gap/Core cells; blocked = Wall/Building cells. One connected sheet,
  no fragile fusion. The exit seam sits on a known walkable gate cell.

## Phasing + scope guardrails
- **Phase 1 (this plan):** builder generates the grid base from the grid-map; 3 wall tiers as data + 3 meshes;
  reproducible. NO player placement yet. Bounded, single-level, no new resource.
- **Phase 2 (post-grant):** build-mode lets the player place/upgrade wall cells + buildings on the grid (the real
  base-builder). Deferred per scope discipline (`scope-discipline-not-an-mmo`).

## Open items for owner
1. **Cell size** — 4m (proposed) vs 2m.
2. **Wall meshes** — need 3 segment meshes (wood/iron/steel). Use existing `Wall_Medieval_Stone` for one tier +
   source/retexture 2 more? Or 3 new low-poly segments. (Art call.)
3. **Upgrade granularity** — per-segment (CoC late-game) vs "upgrade all walls" one button (CoC early). Recommend
   start with **upgrade-all** (simpler UX + table).
4. HP/cost numbers above — first-pass, tune later.

## Build order (Phase 1)
1. `GridMap` data + `BlueprintToGridMap` converter (castle → grid-map, confidence-gated).
2. `WallProgression` table (tier → HP/mesh/cost) + 3 segment meshes wired.
3. `CastleHubBuilder` grid path: instantiate walls/buildings/core from the grid-map; nav bake from cell state.
4. Verify reproducible (rebake matches), enemies path gate→core, exit seam on a gate cell.
