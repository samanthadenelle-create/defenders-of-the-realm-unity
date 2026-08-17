# Item / Resource Asset Map (live scan)

**Status:** living map · **Scanned:** 2026-08-04 · **Purpose:** one place that says  
*catalog id → which sprite on disk* for currency, materials, consumables, troops,  
collectors/pallets, and gear summary.  
**Not** a full 3D mesh inventory (see indexes below).

> **See also: [`docs/reference/ICON_CATALOG.md`](reference/ICON_CATALOG.md)** (2026-08-16) — the exhaustive
> icon registry: all 1 076 icon files across `ItemIcons` / `RpgUi` / `Talents` / `HudIcons` /
> `ProjectileIcons`, every row tagged Ranger / Knight / Mage / Shared and cited. It covers what THIS file
> does not: the authored-vs-fallback resolution order, orphans, collisions, and the
> **`Resources` vs `StreamingAssets` weapons.json desync that shelves 356 icons**. This file stays the
> *catalog-id → sprite* map for currency, materials, consumables and collector props; that one is the
> *icon-asset → consumer → class* registry.

### Related docs that already exist (you were right)

| Doc | What it maps |
|-----|----------------|
| [`docs/MASTER_CATALOG/resources-art.md`](MASTER_CATALOG/resources-art.md) | **Live** `Assets/Resources/**` folders (ItemIcons ~492, RpgUi, HudIcons, currency) |
| [`docs/asset-inventory/README.md`](asset-inventory/README.md) | Pack inventory (KayKit, polyperfect, Blink, VFX) — what we *own* |
| [`docs/asset-inventory/05_resources_project_built.md`](asset-inventory/05_resources_project_built.md) | Committed runtime art |
| [`docs/MASTER_ASSET_REFERENCE.md`](MASTER_ASSET_REFERENCE.md) | Older 3D structure key map (partially stale) |
| [`docs/polyperfect-asset-catalog.md`](polyperfect-asset-catalog.md) | Polyperfect prefabs |
| [`docs/kaykit-asset-catalog.md`](kaykit-asset-catalog.md) | KayKit |
| [`docs/ART_BRIEF_storage_containers.md`](ART_BRIEF_storage_containers.md) | Lumberyard/Foundry/Silo **art direction** (fill states) |
| [`docs/ARMOR_IMAGE_NAMING_GUIDE.md`](ARMOR_IMAGE_NAMING_GUIDE.md) | Armor PNG naming → ItemIcons |
| [`docs/GEAR_GENERATOR_COVERAGE.md`](GEAR_GENERATOR_COVERAGE.md) | Gear model generator coverage |
| [`concept-icons.json`](../Assets/Resources/Data/Canonical/concept-icons.json) | **Runtime** concept → RpgUi sprite (HUD/abilities) |
| Gear Caster | `Defenders > Gear > Gear Caster` — browse weapons/armor + assign PNG |

**Gap this file fills:** none of the above was a single **item/resource id → icon file** table for collect/bank/materials. This is that table (scanned from live JSON + sprites).

**Browse tools:** Gear Caster (gear); Project folders `Resources/ItemIcons`, `Resources/RpgUi/currency` for the rest.

**Refresh:** re-scan catalogs + `Resources/**/*.png` when icons change; keep dual-copy JSON in sync when wiring `iconId`/`iconPath`.

---

## Currency (bank / HUD)

| concept-icons key | mapped role/name | Resources file |
|---|---|---|
| `wood` | `currency/currency_wood` | RpgUi/currency/currency_wood.png (YES) |
| `iron` | `currency/currency_iron` | RpgUi/currency/currency_iron.png (YES) |
| `food` | `currency/currency_food` | RpgUi/currency/currency_food.png (YES) |
| `crystal` | `currency/currency_crystal` | RpgUi/currency/currency_crystal.png (YES) |
| `gold` | `currency/currency_gold` | RpgUi/currency/currency_gold.png (YES) |

## Materials

| id | JSON icon | Best ItemIcons stem | Status |
|---|---|---|---|
| `ing_moonbloom` | ItemIcons/ing_moonbloom | `ing_moonbloom` | MAPPED |
| `ing_ironroot` | ItemIcons/ing_ironroot | `ing_ironroot` | MAPPED |
| `ing_ember_crystal` | ItemIcons/ing_ember_crystal | `ing_ember_crystal` | MAPPED |
| `ing_starbloom` | ItemIcons/ing_starbloom | `ing_starbloom` | MAPPED |
| `ing_shadowcap` | ItemIcons/ing_shadowcap | `ing_shadowcap` | MAPPED |
| `ing_aether_shard` | ItemIcons/ing_aether_shard | `ing_aether_shard` | MAPPED |
| `ing_spring_water` | ItemIcons/ing_spring_water | `ing_spring_water` | MAPPED |
| `ing_oil_flask` | ItemIcons/ing_oil_flask | `ing_oil_flask` | MAPPED |
| `ing_cloth_scrap` | ItemIcons/ing_cloth_scrap | `ing_cloth_scrap` | MAPPED |
| `ing_quickfoot` | ItemIcons/ing_quickfoot | `ing_quickfoot` | MAPPED |
| `ing_heartstone_crystal` | ItemIcons/ing_heartstone_crystal | `ing_heartstone_crystal` | MAPPED |
| `ing_elarion_petal` | ItemIcons/ing_elarion_petal | `ing_elarion_petal` | MAPPED |
| `HealthHerb` | (none) | `-` | GAP |
| `BoneFragment` | (none) | `-` | GAP |
| `ManaCrystalShard` | (none) | `-` | GAP |
| `ArcaneDust` | (none) | `-` | GAP |
| `IronScrap` | (none) | `-` | GAP |
| `quench_oil` | (none) | `-` | GAP |
| `heartwood_core` | (none) | `-` | GAP |
| `reforged_steel` | (none) | `-` | GAP |
| `oathweld_plating` | (none) | `-` | GAP |
| `heartwood_bough` | (none) | `-` | GAP |
| `last_pressing` | (none) | `-` | GAP |
| `aether_catalyst` | (none) | `-` | GAP |
| `dry-reed` | (none) | `-` | GAP |
| `oil-soaked-cloth` | (none) | `-` | GAP |
| `ember-resin` | (none) | `-` | GAP |

## Consumables

| id | JSON icon | Best ItemIcons stem | Status |
|---|---|---|---|
| `minor-heal-potion` | (none) | `-` | GAP |
| `greater-heal-potion` | (none) | `-` | GAP |
| `cons_mana_draught` | (none) | `-` | GAP |
| `traveler-rations` | (none) | `-` | GAP |
| `scout-tent-kit` | (none) | `-` | GAP |
| `cons_mending_salve` | ItemIcons/cons_mending_salve | `cons_mending_salve` | MAPPED |
| `cons_ironbark_tonic` | ItemIcons/cons_ironbark_tonic | `cons_ironbark_tonic` | MAPPED |
| `cons_emberfire_bomb` | ItemIcons/cons_emberfire_bomb | `cons_emberfire_bomb` | MAPPED |
| `cons_swiftstep_elixir` | ItemIcons/cons_swiftstep_elixir | `cons_swiftstep_elixir` | MAPPED |
| `cons_arcane_clarity` | ItemIcons/cons_arcane_clarity | `cons_arcane_clarity` | MAPPED |
| `cons_suppressing_smoke` | ItemIcons/cons_suppressing_smoke | `cons_suppressing_smoke` | MAPPED |
| `cons_heartward_draught` | ItemIcons/cons_heartward_draught | `cons_heartward_draught` | MAPPED |
| `cons_elarion_blessing` | ItemIcons/cons_elarion_blessing | `cons_elarion_blessing` | MAPPED |
| `cons_field_poultice` | (none) | `-` | GAP |
| `cons_hearthfire_stew` | (none) | `-` | GAP |
| `cons_wardens_campfire` | (none) | `-` | GAP |
| `cons_purifying_draught` | (none) | `-` | GAP |

## Troops

| id | iconId | Notes |
|---|---|---|
| `troop-footman` | `icon_sword` | concept-icons / RpgUi/icons |
| `troop-archer` | `icon_combat` | concept-icons / RpgUi/icons |
| `troop-spearman` | `icon_sword` | concept-icons / RpgUi/icons |
| `troop-shieldguard` | `icon_shield` | concept-icons / RpgUi/icons |
| `troop-outrider` | `icon_compass` | concept-icons / RpgUi/icons |
| `troop-battlemage` | `icon_energy_sword` | concept-icons / RpgUi/icons |
| `troop-echo-legionnaire` | `icon_tree` | concept-icons / RpgUi/icons |

## Weapons summary
- Total: 96
- With iconPath: 76
- Emoji-only (typical): 20
- id exact match ItemIcons stem: 76

## Armor summary
- Total: 24
- id exact match ItemIcons: 18

## Collectors + pallets (structures)

| id | role | suggested icon |
|---|---|---|
| `collector_farm` | collector food | `currency_food / food` |
| `collector_lumbermill` | collector wood | `currency_wood / hud_wood` |
| `collector_forge` | collector iron | `currency_iron / Iron_Bar_1` |
| `lumberyard` | pallet wood | `bag-icon + wood` |
| `foundry` | pallet iron | `crafting-icon / Iron_Bar` |
| `silo` | pallet food | `bag-icon + food` |
| `jeweler` | shop only | `currency_crystal / ing_ember_crystal` |

### Collector stack props (3D) — OWNER SELECTION, stated 2026-08-16

**Provenance:** owner selection, stated verbatim 2026-08-16 — *"log sack of flour and iron bar"*.
Resolved to on-disk paths and committed the same day. **This table is the record** — before it
existed there was no committed trace of these picks anywhere in the repo, and they were nearly
re-sourced from scratch.

These are the diegetic props `CollectorStackView` stacks as a collector fills (20 steps, 4-column
brick grid). They live in the catalog asset at
`Assets/Resources/Collectors/CollectorStackPropCatalog.asset`, created/wired by
`Defenders > Art > Build Collector Stack Prop Catalog`
(`DeNelle.Editor.CollectorStackPropCatalogBuilder.Build`) and gated by
`CollectorStackPropCatalogRegression` (marker `COLLECTOR_PROPS_OK`).

| Resource | Owner's words | Prop asset | Path |
|---|---|---|---|
| Wood | "log" | `Wood_Log_A.fbx` | `Assets/Models/KayKit/KayKit Resource Bits 1.0/Assets/fbx(unity)/Wood_Log_A.fbx` |
| Food | "sack of flour" | `Food_Flour.fbx` | `Assets/Models/KayKit/KayKit Resource Bits 1.0/Assets/fbx(unity)/Food_Flour.fbx` |
| Iron | "iron bar" | `Iron_Bar.fbx` | `Assets/Models/KayKit/KayKit Resource Bits 1.0/Assets/fbx(unity)/Iron_Bar.fbx` |
| Crystals | *(not named)* | **unwired on purpose** | candidates awaiting her word: `Gem_Medium.fbx`, `Gems_Pile_Small.fbx` (same pack) |

Alternates in the same pack if a pick ever needs more visual mass: `Wood_Log_B`, `Wood_Log_Stack`,
`Iron_Bars`, `Iron_Bars_Stack_Small/Medium/Large`. `Pallet_Wood.fbx` is the natural base for the
WO-903 storage pallets (separate lane — not this catalog).

**Why the picks are recorded as GUIDs even though the pack is gitignored** (`.gitignore:106`
`/Assets/Models/*`): on a machine without KayKit imported the GUIDs resolve to null,
`CollectorStackPropCatalog.TryGet` returns false on its `entry.Prop != null` line, and the view
takes its abstract fill-bar fallback — the same path every collector took before this catalog
existed. No throw, no broken render. Copying the FBX into `Resources/` to load them by path
instead would import gitignored pack art into git, against the standing big-art-out-of-git policy
(owner ruling 2026-07-15). Note this does **not** trip
`VfxResourceSelfContainmentRegression` — that oracle is scoped to `Assets/Resources/VFX/**`, and
`Assets/Models/` is not among its `GitignoredArtRoots`.

**Scale is measured, not hand-picked.** `CollectorStackPropCatalogBuilder.FitScale` fits each model
to one cell of the view's grid (`SlotSize.x/4` wide, `SlotSize.y/5` tall) from its own mesh bounds —
the fit-to-height rule from DEF-208 / WO-751. A log, a flour sack and an iron bar have wildly
different native sizes; one constant cannot suit all three.

---

## Suggested fills for GAPS (existing sprites only)

| Catalog id | Use this ItemIcons / currency stem |
|------------|--------------------------------------|
| `HealthHerb` | `ing_moonbloom` or `ing_starbloom` |
| `BoneFragment` | no bone sprite found — leave GAP or generic `icon_combat` |
| `ManaCrystalShard` | `ing_aether_shard` |
| `ArcaneDust` | `ing_ember_crystal` |
| `IronScrap` | `Iron_Bar_1` (if imported under ItemIcons) or `currency_iron` |
| `quench_oil` | `ing_oil_flask` |
| `heartwood_core` / `heartwood_bough` | `currency_wood` / `icon_tree` |
| `reforged_steel` / `oathweld_plating` | `Iron_Bar_2` if present |
| `last_pressing` | `ing_spring_water` or potion |
| `aether_catalyst` | `ing_aether_shard` |
| `dry-reed` | `Fiber` if present else `currency_food` |
| `oil-soaked-cloth` | `ing_cloth_scrap` |
| `ember-resin` | `ing_ember_crystal` |
| `minor-heal-potion` / greater | `Health_Potion` / `Mana_Potion` if under ItemIcons |
| `cons_mana_draught` | `Mana_Potion` |
| `traveler-rations` / stew | `currency_food` / `food` |
| `scout-tent-kit` | `bag-icon` / `loot-icon` |
| Archer troop | prefer bow sprite over `icon_combat` when wiring 858/train UI |

---

## Wire rules (implementers)

1. **Bank / collect float:** `concept-icons` currency keys → `RpgUi/currency/currency_*` (already YES).  
2. **Materials/consumables with MAPPED stem:** set JSON `iconId` or `iconPath` to that stem so UI loaders resolve (many files exist but catalog still says none).  
3. **Weapons:** 76/96 already have `iconPath`; remainder via Gear Caster Assign PNG.  
4. **Pallets vs collectors:** different silhouette (bag/crafting vs raw currency) — see collectors table.  
5. **Do not** invent new art until GAP has no stem above.
