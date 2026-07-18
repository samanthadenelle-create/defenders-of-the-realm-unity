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

1. **Room Forge** — create working room → drop KayKit meshes → add N/E/S/W sockets → **Save Room Prefab + Catalog**.  
   Output: `Assets/Dungeon/Rooms/<RoomId>.prefab` + `rooms-catalog.json`.  
2. **Layout JSON** — ordered rooms + socket connections under  
   `StreamingAssets/Data/Canonical/dungeon-layouts/` (dual-copy Resources).  
3. **DungeonBaker** — instantiate, mate sockets (hard gate), seal unmated, NavMesh bake, save  
   `Assets/Scenes/DungeonCompose/<dungeonId>.unity`.

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
