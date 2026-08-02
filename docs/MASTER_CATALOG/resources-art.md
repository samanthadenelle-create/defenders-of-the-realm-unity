# Master Catalog — resources-art

**Verified 2026-08-02** from the actual tree (`Assets/Resources/**` on disk, `git ls-files`,
`.gitignore`, `.gitattributes`, consumer-code grep, git history) — NOT from comments.
Supersedes the 2026-06-12 body (+2026-07-03 note). Live anchor at write time:
`CANON_GROUND_TRUTH_2026-08-01.md`. `Resources.Load(path)` = path relative to any
`Resources/` folder, no extension; an FBX is loadable as a GameObject.

---

## 1. Folder inventory (33 dirs under `Assets/Resources/`, non-meta file counts)

| Folder | Files | What it is (consumer) |
|---|---|---|
| Arena | 28 | `ForestClearingArena.prefab` + rocks/trees FBX + `Backdrops/` jpgs — `BattleArena.cs:577,815-817` |
| Audio | 2 | `Music/GameOver.mp3`, `bellssteel-panic.mp3` |
| Bridges | 1 | `Bridge_Medieval_Stone.prefab` — `CastleMoatBuilder.cs:630` |
| Cosmetics | **0** | EMPTY — `PetDeployer` cosmetic loads still silent-null |
| Data | 77 | Canonical JSON — DATA catalog scope, not here |
| Dialogue | 0* | `DialogueSystem.prefab` folder (prefab present; count excludes .meta quirk) |
| Dungeons | 4 | `FolksGranary.asset` + `HealersCottage.asset` DungeonDefs, `enemy_outpost.fbx` (+tex) |
| Echoes | 6 | `Portraits/` — 6 echo portraits (EmberPhoenix, Frosthowl, StonewardenBear, StormcoilSerpent, VerdantStag, VoidwingRaven) — `EchoRosterCatalog.cs:200` |
| Enemies | 154 | AccuRig enemy bodies + controllers — §3 |
| Harvest | 8 | 4 Tripo FBX `wood/iron/food/crystals` (+tex) — `MineNodeVisual.cs:65-68`, `HarvestSite.cs:357-360` |
| Hedges | 1 | `Fence_Shrub.prefab` — `CastleMoatBuilder.cs:123` |
| Heroes | 104 | Hero bodies + props/weapons — §4 |
| HudIcons | 64 | Town/combat HUD icons; class dirs `Healer/Knight/Ranger/Wizard` + **new `BuildingUpgrades/`** |
| ItemIcons | 492 | Flat sprite-sheet set — `ItemIconCatalog.cs:9` (`LoadAll<Sprite>("ItemIcons/<sheet>")`, indexed by sprite name; grew from 8 cryptic jpgs) |
| Materials | 3 | incl. `RoundedChatBubble.mat` |
| NPCs | 37 | 4 NPC prefabs + **`KayKit/` 12-body stage** — §2 |
| OffsetForge | 1 | `offsets.json` hand-dialed placement offsets — `CastleMoatBuilder.cs:640` |
| PatriciaLight | 9 | REMOVED-module remnant (`tower2.fbx`+tex); no live loader — keep-dead |
| PetPortraits | 3 | `pet-aether-sprite/flame-pup/ice-wolf` PNGs |
| Pets | 25 | **NOW BACKED** (was empty): `aether-sprite/flame-pup/ice-wolf.fbx` (+fox tex/fbm, Materials) — `PetDeployer.cs:616` `Load<GameObject>("Pets/"+Species)` resolves the FBX; controllers/clips still miss → procedural fallback (`PetDeployer.cs:697-705,768`) |
| Portraits | 27 | Building portraits incl. per-tier tower portraits (`archer-tower-2/-3`, `ballista-2/-3`, `catapult-*`, `arcane-spire-*`), `brom.jpg`, `Sylas.png` |
| ProjectileIcons | 2 | 2 sheets — `ProjectileArtCatalog` |
| Raids | 1 | `Raids_banner.jpg` |
| RpgUi | 482 | Code-built-UI sprite library, **21 role dirs** (abilities, badge, bars, button, classslot, crown, currency, decoration, element, emblem, font, frame, hud, icons, panel, potion, prefabs, prefab_deps, silhouette, slot, spellicons) — `RpgUiCatalog`; fed by RpgUiImporter (gitignore line 317) |
| Sfx | 4 | `Heal.mp3`, `LookoutHorn.wav`, `Spell_Impact.mp3`, `Swords_Clash.mp3` — everything else still procedural `Generate*()` fallback in `GameSfx` |
| Structures | 128 | ⚠ **gitignored folder with a tracked exception set** — §5 |
| Talents | 68 | talent-tree icon PNGs: `knight/` `ranger/` `wizard/` `shared/` — `EchoService.cs:548` + skill-tree UI |
| Textures | 2 | misc |
| Title | 2 | `Title_H/Title_L.jpg` |
| Towers | 4 | `ArcherTower/DevTower/FrostTower/MageTower.asset` TowerData SOs |
| VFX | 15 | **`HovlVfxCatalog.asset`** + `VFXCatalog.asset` + 13 `Projectiles/*` prefabs — §6 |
| VfxParade | 1 | `VfxParadeManifest.asset` — AdminOverlay "VFX Parade" (`AdminOverlay.cs:544`; bake via VfxParadeManifestBuilder.Build) |
| Walls | 15 | `wood/iron/steel_wall.fbx` + PBR tex — `WallTierData.cs:78-85` (steel = "PENDING owner art") |

**ABSENT (still):** `Resources/HeroPortraits/` — `TitleController`/`HeroSelectController`/
`PortraitCache` load `HeroPortraits/<slug>` and get null (carried landmine, anchor 08-01 §4).
Also still unbacked: `Cosmetics/*`, `Intro/*`, `UI/panel_bg|menu_bg`, `heart-wing`.

---

## 2. NPCs — 4 prefabs + the KayKit 12-body stage (WO-818, shipped 2026-08-01)

- Prefabs: `NPC_Blacksmith`, `NPC_Merchant`, `NPC_Peasant_Mevina`, `NPC_Peasant_Tob`
  (backed by the LFS-tracked `Assets/Models/People/` pack). `TorchWardenDress.cs:53-54` loads
  Mevina/Tob as dungeon torch-warden bodies.
- **`NPCs/KayKit/` — 12 tracked Humanoid bodies** (commit `e8bd17b0`, `KAYKIT_STAGE_OK 12/12`):
  Barbarian, BlackKnight, Cleric, Druid, Engineer, Farmer_A, Farmer_B, Hoarder, Mage,
  Paladin_with_Helmet, Ranger, Tiefling — each FBX + `<name>_texture.png` + `Materials/*.mat`,
  plus **`KayKitNpcIdle.controller`**.
- Resolver: **`KayKitNpcBody`** (`Assets/_Modules/Village/NPCs/KayKitNpcBody.cs`) — the ONE
  chain: `NPCs/KayKit/<npcModel>` first → People-pack chain → capsule; one `FlowTrace.Warn` on an
  authored-but-broken slug, never a blank NPC. Driven by `repo.npcModel` on exactly 12 rows of
  `structures-catalog.json` v6 (`RepoProps.cs:138`); consumed by `BarracksNpcInjector` +
  `CastleVendorNpcInjector`. Body swap = one-word owner JSON retag (creative pick = OWNER-ONLY).
- Oracle: **`CheckNpcModels`** in `DataRegression` (`NPC_MODELS_OK` — parity + slug-file existence
  + the exactly-12 pin; a 13th row must update the oracle in the same commit).
- Known gap: KayKit bodies stand **statically** (no AmbientNPC/Animator wiring on the FBX) —
  animated idles = follow-up WO (anchor 08-01 §2).

---

## 3. Enemies — AccuRig cast + licensed dragon (WO-760)

On-disk roster (root, non-meta): **14 body FBX** — `Demon`, `Necromancer`,
`Orc_{Berserker,Mage,Necromancer,Shaman,Tank,Warrior}`, `Skeleton_{Golem,Healer,Mage,Minion,
Rogue,Warrior}` (+ per-orc `.json`/`.mat`, `skeleton_texture_A.png` + `_URP.mat`).
**10 controllers**: `Boss`, `HumanoidEnemy`, `LargeEnemy`, `LargeHumanoid`, `OrcHumanoid`,
`OrcHumanoid_Mage`, `OrcHumanoid_Tank`, `OrcHumanoid_Warrior`, `OrcWarband`, `SkeletonHumanoid`.
Shared humanoid retarget path = `SkeletonHumanoid` / KayKit Rig_Medium (`EnemyAnimatorFactory`,
`docs/SME/KAYKIT_SME.md`, REQUIRED_PACKS §2). `EnemyVfxSet_Default.asset` = live wiring SO whose
ranged-cast fields are **HovlVfxCatalog string keys** (`EnemyTypeVfxSet.cs:80-139`).

**Boss_Dragon — LICENSED (WO-760, commit `08b912bf`, 2026-07-24):**
- `Boss_Dragon.prefab` rebuilt from the licensed **`Assets/Dragon/`** rig (WDallgraphics product
  71047, now git-TRACKED, with `Animations/dragon@*.FBX` set) via DragonAnimatorSetup +
  `SyndrathDragon.controller` (**force-tracked** inside otherwise-gitignored
  `Assets/Generated/Animators` — deliberate two-machine-drift guard).
- **CC-BY-NC removal ledger (git-rm'd in `08b912bf`):** old `Dragon.fbx` + 2 controllers +
  materials + orphan `Prefabs/Village/Generated/Boss_Dragon.prefab`; unlicensed "RedDragon 1.2"
  stray deleted; `EnemyFactory` dragon keys repointed `Dragon` → `Boss_Dragon` (old key retired).
  The 07-23 "RESOLVED" was comment-only; the 07-24 builder-run + git-rm actually cleared the
  commercial-ship blocker. Also gone vs the 06-12 catalog: `OgreMage.fbx`, `Troll.fbx`,
  `Dragon.controller` (the CC cluster).

---

## 4. Heroes — KnightV3 is THE body; Cleric/Mage/Ranger FBX are GONE

- On disk/tracked FBX at root: **`Knight.fbx`, `knightV2.fbx`, `KnightV3.fbx` only** (all LFS;
  `KnightV3.fbx` verified `filter: lfs`). `Cleric/Mage/Ranger` keep only their `.controller` +
  stale `.fbx.tripo-extracted` markers — **the 4-hero FBX set no longer exists**; REQUIRED_PACKS
  §3 ("Knight, KnightV3, Mage, Cleric tracked") is stale on this point.
- **`ff.knightv3` default ON** (`FeatureFlags.cs:475-484`): `HeroBodySwapper.cs:78-86,322-345`
  routes EVERY class to `BuildKnightV3Body` — KnightV3 is a CC/AccuRIG export, raw FBX bound to
  `Knight.controller` at runtime, own embedded `Material_Pbr` (extracted to `KnightV3.fbm/`).
  Fallback if missing: `KnightPackage.prefab` (Paladin package) / legacy Tripo Knight.
  `KnightMocap.controller` = the `ff.mocaploco` studio-mocap locomotion set (ON per anchor §3b).
  Also present: `SC_Archer.prefab`, `SC_Footman.prefab` (Supercyan troop bodies), `Emotes/` dir.
- `Props/`: `Bow.fbx/.prefab/.mat` + `ranger_texture`; `Props/Weapons/`: axe_A, bow_A/B/C,
  dagger_A, hammer_A, shield_A, staff_A–D, sword_D/F/G, wand_A **+ `_tripobak_sword_A.fbx` and a
  `sword_A.prefab`** (sword_A.fbx was renamed to the `_tripobak_` backup — prop-gap: code keying
  `Weapons/sword_A` FBX now hits the prefab/backup pair, not a root FBX). Attach-side rig fix
  lives in `EquipmentController.cs:435` (KnightV3 CC_Base RightHand axis correction).

---

## 5. Structures — the gitignore policy + two-machine drift (READ THIS BEFORE TRUSTING A CLONE)

**Policy:** `/Assets/Resources/Structures/` is **WHOLLY gitignored** (`.gitignore:121-122`,
"Owner Tripo art — large FBX kept local"). Everything inside travels by **LAN/zip copy** between
the owner's machines — EXCEPT a small force-tracked exception set.

- **Tracked (28 files; 4 FBX):** `ArcaneSpire_1/2/3.fbx` + `WizardTower_1.fbx` (+ their `.fbm`
  textures/albedos), `ArcaneSpire_Albedo.png`, `ArcaneTower_Albedo.jpg`,
  `Materials/Color_bcf8….mat`, `Textures/` (2), `TreeofLife_basecolor.JPEG`.
- **LAN-copy only (untracked, ~37 top-level items):** ALL **23 prefabs** (Altar, Anvil, Ballista,
  Catapult, Gate_Medieval_Medium, House_Medieval_S/M/L, Marketplace_Stand_Simple, Pillar_Ionic,
  Stables_Medieval, Torche_Wall, Tower_Castle_Round, Tower_Medieval_Big/Wood,
  **Tower_Tribal_Tier1/2/3**, Wall_Medieval_Stone/Wood, Watermill_Medieval, Well,
  Windmill_Medieval) + **14 FBX** (`arcane tower`, arena, armorer, barracks, farm, Forge,
  GenericContainer, jeweler, lumbermill, PetHouse2, Portal, store, **tree_of_life**, WoodBox)
  + their texture folders.
- **Live consumers of UNTRACKED assets** (a bare clone silently loses these visuals):
  `HubStructureVisualInjector.cs:74-84,137` (arcane tower/Forge/armorer/store/jeweler/lumbermill/
  farm/barracks/PetHouse2/arena swaps — incl. the DEF-arcane-white flat-path albedo fix note),
  `TreeOfLifeMaterialFixer.cs:59-73` (**`Structures/tree_of_life` FBX untracked while its
  basecolor JPEG IS tracked** — the centrepiece Tree of Life model is LAN-only),
  `CatalogBootstrap.cs:207` (`Structures/Tower_Medieval_Big`), `CraftingStationInjector.cs:63-66`,
  `JewelerStationInjector.cs:55-58`, `CatalogEntry.cs:44` texPath.
- **Loud-drift mitigations (why this doesn't rot silently):**
  1. `tools/art/REQUIRED_PACKS.md` — the human manifest of tracked-fallback vs zip-travel packs
     (authority: `docs/PAIN_POINTS_2026-07-26.md` §1.2 ruling "Tracked runtime + zip travel").
  2. `tools/art/verify-runtime-art.ps1` — fresh-clone gate: non-zero exit if a TRACKED fallback
     is missing; WARNs on absent gitignored packs (run before building).
  3. Builders degrade LOUD-but-soft: every miss = one `Debug.LogWarning` + tinted primitive,
     never an error (`EnemyStrongholdBuilder.cs:27-31` TGVRU rule; `RaidBaseGenerator.cs:469,500`).
  4. Precedent lessons encoded in-tree: the terrain-material RCA (§7) and the WO-760
     force-tracked `SyndrathDragon.controller` are the same drift class, solved the same way.

---

## 6. VFX — Hovl catalog + projectile prefabs

- **`VFX/HovlVfxCatalog.asset`** — THE key→prefab map for the (gitignored, 236MB) `Assets/Hovl
  Studio/` pack (`.gitignore:216-219`). Authored via `Defenders/VFX/Generate Hovl VFX Catalog`
  (`AbilityCatalog.cs:130`). Consumers resolve string keys through it: `DefenseTower.cs:809`
  (manual overlay wins; null key = PlayKey no-op), `Enemy.cs:1573` (one VFXManager pool),
  `GearCatalog.cs:54` (element→weapon VFX), `EnemyTypeVfxSet` ranged-cast keys, `HeroAbilities`.
  Owner tags keys; CLI maps verbatim (memory: no creative picks).
- `VFX/VFXCatalog.asset` — sibling catalog SO. `VFX/Projectiles/` — 13 prefabs:
  `Projectile_{Arcane,Fire,Fire_3,Ice,Storm}`, `Explosion_{Arcane,Fire,Ice,Storm}`,
  `Casting_Fire`, `Casting_Fire_2`, `Spell_Fire_6`, `Flash_generic` (`ProjectileVFXCatalog`).
- ⚠ **Unity Particle Pack has NO fallback**: 54 owner-tagged keys in
  `Assets/Editor/VfxManualPicks.json` point into gitignored `Assets/UnityTechnologies/ParticlePack/`
  (191MB); a machine without the pack silently loses those effects (REQUIRED_PACKS table row —
  its own flagged follow-up: promote used prefabs into tracked Resources).

---

## 7. Generated Terrain — TRACKED (the magenta-ground RCA)

`Assets/Generated/` is gitignored EXCEPT **`!Assets/Generated/Terrain/`** (`.gitignore:182-199`):
`ExteriorTerrainData.asset` (binary-forced), `ExteriorTerrainMaterial.mat`, 5 `.terrainlayer`
files (Dead/Grass/Mud/Snow/Stone), `AvalonDawnSkybox.mat` — all in `git ls-files`. RCA 2026-07-15:
the material only ever existed as a bake artifact on one machine → GUID dangled elsewhere →
magenta ground; the WHOLE bake folder is now tracked so scene-bound GUIDs always resolve.
(`*TerrainData.asset binary` in `.gitattributes` prevents the EOL-mangle corruption class.)

---

## 8. Gitignored pack ledger (ABSENT on fresh clone — zip/LAN travel)

From `.gitignore` (line cites) — cross-ref the full how-to-obtain table in
`tools/art/REQUIRED_PACKS.md`:
- `/Assets/Models/*` (106) EXCEPT `!/Assets/Models/People/` (107, LFS-tracked) — with People's
  `{Human,Orc,Troll,textures}/` re-excluded (304-311) and obj/ma/mtl stripped (109-114)
- `/Assets/Resources/Structures/` (121) + `/Assets/Art/TripoStructures/` (119) — §5
- `/Assets/polyperfect/` (128, 246MB; `_M` tier only; `docs/polyperfect-asset-catalog.md`)
- `/Assets/Quaternius/` (288)
- `/Assets/Hovl Studio/` (218, 236MB — only HovlVfxCatalog keys referenced)
- `Assets/UnityTechnologies/ParticlePack` (392-393, 191MB/886 files — NO fallback, §6)
- `/Assets/Lana Studio/Casual RPG VFX/Upgrade for URP/` (312) — the base Lana pack IS tracked
  (its 15 demo scenes appear in `git ls-files`)
- Supercyan pack (132-134 policy note; `SC_Archer/SC_Footman` prefabs in Resources/Heroes are the
  tracked fallback)

**LFS patterns** (`.gitattributes`): `*.mp3 *.wav *.fbx *.png *.jpg *.jpeg *.JPEG *.tga *.mp4
*.psd` → LFS; Unity text assets forced `text eol=lf` (`*.asset *.unity *.prefab *.meta *.mat
*.anim *.controller` …) with binary overrides for `*TerrainData.asset`, `NavMesh-*.asset`,
`LightingData.asset`, the chain/DungeonCompose scenes; `Assets/Firebase/**` +
`Assets/GoogleSignIn/**` native binaries → LFS (WO-769).

---

## 9. Risk ledger / FLAGS

- **FLAG-1 (two-machine drift, standing):** §5 — 23 prefabs + 14 FBX under Resources/Structures
  exist ONLY on machines that received the LAN copy; consumers degrade to warnings/primitives.
  Run `verify-runtime-art.ps1` on every fresh clone. Highest-value promote-to-tracked candidates:
  `tree_of_life.fbx` (hub centrepiece), `Tower_Medieval_Big.prefab` (CatalogBootstrap default).
- **FLAG-2 (stale doc vs disk):** REQUIRED_PACKS.md §3 still lists Mage/Cleric hero FBX as
  tracked — only Knight/knightV2/KnightV3 exist (§4). PetDeployer's "all targets UNBACKED" note
  in older canon is half-stale: bodies now resolve, controllers/clips/cosmetics still don't.
- **FLAG-3 (absent art, carried):** `HeroPortraits/` folder still absent (every hero-portrait
  load nulls); `Cosmetics/` empty; `Intro/`, `UI/`, `heart-wing` unbacked; Sfx beyond the 4
  shipped files = procedural fallback.
- **FLAG-4 (no-fallback pack):** ParticlePack keys (54) silently vanish on a pack-less machine —
  the ONE pack whose absence has no tracked runtime fallback (§6).
- **FLAG-5 (license hygiene, resolved but watch):** CC-BY-NC dragon cluster git-rm'd in
  `08b912bf`; licensed `Assets/Dragon/` + force-tracked `SyndrathDragon.controller` are the only
  sanctioned dragon assets. Never resurrect `Enemies/Dragon.*` keys.
- **FLAG-6 (naming bugs, carried):** `HudIcons/Wizard/wiard.jpg`, `Wizard_Lightining.jpg` typo
  keys; `HudIcons/a pic.png` stray; `Structures/arcane tower.fbx` space-in-name (its albedo had
  to move OUT of the same-named folder to resolve — `HubStructureVisualInjector.cs:75`).
- **FLAG-7 (static KayKit NPCs):** the 12 staged bodies have no Animator wiring — they idle
  frozen until the follow-up WO lands (§2).
- **FLAG-8 (EnemyVfxSet bare-ish):** `EnemyVfxSet_Default.asset` remains the live per-type VFX
  wiring point; ranged-cast keys route through HovlVfxCatalog — un-authored keys no-op by design
  (`HeroAbilities.cs:1422`), so silence here is expected, not proof of wiring.
