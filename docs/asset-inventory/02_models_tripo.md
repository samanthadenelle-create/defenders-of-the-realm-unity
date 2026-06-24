# Asset Inventory 02 — Models / Tripo / Art (non-KayKit)

Read-only due-diligence survey. FACTUAL MAP, not a recommendation.
Scope: `Assets/Models/*` (non-KayKit) + `Assets/Art/*`, plus Tripo character/
structure models wherever they live. KayKit packs are out of scope (separate doc).

## Summary

- Playable/usable CHARACTERS here: the **Tripo hero models live in
  `Assets/Resources/Heroes/`** (Knight.fbx, Mage.fbx, Cleric.fbx, Ranger.fbx —
  the canon single-Knight north-star hero is `Resources/Heroes/Knight.fbx`,
  loaded by `HeroBodySwapper.cs`). Tripo ENEMY models (Orc Mage/Tank/Warrior +
  Knight hero source) sit in `Assets/Art/Incoming_Tripo/`.
- Structures: Tripo owner art in `Assets/Art/TripoStructures/` (BuildTower, Farm,
  Forge, LumberMill, PetHome, Portal). CastleGate fbx + (empty) Cathedral in
  `Assets/Models/`. Crystals/Marketplace/Towers/Tree_Of_Life in `Assets/Art/`.
- NPC pack `Assets/Models/People/` (DEF-91, LFS-committed) = 4 rigged townsfolk
  (Blacksmith, Merchant, Peasant Mevina, Peasant Tob) + a 3-LOD FighterClass.
- Pet: `Assets/Models/Pet/` = a single Fox/Coyote bake (textures only at top;
  raw `sprite.fbx` in `_archive_raw/`). Gitignored.

### Summary table

| Folder | Type | Count (key) | Rigged | Gitignored | Used in code? |
|---|---|---|---|---|---|
| Models/People | NPC townsfolk pack | 4 chars + 1 LOD set; ~4-5 anims each | YES | NO (LFS) | YES (enemies.json) |
| Models/Pet | Fox/Coyote pet | 1 char (bake + raw) | YES (raw) | YES | YES (ice-wolf.json) |
| Models/CastleGate | Gate+tower structure | 1 fbx | static | NO | NO |
| Models/Cathedral | (EMPTY) | 0 | - | YES | NO |
| Art/TripoStructures | Tripo building art | 6 structures | static | YES | NO |
| Art/Incoming_Tripo | Tripo hero+enemy src | 1 hero + 3 orcs | static src | NO | NO (staging) |
| Art/Heroes/ATB | concept JPGs | 4 images | n/a | NO | NO |
| Art/Enemies/ATB | concept JPGs | 4 images | n/a | NO | NO |
| Art/Towers | tower structures | 2 fbx | static | NO | NO |
| Art/Crystals | prop | 1 fbx | static | NO | - |
| Art/Marketplace | structure | 1 fbx | static | NO | - |
| Art/Tree_Of_Life | hero prop | 1 fbx (root) | static | NO | - |
| Resources/Heroes (Tripo) | PLAYABLE HEROES | 4 fbx | rigged | NO | YES (HeroBodySwapper) |

Note: "Used in code?" = cheap grep of `Assets/_Modules` + `Assets/Resources`.

---

## Assets/Models/People/  (NPC townsfolk pack — DEF-91)

What it is: optimized human NPC pack, the committed-via-LFS exception.

- Characters (rigged, SKM_ = skinned mesh): **Blacksmith, Merchant,
  Peasant (Mevina), Peasant (Tob)** — each its own subfolder with .fbx + .ma +
  .obj + .mtl + Textures/ + Animation/.
- LOD set at root: `FighterClass` at 3 LODs (`0_..LOD0`, `1_..LOD1`,
  `2_..LOD2` .Fbx + .json) + `0_FighterClass...fbm/` (7 textures).
- Animations (per-char, FBX clips):
  - Blacksmith: 5 (Forging, Idle_1, Talking, Talking2, Walk)
  - Merchant: 4 ; Peasant Mevina: 4 ; Peasant Tob: 4
  - Peasant Tob also ships `SKM_Peasant_Tob_Unity.fbx` (Unity-ready variant).
- RIGGED: YES (skinned + per-char anim sets).
- Gitignored: NO — tracked via Git LFS (filter: lfs confirmed). The exception.
- Used in code: YES — referenced by path in
  `Assets/Resources/Data/Canonical/enemies.json` (folder path only; no _Modules
  .cs reference to the SKM_ meshes directly).

## Assets/Models/Pet/  (Fox / Coyote pet)

What it is: a single pet creature.
- Top level: only `0_Fox_Normal_Normal_512_LOD0.fbm/` = 3 baked textures
  (Coyote_Mesh_Bake Diffuse/Metallic/Normal). The fbx itself is NOT at top level.
- `_archive_raw/` holds the raw source: `sprite.fbx` + `sprite.fbm/` +
  `Materials/` (1 .mat, GUID-named).
- RIGGED: the raw `sprite.fbx` is the animated source.
- Gitignored: YES.
- Used in code: YES — `Assets/Resources/Pets/ice-wolf.json` references
  `Models/Pet`. (Note: the live pet roster art is mostly under
  `Resources/Pets/` — aether-sprite, flame-pup, sprite — separate from this.)

## Assets/Models/CastleGate/

What it is: a castle gate + ballast + tower structure (single fbx).
- `castle+ballast+Tower.fbx` + `.fbm/` (basecolor jpg etc). Static.
- Gitignored: NO. Used in code: NO grep hits in _Modules/Resources.

## Assets/Models/Cathedral/  (EMPTY)

Folder exists with only a `.meta`; **no model files**. Gitignored: YES.
Used in code: NO. (Placeholder.)

---

## Assets/Art/TripoStructures/  (owner Tripo building art)

What it is: the owner's Tripo-generated STRUCTURE models (gitignored owner art).
- Structures (each .fbx + .fbm/ textures + .tripo-extracted marker):
  **BuildTower, Farm, Forge, LumberMill, PetHome, Portal** (Portal = .fbm only,
  no top-level fbx seen). PetHome has the largest texture/material set
  (~27 part materials in `Materials/`).
- Shared `Materials/` + `Textures/` folders.
- RIGGED: NO (static structures).
- Gitignored: YES (confirmed).
- Used in code: **NO.** Grep for "BuildTower" in _Modules hits the
  `BuildTower` CODE SYMBOL (TowerConstruction / TowerConstructionQueue /
  BuildMenu / OnboardingFlow) — NOT this `BuildTower.fbx` art. "TripoStructures"
  and "Forge.fbx" have zero hits. So this art is not wired into code.

## Assets/Art/Incoming_Tripo/  (Tripo character SOURCE / staging)

What it is: staging area for incoming Tripo character art (heroes + enemies).
- `Heroes/Knight/` — **Knight.fbx** + 4 PBR maps (basecolor/metallic/normal/
  roughness; "medieval_knight_3d_model_*"). This is the SOURCE of the live
  Knight hero.
- `Enemies/Orcs/` — three orcs, each .fbx + 4 PBR maps:
  **Orc_Mage, Orc_Tank, Orc_Warrior** (the V1 orc family).
- RIGGED: source fbx (import staging); no anim clips alongside.
- Gitignored: NO (tracked).
- Used in code: NO direct grep hits (staging; the LIVE copies live in
  `Resources/Heroes` + `Resources/Enemies`).

## Assets/Art/Heroes/ATB/ + Assets/Art/Enemies/ATB/  (concept art, NOT models)

- Heroes/ATB: 4 JPGs — elara_healer_states, grom_knight_states,
  thrain_wizard_states, roster_animation_states_gray (animation-state concept
  sheets).
- Enemies/ATB: 4 JPGs — goblin_family, orc_mixed_family, orc_warband,
  troll_family.
- No fbx/prefabs. Reference imagery only.

## Other Assets/Art/ folders (brief)

- **Towers/**: 2 fbx — BlastTower, VikingWatchTower (static structures).
- **Crystals/**: 1 fbx (Crystals) + crystals/ subfolder. Prop.
- **Marketplace/**: 1 fbx (marketplace) — structure.
- **Tree_Of_Life/** + `Tree_Of_Life.fbx` at Art root: the Heart-of-Elarion
  world tree (enchantedtree3dmodel textures). Static.
- **Title/**, **Hero Select/**: title-screen / hero-select JPG art (no models).
- **UI/**: HudIcons, ItemIcons, Raids (2D UI art).
- **VFX/**: Projectiles (VFX art).
- All tracked (not gitignored) except where noted.

---

## Tripo model LOCATIONS (cross-cutting — where the Tripo art actually lives)

- **PLAYABLE HEROES (live, rigged):** `Assets/Resources/Heroes/` —
  Knight.fbx, Mage.fbx, Cleric.fbx, Ranger.fbx, each with `*.fbm/`
  (tripo_mat_* Diffuse/Normal) + `*.fbx.tripo-extracted`. Materials in
  `Resources/Heroes/Materials/` (tripo_mat_*). `HeroBodySwapper.cs` loads
  `Resources/Heroes/Knight.fbx` as the canon armored Knight (-90 yaw, Tripo
  material pipeline).
- **Hero weapon props (Tripo-extracted):** `Resources/Heroes/Props/Weapons/`
  (axe_A, bow_A/B/C, dagger_A, hammer_A, shield_A) + `Props/Bow.fbx`.
- **PETS (Tripo):** `Assets/Resources/Pets/` — aether-sprite, flame-pup,
  sprite (tripo_mesh_*/tripo_node_* textures) + Materials/.
- **ENEMIES (Tripo):** `Assets/Resources/Enemies/Demon.fbm` (tripo_mat_*); orc
  source art in `Art/Incoming_Tripo/Enemies/Orcs`.
- **STRUCTURES (Tripo):** `Assets/Art/TripoStructures/` (gitignored owner art).
- **STAGING:** `Assets/Art/Incoming_Tripo/` (Knight + Orcs source).

Markers: `*.tripo-extracted` files mark assets run through the Tripo extract
pipeline; `tripo_mat_*` materials/textures are the Tripo PBR outputs.
