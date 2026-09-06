# ART REQUEST - Manage tab portraits (Defense, Troops, Research)

**Raised:** 2026-09-06 (CLI) at the owner's offer - *"give me a detailed description and I will work with Codex in the
morning to get all the detailed art for all the different components"*.
**Why now:** WO-1422 rebuilds Manage - Defense, Research and Troops into the same portrait-rail + selected-card
workspace that WO-1418 gave Buildings. **The code ships without any of this art** (it falls back), but every card in
those three tabs will show either flat art at all tiers or a generic icon until these land.

⚠ **Everything below was measured this session by enumerating `Assets/Resources/`.** Counts and filenames are read off
disk, not from a doc.

---

## 0. The one decision worth making first: ONE folder, ONE naming convention

There are currently **two** portrait conventions and only one of them is tier-aware:

| Folder | Files | Tier-aware? | Reached by |
|---|---|---|---|
| `Assets/Resources/Portraits/Buildings/` | 25 | **YES** - `<slug>-<level>.png` | `ManageScreenPanel.LoadManageBuildingSprite` |
| `Assets/Resources/Portraits/` (root) | 39 | partly - some `-2`/`-3` exist | `BuildPaletteUI.ResolveEntryArtPublic`, which **never appends a level** |

**The consequence, measured:** `archer-tower-2.png` and `archer-tower-3.png` are on disk right now and **no code path in
Manage can reach them**, because the only level-aware loader looks solely in `Portraits/Buildings/`. WO-1422 adds a
root-folder level probe to work around this tonight.

**The ask: author every new file into `Assets/Resources/Portraits/Buildings/` using `<id>-<level>.png`.** One folder,
one loader, no workaround. If Codex delivers there, the root probe becomes a fallback that never fires.

**Reference for style:** the 25 existing files in that folder (`barracks-1..6`, `forge-1..4`, `lumbermill-1..4`,
`arcane-tower-1..4`, `armorer-1..4`, `farm-1..4`) are the owner-approved look. Match them: round-medallion friendly,
readable at **112 px** (the rail row height) and at roughly **150 px** (the card portrait), with the tier visibly
escalating - more structure, more detail, more light as the number climbs.

---

## 1. DEFENSE - the biggest gap (highest priority)

The Defense tab lists every placed structure that carries an upgrade ladder. Measured from
`Assets/Resources/Data/Canonical/structures-catalog.json`, that is these ids and ceilings:

| id | ladder | tiers needed | exists today |
|---|---|---|---|
| `tower_ground_archer` | 3 | `-1 -2 -3` | `Portraits/archer-tower{,-2,-3}.png` - wrong folder |
| `tower_ballista` | 3 | `-1 -2 -3` | `Portraits/ballista{,-2,-3}.png` - wrong folder |
| `tower_catapult` | 3 | `-1 -2 -3` | `Portraits/catapult{,-2,-3}.png` - wrong folder |
| `tower_arcane_spire` | 3 | `-1 -2 -3` | `Portraits/arcane-spire{,-2,-3}.png` - wrong folder |
| `tower_siege_tower` | 3 | `-1 -2 -3` | **only** `Portraits/Sky_Ballista.png` - one flat file, no tiers |
| `wall_wood` | 2 | `-1 -2` | **only** `Portraits/Wooden_Wall.png` - one flat file |
| `mine_crystal` | 3 | `-1 -2 -3` | **only** `Portraits/Crystal_Mines.png` - one flat file |
| `healing_caravan` | 3 | `-1 -2 -3` | **only** `Portraits/Healing_Caravan.jpg` - one flat file, and a JPG |
| `lumberyard` | **6** | `-1`..`-6` | **only** `Portraits/storage_wood.png` - one flat file |
| `foundry` | **6** | `-1`..`-6` | **only** `Portraits/storage_iron.png` - one flat file |
| `silo` | **6** | `-1`..`-6` | **only** `Portraits/storage_food.jpg` - one flat file, and a JPG |

**Total Defense request: 41 files.** Of those, **12 already exist as art** and only need re-cutting into the
`Portraits/Buildings/<id>-<level>.png` convention (the five tower families' tiers 1-3, minus siege tower). The other
**29 are genuinely new**, and the three six-tier storage ladders are 18 of them.

Notes that affect the art, not just the naming:
- The three storage containers climb to **six** levels (`RepoProps.MaxStructureLevel = 6`), and a maxed container takes
  its resource store from 2000 to 34000. The tier-6 art should read as *"this is a serious piece of infrastructure"*,
  not a slightly bigger shed.
- `wall_stone` and the Gate carry **no ladder** and are correctly absent from this list - do not author tiers for them.
  A single flat gate portrait is still wanted, see section 4.
- `Healing_Caravan.jpg` and `storage_food.jpg` are **JPGs** among PNGs. Author the new set as PNG throughout.

## 2. TROOPS - tier portraits

Nine troop cards exist at `Assets/Resources/RpgUi/troop/troop-<unit>.png`: `archer`, `battlemage`, `catapult`,
`echo-legionnaire`, `field-cleric`, `footman`, `outrider`, `shieldguard`, `spearman`. **None has a tier variant**, so a
Level 3 Footman shows the same portrait as a Level 1 Footman - the card's whole point is that the ladder is visible.

**Request: tier variants for the units that actually ladder.** Before authoring, confirm each unit's ceiling from the
troop catalog; the ask is `<unit>-<level>` for every tier above 1. If every unit climbs to 3, that is **18 new files**
(9 units x tiers 2 and 3).

Keep them in `RpgUi/troop/` with the existing `troop-` prefix, or move the whole set into the
`Portraits/Buildings/` convention - **the owner's call, and worth making once**. A single convention for all four Manage
tabs is the reason section 0 exists.

## 3. RESEARCH - nearly complete, two gaps

`Assets/Resources/HudIcons/BuildingUpgrades/` holds **15 `.jpg` + `Upgrade.png`** and covers all **17 authored perks**.
This is the one axis that is essentially done.

Two small asks:
1. **Two perks share an icon** - `arcane-wellspring` reuses `Arcane_Tower_T1_Mana_Attunement`, and
   `lumber-ancient-sawmill` reuses `Lumber_Mill_T1_Construction_Aid`. Two new icons make the set 1:1.
2. **The files are JPGs** named `<Building>_T1_<Perk>.jpg`. They will sit next to PNG portraits in the same card. If
   they show compression artefacts against the dark obsidian plate, re-export as PNG.

## 4. SMALLER GAPS FOUND WHILE LOOKING

- **No gate portrait at all.** `Assets/Resources/Walls/` exists and is **empty of images**. The Gate has no ladder so it
  needs one flat portrait, not a set.
- **`farm` has zero authored perks** (every other researchable building has 3-4). That is a content gap, not an art
  gap - flagged here because the Farm card will ship with no second door because of it.
- **Currency icons are complete** - `RpgUi/currency/` has all eight including `currency_wood.png` and
  `currency_stone.png`. An older note claiming those two were missing is stale; the cost chips will render icons, not
  word fallbacks.

---

## 5. Priority, if the morning is short

1. **The 12 re-cuts** (existing tower tier art into `Portraits/Buildings/<id>-<level>.png`). Cheapest win; turns five
   Defense ladders from flat to tiered with no new painting.
2. **The three storage ladders, 18 files.** Six tiers each, and they are the most-upgraded structures in the game.
3. **Troop tiers, 18 files.**
4. `tower_siege_tower`, `wall_wood`, `mine_crystal`, `healing_caravan` tiers - 11 files.
5. The two duplicate research icons and the gate portrait - 3 files.

**Grand total if everything lands: 41 Defense + 18 Troops + 3 misc = 62 files**, of which 12 are re-cuts of art that
already exists.

## 6. What the code does without any of this
Every card falls back, in this order: level-suffixed portrait, unsuffixed portrait, the build-palette resolver and its
alias table, `ConceptIconResolver`, then a generic hammer icon with a `FlowTrace.Warn` naming the id. **Nothing breaks
and nothing is blocked** - the tabs simply show flat or generic art. Grep a capture log for that warn line to get the
live list of ids with no art.
