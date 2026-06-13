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
1. **Cell size — DECIDED: 1.0m** (= the wall segment's native width, per the owner's tier art dims
   1.0 W × 1.5 H × 0.2 thick). Grid snaps to 1m so segments tile seam-to-seam with zero scaling.
2. **Wall meshes — RESOLVED (owner-provided, 2026-06-13):** three distinct tier meshes (true silhouette ladder):
   - **Tier 1 Wood** = wood palisade (pointed logs + stone footing).
   - **Tier 2 Iron** = plank wall with riveted iron bands (the dimensioned 1.0×1.5×0.2 segment).
   - **Tier 3 Reinforced Steel** = steel plates with glowing blue RUNES + gold trim — the runic glow IS the
     Iron+Crystals magic-temper tier (narrative lands: smelt iron → rune-temper with crystals).
   PENDING: get the FBX/prefab files into the project (path TBD) + URP/Tripo material pass on import, then wire
   each as `WallTierData.straightPrefab[tier]`.
3. **Upgrade granularity — DECIDED (owner 2026-06-13): exactly TWO modes.**
   - **Single cell** — tap one wall segment, upgrade it a tier (cost = that cell's next-tier cost).
   - **Entire wall** — one action upgrades ALL wall cells a tier (cost = sum of each cell's next-tier cost; cells
     already at the target tier are skipped). Mixed-tier walls level up toward uniform.
   No side/ring or drag-select grouping. Data model is trivial: single = the tapped cell; entire = every Wall cell.
4. HP/cost numbers above — first-pass, tune later.

## Build order (Phase 1)
1. `GridMap` data + `BlueprintToGridMap` converter (castle → grid-map, confidence-gated).
2. `WallProgression` table (tier → HP/mesh/cost) + 3 segment meshes wired.
3. `CastleHubBuilder` grid path: instantiate walls/buildings/core from the grid-map; nav bake from cell state.
4. Verify reproducible (rebake matches), enemies path gate→core, exit seam on a gate cell.
