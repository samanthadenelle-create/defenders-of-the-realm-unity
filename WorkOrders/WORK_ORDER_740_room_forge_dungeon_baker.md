# WORK ORDER 740 — Room Forge + Dungeon Baker (socketed room pipeline)

**Status:** DONE (reconciled 2026-08-09 from the tree - the scaffold branch was MERGED to mainline (`ecb55e53` feat, `070f955f` merge, `a87cdee2` meta pairs) and the pipeline is in production: WO-930 records `cb092b7f` baking all four content dungeons PathComplete through RoomForge / DungeonBaker. NOT felt-verified; no `.RESULT.md`)

**Status:** IN PROGRESS (scaffold landed on `feat/room-forge-dungeon-baker`)  
**Priority:** P1 (dungeon authoring; parallel to barracks CoC)  
**Silo:** Editor / Dungeons  
**Branch:** `feat/room-forge-dungeon-baker`  
**Effort:** L (scaffold S done; KayKit rooms + runtime wire remaining)  

---

## Goal

Owner-authored **modular dungeon rooms** with standardized **sockets**, composed via **JSON layouts**, baked with a **door-touch-door hard gate** — no hand-edit of shipping scenes; scales to full maps; later feeds a seeded endless composer.

---

## Scaffold already on branch (do not re-greenfield)

| Piece | Path |
|-------|------|
| Socket types | `Assets/_Modules/Dungeons/RoomForge/RoomSocketType.cs` |
| Socket MB + gizmos | `Assets/_Modules/Dungeons/RoomForge/RoomSocket.cs` |
| Room meta | `Assets/_Modules/Dungeons/RoomForge/RoomPrefabMeta.cs` |
| Compose JSON DTOs + catalog | `Assets/_Modules/Dungeons/RoomForge/DungeonComposeLayout.cs` |
| Room Forge window + prop carousel | `Assets/Editor/RoomForge/RoomForgeWindow.cs` |
| **Shared KayKit wall/floor mats** | `Assets/Editor/RoomForge/RoomForgeMaterials.cs` → `Assets/Dungeon/Materials/` |
| Default room kit builder | `Assets/Editor/RoomForge/DefaultDungeonRoomsBuilder.cs` |
| DungeonBaker | `Assets/Editor/RoomForge/DungeonBaker.cs` |
| Sample layouts | `d4_sunken_crypt_spine.json`, `demo_branching_kit.json` |
| Prefab folder | `Assets/Dungeon/Rooms/` |
| README | `Assets/_Modules/Dungeons/RoomForge/README.md` |
| Editor asmdef | `DeNelle.Dungeons` reference added |

**Menus:** `Defenders/Dungeon/Room Forge` · `Bake Compose Layout (default spine)` · `Bake Compose Layout From Selected JSON`

---

## Acceptance (scaffold)

- [x] Branch created  
- [x] Socket types Door / Arch / StairUp / StairDown  
- [x] RoomForge saves prefab + rooms-catalog.json  
- [x] Layout JSON spine sample  
- [x] Baker mates sockets / seals unmated / NavMesh / saves scene  
- [ ] CompileGate green on branch  
- [ ] Manual: forge 3 rooms → bake spine matesOk=2  

## Acceptance (next slice — follow-up)

- [ ] EntryHall / CombatChamber / RewardVault KayKit-dressed prefabs  
- [ ] Baker zero mateFail on real prefabs  
- [ ] Runtime hook (DungeonController or WO-584 resolver) optional  
- [ ] Overlap bounds lint + shrine min rule enforced hard  

---

## How to smoke-test (Editor)

1. Open `Defenders/Dungeon/Room Forge`.  
2. Create EntryHall (1×1), add +N and +S doors, Save.  
3. Create CombatChamber, +N +S, Save.  
4. Create RewardVault, +S (and optional +N), Save.  
5. `Bake Compose Layout (default spine)` — check Console: matesOk, sealed count, scene under `Assets/Scenes/DungeonCompose/`.  

If prefabs missing, baker spawns **placeholder** box rooms with cardinal sockets so the spine still mates.

---

## Not in scope (this WO)

- Replacing healers-cottage `DungeonLayout` wall-run format.  
- Endless seed composer (later, same JSON).  
- Live multiplayer.  

---

## RESULT

`WorkOrders/WORK_ORDER_740_room_forge_dungeon_baker.RESULT.md`
