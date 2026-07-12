# SME Dossier — polyperfect Low Poly Ultimate Pack & Quaternius Medieval Village MegaKit

**Authored:** 2026-07-11 (overnight SME research session)
**Scope:** the two gitignored world-art packs — `Assets/polyperfect/` and `Assets/Quaternius/`
**Verified from:** on-disk trees at `C:\eoa` (branch `wip/village2-and-f8-tickets`), code greps of
`Assets/_Modules` + `Assets/Editor` + `Assets/_Village2`, existing canon docs, the owner's Asset
Store ledger (`docs/SME/ASSET_STORE_LEDGER_2026-07-12.md`), and vendor sites (polyperfect.com,
quaternius.com). Every prefab-name claim below was re-verified against the actual files today.

**Product identities (ledger-canonical):**
- **polyperfect — Low Poly Ultimate Pack v10.0** (released 2025-10-15, purchased 2026-05-29).
  Unity Asset Store package 54733. v10 = Landmarks update (18 landmarks + landmark islands +
  26 weapons + Landmarks demo scene) **plus a "BIG cleanup and name fixing" pass** (rename-risk
  audit in §P5). v9.5 added the Empire set (291 models + 18 people). Official URP materials
  package has shipped since v8.2. v7.0 introduced the **Generic animation system** (§P3).
- **Quaternius — Medieval Village MegaKit** (not on the store ledger; sourced from
  quaternius.com / itch.io, **CC0**). We hold the *source version* — the Unity-URP
  implementation with custom ShaderGraph shaders and per-model optimized collision meshes.

---

## Table of contents

- [Part A — polyperfect Low Poly Ultimate Pack](#part-a--polyperfect-low-poly-ultimate-pack)
  - [P1. Inventory](#p1-inventory)
  - [P2. The quality-tier system (_M vs _T) — and a catalog correction](#p2-the-quality-tier-system-_m-vs-_t--and-a-catalog-correction)
  - [P3. The Generic animation system (v7.0) — implementation & logic](#p3-the-generic-animation-system-v70--implementation--logic)
  - [P4. How WE consume the pack (file:line consumer map)](#p4-how-we-consume-the-pack-fileline-consumer-map)
  - [P5. v10 "BIG cleanup and name fixing" — rename-risk audit](#p5-v10-big-cleanup-and-name-fixing--rename-risk-audit)
  - [P6. Intended usage + the URP conversion story](#p6-intended-usage--the-urp-conversion-story)
- [Part B — Quaternius Medieval Village MegaKit](#part-b--quaternius-medieval-village-megakit)
  - [Q1. Inventory](#q1-inventory)
  - [Q2. How WE consume the kit (file:line consumer map)](#q2-how-we-consume-the-kit-fileline-consumer-map)
  - [Q3. Intended usage + URP story](#q3-intended-usage--urp-story)
- [Part C — Web research: vendors, exact packs, companions](#part-c--web-research-vendors-exact-packs-companions)
- [Part D — Opportunities + gaps](#part-d--opportunities--gaps)
- [Part E — Fresh-clone reimport pitfalls (both packs)](#part-e--fresh-clone-reimport-pitfalls-both-packs)
- [Part F — Executive summary](#part-f--executive-summary)

---

# Part A — polyperfect Low Poly Ultimate Pack

## P1. Inventory

**Root: `Assets/polyperfect/`** (~246 MB, gitignored — `.gitignore:120`)

```
Assets/polyperfect/
├── Common/                          ← runtime code! Polyperfect.Common.asmdef
│   └── - Code/Scripts/              AnimationDelay.cs, AnimationOffset.cs (§P3)
├── PolyPerfect_AssetList.txt        ← vendor-generated full file list, 12,573 lines
│                                       (stamped 06/07/2026 — generated on THIS machine)
└── Low Poly Ultimate Pack/
    ├── README_LowPolyUltimatePack.pdf
    ├── URP_LowPolyUltimatePack.unitypackage   ← official URP material conversion (since v8.2)
    ├── PRESET_Mesh_Rig.preset / PRESET_Mesh_Static.preset  ← FBX import presets
    ├── Animations/                  ← §P3 (People_Animation + Position/Rotation/Scale loops)
    ├── Materials/                   M_Atlas_LPUP.mat (THE shared atlas material),
    │                                M_Atlas_Night_LPUP, M_Atlas_Transparent_LPUP,
    │                                M_Rays_LPUP + Colors/ (per-color solid materials,
    │                                e.g. M_21_Grey_Light_LPUP used by CastleHubBuilder)
    ├── Particles/                   particles_lava / _smoke / _vulcaon_smoke prefabs
    ├── Terrains/                    DEMO_07_Wildwest.asset (TerrainData)
    ├── Textures/                    Atlas_Albedo_LPUP.png + Emission/Night/Gradient/
    │                                Specular atlases + source PSD
    ├── _M/                          ← THE tier we use (CLAUDE.md §4)
    │   ├── Meshes_M/<Category>_M/SM_<Name>.fbx      (meshes carry the SM_ prefix)
    │   │   └── People_M/Rigs_M/SKM_<Name>_Rig.fbx   (240 skinned people rigs)
    │   └── Prefabs_M/<Category>_M/<Name>.prefab     (prefabs use BARE names, no SM_)
    └── _T/
        ├── Meshes_T/ …
        └── Prefabs_T/<Category>_T/  (full parallel tier — 3,156 prefabs, see §P2)
```

**Prefab counts (re-verified from disk 2026-07-11):**
- `_M/Prefabs_M`: **3,080 prefabs** across **41 category folders**. Per-category counts in
  `docs/polyperfect-asset-catalog.md` lines 31–38 remain accurate (Empire 291, Scifi 287,
  People 243 top-level + Rigs_M, Buildings 237, Apocalypse 202, Furniture 195, Nature 177,
  Props 115, … Fantasy 82, Medieval 41, Tribal 36, Animals 28, Roman 2).
- `_T/Prefabs_T`: **3,156 prefabs**, same 41 categories mirrored with `_T` suffix.
- People: **246 static prefabs** in `People_M/` + **240 rigged prefabs** in `People_M/Rigs_M/`.
- Landmarks (the v10 headline): **18 landmark prefabs** in `Landmarks_M/`
  (BigBen, EiffelTower, TajMahal, Stonehenge, Statue_Moai, StatueOfLiberty, …) plus a
  `Landmarks_Islands_M/` subfolder (the "landmark islands"). All off-theme for Elarion
  except possibly Stonehenge/Petra/Moai as ruins dressing.

**The single-atlas design:** every model in the pack UV-maps into ONE albedo atlas
(`Atlas_Albedo_LPUP.png`) driven by ONE material (`M_Atlas_LPUP.mat`). This is why the pack
batches so well (draw calls collapse) and why the whole 3,000-model pack is ~246 MB — smaller
than the single Tripo Cathedral (84 MB) it replaced. `M_Atlas_Night_LPUP` swaps the emission
atlas for a lit-windows night look; `M_Atlas_Transparent_LPUP` is the alpha variant (glass etc.).

## P2. The quality-tier system (_M vs _T) — and a catalog correction

The official meaning (vendor page + README): every model ships in **two versions** —
- **`_M` = "Material" version** — polygons are assigned to swappable *materials*, so you can
  recolor pieces per-slot. This is the flexible/prototyping tier.
- **`_T` = "atlas Texture" version** — everything baked to the single texture atlas, the
  maximum-performance tier for mobile.

**⚠ Catalog correction:** `docs/polyperfect-asset-catalog.md` (line 28) describes `_T` as
"Tribal (owner's tower/outpost art)" and `docs/POLYPERFECT_NOTES.md` calls `_M` "mid/standard
LOD". Both are misreadings: `_T` is a **full parallel tier of all 41 categories (3,156
prefabs)** — `Tribal_T` is merely the one `_T` folder the owner happened to standardize on for
tower art (`GarrisonSceneBuilder.Scenes.cs:37-43` loads from `_T/Prefabs_T/Tribal_T/`
explicitly, and `CatalogPrefabImporter.cs:49` knows the `_T` root). There are no `_H`/`_L`
tiers on disk (the catalog's line 29 mentions them; they do not exist in v10). Practical
takeaway unchanged: **project canon = use `_M` prefabs** (CLAUDE.md §4); `Tribal_T` is the
one sanctioned `_T` exception.

Naming convention (a recurring trap, now settled): **prefabs use bare names**
(`Wall_Medieval_Stone.prefab`), **mesh FBXs carry `SM_`** (`SM_Wall_Medieval_Stone.fbx`),
**skinned rigs carry `SKM_…_Rig`**. Builders reference prefabs; only the one-shot generators
(`BridgePrefabGenerator`, tree fixers) touch `SM_` FBXs directly.

## P3. The Generic animation system (v7.0) — implementation & logic

This is the pack's mechanism for animating its 240+ people, and it is **fully present and
unused in our project**. Verified implementation, piece by piece:

**1. Two people-prefab families.**
- `_M/Prefabs_M/People_M/<Name>.prefab` (246) — **static statues**. Verified on
  `Man_Knight.prefab`: GameObject + Transform + MeshFilter + MeshRenderer + CapsuleCollider.
  No Animator, no bones. These are crowd/dressing props only.
- `_M/Prefabs_M/People_M/Rigs_M/<Name>_Rig.prefab` (240) — **the animatable versions**.
  Verified on `Man_Knight_Rig.prefab`: GameObject + Transform + **Animator (class 95)** +
  **SkinnedMeshRenderer (class 137)** + CapsuleCollider. The Animator ships with **no
  controller assigned** — you supply one (next point). Mesh source:
  `_M/Meshes_M/People_M/Rigs_M/SKM_<Name>_Rig.fbx`, imported with `animationType: 3` =
  **Generic** (NOT Humanoid).

**2. One shared clip library, one shared skeleton.**
`Animations/People_Animation/SKM_People_Animations.fbx` contains **5 clips**:
`walk-cycle`, `run-cycle`, `idle-standing`, `sitting-pose`, `dead-pose`
(takes named `Armature|walk-cycle` etc.). The armature uses standard bone names
(`Hips, Spine, Spine1, Spine2, Neck, Head, Left/RightShoulder → Arm → ForeArm → Hand,
Left/RightUpLeg → …`). **The logic that makes this work:** every one of the 240 rig FBXs
shares this *identical* armature hierarchy, so Generic-rig clips (which bind by transform
path) retarget across all of them for free. That is the whole "Generic animation system" —
no Avatar remapping, just a strictly shared skeleton.

**3. Five drop-in AnimatorControllers**, same folder:
`CTL_People_Idle`, `CTL_People_Walk`, `CTL_People_Run`, `CTL_People_Dead_Pose`,
`CTL_People_Sitting_Pose` — each a single-state looping controller wrapping one clip.

**4. Crowd de-sync helpers** — the only runtime code in the pack, namespace
`Polyperfect.Common` (asmdef `Assets/polyperfect/Common/Polyperfect.Common.asmdef`):
- `AnimationDelay.cs` — `[DefaultExecutionOrder(-50)]`, `RequireComponent(Animator)`.
  **OnValidate disables the Animator in the prefab**; a `Start()` coroutine waits
  `ConstantDelay + Random(0, RandomDelay)` seconds, re-enables the Animator, and fires an
  `OnAnimStart` UnityEvent. Effect: villagers don't all start their idle on frame 0.
- `AnimationOffset.cs` — `[DefaultExecutionOrder(-100)]`. Calls
  `Animator.Update(ConstantOffset + Random(0, RandomOffset))` once at start — i.e. it
  *scrubs* the animator forward, so identical loops play out of phase. If an
  `AnimationDelay` is present it chains off `OnAnimStart` instead (so the offset applies
  after the delayed start, not to a disabled Animator).

**5. Property-animation library** (same v7 system, for props not people):
`Animations/Position|Rotation|Scale/` — loop clips + controllers such as
`CTL_Rotation_Y_360_3s_Loop` (windmill blades, water wheels), `CTL_Rotation_Z_5_5s_Loop`
(sway), `CTL_Position_Y_1_5s_Loop` (bobbing), `CTL_Balloon`, `CTL_Scale_110_2s_Loop`
(pulse). Drop the controller on any prop's Animator and it animates — no code.

**Recipe to animate a polyperfect villager in OUR project (e.g. ambient NPCs in
MainCastle_Hall / Village2):**
1. Instantiate `_M/Prefabs_M/People_M/Rigs_M/<Name>_Rig.prefab` (editor-bake via
   AssetDatabase like our other builders — pack is outside Resources).
2. Assign `runtimeAnimatorController = CTL_People_Idle` (or Walk/Run/Sitting). For a
   wander-capable villager, author ONE tiny controller of ours with Idle/Walk states and a
   `Speed` float, reusing the pack's 5 clips — all 240 skins share it.
3. Add `AnimationDelay` + `AnimationOffset` with small random ranges for crowds.
4. Medieval-appropriate skins already shortlisted in the catalog §7: `Man_Knight_Rig`,
   `Man_Knight_Soldier_Rig`, `Man_Monk_Rig`, `Man_Monk_Old_Rig`, `Man_Lord_Rig`,
   `Man_Farm_Rig`, `Woman_Farm_Rig`, `Man_Sir_Rig`, `Man_Servant_Rig`, `Skeleton_Rig`,
   `Skeleton_Soldier_Rig` (all verified present in `Rigs_M/`).

**Limits to respect:** only 5 clips — idle/walk/run/sit/dead. No work loops (hammering,
farming), no attacks, no talk gestures. The rigs are **Generic**, so our mocap library
(Humanoid, ActorCore/Studio Mocap) does NOT retarget onto them as-imported. (Switching a
rig FBX's import to Humanoid *might* work given the clean bone names, but is unverified —
treat as an experiment WO, not a fact.) For ambient village life, the 5 clips are enough;
for actor NPCs, our KayKit/Blink/Tripo rig pipeline remains the tool.

## P4. How WE consume the pack (file:line consumer map)

All consumption is **editor-bake-time by asset path or GUID** (the pack is gitignored and
outside `Resources/`, so runtime `Resources.Load` cannot reach it — prefabs that must load
at runtime get mirrored into `Assets/Resources/Structures/` by `CatalogPrefabImporter`).
Every builder LogWarnings-and-skips when the pack is absent (CLAUDE.md §4).

**Editor builders (Assets/Editor/) — the heavy consumers:**

| Consumer | Where | What it uses |
|---|---|---|
| `VillageSceneBuilder.Walls.cs` | :397–450, :418 | WO-101 stone perimeter: `Buildings_M/parts/Building Walls_M/Wall_Stone_3x3_A.prefab` + gate/corner pieces; :822 bake summary |
| `VillageSceneBuilder.Content.cs` | :387–393 | `PrefabM`/`PrefabM2` building slots; roots `Medieval_M/` + `Farm_M/` |
| `VillageSceneBuilder.CityManifest.cs` | :32–35, :283–298 | manifest strings `polyperfect/<path>` resolve against `_M/Prefabs_M/`; :232 notes People_M rigs vary |
| `VillageSceneBuilder.Fortify.cs` | :473, :591 | `Stairs_Medieval_Stone` rampart ramps |
| `CastleHubBuilder.cs` | :117, :665, :1411, :1507, :1878 | `Medieval_M/` root; `Wall_Medieval_Stone`, `Bridge_Medieval_Stone`, Colors mat `M_21_Grey_Light_LPUP.mat` |
| `CastleWallKitSpawner.cs` | :30, :52 | same Medieval_M wall kit; its dialog names the URP fix menu |
| `CastleWallsFromRecipe.cs` | :25 | `PolyRoot = _M/Prefabs_M/Medieval_M/` |
| `EnemyStrongholdBuilder.cs` | :76, :995–1205 | Village2 stronghold props: polyperfect `_M` → `Resources/Structures` fallback chain |
| `GarrisonSceneBuilder.Scenes.cs` | :33, :43, :647–794 | `_M/Prefabs_M/` + **`_T/Prefabs_T/Tribal_T/`** (the one _T consumer); role→prefab prop map with alt-category retry |
| `CatalogPrefabImporter.cs` | :31, :49 | mirrors `_M`/`_T` prefabs → `Assets/Resources/Structures/` for runtime loading |
| `BridgePrefabGenerator.cs` | :21 | GUID-pinned `SM_Bridge_Medieval_Stone.fbx` → committed prefab |
| `HedgePrefabGenerator.cs` | :23 | GUID-pinned `Building Fences_M/Fence_Shrub.prefab` → committed prefab (moat hedge) |
| `WallTools/CastleBarracksPlacer.cs` | :26 | `Military_M/Military_Barracks.prefab` (shrunk per owner 2026-06-14) |
| `PolyperfectUrpFix.cs` | :41 | THE URP fix menu (§P6) |
| `MagentaMaterialFixer.cs` | :65–67 | delegates to PolyperfectUrpFix first, then generic pass |
| `WebGLTextureShrink.cs` | :51, :89 | WebGL: caps polyperfect atlases at 512 px |
| `WebGLPlayerSettingsConfigurator.cs` | :159 | same 512 cap in build settings |

**Runtime modules (Assets/_Modules/) — references, fallbacks, healers:**

| Consumer | Where | Relationship |
|---|---|---|
| `Village/Catalog/StructureFactory.cs` | :74, :219 | skins catalog LOOK prefabs (mirrored polyperfect visuals); tier-rotation special case for `Tower_Medieval_Big` |
| `Core/Catalog/CatalogEntry.cs` | :36 | `visualPrefabPath` documented as "Resources/polyperfect-style prefab path" |
| `Village/BuildMode/PlacementGrid.cs` | :8, :36 | **3 m grid cell chosen to match polyperfect 3×3 modular walls** |
| `Village/BuildMode/GhostPreview.cs` | :251 | ghost-shader fallback for polyperfect Standard-shader variants |
| `Village/BuildMode/RotationCorrectionRegistry.cs` | :19 | per-pack yaw corrections (Quaternius/KayKit/Polyperfect) |
| `Village/HubStructureVisualInjector.cs` | :7 | MainCastle_Hall's 8 structures bake from polyperfect/Quaternius prefabs |
| `Village/NPCs/BarracksNpcInjector.cs` | :9, :106–110 | NPCs attach to the polyperfect barracks; Warns (not Fails) if pack absent |
| `Village/World/CastleMoatBuilder.cs` | :117, :623, :788, :831 | Fence_Shrub hedge ring via the committed Resources prefab; runtime color fix note |
| `Village/World/FishSchool.cs` | :12 | builds primitive fish if the polyperfect fish prefab is absent (clone-safe) |
| `Core/TreeOfLifeMaterialFixer.cs` | :14–34, :144, :516 | runtime healer for the Village2 centrepiece tree (same conversion as PolyperfectUrpFix) |
| `Core/EnvironmentTreeMaterialFixer.cs` | :5–14, :273 | runtime healer for white ring trees (SM_Tree_Round/_Oak/_Baobab) |
| `Core/MagentaGuard.cs` | :13 | orchestrates the targeted fixers |
| `Village/World/Camps/CampVisual.cs` | :8 | deliberately NO prefab hard-dependency |

**Asset families LIVE today:** Medieval_M (walls/towers/gates/houses/siege), Buildings_M
parts (Wall_Stone_3x3 kit, Fence_Shrub), Farm_M (Farm_House), Military_M (barracks),
Nature_M trees (via generators + fixers), Tribal_T (garrison/outpost ladder),
Fantasy_M (torches/altar in catalog dressing), Colors materials.
**Owned but UNUSED:** the whole animation system (§P3), People rigs, Animals_M (28 — only
catalog-listed), Landmarks_M/Empire_M (v9.5/v10 content), Scifi/Apocalypse/WW2/etc.
(off-theme by design — catalog §9 "shelve" list), the `_T` performance tier (except Tribal_T),
Particles/, the Wildwest demo terrain.

## P5. v10 "BIG cleanup and name fixing" — rename-risk audit

Timeline removes most of the risk: v10.0 shipped **2025-10-15**; the owner purchased
**2026-05-29**; the pack landed on disk and `docs/polyperfect-asset-catalog.md` was verified
*from that disk* on **2026-06-13**. So our catalogs and builders were authored against
**post-cleanup v10 names** — we never lived through the rename.

Belt-and-braces, I spot-verified every load-bearing prefab name the builders/catalog
reference against today's disk — **all 26 resolve**: `Wall_Medieval_Stone`,
`Wall_Medieval_Wood`, `Bridge_Medieval_Stone`, `Stairs_Medieval_Stone`,
`Tower_Castle_Round`, `Tower_Castle_Square`, `Tower_Medieval_Big`, `Tower_Medieval_Wood`,
`Gate_Medieval_Medium`, `Gate_Medieval_Small`, `Military_Barracks`, `Fence_Shrub`,
`Catapult`, `Ballista`, `Farm_House`, `Windmill_Medieval`, `Stables_Medieval`,
`House_Medieval_Medium`, `House_Medieval_Large`, `Watermill_Medieval`, `Well`, `Torche`,
`Torche_Wall`, `Anvil`, `Fountain`, `Altar` — plus the People names (`Man_Knight`,
`Man_Monk`, `Man_Lord`, `Man_Sir`, `Man_Servant`, `Woman_Farm`, `Skeleton`, …).

Residual quirks to know (v10 names, not bugs):
- `Torche` / `Torche_Wall` — the vendor's French-flavored spelling survived the cleanup;
  never "Torch".
- Animals: `Bear_Brown`/`Bear_Polar` (no bare `Bear`), `Sheep_White` (no bare `Sheep`),
  `T_Rex` in Animals_M vs `Trex` in Tribal_T — the catalog already corrected these.
- GUID-pinned consumers (`BridgePrefabGenerator`, `HedgePrefabGenerator`) are immune to
  renames but break if the owner ever re-imports a *future* version where the vendor
  regenerated GUIDs — symptom would be their "pack not imported?" warning while the pack
  is visibly present.
- Any doc authored **before 2026-06-13** naming polyperfect FBXs may carry pre-v10 names —
  trust the catalog + disk, not old WOs.

## P6. Intended usage + the URP conversion story

**Vendor-intended workflow:** drag prefabs from `Prefabs_M`/`Prefabs_T`; use the import
presets (`PRESET_Mesh_Static` / `PRESET_Mesh_Rig`) when adding meshes; for URP projects,
import the bundled `URP_LowPolyUltimatePack.unitypackage`, which replaces the pack's
materials with URP versions. The README PDF at the pack root is the official doc; the
vendor maintains the pack with regular updates and takes content requests.

**The problem:** the pack ships **Built-in/Standard-shader materials**. Under URP those
render as the magenta/pink error shader (Unity's missing-shader fallback), or — the WO-323
tree case — flat white when a renderer slot resolves to no material at all.

**Our conversion (headless equivalent of the vendor's unitypackage):**
`Assets/Editor/PolyperfectUrpFix.cs` — menu **Defenders ▸ Art ▸ Fix Polyperfect URP
Materials**, batchmode `-executeMethod DeNelle.Editor.PolyperfectUrpFix.Fix`. Logic:
1. `AssetDatabase.FindAssets("t:Material")` under `Assets/polyperfect`.
2. For each material on `Standard` / `Legacy Shaders/*` / `Standard (Specular setup)` /
   InternalErrorShader / null: capture `_Color`, `_MainTex`, `_EmissionColor`; swap shader
   to `Universal Render Pipeline/Lit`; restore as `_BaseColor`/`_BaseMap`/emission
   (+keyword); force `_Smoothness=0.1`, `_Metallic=0`. Materials already on URP are left
   alone → **idempotent, in-place (GUIDs preserved)** so baked scenes keep rendering
   without a re-bake.
3. WO-323 second pass: force-reimports every `SM_Tree*` FBX so
   `PolyperfectTreePostprocessor` (the supported Unity-6 import path —
   `OnPreprocessModel` sets ImportViaMaterialDescription, `OnAssignMaterialModel` returns
   `M_Atlas_LPUP`) rebinds the shared atlas at import level. (The old
   External-material-location remap is obsolete in Unity 6 and was removed.)

Defense in depth on top of that: `MagentaMaterialFixer` (generic any-pack editor pass that
delegates to PolyperfectUrpFix first), and runtime healers `TreeOfLifeMaterialFixer` /
`EnvironmentTreeMaterialFixer` / `MagentaGuard` that repair individual renderers in a live
session when a bake predates the fix.

**Rule: after ANY (re)import of the pack, run the fix menu before judging any visual bug.**

---

# Part B — Quaternius Medieval Village MegaKit

## Q1. Inventory

**Root: `Assets/Quaternius/Medieval Village MegaKit/`** (gitignored — `.gitignore:261`).
This is the **only Quaternius set we hold** (verified: `Assets/Quaternius/` contains exactly
one pack folder). One near-duplicate exists: `Assets/Medieval Village/FBX/` is the same raw
MegaKit FBX source unzipped at top level — always prefer the Quaternius prefabs
(`docs/INSTALLED_PACKS_INDEX.md`, art-only packs section).

```
Medieval Village MegaKit/
├── Levels/            L_SampleScene_1.unity  ← artist-assembled demo village
│                      + SampleScene_Ground 1.fbx
├── Materials/         6 ShaderGraphs: M_BaseMaterial, M_BaseWear, M_Leaves,
│                      M_Plaster, M_WindowGlass, M_SampleScene_Ground
│                      + 15 material instances MI_Brick / MI_FlatTiles /
│                      MI_MetalOrnaments / MI_Plaster / MI_RedBrick / MI_RockTrim /
│                      MI_RoundRocks / MI_RoundTiles / MI_UnevenBrick / MI_Vine /
│                      MI_WindowGlass / MI_WoodTrim / MI_WoodTrim_Wear / …
│                      + full PBR texture set T_*_BaseColor/Normal/Roughness/ORM
│                      + wear masks (T_TopWear/T_MidWear/T_BottomWear)
└── Modules/
    ├── Prefabs/       304 prefabs in 4 categories:
    │   ├── Prop/          56  (balconies, stairs, chimneys, crates, wagon,
    │   │                       9 vine pieces, fences, hole covers, supports)
    │   ├── Roof/         109  (flat-tile + round-tile families, many footprints
    │   │                       2x1 … 6x14, dormers, overhangs, inclines)
    │   ├── Wall/          91  (brick/plaster/wood/uneven-brick/red-brick walls,
    │   │                       interior+exterior corners, floors incl.
    │   │                       Floor_Brick / Floor_RoundRocks / Floor_WoodDark…)
    │   └── Window-Door/   48  (7 door designs × flat/round arch, door frames
    │                           in brick/wood-dark/wood-light, windows)
    └── Source Models/ ~600 FBX (raw meshes) + Collisions/Collision_<Name>.fbx
                       — SEPARATE optimized collision meshes per model
```

Key design facts (vendor + on-disk): 300+ modular pieces that **snap to a grid**; wall
pieces model **exterior AND interior faces simultaneously** (buildings are enterable);
the ShaderGraph materials implement a **customizable wear system** (the T_*Wear masks +
M_BaseWear graph let you tint grime/wear without new textures). **License: CC0** — no
attribution, no restrictions, redistribution fine (which is why gitignoring it is purely a
repo-size choice, not a legal one).

## Q2. How WE consume the kit (file:line consumer map)

The MegaKit is the **source art of the Village2 factory** — our sister-city/raid-target
generator — and the beauty layer of the castle hub.

| Consumer | Where | What it does |
|---|---|---|
| `Assets/_Village2/Village2Generator.cs` | :4, :33–46, :357 | THE consumer. Plain MonoBehaviour (Assembly-CSharp) generating a 4-quadrant town (±42/±33 m) around the Tree of Life. Inspector slots name the intended pieces: `Wall_UnevenBrick_Straight` (walls), `Balcony_Simple_Straight/Corner` (ramparts), `Corner_ExteriorWide_Brick` (tower bases), `Wall_Arch` (gates), `Floor_Brick` (roads), `Stairs_Exterior_Straight`. **:357 — the pivot gotcha:** kit pivots vary per piece, so the generator measures each prefab's renderer bounds and compensates; never hand-place by raw transform. |
| `Assets/Editor/Village2Build.cs` | :30–40, :51–58, :63, :268–274 | Headless tooling. Menu "Defenders/Village2/1. Harvest Quaternius Buildings": opens `L_SampleScene_1.unity`, extracts the artist-assembled roots **Houses A, House C, House C (2), House D, Tower** → saves as project prefabs (HouseA/HouseC/HouseC2/HouseD/KitTower). `QuaterniusPieceRoots` (:34) searches all four `Modules/Prefabs/<Category>/` roots by filename (:496–509, warns on miss). Ground uses the kit's own `MI_SampleScene_Ground.mat` (:274). |
| `Assets/Editor/CastleHubBuilder.cs` | :50, :119, :160–218, :335, :401, :652 | Castle hub bake mixes Quaternius plaster wall runs + vine dressing over the polyperfect stone skeleton; `QRoot = Modules/Prefabs/` (:119); warns per missing prefab (:652). |
| `Assets/Editor/EnemyStrongholdBuilder.cs` | :189 | Village2 stronghold: Quaternius wall-ring props (~15.75 m wide) **carve the navmesh** — the imperfect carve note is load-bearing for enemy pathing. |
| `Assets/Editor/CastleHomeBuilder.cs` | :10, :40, :208 | Aspirational notes: swap proxy pieces for Quaternius walls/roofs/towers "for final beauty". |
| `Assets/_Modules/Village/World/Camps/ClaimableCamp.cs` | :121–153 | `SpawnQuaterniusEnemyCamp()` — **a STUB**: spawns 3 primitive cubes named "QuaterniusEnemyProp". The real Quaternius camp props were never wired (opportunity, §D). |
| `Assets/_Modules/Village/HubStructureVisualInjector.cs` | :7 | hub structures bake from polyperfect/Quaternius prefabs. |
| `Assets/_Modules/Village/BuildMode/RotationCorrectionRegistry.cs` | :19 | per-pack yaw corrections include Quaternius pieces. |
| `Assets/Editor/AssetImportPostprocessor.cs` | :89 | import rules applied under `Assets/Quaternius/`. |
| `Assets/Editor/TextureBatchOptimizer.cs` | :78 | WO-408: scans the gitignored pack's textures for the WebGL override (missing root skipped with a warning). |

**Families LIVE:** Wall (UnevenBrick + plaster runs), Prop (balconies as ramparts, vines,
stairs), Floor pieces as roads, the 5 harvested sample-scene buildings, the ground material.
**Owned but UNUSED:** most of the 109 Roof pieces (harvested buildings carry theirs, but we
never compose custom roofs), all 48 Window-Door pieces as standalone modules (enterable
interiors!), the separate collision meshes (`Source Models/Collisions/` — nothing wires
them; we rely on render-mesh or builder colliders), the wear-tint customization in
M_BaseWear, and the interior-face capability of the walls.

## Q3. Intended usage + URP story

Vendor-intended workflow: grid-snap the modules (walls define footprints, roofs cap them,
Window-Door punch openings, Props dress) — "thousands of combinations". The source version
we hold is the engine implementation: prefabs pre-wired to the ShaderGraph materials with
optimized collisions available per model.

**URP: already native.** The materials are URP ShaderGraphs — **no magenta-fix step exists
or is needed for this pack** (`docs/QUATERNIUS_NOTES.md`). This is precisely why the
Village2 factory was built from this kit. Corollary: if a Quaternius piece ever renders as
the magenta error shader, the cause is NOT the polyperfect-style Standard-shader problem —
suspect a broken ShaderGraph compile or a missing Shader Graph package, and instrument
before touching materials (CLAUDE.md §12).

Project gotchas (all encoded in `docs/QUATERNIUS_NOTES.md`):
1. Use **prefabs**, never `Source Models/` FBX (prefabs carry material assignments).
2. **Pivots vary per piece** — place through the generator's pivot-correcting path.
3. Collision meshes are separate files — wire `Collision_<Name>.fbx` if accuracy matters.

---

# Part C — Web research: vendors, exact packs, companions

**polyperfect** (polyperfect.com; Unity Asset Store publisher 19123):
- Our pack = [Low Poly Ultimate Pack, Asset Store id 54733](https://assetstore.unity.com/packages/3d/props/low-poly-ultimate-pack-54733) —
  list price €138, frequently ~€69 on sale; 7+ years of continuous updates; vendor accepts
  content requests for future updates. Official page:
  [polyperfect.com/low-poly-ultimate-pack](https://www.polyperfect.com/low-poly-ultimate-pack).
  Confirms the two-tier M(aterial)/T(atlas-texture) system and the bundled URP upgrade path.
- **Companion packs we do NOT own** (relevant if gaps appear):
  [Poly Universal Pack](https://assetstore.unity.com/packages/3d/props/poly-universal-pack-215157)
  (the newer sibling), **Low Poly Animated Animals**, **Low Poly Animated People** (these two
  are the "fully animated" upgrades — the LPUP's 5-clip system is the lightweight cousin),
  Low Poly Icon Pack, Ultimate Crafting System.
- Version history relevant to us (ledger): v7.0 Generic animation system → v8.2 URP
  materials package → v9.5 Empire set (291 models + 18 people) → **v10.0 (2025-10-15)
  Landmarks + name-cleanup = the version on disk**.

**Quaternius** (quaternius.com; also quaternius.itch.io — all packs **CC0**):
- Our pack = [Medieval Village MegaKit](https://quaternius.com/packs/medievalvillagemegakit.html)
  ([itch.io](https://quaternius.itch.io/medieval-village-megakit),
  [OpenGameArt](https://opengameart.org/content/medieval-village-megakit)) — 300+ grid-snapping
  modular pieces, FBX/OBJ/glTF + .BLEND source, with Unity(URP)/Unreal/Godot implementations
  including the custom wear shaders and per-model optimized collisions (the variant we hold).
- **Companion CC0 packs that fit our world (all free):**
  [Fantasy Props MegaKit](https://quaternius.com/packs/fantasypropsmegakit.html) (furniture,
  weapons — ideal dungeon/interior dressing for the harvested enterable buildings),
  Medieval Village Pack (whole pre-built buildings), Ultimate RPG Pack, Modular Medieval
  Building Pack, Ultimate Fantasy RTS (isometric medieval — building/unit silhouettes),
  Stylized Nature MegaKit / Ultimate Nature Pack / Stylized Tree Pack (nature),
  Downtown City MegaKit and Modular Sci-Fi MegaKit (off-theme).
- Because everything is CC0, adopting a companion pack is a download-and-drop decision —
  no purchase, no license bookkeeping; only the gitignore/reimport policy applies.

Sources: [Asset Store — Low Poly Ultimate Pack](https://assetstore.unity.com/packages/3d/props/low-poly-ultimate-pack-54733) ·
[polyperfect.com](https://www.polyperfect.com/low-poly-ultimate-pack) ·
[quaternius.com — Medieval Village MegaKit](https://quaternius.com/packs/medievalvillagemegakit.html) ·
[quaternius.itch.io](https://quaternius.itch.io/medieval-village-megakit) ·
[Godot Asset Store listing](https://store.godotengine.org/asset/quaternius/medieval-village-megakit/)

---

# Part D — Opportunities + gaps

**1. Animate the village (highest leverage, zero new spend).** The §P3 system means 240
ready-rigged villagers with idle/walk/run/sit clips are sitting unused. An editor-bake
"VillagerInjector" (instantiate Rig prefabs, assign CTL_People_* or one shared 2-state
controller, add AnimationDelay/Offset) would make MainCastle_Hall and Village2 feel alive.
Medieval-safe skins are already shortlisted (knight, monk, lord, farmer pair, servant).
Fits the ten-year-old test directly.

**2. Prop animation for free.** `CTL_Rotation_Y_360_3s_Loop` on the `Windmill_Medieval`
blades and `Watermill_Medieval` wheel — the buildings we already place are static today.

**3. ClaimableCamp Quaternius stub → real art.** `ClaimableCamp.cs:135` spawns labeled
cubes. The harvest pipeline (`Village2Build.HarvestQuaterniusBuildings`) already produces
HouseA/HouseC/HouseD/KitTower prefabs — routing those (or Wall/Prop pieces) into the camp
spawn is a small, contained WO.

**4. Enterable buildings.** Quaternius walls model interiors; the 48 Window-Door modules
are unused. Combined with the WO-584 dungeon/outpost/arena primitive and the WO-479
chunk-composer north star, the MegaKit can compose *interior* spaces, not just facades.

**5. Dungeon dressing without new packs.** polyperfect `Fantasy_M` has a complete dungeon
kit (walls/floors/pillars/doors/prison/torture props — catalog §5) and Quaternius' free
Fantasy Props MegaKit (CC0) layers furniture/weapons on top.

**6. Farm/ambient life.** `Animals_M` (28) is catalog-listed but unplaced: Cow/Hen/Pig/
Sheep_White around the Farm, Dog in the village, Deer in the OuterWorld tree ring. Note the
animals in THIS pack are static meshes — the vendor's Animated Animals pack is the animated
version (not owned).

**7. Player-defined-map pivot synergy (WO-673).** The Build-mode grid is already 3 m to
match polyperfect walls (`PlacementGrid.cs:36`), and `RotationCorrectionRegistry` already
normalizes pack yaws — both packs are pre-adapted to player-placed structures. New
placeable-structure LOOKs should keep coming from `Medieval_M` + mirrored
`Resources/Structures` prefabs (the runtime-loadable set).

**8. Correct the catalog's tier description** (§P2): `_T` = atlas-texture tier of all 41
categories, not "Tribal"; `_H`/`_L` don't exist in v10. One-line fix in
`docs/polyperfect-asset-catalog.md` + `docs/POLYPERFECT_NOTES.md` next time canon is touched.

**Known pitfalls recap:** GUID-pinned generators break silently-ish on a vendor GUID
regeneration (watch their warnings after any future pack update); the `Assets/Medieval
Village/FBX/` duplicate tempts agents into referencing raw FBX (don't); Quaternius pivots
float/sink pieces when hand-placed; polyperfect People_M static prefabs look like NPCs but
cannot animate — use Rigs_M.

---

# Part E — Fresh-clone reimport pitfalls (both packs)

Both packs are **gitignored** and therefore absent on a fresh clone. Builders are written to
degrade gracefully — `Debug.LogWarning` + skip, never error (CLAUDE.md §4) — so a clone
*compiles and bakes* but produces bald scenes: no wall perimeter, arch-only gates, missing
barracks/bridge/hedge, primitive fish, no Village2 beauty pieces.

Restore procedure:
1. **polyperfect:** import "Low Poly Ultimate Pack" v10.0 from the owner's Asset Store
   account (or copy `Assets/polyperfect/` from an existing machine — the historical path was
   `Documents\defenders-unity`, current home `C:\eoa`). Then **immediately run
   Defenders ▸ Art ▸ Fix Polyperfect URP Materials** (or batchmode
   `-executeMethod DeNelle.Editor.PolyperfectUrpFix.Fix`) — otherwise everything under the
   pack renders on the magenta/pink error shader and trees render flat white.
2. **Quaternius:** copy `Assets/Quaternius/` in (or re-download the Medieval Village MegaKit
   source version from quaternius.com/itch.io — CC0). **No material fix needed** (URP-native).
3. Re-run the affected bakes (VillageSceneBuilder / CastleHubBuilder / Village2 harvest) if
   scene content was baked while the packs were absent.
4. WebGL builds: `TextureBatchOptimizer` (WO-408) and `WebGLTextureShrink` re-apply the
   512 px caps to the freshly imported textures — run before a size-sensitive Vercel deploy
   (100 MB/file cap).

---

# Part F — Executive summary

We own two complementary environment-art packs, both kept out of git for size and both
already deeply wired into the scene-baking pipeline.

The polyperfect Low Poly Ultimate Pack (v10.0, Asset Store, purchased May 2026) is the
project's structural skeleton: roughly 3,080 mobile-tier prefabs across 41 themed
categories, all sharing a single texture atlas so the entire pack draws efficiently and
weighs less than one of the old Tripo buildings it replaced. Our village walls, towers,
gates, houses, the castle-hub perimeter, the moat hedge, the barracks, and the garrison
outposts all come from it, resolved by asset path at bake time through the editor builders,
with a mirror-into-Resources step for anything that must load at runtime. The pack ships
with materials written for Unity's older built-in renderer; on our render pipeline those
show up as the loud error-shader fallback until the one-menu fix
(Defenders ▸ Art ▸ Fix Polyperfect URP Materials) converts them in place — that fix is
mandatory after every re-import and is idempotent. Two findings from this audit matter
going forward. First, the pack contains a complete, unused animation system: 240 rigged
villager variants sharing one skeleton, five looping clips (idle, walk, run, sit, dead),
ready-made controllers, and two small helper scripts that stagger and de-phase crowds —
enough to populate our towns with living NPCs without buying anything or building a rig.
Second, the version-10 rename risk the coordinator flagged is retired: we installed v10
after its cleanup, our catalog was verified against that same disk, and I re-checked every
prefab name the builders reference — all resolve.

The Quaternius Medieval Village MegaKit (free, CC0, from quaternius.com) is the beauty and
modularity layer: 304 grid-snapping wall, roof, door-window, and prop modules with
double-sided walls that support enterable interiors. Unlike polyperfect it is already
native to our render pipeline — no material fix ever needed — which is exactly why the
Village2 town generator was built on it. Its two traps are per-piece pivots (place through
the generator, never by hand) and separate collision meshes nobody has wired yet. The
enemy-camp code that claims to use Quaternius props is still a placeholder spawning plain
cubes — a cheap, contained upgrade. Because everything Quaternius publishes is CC0, their
companion kits (notably the Fantasy Props MegaKit for dungeon interiors and their nature
packs) are free, zero-license additions whenever we need more dressing.

On a fresh clone both packs are simply absent: the project still compiles and bakes, but
scenes come out bald. The restore is copy-or-reimport, run the polyperfect material fix,
and re-bake — documented step by step in Part E.
