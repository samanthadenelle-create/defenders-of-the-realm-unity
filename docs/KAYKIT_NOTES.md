# KayKit Asset Packs — Quick Notes

Low-poly stylized art that supplies most village buildings, dungeon kits, heroes, and
wave enemies. Root: `Assets/Models/KayKit/`.
Sources read: `Assets/Editor/KayKitMaterials.cs`, the per-pack ReadMe.txt files,
and the on-disk folder tree (2026-06-05).

> **For the full creative pick-list (every pack, per-building/hero/enemy mapping,
> boss table, magical-layer props) see `docs/kaykit-asset-catalog.md`.** This file
> is the *technical* companion — rig, animation, material, and path facts only.

## Packs present (21 packs + curated live-set folders)
Adventurers 2.0 (heroes), Skeletons 1.1 (wave enemies), Character Animations 1.1
(shared anim library), Medieval Hexagon Pack 1.0.1 (village buildings), Dungeon
Remastered 1.1 (dungeon interiors), Forest Nature Pack, Mystery Monthly Series 4 & 5
(~31 rigged bonus characters), plus Bits packs (Block, Board Game, City Builder,
Fantasy Weapons, Furniture, Halloween, Holiday, Platformer, Prototype, Resource,
Restaurant, RPG Tools, Space Base).

## Key paths
- **Always import from `fbx(unity)/`** inside each pack (KayKit pre-baked Unity import
  settings), e.g. `KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/`.
- **Curated `.glb` "live set"** (the wired-in subset) lives at the KayKit root:
  `Assets/Models/KayKit/{characters,enemies,medieval,dungeon,weapons,anim}/`.
  **These were EMPTY on disk at audit time** — the `.glb` subset is gitignored /
  not present on this checkout. Treat the full packs as the warehouse.
- **Runtime enemy/hero controllers** are copied into `Assets/Resources/Enemies/`
  (e.g. `HumanoidEnemy`, `LargeEnemy`, `Boss`, `Dragon`, `OrcWarband`) by the editor
  AnimatorSetup so they can be `Resources.Load`ed at runtime.

## Shared rig + animation (the load-bearing fact)
- Every KayKit humanoid (Adventurers, Skeletons, all Mystery Monthly chars) shares
  ONE skeleton: **`Rig_Medium`** (standard) or **`Rig_Large`** (golems/bruisers).
  So **one retargeted controller drives the whole cast** — build it once per rig.
- Animation library: `KayKit Character Animations 1.1/Animations/fbx/Rig_Medium/`
  (and `Rig_Large/`). Each clip *set* is ONE multi-take FBX:
  `Rig_Medium_General`, `_MovementBasic`, `_MovementAdvanced`, `_CombatMelee`,
  `_CombatRanged`, `_Special` (casts/channels — Mage abilities), `_Simulation`,
  `_Tools` (mining/harvest). Mannequins for preview: `Mannequin Character/characters/
  Mannequin_Medium.fbx` / `Mannequin_Large.fbx`.

## How to use from code
- `DeNelle.Village.EnemyAnimatorFactory.Apply(GameObject visual, string modelName)`
  (`Assets/_Modules/Village/PatriciaLight/EnemyAnimatorFactory.cs`) — resolves the rig
  family by model name and stamps the matching shared controller from
  `Resources/Enemies/`. `applyRootMotion = false` (NavMesh/glide drives position).
  No-op-safe if the controller asset is missing.
- `EnemyRig` enum: `HumanoidMedium / HumanoidLarge / Boss / Dragon / OrcWarband`.
  NOTE the orc family is a separate **Tripo Humanoid** rig (DEF-221) — it canNOT use
  the KayKit Generic controller, hence its own `OrcWarband` controller.

## Material / URP gotcha
- KayKit ships models with **no `.mat` files**. On URP import they come out **white**
  (no `_BaseMap`) or **magenta** (Built-in Standard shader). Each pack ships ONE
  shared palette atlas beside the FBX (`hexagons_medieval.png`, `*_texture.png`,
  `dungeon_texture.png`).
- **Fix:** `Tools ▸ DeNelle ▸ Fix KayKit Materials` (menu) or batch
  `-executeMethod DeNelle.Editor.KayKitMaterials.FixAllMaterials`
  (`Assets/Editor/KayKitMaterials.cs`). It builds ONE `URP/Lit` `.mat` per atlas
  (smoothness 0, metallic 0, opaque) and remaps every FBX importer to it. Idempotent.
  Never picks the seasonal `_Fall/_Summer/_Winter` atlas variants.
- A `AssetImportPostprocessor.OnAssignMaterialModel` hook makes FUTURE imports correct
  automatically; `KayKitMaterials` repairs models imported before the hook existed.

## Doc sources
- `docs/kaykit-asset-catalog.md` (the creative catalog)
- `Assets/Editor/KayKitMaterials.cs` (the URP repair logic)
- Per-pack `ReadMe.txt` (e.g. `KayKit Board Game Bits 1.0/Assets/ReadMe.txt`)
- Vendor: https://kaylousberg.itch.io / https://kaylousberg.com
