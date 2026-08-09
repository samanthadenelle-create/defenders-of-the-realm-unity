# KayKit — SME Reference (Canon)

**Date:** 2026-07-11 (overnight SME research)
**Pack root:** `Assets/Models/KayKit/` (21 packs + curated live-set folders) plus a duplicate `Assets/Models/KayKit Adventurers 2.0/` — paths are repo-root-relative; the root is machine-dependent
**Author/brand:** Kay Lousberg — kaylousberg.com / kaylousberg.itch.io
**License:** CC0 (all itch.io packs, including paid EXTRA/SOURCE tiers) — commercial use unrestricted, no attribution required
**Status of this doc:** supersedes-and-extends `docs/kaykit-asset-catalog.md` (2026-05-19 creative pick-list) and `docs/KAYKIT_NOTES.md` (2026-06-05 technical notes). Those remain useful; this is the verified deep reference.

> ⚠ **Gitignore fact (load-bearing):** everything under `Assets/Models/*` is gitignored
> (`.gitignore:98`). The KayKit packs exist only on this machine. Every build-shipping KayKit
> asset was **copied into a committed folder** (`Assets/Resources/...`) — see §2.3. A fresh
> clone has NO KayKit models; all consuming code degrades to primitives/warnings by design.

---

## Table of contents

1. [Inventory — what is on disk](#1-inventory)
   - 1.1 Pack-by-pack table
   - 1.2 Character rosters (every model, by name)
   - 1.3 Weapons
   - 1.4 Animation content
   - 1.5 Texture / material scheme
   - 1.6 Curated "live set" folders + known on-disk gaps
2. [How WE consume it](#2-how-we-consume-it)
   - 2.1 Editor-side consumers (import, materials, builders)
   - 2.2 Runtime consumers
   - 2.3 The committed-copy pattern (build safety)
   - 2.4 Live in-game vs unused
3. [Intended usage (per Kay's docs)](#3-intended-usage)
4. [Web research — packs, licensing, catalog](#4-web-research)
5. [Opportunities + gaps](#5-opportunities--gaps)
6. [Executive summary](#6-executive-summary)

---

## 1. Inventory

### 1.1 Pack-by-pack table (verified on disk 2026-07-11)

Counts are file counts under each folder (fbx = models; every pack ships each model twice —
raw `fbx/` and Unity-tuned `fbx(unity)/` — so unique-model count is roughly half the fbx count).

| Pack folder | FBX | PNG | .mat | Size (MB) | Theme / role |
|---|---:|---:|---:|---:|---|
| KayKit Adventurers 2.0 | 129 | 94 | 47 | 28 | 9 hero characters + ~60 props/weapons |
| KayKit Skeletons 1.1 | 42 | 14 | 9 | 17 | Undead enemy family (⚠ character FBXs MISSING on disk — §1.6) |
| KayKit Character Animations 1.1 | 16 | 1 | 2 | 30 | Shared clip library (Rig_Medium + Rig_Large) + 2 mannequins |
| KayKit Dungeon Remastered 1.1 | 566 | 33 | 6 | 57 | Modular dungeon interiors (walls, floors, stairs, traps, props) |
| KayKit Fantasy Weapons Bits 1.0 | 96 | 6 | 8 | 16 | 48 unique weapons (A–G variants) — the gear-v1 weapon source |
| KayKit Medieval Hexagon Pack 1.0.1 | 808 | 67 | 111 | 86 | Hex-tile medieval town (buildings in team-color variants + neutral) |
| KayKit Forest Nature Pack 1.0 | 3176 | 31 | 50 | 178 | Trees, rocks, bushes, terrain (multi-season variants) |
| KayKit Mystery Monthly Series 4 | 128 | 94 | 107 | 84 | 19 bonus rigged characters (see roster) |
| KayKit Mystery Monthly Series 5 | 110 | 79 | 96 | 44 | 14 bonus rigged characters (see roster) |
| KayKit City Builder Bits 1.0 | 146 | 5 | 8 | 18 | Modern city (roads, cars, blocks) |
| KayKit Furniture Bits 1.0 | 148 | 11 | 8 | 14 | Indoor furniture (beds, desks, shelves, lamps) |
| KayKit Halloween Bits 1.0 | 204 | 5 | 8 | 21 | Graveyard/spooky props |
| KayKit Holiday Bits 1.0 | 276 | 7 | 11 | 41 | Christmas/winter props |
| KayKit Resource Bits 1.0 | 264 | 7 | 8 | 38 | Gems, ore, gold, crates, food |
| KayKit Restaurant Bits 1.0 | 450 | 5 | 8 | 44 | Kitchen/restaurant props |
| KayKit RPG Tools Bits 1.0 | 138 | 18 | 23 | 16 | Anvils, pickaxes, lanterns, keys, maps |
| KayKit Board Game Bits 1.0 | 486 | 381 | 332 | 77 | Chess, cards, dice, chips (only pack with a ReadMe.txt on disk) |
| KayKit Block Bits 1.0 | 116 | 8 | 8 | 18 | Grid-block level pieces |
| KayKit Platformer Pack 1.0 | 1050 | 36 | 45 | 104 | Platformer level kit |
| KayKit Prototype Bits 1.1 | 173 | 12 | 12 | 22 | Greybox/prototyping shapes |
| KayKit Space Base Bits 1.0 | 138 | 7 | 12 | 18 | Sci-fi base props |
| `dungeon/` (curated live set) | 422 | 2 | 4 | 24 | glTF+FBX subset of Dungeon Remastered — **wired into builders** |
| `medieval/` (curated live set) | 0 (72 gltf) | 2 | 0 | 8 | glTF subset of hex-pack buildings |
| `weapons/` (curated live set) | 0 (7 gltf) | 0 | 0 | 1 | glTF subset: bow, sword, staff, wand, shield, spellbook, arrow |
| `anim/`, `characters/`, `enemies/` | 0 | 0 | 0 | 0 | **EMPTY** on this checkout (see §1.6) |

Total on disk: ~970 MB, ~8,600 FBX files (~4,300 unique models).

The duplicate `Assets\Models\KayKit Adventurers 2.0\` (outside the `KayKit\` root) is an
older copy with textures loose beside the FBX files; the canonical copy is the one under
`KayKit\`. Both are gitignored. Candidate for deletion at asset-purge time (memory:
`asset-purge-deferred-to-polish-end`).

### 1.2 Character rosters (every rigged character, by name)

**Adventurers 2.0** — `KayKit Adventurers 2.0/Characters/fbx/` (9 FBX):
Barbarian, Barbarian_Large (Rig_Large), Druid, Engineer (with turret_base prop), Knight,
Mage, Ranger, Rogue, Rogue_Hooded. We own the EXTRA tier (Engineer/Druid/Barbarian_Large
plus 3 alt textures per character are EXTRA-tier content and are present).

**Skeletons 1.1** — sample images show the roster: Warrior, Rogue, Mage, Minion, plus
EXTRA-tier Golem (Rig_Large) and Necromancer. ⚠ On this checkout the
`KayKit Skeletons 1.1/characters/fbx/` folder is **EMPTY** — only weapons, animations,
textures, and sample images are present (§1.6).

**Mystery Monthly Series 4** (July 2023 – June 2024, one drop per month, 19 character FBX):
| Month | Characters |
|---|---|
| 1 Jul 2023 | OrcRaider |
| 2 Aug 2023 | Driver (+ car assets) |
| 3 Sep 2023 | MonsterCostume, Monster |
| 4 Oct 2023 | Werewolf_Man, Werewolf_Wolf |
| 5 Nov 2023 | Animatronic_Normal, Animatronic_Creepy |
| 6 Dec 2023 | ActionFigure |
| 7 Jan 2024 | SpaceRanger, SpaceRanger_FlightMode |
| 8 Feb 2024 | Ninja |
| 9 Mar 2024 | Survivalist |
| 10 Apr 2024 | Paladin, Paladin_with_Helmet |
| 11 May 2024 | Clown |
| 12 Jun 2024 | Robot_One, Robot_Two |

**Mystery Monthly Series 5** (July 2024 – June 2025, 14 character FBX):
| Month | Characters |
|---|---|
| 1 Jul 2024 | CombatMech |
| 2 Aug 2024 | Superhero |
| 3 Sep 2024 | BlackKnight (large) |
| 4 Oct 2024 | Vampire |
| 5 Nov 2024 | Witch |
| 6 Dec 2024 | Helper_A, Helper_B |
| 7 Jan 2025 | FrostGolem (Rig_Large) |
| 8 Feb 2025 | Caveman |
| 9 Mar 2025 | Clanker (large) |
| 10 Apr 2025 | Protagonist_A, Protagonist_B |
| 11 May 2025 | Hiker |
| 12 Jun 2025 | Tiefling |

Both Mystery series ship their own `Animations/fbx/Rig_Medium` + `Rig_Large` folders
(the shared clip library, re-bundled) plus per-character accessories and alt textures.

### 1.3 Weapons

**Fantasy Weapons Bits 1.0** (`Assets/fbx(unity)/`, 48 unique meshes) — the source of the
hero's visual gear (§2): arrow_A–C; axe_A–D; bow_A–C (each with `_withString` variant);
dagger_A–C; fistweapon_A–C (with stacked/left/right variants); halberd; hammer_A–D;
scythe; shield_A–D; spear_A–B; staff_A–D; sword_A–G; wand_A–B.

**Adventurers 2.0 accessories** (`Assets/fbx(unity)/`, ~60 meshes): 1H/2H swords and axes
(with Large variants for Barbarian_Large), bow/bow_withString, crossbow_1handed/2handed,
dagger, druid_staff, engineer_Wrench, staff, wand, 5 shields (badge/round/round_barbarian/
spikes/square, each with `_color` variant), quiver, arrow bundles, ammo crates, smokebomb,
spellbook (open/closed), turret_base, mugs, and 16 potions (4 sizes × 4 colors).

**Skeletons 1.1 weapons** (`assets/fbx(unity)/`, 19 meshes): Skeleton_Axe, Blade, Crossbow,
Dagger, Mace (+_Large), Golem_Axe (+_Large), Scythe, Staff, Quiver, arrows (whole/half/
broken), 4 shields (Small/Large × A/B).

### 1.4 Animation content

**Character Animations 1.1** is the shared library. One multi-take FBX per category:

- `Animations/fbx/Rig_Medium/`: `Rig_Medium_General`, `_MovementBasic`, `_MovementAdvanced`,
  `_CombatMelee`, `_CombatRanged`, `_Special`, `_Simulation`, `_Tools` (8 FBX).
- `Animations/fbx/Rig_Large/`: same minus CombatRanged and Tools (6 FBX).
- `Mannequin Character/characters/Mannequin_Medium.fbx` + `Mannequin_Large.fbx` (preview rigs).

Clip families: locomotion (idle/walk/run/jump/hop), melee (1H attack, heavy, block, spin,
combo), ranged (1H/2H shoot, bow), general (defeat, cheer, wave, pickup, throw, interact,
dance, roll, dash ×4 directions), simulation and tool loops (mining/harvest). Adventurers 2.0,
Skeletons 1.1, and both Mystery series each also ship a `MovementBasic` + `General` subset of
the same library, so characters animate even without the standalone pack.

### 1.5 Texture / material scheme

- KayKit ships **no Unity .mat files**. Each pack has one or a few shared **palette atlases**
  (flat-color texture the whole pack UV-maps into): `hexagons_medieval.png` (hex pack),
  `dungeon_texture.png` (dungeon), `<character>_texture.png` per character (plus
  `_alt_A/B/C` variants — we own all four texture variants for all 7 Adventurer classes),
  `skeleton_texture_A/B.png`.
- Left alone, Unity auto-imports the FBX-embedded material as Built-in Standard → renders
  **white or magenta in URP**. Two project systems fix this (§2.1): an import postprocessor
  for future imports and `KayKitMaterials.FixAllMaterials` for repair. The result is one
  shared `Universal Render Pipeline/Lit` .mat per pack (smoothness 0, metallic 0 — the flat
  low-poly look), remapped into every FBX importer.

### 1.6 Curated "live set" folders + known on-disk gaps

The six short-name folders at the KayKit root (`characters/`, `enemies/`, `medieval/`,
`dungeon/`, `weapons/`, `anim/`) are the historical "wired-in" glTF subset. Current state:

| Folder | State 2026-07-11 | Consumed by |
|---|---|---|
| `dungeon/` | **POPULATED** (211 glTF + 422 FBX — full Dungeon Remastered set as glTF) | `KayKitChallengeOutpostBuilder` (`KayFolder` const, line 31), `DungeonChainBuilder` (`KayDungeonFolder`, line 40), `CastleBuilderTester` dungeon resolve |
| `medieval/` | POPULATED (72 glTF: barracks, blacksmith, castle, church, mines, tavern, towers, townhall, windmill, well, etc.) | legacy Village wiring; hex-pack `fbx(unity)/` is now the primary source |
| `weapons/` | POPULATED (7 glTF: arrow_bow, bow_withString, shield_round, spellbook_closed, staff, sword_1handed, wand) | legacy; superseded by committed `Resources/Heroes/Props/` |
| `anim/`, `characters/`, `enemies/` | **EMPTY** | `WaveData.cs:83` still documents `Assets/Models/KayKit/enemies/<key>.glb` — a stale comment; the real path is `Resources/Enemies/` (§2.2) |

**Gap ledger (things code expects that disk doesn't have, or vice versa):**
1. `KayKit Skeletons 1.1/characters/fbx/` is **empty** — the skeleton character models were
   never copied to this checkout (weapons/anims/textures are present). The game does not
   break: the live skeleton enemies are separate re-rigged FBXs committed under
   `Assets/Resources/Enemies/` (Skeleton_Minion/Warrior/Rogue/Mage/Healer/Golem — the
   AccuRig/Mixamo Humanoid family that carries KayKit's `skeleton_texture_A.png`).
   To pull more KayKit skeleton variants, re-download the pack.
2. `WaveData.cs:83` comment points at the empty `enemies/` folder — stale, harmless.
3. Only `KayKit Board Game Bits 1.0/Assets/ReadMe.txt` survived import — every other pack's
   ReadMe/license text file was not copied in. (License is CC0 regardless — §4.)

---

## 2. How WE consume it

### 2.1 Editor-side consumers (import pipeline + scene builders)

41 files under `Assets/Editor` reference KayKit. The load-bearing ones:

| Consumer | File (line) | What it does |
|---|---|---|
| **Import postprocessor** | `Assets/Editor/AssetImportPostprocessor.cs:76` (`KayKitRoot`), `:130` (model), `:173` (material), `:216` (texture) | Auto-applies import settings to everything under `Assets/Models/KayKit/`: Read/Write off, lightmap UVs for statics only, **Animation Type = Generic** (`:29`), and wires each auto-material to URP/Lit + the pack's palette atlas |
| **Material repair** | `Assets/Editor/KayKitMaterials.cs` (menu `Tools ▸ DeNelle ▸ Fix KayKit Materials`; batch `DeNelle.Editor.KayKitMaterials.FixAllMaterials`) | Repairs already-imported models: finds the palette atlas per folder (`FindPaletteTexture`), creates ONE shared URP/Lit .mat, remaps every FBX importer to it. Idempotent |
| **Animator factory** | `Assets/Editor/AnimatorSetup.cs:83` (`AnimRoot` = `KayKit Character Animations 1.1/Animations/fbx/`) | Builds ALL shared controllers from the KayKit clip library and writes them into committed `Resources/Enemies/`: `HumanoidEnemy` (Rig_Medium), `LargeEnemy` (Rig_Large), `Boss`, `Hero`, `Pet` |
| **Challenge outpost builder** | `Assets/Editor/KayKitChallengeOutpostBuilder.cs:31` (`KayFolder = Assets/Models/KayKit/dungeon`) | Script-builds `Assets/Scenes/KayKitChallengeOutpost.unity` — triple-ring enemy outpost dressed with dungeon tiles + NavMesh bake. Menu `Defenders/World/Build KayKit Challenge Outpost` |
| **Dungeon chain builder** | `Assets/Editor/DungeonChainBuilder.cs:40` (same `dungeon/` folder) | Bakes the Outpost1 → Dungeon → Outpost2 walkable chain scenes |
| **Village walls** | `Assets/Editor/VillageSceneBuilder.Walls.cs:29–163` | `wall_straight.fbx`, `wall_corner_A_outside.fbx`, `wall_straight_gate.fbx` from `Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/buildings/neutral/` (`HexPackRoot` at `VillageSceneBuilder.cs:85–90`). Gate visual now hidden in favor of polyperfect gate (WO-136 note at `:156`) |
| **City manifest** | `Assets/Editor/CityManifest.json:34` (`kaykit_hex` root) + `VillageSceneBuilder.CityManifest.cs:283` | Village2/city buildings resolve `kaykit_hex/<name>` prefab strings; 4 mandated KayKit corner watchtowers |
| **Bow prop builder** | `Assets/Editor/BowPropBuilder.cs` | Turns the committed KayKit bow FBX into `Resources/Heroes/Props/Bow.prefab` with a URP atlas material |
| **Anim proof importer** | `Assets/Editor/ActionClipImporter.cs:234` | Imports Mystery Series 4 OrcRaider.fbx clips |
| **Gear catalog generator** | `Assets/Editor/Catalog/GearCatalogGenerator.cs:32,570` | Documents that the ~9k-FBX KayKit warehouse lives outside Resources (not build-loadable) |
| Other builders | `CastleBuilderTester.cs:764+`, `CastleHomeBuilder.cs`, `CastleHubBuilder.cs:363`, `DungeonSceneBuilder.cs`, `FolksGranaryBuilder.cs`, `ExteriorTerrainBuilder.cs`, `VillageSceneBuilder.Dressing/.Characters/.Content/...` | Resolve KayKit dungeon/hex/forest pieces opportunistically, always with missing-pack fallbacks |

**Key import decision:** our postprocessor imports KayKit rigs as **Generic**, not Humanoid
(`AssetImportPostprocessor.cs:29`). We then drive them with KayKit's OWN clips (no retarget
needed — same skeleton), via the controllers AnimatorSetup builds. This diverges from Kay's
official Humanoid recommendation (§3) and is deliberate: Generic playback of native clips is
exact, cheap, and avoids Humanoid muscle-space distortion of the stylized proportions.

### 2.2 Runtime consumers (`Assets/_Modules`, 37 files)

| Consumer | File (line) | Live? | What it does |
|---|---|---|---|
| **EquipmentController** | `Assets/_Modules/Village/Hero/EquipmentController.cs:71–213` | **LIVE** | The gear-v1 weapon-id → KayKit mesh map. `mage_starter→wand_A`, `mage_oak/arcane/void→staff_A/B/C`, `aegis_aetherstaff→staff_D`, `knight_iron/oath/dawn→sword_D/F/G`, `ranger_starter/yew/storm→bow_A/B/C`, `aegis_hallowed_censer→hammer_A`, shields→`shield_A`; fallback inference at `:2597–2609`. Loads from `Resources/Heroes/Props/Weapons/` (`:63`), primitive stand-in if absent (`:2668`). `knight_starter` is now a **Blink** native prefab (`:197`) — KayKit sword_A retired for the starter |
| **HeroBowAttachment** | `Assets/_Modules/Village/Hero/HeroBowAttachment.cs:15–24` | **LIVE** | Attaches the committed KayKit `bow_withString` (as `Heroes/Props/Bow.prefab`) to the Ranger's LeftHand bone; procedural bow fallback |
| **HeroPreviewViewer** | `Hero/HeroPreviewViewer.cs:16,238` | LIVE | Character-screen preview reuses the same KayKit weapon/shield meshes |
| **GearVisualApplier** | `Hero/GearVisualApplier.cs:15,123` | LIVE | Routes bow classes through HeroBowAttachment; normalizes KayKit props to ~0.92 m held |
| **EnemyFactory** | `Enemies/EnemyFactory.cs:345–427` | LIVE | Maps `hollow-*` enemy ids → `Skeleton_*` models loaded from `Resources/Enemies/` (committed AccuRig re-rigs wearing the KayKit skeleton texture, NOT the gitignored pack) |
| **EnemyAnimatorFactory** | `Enemies/EnemyAnimatorFactory.cs:8–61` | LIVE | Routes rigs to the KayKit-clip controllers: `HumanoidEnemy` (Rig_Medium Generic), `LargeEnemy` (Rig_Large Generic); AccuRig/Mixamo families go to their own Humanoid controllers instead |
| **Enemy / WaveManager / WaveData** | `Enemies/Enemy.cs:315,2547`; `Waves/WaveManager.cs:1817`; `Waves/WaveData.cs:83` | LIVE | Animator lives on the KayKit-style mesh child; capsule stand-in when no model |
| **AtbCombatantSwapper** | `BattleATB/AtbCombatantSwapper.cs:14,327–334` | LIVE | ATB battles reuse the shared KayKit enemy controller for staged combatants |
| **WallSegment / WallLayout** | `Walls/WallSegment.cs:12`; `Walls/WallLayout.cs:15–283` | LIVE | Village walls are KayKit hex `wall_straight`/`wall_corner` modules; hex pitch ~1.732 u drives layout math |
| **BuildingCatalog** | `Buildings/BuildingCatalog.cs:84` | LIVE | Building defs carry a KayKit Medieval Hexagon mesh name (e.g. `tower_A`) as `visualPrefab` |
| **KayKitAnimProof** | `DevTools/KayKitAnimProof.cs:39` + `DevPanelController.cs:831` | Dev-only | Spawns the KayKit Adventurers Knight beside the Tripo hero for side-by-side animation-quality proof (editor-only — pack is gitignored) |
| **Pets** | `Pets/Pet.cs:173`, `PetDeployer.cs:33,506`, `PetClipPlayer.cs:7` | LIVE | Starter pets use the Rig_Medium `Pet.controller`; ice-wolf (Tripo/CC5) explicitly does NOT |
| **DungeonHero / DungeonController** | `Dungeons/DungeonHero.cs:15,95`; `DungeonController.cs:38` | Partially | Dungeon hero collides with KayKit wall meshes; dungeon model pack "not yet wired" per `:38` |
| **NPCs** | `NPCs/AmbientNPC.cs:4,213`, `VillageNpcInjector.cs:5` | Fallback-live | Ambient villagers designed around KayKit civilian models; primitives stand in when absent |
| **ChallengeOutpostVictoryController / CavePortalRepointInjector** | `World/Camps/ChallengeOutpostVictoryController.cs:41`; `World/CavePortalRepointInjector.cs:45` | **LIVE (owner directive 2026-07-10)** | The KayKitChallengeOutpost scene is the walk-up outpost target; cave portals repointed to it |
| **Material fixers** | `Core/EnvironmentTreeMaterialFixer.cs:6,220,273`; `Core/TreeOfLifeMaterialFixer.cs:319` | LIVE | Runtime repair of KayKit hex decoration trees (`trees_A_large` etc.) whose external-material remap is absent on fresh clones |
| **FeatureFlags** | `Core/FeatureFlags.cs:342` | LIVE | Flag gating the KayKit-skinned dungeon portals |
| **HelpMenu** | `HUD/HelpMenu.cs:192` | LIVE | Credits line: "Models: KayKit + Tripo" |

### 2.3 The committed-copy pattern (build safety)

Because the pack is gitignored, every runtime-needed KayKit asset is **copied into committed
Resources folders** and loaded from there:

- `Assets/Resources/Heroes/Props/` — `Bow.fbx` + `Bow.prefab` + `ranger_texture.png`
- `Assets/Resources/Heroes/Props/Weapons/` — axe_A, bow_A/B/C, dagger_A, hammer_A, shield_A,
  staff_A/B/C/D, sword_D/F/G, wand_A (all from Fantasy Weapons Bits) + Blink `sword_A.prefab`
- `Assets/Resources/Enemies/` — the AnimatorSetup-built controllers (`HumanoidEnemy`,
  `LargeEnemy`, `Boss`, `Dragon`, orc/skeleton Humanoid controllers) + the AccuRig skeleton
  family FBXs + `skeleton_texture_A.png`/`_URP.mat` (texture originally from Skeletons 1.1)

Rule of thumb for future work: **never `Resources.Load` from `Assets/Models/KayKit/...`** —
copy the specific mesh/texture into a committed Resources folder (or Addressables group),
then load the copy. `PeopleCharacterImporter.cs` / `BowPropBuilder.cs` are the precedents.

### 2.4 Live in-game vs unused (summary)

**Live in shipping builds:** Fantasy Weapons Bits meshes (hero gear visuals), Adventurers bow
(Ranger), Character Animations clip library (baked into all enemy/pet controllers), Medieval
Hexagon walls/gates/buildings/watchtowers/decoration-trees (Village + city manifest), Dungeon
Remastered glTF set (KayKitChallengeOutpost — the current walk-up outpost — and the dungeon
chain scenes), skeleton texture. **Dev-only:** Adventurers Knight (anim proof).
**Fully unused (17 of 21 packs):** Furniture, RPG Tools, Resource (partly), Halloween, Holiday,
Restaurant, Board Game, Block, Platformer, Prototype, City Builder, Space Base, Forest Nature
(partly used for dressing), both Mystery Monthly series (33 rigged characters, zero consumers
except the OrcRaider clip import at `ActionClipImporter.cs:234`).

---

## 3. Intended usage

Only one pack ReadMe survived on disk (`Board Game Bits/Assets/ReadMe.txt`), so intended
usage below is sourced from Kay's official pages (§4 citations) plus pack structure:

- **Folder convention:** every pack ships `fbx/` (raw) and `fbx(unity)/` (Unity-tuned import
  settings). **Always take `fbx(unity)/`** — established project canon since the 2026-05-19
  catalog. `SOURCE/` folders hold .blend-adjacent working textures (we own SOURCE tier for
  several packs).
- **One shared rig:** every KayKit humanoid — Adventurers, Skeletons, all Mystery Monthly
  characters, mannequins — is skinned to ONE skeleton, `Rig_Medium`; bulky bodies
  (Barbarian_Large, Skeleton Golem, Frost Golem, Black Knight, Clanker) use `Rig_Large`.
  One clip library therefore drives the entire cast with zero retargeting.
- **Kay's official Unity path:** import as **Humanoid**, or "Copy avatar from Existing"
  pointing at the shared `Rig_Medium_Avatar` from the free "KayKit Free Sample Pack (for
  Unity)" on the Asset Store. Per-engine implementation guides (Unity/Godot/Unreal) ship
  inside the Character Animations download.
- **OUR path differs (deliberately):** `AssetImportPostprocessor` imports KayKit rigs as
  **Generic** and plays KayKit's native clips directly (exact playback, no muscle-space
  remap). Interaction with the Humanoid/AccuRig pipeline: KayKit Generic rigs canNOT receive
  our Mixamo/AccuRig Humanoid clips and vice versa — that is why `EnemyAnimatorFactory`
  routes AccuRig families (skeleton warband, orcs, brutes) to Humanoid controllers and KayKit
  bodies to the Generic `HumanoidEnemy`/`LargeEnemy` controllers. If a KayKit character ever
  needs our Humanoid mocap (e.g. the KnightMocap combat set), re-import that one FBX as
  Humanoid — the rig is Humanoid-compatible per Kay — but expect proportion quirks and keep
  it off the shared Generic controllers.
- **Modularity / bits system:** "Bits" packs are kit-bash prop libraries sharing the palette-
  atlas texture scheme; weapons/accessories are separate meshes intended to be parented to
  hand/attachment bones (exactly what `EquipmentController`/`HeroBowAttachment` do). The
  Knight has removable helmet parts; bows have string variants (and an animatable string
  blend shape per Kay's page); shields ship `_color` variants for team tinting.

---

## 4. Web research

All claims verified against official pages 2026-07-11 (research agent, cited per item).

**Licensing — the headline:** every itch.io KayKit pack is **CC0** — "free for personal and
commercial use, no attribution required." Paid tiers (EXTRA characters, SOURCE .blend files,
Mystery Monthly) are CC0 too; payment buys access, not a different license. The only stated
(non-binding) request: don't resell unmodified copies. Sources:
kaylousberg.itch.io/kaykit-adventurers, /kaykit-skeletons, /kaykit-animations,
/kaykit-series-6. **Shipping our commercial game on these assets is unrestricted; a credit
(already present in HelpMenu) is optional courtesy.** Caveat: the *Unity Asset Store* variants
("KayKit — Adventurers Character Pack (for Unity)", $11.99) are under the standard Asset
Store EULA, not CC0 — our copies are the itch.io CC0 ones.

**Tier structure:** character packs run FREE (base roster) / EXTRA (~$7.95+, extra characters
+ alt textures) / SOURCE (~$11.95+, .blend). Bits/environment packs are free or
pay-what-you-want. Our on-disk contents (Engineer/Druid/Barbarian_Large, alt textures,
SOURCE folders) show we own EXTRA/SOURCE tiers for the character packs.

**Official rosters (match our disk):** Adventurers free = Knight, Barbarian, Rogue (+hooded),
Mage, Ranger; EXTRA = Engineer (with turret), Druid, Barbarian_Large. Skeletons free =
Warrior, Rogue, Mage, Minion; EXTRA = Skeleton Golem + Necromancer.

**Character Animations:** v1.2 exists upstream (adds glTF format + 12 new clips over our
1.1's ~24); newer Mystery bundles cite ~130 animations per rig — the upstream library has
grown well past what we have. Free upgrade: re-download. Source: kaylousberg.itch.io/kaykit-animations.

**Mystery Monthly:** Kay's Patreon program (patreon.com/kaylousberg, from ~$3.50/mo) — one
mystery rigged character per month; each 12-month run is later sold as a numbered Series
($19.99). Six series exist. Series 4/5 content matches our disk exactly (§1.2).
Source: kaylousberg.com/patreon-characters.

**Packs we do NOT own:**
- **Mystery Monthly Series 6** ($19.99, completed ~July 2026): Lorekeeper, Orc Brute, Cleric,
  Monstrosity, Plant Warrior, Toy Soldier, 4-GTN ×2, Hoarder, Avian Swordsman, Marksman,
  Magical Girls, Farmers ×2. The **Orc Brute and Cleric** are directly on-theme for us.
  Source: kaylousberg.itch.io/kaykit-series-6.
- Mystery Series 1–3 (older; earliest use the legacy leg-less style).
- Legacy freebies: original Dungeon Pack, original Skeletons, original Character Animations,
  Spooktober Seasonal, Mini Game Variety, Medieval Builder.
- Bundles: Bits Bundle 1 & 2 ($19.95 each); "The Complete KayKit" ($150 — all current AND
  future packs). Source: kaylousberg.itch.io/kaykit-complete.
- Note: no pack named "Dungeon Elements" exists; the dungeon line is Legacy → Remastered
  (we own Remastered). No standalone Halloween *character* pack — Halloween content is props.

**Official docs/samples:** GitHub org github.com/KayKit-Game-Assets mirrors the free packs
(asset mirrors, not engine samples). The "KayKit Free Sample Pack (for Unity)" (Asset Store)
carries the shared `Rig_Medium_Avatar` and is the official Unity on-ramp. Support hub is the
KayKit Discord. Formats: FBX + glTF everywhere; SOURCE tier adds .blend.

---

## 5. Opportunities + gaps

### Opportunities (sitting on disk, zero license cost, zero consumers today)

1. **33 rigged Mystery Monthly characters** on the same Rig_Medium/Rig_Large skeleton our
   `HumanoidEnemy`/`LargeEnemy` controllers already drive. Instant-roster candidates:
   - Enemies: OrcRaider (orc theme fits the Tripo orc arc), Werewolf_Wolf/Man (night raids),
     Vampire + Witch (dungeon bosses), FrostGolem + BlackKnight + Clanker (Rig_Large heavies —
     `EnemyFactory.cs:350` already reserves "brute/tank → separate id when added"),
     Animatronic_Creepy / Monster (dungeon horrors), Ninja (skirmisher).
   - NPCs: Paladin (quest-giver knight), Helpers ×2 (village children/assistants), Hiker,
     Survivalist, Caveman, Protagonists ×2, Tiefling (per `AmbientNPC.cs` the villager
     system is already built to accept real models over its primitive fallbacks).
   - Pipeline is proven: drop FBX → postprocessor imports Generic → assign `HumanoidEnemy`
     controller → done. `KayKitAnimProof` is the harness to verify each one visually.
2. **Furniture Bits + RPG Tools Bits** for the MainCastle_Hall interior and the player-defined
   building interiors (WO-673 pivot) — beds, desks, lamps, anvils, lanterns, keys. Flagged
   "high value, untapped" since the 2026-05-19 catalog; still untouched.
3. **Dungeon Remastered full FBX set** for the chunk-composer north-star (WO-479 /
   `scene-chunk-dungeon-composer-northstar`): the builders currently use only the glTF
   live-set subset; the 283-unique-model warehouse (stairs, railings, doors, traps, torture
   props, crypts) is the natural chunk vocabulary.
4. **Resource Bits** for harvest/crafting drop visuals (gems, ore, gold piles, crates) — the
   crystal economy uses a slice; loot tables could be fully skinned from it.
5. **Halloween Bits** (gravestones, coffins, dead trees) for a graveyard region or the
   undead-raid dressing around Village2.
6. **Skeletons 1.1 weapons** (Skeleton_Scythe, Golem_Axe_Large, bone shields) as enemy-held
   props — `EquipmentController`'s attach path works for enemies too via OffsetForge grips.
7. **Adventurers alt textures** (4 palettes × 7 classes) — free visual variety for ATB enemy
   variants or NPC crowds without new meshes.
8. **Free upstream upgrades:** Character Animations 1.2 (+12 clips, glTF) and any pack
   re-downloads are $0. Series 6 ($19.99) is the only on-theme purchase gap.

### Gaps / incompatibilities

1. **Skeletons 1.1 character FBXs missing on disk** (`characters/fbx/` empty). Not currently
   breaking (live skeletons are the committed AccuRig family), but the pack can't serve new
   skeleton variants until re-downloaded.
2. **Gitignore boundary:** nothing under `Assets/Models/KayKit/` can ship. Every new use
   requires the committed-copy step (§2.3). Budget it into any WO that adopts new KayKit art.
3. **Generic-vs-Humanoid split:** KayKit Generic rigs can't consume our KnightMocap/Mixamo
   Humanoid clips without a per-FBX Humanoid re-import; keep KayKit characters on the KayKit
   clip library (which is the point of the shared rig).
4. **Art-direction mismatch risk:** hero canon is the Tripo Knight (combat-pivot north star);
   KayKit characters read chunkier/toy-like. Fine for enemies/NPCs/props; do not swap the
   hero without an owner decision (`KayKitAnimProof` exists precisely to compare).
5. **Off-theme packs:** City Builder, Space Base, Restaurant, Platformer, Block, Board Game,
   Holiday, Prototype have no place in the fantasy game (Prototype Bits is fine for greyboxing;
   Board Game Bits could serve a tavern mini-game someday). They cost only disk space —
   purge-candidates at polish end.
6. **Duplicate Adventurers folder** at `Assets\Models\KayKit Adventurers 2.0\` — redundant
   older copy; purge candidate.
7. **Stale doc pointers:** `KAYKIT_NOTES.md` says the live-set folders were all empty
   (dungeon/medieval/weapons are now populated); `WaveData.cs:83` points at the empty
   `enemies/` folder; `HeroBowAttachment.cs` header's "No such asset is committed" comment
   near `_resourcesBowPath` contradicts the committed `Bow.prefab` that exists.

---

## 6. Executive summary

KayKit is the project's biggest single art dependency after Tripo: 21 packs, roughly 970 MB
and 4,300 unique low-poly models, all by one author (Kay Lousberg) and all licensed CC0 —
we can ship them commercially with no attribution and no fees. The packs live in a gitignored
folder on this machine only, so everything the game actually loads at runtime is a copied,
committed duplicate under Assets/Resources; that copy step is the one rule every future
KayKit adoption must follow.

Today the game uses four things from KayKit. First, the Fantasy Weapons Bits meshes are the
visual gear system: every staff, sword, bow, wand, hammer and shield the hero equips is a
KayKit mesh mapped by id in EquipmentController, with the Ranger's bow handled by its own
attachment component. Second, the Character Animations clip library is the animation backbone
for all non-mocap characters: an editor script bakes shared animator controllers from it, and
those controllers drive enemies, bosses, and pets. Third, the Medieval Hexagon pack supplies
the village's walls, gates, watchtowers and several buildings. Fourth, the Dungeon Remastered
set dresses the KayKitChallengeOutpost scene — as of the 2026-07-10 owner directive, the
game's live walk-up outpost — plus the dungeon chain scenes.

The biggest untapped value is characters. We own 33 fully rigged Mystery Monthly characters
(orc raider, werewolf, vampire, witch, frost golem, black knight, paladin, villagers and
more) plus the eight Adventurers, and every one of them is skinned to the same shared
skeleton our existing animator controllers already drive — meaning new enemies and village
NPCs are a drop-in exercise, not an art project. The furniture, tools, and resource prop
packs are similarly ready-made for castle interiors, the player-building pivot, and loot
visuals. The notable defects found: the Skeletons pack's character models are missing from
this checkout (harmless today — the live skeletons are a separate committed family), a few
stale comments and doc pointers, and one redundant duplicate folder. The only purchase worth
considering is Mystery Series 6 (about twenty dollars) for its orc brute and cleric; the
animation library also has a free upstream upgrade with roughly a hundred more clips.

**Bottom line:** legally frictionless, technically already integrated at the pipeline level,
and sitting on a large unused roster that matches exactly what the game keeps needing —
more enemy variety and real villager NPCs.
