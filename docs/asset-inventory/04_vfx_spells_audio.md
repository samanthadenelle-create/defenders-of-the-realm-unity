# Asset Inventory 04 - VFX, Spells, Audio, Shaders

Read-only due-diligence survey (2026-06-24). FACTUAL MAP, not a recommendation.
Counts exclude `.meta` files. Surveyed under `C:\eoa\Assets`.

## Summary table

| Pack / dir        | Type                         | Files | Prefabs | Gitignored | Used in game?                         |
|-------------------|------------------------------|-------|---------|-----------|----------------------------------------|
| Mirza Beig        | Particle VFX (Ultimate VFX)  | 3428  | 564     | YES       | Partial - via VFXCatalog wrappers      |
| Spells Pack       | Spell/projectile VFX         | 2039  | 466     | YES       | YES - nested in Resources/VFX wrappers |
| Black Dragon      | Creature model (1 FBX)       | 9     | 0       | YES       | Likely (DragonBoss) - unverified link  |
| Action            | Character animation FBX      | 401   | 0       | NO (tracked) | Likely (hero/enemy clips)           |
| Audio             | Music + 1 mixer (committed)  | 44    | 0       | NO (tracked) | YES                                  |
| Shaders           | Custom shaders (3 + mats)    | 6     | 0       | NO (tracked) | YES (ForceFieldGate, chat bubble)    |

VFX pipeline reality: `VFXCatalog.asset` wires ~50 `VFXType` entries but they point
to only **38 distinct prefab GUIDs**, and the project-built prefab set in
`Resources/VFX/` is just **9 prefabs**. So most catalog "wow" effects are either
shared GUIDs or fall back to procedural `AbilityVfxKit`. Thousands of pack effects
sit UNWIRED.

---

## Mirza Beig - Ultimate VFX (gitignored)

One line: Mirza Beig "Ultimate VFX" Asset Store particle pack - the big general-purpose
particle library (storms, fire, smoke, shockwaves, lightning).

- Path: `Assets/Mirza Beig/`
- gitignored: YES (`git check-ignore` -> match)
- Files: 3428. Prefabs: 564 (Loop 173, Oneshot 88 under Ultimate VFX + demos/expansions).
- Materials: 719, Textures (png): 215, Shaders: 24, Scripts (cs): 56, Scenes: 15.
- Structure: `Particle Systems/Ultimate VFX/` with `Prefabs/{Loop,Oneshot}`,
  `Expansions/{XP-ACTION, XP-CONSTR.KIT, XP-SHOCKWAVES, XP-STORM, XP-TITLES}`, Demos.
- Custom shaders (24): Add/Alpha/AnimBlend/DistanceFade families, image effects
  (Sharpen, Rain), particle shaders.

Notable effect families (by prefab-name keyword count):
- smoke x72, storm x67, fire x30, shock x21, lightning x18, ice x18,
  explosion x16, spark x15, portal x5, nova x4, impact x4, blood x3, aura x2,
  shield x1, beam x1.
- Combat "wow" usable: shockwave rings (XP-SHOCKWAVES), storm/lightning
  (XP-STORM), fire/explosion oneshots, nova bursts, portal loops, title embers.

Used in game: PARTIALLY. `VFXManager.cs` header names Mirza Beig as a prefab source.
Some `VFXCatalog` GUIDs resolve into Mirza/Lana content via Resources wrappers, but
the bulk of 564 prefabs is unwired.

---

## Spells Pack (gitignored)

One line: element-matrixed spell VFX pack - casting/projectile/explosion/aura/buff/
shield organized by element (Arcane, Dark, Fire, Ice, Light, Nature, Storm). This is
the pack the combat projectile + spell pipeline actually draws from.

- Path: `Assets/Spells Pack/`
- gitignored: YES
- Files: 2039. Prefabs: 466 (incl. Variations). Materials: 148, Textures (tif/png/tiff): 286,
  FBX: ~60, Shaders: 4, anim: 9, controllers: 8.
- Structure: `Particles/Prefabs/{Auras, Buffs, Projectiles/{Casting,Explosion,Projectiles},
  Shields, Spells, Tomes, Variations}` + a `Demo/` scene.

Notable families (clean element-by-effect matrix):
- Auras x7, Buffs x7, Shields x7 (one per element).
- Casting x20, Explosion x20, Projectile x20 (elements x numbered variants 2/3/4).
- Named examples: `Projectile_Arcane/Fire/Ice/Storm/Dark/...`,
  `Explosion_Fire_4`, `Casting_Ice_3`, `Aura_Fire`, `Buff_Storm`, `Shield_*`.
- Ideal for: spell projectiles, cast wind-ups, impact bursts, hero/enemy buffs,
  ward/shield bubbles. Directly matches the `Impact_*/Projectile_*/Cast_*/Aura_*`
  VFXType taxonomy.

Used in game: YES (most-wired pack). The 9 project prefabs in `Resources/VFX/Projectiles/`
NEST Spells Pack prefabs/materials by GUID - confirmed e.g.
`Resources/VFX/Projectiles/Projectile_Arcane.prefab` references Spells Pack
`Spell 4.mat`, `Explosion_Arcane.prefab`, and the pack's demo `Projectile.cs`.
`ProjectileVFXCatalog.cs` and `PooledProjectile.cs` document Spells Pack as the
flying-body + impact source. Still only a thin slice of 466 prefabs is wired.

---

## Black Dragon (gitignored)

One line: a single rigged/animated dragon creature model (NOT a VFX pack).

- Path: `Assets/Black Dragon/`
- gitignored: YES. Files: 9.
- Contents: `Dragon_Baked_Actions_fbx_7.4_binary.fbx` (baked-action rig) +
  `Materials/` (Dragon_Bump_Col2 diffuse jpg+mat, Dragon_Nor_mirror2 normal jpg).
- Used in game: LIKELY - `_Modules/Village/Enemies/DragonBoss.cs` exists and the
  VFXType enum has a full `Boss_*` phase set; direct asset->script link not verified
  in this survey.

---

## Action (tracked)

One line: a character animation library (FBX clips only) for heroes + enemies - NOT VFX.

- Path: `Assets/Action/`
- gitignored: NO (tracked in git).
- Files: 401, all `.fbx` (198 unique animation FBX + metas).
- Breakdown: Knight 99, Enemies 20, Wizard 15, Shared 15, Ranger 13.
- Sample (Knight): `idle`, `crouch to standing idle`, `draw sword 1/2`, ...
- Used in game: LIKELY - matches the Knight/Ranger/Wizard + enemy roster; clip
  wiring to animators not traced here.

---

## Audio (tracked)

One line: music tracks + the master audio mixer (committed to git).

- Path: `Assets/Audio/`. gitignored: NO. Files: 44.
- Audio clips (mp3): 18 music tracks. SFX: NONE here (SFX is procedural - see note).
- Mixer: 1 (`Resources/Audio/GameAudioMixer.mixer`).
- Music inventory:
  - Battle: `Overworld_Battle_1`, `Overworld_Battle_2`, `Overworld_Boss_Fight`,
    `Overworld_Victory`, `battle`, `battle_theme_NEW`, `battle_theme2_NEW`,
    `battle_theme3_NEW`.
  - World/Village: `mainworld1_NEW`, `world_theme_NEW`, `village`, `title`,
    `echo_theme`.
  - Raid: `brass-rampart`.
  - Stingers: `victory`, `Victory/Victory`, `defeat`.
- SFX NOTE: no .wav/.ogg SFX clip folder here. SFX is generated in code -
  `_Modules/Audio/ProceduralSfx.cs` + `SfxClipLibrary.cs` + `SfxId.cs`
  (procedural/synthesized), routed through the AudioService.
- Used in game: YES - `Resources/`-loaded music + the committed mixer.

---

## Shaders (tracked)

One line: a few hand-written project shaders (not a pack).

- Path: `Assets/Shaders/`. gitignored: NO. Files: 6.
- `ForceFieldGate.shader` (+ `ForceFieldGate.mat`) - region-gate force field
  (ties to the RegionGate crossing primitive).
- `RoundedChatBubble.shader` - UI chat/dialogue bubble.
- Used in game: YES (gate visual + chat UI). NOTE: Mirza Beig ships 24 more particle
  shaders inside its (gitignored) pack folder - not counted here.

---

## What the VFXCatalog actually MAPS today vs what's available

- VFXType enum defines ~95 named events; `VFXCatalog.asset` wires ~50 of them.
- Those ~50 entries resolve to only **38 distinct prefab GUIDs** (heavy GUID reuse -
  several VFXTypes share one prefab, e.g. multiple Type 53/54/59/60 -> same guid).
- The project's OWN built VFX prefab set is **9 prefabs** in `Resources/VFX/Projectiles/`
  (`Projectile_/Explosion_` x Arcane/Fire/Ice/Storm + `Flash_generic`) - thin wrappers
  that nest Spells Pack content.
- Unwired entries fall back to procedural `AbilityVfxKit` (per VFXManager design).
- AVAILABLE but UNWIRED: ~455 Spells Pack prefabs and ~555 Mirza Beig prefabs are NOT
  referenced by the catalog - a large untapped library for spell trails, hit impacts,
  buffs, shockwaves, storm/lightning, auras.

Standout combat effects to draw from (available, mostly unwired):
- Spells Pack: full element matrix of Casting / Projectile / Explosion / Aura / Buff /
  Shield (Arcane/Fire/Ice/Storm/Dark/Light/Nature) - the cleanest match to the
  Impact_/Projectile_/Cast_/Aura_ taxonomy.
- Mirza Beig: XP-SHOCKWAVES (slam rings), XP-STORM (lightning/storm), fire & explosion
  oneshots, nova bursts, portal loops - good for boss/elite "wow" and ground slams.
