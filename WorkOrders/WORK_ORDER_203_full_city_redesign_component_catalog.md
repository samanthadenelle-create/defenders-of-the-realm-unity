**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 152 — Full City Redesign via Designer + Component Catalog (the "well-divided city")

**Status: DRAFT — sequenced AFTER WO-151**
**Date:** 2026-05-30
**Priority:** Medium-High — the payoff of the CREATE verb; turns the 5-building village into a designed, district-based city the player (and we) build from a component catalog
**Scope:** Large — design-led. A **designer** lays out the city; the layout is authored from a **catalog of reusable components**, not hand-placed one-offs. Spans builder (architect lane) + catalog content + a player-facing palette.
**Lane:** design (owner + designer + UI specs) · catalog content (Core data) · architect (`VillageSceneBuilder` / a new city builder, single-writer) · CLI (code + bake).
**Owner ask (verbatim):** *"village progression + crafting first then redesign full city with a designer and catalog of components."*

---

## STEP ONE (owner 2026-05-30) — a STRUCTURED village, legible & visible from everywhere

Before any of the district/catalog build below, the owner's **first** requirement: the village must be
**structured and readable from everywhere in the city** — a legible layout with a clear center you can
always orient by. This is a *wayfinding + identity* principle, not just decoration:

- **A tall central landmark anchors the whole space.** The **Heart of Elarion** (the world-tree /
  reliquary at `(0,0,0)`) — and/or the castle's central spire/keep — should rise tall enough to be
  **seen from every district**, so wherever the player stands they can orient toward the center. The
  city reads as "structured around a heart," not a scatter of buildings.
- **Districts arranged readably around the center** (the Civic/Production/Smithing/Companion/Defense
  layout below) so the structure is legible from above and from street level — clear roads/sightlines
  radiating from the center to the gates.
- **Camera supports the read** — with the high walls (WO-136) and the over-wall camera (WO-156), the
  player should be able to take in the structured city; the central landmark is the visual constant.

**This is the FIRST deliverable of the redesign** — get the structured, center-anchored, see-it-from-
everywhere layout standing, THEN layer the full district/catalog build (below) and the polish (next).

### Polish backlog (designer / VFX — NOT now, owner flagged "that's for designers")

- **Pulsing ambient crystals** — subtle crystals that pulse/glow around the city for a unique visual
  signature (the relocated-crystal motif as *atmosphere*, distinct from the harvest nodes). Designer/VFX
  pass; reuse the Mirza Beig VFX (URP-converted). Captured here so it isn't lost — **do not build now**;
  it lands after the structured layout + districts.

## Sequencing — this is the SECOND step, on purpose

1. **FIRST: WO-151** (village progression + crafting) — ships the depth loop on the *current* 5-building roster. Proves the systems (BuildingUpgrade, VillageLevel, Forge/Armory effects) work before we rearrange the city around them.
2. **THEN: WO-152 (this)** — redesign the full city layout, district-based, authored from a component catalog, with a designer driving the look.

Doing progression first means the redesign drops finished, working buildings into a better-arranged city — not a pretty empty shell we then have to wire. **Do not start 152's city-rebuild until 151's systems are green**, or the redesign churns the builder while the upgrade system is still landing in it.

---

## RECONCILE — the catalog already exists (do NOT rebuild it)

Per `docs/PLAYER_BASE_DESIGN_CATALOG_ROADMAP.md` + `docs/CATALOG_SYSTEM.md`, the catalog **data model and placement engine are already built** and compile green:

| Exists | Where | Role |
|---|---|---|
| Catalog data model | `Assets/_Modules/Core/Catalog/` (`CatalogType`, `CatalogEntry`, `RepoProps`, `PlacementRules`, `CatalogRegistry`) | the `catalog ⊥ repo` (look vs behavior) component taxonomy — **the foundation** |
| `StructureFactory` + `CatalogBootstrap` | `Assets/_Modules/Village/Buildings/` (WO-148) | one creation path that instantiates a `CatalogEntry` |
| Placement runtime | `TowerPlacementSystem` | ghost/snap/overlap/affordability placement loop |
| Build-mode architecture | `docs/build-mode-architecture.md` (WO-108) | the data-driven base-layout model (`PlacedStructureData` + `BaseLayout`) |

**This WO does NOT redesign the catalog system.** It (a) **populates** the catalog with the city's component set, (b) has a **designer** compose the city from those components, and (c) authors that layout through the factory so the city is data-driven (rebuildable, and the seed for player build-mode).

---

## Goal

Replace the ad-hoc 5-building village with a **designed, district-based city** — a "well-divided city" (owner's words) — assembled from a **catalog of reusable components** (buildings, walls, roads, props, district markers) rather than hand-placed singletons. The result is:
- A city a **designer** has intentionally laid out (districts, sightlines, flow), not a quadrant-spread of 5 boxes.
- Authored as **data** (a `BaseLayout`/composite of `CatalogEntry` placements), so it rebuilds deterministically and seeds player build-mode (WO-108).
- Carrying the WO-151 buildings (Store, Forge, Pet Home, Tower, Farm, + Lumbermill/Ironworks/Armory) in their districts, fully functional.

---

## Districts (design intent — designer refines)

From the owner's "well-divided city … production cluster … smithing quarter":
- **Civic/Market district** — Store + the materials stall (WO-151 §5); the player's entry/social hub.
- **Production cluster** — Farm + Lumbermill + Ironworks grouped (the resource quarter).
- **Smithing quarter** — Forge + Armory paired (the combat-power quarter).
- **Companion corner** — Pet Home.
- **Defense line** — Tower + the WO-136 castle wall/ramparts framing the city.
- **Center** — the Heart of Elarion (unchanged, 0,0,0).

> Districts are a layout *intent*, not hardcoded coordinates — the designer composes them; the catalog supplies the pieces.

---

## Workstreams

**152a — Catalog content pass (Core data, parallel-safe, no builder).**
Populate `CatalogRegistry` with the city's component set as `CatalogEntry` defs: each building (look = `visualPrefabPath` to its Polyperfect `_M` prefab; behavior = `RepoProps`/`behaviorId`), plus reusable city dressing (roads, plaza paving, lamps, fences, district markers, walls/gates). This is pure Core data + content — no `VillageSceneBuilder` edit, runs alongside 151.

**152b — Designer layout pass (design, no code).**
A designer composes the district layout from the 152a catalog — districts, building plots, road network, sightlines. Output: a layout spec (plots + which `CatalogEntry` per plot + yaw) the builder consumes. Can use a blockout (greybox) first. **This is where the designer drives the look** — UI/CLI implement to the designer's layout, don't invent it.

**152c — Data-driven city build (architect lane, single-writer, gated on 151 + 152a/b).**
A `CityLayoutLoader` (or extend the builder) instantiates the designed layout via `StructureFactory.Create(entry)` per placement — the runtime twin of the old hand-placement, but reading the designer's data. Then bake. Honors the WO-136 wall/ramparts, the WO-151 buildings, gate clearance, navmesh. **One writer on the builder; coordinate per CLAUDE.md §9.**

---

## Acceptance criteria

1. The city is **composed from `CatalogEntry` components** via `StructureFactory`, not hand-placed singletons — `CatalogRegistry` holds the city's component set.
2. Layout reflects a **designer's district plan** (Civic/Market, Production cluster, Smithing quarter, Companion corner, Defense line) — visibly "well-divided," not a quadrant spread.
3. All WO-151 buildings present and functional in their districts (Store, Forge, Pet Home, Tower, Farm, Lumbermill, Ironworks, Armory) with their [F] interactions + upgrade panels intact.
4. The layout is **data** (a composite/`BaseLayout` of placements) — rebuildable deterministically, and usable as the **seed for player build-mode** (WO-108).
5. WO-136 castle wall + ramparts + parapet preserved; Heart at center; no magenta/stray content (WO-150 clean state held).
6. NavMesh rebuilt; spawn-0..3 → Heart path valid; gate clearance honored.
7. No new catalog system invented — built on `Core/Catalog/` + `StructureFactory` (reconcile, don't replace).
8. Brace-balance check on any `.cs`; `VillageSceneBuilder`/city builder edited by one writer; editor closed for bake.

## Open questions for owner / designer

- **Who is the designer** — you, a teammate, or should UI produce the first blockout layout for the designer to refine?
- **Scope of redesign** — full city rebuild in one pass, or blockout → iterate district-by-district (recommended, matches the de-risk cadence that worked for the parapet)?
- **Builder approach** — extend `VillageSceneBuilder` (the proven path), or stand up a separate `CityLayoutLoader` that reads the catalog layout (cleaner for player build-mode reuse)? Recommend the loader, so the same data path serves both the authored city and player-built bases.

## What NOT to touch (until sequenced)

- Do **not** start the city rebuild before WO-151 systems are green (avoid churning the builder mid-feature).
- Do **not** rebuild the catalog data model or `StructureFactory` (exists — WO-148).
- Do **not** hand-edit `Village.unity`; regenerate via the builder/loader, editor closed.
- Do **not** regress WO-136 walls/ramparts or WO-150's clean roster.

## Done checklist (CLAUDE.md §10)

- [ ] Catalog populated with the city component set (152a) — Core data, compiles
- [ ] Designer district layout captured as data (152b)
- [ ] City built via `StructureFactory` from the layout data (152c); no hand-placed singletons
- [ ] All WO-151 buildings functional in districts; WO-136 walls + Heart intact; no magenta/strays
- [ ] NavMesh rebuilt; spawn→Heart verified; gate clearance honored
- [ ] Brace balance passed; single-writer on builder; bake with editor closed
- [ ] `WORK_ORDER_152_full_city_redesign_component_catalog.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
