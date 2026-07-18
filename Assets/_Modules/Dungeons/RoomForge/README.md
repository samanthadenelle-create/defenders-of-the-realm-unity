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

## Relation to existing systems

| System | Role |
|--------|------|
| `DungeonLayout` / healers-cottage JSON | Legacy wall-run layout for `DungeonController` |
| `DungeonComposer` / `DungeonChainBuilder` | Procedural demo / chain scenes |
| **Room Forge compose path** | Prefab rooms + sockets (this module) — does **not** replace live combat until wired into runtime |

## Next steps

1. Forge EntryHall / CombatChamber / RewardVault prefabs with real KayKit art.  
2. Re-bake spine until mateOk=2, matesFail=0.  
3. Wire baked scene into `DungeonController` or WO-584 space resolver.  
4. Optional: seeded endless composer reading the same JSON shape.
