# Catalog System — types, parts, and build granularity

> Owner (2026-05-30): "a real catalog… determine how many types… wall / stairs / room, each with
> prefab-selection parts, so you can isolate a **single cell OR a completed prefab**." This is the
> concrete `Catalog` half of the **catalog ⊥ repo** model (look vs behavior) that feeds build-mode.
> Pairs with `WORLD_ENGINE_ARCHITECTURE.md` (dispatcher/NavSurface) + `build-mode-architecture.md`.

## The key insight — two grains of building

The catalog serves **both** levels of granularity, the player picks per placement:

- **Cell** — one snap-grid unit: a single wall tile, one stair, a floor square. Precise (Sims/Minecraft grain).
- **Composite** — a completed prefab = a **pre-arranged bundle of cells**: a Room = floor + 4 walls + door.
  Fast (CoC grain) — drop a whole structure at once.

A Composite is authored *as cells*, so the engine treats everything uniformly: placing a composite =
placing its cell set. So a player can **drop a room, then isolate and edit a single cell of it** — one
system, two grains.

## Type taxonomy (the palette tabs) — start with the structure core

| `CatalogType` | Cells (parts) | Composites |
|---|---|---|
| **Wall** | `Wall_Stone_3x3` A/B/C, `Wall_Wood`, corners, gate-gap | wall run · corner bastion |
| **Stairs** | a stair cell (+ its `NavSurface` plank) | full ramp → rampart |
| **Floor** | `Terrain_Plane_*`, plaza/road tile | room floor pad |
| **Room** | — | floor + walls + door, pre-arranged |
| **Tower** | `Tower_Castle_Round/Square` | — |
| **Gate** | `Gate_Medieval_*` | gatehouse |
| **Resource** | crystal node, mine, well | — |
| **Decoration** | props, banners, torches | courtyard set |

**Start with Wall · Stairs · Floor · Room** — the "build a structure" core; the rest layer in.

## Data model

```
enum CatalogType { Wall, Stairs, Floor, Room, Tower, Gate, Resource, Decoration }

CatalogEntry {
  id, displayName, type
  kind: Cell | Composite
  visual : prefab ref          // LOOK (catalog half) — polyperfect prefab / skin / cosmetic
  repo   : RepoPropsRef        // BEHAVIOR (repo half) — NavSurface kind, footprint, build cost,
                               //   snap/rotation rules, ownership-gate
  composite : CellPlacement[]  // Composites only: cell-entry id + grid offset + rotation
}
```

- **Cell** entry = one prefab + its repo props (incl. its `NavSurface`). Occupies one grid unit.
- **Composite** entry = a list of cell placements (relative grid offsets + 90° rotations). Drops as a bundle.
- A **purchased cosmetic** is a `CatalogEntry` with an ownership-gate — swaps `visual`, keeps `repo`
  (so it's a re-skin, never a power change — the structural cosmetic-only guarantee).

## How it feeds build-mode

The build palette **is** the catalog, tabbed by `CatalogType`. Select an entry (cell or composite) →
ghost preview → free-place at the cursor + **rotate** + **edge-snap to neighbouring pieces** (no grid) → `dispatcher.Build(entry, RuntimePlaced)`
instantiates visual + `NavSurface` (and for composites, each cell, carving NavMeshObstacle so enemies
re-path live). Granularity is the player's choice on every placement.

## Placement conditions + snap (models: CoC placement, Fallout 4 builder)

Each entry's **repo** carries **`PlacementRules`** — the conditions evaluated the instant the player
selects it, so the ghost reads **valid (green) / invalid (red)** before commit. CoC's "select item →
run the conditions for where it can be placed" is precisely this. Rules are **data**, so every item —
base section, player-built object, or purchased cosmetic — carries its own valid-placement logic:
**test the rules once, placement is correct everywhere** (and it's the antifragile surface — a bad
placement nobody anticipated becomes a new rule, enforced for all entries).

`PlacementRules` examples: no-overlap (footprint) · must-sit-on (terrain/biome type) · adjacency
(stairs require a wall top) · distance/zone (clearance from gates) · support (needs a floor under it) ·
affordable (cost) · owned (cosmetics). The rule is *"does this naturally work **here**"*, evaluated at
the free cursor position — not "which grid cell."

**NO GRID — free / organic placement (Fallout 4 · Valheim grain).** The owner explicitly does not want
grid-snap. Pieces place **freely** at the cursor (continuous position + rotation) and **snap to each
other's connection points** — a wall's edges, a floor's perimeter, a stair's top/bottom — so structures
assemble *wherever they naturally fit*: a wall clicks onto a floor edge, stairs onto a wall top. The
ghost is valid the moment `PlacementRules` pass, anywhere — no cell lock. That edge-snap-to-neighbours
(not snap-to-grid) is what makes building *feel* good. A **Composite is just a pre-snapped connector
graph** of cells.

Reuse: `TowerPlacementSystem` already does ghost + overlap + cost; `ValidateBuildingGateClearance` is a
distance rule. The engine generalizes these scattered checks into **per-entry `PlacementRules`** the one
placement system evaluates — so all the rules become declarative data, not hand-coded special cases.

## Reuse (this is mostly classification, not new art)

The polyperfect prefab catalog (`docs/polyperfect-asset-catalog.md`) **is already the cell-parts
library** — `Wall_Stone_3x3`, `Stairs_Medieval_Stone`, `Tower_Castle_*`, `Terrain_Plane_*`, gates.
The Catalog is largely a **classification + repo-props layer over prefabs that already exist**;
Composites are **authored arrangements (data)**, not new meshes. So building the catalog is data work,
not an art pipeline.

## Open question for the catalog pass
"How many types" → start with the 8 above (4 core first). The cell **grid size** (the snap unit) should
match the existing `Wall_Stone_3x3` = ~3 m footprint, so walls/floors/stairs all tile on one 3 m grid.
