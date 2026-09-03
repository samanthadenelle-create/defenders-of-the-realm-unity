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
| VFX | **119** *(was 15; recount 2026-08-06)* | `HovlVfxCatalog.asset` + `VFXCatalog.asset` + **36 prefabs across `Projectiles/` (13), `Death/` (6), `Harvest/` (6), `Aura/` (4), `Env/` (3), `Boss/` (1), `Weapon/` (1)** + **`_Shared/` (83 files: Materials/Textures/Models/Shaders/Animation)** — the WO-886/887/888 build-out plus the `948080f5` self-containment mirror. §6 |
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

## 3b. Enemy bodies — the shared-texture TRAP (added 2026-08-20, verified from a render)

- **`TripoAssetPostprocessor.OnPreprocessModel` claims EVERY FBX under `Assets/EnemyContent/`,**
  not just Tripo output, and force-sets `materialLocation = External (legacy)` +
  `materialName = BasedOnTextureName` + `materialSearch = RecursiveUp` on every import that has no
  `<Body>.fbx.tripo-extracted` sentinel beside it. Legacy External mode **resolves a model's
  material by SEARCHING the project for a `.mat` named after the TEXTURE, and IGNORES the `.meta`
  `externalObjects` remap table entirely.**
- **Consequence, and it is a class of bug, not an instance:** any two bodies whose diffuse files
  share a NAME collapse onto ONE project material. The four AccuRig skeleton bodies
  (`Skeleton_Warrior/Rogue/Healer/Mage`) all name their diffuse `Material_Pbr_Diffuse`, so
  **7 enemy ids rendered with the Mage's texture** — the owner's "enemies not having coloring".
  Tripo bodies escape only because their textures carry unique hashed names
  (`tripo_mat_<hash>_Pbr_Diffuse`). That is why the Orc/Troll set never showed the defect.
- **⚠ Rewriting the `externalObjects` remaps DOES NOT FIX IT and is how you make it worse.**
  `EnemyMaterialRemap` / `SearchAndRemapMaterials` resolve by the same name search, so repointing
  the remaps only MOVES the collision — observed live: all seven flipped to the *Warrior's*
  texture and the previously-correct Mage broke.
- **The fix is all three together:** write the sentinel (`<Body>.fbx.tripo-extracted` — the
  postprocessor's own opt-out), set `materialLocation = InPrefab`, and remap to a **per-body
  `.mat`** bound to that body's own `<Body>.fbm/` diffuse. Any two of the three still render
  another body's art. Tool: `DeNelle.Editor.EnemyBodyMaterialFixer.Run`
  (`ENEMY_BODY_MATERIAL_FIX_OK`). Guard: `EnemyBodyTextureRegression.RunAll`
  (`ENEMY_BODY_TEXTURE_OK`) — asserts no two bodies share a base map and every base map lives in
  its own `.fbm`. Evidence: `EnemyProvingHarness.RunBatch` → `Builds/EnemyCaps/*.png`.
- `Materials/Material_Pbr.mat` is an **addressable entry** (`Enemy_Art.asset`,
  `Enemies/Materials/Material_Pbr`) and holds the **Mage's** diffuse — correct; **add** per-body
  siblings, never mutate or delete it. `Materials/Material_Pbr_Diffuse.mat` is a pre-existing
  orphan holding the Warrior diffuse; it is the asset the texture-name search kept landing on.
- `Skeleton_Healer.fbm/` did not exist until 2026-08-20; the Healer's diffuse was embedded in the
  FBX and had **never been extracted**, so it had no art of its own to bind. It does now.

## 3c. The two `_NEW` bodies — where an enemy's colour ACTUALLY comes from (2026-08-20)

- **`_NEW` DISAMBIGUATES A MESH FILE, NOT A CHARACTER.** `Necromancer_NEW.fbx` and
  `Skeleton_Golem_NEW.fbx` carry the suffix only because a legacy sculpt of the same name already
  sat in `EnemyContent`; their authored atlases ship under the **legacy** name
  (`TripoTex/Necromancer_basecolor.jpg`, `TripoTex/Skeleton_Golem_basecolor.jpg`). These two matter
  more than most — **WO-954 swapped TO them** because the owner rejected the KayKit originals, so
  the bodies she chose were the ones rendering as **pure-white silhouettes**
  (`Builds/EnemyCaps_before/necromancer.png`, `hollow-brute.png`).
- **THE RESOLUTION ORDER, which is the fact that was missing.** For any model there are three
  sources, in precedence:
  1. **The imported material on the mesh** (`externalObjects`, or the importer's name SEARCH).
     Real in edit mode, in a build, and before `Start()`. The only tier guaranteed to match the
     mesh's own UVs. **This was empty for both bodies.**
  2. **`TripoMaterialFixer`'s fallback atlas** — `EnemyFactory.ResolveBasecolor` →
     `SetFallbackTexture("Enemies/TripoTex/<name>_basecolor")`. Applies **only** when the source
     material has no map. **RUNTIME-ONLY** (`Run()` is driven from `Start()`), and resolved through
     a Resources/Addressables address whose **Resources half no longer exists**
     (`Assets/Resources/Enemies` was deleted).
  3. The family **miss-tint** / `EnemyBodyColorGuard` — a flat colour floor, not a look.
- **The `_NEW` name-strip in `EnemyFactory.ResolveBasecolor` (91ea3ca95) is CORRECT and STAYS** —
  but it only ever reaches tier 2, which is why every editor-side observer still saw a blank body.
  Binding at tier 1 is what makes the look independent of load path and lifecycle.
- **Per-body albedo source (verified by render, not by name):**
  - `Necromancer_NEW` **carries its own embedded art** →
    `Necromancer_NEW.fbm/tripo_mat_82fc39ea_Pbr_Diffuse.jpg`. Own UVs; preferred.
    *(The legacy `TripoTex/Necromancer_basecolor.jpg` **also fits** — proven in
    `Builds/OrcCaps/10_Necromancer_NEW__TripoTex_Necro.png`. Which one ships is a creative call.)*
  - `Skeleton_Golem_NEW.fbm` **is EMPTY** — that FBX embeds no texture, so the legacy
    `TripoTex/Skeleton_Golem_basecolor.jpg` is its only candidate. **It fits**: cracked-stone
    plating registers on the armour plates, the skull lands on the head, ribs on the chest cavity
    (`Builds/OrcCaps/13_Skeleton_Golem_NEW__TripoTex_Golem.png`).
- **Tool: `DeNelle.Editor.NewBodyAlbedoBinder.Run`** (`NEW_BODY_ALBEDO_OK <n>/<n>`). Applies §3b's
  same three-part fix — sentinel, `InPrefab`, per-body `.mat` — with the albedo chosen as **own
  `.fbm` diffuse first, `_NEW`-stripped `TripoTex` atlas second**. ⚠ It gathers material names from
  **three** places (FBX sub-assets, the existing external-object map, the prefab's renderers): once
  a material is remapped it stops being a sub-asset, so a sub-asset-only probe reports "no embedded
  Material to remap" and cannot repair the state the postprocessor creates.
- **Guard: `EnemyArtCoverageRegression.RunAll`** (`ENEMY_ART_COVERAGE_OK` / `_FAIL`) — every model
  referenced by `enemies.json` must resolve a basecolor at some tier (bound / own `.fbm` / atlas /
  loose pack image). **Marked `regression-registry: standalone` ONLY until the orc art lands**;
  register the `[enemy-art-coverage]` row in `DataRegression.RunAll` then.
- **⚠ OPEN HOLE — THE SENTINEL IS GITIGNORED (`.gitignore:573  *.tripo-extracted`).** All six
  `<Body>.fbx.tripo-extracted` files (§3b's four skeletons + these two) exist only in the local
  working tree. The `.meta` half of the fix IS tracked, but `OnPreprocessModel` rewrites
  `materialLocation`/`materialName` on **every import that has no sentinel**, so on a fresh clone
  the tracked settings are overwritten and both fixes silently revert to the texture-name SEARCH.
  This is measured, not inferred: the first pass at the `_NEW` binding wrote `externalObjects`
  **without** the sentinel, and the reimport flipped `materialLocation` to External and extracted
  texture-named materials, ignoring the remap entirely. **Either un-ignore the sentinels or move
  the opt-out to a tracked asset** — until then this repair does not survive a clone.
- **⚠ THE ORC NAMING TRAP.** `EnemyContent/OrcTex/` holds three per-body orc atlases — but they are
  **`Orc_Mage`, `Orc_Tank`, `Orc_Warrior`**, while `enemies.json` references **`Orc_Berserker`,
  `Orc_Shaman`, `Orc_Necromancer`**. Adjacent names, different bodies. "The orc art is already in
  the project" is therefore false, and only matching the folder against the DATA shows it.
  **⚠ PARTLY SUPERSEDED 2026-08-20 — see §3d: `orc-shaman` now references `Orc_Mage`,** so the
  "no enemies.json row names an OrcTex body" half of this bullet is no longer true. The naming
  trap itself still stands for `Orc_Tank` / `Orc_Warrior`, and the OrcTex atlases are still the
  WRONG art for the current `Orc_Mage` mesh (§3d) — which is the sharper version of the same warning.

## 3d. `Orc_Mage` — the slot was REUSED, and its old atlases are now poison (2026-08-20)

- **The mesh at `EnemyContent/Orc_Mage.fbx` was REPLACED** (owner ruling *"use the unused orcmage"*)
  with a fresh AccuRig delivery: 100 skin clusters on a `CC_Base` skeleton, 60,351 verts, one
  material. **The `.meta` — and therefore the GUID — was PRESERVED**, so every Addressables entry
  and every reference that pointed at the old sculpt still resolves. Only the binary changed.
  *(This is the SECOND replacement of this slot; a 2026-08-09 swap preceded it, which is why the
  file already had one stale atlas before this one added a second.)*
- **⛔ `TripoTex/Orc_Mage_basecolor.jpg` AND `OrcTex/Orc_Mage_basecolor.jpg` ARE BOTH STALE** — each
  was baked for a DIFFERENT, superseded sculpt. Rendered on the current mesh they produce the
  camouflage-patch scramble, proven in `Builds/OrcMageCaps/03_*.png` and `04_*.png`. They are
  **deliberately left on disk** (`EnemyRigColorRegression:182` and `AtbCombatantSwapper:761-763`
  assert the OrcTex path exists, and the folders are shared with `Orc_Tank` / `Orc_Warlord` /
  `Orc_Warrior`) — **ADD, never mutate.** Deleting them is a separate ticket.
- **WHY THEY ARE HARMLESS, measured rather than assumed.** `EnemyFactory` still hands the fixer
  `SetFallbackTexture("Enemies/TripoTex/Orc_Mage_basecolor")` — the poison IS registered. But
  `TripoMaterialFixer`'s per-slot rebuild reaches the fallback only through
  `if (tex == null && fallbackTex != null)`, and the built body measures **1/1 renderer slots
  carrying their own `_BaseMap`** from `Orc_Mage.fbm/tripo_mat_2256a6d3_Pbr_Diffuse.jpg`, with no
  forced texture and no fallback tint. `tex == null` is false on every slot, so the branch never
  runs. **Tier-1 binding is what makes the stale atlas unreachable** — this is the concrete case
  §3c's precedence list describes. Instrument: `DeNelle.Editor.OrcMageProof.RunBatch`
  (`ORC_MAGE_PROOF_OK`), which prints the slot census beside the verdict.
- **The delivery shipped TWO diffuse images and they are different pictures, not one file renamed**
  — `orcmage.fbm/tripo_mat_2256a6d3_Pbr_Diffuse.jpg` (md5 `b2bd4950`, AccuRig re-bake) and the
  unrigged convert's `orcmage_basecolor.JPEG` (md5 `f90e74b7`). **Both REGISTER on the rigged
  mesh** — they share a UV layout, so the usual "the wrong atlas smears" tell does NOT fire here
  and cannot be used to choose. The AccuRig bake is the one that ships because it is visibly
  cleaner (the convert's is blotchy, with baked-in AO mottling); `Builds/OrcMageCaps/01_*.png` vs
  `02_*.png` is the comparison. **Do not assume a mismatched atlas always scrambles.**
- Tool: `DeNelle.Editor.OrcMageBodyImport.Run` (`ORC_MAGE_IMPORT_OK`) — applies §3b's three-part
  fix for this body and measures the result back off the imported asset (bones, avatar validity,
  upright check, slot-to-`.mat` census) instead of asserting it.
- **`Orc_Mage_Legacy.fbx` and the `tripo_mat_80c4114e` pair inside `Orc_Mage.fbm/` are now more
  clearly dead** — `80c4114e` was already proven pixel-identical to `Orc_Tank`'s, and it belonged
  to a sculpt that is now two replacements behind. Left in place; cleanup is its own ticket.
- **`orc-shaman` now wears `Orc_Mage`** (owner ruling: *"shaman and mage can use same form one is
  healer class other is dps"*). Consequence worth knowing: **`Orc_Shaman.fbx` is now referenced by
  nothing but the `ogre` stand-in** (`ogre` asks for `OgreMage`, which has no mesh, and falls back
  to `Orc_Shaman` in `EnemyFactory`). The remaining BARE orc bodies are `Orc_Berserker`
  (worn by `orc-berserker` + `orc-raider`), `Orc_Necromancer`, and `Orc_Shaman` (via `ogre`).

## 3e. `Cellar_Hollow` — imported, bound, PROVEN, and ~~DELIBERATELY NOT WIRED~~ **NOW WIRED** (2026-08-20)

> ### ⛔ SUPERSEDED LATER THE SAME DAY — `Cellar_Hollow` AND `Hollow_Walker` ARE WIRED.
> Commit **`577bde576`** made the five-edit change this section says an art-import lane does not own,
> for **both** bodies: `KnownHollowModels`, `CommittedModels`, the `HollowTable` row (ModelKey +
> AnimatorRig), `EnemyAnimatorFactory.RigFor` → `SkeletonHumanoid`, and
> `EnemyResolverRegression.ExpectedBaseModel`, plus `enemies.json` (both copies, now md5-identical).
> `cellar-hollow` **lost its `Variant "cellar"`** (owner's read: *"a tanky type or barbarian ish
> type"*, not a kneeling mourner). Both bodies were also added to **`EnemyFactory.AccuRigIntake`** —
> `CC_Base` **+X-forward** exports that would otherwise spawn turned 90°, which is a **separate axis**
> from rig class (the intake is which way a mesh FACES; the rig is which clips it plays).
> Result: `REGRESSION_OK 227/227 suites, 0 red` + `COMPILE_GATE_OK`; the four failures that stood
> before it, all naming `Hollow_Walker` or `Cellar_Hollow`, are closed.
>
> **The analysis below stays exactly as written — it is the reason the wiring was needed and it was
> right.** Only the "NOT WIRED" verdict is out of date. The open question in the last bullet (⚠ *it
> does not read as a hollow one* — a living green-skinned soldier, no exposed bone) was **NOT**
> answered by that commit and is still an owner call.

- **The body is in the tree and it is correct.** `Assets/EnemyContent/Cellar_Hollow.fbx` (AccuRig,
  94 bound bones, humanoid Avatar valid) + `Cellar_Hollow.fbm/tripo_mat_acabe1ac_Pbr_{Diffuse,Normal}.jpg`
  + `Materials/Cellar_Hollow_Body.mat` + the sentinel. §3b's three-part fix applied by
  `DeNelle.Editor.CellarHollowImport.Run` (`CELLAR_HOLLOW_IMPORT_OK`); proven by
  `DeNelle.Editor.CellarHollowProof.Run` (`CELLAR_HOLLOW_PROOF_OK`, `Builds/CellarHollowProof/`).
  `EnemyBodyTextureRegression` is green at **11** embedded-media bodies with it in scope.
- **⛔ `enemies.json` WAS NOT CHANGED, ON PURPOSE.** Pointing the `cellar-hollow` row at this body
  would have been INERT AND RED at the same time, which is the fact worth writing down:
  `cellar-hollow` is a HOLLOW id, so `EnemyFactory.ModelForEnemy` resolves it through
  `EnemyResolver.TryResolveHollowModel`, which honours a data `modelKey` **only if it is in
  `EnemyResolver.KnownHollowModels`** — a seven-name set that predates this body. The row would
  still have spawned `Skeleton_Minion` while `EnemyResolverRegression` check 12 failed the tree for
  naming a key that is neither committed nor declared art-pending. **A new enemy body is a
  three-edit change, not a data edit** (all three in files an art-import lane does not own):
  `KnownHollowModels`, `CommittedModels` (both `Assets/_Modules/Core/Enemies/EnemyResolver.cs`),
  and `ExpectedBaseModel` in `Assets/Editor/Regression/EnemyResolverRegression.cs`. The same pin
  blocks `Hollow_Walker`, imported the same day by a different lane.
- **THE FILENAME.** Delivered as `cellar hollow.fbx`, **with a space** — the only one in the
  delivery set. Imported as `Cellar_Hollow`: space → underscore, Pascal case, matching the roster's
  key shape and `displayName` "Cellar Hollow", so `EnemyFactory.TryBasecolor`'s
  `Enemies/TripoTex/<model>_basecolor` probe would resolve rather than silently miss.
- **⚠ THIS DELIVERY DOES NOT EMBED ITS TEXTURES — and `ExtractTextures` LIES ABOUT IT.** Measured:
  `ModelImporter.ExtractTextures` returned **`true`** and wrote **nothing**. AccuRig shipped the two
  maps as loose files in a sibling `cellar hollow.fbm/` folder that the FBX references by relative
  path. So the return value proves nothing here — only an image actually being in `<Body>.fbm/`
  does, which is what the importer asserts. Staging the delivery's `.fbm` contents beside the
  renamed FBX is a required import step for AccuRig bodies of this shape.
- **THE TEXTURE FORK WAS BENIGN THIS TIME — proven, not assumed.** The hashed
  `tripo_mat_acabe1ac_Pbr_Diffuse.jpg` (179,783 B) and the pretty
  `cellar_hollow_basecolor.JPEG` (112,608 B) have different md5s but are **the same bake at
  different JPEG quality**: rendered on the same rigged mesh from one camera they are
  indistinguishable (`ab_embedded_fbm.png` vs `ab_convert_basecolor.png`, coverage 8.22% both).
  That is the OPPOSITE of the orc delivery an hour earlier, where the two were different bakes —
  so "AccuRig re-bakes its own atlas" is **not** a rule, and the A/B render stays the only way to
  know which delivery you have.
- **⚠ IT DOES NOT READ AS A HOLLOW ONE.** The body is a **living green-skinned orc/goblin soldier**
  in a bucket helm with strapped leather and steel plate — no exposed bone anywhere. Beside
  `Skeleton_Warrior` and `Skeleton_Rogue` (bare skulls, bone limbs) it matches on rig, scale
  (H≈1.04 m vs 1.06 / 1.18 m), poly style and value range, and differs on SPECIES. Whether it
  ships as `cellar-hollow` or joins the Orc Warband is an owner call, and it is the reason this
  landed unwired rather than swapped in.
- **Facing:** it points the opposite way to the KayKit hollows (photographed from the same camera,
  the skeletons face the lens and this body shows its back) — the AccuRig `+X`-forward convention.
  Registering it means adding it to `EnemyFactory.AccuRigIntake` too, or it spawns turned 90°.

## 4. Heroes — KnightV3 is THE body; Cleric/Mage/Ranger FBX are GONE

- On disk/tracked FBX at root: **`Knight.fbx`, `knightV2.fbx`, `KnightV3.fbx` only** (all LFS;
  `KnightV3.fbx` verified `filter: lfs`). `Cleric/Mage/Ranger` keep only their `.controller` +
  `.fbx.tripo-extracted` markers — **the 4-hero FBX set no longer exists**; REQUIRED_PACKS
  §3 ("Knight, KnightV3, Mage, Cleric tracked") is stale on this point.
- **REFUTATION 2026-08-06 (`d0c7b8fd`) — `*.fbx.tripo-extracted` is NOT a parked mesh.**
  `Ranger.fbx.tripo-extracted` is a **125-byte PLAIN TEXT SENTINEL** written by
  `TripoAssetPostprocessor`. **There is nothing to un-park.** Knight's sentinel sits beside a
  live `Knight.fbx`, which proves the marker never blocked an import. **WO-909's premise was
  wrong** and the comments repeating it are fixed. Do not spend another cycle on it.
- **THE INVISIBLE-HERO P0, FIXED (`d0c7b8fd`) — this folder's absence was shipping NOTHING.**
  Ranger and Mage have **no FBX at all**, so both fell to a **Blink base body**, and
  `Assets/Blink` is **GITIGNORED**. On a fresh clone the terminal fallback logged a failure and
  **RETURNED WITHOUT INSTANTIATING ANYTHING**, after `Start` had already destroyed the
  placeholder — **an INVISIBLE HERO, not a Knight-degrade**. Both bail-outs now build a
  **tracked KayKit body** (`HeroBodySwapper.BuildTrackedFallbackBody`; the KayKit stage in §2 is
  the only humanoid body set actually in the repo, verified via `git ls-files`). A missing art
  pack may now look WRONG; it can never look like NOTHING. Ranger/Mage body art stays OPEN.
- **Hero portraits — a HALF-fixed import defect (`d0c7b8fd`).** **Thrain's** portrait was
  imported as a plain TEXTURE, so `Load<Sprite>` returned null and it fell to the blurrier
  RawImage path while Sylas hit the crisp one; its `.meta` now differs from Sylas's only by
  guid. **Grom and Elara carry the IDENTICAL defect and are NOT fixed — and Grom is the DEFAULT
  hero**, so the default hero renders on the blurry path. OPEN.
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
  its own flagged follow-up: promote used prefabs into tracked Resources). **PARTIALLY ADDRESSED
  2026-08-06** — the promote-to-tracked follow-up is what §6-DELTA below actually did.

### DELTA 2026-09-02 — `MarqueeSpellVfx`, and the gitignored-prefab trap under it

**`Assets/_Modules/Village/Vfx/MarqueeSpellVfx.cs` (`DeNelle.Village`, WO-1305 Part A).** The ONE
declaration of which owner-tagged VFX keys are SELF-CONTAINED ("marquee") spells — prefabs that own
cast **and** flight **and** impact themselves, so the ability system must SUPPRESS its own projectile
spawn for that cast. Without the declaration such a prefab produces TWO bodies per cast: the prefab's
own authored fireballs plus the engine's orb travelling to the real target.

- ⛔ **IT IS A STRING SET AND NOTHING ELSE. `VFXManager.PlayKey` REMAINS THE SINGLE SPAWN OWNER.**
  The class holds a `HashSet<string>` and answers `IsMarquee(key)`; it **never instantiates
  anything**, owns no pool, and is not a second spawner. Marquee keys play through the same pooled
  `PlayKey` path as every other key. (`Assets/_Modules/Village/Vfx` is already scar tissue from a
  second VFX stack — do not grow one here.)
- **NOT a creative pick.** A key appears only because the OWNER tagged that prefab to that key in her
  VFX Caster (`Assets/Editor/VfxManualPicks.json`, `manual:true`) AND ruled the effect a marquee.
  Never add one from a CLI judgement call (project memory `vfx-map-owner-tags-no-creative-pick`).
- Declaring a key here does **not** bind it to an ability; the owner still binds it via a
  `motion-castings.json` row's `vfxKey` or an owner-tagged `abilities.json` `VfxCast`.
- `TraceRecognised` fires `FlowTrace.Once` per key, deliberately: a suppression that leaves no trace
  is indistinguishable from a broken projectile (CLAUDE.md §12).
- The one declared key today is `firespell_Cast`. **Its `IsLoop: 0` catalog row is a HARD
  PREREQUISITE** — 4 of the prefab's 7 emitters are authored looping, and only an `IsLoop:0` row lets
  `VFXManager.EnforceOneshotEmission` clear them (see the 08-06 delta below on `IsLoop` being derived,
  not hand-checked).
- ⚠ **AND THE TRAP:** that prefab lives at `Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab`,
  and **`Assets/Spells Pack/` is GITIGNORED** (`.gitignore:430-431`, confirmed by
  `git check-ignore -v`). A prefab edit there **cannot be committed, never reaches another machine,
  and dies at the next re-import — while still changing what the local build produces.** That is the
  §16 shape exactly: it works here and proves nothing about what ships. Anything load-bearing about a
  Spells Pack prefab must be captured in TRACKED files (a catalog row, a mirrored copy under
  `Assets/Resources/VFX/_Shared/`, or a declaration like this class), never in the prefab alone.

### DELTA 2026-08-06 - the VFX build-out, the self-containment P0, and what is still open

*Sourced from commits `3db877d2`, `bd532d5b`, `7f3971a3`, `0011b8ba`, `a186c282`, `a12c6d22`,
`948080f5`, `29f9ac2b`, `4ef2d532`, `1534dffb`, `449b16bb` (2026-08-05).*

**A. THE SELF-CONTAINMENT P0 (`948080f5`) — the whole reason FLAG-4 existed.**
`CopyAsset` duplicates the **PREFAB ONLY** — never its materials, textures, shaders, meshes or
animations. So every prefab duplicated into `Resources/VFX` was a **tracked file pointing
straight back into GITIGNORED art**. Measured: **27 of 28 prefabs, 183 references, 73 distinct
assets** — all rendering missing (magenta / untextured / invisible by platform) on any machine
without the packs. **Now 0**, verified TWICE (the mirror's own report plus an independent
recursive GUID walk that does not reuse the builder's code). Exposure reached a mesh
(`FireFly.fbx`), a nested pack prefab pulled in through the ParticleSystem **LIGHTS** module,
two `.anim`, a `.controller` and two C# MonoBehaviours. **~23.85 MB mirrored, deduped**, into
`Assets/Resources/VFX/_Shared/` (`Glow.mat` was referenced twelve times, `Trail` nine — ONE
copy each). The mirrored FireFly shader is renamed under a **`VFXMirror` namespace** so it
cannot collide with the pack copy still present on this machine; materials bind by GUID, so the
rename is free.
- **THE TWO SCRIPTS COULD NOT BE MIRRORED AND WERE STRIPPED** — the one judgement call, and it
  is **felt-visible: `Casting_Fire` no longer spawns a projectile.** Copying a `.cs` would put
  two identical types in `Assembly-CSharp` and take the compile gate down for every lane.
  Removal is right on its own merits: nothing references the pack namespace, and inside a
  POOLED manager-driven prefab those demo scripts read a Rigidbody that is not there,
  `Destroy()` a pooled instance on collision, and `InvokeRepeating` a fireball once a second
  forever.
- **New regression** (`Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs`,
  marker `VFX_ART_MIRROR_OK`) walks every `Resources/VFX` prefab and **fails on ANY dependency
  in a gitignored root**. It deliberately does **NOT** require zero deps outside the VFX tree
  (that would force mirroring tracked art like Lana Studio and the URP package shaders) and
  reports that total separately.
- **THE MIRROR ONLY CONVERGED ON A FIRST RUN — fixed in `29f9ac2b`.** It seeded its dependency
  walk from the PREFABS, so on any later run the prefab already pointed at the MIRRORED
  material, the walk saw a target outside the pack, skipped it, and **never re-entered that
  material** — leaving the pack texture the material itself referenced undiscovered. **Six
  prefabs read as self-contained while their art was one hop away.** It now re-seeds from
  everything already mirrored: **a fixed point has to be fixed ACROSS runs.** Two collisions
  surfaced: `ParticlesLight`, and `ramp01`/`Ramp01` — the second pair differing **ONLY IN
  CASE**, which is one file on Windows and two on CI.
- **NOT gitignored, contrary to assumption: Lana Studio.** Only its **URP upgrade subfolder**
  is (`.gitignore:312`, already correct in §8). `Flash_generic` sources all seven of its
  dependencies there and measures **zero exposure**.
- **KNOWN, NOT FIXED:** `SpellsPackVfxMirror.cs`'s header makes the same false fresh-clone
  claim; and **`_Shared/Textures` is 16.9 MB of `.tif` sitting OUTSIDE the texture-optimizer
  sweeps' root list**. Also: **the base tower's Hovl muzzle key points straight into the
  gitignored pack** so it renders nothing on a fresh clone — left alone because it carries the
  tier scale; the tracked type now plays alongside it.

**B. IT WAS A CONNECTION PROBLEM, NOT AN ART PROBLEM (`3db877d2`).** **26 of 79 enum values are
wired to real art with ZERO gameplay callers** — the PERFECT-hit flash, four per-species death
bursts, the enemy caster's bolt. **Six whole TRACKED Lana categories sit at 0% usage.** A GUID
sweep of **8,795 prefabs and 156 scenes found ZERO VFX scripts attached anywhere**, which is
what makes `EliteVFXController` dead three separate ways.
- `a186c282` connected four (PERFECT hit flash, per-species enemy deaths, the enemy's blow
  landing, the hero's own death). `Die()` had routed override -> typeSet -> generic and **never
  consulted the species**, so the pool/factory spawn path could never reach the four authored
  death bursts; species now sits AFTER the authored per-prefab set and before the generic.
- `0011b8ba` appended **16 new `VFXType` values after `Boss_FireBreath`** — append-only,
  because the catalog serialises `VFXType` by **ORDINAL, not by name** (verified:
  `Boss_FireBreath` still reads `Type: 79`).
- `a12c6d22` built **14 of those 16** into tracked prefabs + catalog rows (marker
  `PARTICLE_PACK_VFX_BUILD_OK`, `Assets/Editor/ParticlePackVfxBatchBuilder.cs`). Emission is
  **MEASURED off each asset, never taken from the doc**, and `IsLoop` goes through the shared
  `VfxLoopFlagRegression` resolver rather than a second local derivation. All 14 sources ship
  `playOnAwake` enabled and the builder **clears it on every system**, or a prewarmed pool
  instance emits at the world origin. **`Enemy_Spawn` and `Despawn_Dissolve` are DEFERRED, not
  faked** — they are SCRIPTED recipes carrying a pack MonoBehaviour plus a demo mesh to
  dissolve, and need a runtime component driving the TARGET's material cutoff: authoring work,
  not a copy. `Env_Candle` uses `TinyFlames`, **not** the pack's `Candles`, because `Candles`
  carries candle GEOMETRY (three mesh renderers).

**C. `Resources/VFX/Boss/Boss_FireBreath.prefab` — the Particle Pack's first sanctioned import
(`7f3971a3`, WO-759).** The dragon's finale breath had been a comment: `FireBreath()` was an
anim trigger plus instant damage with NO stream, so **the player took 8.5 damage from a dragon
that visibly did nothing.** The pack prefab is a **THREE-LAYER RECIPE** (FlameThrower +
FireEmbers(3) + Smoke at rates 30/100/20), duplicated **whole** into `Assets/Resources/VFX/Boss/`.
The builder **PROVES the emission family off the real asset** (confirmed CONTINUOUS,
`rateOverTime=30`, bursts=0) and **hard-fails if the root reads as burst**. Socket authored by
script onto `dragon_Snout_bone` (the rig has 198 named nodes; the resolver ranks
snout > mouth > jaw > chin > head and demotes `_ctrl` / nub / IKGoal helpers). URP
`m_RequireDepthTexture` **0 -> 1** (pack fire uses soft particles, which hard-clip into geometry
without a depth buffer); **HDR deliberately left OFF as a mobile perf call.** Marker
`BOSS_FIREBREATH_BUILD_OK`. **Known follow-up:** the Medium quality tier (root layer only) is
NOT reachable from `DragonBoss` — `VFXHandle` exposes no accessor for the pooled GameObject by
design; today Medium and High both get the full three-layer stack.

**D. `449b16bb` — MagentaGuard FALSE POSITIVE (an art-absence report that was not one).**
`MagentaProbe` FAILed on the Arcane Tower Hovl aura (`ElectricyCenter`, slot 0, material NULL).
**No art was missing.** On a `ParticleSystemRenderer`, slot 0 is the particle material and slot
1 the trail; that system is **trail-only by vendor design**, so the empty slot is legitimate.
The guard assigned a shared opaque URP/Lit (`NullSlot_MagentaFix`) **OUTSIDE the dedupe guard**,
so it was unconditional and **stuck in built players — the aura rendered as a white opaque
blob. 28 of 261 Hovl prefabs carry this pattern**, all sharing one material instance. A
renderer is now exempt **only** when it is a `ParticleSystemRenderer` **AND** at least one other
slot holds a valid material; an **all-null particle renderer is still a real defect and is still
reported**.

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
- ⛔ **`/Assets/Spells Pack/` (430-431)** — verified 2026-09-02 with `git check-ignore -v`; the
  folder EXISTS on this machine and is ignored in full (the `.meta` too). It is the home of
  `Spell_Fire_9.prefab`, the first declared marquee VFX (§6 DELTA 2026-09-02). **An edit to a prefab
  in here cannot be committed and dies at the next re-import, while still changing the local build** —
  so never let a gitignored prefab be the only place a behaviour is recorded.
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
- **FLAG-4 (no-fallback pack) — LARGELY CLOSED 2026-08-06 (`948080f5`, `29f9ac2b`), with named
  residue.** The `Resources/VFX` tree is now **self-contained: 183 pack references across 27 of
  28 prefabs -> 0**, verified twice, with **~23.85 MB deduped into `Resources/VFX/_Shared/`** and
  a standing regression (`VFX_ART_MIRROR_OK`) that FAILS on any dependency in a gitignored root.
  **Residue that is still real:** (a) the base tower's Hovl **muzzle key still points into the
  gitignored pack** and renders nothing on a fresh clone (deliberate — it carries the tier
  scale; the tracked type plays alongside it); (b) `SpellsPackVfxMirror.cs`'s header repeats the
  old false fresh-clone claim; (c) **`_Shared/Textures` is 16.9 MB of `.tif` outside the
  texture-optimizer sweeps' root list**; (d) any owner-tagged `VfxManualPicks.json` key added
  from here on re-opens the same hole unless it is mirrored. §6-DELTA A.
- **FLAG-9 (2026-08-06, VFX loop-cap leak — the P0 the towers were hiding, `bd532d5b`):**
  `IsLoop` was a **sticky manual checkbox** that `VfxCasterWindow` force-set true for any
  Projectile/Aura row; **95 of 135 Hovl rows carried `IsLoop:1`**, including every `PP_*Impacts`
  and `PP_MuzzleFlash` — all single bursts at t=0. **A loop row never returns its slot** (the
  oneshot branch registers a deadline and gets swept; the loop branch does a bare `++` and hands
  back a handle, and the only loop reclaim frees DESTROYED hosts — pooled objects are never
  destroyed). **Cap is 20.** Both catalog generators now DERIVE `IsLoop` from the art
  (`VfxLoopFlagRegression` is the single resolver; **53 of 122 picks were wrong**); marker
  `VFX_LOOPFLAG_OK`. **STILL OPEN and deliberately NOT bundled: the ONESHOT pool saturates at
  40/40** in three separate captures — different pool, different reclaim path; the loop fix must
  NOT be assumed to close it.
- **FLAG-10 (2026-08-06, `449b16bb`): a MagentaGuard FAIL is not proof of missing art.** The
  Arcane Tower aura `ElectricyCenter` FAILed on slot 0 = null while being **trail-only by vendor
  design** (slot 0 = particle material, slot 1 = trail). The guard's opaque URP/Lit repair sat
  **outside the dedupe guard**, so it was unconditional and **stuck in built players — a white
  opaque blob**. **28 of 261 Hovl prefabs carry the pattern.** Exemption is now conditional
  (ParticleSystemRenderer AND another slot valid); an all-null particle renderer is still a real
  defect. §6-DELTA D.
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
