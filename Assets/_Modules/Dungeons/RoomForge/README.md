# Room Forge — socketed dungeon room authoring

**Branch intent:** `feat/room-forge-dungeon-baker`  
**Owner architecture:** visual Room Forge → room prefabs + sockets → layout JSON → DungeonBaker (door-touch-door).

## Menus

| Menu | Action |
|------|--------|
| `Defenders/Dungeon/Room Forge` | Author a room (canon cells, sockets, KayKit pieces) |
| `Defenders/Dungeon/Bake Compose Layout (default spine)` | Bake `d4_sunken_crypt_spine.json` |
| `Defenders/Dungeon/Bake Compose Layout From Selected JSON` | Bake selected layout asset |

## Pipeline

1. **Materials (simple)** — `Defenders/Dungeon/Ensure Room Forge Materials`  
   Creates **one shared wall mat + one shared floor mat** from KayKit `dungeon_texture.png`.  
   All default rooms use these (no per-wall UV art). Reward/boss can use warm **accent** floor.  
2. **Default rooms** — `Defenders/Dungeon/Build Default Room Prefabs`  
   Entrance, Straight, TurnLeft/Right, TJunction, Intersection, DeadEnd, ChokePoint, CombatChamber, LoreShrine, RewardVault, SecretAlcove, StairUp/Down, SideBranch, BossKeep.  
3. **Room Forge** — create working room → **KayKit prop carousel** (barrel/crate/chest/…) → add N/E/S/W sockets → **Save**.  
   Output: `Assets/Dungeon/Rooms/<RoomId>.prefab` + `rooms-catalog.json`.  
4. **Layout JSON** — ordered rooms + socket connections under  
   `StreamingAssets/Data/Canonical/dungeon-layouts/` (dual-copy Resources).  
5. **DungeonBaker** — instantiate, mate sockets (hard gate), seal unmated, NavMesh bake, save  
   `Assets/Scenes/DungeonCompose/<dungeonId>.unity`.

### Why one atlas for walls

KayKit dungeon pieces already share `dungeon_texture.png`. Room shells are procedural cubes — tiling that atlas on **all** walls/floors is the fast, consistent look. Dress with real KayKit **props** from the carousel for readable variety (props keep their own materials via `Fix KayKit Materials`).

## Room metric canon (BINDING — WO-919 + WO-922)

**All room-shell numbers live in `RoomForgeCanon.cs` (this folder). Read them; never re-type
them.** The builder, the baker's placeholder room, the dresser's fallback footprint and the
regression oracles all consume the same consts, which is what stops the four from drifting.
It sits in the runtime `DeNelle.Dungeons` assembly for the same reason `DungeonBakerChecks`
does: `DeNelle.Editor` → `DeNelle.EditorRegression`, so an oracle cannot reference the builder.

| Const | Value | Note |
|-------|-------|------|
| `Cell` | **10 m** | WO-922, was 6 m. 1×1 room = 10×10 m; 2×2 (combat/boss) = 20×20 m. |
| `WallHeight` | **4.0 m** | WO-919, was 2.8 m (chest-height → open-top box maze under blue sky). |
| `ChokeWallHeight` | **3.8 m** | `WallHeight − 0.2`, the WO-919 floor for interior masses. |
| `DoorGap` | 2.2 m | Doorway clear width — unchanged by the widen. |
| `WallThickness` | 0.4 m | |
| `FloorSlabThickness` | 0.1 m | Slab top face is local `y = 0`. |
| `CeilingThickness` | 0.3 m | WO-919 slab, seated on the wall top. |
| `FloorOccupiedHeight` | **4.4 m** | slab + wall + ceiling. Must stay under `FloorSeparationY`. |

`DungeonBakerChecks.FloorSeparationY` stays **6 m**: its constraint is clearance, not the cell,
and 4.4 < 6 with 1.6 m to spare. `RoomForgeRegression` case 11 and
`DungeonMultiLevelRegression` case 2 both assert that relation against the canon.

**A source edit here changes nothing on disk.** `Assets/Dungeon/Rooms/*.prefab` are generated —
re-run `Defenders/Dungeon/Build Default Room Prefabs`, recompose every graph, then re-bake.
Case 11 fails loudly while the prefabs are stale, which is the point.

### Enclose (WO-919)

Each room gets a **`Ceiling`** child: a solid slab spanning the footprint plus the wall
thickness, underside flush with the wall top. It carries **no collider** (the baker's NavMesh
uses `NavMeshCollectGeometry.PhysicsColliders`, so a collider would voxelize into a walkable
roof that `SamplePosition` could snap a hero seat or spawner onto) and is **not**
`NavigationStatic`. `DungeonBaker` also nulls `RenderSettings.skybox` on the composed scene;
`ambientMode` is already `Flat`, so no light changes — only the blue dome stops drawing. The
in-room **camera background** is deliberately NOT set at bake: the composed bake seats no
camera, so that stays owned by the runtime rig (WO-920 / WO-1004) — one owner, not two.

Unmated doorways seal with a slab that now spans the **full** wall height. It used to be a
2.5 m cube centred on a floor-level socket (so it plugged y −1.25…+1.25 — half of it under the
floor); at 4 m walls that left a 2.75 m letterbox of open sky at every dead end.

## Socket types

`Door` · `Arch` · `StairUp` · `StairDown` — see `RoomSocketType.cs`.

## Socket-id canon (BINDING — WO-745)

Default-library rooms name their sockets by the SHORT cardinal form to match
`rooms-catalog.json` (written by `DefaultDungeonRoomsBuilder` / `RoomForgeWindow`):

`n_door_01` · `s_door_01` · `e_door_01` · `w_door_01` · `stair_up_01` · `stair_down_01`

Layout JSON `connections` MUST reference these exact ids. WO-745 root cause: the two sample
layouts referenced the long form (`north_door_01`, …), which exists on NO shipped prefab, so
**every** connection failed with `reason=missing-socket` (matesFail=connections, sealed=all
sockets) yet the scene still saved. The placeholder fallback room (`DungeonBaker.CreatePlaceholderRoom`)
also uses the short ids, so a missing-prefab bake stays consistent.

## Verify / regression (WO-745)

The pipeline's permission gate is `Assets/Editor/Regression/RoomForgeRegression.cs`:

- Standalone: `run-unity-method DeNelle.Editor.Regression.RoomForgeRegression.RunAll`
  → prints `ROOMFORGE_REGRESSION_OK` / `ROOMFORGE_REGRESSION_FAIL`.
- Wired into `DataRegression.RunAll` as `[room-forge]` (headless batch gate).
- 11 cases: catalog integrity (17 rooms) · dual-copy law · TypesCompatible matrix ·
  mate math (touch/nudge/distance/alignment/yaw) · seal (wall vs secret vs off) ·
  hard gate (fix 1 abort) · re-verify+overlap (fix 2) · navmesh path-connectivity ·
  **sample layouts green** (`d4_sunken_crypt_spine` + `demo_branching_kit`, each
  `matesOk == connections`, `matesFail == 0`, **`sealed == 1`**) · determinism ·
  **shipped room shells match `RoomForgeCanon`** (cell / floor span / wall height /
  ceiling present, collider-free, nav-inert, flush with the wall top — WO-919 + WO-922).
- Cases 4–7 use a deliberate **6 m fixture cell** (`FixtureCell`), pinned by their own ±3
  socket literals, NOT the kit canon. Kit geometry is case 11's job.
- Every case builds throwaway in-memory GameObjects (torn down after); it NEVER opens or
  saves a shipping `.unity` scene and references NO KayKit art (passes with the pack absent).

**Single source of truth:** the mate / seal / re-verify / overlap / compose logic lives in the
runtime `DungeonBakerChecks.cs` (this folder), so the editor `DungeonBaker` **and** the oracle
drive the exact same code — no duplication. (It is in the runtime `DeNelle.Dungeons` assembly,
not the editor one, because `DeNelle.Editor` already references `DeNelle.EditorRegression`;
placing the shared checks in the editor assembly would create a reference cycle.)

### The two WO-745 contract fixes (pinned by the oracle)

1. **Hard gate is hard.** Any mate/drift/overlap failure aborts the bake: no scene saved, no
   Build Settings entry (`DungeonBaker` returns on `ComposeOutcome.Aborted`). Optional debug save
   to `_FAILED_<id>.unity` OUTSIDE Build Settings behind editor pref
   `DungeonBaker.SaveFailedScenes` (default OFF).
2. **Order-independent mates.** After all connections mate, every one is re-verified (a later
   nudge that drags a room off an earlier mate = `reason=drift`) and all room footprints are
   AABB-overlap checked (`reason=overlap`).

## Instrumentation & the runtime-quiet law

Baking/authoring emit the FlowTrace bands `[Flow:DungeonBake]` (per-mate reason enum:
`missing-instance` / `missing-socket` / `type-mismatch` / `distance` / `alignment` / `drift` /
`overlap`; seal events; the machine-parseable `SUMMARY id= rooms= matesOk= matesFail= sealed=
saved=` line) and `[Flow:RoomForge]` (room save + dual-copy catalog writes). **This is EDITOR
tooling — it may log loudly.** Any FUTURE *runtime* dungeon loader that reuses this pipeline MUST
follow the player-quiet law (CLAUDE.md §12 / INSTRUMENTATION_STANDARD §1.5): loud only to the
db / log, never spamming a shipped player build.

## Relation to existing systems

| System | Role |
|--------|------|
| `DungeonLayout` / healers-cottage JSON | Legacy wall-run layout for `DungeonController` |
| `DungeonComposer` / `DungeonChainBuilder` | Procedural demo / chain scenes |
| **Room Forge compose path** | Prefab rooms + sockets (this module) — does **not** replace live combat until wired into runtime |

## Next steps

1. Forge EntryHall / CombatChamber / RewardVault prefabs with real KayKit art.  
2. ~~Re-bake spine until mateOk=2, matesFail=0.~~ **Done (WO-745)** — spine bakes `matesOk=2,
   matesFail=0, sealed=1`; demo bakes `matesOk=9, matesFail=0, sealed=1`; both pinned by
   `RoomForgeRegression` case 9.  
3. Wire baked scene into `DungeonController` or WO-584 space resolver.  
4. Optional: seeded endless composer reading the same JSON shape.
