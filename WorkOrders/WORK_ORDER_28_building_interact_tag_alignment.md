# WORK ORDER 28 — Building Interact Tag Alignment (Pet House / Farm / Arcane Tower)

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: old-village building tags; Village.unity deleted, buildings re-owned by the player-built town model)
**Date:** 2026-05-26
**Author:** Bug triage — playtest screenshots
**Priority:** High — three core gameplay buildings are non-interactable / invisible

---

## Problem

The `[F]` interact prompts for three buildings appear at the correct world coordinates
but **no visible building mesh is present** at those positions. The hero walks into
empty space, sees a floating prompt, but there is nothing to look at.

Observed in screenshots:

| Building | Coord in builder | What player sees at prompt |
|---|---|---|
| Pet House | `(-17, 0, -10.5)` | Prompt floats in empty village green |
| Farm | `(19, 0, -1)` | Prompt appears near the Folk's Granary ruins column |
| Arcane Tower | `(6, 0, -12.5)` | Prompt appears near the Elarion world-tree |

The `BuildingInteractable` colliders and `BoxCollider` footprints are working correctly —
the bug is that the **CustomFbx visual child meshes are invisible or not rendering**.

---

## Root Cause Diagnosis

`VillageSceneBuilder.BuildBuildings()` instantiates each custom building mesh and applies
materials via two paths:

**Path A — Single-texture buildings (Farm, Arcane Tower):**
```csharp
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(b.BaseColorTex);
var lit = Shader.Find("Universal Render Pipeline/Lit");
if (tex != null && lit != null) { /* apply material */ }
else { Debug.LogWarning(...); }   // <-- leaves Tripo patchwork materials
```
If `AssetDatabase.LoadAssetAtPath` returns `null` at bake time (e.g. import not yet
finalised, path case mismatch), the building keeps its raw Tripo-extracted URP
materials — which render as vertex-colour rainbow patchwork or flat grey, both of
which can appear almost invisible against the green terrain.

**Path B — Multi-part buildings (Pet House):**
```csharp
var bFixer = FindType("DeNelle.Core.TripoMaterialFixer");
if (bFixer != null) {
    var fx = visual.AddComponent(bFixer);
    bFixer.GetMethod("ForceRebuildAll")?.Invoke(fx, null);
}
```
`TripoMaterialFixer.Run()` fires in `Start()` (runtime). It depends on the FBX
sub-mesh materials having a valid `_MainTex` / `_BaseMap` at runtime — if those
texture references weren't serialised into the scene properly the fixer rebuilds
each material as URP/Lit with `col = Color.white` and **no texture**, producing
a white-to-near-invisible mesh depending on lighting.

In all three cases the **Building GameObject, its BoxCollider, and BuildingInteractable
are at the correct position** — only the rendered visual is wrong.

---

## Fix

### 1. Pet House — `PetHome.fbx` (multi-part, no `BaseColorTex`)

In `VillageSceneBuilder.Buildings[]`, the Pet House entry has no `BaseColorTex`.
The 27-part PetHome model has pre-extracted material `.mat` files in
`Assets/Art/TripoStructures/Materials/PetHome_tripo_part_*_basecolor.mat`.

**Option A (preferred):** Assign a representative single basecolor texture the same
way Farm/Tower do. PetHome's part_0 basecolor is the dominant surface. Add:
```csharp
BaseColorTex = "Assets/Resources/Structures/PetHome_basecolor.JPEG",
```
That file already exists at `Assets/Resources/Structures/PetHome_basecolor.JPEG`.

**Option B (fallback):** In the `TripoMaterialFixer` path, additionally call
`SetFallbackTexture("Structures/PetHome_basecolor")` on the fixer component so it
can find the texture at runtime even if the FBX sub-mesh refs are missing:
```csharp
fxType.GetMethod("SetFallbackTexture")?.Invoke(fx, new object[] { "Structures/PetHome_basecolor" });
fxType.GetMethod("SetFallbackTint")?.Invoke(fx, new object[] { new Color(0.98f, 0.82f, 0.48f) });
fxType.GetMethod("ForceRebuildAll")?.Invoke(fx, null);
```

### 2. Farm — `Farm.fbx` (single texture path)

Verify `AssetDatabase.LoadAssetAtPath<Texture2D>` succeeds for:
```
Assets/Art/TripoStructures/Farm.fbm/farm_basecolor.JPEG
```
File exists on disk. If the warning fires at bake time, it means the FBX import
has not been imported before the builder runs. Fix: call
`AssetDatabase.ImportAsset(b.BaseColorTex)` before the `LoadAssetAtPath` call:
```csharp
AssetDatabase.ImportAsset(b.BaseColorTex, ImportAssetOptions.ForceSynchronousImport);
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(b.BaseColorTex);
```
Apply to all `BaseColorTex` entries in the builder (Farm, Arcane Tower, Crystal Mine,
Workshop).

### 3. Arcane Tower — `BuildTower.fbx` (single texture path)

Same fix as Farm. Verify:
```
Assets/Art/TripoStructures/BuildTower.fbm/build_tower_basecolor.JPEG
```
Exists on disk (confirmed). Apply `ImportAsset` guard before `LoadAssetAtPath`.

### 4. Console error: "Can't remove Light because UniversalAdditionalLightData depends on it"

Appearing 3–5× per scene load in every playtest screenshot. Some builder is calling
`DestroyImmediate` on a `Light` component directly (not via `StripColliders`, which
only touches `Collider`). Locate the call site and replace with the safe pattern:
```csharp
// Safe: destroy the dependent component first, then the Light
var lightData = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
if (lightData != null) DestroyImmediate(lightData);
var light = go.GetComponent<Light>();
if (light != null) DestroyImmediate(light);
```

---

## Files to Edit

- `Assets/Editor/VillageSceneBuilder.cs`
  - `BuildBuildings()` — add `ImportAsset` guard (§Fix 2 / §Fix 3)
  - Pet House entry in `Buildings[]` — add `BaseColorTex` OR update fixer wiring (§Fix 1)
  - Locate the `DestroyImmediate(light)` call → apply safe pattern (§Fix 4)

---

## Acceptance Criteria

- [ ] Hero walks to `(-17, 0, -10.5)` and sees a Pet House building mesh **and** the `[F] Pet House` prompt
- [ ] Hero walks to `(19, 0, -1)` and sees a Farm building mesh **and** the `[F] Farm` prompt
- [ ] Hero walks to `(6, 0, -12.5)` and sees a Tower building mesh **and** the `[F] Tower` prompt
- [ ] No "Can't remove Light" errors in the Development Console on scene load
- [ ] Re-run `VillageSceneBuilder` (Defenders > Week 3 > Build Village Scene) to verify — **owner-gated re-bake required after code changes**
