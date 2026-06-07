# polyperfect — Low Poly Ultimate Pack Notes

A huge single-atlas low-poly art pack (buildings, walls, nature, props, animals)
that replaced the heavy Tripo village meshes. Root:
`Assets/polyperfect/Low Poly Ultimate Pack/`.
Source: `README_LowPolyUltimatePack.pdf` (could not be auto-extracted in this env)
+ on-disk tree + project usage (2026-06-05).

> **For the actual pick-list (every wall/tower/building/prop FBX name → use) see
> `docs/polyperfect-asset-catalog.md`.** This file is the technical companion only.

## Present on disk?
**Present on THIS checkout** (`Assets/polyperfect/Low Poly Ultimate Pack/` has
`_M/`, `Common/`, etc.). But it is **gitignored** (CLAUDE.md §4, ~246 MB) and
**absent on fresh clones** — re-import from the Asset Store, then run the URP fix.
On a machine where it's missing, builders `Debug.LogWarning` (not error) and skip
the polyperfect pieces.

## Path conventions
- **Quality tier `_M` is the one to use** (mid/standard LOD).
  - Meshes: `_M/Meshes_M/<Category>_M/SM_<Name>.fbx`
  - **Prefabs (use these): `_M/Prefabs_M/<Category>_M/`**
  - Terrain: `Terrains/` and `_M/Meshes_M/Terrains_M/` + `Tiles_M/`
- All FBX are prefixed `SM_<Name>`. Whole pack shares **one atlas texture** →
  great draw-call batching (mobile/Seeker friendly).
- Real paths the builders use (verified):
  - `_M/Prefabs_M/Buildings_M/parts/Building Walls_M/Wall_Stone_3x3_A.prefab`
  - `PackRoot = Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/`
    (`PatriciaLightSceneBuilder.cs`, `VillageSceneBuilder.Walls.cs`).
- Modular stone walls snap on a **3 m / 3×3 grid** — `PlacementGrid.cs` cell size
  is 3 m specifically to match this.

## How it's used in the project
Mesh-only swap — no runtime code depends on it. `VillageSceneBuilder` /
`PatriciaLightSceneBuilder` reference the `_M` prefabs by path at bake time to build
the wall perimeter and dress the village; colliders are stripped where the builder
adds its own. Catalog the prefab names from `docs/polyperfect-asset-catalog.md`
before referencing one — don't guess `SM_` names.

## URP material fix (mandatory)
- Polyperfect ships **Built-in / Standard materials → grey/pink (magenta) under URP.**
- **Fix:** menu **`Defenders ▸ Art ▸ Fix Polyperfect URP Materials`**
  (`Assets/Editor/PolyperfectUrpFix.cs`) — converts ALL pack materials to URP.
  Run after any re-import. (A lighter per-asset healer is `TreeOfLifeMaterialFixer`.)

## Gotchas
- Re-import + URP-fix is the first step on any fresh clone, or the village renders
  pink and walls go missing.
- Use `_M` prefabs — not the `_L`/source tiers — for the agreed quality/perf tier.
- Builders are the single bottleneck for placement (CLAUDE.md §9) — change placement
  through `VillageSceneBuilder`, not by hand-editing the scene.

## Doc sources
- `docs/polyperfect-asset-catalog.md` (full FBX pick-list)
- `Assets/polyperfect/Low Poly Ultimate Pack/README_LowPolyUltimatePack.pdf`
- `Assets/Editor/PolyperfectUrpFix.cs` (the URP material conversion)
