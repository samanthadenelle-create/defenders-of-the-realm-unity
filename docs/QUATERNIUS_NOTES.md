# Quaternius — Medieval Village MegaKit Notes

CC0 modular medieval building kit (URP-native) — the source art for the **village
factory / sister-city generator** (Village2). Root: `Assets/Quaternius/`.
Source: on-disk tree + project usage (2026-06-05).

## Present packs
Only one: **`Medieval Village MegaKit/`** (~600 source FBX, ~304 prefabs).

## Key paths
- **Prefabs (use these): `Medieval Village MegaKit/Modules/Prefabs/<Category>/`**
  e.g. `Modules/Prefabs/Prop/Balcony_Cross_Corner.prefab`,
  `.../Prop/HoleCover_Straight.prefab`. Categories under `Modules/Prefabs/`
  (Prop, walls, roofs, etc. — modular pieces, not whole buildings).
- Source meshes: `Modules/Source Models/<Name>.fbx`
  (+ `Modules/Source Models/Collisions/Collision_<Name>.fbx` — separate collision meshes).
- Materials (URP shadergraph-based): `Medieval Village MegaKit/Materials/`
  — `M_BaseMaterial.shadergraph`, `M_BaseWear.shadergraph`, `M_Leaves`, `M_Plaster`,
  `M_WindowGlass`, instances `MI_Brick`, `MI_Plaster`, `MI_WoodTrim`, etc., with
  PBR textures `T_*_BaseColor/Normal/Roughness.png`.
- Sample scene: `Medieval Village MegaKit/Levels/L_SampleScene_1.unity`.

> NOTE: `Assets/Medieval Village/FBX/` (top-level, separate folder) holds the SAME
> raw FBX set (`Balcony_Cross_Corner.fbx`, …) — i.e. the unzipped MegaKit source.
> Prefer the prefabs under `Assets/Quaternius/.../Modules/Prefabs/`.

## URP state
**Already URP-native** — materials are ShaderGraph (`*.shadergraph`). Unlike KayKit /
Spells / Lana / polyperfect, **no magenta-fix step needed.** This is exactly why the
village factory was built from this kit as the URP source.

## How the project harvests these into buildings
- `Assets/_Village2/Village2Generator.cs` + `Assets/Editor/Village2Build.cs` are the
  generator: they place these modular Quaternius pieces into a town around a centred
  Tree of Life (4 quadrants, modular walls + balcony ramparts).
- **Pivot gotcha (called out in the generator):** Quaternius kit pivots vary per piece,
  so raw placement makes pieces float/sink — the generator compensates per-prefab pivot.
  Use the generator's placement path, don't hand-place by transform.
- Architecture intent (project memory: village-factory): harvest the sample-scene
  buildings → prefabs → generate camps/cities via one factory (recipe → StructureFactory
  → BaseLayout), replayable/headless.

## Gotchas
- Use **prefabs**, not raw `Source Models/` FBX (prefabs carry the shadergraph material
  assignments + correct setup).
- Collision meshes are **separate** files under `Source Models/Collisions/` — wire them
  if you need accurate colliders rather than the render mesh.
- Per-piece pivot variance → place via the generator's pivot-correcting path.

## Doc sources
- On-disk: `Assets/Quaternius/Medieval Village MegaKit/`
- `Assets/_Village2/Village2Generator.cs`, `Assets/Editor/Village2Build.cs`
- Vendor: https://quaternius.com (CC0)
