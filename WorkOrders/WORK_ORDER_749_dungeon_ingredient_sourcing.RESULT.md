# WORK ORDER 749 — RESULT

**Status:** DONE — gate-green, committed local `0c64daaa` on `wip/village2-and-f8-tickets`. **Push HELD; owner felt-verify pending.**
**Implemented by:** Claude (CLI), 2026-07-19.

## What landed
The delve -> gather -> bank -> craft loop; dungeons now yield crafting ingredients.
- **CREATED `Assets/_Modules/Dungeons/Crafting/DungeonLootGrant.cs`** — single dungeon->larder resolver: `GrantChest(rewardKey)` (rewardKey -> loot roll -> `VillageInventory.Add`), `GrantEncounter(isBoss)` (per-encounter ATB victory roll: boss->dungeon-miniboss else dungeon-hollow), `DepositDungeonInventory` (banks per-run scatter on exit). `[Flow:DungeonLoot]` + Guard on every branch.
- **CREATED `DungeonChestInteract.cs`** — proximity auto-open, dedupe via `DungeonRuntimeState.OpenChest`.
- **EDITED `IngredientPickup.cs`** (`CreateRuntime` factory) + **`DungeonController.cs`** — runtime-authors chests + 10 floor-scatter ingredients (no scene bake); deposits DungeonInventory to the larder on `ExitToVillage`/teardown before `Clear()`; grants encounter loot on victory.
- **DATA (dual-copy, v2):** 4 new loot tables `dungeon-hollow`/`dungeon-miniboss`/`dungeon-deepboss`/`dungeon-chest` (deepboss carries the 5 legendary components). 10 new scatter ingredient defs in `crafting-recipes.json`. **Defined the 7 gear-component `MaterialDef`s** the tables drop (quench_oil, heartwood_core, reforged_steel, oathweld_plating, heartwood_bough, last_pressing, aether_catalyst) — this closed the phantom-drop gate reds the initial pass introduced.

## Decisions
- **Gap 3 (ATB vs composed-chain):** chose the ATB per-encounter victory roll (lowest risk, zero touch to the working `OutpostEnemyGroupSpawner` composed-chain that dg_starter uses).
- **Gap 2 id canonicalization:** larder-native `materialId` form; new scatter uses larder-native ids so the bridge deposits 1:1. `LarderAlias` seam provided (identity today).

## Gate
`COMPILE_GATE_OK`. `DataRegression.RunAll` = 6 fails = **5 committed baseline** (arena ground, B2 dual-wallet, pet-slot, core-save Tribes/Wards/Arena, orc-raider) + **1 pre-existing d4 working-tree artifact** (the dungeon session's uncommitted rooms-catalog/EntryHall/d4 socket rework — NOT this WO). **ZERO new red** from WO-749 after the MaterialDef fix.

## Deferred / follow-up
- **Gap 5 — Echo Exploration multiplier** (`EchoAssignments.LaneExploration`) SKIPPED as out-of-lane -> new WO.
- **Optional polish bake** — `DungeonSceneBuilder` could place KayKit chest meshes / pickup props for the 10 scatter placements; until then they render as glyph/tint procedural motes (functionally complete).
- 8 missing ingredient icons (glyph fallback covers). `IronScrap` drops but no recipe consumes it yet.
- Composed-chain (dg_starter) breakable loot unchanged (repointing needs a `DungeonChainBuilder` re-bake).
