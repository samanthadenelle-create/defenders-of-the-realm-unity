# Room Forge — socketed dungeon room authoring

**Branch intent:** `feat/room-forge-dungeon-baker`  
**Owner architecture:** visual Room Forge → room prefabs + sockets → layout JSON → DungeonBaker (door-touch-door).

## Menus

| Menu | Action |
|------|--------|
| `Defenders/Dungeon/Room Forge` | Author a room (6u cells, sockets, KayKit pieces) |
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
- 10 cases: catalog integrity (17 rooms) · dual-copy law · TypesCompatible matrix ·
  mate math (touch/nudge/distance/alignment/yaw) · seal (wall vs secret vs off) ·
  hard gate (fix 1 abort) · re-verify+overlap (fix 2) · navmesh path-connectivity ·
  **sample layouts green** (`d4_sunken_crypt_spine` + `demo_branching_kit`, each
  `matesOk == connections`, `matesFail == 0`, **`sealed == 1`**) · determinism.
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
