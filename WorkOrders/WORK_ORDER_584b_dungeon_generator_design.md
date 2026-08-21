<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **NUMBER COLLISION (letter sub-number) — this document is WO-584b, NOT WO-584. `WORK_ORDER_584_dungeon_outpost_arena_consolidation.md` owns WO-584.**
> Referred to hereafter as **WO-584b**. The clash is a *parser* artefact: `tools/board_build.py` strips the
> trailing letter, so WO-584b reads as a second claim on WO-584 on `BOARD.html`. The letter suffix was and
> remains deliberate — this is a sibling spec of WO-584, not a duplicate mint.
> Flagged by the 2026-08-16 Sunday board-grooming pass. Banner only — nothing renumbered or deleted.

# WORK ORDER 584b — Dungeon MAP GENERATOR (modular-grid: rooms → connectors → reachability)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Parent:** WORK_ORDER_584 (Dungeon / Outpost / Arena one-space consolidation) — this is the **map-creation** half of WO-584 §5 / §3b.
**Silo:** World/Environment (editor builder + data). Combat/AI only consumes the spawn slots it emits.
**Canon:** memory `scene-chunk-dungeon-composer-northstar`, `region-gate-crossing-primitive`,
`dungeon-outpost-arena-one-space-primitive`, `owner-thinks-in-data-structures`,
`village2-hand-tuned-no-blind-regenerate` (CLI-creates → owner-hand-edits → CLI-offsets loop).
**Read-first sources (verified from code, not comments — CLAUDE.md §12):**
`Assets/Editor/DungeonComposer.cs`, `Assets/Editor/EnemyStrongholdBuilder.cs`,
`Assets/_Modules/Village/World/RuntimeRegionGate.cs`, `Assets/Resources/Data/Canonical/garrison-recipes.json`,
`Assets/Resources/Data/region-gates.json`.

---

## 1. The decision (why this WO exists)

WO-584 settled the dungeon as an **isolated Arena-skinned space entered by a RegionGate warp**. What it
did NOT specify is **how the dungeon's floorplan is laid out**. Today `DungeonComposer.cs` hardcodes a
3-room linear demo recipe (Entry → Choke → Keep) in C#; `EnemyStrongholdBuilder.cs` lays one fixed
concentric stronghold. Neither **generates** a varied floorplan from data.

Owner-specified architecture (2026-06-28): the **Fallout / Bethesda MODULAR-GRID pattern**.

```
MODULE CATALOG (data)              MAP RECIPE (data, seeded)
  rooms[]   = fixed-footprint        seed + { enemyLevel, bossStyle,
             prefabs, sockets,                 depth, componentMix }
             enemy/prop/treasure      grid bounds + theme + enemy set
             SLOTS
  connectors[] = fixed-size              │
             corridor/door prefabs       ▼
             (or zero-length)      GENERATOR (editor builder, 584c)
                                     1. place room tiles on a GRID
                                     2. link adjacent SOCKETS with connectors
                                        (intra-space RegionGate crossing pattern)
                                     3. populate slots (enemies/props/treasure)
                                     4. VALIDATE entrance→boss reachability
                                        (NavMesh.CalculatePath == PathComplete)
                                     5. bake navmesh + save scene
```

**This is a DATA + builder job, not new combat.** The generator emits a walkable, navmesh-baked space
with named spawn slots; the **verified Arena loop** (WO-584 Slice 1) does the fighting. Zero new combat
or traversal code.

---

## 2. Core concepts (the modular-grid contract)

### 2.1 The grid
- One **standardized room footprint** = one grid cell. `gridCell = 20m` (catalog-level constant).
- A room occupies `footprint = [w,d]` cells; **v1 ships 1×1 only** (uniform tiles — the Bethesda
  "kit-piece" rule). Multi-cell rooms are a later catalog addition; the schema already carries the field.
- A cell at grid `(col,row)` has world centre `((col - cols/2)*gridCell, 0, (row - rows/2)*gridCell)`.
  The generator works in cell space; world placement is one multiply (mirrors `DungeonComposer.MakeFloor`
  which already places floors at arbitrary `cx,cz`).

### 2.2 Sockets (the doorway contract)
- Every room edge that can connect carries a **socket** at the **edge midpoint, floor level, facing
  outward** — the standardized doorway position. Socket names = compass edges `N / E / S / W`.
- Two rooms in adjacent cells connect **only** if both expose the facing socket (A's `E` ↔ B's `W`,
  A's `N` ↔ B's `S`). This is the entire adjacency rule — no geometry intersection tests.
- Sockets are authored INTO the room prefab as empty child markers named `Socket_N` … `Socket_W` at the
  exact cell-edge offset (`±gridCell/2` on the relevant axis). The generator reads marker transforms; if
  a prefab is missing (pack not imported) it falls back to the computed edge-midpoint (same degrade-to-
  primitive pattern `EnemyStrongholdBuilder.ResolveRole` uses).

### 2.3 Slots (spawn + treasure + prop anchors)
- Each room prefab authors anchor markers the generator populates:
  - `Slot_Enemy_*`  → enemy spawn points (consumed by `GarrisonController`, EXACTLY as
    `DungeonComposer.BuildEncounter` already builds an `EnemySpawnPoints` group + `Spawn_i` children).
  - `Slot_Treasure_*` → chest / coins / key anchors (KayKit chests×5, coins×4, keys×4 — WO-584 §3).
  - `Slot_Prop_*` → torch / barrel / banner dressing anchors (`ScatterDecorProps` role list).
- The catalog records the COUNT of each slot type per room so the generator/validator knows the budget
  without instantiating. Missing prefab → generator synthesizes ring-arranged slots (the math
  `DungeonComposer.BuildEncounter` already uses for its spawn ring).

### 2.4 Connectors (fixed-size corridors / doors, via the RegionGate crossing primitive)
- A **connector** joins two linked sockets. Three `crossing` kinds — all reuse the **RegionGate
  intra-space crossing recipe** (`RuntimeRegionGate` parts 1+4+5), NOT a cross-scene warp:
  - `weld` (length 0) — **rooms abut**; the shared socket edge is welded directly (a short walkable deck
    overlapping both floors so the navmesh fuses — `RuntimeRegionGate.BuildApproachDeck`'s overlap-weld,
    proven). No corridor geometry.
  - `door` (length 0) — abutting rooms with a doorway frame prop + **funnel choke panels**
    (`RuntimeRegionGate.BuildFunnelPanels`: thin BoxCollider + carving `NavMeshObstacle` either side of
    the opening so navmesh routes only through the door).
  - `corridor` (length ≥ 1 cell) — a fixed-size corridor tile spanning the gap between non-abutting
    cells (`DungeonComposer.BuildCorridor` already builds an axis-aligned floor strip + side rails;
    generalize it to consume a connector module).
- **Why RegionGate and not bespoke doorway code:** the funnel-panel + walkable-deck-weld pattern is the
  project's *proven* way to make a navmesh route through a single opening without leaking or pinching
  (forged over weeks on the castle seam). The dungeon door is the same problem at smaller scale — reuse it.
- Connectors are themselves fixed-size kit pieces (one straight, one L, one door, one weld) selected by
  the relative position of the two cells.

### 2.5 Reachability validation (the gate that makes a map shippable)
- After bake, the generator MUST prove **entrance → boss is PathComplete** using
  `NavMesh.CalculatePath` — the EXACT oracle `EnemyStrongholdBuilder.VerifyTraversal` already runs
  (`VERIFY(a) … PATHCOMPLETE-OK` / staged `StagePath` probes / `BlockProof` for walls).
- It ALSO proves every `Slot_Enemy_*` is on-mesh and reachable from the entrance (no soft-locked spawn —
  the `LogSpawnReachability` pattern from `RuntimeRegionGate`).
- A map that fails reachability is **re-rolled** (increment seed → regenerate, bounded retries) and, if
  still failing, REJECTED with a loud `FlowTrace.Fail` (never ship an unsolvable dungeon — CLAUDE.md §12).

---

## 3. Reuse map (do NOT greenfield — extend these)

| Need | Reuse | From |
|---|---|---|
| Room floor + dark walls + torches + lighting | `MakeFloor` / `BuildRoomRails` / `PlaceRoomTorches` / `ApplyDarkLighting` | `DungeonComposer.cs` |
| Corridor floor strip + rails between rooms | `BuildCorridor` (generalize: consume a connector module) | `DungeonComposer.cs` |
| Per-room encounter → `GarrisonController` + spawn ring | `BuildEncounter` + `SetStringArray`/`SetInt` reflection wiring | `DungeonComposer.cs` |
| Prop / treasure / trap placement, degrade-to-primitive | `ResolveRole` / `PlaceOneCounted` / `ScatterDecorProps` / `BuildTraps` | `EnemyStrongholdBuilder.cs` |
| NavMesh bake (single-tile, PhysicsColliders, RemoveAllNavMeshData first) | `BakeNavMesh` | `EnemyStrongholdBuilder.cs` |
| Reachability + block proofs (entrance→boss, slot on-mesh) | `VerifyTraversal` / `StagePath` / `BlockProof` / `LogNavLine` | `EnemyStrongholdBuilder.cs` |
| Door/corridor that routes navmesh through ONE opening (weld deck + funnel panels + AI link) | `BuildApproachDeck` / `BuildFunnelPanels` / `BuildAiLink` | `RuntimeRegionGate.cs` |
| Recipe DTO parsed by a LOCAL Newtonsoft type (Core untouched) | `StrongholdRecipe*` DTO pattern | `EnemyStrongholdBuilder.cs` |
| Scene save + Build Settings register | `SaveScene` / `EnsureInBuildSettings` | both |
| Save scene name discipline (never hand-edit `.unity`) | builder writes a NEW scene per map | CLAUDE.md §3 |

The new generator (`DungeonMapGenerator.cs`, 584c) is **orchestration over these helpers** — a grid
loop that calls them per cell/edge, plus the catalog/recipe readers. Estimated net-new logic: graph
layout + socket adjacency + the catalog DTO. Everything physical already exists and is proven.

---

## 4. Data model (schemas)

Two data files, both under `Assets/Resources/Data/Dungeons/` (Resources so a WebGL build can
`CanonicalJson.Read` them, same as `garrison-recipes.json`). Written as part of THIS WO:

### 4.1 `dungeon-modules.json` — the MODULE CATALOG (the kit)
The room + connector prefab library. One catalog feeds every map. Schema:

```jsonc
{
  "gridCell": 20.0,            // metres per cell (standardized footprint)
  "wallHeight": 4.0,
  "floorThickness": 0.5,
  "rooms": [
    {
      "id": "room_entrance",            // referenced by map nodes
      "prefab": "Dungeons/Modules/Room_Entrance", // Resources/AssetDB path; null => tinted primitive box
      "footprint": [1, 1],              // cells (W,D). v1 = 1x1 only
      "sockets": ["N"],                 // doorway edges this module exposes
      "tags": ["entrance"],             // role tags the componentMix biases on
      "enemySlots": 0,                  // count of Slot_Enemy_* anchors
      "propSlots": 2,                   // count of Slot_Prop_*
      "treasureSlots": 0,               // count of Slot_Treasure_*
      "weight": 1.0                     // base selection weight
    }
  ],
  "connectors": [
    {
      "id": "weld_abut",                // referenced by map links
      "prefab": null,                   // weld has no geometry
      "length": 0,                      // cells; 0 = abutting rooms
      "width": 4.0,
      "crossing": "weld",               // weld | door | corridor
      "tags": ["abut"]
    }
  ]
}
```

**Field rules**
- `sockets` is the connection contract (§2.2). A link is only legal if both rooms expose the facing socket.
- `crossing` selects which RegionGate sub-pattern the builder runs (`weld` / `door` / `corridor`, §2.4).
- `length 0` ⇒ abutting cells (no gap); `length ≥ 1` ⇒ that many empty cells of corridor between rooms.
- `prefab: null` anywhere ⇒ degrade to a tinted primitive (existing `ResolveRole` fallback) so a
  pack-less clone still builds a navigable grey-box dungeon (TGVRU: no silent invisible blocker).

### 4.2 Map recipe — a seeded dungeon (`dungeon_*.json`)
Three shipped this WO (small/medium/large). Schema:

```jsonc
{
  "id": "dungeon_small_crypt",
  "catalog": "dungeon-modules",       // which catalog (Resources/Data/Dungeons/<name>.json)
  "seed": 1337,                       // deterministic RNG seed (System.Random, same as builders)
  "theme": "crypt",
  "lighting": "torch_dark",           // maps to ApplyDarkLighting profile
  "enemies": ["hollow-walker", "hollow-warrior"],  // roster GarrisonController draws from
  "boss": null,                       // boss enemy id, or null (no boss room built — WO-550 rule)
  "params": {
    "enemyLevel": 3,                  // GarrisonController min/maxLevel center
    "bossStyle": "none",              // none | brute | caster | swarm  (biases boss room module + roster)
    "depth": 3,                       // target entrance→boss path length (# rooms on the critical path)
    "componentMix": {                 // role weights biasing room SELECTION (sum need not = 1)
      "combat": 1.0,
      "treasure": 0.5,
      "trap": 0.3,
      "puzzle": 0.0
    }
  },
  "grid": { "cols": 3, "rows": 3 },   // generator bounding grid

  // AUTHORED SKELETON (optional). If present, the generator PINS these cells/links and fills the rest
  // from seed+params; if ABSENT, the generator lays the whole graph from seed+params inside `grid`.
  // The 3 samples include an explicit skeleton so they are concrete + renderable today; a pure-parametric
  // map simply omits `nodes`/`links`.
  "nodes": [
    { "cell": [1, 0], "module": "room_entrance", "role": "entrance" },
    { "cell": [1, 1], "module": "room_hall_4way", "role": "combat" },
    { "cell": [1, 2], "module": "room_treasure", "role": "treasure" }
  ],
  "links": [
    { "from": [1, 0], "to": [1, 1], "connector": "corridor_straight" },
    { "from": [1, 1], "to": [1, 2], "connector": "door_abut" }
  ]
}
```

**Node/link rules**
- Exactly ONE node with `role: "entrance"` (hero spawn — generator seats the Arena entry warp there).
- `role: "boss"` node built ONLY if `boss != null` (WO-550: no boss → no empty boss room).
- Every `link` must connect two `nodes` in adjacent cells whose facing sockets both exist, else the
  generator logs `FlowTrace.Fail` and drops the link (then reachability validation catches the orphan).
- `connector` id resolves in the catalog; a `length 0` connector requires the two cells to be adjacent,
  a `length ≥ 1` connector requires exactly that many empty cells between them on one axis.

---

## 5. Generator algorithm (584c implementation spec — NOT built here)

Deterministic from `seed` (`new System.Random(seed)` — identical pattern to every existing builder).

1. **Load** catalog + map recipe (LOCAL Newtonsoft DTO, Core untouched — `StrongholdRecipe` pattern).
2. **Lay the critical path.** If `nodes` authored → use them. Else: from the entrance cell, random-walk
   `depth` rooms across `grid` (4-neighbour steps, no revisit), weighting each step's room module by
   `componentMix` × module `weight` (combat-tagged rooms favoured by `componentMix.combat`, etc.). Place
   the boss room (if `boss != null`) at the path end.
3. **Branch** (optional side rooms) until the grid budget (`cols*rows` × fill ratio) is met — treasure/
   trap rooms hang off critical-path rooms via spare sockets, biased by `componentMix`.
4. **Place room tiles** on the grid: per node, `MakeFloor` + `BuildRoomRails` + instantiate the room
   prefab (or primitive). Read its `Socket_*` markers.
5. **Link sockets with connectors:** per `link`, resolve the connector module and run the matching
   RegionGate sub-pattern — `weld` (overlap deck), `door` (frame + funnel panels), `corridor`
   (`BuildCorridor` strip). Adjacent abutting rooms with no explicit link but matching open sockets MAY
   auto-weld (config flag) so the floorplan reads as connected.
6. **Populate slots:** per room, fill `Slot_Enemy_*` → `BuildEncounter` `GarrisonController` (roster =
   recipe `enemies`, levels from `enemyLevel`); `Slot_Treasure_*` → chest/coins/key props;
   `Slot_Prop_*` → torch/banner/barrel dressing. Traps at corridor/door chokes (`BuildTraps`).
7. **Lighting:** `ApplyDarkLighting` (or the recipe `lighting` profile) + torch slots.
8. **Bake** navmesh (`BakeNavMesh`: single tile, PhysicsColliders, `RemoveAllNavMeshData` first).
9. **VALIDATE** (§2.5): entrance→boss `PathComplete`; every `Slot_Enemy_*` on-mesh + reachable. Fail →
   re-roll seed (bounded, e.g. 8 tries) → still fail → `FlowTrace.Fail` + abort (no unsolvable ship).
10. **Save** a NEW scene `Assets/Scenes/Dungeon_<id>.unity` + register in Build Settings. Never hand-edit.

**Parameters recap** — `seed` + `{ enemyLevel, bossStyle, depth, componentMix }` (plus `grid`, `theme`,
`enemies`, `boss`). `seed` makes it repeatable; the four params shape difficulty/length/flavour; the
catalog supplies the kit. This is the progression-scaled SEED BUDGET of memory
`scene-chunk-dungeon-composer-northstar` (depth ↔ size, enemyLevel ↔ difficulty, componentMix ↔ AI
strategy/flavour points).

**Build loop (owner 2026-06-28, WO-584 §3b):** CLI authors the generator placing chunks roughly →
owner hand-tunes seating/layout by eye in the editor → CLI captures corrections into the Offset Forge /
`RotationCorrectionRegistry` (memory `model-alignment-offset-tool`) so the build is repeatable forever.
The "create" step needs to be canon-clean (no inspector drag-drop, reuse the builders above) and close
enough to hand-tune — NOT pixel-perfect grid math.

---

## 6. What NOT to touch
- **No `.cs` in THIS WO.** 584b is design + data only. The generator (`DungeonMapGenerator.cs`) is the
  gated follow-up 584c.
- **No new combat / traversal code** — slots feed the existing `GarrisonController`; fighting is the
  WO-584 Arena loop. ZERO combat code.
- **No cross-region seam / navmesh stitching** — a dungeon is an ISOLATED space entered by a RegionGate
  WARP (WO-584). The connectors here are INTRA-space door/corridor crossings, not scene warps.
- **No hand-edited `.unity`** (CLAUDE.md §3) — every map is a builder output to a NEW scene.
- **Do not modify `DeNelle.Core`** — parse recipes with a LOCAL editor DTO (the `StrongholdRecipe`
  pattern), so Core stays clean and one file can feed multiple readers.
- **Do not greenfield** — extend `DungeonComposer` / `EnemyStrongholdBuilder`; reuse RegionGate.

## 7. Acceptance criteria (for 584c, the implementation WO)
- [ ] `dungeon-modules.json` catalog parses; rooms expose sockets + slot counts; connectors carry
      `crossing` kind. (Data shipped THIS WO.)
- [ ] Three seeded maps (small/medium/large) parse + each builds a walkable, navmesh-baked scene.
- [ ] Same `seed` → byte-identical layout (deterministic `System.Random`).
- [ ] Room tiles placed on the grid; adjacent sockets linked by the correct connector (`weld`/`door`/
      `corridor`) using the RegionGate sub-patterns.
- [ ] Reachability validated: entrance→boss `PathComplete`; every enemy slot on-mesh + reachable;
      a forced-fail map re-rolls then aborts loud (no unsolvable ship).
- [ ] Pack-less clone still grey-boxes a navigable dungeon (primitive fallback; no invisible blocker).
- [ ] Slots feed `GarrisonController`; the WO-584 Arena loop fights it. No new combat code.
- [ ] Each map felt-verified by owner (PO closes) before the kit expands.

## 8. Files
- `WorkOrders/WORK_ORDER_584b_dungeon_generator_design.md` (this doc)
- `Assets/Resources/Data/Dungeons/dungeon-modules.json` (module catalog + schema-by-example)
- `Assets/Resources/Data/Dungeons/dungeon_small_crypt.json` (small — 3-room linear, authored skeleton)
- `Assets/Resources/Data/Dungeons/dungeon_medium_warren.json` (medium — branching, treasure + trap)
- `Assets/Resources/Data/Dungeons/dungeon_large_keep.json` (large — multi-wing + boss, deeper budget)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
