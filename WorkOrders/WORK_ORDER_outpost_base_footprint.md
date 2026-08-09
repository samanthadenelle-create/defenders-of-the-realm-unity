# WORK ORDER (number TBD per registry) — Outpost base footprint (predefined, upgradable)

**Status:** SPEC (reconciled 2026-08-09 - restates this file's own DESIGN CAPTURE (owner thinking-out-loud 2026-06-07) line in the canonical vocabulary; the file carries no WO number of its own (number TBD per registry) and no commit references it)

**Status: DESIGN CAPTURE (owner thinking-out-loud 2026-06-07).** **Lane:** Roaming/Outposts (relates to
WO-143 roaming raids + ClaimableCamp). Build later — captured so it slots into the existing pipeline.

## Idea
When the player **secures/claims an outpost location**, highlight a **predefined base footprint** — a
bounded buildable zone — that is **upgradable** to expand (bigger footprint / more capacity over time).
CoC-style defined base, but reusing the town-design pipeline we just built.

## The flow (owner 2026-06-07)
1. **Establish outpost → enter Build Mode**, with the claimed spot as the **local origin `(0,0,0)`**.
2. Player **places a piece → aligns it on the grid → rotates 90°** (RotateModelMenu Turn L/R).
3. **Done** — the outpost is built; the BaseLayout recipe is captured **in local space (relative to 0,0,0)**.

**Why local origin matters (the superpower):** because every placement is recorded relative to the outpost's
`(0,0,0)`, the recipe is **portable** — the SAME outpost design can be baked at ANY claimed world location by
applying that outpost's world transform (position + rotation) to the local recipe. One design → reusable
everywhere = the sister-city generator, still pure TRS math. (Village uses world coords today; outposts should
record LOCAL coords for portability — small delta in how BaseLayout stores the origin.)

## Why it drops straight onto what exists (no greenfield)
- **Claim → reveal footprint:** `ClaimableCamp` (Assets/_Modules/Village/World/Camps/) on claim reveals
  the outpost's predefined footprint on the `PlacementGrid` — same grid/ghost/place loop as the village.
- **Author with the same tools:** inside that footprint the player places + orients via the SAME
  `CatalogRegistry` → `StructureFactory` → ghost → `RotateModelMenu` (Turn L/R) → appends to a per-outpost
  `BaseLayout`. An outpost is just a **mini-town** with a bounded zone.
- **Recipe → bake:** the outpost's `BaseLayout` is a placement recipe (location + rotation; scale/material
  from the curated catalog) — replayable/headless, exactly like the village (catalog = correctness,
  recipe = transform; "all math").
- **Upgrade expands the footprint:** an upgrade grows the buildable cell area (and/or capacity) — CoC-style
  base progression + a resource/crystal SINK. Tie into the existing upgrade-building economy.

## Per-item placement-context tag (owner 2026-06-07) — "so outposts stay light"
Each catalog build item gets a tag for WHERE it can be built. The palette filters by the active build context:
- Add `[JsonProperty("buildContext")]` to `CatalogEntry` — a flags enum (Village / Outpost / Both); default
  **Both** if unset so nothing breaks.
- **Village build mode** → shows everything. **Outpost build mode** → shows only `Outpost`/`Both` items.
- Heavy economy buildings (**Forge, Armorer, mills, granary**) = **Village-only** → outposts stay LIGHT
  (towers, walls, basic defensive/military structures). `BuildPaletteUI` filters cards by the controller's
  current context.
- Pure data + a filter — part of curating the refined catalog (the same catalog the orient tool refines).

## Open questions (for build time)
- Footprint shape/sizes per outpost tier (e.g. 6×6 → 8×8 → 10×10) — data-driven in the catalog/outpost def.
- How the footprint highlight renders (reuse the grid highlight) + the gate/clearance rules inside it.
- Persisting per-outpost BaseLayout in save (extends the party/zone persistence work).

## Notes
- Reuses: ClaimableCamp, PlacementGrid, BuildModeController, RotateModelMenu, CatalogRegistry/StructureFactory,
  the BaseLayout recipe + generator bake. Pure composition of hardened pieces.
- Relates to WO-143 (roaming raids) + the roaming-troops/reclaim-camps pillar. Local WO; assign a number in the registry.
