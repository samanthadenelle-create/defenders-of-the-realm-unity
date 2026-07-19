# WORK ORDER 749 — Dungeons as the crafting-ingredient source (80%+ gatherable from delving)

**Status:** DONE — committed `0c64daaa` 2026-07-19; RESULT: `WORK_ORDER_749_dungeon_ingredient_sourcing.RESULT.md`. Owner felt-verify pending.
**Classification:** NEW FEATURE (composed from ~70% existing parts — wiring + content, not greenfield).
**Owner (PO):** Sam — dungeons should yield 80%+ (ideally more) of crafting ingredients -> replay value.
**Pillar:** dungeon expansion ([[dungeon-pillar-roadmap]]); ties the Echo Exploration lane to real loot.

---

## The vision
Delve dungeon -> gather ingredients (floor scatter + breakables + chests + dungeon-only enemy drops) ->
bank to the village larder on exit -> craft consumables/jewelry/gear -> return deeper. 80%+ of all
crafting ingredients gatherable from dungeons -> a real reason to replay them.

## Ingredient reality (enumerated from all 6 recipe catalogs — 30 ingredients)
- **12 of 30 have NO source today** = the dungeon's biggest win: `ing_spring_water`, `ing_moonbloom`,
  `ing_cloth_scrap`, `ing_shadowcap`, `ing_quickfoot` (gatherables) + the 7 gear components
  (`quench_oil`, `heartwood_core`, and the 5 legendaries `reforged_steel`/`oathweld_plating`/
  `heartwood_bough`/`last_pressing`/`aether_catalyst` — **legendary gear is un-craftable right now**).
- The rest drop from orc/hollow enemies or are boss-gated; dungeon becomes primary for the commons,
  additive for gems/legendaries (which also drop in village raids).

## Assets (mostly covered; a content WO fills the rest)
- Icons: 12/20 material ids have PNGs (all `ing_*` covered); **8 missing** (HealthHerb, BoneFragment,
  ManaCrystalShard, ArcaneDust, IronScrap, dry-reed, oil-soaked-cloth, ember-resin).
- Models: no purpose-built pickup prefab, but KayKit FBXs cover nearly every concept
  (`Resource Bits/Gem_*`, `Textiles_*`, `Iron_Nugget_*`, `Witch/Mushroom`, Forest Nature bushes/grass,
  potion/bottle FBXs, `Halloween/bone_*`). Only ArcaneDust + ember-resin lack a model (tinted VFX mote).
- Pickup VFX ready: Lana `Loot_iddle`/`Loot_pick_up`/`backlight_coin`/`Orbs_gold`. The
  `crafting-recipes.json` per-ingredient `glyph`+`tint` gives a **procedural mote fallback** so this
  ships before icons/models land.

## Distribution (~90% dungeon-reachable, 27/30)
- **(a) Floor scatter** (12): moonbloom/spring_water/cloth_scrap/shadowcap/quickfoot/starbloom/ironroot/
  oil_flask/elarion_petal + HealthHerb/dry-reed/oil-soaked-cloth. Thematic anchors already in dungeon
  data (oilStones, dark rooms, garden approach).
- **(b) Treasure chests** (6): ember-resin/rare-essence/ManaCrystalShard/ArcaneDust/quench_oil/heartwood_core.
- **(c) Dungeon-only enemy drops**: hollow table (5: BoneFragment/IronScrap/monster-hide/wild-herb/
  tattered-cloth) + mini-boss/boss gems (3, `bossOnly`) + a NEW deep-boss table sourcing the 5 legendaries.

## Systems: reuse vs build
**Reuse (no new code):** `BreakableContainer` (crate/barrel -> larder loot roll, already placed by
`DungeonChainBuilder`), `ItemPickupSpawner`/`ItemPickupMarker`, `LootTableCatalog`+`ItemDropSystem.RollAndDeposit`,
`VillageInventory.Add` (persistent larder, GearInventory-backed + Neon-synced), the Lana Loot/Orbs VFX,
the glyph/tint mote fallback. `loot-tables.json` already has `source:"dungeon"` tables.
**BUILD (the real gaps):**
1. **Chest interactable + rewardKey resolver** — `DungeonRuntimeState.OpenChest` grants NOTHING today;
   `DungeonChest.rewardKey` is read by no C#. Add an interact component + a `rewardKey -> loot roll ->
   VillageInventory.Add` resolver. (Biggest single gap.)
2. **Dungeon -> larder bridge for scatter** — per-run `DungeonInventory` is wiped on exit + never persists.
   Repoint `IngredientPickup`/`DungeonInventory.CollectPickup` at `VillageInventory`, or deposit on
   `ExitToVillage` before `.Clear()`. Reconcile ids to `materials.json` (namespace fragmentation:
   `dry-reed` vs `ing_*` vs PascalCase).
3. **ATB-cottage enemy loot** — `ItemDropWatcher` only sees live `Village.Enemy` (the composed-chain
   `OutpostEnemyGroupSpawner` path, which dg_starter_loop now uses); the separate ATB dungeon drops none.
   Grant a per-encounter dungeon roll on ATB victory return, OR bias dungeons to the composed-chain model.
4. **Data:** new `dungeon-hollow` / `dungeon-miniboss` / `dungeon-deepboss` loot tables (+ SA mirrors);
   extend scatter placement to the 12 floor ingredients.
5. **(Optional)** wire the stubbed **Echo Exploration lane** (`EchoAssignments.LaneExploration`) as a
   dungeon ingredient-yield multiplier — a natural progression hook.

## Balance (delve yields)
~10-16 scatter + 2-3 breakables + 3 chests per Cottage-scale delve = 2-4 consumable crafts/delve
(rewarding, not grindy); jeweler tier-up ~2-3 delves; a legendary weapon ~5+ deep delves (epic by design).

## Follow-up content WO (art)
8 missing icons; pickup prefabs wrapping the KayKit FBXs (ArcaneDust/ember-resin -> tinted mote).
`IronScrap` drops but no recipe consumes it — add a recipe or drop it.

**Key files:** `Dungeons/State/DungeonRuntimeState.cs`, `Dungeons/DungeonLayout.cs` (DungeonChest.rewardKey),
`Dungeons/Crafting/{IngredientPickup,DungeonInventory}.cs`, `Village/Crafting/VillageInventory.cs`,
`Village/World/BreakableContainer.cs`, `Village/Items/{ItemDropWatcher,ItemDropSystem,LootTableCatalog}.cs`,
`Editor/{DungeonSceneBuilder,DungeonChainBuilder}.cs`, + `loot-tables/crafting-recipes/materials` json (dual-copy).
