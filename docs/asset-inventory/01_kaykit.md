# Asset Inventory 01 - KayKit Vendor Library

Read-only due-diligence survey of `C:\eoa\Assets\Models\KayKit\` (the large,
gitignored, never-catalogued KayKit vendor library) plus the duplicate top-level
`Assets\Models\KayKit Adventurers 2.0\`. FACTUAL MAP ONLY - no recommendations.

Survey date: 2026-06-24. Counts are de-duplicated unique model basenames
(every pack ships each model twice: `Assets/fbx/` AND `Assets/fbx(unity)/`,
plus a parallel `.obj` export and often a `.gltf` copy - raw `find` counts are
~2x-3x inflated). "rigged" = skinned/animated humanoid or creature; "static" =
props/tiles/environment with no skeleton.

ALL of `Assets/Models/*` is gitignored (`.gitignore:93 /Assets/Models/*`, only
`People/` re-included) - so EVERY KayKit pack below is gitignored and must be
re-imported on a fresh clone. Confirmed via `git check-ignore` on samples from
Adventurers, Character Animations, MM5, dungeon, and the top-level Adventurers
folder - all ignored.

---

## Summary Table

| Pack | Type | Unique models | Rigged | Gitignored | Used in code |
|---|---|---|---|---|---|
| KayKit Adventurers 2.0 | characters + weapons/props | ~71 (9 chars, ~58 props) | YES (chars) | yes | yes (refs) |
| KayKit Character Animations 1.1 | SHARED RETARGET RIG (clips + mannequins) | 16 rigs | YES | yes | YES (8 refs) |
| KayKit Skeletons 1.1 | skeleton-rig weapon/shield attachments + rigs | ~23 | rig + attachments | yes | yes (1 ref) |
| KayKit Mystery Monthly Series 4 | monthly CHARACTERS (12 mo) | ~80 (19 char fbx) | YES | yes | yes (Orc Raider) |
| KayKit Mystery Monthly Series 5 | monthly CHARACTERS (12 mo) | ~64 (14 char fbx) | YES | yes | yes (refs) |
| KayKit Fantasy Weapons Bits 1.0 | weapons/shields/staves | ~48 | static | yes | yes (refs) |
| KayKit Dungeon Remastered 1.1 | dungeon environment/props | ~283 | static | yes | YES (loaded) |
| KayKit Medieval Hexagon Pack 1.0.1 | hex-tile buildings/terrain | ~403 | static | yes | YES (14 refs) |
| KayKit Forest Nature Pack 1.0 | trees/foliage/rocks | ~1588 | static | yes | YES (5 refs) |
| KayKit City Builder Bits 1.0 | city buildings/props | ~73 | static | yes | no |
| KayKit Resource Bits 1.0 | resource/gather props | ~132 | static | yes | no |
| KayKit RPG Tools Bits 1.0 | RPG UI/table props | ~69 | static | yes | yes (2 refs) |
| KayKit Furniture Bits 1.0 | furniture | ~74 | static | yes | yes (3 refs) |
| KayKit Block Bits 1.0 | modular blocks | ~58 | static | yes | no |
| KayKit Board Game Bits 1.0 | board-game pieces | ~243 | static | yes | no |
| KayKit Halloween Bits 1.0 | seasonal props | ~102 | static | yes | no |
| KayKit Holiday Bits 1.0 | seasonal props | ~138 | static | yes | no |
| KayKit Platformer Pack 1.0 | platformer env/props | ~525 | static | yes | no |
| KayKit Prototype Bits 1.1 | greybox/prototype | ~88 | static | yes | no |
| KayKit Restaurant Bits 1.0 | restaurant props | ~225 | static | yes | no |
| KayKit Space Base Bits 1.0 | sci-fi base props | ~69 | static | yes | no |
| dungeon/ (working copy) | dungeon env (gltf+fbx) | ~422 fbx / 211 gltf | static | yes | yes |
| medieval/ (working copy) | medieval buildings (gltf) | ~72 gltf | static | yes | yes (Hexagon-derived) |
| weapons/ (working copy) | 7 weapon gltf | ~7 gltf | static | yes | yes |
| anim/ characters/ enemies/ | EMPTY placeholder dirs | 0 | - | yes | path-referenced |

Note: `KayKit Adventurers 2.0` exists in BOTH `Assets/Models/KayKit/` and as a
top-level `Assets/Models/KayKit Adventurers 2.0/` - same pack, duplicated.

---

## SHARED RIG (the key finding) - KayKit Character Animations 1.1

This is the central "shared humanoid rig" asset. It ships the FULL animation clip
library split into two body scales x clip categories, plus the two retarget
mannequins:

- `Rig_Medium_*`: MovementBasic, MovementAdvanced, CombatMelee, CombatRanged,
  General, Simulation, Special, Tools  (8 rigs)
- `Rig_Large_*`: MovementBasic, MovementAdvanced, CombatMelee, General,
  Simulation, Special  (6 rigs)  [Large has no CombatRanged / no Tools]
- `Mannequin Character/characters/Mannequin_Medium.fbx` and `Mannequin_Large.fbx`
  - the rig-only avatar bodies used as retarget source/preview.

EVERY KayKit character pack rides this SAME rig. Confirmed: each of these packs
ships its own copy of the matching `Rig_Medium_General`, `Rig_Medium_MovementBasic`,
`Rig_Large_General`, `Rig_Large_MovementBasic` fbx alongside its character meshes:
- **KayKit Adventurers 2.0** (Animations/fbx/Rig_Large + Rig_Medium)
- **KayKit Skeletons 1.1** (Animations/fbx/Rig_Large + Rig_Medium)
- **KayKit Mystery Monthly Series 4** (Animations/fbx)
- **KayKit Mystery Monthly Series 5** (Animations/fbx)

So Adventurers + Skeletons + MM4 + MM5 characters are all retargetable from the
one Character Animations 1.1 clip set (Medium for human-scale, Large for big
bodies / golems). This is the shared-humanoid-rig backbone for a hero/enemy/equip
pipeline. (`Mannequin` is referenced in 3 code files; Character Animations in 8.)

---

## Playable CHARACTERS (rigged, ride the shared rig)

### KayKit Adventurers 2.0 - Characters/fbx/ (9 fbx)
Barbarian, Barbarian_Large, Druid, Engineer, Knight, Mage, Ranger, Rogue,
Rogue_Hooded. (Barbarian_Large uses the Large rig; rest are Medium.)

### KayKit Mystery Monthly Series 4 - 12 monthly drops (19 character fbx)
Each month = one themed character under `<NN - Month - Name>/character[s]/`:
OrcRaider, Driver, Monster + MonsterCostume (Sept), Werewolf_Man + Werewolf_Wolf,
Animatronic_Normal + Animatronic_Creepy, ActionFigure, SpaceRanger +
SpaceRanger_FlightMode, Ninja, Survivalist, Paladin + Paladin_with_Helmet, Clown,
Robot_One + Robot_Two. (Fantasy-relevant: OrcRaider, Paladin, Werewolf.)

### KayKit Mystery Monthly Series 5 - 12 monthly drops (14 character fbx)
CombatMech, Superhero, **BlackKnight**, **Vampire**, **Witch**, Helper_A/Helper_B,
FrostGolem, Clanker, Protagonist_A/Protagonist_B, Hiker, Caveman, **Tiefling**.
(Fantasy-relevant: BlackKnight, Vampire, Witch, Tiefling, FrostGolem.)

### KayKit Skeletons 1.1 - skeleton enemy rig
Ships the 4 shared rigs + skeleton WEAPON/SHIELD attachment meshes (the skeleton
body itself is delivered through the rig/mannequin, not as a separate roster fbx
under characters/ - that subfolder is empty in this install). Attachments below.

---

## WEAPONS / SHIELDS coverage

### KayKit Fantasy Weapons Bits 1.0 (~48 unique)
Swords A-G (7), axes A-D (4), hammers A-D (4), daggers A-C (3), bows A-C
(+_withString variants), arrows A-C, staves A-D (4), wands A-B, spears A-B,
halberd, scythe, fistweapons A-C (+left/right/stacked), shields A-D (4).
Broadest standalone weapon set.

### KayKit Adventurers 2.0 - Assets (weapon/prop subset, ~58)
Melee: sword_1handed, sword_2handed (+color), axe_1handed/2handed (+_Large),
dagger, druid_staff, staff, wand, engineer_Wrench.
Ranged: bow (+withString), crossbow_1handed/2handed, shotgun, arrow_bow/crossbow
(+bundle), quiver, smokebomb, turret_base.
Shields: shield_round (+color/+barbarian/+barbarian_Large), shield_square (+color),
shield_spikes (+color), shield_badge (+color).
Consumables/props: potions (small/medium/large/huge x blue/green/orange/red),
mug_empty/full (+_Large), spellbook_open/closed. (`_Large` = scaled for Large rig.)

### KayKit Skeletons 1.1 - skeleton-rig attachments (~19 meshes)
Skeleton_Axe, _Blade, _Mace (+_Large), _Dagger, _Scythe, _Staff, _Crossbow,
_Quiver, _Arrow (+_Half/_Broken/_Broken_Half), Skeleton_Golem_Axe (+_Large),
Skeleton_Shield_Small_A/B, Skeleton_Shield_Large_A/B.

### weapons/ working copy (7 gltf)
arrow_bow, bow_withString, shield_round, spellbook_closed, staff, sword_1handed,
wand - a curated gltf subset (Adventurers-derived) used in-engine.

---

## ENVIRONMENT / PROP packs (static, no rig)

- **KayKit Dungeon Remastered 1.1** (~283): full modular dungeon - walls, floors,
  stairs, doors, banners (blue/brown/green/red + patternA), pillars, props.
  Also present as the working `dungeon/` copy (gltf+fbx, ~422 fbx / 211 gltf).
  LOADED in-game (DungeonComposer/DungeonSceneBuilder; literal `fbx(unity)` paths).
- **KayKit Medieval Hexagon Pack 1.0.1** (~403): hex-tile terrain + buildings
  (archeryrange, barracks, blacksmith, church, homes, lumbermill, market, etc.,
  in color variants). Most-referenced env pack (14 code refs). Working `medieval/`
  copy (~72 gltf) is the curated in-engine subset.
- **KayKit Forest Nature Pack 1.0** (~1588 - by far the largest): trees, foliage,
  rocks, terrain. 5 code refs. (Canon note: world trees come via this pack /
  ExteriorTerrainBuilder.)
- **KayKit City Builder Bits 1.0** (~73), **Resource Bits 1.0** (~132),
  **RPG Tools Bits 1.0** (~69, 2 refs), **Furniture Bits 1.0** (~74, 3 refs),
  **Block Bits 1.0** (~58), **Board Game Bits 1.0** (~243), **Halloween Bits 1.0**
  (~102), **Holiday Bits 1.0** (~138), **Platformer Pack 1.0** (~525),
  **Prototype Bits 1.1** (~88), **Restaurant Bits 1.0** (~225),
  **Space Base Bits 1.0** (~69). All static prop/tile kits.

---

## Working-copy / placeholder dirs (inside KayKit/)

- `dungeon/`, `medieval/`, `weapons/` - curated gltf (+fbx for dungeon) subsets
  extracted from the full packs for in-engine use; these are what the builders
  reference by path.
- `anim/`, `characters/`, `enemies/` - **EMPTY** directories (only `.meta`).
  Code references a path `Assets/Models/KayKit/enemies/<key>.glb`
  (WaveData.cs / WaveManager.cs, e.g. `Skeleton_Minion.glb`) but no such file
  exists here - dangling/planned target, currently unpopulated.

---

## Code-usage detail

`Assets/Editor/KayKitMaterials.cs` is a blanket URP material-fixer that processes
everything under `Assets/Models/KayKit/` (returns a shared `Universal Render
Pipeline/Lit` for any KayKit import) - so the whole library is touched by import
post-processing, but only a subset is actually instantiated in scenes.

Literal asset-path string references found in code (actually loaded):
- Medieval Hexagon Pack `Assets/fbx(unity)/...` (x3)
- Dungeon Remastered 1.1 `Assets/fbx(unity)/...` (x2)
- Forest Nature Pack `Assets/fbx(unity)/...`
- Mystery Monthly Series 4 `.../Orc Raider/character/OrcRaider.fbx`
- Mystery Monthly Series 5 (path ref)
- Character Animations 1.1 `Animations/fbx/...`
- Skeletons 1.1 `assets/fbx(unity)/...`

Reference COUNTS in `.cs` under _Modules/Resources/Editor (incl. comments/docs):
Medieval Hexagon 14, Character Animations 8, Adventurers 5, Forest Nature 5,
Mystery Monthly 4, Furniture 3, Mannequin 3, RPG Tools 2, Skeletons 1.
No code refs found for: City Builder, Resource, Block, Board Game, Halloween,
Holiday, Platformer, Prototype, Restaurant, Space Base.
