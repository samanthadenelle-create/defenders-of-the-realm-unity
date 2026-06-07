# WORK ORDER 189 — Elarion City: Persistent Populate Manifest

**Status:** READY TO IMPLEMENT
**Lane:** A (Village Scene — SERIAL, `VillageSceneBuilder.cs`). After Batch A bug-fixes land.
**Source:** owner — "creative city with items, very empty in here" (repeated requirement)
**Design:** `DESIGN_ELARION_CITY.md`
**Priority:** P1 — the city is empty and KEEPS reverting; this makes it durable.

## The core problem to fix
The village reverts to bare grass because content isn't persisted — every rebake regenerates the scene
and wipes anything not encoded as builder data. **Build a City Manifest the builder reads on every bake**,
so the populated city is reproduced (not wiped) each time. This is the anti-regression mechanism — do NOT
hand-place into the scene.

## Part 1 — Manifest data (persistence)
- Create a serializable **CityManifest** (ScriptableObject or JSON in Resources) listing every placement:
  `prefabId, position, rotation, scale, district, optional propSet, optional npcBinding`.
- `VillageSceneBuilder` reads the manifest during BuildVillage and instantiates everything from it.
- Ties WO-148 (catalog factory — ONE place that resolves prefabId→prefab) + WO-149 (base persistence).
- Editing/adding a building = editing the manifest. Rebake reproduces it **exactly**.

## Part 2 — Populate the roster (per DESIGN_ELARION_CITY.md §3, target ~28–36 placements)
- Heart-seat (center) + inner-ring production (Barracks, Forge, Arcane Tower, Lumbermill, Granary/Farm,
  Pet House) + mid-ring market/homes + outer-ring + 4 corner towers.
- **Towers use KayKit simple meshes** — archer/defense → `building_watchtower` (NOT the ornate
  `Tower_Medieval_Big`); weaponized → `building_tower_cannon/catapult`. (Owner 2026-05-31.)
- Cobbled main roads (`Stone_Brick`) from each of the 4 bridges to the Heart.
- **Decoration density pass** (§4): stalls, barrels, crates, carts, hay, fences, banners, torches,
  planters, trees, wells. It must read as a lived-in town, not 3 buildings on a lawn.

## Part 3 — NPC / Warden binding (the blacksmith problem)
Playtest: the blacksmith Warden stands in an empty field doing a smithing animation with **no hammer
and no forge**. Wardens must be **bound to their building, with the right held prop + contextual anim**:
- **Blacksmith → at the Forge, beside an `Anvil`, with a HAMMER prop in hand** (`Hammer` Tools_M, or
  KayKit smith hammer), smithing animation. Same pattern for other Wardens (farmer at farm, etc.).
- Held props attach to the NPC's hand bone/socket (reuse the hero gear-attachment approach).
- NPC placements live in the manifest too (so they persist + sit at the correct building).
- Ties existing daily quests ("Tend a building", "Bond Rank") + WO-116 barks.

## Do NOT touch
- Terrain (WO-173), bridges (WO-188), stairs (WO-183) — separate Batch A orders this folds in beside.

## Acceptance
- City matches DESIGN_ELARION_CITY.md roster + density; not empty.
- **A rebake reproduces the full city identically** (the regression test: bake twice, scenes match).
- Towers use KayKit watchtower mesh; blacksmith stands at the forge with a hammer in hand at an anvil.
- All placements come from the manifest; nothing hand-placed in the scene.

## Gate
Brace check; green build; commit `feat: implement WO-189 — persistent city populate manifest`; folds into the Batch A village bake. Screenshots (incl. a rebake-twice comparison) for UI validation.
