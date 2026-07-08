# WORK ORDER 613B — Outpost = PREFAB CHUNK assembly (kill freeform generation)

**Status:** READY TO IMPLEMENT — owner re-ruling F8-28 (2026-07-08): *"the outpost should be
assembled from prefabs"* — Outpost1's generated layout *"makes no sense"*.
**WO number 613B PROVISIONAL** (authority = MASTER_PIPELINES_BACKLOG + `CLI_LANES_WO_NUMBERS.md`;
613B deliberately avoids colliding with WO-613 VFX moments — confirm on mint; specs run past 612).
**Lane:** World/Environment (§9 — architect lane; no combat, no seam work).
**Canon spine (READ FIRST, BINDING):** WO-479 scene-chunk composer (anchor-relative chunks + JSON
recipes + progression-scaled seed budget), WO-584 dungeon/outpost/arena one-space-primitive
(+ §3 KayKit chunk inventory, §3b harvested recipe beats, §119 "CLI creates → owner hand-edits →
CLI offsets" loop), memory `scene-chunk-dungeon-composer-northstar`, `docs/DUNGEON_DESIGNS.md` /
`docs/dungeon-3d-healers-cottage-design.md` (the KayKit room-assembly precedent that already works).

---

## 1. The problem (what the owner saw tonight)

Outpost1 (`Assets/Scenes/Outpost1.unity`, built by `DeNelle.Editor.DungeonChainBuilder.BuildOutpost1`
in `Assets/Editor/DungeonChainBuilder.cs`) is a **freeform-generated box layout**: primitive-cube
floor/perimeter/choke walls + `KayTile(nameContains)` name-fishing dressing dropped on a fixed rail
(`DressWalls` hardcodes an -X strip at x=-14.4). Nothing about it was *designed* — and it reads that
way. Three concrete failures flagged:

1. **Layout makes no sense** — generated primitives + token-matched dressing, not authored chunks.
2. **Foreign props intersect the layout** — the tree-in-outpost class: world placement systems
   (terrain/scatter trees, `HarvestSite` wood nodes, `TribeManager` camp props) know nothing about
   the outpost footprint, so props end up standing inside walls/rooms.
3. **Unbound/black planes** — un-remapped materials render black/magenta (the exact class fixed
   durably for ArcaneSpire in commit `f23d05ae`: `TripoAssetPostprocessor` extract **+ remap +
   save**; the runtime band-aid `OutpostMaterialFixInjector` proves the import-time gap exists).
4. **The dungeon entrance doesn't read as a dungeon** — the current marker is
   **`Outpost1Exit_ToDungeon`** (a bare invisible 6x4x6 trigger created by
   `DungeonChainBuilder.AddTransition` at (0,0,12), prompt "Enter the Dungeon", **no visual at
   all**); the nearest visible geometry is generic house-ish wall dressing, so the exit reads as a
   house/doorway in a shed, not a descent into a dungeon.

## 2. The ruling (owner, F8-28 — the acceptance spine)

Outposts are **ASSEMBLED FROM PREFAB CHUNKS** — anchor-relative, composed by **JSON recipes**,
seeded by the **progression-scaled seed budget** (WO-479). **Never freeform generation**: no
primitive-cube rooms, no `nameContains` token-fishing for dressing, no hardcoded dressing rails.
The generator picks and places *authored chunks*; it does not invent geometry.

## 3. Scope — what to build

### 3a. Chunk library (one-time authoring pass, WO-584 Slice 5 executed for outposts)
- Prefab-ify an **outpost chunk starter set** from KayKit Dungeon Remastered 1.1
  (`Assets/Models/KayKit/KayKit Dungeon Remastered 1.1/`, gitignored, 0 prefabs today) +
  polyperfect `_M` where it fits better: `Outpost_EntryYard`, `Outpost_PerimeterWall_N/E/S/W`
  (with gate variant), `Outpost_ChokeRoom`, `Outpost_LootRoom`, `Outpost_DungeonMouth` (see 3d).
- Each chunk = a prefab captured **relative to its own anchor** (WO-479 Collection pattern) carrying
  its own markers: `Spawn_*` anchors, torch points, breakable spots — markers travel with the chunk.
- Materials fixed **at import/authoring time** via the `TripoAssetPostprocessor` extract+remap+save
  pattern (`f23d05ae`): externalObjects mapped to real .mat assets whose `_BaseMap` is the bound
  albedo, `.tripo-extracted` marker written. No runtime material fixers for new chunks.
- Seating/pivot corrections captured through Offset Forge / `RotationCorrectionRegistry`
  (memory `model-alignment-offset-tool`) — the proven "CLI creates → owner hand-edits → CLI
  captures offsets" loop (WO-584 §3b). No naive grid math against raw KayKit pivots.

### 3b. Recipe + composer (reuse WO-479 machinery — do NOT greenfield)
- **Outpost recipe JSON** in `Assets/Resources/Data/` (sibling of `scene-links.json` /
  `garrison-recipes.json`): ordered placements `{ chunkId, anchor(x,y,z), yawDeg }` + the spawn
  roster reference. Owner thinks in data structures — the layout IS the JSON.
- Composer: extend the existing capture→recipe→replay path (`Village2Playable.ReplayRecipeIntoScene`
  generalization per WO-479; `DungeonComposer` / `EnemyStrongholdBuilder` bake idioms for
  floor-fill, colliders, NavMeshSurface). Deterministic: same (recipe, seed) → same outpost.
- **Seed budget** (WO-479 progression scalar) selects among authored recipe variants / optional
  chunk slots — it sizes the gauntlet; it never mutates chunk interiors.
- Rebuild `Outpost1.unity` from a hand-authored starter recipe via a batchmode entry
  (`Defenders > World > ...`), replacing `DungeonChainBuilder.BuildOutpost1`'s freeform body.
  Keep the chain contract intact: entry marker at (0,0,-12), exit trigger → `Dungeon` at the same
  `EntryPos`, Build-Settings registration, one connected navmesh bake + `BakeAndVerify`.

### 3c. Footprint exclusion (kills the tree-in-outpost class)
- The composer **registers each placed chunk's world-space footprint** (XZ bounds, small margin)
  in a queryable registry (Core-side, data-only).
- **Compose-time sweep:** any pre-existing foreign object (trees, rocks, `HarvestSite` nodes,
  `TribeManager` camp props) whose bounds intersect a registered footprint is relocated outside or
  removed — logged via `FlowTrace.Warn("OutpostCompose", ...)`, never silent.
- **Placement-time check:** the world scatter/placement systems above consult the registry before
  dropping a prop (mirror the existing `DistanceToCavePath` reject idiom in
  `ExteriorTerrainBuilder`). No prop may spawn inside an outpost footprint.

### 3d. Dungeon entrance = a DUNGEON affordance
- Replace the invisible `Outpost1Exit_ToDungeon` trigger with an **`Outpost_DungeonMouth` chunk**:
  a KayKit dungeon **stone arch/doorway + descending dark mouth + flanking torches** (reuse
  `TorchFlicker` + the warm point-light idiom already in `DressTorches`) — it must read at a glance
  as "a dungeon starts here", not a house. Colorblind rule: the SHAPE (arch + dark descent + fire)
  carries the meaning, never a color tint.
- The existing `SceneTransitionTrigger` (walk-up entry, prompt "Enter the Dungeon", single-load →
  `Dungeon` @ EntryPos) moves ONTO the chunk's anchor — behavior unchanged, only the read changes.

## 4. Files to touch
- `Assets/Editor/DungeonChainBuilder.cs` — `BuildOutpost1` becomes recipe-driven (chain contract
  preserved); retire `DressWalls`/`KayTile` token-fishing for outpost use.
- NEW composer/editor code under `Assets/Editor/` (extend `DungeonComposer` /
  `EnemyStrongholdBuilder` idioms — do not duplicate their bake code).
- NEW `Assets/Resources/Data/outpost-recipes.json` (or per-outpost file — CLI call, keep it flat).
- NEW chunk prefabs + extracted materials (force-add extracted assets where folders are gitignored,
  per `f23d05ae`).
- Footprint registry (Core, pure data) + reject checks in the named placement systems
  (`HarvestSite`, `TribeManager`, any live tree/rock scatter path).
- `Assets/Editor/TripoAssetPostprocessor.cs` — only if a chunk source needs the extract+remap pass
  generalized (pattern exists; extend, don't fork).

## 5. What NOT to touch
- **BattleArena combat itself** — zero combat code; this is world/layout only (WO-584 law).
- **The WO-584 resolver contract stays** — `spaceType` → resolver registry routing is untouched;
  this WO changes what the outpost space is BUILT from, not how it's entered or resolved.
- **No seam/cross-region navmesh work** — outposts stay isolated, port-in spaces (memory
  `no-seams-ever-port-around`).
- **No hand-edited `.unity` scenes** (§3) — everything lands via the batchmode builder.
- **No inspector drag-drop prefab refs** (memory `never-dragdrop`) — chunks resolve via
  registry/Resources/data, exactly the WO-584 §3b rejection of Grok's generator.
- `OutpostMaterialFixInjector` stays as-is for the legacy baked OuterWorld instance — new chunks
  must simply never need it.

## 6. Acceptance criteria (each fleet-oracle- or screenshot-verifiable)
- [ ] **Prefab-chunk assembly:** rebuilt Outpost1 contains ZERO builder-generated primitive-cube
      room/wall geometry and zero `Fallback_*` KayTile cubes — every structural piece traces to a
      chunk prefab named in the recipe JSON. Oracle: scene census over the outpost root asserts
      every renderer's source prefab ∈ recipe chunk set; `FlowTrace.Step("OutpostCompose", ...)`
      per placement.
- [ ] **Deterministic:** two batchmode composes of the same (recipe, seed) produce identical
      placement dumps (the Village2LayoutDump pattern). Oracle: diff the dumps.
- [ ] **No foreign props in the footprint:** overlap oracle sweeps all non-recipe renderers/colliders
      against the registered chunk footprints → zero intersections; a deliberately planted test tree
      inside the footprint is relocated/removed and logged. Screenshot: outpost interior, no
      tree/rock breaching a wall or floor.
- [ ] **No unbound/black planes:** material oracle walks every renderer in the composed outpost →
      every material is URP-compatible with a bound `_BaseMap` (no InternalErrorShader, no
      null-albedo). Chunk sources carry `.tripo-extracted` markers where the pattern applies.
      Screenshot at play lighting: no black/magenta planes.
- [ ] **Dungeon entrance reads as a dungeon:** `Outpost_DungeonMouth` chunk stands where the bare
      `Outpost1Exit_ToDungeon` trigger was; walk-up prompt + single-load transition to `Dungeon` @
      (0,0,-12) still fire (fleet exit-leg oracle). Screenshot for PO: the mouth reads
      arch/descent/torch — not a house.
- [ ] **Chain contract intact:** entry seat at (0,0,-12), navmesh entry→exit `PathComplete`
      (`BakeAndVerify` + the AutoPilot `NavReachable` oracle), scene registered in Build Settings.
- [ ] `COMPILE_GATE_OK` + fleet pass; **push held for owner felt-pass** (PO closes F8-28 — the
      layout must *make sense* to her eye; expect the hand-edit → offset-capture loop before close).
