# WO-1251 RESULT — Crystal Mine null material slot

**Status:** implemented (not committed; Unity gate NOT run — silo forbade batchmode)

## Slot identified

- Asset: `Assets/StructureContent/CrystalMine.fbx` (Addressables `Structures/CrystalMine`, guid `3c5d0584b7cd64649a972c3851bd1617`).
- Renderer: `'CrystalMine'` (imported root named after the FBX).
- Mesh: 1 (`tripo_mesh_429c9bff`) · FBX material: **`tripo_mat_429c9bff`** · 1 `LayerElementMaterial` → **slot 0** is the only slot.
- Importer was `externalObjects: {}`, `materialName: 0` (BasedOnTextureName), `materialSearch: 1`.
- Texture token Unity searches for: **`gem_mine_3d_model_basecolor`**. Textures already lived in `CrystalMine_Textures/` (albedo guid `f2473d16b32fc394584b60da68dc5a27`). **No matching `.mat` existed.**

## Authoring vs Addressables resolution

**Authoring.** The Addressables entry points at the FBX itself; `DependencyClosureTrace` ran on a **loaded** prefab and found `sharedMaterials[0] == null`. A missing/unpushed bundle would fail the resolve (capsule/placeholder), not leave a named renderer with an empty slot. The FBX Phong binds textures by a Tripo Linux absolute path (`/mnt/pfs/server/tripo-studio/.../gem_mine_3d_model_basecolor.JPEG`) that is not in this repo; Unity's BasedOnTextureName search then looks for `gem_mine_3d_model_basecolor.mat`, which was absent, so slot 0 stayed null.

Fix: authored URP/Lit `Materials/gem_mine_3d_model_basecolor.mat` (same pattern as HealingCaravan's `medieval_wagon_3d_model_basecolor.mat`) and remapped **both** importer identifiers (`gem_mine_3d_model_basecolor` and `tripo_mat_429c9bff`) onto it. Renderer was not hidden. Addressables grouper was not run.

## Sweep (by shared token, not name)

Token = `*_basecolor` texture + `materialName: 0` search, plus the `_Textures/` + `tripo_mat_*` Tripo pipeline.

- **CrystalMine** — unique miss: `gem_mine_3d_model_basecolor.jpg` had no `.mat`. **Fixed.**
- **HealingCaravan / Wagon** — same pipeline (`tripo_mat_21bc9f49`, `medieval_wagon_3d_model_basecolor`); mat already present. No null-slot sibling.
- Every other `*_basecolor` texture under `StructureContent` already has a matching `Materials/*_basecolor.mat` (Ballista L1–L3, Armorer, Jeweler, Forge/WeaponSmith, RealmStore, ShopAndCrafting, TreeofLife, wooden watchtower parts).
- **IronMine** is KayKit hexagons (`hexagons_medieval`), not this token.

No other StructureContent materials were edited.

## Files changed

- `Assets/StructureContent/Materials/gem_mine_3d_model_basecolor.mat` (+ `.meta`, guid `9e4c1b7a2d8f4503b6c91e0a5d4f72b8`)
- `Assets/StructureContent/CrystalMine.fbx.meta` (externalObjects remap)
- `Assets/Editor/Regression/StructureNullMaterialSlotRegression.cs` (+ `.meta`)
- `Assets/Editor/Regression/DataRegression.cs` (registration `[structure-null-slot]` above the END fence)

## Oracle

General form: every catalog `visualPrefabPath` / `upgradeVisualPath` plus every GameObject in Addressables group `Structure_Art` — MeshRenderer/SkinnedMeshRenderer with a mesh must have a non-null slot covering every submesh.

- Empty/unenumerable set → `RegressionOutcome.Skip` (never quiet green).
- Individual unresolved keys → `PartialSkip`.
- RED conceptually: CrystalMine slot 0 with no mat **was** the red (seq 3618). Positive control: null any structure MeshRenderer slot → suite must fail naming asset/renderer/slot.

## Brace check

- `StructureNullMaterialSlotRegression.cs` — 34/34, no NULs
- `DataRegression.cs` — 963/963, no NULs

## Not done here (silo)

- No commit, no Unity batchmode, so no `COMPILE_GATE_OK` / `REGRESSION_OK` on a fresh log and no Crystal Mine screenshot. Next content build must pick up the FBX remap + new `.mat` (do **not** re-run the grouper) and `tools\r2-ship.ps1` before the device can show colour. Owner felt-verify after that.
