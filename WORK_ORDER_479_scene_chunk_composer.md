# WORK_ORDER_479 — Scene/Dungeon Creator: composable anchor-relative CHUNKS

**Status: DESIGN — owner north-star (2026-06-21), ready to scope** · Supersedes the one-off Village2 builder approach.

## Owner vision (verbatim intent)
"Create a dungeon or scene creator so one [collection] is just an outer perimeter, the next is a small camp, and
[I] move those collections around to a starting point of each and build it in scripted chunks."

## The architecture
Generalize the existing capture→recipe→build pattern (CastleOffsetCapture, Village2Playable Capture/Replay,
Village2LayoutDump) ONE level up — from "one scene's pieces" to "a library of composable chunks":

- **Collection (chunk):** a named, self-contained unit captured **relative to its own anchor/start-point** —
  e.g. `OuterPerimeter`, `SmallCamp`, `Stronghold`, `DungeonRoom_*`. Stored as a recipe JSON of pieces
  (prefab/customFbx + LOCAL TRS relative to the anchor). Drop-anywhere, reusable across scenes.
- **SceneRecipe:** an ordered list of placements `{ collectionId, worldAnchor(x,y,z), yawDeg }`. The owner moves
  each collection's start-point to taste; capture records the anchor.
- **Composer (scripted-chunk build):** given a SceneRecipe, for each placement → instantiate the collection at
  its world anchor + yaw → builds the scene in chunks. Deterministic + reproducible (a rebuild never reverts the
  owner's layout — solves the castle/Village2 "regen wipes hand-dialing" problem permanently).
- **Markers travel with the chunk:** spawn points, hero-start, enemy-spawn groups are captured relative to the
  anchor too, so a placed `SmallCamp` brings its own `Spawn_*` + nav intent.

## Reuse (do NOT greenfield)
- Capture: extend `Village2LayoutDump`/`CastleOffsetCapture` to capture a sub-tree **relative to an anchor**.
- Replay/build: `Village2Playable.ReplayRecipeIntoScene` already instantiates a recipe — generalize to place a
  Collection at an arbitrary anchor+yaw, and to compose multiple.
- Colliders/floor: the `EnsureStructureCollider` fit + a floor-fill per chunk (kills "no colliders" + "huge hole").
- Nav: bake combined navmesh per composed scene (Village2Playable Phase C pattern).

## Village2 = the first proof
Its `StrongholdRoot` (130 objs, captured to `village2-layout-dump.json`) splits by the owner's own spawn zones into
3 starter collections: **OuterPerimeter** (walls/gate), **CourtyardCamp** (Spawn_Courtyard/Chokepoint), **KeepCore**
(Spawn_Keep/Rear). Capture each relative to an anchor → Village2 becomes a SceneRecipe composing the three.

## Two component classes (owner 2026-06-21)
1. **Structural chunks** (geometry): OuterPerimeter, SmallCamp, Stronghold, DungeonRoom, corridor.
2. **Gameplay components** (interactive, anchor-relative, parameterized): **Trap** (trigger + effect), **Squeeze/Choke
   point** (narrow pass — already an idea in Village2's `Spawn_Chokepoint`), **Fake Wall** (looks solid, passable /
   reveals on trigger), **Bridge** (a crossing — reuse the **RegionGate** primitive), **Maze components** (modular
   wall segments that tile into a maze). Each is a small scripted unit with params (damage, width, reveal-condition,
   span) and its own anchor — so a dungeon places "a trap here, a fake wall there, a bridge over this gap."

## JSON-driven DYNAMIC dungeon builder (the end state)
A **dungeon recipe JSON** composes the above dynamically: rooms + corridors + the gameplay components, connected by
anchors/sockets. The builder reads the JSON and constructs the dungeon in scripted chunks at runtime/build —
**authored OR procedurally generated** (a generator emits the JSON; the same builder consumes it). This is the
"json dungeon builder": data in → playable dungeon out, every piece a reusable component, fully reproducible.

## Progression-scaled SEED (owner 2026-06-21)
The procedural generator is driven by a **seed whose BUDGET scales with player progression** — "the further they go
or the higher their level, the bigger the seed." A bigger seed budget → the generator emits a LARGER, HARDER dungeon:
more rooms/corridors, more gameplay components (traps/chokes/maze depth), higher enemy count + level. Same (seed,
budget) → the exact same dungeon (reproducible — for debugging, sharing, fair retries; mirrors the AutoPilot seeded-
chaos idea [[autopilot-chaos-not-one-scripted-path]]). Reuse the existing level-scaling (GarrisonStatBlocks.ApplyLevelScale,
baseEnemyLevel/levelOffset) for the enemy side + the OuterWorld danger-gradient idea for spatial difficulty.
Budget inputs: dungeon depth reached + player level → a single scalar the generator spends on size + difficulty.

The seed budget is a **universal encounter dial** — it doesn't only size the dungeon; the generator spends it across:
- **Dungeon size/complexity** — rooms, corridors, gameplay-component density (traps/chokes/maze depth).
- **Enemy count + level** — via the existing GarrisonStatBlocks scaling.
- **Troop counts** — number of troops fielded (player garrison and/or enemy defenders) scales with the budget.
- **AI strategy points** — the enemy AI gets a *strategy budget* it spends on tactics (formations, flanks, ability
  use, reinforcement waves) — deeper/higher-level = a smarter, better-resourced AI, not just more HP. (New AI
  layer; folds onto EnemyBrain roles — a budget the brain spends on plays.)
One scalar in → the generator allocates it across geometry, enemies, troops, and AI sophistication, deterministically.

## Encounter composition — ROLE MIX forces strategy (owner 2026-06-21)
A dungeon room is a composed **encounter** with a deliberate **role mix** (multiple Healers + DPS + Tanks) so the
player must be tactical — focus-fire the Healer first or the room never dies; bait the Tank; use chokes/traps to
split the pack. This REUSES the EXISTING EnemyBrain role AI (Tank charges / Healer pulses Enemy.Heal on the most-
wounded ally / DPS marches) — already proven by EnemyFamilyTestSpawner (3 DPS + 1 Tank + 1 Healer). The seed budget
allocates the role composition: higher budget → more healers + tighter coordination (AI strategy points) → harder,
more strategic rooms, not just bigger HP bars. Encounter = data: each room recipe carries its role roster + the
component layout (chokes/fake-walls) that makes the role mix matter.

### Phasing (proposed)
- **v1 — Composer + 3 Village2 chunks** (OuterPerimeter / CourtyardCamp / KeepCore), anchor-relative capture + compose.
- **v2 — Gameplay component library** (Trap, Choke, FakeWall, Bridge=RegionGate, Maze tile) as parameterized chunks.
- **v3 — JSON dungeon builder** (recipe → composed dungeon) + an optional procedural recipe generator.

## Open questions for the owner (scope this before building)
1. Chunk taxonomy: confirm the starter set (OuterPerimeter, SmallCamp, Stronghold, DungeonRoom?) + naming.
2. Authoring loop: drag collections in-editor + a "Capture Collection (with anchor)" menu, then a "Compose Scene
   from Recipe" menu? (Mirrors the castle flow you already know.)
3. Scope of v1: just the composer + the 3 Village2 chunks, or include a dungeon-room set?

## NOT touch (until scoped)
The shipping scenes; the existing Village2Playable phases (extend, don't break). This WO is the design spine; a
build WO follows once the taxonomy + authoring loop are confirmed.
