# ART REQUEST - Manage tab portraits (Defense, Troops, Research)

**Raised:** 2026-09-06 (CLI) at the owner's offer - *"give me a detailed description and I will work with Codex in the
morning to get all the detailed art for all the different components"*.
**Why now:** WO-1422 rebuilds Manage - Defense, Research and Troops into the same portrait-rail + selected-card
workspace that WO-1418 gave Buildings. **The code ships without any of this art** (it falls back), but every card in
those three tabs will show either flat art at all tiers or a generic icon until these land.

âš  **Everything below was measured this session by enumerating `Assets/Resources/`.** Counts and filenames are read off
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

âš  **Section 6 describes the LEGACY `ManageScreenPanel` card ONLY. The new Manage BUILD/ARMY tile grid has NO fallback
chain at all** - `ManageWorkspacePanel.cs:565` calls `ManageArt.LoadSprite(tile.PortraitKey)` once and paints the
warm-tan placeholder disc if it misses. That is deliberate and it should stay: a wrong icon is a lie, a blank frame that
logs once is honest. But it means a missing tile on the GRID is invisible in a way it never was on the card, which is
why section 7 exists.

---

## 7. BUILD TILE GRID - the exact missing set (added 2026-09-06, portrait lane)

Measured this session against the tree, not inferred. Pinned by
`Assets/Editor/Regression/ManagePortraitCoverageRegression.cs`, whose dated exemption list holds exactly the ids below
and **FAILS the moment one of them starts resolving**, so a delivered file forces its own line out of this document.

### 7a. FIXED IN CODE, NOT AN ART REQUEST - the 20 tier keys

`BuildBuildingChoices` emitted `Portraits/<ladder>[-N]` (the mixed root), so **every owned building above level 1
painted a blank tile in the grid while its art sat in `Portraits/Buildings/`**. All 26 authored tiers of the six
ladders are present in that folder; the root was missing all twenty of the `-2..-6` keys. Re-pointed to
`ManageArt.BuildingPortraitKey`. **No art needed. Nothing to draw.**

---

## ✅ OWNER RULING 2026-09-06 - OPTION A. This section is now settled.

**Building art is ONE folder - `Assets/Resources/Portraits/Buildings/` - and every file is named by its CATALOG ID.**
Matches the 27 files already there, matches `heart.png` as delivered, and matches what section 0 of this document
already asked for.

Landed with the ruling: `ComposeUnplacedItem` now keys through `ManageArt.BuildingPortraitKey(row.Id, 0)`, and
`manageArtKey` goes back to being what the catalog note always called it - **the art-to-id join label, never a
Resources key.** Both halves are pinned by `ManagePortraitCoverageRegression`
(`[unplaced-uses-building-portrait-key]`, `[vm-uses-building-portrait-key]`).

⭐ **The exemption list was re-keyed in the same change, and that is the part that matters.** It used to hold the 24
Sheet-A names. Nobody will ever author `building-store.png` - they will author `market.png` - so those exemptions
could never expire and would have become permanent silent skips. Keyed off the catalog id, they are now the *exact*
filename the artist writes: **drop the PNG in and the suite FAILS as stale until its line is deleted from this
document.** The list cannot rot.

### 7b. THE ART REQUEST - 20 files, exact filenames

Author each as `Assets/Resources/Portraits/Buildings/<filename>`, 1024x1024 PNG, matching the look of the 27 files
already in that folder (round-medallion friendly, readable at 112 px and at ~150 px).

| # | Filename to author | Building, as the player sees it | Sheet A tile |
|---|---|---|---|
| 1 | `healing_caravan.png` | Healing Caravan | `building-healing-caravan` |
| 2 | `pet-house.png` | Echo Hollow | `building-echo-hollow` |
| 3 | `workshop.png` | Crafting Station | `building-crafting-station` |
| 4 | `market.png` | Store | `building-store` |
| 5 | `collector_farm.png` | **Quarry** | `building-quarry` |
| 6 | `collector_forge.png` | **Iron Mine** | `building-iron-mine` |
| 7 | `collector_lumbermill.png` | **Lumber Mill** | `building-lumber-mill` |
| 8 | `lumberyard.png` | Lumberyard (wood store) | `building-lumberyard` |
| 9 | `silo.png` | Stoneyard (stone store) | `building-stoneyard` |
| 10 | `foundry.png` | Foundry (iron store) | `building-foundry` |
| 11 | `mine_crystal.png` | Crystal Mine | `building-crystal-mine` |
| 12 | `jeweler.png` | Jeweler | `building-jeweler` |
| 13 | `tower_ground_archer.png` | Archer Tower | `building-archer-tower` |
| 14 | `tower_ballista.png` | Ballista | `building-ballista` |
| 15 | `tower_arcane_spire.png` | Arcane Spire | `building-arcane-spire` |
| 16 | `tower_catapult.png` | Catapult | `building-catapult` |
| 17 | `tower_siege_tower.png` | Sky Ballista (Anti-Air) | `building-sky-ballista` |
| 18 | `wall_wood.png` | Wooden Palisade | `building-wooden-palisade` |
| 19 | `wall_stone.png` | Stone Wall | `building-stone-wall` |
| 20 | `gate_stone.png` | Stone Gate | `building-stone-gate` |

**Already covered - do NOT draw these four.** `arcane-tower` (Cathedral of Magic), `armorer`, `barracks`, `forge`
(Weaponsmith) each already have art under their own catalog id in that folder, tiers and all. Sheet A draws a tile for
them; it is not needed.

⛔ **Rows 5-7 are the ones that will produce wrong files if skimmed.** `collector_farm`, `collector_forge` and
`collector_lumbermill` are **catalog ids**; the existing `farm.png`, `forge.png`, `lumbermill.png` are named for
**ladder ids**, which are a different namespace that happens to overlap. They are NOT the same picture and the display
names prove it - `collector_farm` is the **Quarry**, not a farm, and `collector_forge` is the **Iron Mine**, not a
forge.

### 7c. TWO THINGS THAT NEED A RULING, NOT ART

**Q1 - `mill` (Mill / gristmill): is this still content?** It has a catalog entry, no tile on any of the three sheets,
and **no `manageArtKey`** - so it is invisible to the sheets *and* to the oracle, which skips rows with no art key.
It is not on the list above because nothing says it should be. It is being raised rather than quietly dropped or
quietly included. **If it is still content it needs a `manageArtKey` row and a `mill.png`; if it is not, it should
come out of the catalog.**

**Q2 - `collector_forge` ("Iron Mine") shares the `forge` upgrade ladder with `forge` ("Weaponsmith").** Measured
from `repo.collectorBuildingId`. That means the *placed* Iron Mine already paints the **Weaponsmith's** portrait, and
has for as long as that path has existed - a wrong icon, not a blank one, and therefore invisible to every oracle.
`collector_farm` -> ladder `farm` and `collector_lumbermill` -> ladder `lumbermill` have the same shape but no id
collision. **This is a data question about whether an Iron Mine should ride the Weaponsmith's ladder at all**, and it
is out of the portrait lane's scope. Authoring `collector_forge.png` fixes the *unplaced* tile; the placed one still
needs this ruling.

### 7d. Reference material - do NOT import

`ArtSource/ManageUiSliced/` holds 231 PNGs sliced from the contact sheets. They carry the **sheet background instead
of transparency** and some crops caught neighbouring labels, so they are a **reference set for confirming what the
sheets depict**, never a drop-in pack. One thing they surfaced: a sheet label reads `filter-chic`, a typo for
`filter-civic`.

