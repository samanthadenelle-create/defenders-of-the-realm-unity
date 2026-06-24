# 05 - Project-Built + Committed Runtime Art Inventory

Read-only due-diligence survey of what THE PROJECT authored/wired and what
committed runtime art actually ships. Factual map only -- not a recommendation.
Vendor packs (polyperfect/Quaternius/Supercyan/etc) are covered by other agents.

Scope: everything under `C:\eoa\Assets`. "Runtime art" = what `Resources.Load`
pulls at play time (the REAL shipped art).

---

## Summary table

| Area | What it is | Size / count | Key files |
|---|---|---|---|
| Resources/Heroes | Tripo hero FBX models + controllers + mats/tex | 4 heroes (Knight/Mage/Ranger/Cleric) + 2 Supercyan prefabs | `Knight.fbx` (1.3MB), `Knight.controller` |
| Resources/Enemies | Orc/Skeleton/Troll/Dragon/Demon FBX + controllers | ~15 FBX, ~8 controllers | `Orc_Warrior.fbx`, `Orc_Tank.fbx`, `Orc_Mage.fbx`, `OrcHumanoid_*.controller` |
| Resources/Data | Canonical JSON catalogs (game data) | 40 canonical JSONs + Upgrades + recipes | `abilities.json`, `enemies.json`, `weapons.json`, `region-gates.json` |
| Resources/Structures | Building/structure art | 76 files | (village/raid structures) |
| Resources/ItemIcons | Item icon sprites | 448 files | (gear/consumable icons) |
| Resources/HudIcons | HUD icon sprites | 63 files | `Knight/knight` hero icon |
| Resources/Arena | Overworld BattleArena props/mats | 29 files | `ForestClearingArena.prefab`, rock FBX, ground mats |
| Resources/Pets | Pet models/data | 25 files | - |
| Resources/Walls | Wall segment art | 15 files | - |
| Resources/VFX | VFX prefabs | 10 files | - |
| Resources/PatriciaLight | LEFTOVER (Defend-the-Tower REMOVED) | 9 files | `tower2.fbx`, `Tower.fbm` only |
| Resources/Portraits | Character portraits | 8 files | - |
| Resources/Harvest | Echo-economy harvest art | 8 files | - |
| Resources/NPCs | NPC art | 4 files | - |
| Resources/Towers | Tower art | 4 files | - |
| Resources/Textures | Misc textures | 4 files | - |
| Resources/Dungeons | Dungeon data | 2 files | - |
| Resources/Audio | Audio assets | 2 files | - |
| Resources/Title | Title screen | 2 files | - |
| Resources/Materials | Misc material | 1 file | - |
| Resources/Dialogue | Dialogue system prefab | 1 prefab | `DialogueSystem.prefab` |
| Resources/Raids | Raid data | 1 file | - |
| Resources/Cosmetics | (only a Pets subfolder, no loose files) | 0 loose | - |
| Prefabs/ (project-authored) | Village/buildings/environment | ~17 prefabs | `Building_*` (10), `TreeOfLife.prefab` |
| _Village2/ | Village2 raid-target generator + recipes | 3 .cs + 3 .json | `Village2Generator.cs`, `Village2PlacementRecipe.json` |
| Generated/Terrain | Generated exterior terrain | 5 terrainlayers + 3 mats/data | `ExteriorTerrainData.asset`, 5 `Exterior_*.terrainlayer` |
| Generated/Animators | Generated animator controllers | 8 | `Hero.controller`, `HumanoidEnemy.controller` |
| Materials/ | Loose project materials | 5 .mat | - |
| Settings/ (URP) | URP render pipeline config | 2 | `DeNelle-URP.asset`, `DeNelle-UniversalRenderer.asset` |
| Data/ (asmdef + canonical) | Code data classes + canonical source JSON | asmdef + .cs + `Canonical/armor.json`,`weapons.json` | `DeNelle.Data.asmdef` |
| Dialogue/ (Yarn) | YarnSpinner dialogue source | ~20+ .yarn + project | `DefendersDialogue.yarnproject`, `NPC_*.yarn` |
| Localization/ | Loc tables | 5 assets | `GameStrings.asset`, `en.asset` |
| Scenes/ | Unity scenes | 19 .unity | (see Scenes section) |
| StreamingAssets/Data/Canonical | Mirror of canonical JSONs (build-loadable) | ~30+ JSON | `dialogue/dialogues.json` |

---

## Resources/Heroes -- the live hero art (Tripo)

`Resources.Load<GameObject>("Heroes/<slug>")` loads the hero FBX at runtime
(confirmed in `AtbCombatantSwapper.cs:133`, `HeroBodySwapper.cs:189`).

- 4 self-rigged Tripo heroes: `Knight.fbx` (1.3MB), `Mage.fbx`, `Ranger.fbx`, `Cleric.fbx`
  -- each with a sibling `<name>.controller`, `.fbm` texture dir, `<name>_tex/`, and
  `.tripo-extracted` marker.
- `Materials/` (Archer/Cleric/HumanCleric/skin mats), `Textures/` (basecolor/normal/
  roughness PNGs, incl. `KnightArmored_normal`), `Props/`.
- Two Supercyan rig prefabs also present: `SC_Archer.prefab`, `SC_Footman.prefab`.

## Resources/Enemies -- the live enemy art

`Resources.Load<GameObject>("Enemies/<slug>")` (confirmed `AtbCombatantSwapper.cs:298/417`,
`EnemyAnimatorFactory.cs`). Controller fallback chain ends at `Enemies/OrcHumanoid`.

- Orc family (the CURRENT V1 enemies): `Orc_Warrior.fbx` (1.16MB), `Orc_Tank.fbx`,
  `Orc_Mage.fbx`, plus `Orc_Berserker/Necromancer/Shaman.fbx`.
  Controllers: `OrcHumanoid.controller` + `_Mage/_Tank/_Warrior` variants + `OrcWarband`.
  Textures in `OrcTex/`: `Orc_{Mage,Tank,Warrior}_{basecolor,metallic,normal,roughness}.jpg`.
- Skeletons: `Skeleton_{Warrior,Mage,Rogue,Minion,Golem}.fbx` + `skeleton_texture_A` + URP mat.
- Trolls/large: `Troll.fbx`, `OgreMage.fbx`, `Demon.fbx`, `Necromancer.fbx`.
- Boss: `Dragon.fbx`, `Boss_Dragon.prefab`, `Boss.controller`, `Dragon.controller`,
  loaded by `WaveManager.cs:1256` as `Enemies/Boss_Dragon`.
- Shared controllers: `HumanoidEnemy`, `LargeEnemy`, `LargeHumanoid`. `EnemyVfxSet_Default.asset`.

## Resources/Data -- the live game-data catalogs

`Canonical/` holds 40 JSON catalogs (the runtime game data). Notable:
- `abilities.json`, `hero-talents.json`, `weaponskill-animations.json` (combat/skills)
- `enemies.json`, `enemy-roles.json`, `waves.json`, `troops.json` (enemy/wave defs)
- `weapons.json`, `armor.json`, `gear-recipes.json`, `crafting-recipes.json`,
  `consumables.json`, `loot-tables.json` (items/loot)
- `buildings.json`, `building-tiers.json`, `structures-catalog.json`, `walls.json`,
  `towers.json`, `heart.json`, `garrison-recipes.json` (village/structures)
- `pets.json`, `pet-skill-trees.json`, `quests.json`, `daily-quests.json`,
  `lore-fragments.json`, `cosmetics.json`, `packs.json`, `wallets.json` (meta/economy)
- `realm-map.json`, `scene-configs.json`, `themes.json`, `audio-mix.json`,
  `canon-strings.json`, `en.json`, `chat-phrases.json`, `dialogue/`, `dungeons/`
- Top level: `region-gates.json` (RegionGate seam config), `orientation-recipes.json`,
  `castle-south-recipe.json`. `Upgrades/`: `FarmUpgrades.json`, `WatchtowerUpgrades.json`.

Mirror lives in `StreamingAssets/Data/Canonical/` (build-loadable copy, incl.
`dialogue/dialogues.json` for the custom MVVM dialogue migration).

## Resources/PatriciaLight -- LEFTOVER only

Per canon, Defend-the-Tower was REMOVED (2026-06-09). What remains: `tower2.fbx`,
`tower2/`, `Tower.fbm/` (texture dir). 9 files total -- art kept, module/scene gone.

## Prefabs/ (project-authored)

- `Prefabs/Village/Generated/` (11): `Building_arcane-tower`, `armorer`, `crystal-mine`,
  `farm`, `forge`, `lumbermill`, `market`, `pet-house`, `workshop`, plus `Boss_Dragon`,
  `Enemy_HollowWalker`.
- `Prefabs/Buildings/` (5): `HouseA`, `HouseC`, `HouseC2`, `HouseD`, `KitTower`.
- `Prefabs/Environment/` (1): `TreeOfLife.prefab` (Heart of Elarion candidate).

## _Village2/ (raid target)

Generator-driven (no hand-placed scene). `Village2Generator.cs`, `TorchFlicker.cs`,
recipes `Village2PlacementRecipe.json`, `Village3BuildingRecipe.json`, and a
`village2-layout-dump.json` capture.

## Generated/

- `Terrain/`: 5 `Exterior_*.terrainlayer` (Dead/Grass/Mud/Snow/Stone), `ExteriorTerrainData.asset`,
  `ExteriorTerrainMaterial.mat`, `AvalonDawnSkybox.mat`.
- `Animators/`: 8 controllers/masks -- `Hero`, `HeroUpperBody.mask`, `Boss`, `Dragon`,
  `HumanoidEnemy`, `LargeEnemy`, `Npc`, `Pet`.
- `Materials/`: 1.

## Materials/ + Settings/ (URP)

- `Materials/`: 5 loose `.mat`.
- `Settings/`: URP config -- `DeNelle-URP.asset` (pipeline), `DeNelle-UniversalRenderer.asset`.

## Data/ + Dialogue/ + Localization/

- `Assets/Data/`: `DeNelle.Data.asmdef`, data classes (`AbilityData/EnemyData/PetData/
  TowerData/WaveData.cs`), source-of-truth `Canonical/armor.json` + `weapons.json`,
  and a Tests/ asmdef with catalog-integrity tests.
- `Assets/Dialogue/` (Yarn -- legacy, being dropped per WO-455): `DefendersDialogue.yarnproject`,
  `NPCs/NPC_*.yarn` (Forge/Armorer/Inn/Barracks/etc), `*_Upgrade.yarn`, `Intro/`,
  `Lore/`, `Companion/`. New dialogue authored in `Canonical/dialogue/dialogues.json`.
- `Assets/Localization/`: `GameStrings.asset` (+ `_en`, `Shared Data`), `en.asset`,
  `LocalizationSettings.asset`.

## Scenes/ (19 .unity)

| Scene | One-line |
|---|---|
| `MainCastle_Hall.unity` | Home hub / game start |
| `OuterWorld.unity` | Streams in additively; overworld walk + encounters |
| `Village2.unity` | Generator-built raid target |
| `Village.unity` | ABANDONED original village (do not hand-edit -- corruption history) |
| `Title.unity` | Title screen |
| `HeroSelect.unity` / `PetSelect.unity` | Selection screens |
| `ATBBattle.unity` | ATB combat scene (flat/static enemies) |
| `Dungeon_Demo` / `Dungeon_FolksGranary` / `Dungeon_HealersCottage` | Dungeon instances |
| `Garrison_frost_keep` / `_hill_fort` / `_ruined_keep` / `_troll_outpost` | Garrison raid maps (4) |
| `RaidBase_IronBastion` / `_fortified_garrison` / `_mage_enclave` / `_raider_camp_small` | Raid base maps (4) |

---

## CURRENT SHIPPED ART (what the live game actually loads)

Per the combat pivot (single-Knight north star, Orcs-first V1):

- **Hero:** `Resources/Heroes/Knight.fbx` (1.3MB Tripo self-rigged model) +
  `Knight.controller`, loaded via `Resources.Load<GameObject>("Heroes/Knight")`
  (`AtbCombatantSwapper`, `HeroBodySwapper`). Mage/Ranger/Cleric also committed but
  Knight is the V1 hero. HUD icon `HudIcons/Knight/knight`.
- **Enemies:** Orc family is V1 -- `Resources/Enemies/Orc_Warrior.fbx` (1.16MB),
  `Orc_Tank.fbx`, `Orc_Mage.fbx` with `OrcHumanoid_{Warrior,Tank,Mage}.controller`
  and `OrcTex/Orc_*_{basecolor,metallic,normal,roughness}.jpg`, loaded via
  `Resources.Load("Enemies/<slug>")`. Skeletons/Trolls/Dragon boss committed but
  not the V1 focus. Dragon boss = `Enemies/Boss_Dragon`.
- **Scenes:** home = `MainCastle_Hall`; `OuterWorld` streams additively; `Village2`
  = raid target; original `Village.unity` abandoned. 4 Garrison + 4 RaidBase maps.
- **Key data catalogs (40 in `Resources/Data/Canonical/`, mirrored to StreamingAssets):**
  `abilities.json`, `hero-talents.json`, `enemies.json`, `enemy-roles.json`,
  `weapons.json`, `armor.json`, `waves.json`, `buildings.json`, `structures-catalog.json`,
  `region-gates.json` (seam), `pets.json`, `quests.json`, `dialogue/dialogues.json`.
- **Note:** `Resources/PatriciaLight` is dead-Defend-the-Tower leftover art only
  (`tower2.fbx` kept). `Resources/Cosmetics` has no loose files (only a `Pets/` subfolder).
