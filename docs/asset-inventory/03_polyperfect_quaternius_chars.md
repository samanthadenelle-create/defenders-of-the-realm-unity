# Asset Inventory 03 — Polyperfect / Quaternius / Supercyan / Blink / Lana Studio

Due-diligence survey (read-only). FACTUAL map of what exists, NOT a recommendation.
Last surveyed: 2026-06-24. Counts from disk; usage = cheap grep of
`Assets/_Modules` + `Assets/Resources`.

## Summary table

| Pack | What it is | Size | gitignored | Rigged chars? | Used in code? |
|---|---|---|---|---|---|
| polyperfect | Low Poly Ultimate Pack (3,080 `_M` prefabs, env/props/buildings) | 450M | YES | A few static People_M humanoids (no anim rig wired) | YES (StructureFactory, builders, RotationCorrectionRegistry) |
| Quaternius | Medieval Village MegaKit — buildings/env modules ONLY | 128M | YES | NO (zero chars, zero anims) | YES (RotationCorrectionRegistry, HubStructureVisualInjector, ClaimableCamp) |
| Supercyan | "Character Pack: Fantasy" — 8 RIGGED fantasy humanoids + shared anim lib | 321M | YES | YES — 8 bodies, ~51 Fantasy combat anims | Referenced (TroopFactory/TroopDef comment-level; not Resources-loadable) |
| Blink | Huge multi-bundle art pack (chars/armor/weapons/NPCs/UI) — JUNKED hero-armor source | 13G | YES | YES — ~292 char prefabs incl. modular armor humans + Orc NPCs | Partial: armor ICONS copied to Resources; bodies NOT Resources-loadable; BlinkWardrobe/UI code refs |
| Lana Studio | Casual RPG VFX pack (particle FX: fire/slash/projectiles/shields) | 125M | NO (tracked) | NO | YES (VFXCatalog, VFXManager, ProjectileVFXCatalog) |

> Pointer: the exhaustive polyperfect catalog already exists at
> `C:\eoa\docs\polyperfect-asset-catalog.md` (verified 2026-06-13). This doc
> SUMMARIZES it and adds the other four packs — do not re-derive polyperfect.

---

## 1. Polyperfect — see existing catalog

Full file-by-file catalog: **`docs/polyperfect-asset-catalog.md`**. Key facts:

- Loadable unit = `_M/Prefabs_M/<Category>_M/<Name>.prefab` (bare `<Name>`, not `SM_`).
  `_M` = the Standard quality tier the project standardizes on (CLAUDE.md sec 4).
  Parallel tiers exist: `_T` Tribal (owner's tower/outpost ladder), `_H`/`_L` rarely used.
- `_M/Prefabs_M` total = **3,080 prefabs across 41 category folders**
  (Fantasy 82, Medieval 41, Animals 28, Tribal 36, Nature 177, Buildings 237, ...).
- Single shared atlas texture → cheap batching.
- gitignored AND outside Resources → NOT `Resources.Load`-able directly; in-editor
  builders reference by GUID, runtime use requires mirroring into
  `Assets/Resources/Structures/`.
- Characters: People_M has a handful of medieval humanoids (`Man_Knight`,
  `Man_Monk`, `Skeleton`, etc.) and Animals_M has 28 creatures (`Wolf`, `Bear_Brown`,
  `Horse`...). These are STATIC display meshes — no animation rig is wired in-project.
- Used in code: YES — StructureFactory, VillageSceneBuilder/builders,
  RotationCorrectionRegistry, HubStructureVisualInjector, MagentaGuard,
  EnvironmentTreeMaterialFixer all reference polyperfect paths/prefabs.

---

## 2. Quaternius — Medieval Village MegaKit

One line: a modular medieval BUILDING/ENVIRONMENT kit. NO characters, NO animals,
NO animations.

Single root: `Assets/Quaternius/Medieval Village MegaKit/`.
Prefabs under `Modules/Prefabs/`:

| Folder | Count |
|---|---|
| Wall | 91 |
| Roof | 109 |
| Window-Door | 48 |
| Prop | 56 |

Total ~304 prefabs. `.anim`/`.controller` files = **0** → entirely static kit.
Props = balconies, stairs (interior + exterior), fences, vines, crates, wagon,
chimneys, bricks, supports. Roofs = extensive modular flat/round tile system.

- Rigged/animated: NO — all static structural geometry.
- gitignored: YES.
- Used in code: YES — RotationCorrectionRegistry, HubStructureVisualInjector,
  ClaimableCamp, MagentaGuard reference Quaternius paths (building/camp art).

---

## 3. Supercyan — Character Pack: Fantasy  (THE key rigged-char pack)

One line: 8 RIGGED low-poly fantasy humanoids sharing a single Mecanim humanoid rig
+ a large shared animation library (incl. a dedicated Fantasy combat anim set).

Rigged character models — `Assets/Supercyan/Models/Fantasy/` (8 bodies):

`fantasy_archer`, `fantasy_barbarian`, `fantasy_demon`, `fantasy_knight`,
`fantasy_mage`, `fantasy_orc`, `fantasy_skeleton`, `fantasy_wizard`.

Character prefabs (`Prefabs/Fantasy/`) — three flavors per body:
`Base/`, `WithItemLogic/`, `WithItemAnimators/`, each in `High Quality/` + `Mobile/`
tiers. 86 prefabs total (8 chars + weapon/item attach prefabs:
Sword, Bow, Arrow, Axe, Mace, Spear, Staff, Knife, Shield).

Animations: shared humanoid library, `Animations/CharacterPackAnimations/` —
~325 anim FBX across categories (Movement, Aim, Crouch, Prone, Strafe + non-fantasy
Office/Hospital/Retail/Cleaner/Zombie sets). The relevant subset:
**`.../Fantasy/` = ~51 combat FBX** — arming/holding/attack/defence per weapon
(SwordAndShield slash/thrust/bash/block, Bow shoot+reload, DualAxes, DualKnives,
Spear thrust, Staff cast/summon, Unarmed) + `stunned`/`hit`/`wakeup`. 26 `.anim`,
2 `.controller` authored in-pack.

- Rigged/animated: YES — full humanoid rig + retargetable anim set. This is the
  pack that maps cleanly to a hero/enemy/troop pipeline (shared rig = one retarget).
- gitignored: YES.
- Used in code: REFERENCED but not runtime-loaded from the pack. `TroopFactory.cs`
  / `TroopDef.cs` mention Supercyan only in a comment (humanoids face +Z, yaw
  convention). No Supercyan prefab lives under `Assets/Resources` → not
  `Resources.Load`-able as-is (would need mirroring like polyperfect).

---

## 4. Blink — multi-bundle art pack (JUNKED hero-armor source)

One line: a very large (13G) multi-bundle art pack — stylized + low-poly characters,
MODULAR human armor, a mega weapon pack, Orc NPCs, and a UI re-skinner toolkit. This
is the source of the JUNKED Blink hero-armor system (project canon: Blink armor
junked 2026-06-22; player hero is now ONE Tripo model).

Top-level bundles: `Art/`, `StylizedArmorBundle2/`, `UltimateBundle/`, `Tools/`.

Whole-pack file counts (non-meta): 2,203 png, 780 prefab, 760 fbx, 390 mat,
160 tga, 149 terrainlayer, 66 asset, 28 unity, 18 psd, 7 cs.

Characters & NPCs (`Art/`):
- `Characters/` — 259 fbx, 292 prefab. Split `LowPoly/` (Humans_LowPoly + anims +
  demo) and `Stylized/` (Humans + Demo_HumanArmorPack1/2/3 + Integrations). The
  modular human armor (bare mannequin + swappable Arms/Legs/Chest/Feet) is the
  Dressable/wardrobe source.
- `NPCs/Stylized/` — 53 fbx. Includes an **Orcs** set
  (`Meshes_Orcs/Prefabs_Orcs/Animations_Orcs/Animations_OrcBoss`) and Demo_NPCs.
- `Weapons/` — 405 fbx (LowPoly MegaWeaponPack1, etc.).
- `Animations/` — ~43 anim/fbx.

Code in-pack: 7 `.cs` — a `StatBar.cs` (UI), 2 `MaterialTilingOffset` editor utils,
and the `Tools/UIReSkinner/` toolkit (`Blink_UI_ReSkinner.cs` + UI template scripts).

- Rigged/animated: YES — rigged stylized + low-poly humans with anim sets, plus
  rigged Orc/OrcBoss NPCs.
- gitignored: YES.
- Used in code: PARTIAL. `BlinkWardrobe.cs` (Dressable capability) + UI code
  (`ElarionUiKit`, `RpgUiCatalog`, MVVM panels, `FeatureFlags` Blink gate) reference
  "Blink" by name. The 3D bodies are NOT under Resources (not runtime-loadable). What
  DID land in Resources = armor ICON pngs only: `Resources/ItemIcons/blink_armor_basic*.png`.
  Per canon the Blink hero-armor system is JUNKED; what remains here is raw art +
  the UI re-skinner tool + the leftover icon set.

---

## 5. Lana Studio — Casual RPG VFX

One line: a particle/VFX pack (fire, slash, projectiles, shields, regen, loot,
states) — NOT characters.

Single root: `Assets/Lana Studio/Casual RPG VFX/`. File counts: 128 prefab,
98 png, 22 mat, 15 unity (demo scenes), 9 fbx (demo floor etc.), 4 controller,
4 anim, 3 cs (demo input/switcher). ~125M.

- Rigged characters: NO — VFX prefabs only (the few fbx are demo-stage geometry).
- gitignored: **NO — this pack is git-TRACKED** (the only one of the five not ignored).
- Used in code: YES — `VFXCatalog.cs`, `VFXManager.cs`, `ProjectileVFXCatalog.cs`,
  `TorchFireController.cs` reference Lana Studio VFX prefabs (a `Flash_generic`
  projectile VFX is mirrored into `Resources/VFX/Projectiles/`).

---

## Bottom line on rigged CHARACTERS (the pipeline-relevant part)

- **Supercyan** = the cleanest rigged-humanoid source: 8 fantasy bodies (Knight,
  Archer, Mage, Wizard, Barbarian, Orc, Skeleton, Demon) on a SHARED humanoid rig
  with a ~51-clip Fantasy combat anim set — retarget-once pipeline. NOT yet
  Resources-loadable (no mirror).
- **Blink** = a much larger but messier rigged source (292 char prefabs, modular
  armor, rigged Orc/OrcBoss NPCs, 405 weapons) — but it is the JUNKED hero-armor
  system; only armor icon pngs reached Resources, bodies are not runtime-loadable.
- **Quaternius** and **Lana Studio** have NO characters (buildings and VFX resp.).
- **Polyperfect** People_M/Animals_M are STATIC display meshes, no anim rig wired.
