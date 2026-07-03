# Master Catalog — resources-art

Scope: `Assets/Resources` (non-data: prefabs, `.controller` animators, FBX, icons)
plus `Assets/Art`, and the gitignored model packs (`Assets/Models/*` KayKit,
`Assets/polyperfect`). Verified by reading the actual files, prefab YAML, the
`.asset` ScriptableObjects, and every `Resources.Load*` call site in code.

`Resources.Load(path)` takes a path **relative to any `Resources/` folder, no
extension**. The catalog below pairs each load-path-family to its backing assets
and flags the ones with **no backing asset on disk** (silent-null at runtime).

---

## 1. Resources.Load path map (code → asset)

Path families resolved from grepping every `Resources.Load*` literal/interpolation.
"Backed" = matching asset(s) exist under an `Assets/Resources/` folder.

| Load path (arg) | Type | Caller(s) | Backed? |
|---|---|---|---|
| `Heroes/<slug>` | GameObject | `AtbCombatantSwapper`, `HeroAnimatorFactory` | YES — `Resources/Heroes/{Cleric,Knight,Mage,Ranger}.fbx` |
| `Enemies/<slug>` | GameObject | `AtbCombatantSwapper`, `EnemyOutpostBuilder` | YES — many FBX under `Resources/Enemies` |
| `Enemies/<controller>` | RuntimeAnimatorController | `AtbCombatantSwapper`, `EnemyAnimatorFactory` | YES — `Boss/Dragon/HumanoidEnemy/LargeEnemy/OrcWarband.controller` |
| `Enemies/Boss_Dragon` | `DragonBoss` (prefab) | `WaveManager` | YES — `Resources/Enemies/Boss_Dragon.prefab` |
| `Pets/<species>` | GameObject | `PetDeployer` | **NO** — `Resources/Pets` is EMPTY (0 files) |
| `Pets/<species>` / `Pets/Pet` | RuntimeAnimatorController | `PetDeployer` | **NO** — no controllers; falls to procedural |
| `Pets/<species>` (clips) | AnimationClip[] | `PetDeployer` | **NO** |
| `Cosmetics/Pets/<equipped>` | GameObject | `PetDeployer` | **NO** — `Resources/Cosmetics/Pets` EMPTY |
| `Cosmetics/Previews/<id>` | Texture2D | `CosmeticShopPanel` | **NO** — no `Cosmetics/Previews` folder |
| `PetPortraits/<id>` | Sprite / Texture2D | `PetDeployer`, `PetSelectController` | PARTIAL — 3 PNGs (`pet-aether-sprite`, `pet-flame-pup`, `pet-ice-wolf`) |
| `HudIcons/<key>` | Sprite | `VillageHudController`, `BattleHudUgui` | YES — large `Resources/HudIcons` set |
| `HudIcons/<sheet>` (LoadAll) | Sprite[] | `VillageHudController` | YES (sheet sub-sprites) |
| `RpgUi/<role>` (LoadAll) | Sprite[] | `RpgUiCatalog` | YES — `Resources/RpgUi/{badge,bars,button,icons,panel,potion}` |
| `ProjectileIcons/<sheet>` (LoadAll) | Sprite[] | `ProjectileArtCatalog` | YES — 2 sheets |
| `Structures/Portal` | GameObject | `DungeonEntranceBootstrap` | YES — `Resources/Structures/Portal.fbx` |
| `Towers/DevTower` | `TowerData` | `BuildMenu`, `TowerLoopDevHarness` | YES — `Resources/Towers/DevTower.asset` |
| `Dungeons` (LoadAll) | `DungeonDef` | `DungeonWorldPortalSpawner`, `DungeonEntranceBootstrap` | YES — `FolksGranary`, `HealersCottage` |
| `Title/Title_L` / `Title/Title_H` | Texture2D | `TitleController` | YES — `Resources/Title/` |
| `HeroPortraits/<slug>` | Sprite/Texture2D | `TitleController`, `HeroSelectController`, `PortraitCache` | **NO Resources/HeroPortraits folder** — see FLAGS |
| `heart-wing` | Texture2D | `HeroSelectController`, `TitleController` | **NO** — no `heart-wing` at Resources root |
| `Intro/intro-<id>` | Texture2D | `StoryIntroController` | **NO Resources/Intro folder** |
| `UI/panel_bg`, `UI/menu_bg` | Texture2D | `ElarionUi` | **NO Resources/UI folder** (lazy, single-try guarded) |
| `Sfx/<id>` | AudioClip | `GameSfx`, `EnemyCombatAudio`, `AudioService`, `IntroCommandBridge` | PARTIAL — only `Sfx/LookoutHorn.wav` ships; ALL others fall back to procedural `Generate*()` |
| `Audio/GameAudioMixer` | AudioMixer | `AudioBootstrap`, `BattleMusicManager` | (Audio folder — out of art scope; mixer expected) |
| `DeNelleAudioService` | GameObject | `AudioBootstrap` | prefab path (audio scope) |
| `Dialogue/DialogueSystem` | GameObject | `CompanionMeetingTrigger`, `IntroSequencePlayer` | YES — `Resources/Dialogue/DialogueSystem.prefab` |
| `Data/Canonical/*` | TextAsset | `CanonicalJson` (all catalogs) | DATA scope (separate catalog) |
| `DevPanelSettings`, `JupiterSwapPanel` | PanelSettings/VisualTreeAsset | DevBootstrap, JupiterSwapBootstrap | UI scope |

---

## 2. Resources/ — art folders (inventory)

### Heroes/ — 4 playable hero/companion bodies (Tripo-rigged FBX)
- FBX + matching `.controller`: `Cleric`, `Knight`, `Mage`, `Ranger`. Slug names
  map to roster (Cleric=Elara/Healer, Knight=Grom, Mage=Thrain/Wizard, Ranger=Sylas).
- Each FBX has a `.fbm/` (embedded materials), a `_tex`/`Textures/` sidecar, and a
  `.fbx.tripo-extracted` marker (written by `TripoAssetPostprocessor`; "delete to
  force re-extract"). `Cleric_tex/` exists; Knight/Mage/Ranger share `Textures/`.
- `Heroes/Materials/` — per-hero `_basecolor` mats + Tripo `tripo_mat_*_Pbr_Diffuse`
  + CC skin/tongue mats (`Std_Skin_Head_Pbr`, `Std_Tongue_Pbr`, `Motion_Dummy_Female`).
- `Heroes/Props/` — `Bow.fbx` + `Bow.prefab` + `Bow.mat` (ranger weapon), `ranger_texture.png`.
- `Heroes/Props/Weapons/` — KayKit-style weapon FBX set: `axe_A`, `bow_A/B/C`,
  `dagger_A`, `hammer_A`, `shield_A`, `staff_A/B/C/D`, `sword_A/D/F/G`, `wand_A` (+`Materials/`).

### Enemies/ — creature FBX + shared animator controllers
- FBX: `Demon`, `Dragon`, `Necromancer`, `OgreMage`, `Troll`,
  `Orc_Berserker`, `Orc_Necromancer`, `Orc_Shaman`,
  `Skeleton_Golem`, `Skeleton_Mage`, `Skeleton_Minion`, `Skeleton_Rogue`, `Skeleton_Warrior`.
- Controllers (5): `Boss`, `Dragon`, `HumanoidEnemy`, `LargeEnemy`, `OrcWarband`.
  Rig→controller routing lives in `EnemyAnimatorFactory.RigFor()/Controller()`
  (Skeleton_Golem→LargeEnemy, Necromancer→Boss, Dragon→Dragon, Orc_*→OrcWarband, default→HumanoidEnemy).
- `Boss_Dragon.prefab` — apex boss prefab, loaded as `DragonBoss` by `WaveManager`.
- `EnemyVfxSet_Default.asset` — `DeNelle.Village.EnemyTypeVfxSet` SO; **all arrays EMPTY**
  (hit/death/attack VFX+SFX unassigned, telegraph 0.5s). Live wiring point, currently bare.
- `Materials/` (Dragon bump/normal, Glow, skeleton), `skeleton_texture_A.png` + `_URP.mat`.

### Structures/ — buildable/decor prefabs (KayKit medieval + Tripo)
- KayKit-derived prefabs: `Altar`, `Anvil`, `Ballista`, `Catapult`,
  `Gate_Medieval_Medium`, `House_Medieval_{Small,Medium,Large}`, `Marketplace_Stand_Simple`,
  `Pillar_Ionic`, `Stables_Medieval`, `Torche_Wall`, `Tower_Castle_Round`,
  `Tower_Medieval_Big`, `Tower_Medieval_Wood`, `Wall_Medieval_{Stone,Wood}`,
  `Watermill_Medieval`, `Well`, `Windmill_Medieval`.
- Tripo FBX: `Portal.fbx` (+`.fbm`, tripo-extracted), `PetHouse.fbx`, `tree_of_life.fbx`.
- Loose Tripo textures at folder root: `PetHome_basecolor.JPEG`,
  `TreeofLife_basecolor/normal/roughness.JPEG`. `Materials/`, `Textures/` subfolders.

### NPCs/ — 4 NPC prefabs
- `NPC_Blacksmith`, `NPC_Merchant`, `NPC_Peasant_Mevina`, `NPC_Peasant_Tob`.
  (Backed by `Models/People` optimized pack — see gitignore note §6.)

### Towers/ — TowerData ScriptableObjects (`DeNelle.Core.Data.TowerData`)
- `ArcherTower` (cost 150, targets 2, upgrade tiers w/ range/damage + visualPrefab GUID),
  `DevTower`, `FrostTower`, `MageTower`. Loaded by `BuildMenu`/dev harness via `Towers/<name>`.

### Dungeons/ — DungeonDef SO (`guid 3b08727…`)
- `FolksGranary`, `HealersCottage` (DungeonId, NameKey i18n, DisplayName, SceneName
  e.g. `Dungeon_HealersCottage`, Banner, AccentColor). `Resources.LoadAll<DungeonDef>("Dungeons")`.

### HudIcons/ — town + combat HUD icon set
- Root PNG/JPG: `Elarion`, `hud_build/compass/crystal/food/heart/intel/invasion_handle/
  invasion_medal/invasion_medallion/inventory/iron/music/quest/settings/strip_bar/talk/
  wave_clock/wave_plate/wood`, `player_frame_bg`, `player_hp_fill`, `player_mp_fill`,
  `population`, plus stray `a pic.png`.
- Per-class ability-icon subfolders (loaded `HudIcons/<Class>/<key>` via BattleHudUgui):
  - `Healer/`: healer, Healer_Heal, Healer_Group_Heal, Healer_Holy, Healer_Smite
  - `Knight/`: knight, Knight_Charge, Knight_Cleave, knight_parry, knight_thrust
  - `Ranger/`: ranger, Ranger_Barrage, Ranger_Poison_Arrow, Ranger_Ranged_Attack, ranger_rapid_fire
  - `Wizard/`: wiard(sic), Wizard_Fireball, Wizard_Lightining(sic), Wizard_Meteor, Wizard_Plasma

### RpgUi/ — code-built-UI sprite atlas, loaded per role via `RpgUiCatalog`
- `badge/badge_level`; `bars/bar_{fill,frame}_{blue,green,red}`; `button/button_gold`;
  `icons/icon_{combat,compass,heart,inventory,quest,settings,shield,sword,talk,tree}`;
  `panel/panel_{bar,inventory,quest,tab}`; `potion/potion_{fire,health,mana}`.

### ItemIcons/ — 8 raw item icons (cryptic source names): `0D5St, CtQcX, Ud37F, VxBVb, WRdWM, bRUz5, inEJH, jdRCa` (.jpg).

### ProjectileIcons/ — 2 sheets: `projectiles_arrows_magic.jpg`, `projectiles_spell_vfx_lifecycle.jpg` (loaded by `ProjectileArtCatalog`, `Sheets[]`).

### Portraits/ — building portraits (jpg): `arcane-tower, armorer, farm, forge, lumbermill, market, pet-house`.

### PetPortraits/ — 3 PNGs: `pet-aether-sprite, pet-flame-pup, pet-ice-wolf`.

### VFX/Projectiles/ — 9 spell VFX prefabs (loaded by `ProjectileVFXCatalog`)
- `Projectile_{Arcane,Fire,Ice,Storm}`, `Explosion_{Arcane,Fire,Ice,Storm}`, `Flash_generic`.

### Cosmetics/Pets/ — **EMPTY** (referenced by PetDeployer skin swap; nothing to load).

### Dialogue/DialogueSystem.prefab — Yarn dialogue runner prefab (Options, Action Button, Heart, line view); spawned by `CompanionMeetingTrigger`/`IntroSequencePlayer`.

### PatriciaLight/ — REMOVED module remnant (per PIPELINE_STATE)
- `tower2.fbx` + `tower2/` Tripo textures (`medievaltower3dmodel_basecolor/normal/roughness.JPEG`).
  Only this kept after Defend-the-Tower removal; no live loader found.

### Misc Resources art roots
- `Title/Title_{H,L}.jpg` (title bg). `Textures/{CastleArch,Cathedral,Knight,Ranger}`.
- `Materials/RoundedChatBubble.mat`. `Audio/{Music/GameOver.mp3, bellssteel-panic.mp3}`,
  `Sfx/LookoutHorn.wav` (only shipped SFX file). `Pets/` empty.

---

## 3. Assets/Art — source art (mostly NOT under Resources/, not runtime-loadable)

These are authoring sources / FBX imports; runtime copies live in `Resources/` where needed.

- `Art/Crystals/Crystals.fbx` (+`crystals/` textures).
- `Art/Enemies/ATB/` — ATB roster reference sheets (jpg): `goblin_family`, `orc_mixed_family`, `orc_warband`, `troll_family`.
- `Art/Hero Select/HeroSelect.jpg`.
- `Art/Heroes/ATB/` — animation-state ref sheets: `elara_healer_states`, `grom_knight_states`, `thrain_wizard_states`, `roster_animation_states_gray`.
- `Art/Marketplace/marketplace.fbx` (+`marketplace/` PBR textures).
- `Art/Title/Title_{H,L}.jpg` (source dupes of Resources/Title).
- `Art/Towers/` — `BlastTower.fbx` (+BlastTower/ textures), `VikingWatchTower/Tower.fbx`.
- `Art/Tree_Of_Life.fbx` (+ `Tree_Of_Life/` textures).
- `Art/TripoStructures/` — Tripo building FBX: `BuildTower`, `Farm`, `Forge`, `LumberMill`,
  `PetHome` (each w/ `.fbm` + `.fbx.tripo-extracted`); `Materials/` (per-part
  `*_tripo_part_N_basecolor.mat` — many PetHome parts), `Textures/`.
- `Art/UI/HudIcons/hud_widgets_sheet.jpg`; `Art/UI/ItemIcons/` (source jpgs + `ConsumablesCrafting/`).
- `Art/VFX/Projectiles/` — `projectiles_arrows_magic.jpg`, `projectiles_spell_vfx_lifecycle.jpg` (source of ProjectileIcons).

---

## 4. Art-consumer code (factories / catalogs) — public API

| Class | File / asmdef | Responsibility | Key public |
|---|---|---|---|
| `VisualFactory` | `_Modules/Village/VisualFactory.cs` · DeNelle.Village | Runtime skinner: Resources.Load model → instantiate under host → fit/seat/strip/URP-fix. Visual-only. | `Skin(host, resourcesPath, SkinOptions)`, `Skin(host, prefab, opts)`; `SkinOptions.Enemy/Structure/Prop`. SkinOptions.LocalRotation applied BEFORE fit/seat (DEF-232 off-pivot fix). |
| `EnemyAnimatorFactory` | `_Modules/Village/Enemies/EnemyAnimatorFactory.cs` · DeNelle.Village | Maps enemy model name → rig → shared controller, loads `Enemies/<controller>`. | `RigFor(modelName)→EnemyRig`; loads controller at runtime. |
| `RpgUiCatalog` | `_Modules/Core/UI/RpgUiCatalog.cs` · DeNelle.Core | Lazy `Resources.LoadAll<Sprite>("RpgUi/<role>")` cache for code-built UI. | `Get(role)`, `Get(role,spriteName)`, `TryGet(...)`, `ClearCache()`. |
| `ProjectileArtCatalog` | `_Modules/Village/Buildings/ProjectileArtCatalog.cs` · DeNelle.Village | Element→projectile/impact icon from 2 sheets. | `ForElement`, `ImpactForElement`, `ForArrow`, `ForSpellOrb`, `ForTowerName`, `ElementForTowerName`. |
| `ProjectileVFXCatalog` | `_Modules/Village/Buildings/ProjectileVFXCatalog.cs` · DeNelle.Village | Loads `VFX/Projectiles/*` prefabs. | (path-keyed loader). |
| `PortraitCache` | `_Modules/DialogueUI/PortraitCache.cs` · DialogueUI | Wraps Texture2D portraits (imported as Textures, not Sprites) into cached runtime Sprites; misses cached too. | static Get(path). |
| `PetDeployer` | `_Modules/Pets/PetDeployer.cs` · DeNelle.Pets | Loads pet body/controller/clips/cosmetic/portrait from `Pets/`,`Cosmetics/Pets/`,`PetPortraits/`. | (all targets currently UNBACKED — falls to procedural). |
| `VillageHudController` | `_Modules/HUD/VillageHudController.cs` · DeNelle.HUD | Town HUD; custom `HudIcons/<name>` sprite wins over sheet sub-sprite. | passive display. |
| `BattleHudUgui` | `_Modules/BattleATB/BattleHudUgui.cs` · DeNelle.BattleATB | ATB combat HUD ability icons via `HudIcons/<Class>/<key>`. | — |
| `GameSfx` | `_Modules/Village/Audio/GameSfx.cs` · DeNelle.Village | Loads `Sfx/<id>` else procedurally generates the clip (`?? Generate*()`). | per-sfx static accessors. |

---

## 5. FLAGS

### Stale-comment-vs-code / contradictory
- **`PortraitCache` / HeroPortraits**: comment says portraits "live at
  `Resources/HeroPortraits/<Name>`" and code (`TitleController`, `HeroSelectController`,
  `PortraitCache`) loads `HeroPortraits/<slug>` — but **there is NO `Resources/HeroPortraits`
  folder on disk.** Every hero-portrait load returns null (cached miss). Class is wired
  but the backing art folder is absent. (Editor `HeroPortraitRenderer.cs` is the intended
  generator — portraits were apparently never rendered/committed.)
- **`EnemyVfxSet_Default.asset`**: a real, loadable SO but **every array is empty**
  (hit/death/attack VFX+SFX). Comment-free, but "Default VFX set" implies content; it is a
  bare placeholder — enemies get no per-type VFX/SFX from it.

### Unbacked load paths (silent null at runtime — caller falls back)
- `Pets/*` and `Cosmetics/Pets/*` — `Resources/Pets` and `Resources/Cosmetics/Pets` are
  **EMPTY**. `PetDeployer` body/controller/clip/cosmetic loads all miss → procedural pet path.
- `Cosmetics/Previews/<id>` — no `Cosmetics/Previews` folder (`CosmeticShopPanel` previews null).
- `Intro/intro-<id>` — no `Resources/Intro` folder (`StoryIntroController` beat images null).
- `heart-wing` — not at Resources root (Title/HeroSelect banner art missing).
- `UI/panel_bg`, `UI/menu_bg` — no `Resources/UI` folder (`ElarionUi` falls to solid fills; guarded single-try).
- `Sfx/*` (except `LookoutHorn`) — only one WAV ships; EnemyHit/EnemyDeath/TowerFire/etc.
  all rely on the `?? Generate*()` synthesised fallback. By design, but means the "real" SFX art is unshipped.

### Scene-gated / removed-module remnants
- `Resources/PatriciaLight/` (`tower2.fbx` + textures) — leftover from the **REMOVED**
  Defend-the-Tower module (PIPELINE_STATE §8). No live loader; dead art kept intentionally.
- `HudIcons/a pic.png` — stray/junk filename, not referenced.

### Naming bugs (typos in shipped icon keys — will mismatch a clean key)
- `HudIcons/Wizard/wiard.jpg` (should be "wizard"), `Wizard_Lightining.jpg` (should be "Lightning").
  Any code keying on canonical spelling won't find these.

### Tripo pipeline markers (informational, not bugs)
- `*.fbx.tripo-extracted` sidecars (Heroes, Structures, Art/TripoStructures) are
  `TripoAssetPostprocessor` state files ("Delete to force re-extract") — not loadable assets.
  Tripo FBX import as Phong → need `DeNelle.Core.TripoMaterialFixer` (see VisualFactory FixTripoMaterials).

---

## 6. Gitignored model packs (ABSENT on fresh clone)

Confirmed via `git check-ignore` and `.gitignore`:

- **`Assets/polyperfect/`** — fully gitignored (246MB Low Poly Ultimate Pack). Use
  `_M` tier prefabs only: `polyperfect/Low Poly Ultimate Pack/_M/Meshes_M/<Category>_M/`
  (Animals_M, Buildings_M, Fantasy_M, Farm_M, Food_M, Furniture_M, Landmarks_M, …).
  Re-import on fresh clone via `Defenders/Art/Fix Polyperfect URP Materials`. Catalog:
  `docs/polyperfect-asset-catalog.md`. Missing prefab → `Debug.LogWarning` not error.
- **`Assets/Models/*`** — gitignored (`/Assets/Models/*`) EXCEPT `Models/People/` which IS
  committed via LFS (optimized NPC pack, DEF-91), but its `Human/`, `Orc/`, `Troll/`,
  `textures/` subdirs are re-excluded by `.gitignore`. So on a fresh clone:
  - **Absent**: `Models/KayKit/*` (Adventurers 2.0, Dungeon Remastered, Skeletons, City
    Builder, Medieval Hexagon, Fantasy Weapons, RPG Tools, Resource Bits, etc.),
    `Models/KayKit Adventurers 2.0/`, `Models/CastleGate/`, `Models/Cathedral/`, `Models/Pet/`,
    and `Models/People/{Human,Orc,Troll,textures}/`.
  - **Present (LFS)**: `Models/People/{Blacksmith,Merchant,Peasant,Peasant Tob}/` — backs the 4 `Resources/NPCs/*.prefab`.
  - Note: the `Resources/Structures/*` and `Resources/Enemies/*` KayKit-derived **prefabs/FBX
    are committed** (they live under Resources), so the runtime town/enemy art survives a fresh
    clone even though the source `Models/KayKit` packs do not. Fresh-clone "black village" =
    missing `Models` packs (memory: fresh-clone-missing-Models).

---

Counts: ~55 distinct Resources.Load path families mapped; Resources art folders:
Heroes (4 heroes + 16 weapons + props), Enemies (13 FBX + 5 controllers + boss prefab +
VFX SO), Structures (20 prefabs + 3 Tripo FBX), NPCs (4), Towers (4 SO), Dungeons (2 SO),
HudIcons (~24 root + 4 class folders ×5), RpgUi (6 role folders, ~24 sprites), ItemIcons (8),
ProjectileIcons (2), Portraits (7), PetPortraits (3), VFX/Projectiles (9). Plus Art source
folders (~12) and 2 gitignored pack trees. ~12 art-consumer classes documented; 11 FLAGS.

> UPDATE 2026-07-03: GameSfx sword-clash = 4-take variant pool (Sfx/SwordClash..4); EnemyCombatAudio death = 2 takes (EnemyDeath2). Gated ff.combatfeel (default ON).
