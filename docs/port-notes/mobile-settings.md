# Mobile-settings port notes — P0 mobile-readiness fixes

**Date:** 2026-05-19
**Source spec:** `docs/audit/mobile-performance.md` (§1, §2.1)
**Scope:** the 6 P0 findings + the P1 URP render items the audit pairs with them.

This pass applies the audit's P0 mobile-readiness fixes as **source only** — the
integrator runs the editor entry point and a build to make them take effect.
Unity itself was not run while writing this.

---

## Files created / changed

| File | Status | Purpose |
|------|--------|---------|
| `Assets/Editor/MobileSettings.cs` | **new** | Editor script — applies P0-1 / P0-2 / P0-3 + the §1.6 URP tuning. |
| `Assets/Editor/VillageSceneBuilder.cs` | **changed** | P0-4 — `BatchingStatic` flags + GPU instancing on dressing materials. |
| `Assets/_Modules/Core/SeekerBootstrap.cs` | **new** | P0-6 — runtime `targetFrameRate` + Seeker device auto-detect. |

No `.asmdef`, `.meta`, or `ProjectSettings/*.asset` file was hand-edited.

---

## Entry point

```
-executeMethod DeNelle.Editor.MobileSettings.ApplyMobileSettings
```

Also on the menu: **Defenders > Setup > Apply Mobile Settings**.

`SeekerBootstrap` needs no entry point — it auto-runs at startup via
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.

> **Run order:** `MobileSettings.ApplyMobileSettings` depends on the URP asset
> existing at `Assets/Settings/DeNelle-URP.asset`. If it is missing, run
> **Defenders > Setup > Activate URP** (`UrpActivator`) first; `MobileSettings`
> logs an error and skips the URP/tier steps rather than crashing.

---

## P0 findings — how each was addressed

### P0-1 — Color space → Linear (audit §1.1)
`MobileSettings.ApplyColorSpace` sets `PlayerSettings.colorSpace = ColorSpace.Linear`.
**Integrator note:** this triggers a *full project asset reimport* — Unity will
churn for several minutes after `ApplyMobileSettings` runs. That is expected.
Every authored material/light should be re-checked once Linear is active
(emissive / lighting math changes), per the audit.

### P0-2 — Android scripting backend → IL2CPP + ARM64 (audit §1.2)
`MobileSettings.ApplyAndroidScriptingBackend` via the `NamedBuildTarget.Android`
PlayerSettings API:
- Scripting backend → **IL2CPP**.
- IL2CPP code generation → **OptimizeSize** ("Faster/smaller builds").
- C++ compiler configuration → **Release**.
- Managed stripping level → **Low** (audit says set one once IL2CPP is on).
- ARM64 — audit-verified already correct; the script *confirms* it and only
  rewrites if ARM64 is missing (it never weakens to add ARMv7).

### P0-3 — Quality tiers Seeker_Low / Seeker_High / Desktop (audit §1.4)
`MobileSettings.ApplyQualityTiers`:
- The QualitySettings level **array is resized to exactly 3** and the slots
  named, through the `QualitySettings` *SerializedObject* (`m_QualitySettings`)
  — Unity exposes no public add/remove-level API. This is the same
  serialization path the Quality inspector uses; it is **not** YAML hand-editing,
  and Unity re-serializes the asset correctly.
- Each tier's values are written through the runtime `QualitySettings` API
  (shadows, shadow distance/resolution, MSAA, anisotropic, pixel-light count,
  `vSyncCount = 0`).
- Tier values (audit §1.4 table):

  | Tier | Shadows | MSAA | Render scale | Target FPS |
  |------|---------|------|--------------|-----------|
  | Seeker_Low | hard only | off | 0.85 | 30 |
  | Seeker_High | soft | 2× | 1.0 | 60 |
  | Desktop | soft | 4× | 1.0 | 60 |

- Android default quality tier → **Seeker_High**, set via the
  `m_PerPlatformDefaultQuality` SerializedProperty on the QualitySettings asset.

### P0-4 — Static batching + GPU instancing in the village (audit §2.1)
`VillageSceneBuilder` — surgical edit. After `BakeVillageNavMesh` bakes the
NavMesh, the new `MarkStaticBatchingAndInstancing()`:
- **OR**s `StaticEditorFlags.BatchingStatic` onto every renderer under the
  static-geometry roots (Ground / Walls / Gates / Roads / Buildings /
  Centerpieces / CityDressing / Approaches). The OR **preserves** the
  `NavigationStatic` bit `BakeVillageNavMesh` already set — it does not clobber it.
  This is a superset of the nav-static roots: it additionally batches
  Centerpieces + CityDressing, which the NavMesh bake never touched.
- Enables `Material.enableInstancing` on the distinct shared materials under
  `CityDressing` (audit §2.1 recommendation 3 — repeated props/fences/trees).

### P0-5 — No playable build / integration pass
**Out of scope for this task** (it is the gameplay-integration work item, not a
config fix). Noted here only so the audit's P0 list is fully accounted for.

### P0-6 — `targetFrameRate` + Seeker auto-detect (audit §1.5)
`Assets/_Modules/Core/SeekerBootstrap.cs` (in `DeNelle.Core`):
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` — runs before the Title
  scene loads, no scene wiring needed.
- Reads `SystemInfo.deviceModel`; `LooksLikeSeeker` matches
  seeker/solana/osom/saga (loose, case-insensitive).
- Selects a tier: Seeker / capable Android → `Seeker_High`; weak Android →
  `Seeker_Low`; desktop → `Desktop`.
- Sets `QualitySettings.vSyncCount = 0` so `Application.targetFrameRate` is
  authoritative, then `Application.targetFrameRate` to the tier target (30/60).
- `ApplyTier` is public + idempotent so a future settings screen can re-invoke
  it when the player changes tier.

---

## §1.6 URP-asset tuning (P1 render items)

`MobileSettings.ApplyUrpMobileTuning` edits `Assets/Settings/DeNelle-URP.asset`
via its SerializedObject. The shared asset is tuned to the **Seeker_High**
profile (the Android default tier):

| URP field | Before | After | Audit |
|-----------|--------|-------|-------|
| `m_SupportsHDR` | on | **off** | §1.6 P1 |
| `m_MSAA` | 1 (off) | **2** | §1.6 P2 |
| `m_RenderScale` | 1.0 | 1.0 | (unchanged) |
| `m_MainLightShadowmapResolution` | 2048 | **1024** | §1.6 P1 |
| `m_ShadowDistance` | 50 | **30** | §1.6 P1 |
| `m_SoftShadowsSupported` | off | **on** | §1.6 P2 |
| `m_SoftShadowQuality` | 2 (High) | **0 (Low)** | §1.6 P2 |
| `m_IntermediateTextureMode` | 1 (Always) | **0 (Auto)** | §1.6 P1 / Risk 7 |

---

## Known follow-ups (deliberately not done here)

1. **Per-tier URP asset variants.** The audit §1.4/§1.6 ideal is one URP asset
   *per* quality tier (Seeker_Low at render scale 0.85 + MSAA off, Desktop at
   MSAA 4× + HDR on). The project ships a single shared `DeNelle-URP.asset`, so
   `MobileSettings` tunes that one asset to the Seeker_High profile and every
   tier points at it. Creating two more URP-asset variants and pointing
   Seeker_Low / Desktop at their own is the clean next step.
2. **GPU instancing vs per-instance `ApplyColor`.** `VillageSceneBuilder.ApplyColor`
   recolours instances by assigning a fresh material, which fragments instancing
   batches. `enableInstancing` is flipped on the dressing materials, but to
   actually get instanced draws the per-tile tint should move to a
   `MaterialPropertyBlock` instanced property or a small set of pre-tinted
   shared materials.
3. **Best fix for the 2,607 ground tiles** is still a combined mesh
   (`StaticBatchingUtility.Combine` / a mesh-merge pass), per audit §2.1
   recommendation 1 — `BatchingStatic` is recommendation 2, the floor not the
   ceiling.
4. **Adaptive Performance provider** wiring (audit §1.5) — the module is in the
   manifest and `m_UseAdaptivePerformance` is on, but no provider is wired.
5. P1 items not in this task's scope: Min SDK → 33, orientation lock, default
   texture compression = ASTC, GPU skinning, terrain tree LOD/distance.

---

## What the integrator must verify

1. Run **Defenders > Setup > Activate URP** if `DeNelle-URP.asset` is absent,
   then **Defenders > Setup > Apply Mobile Settings** (or the `-executeMethod`).
2. Let the **Linear color-space reimport** finish — expect several minutes of
   churn; this is normal.
3. Project Settings > Player (Android): confirm **Color Space = Linear**,
   **Scripting Backend = IL2CPP**, **ARM64** ticked, code generation = Faster,
   C++ config = Release, managed stripping = Low.
4. Project Settings > Quality: confirm exactly **three tiers**
   `Seeker_Low / Seeker_High / Desktop`, Android default = **Seeker_High**.
   (Note: if the project had more than 3 stock tiers, the array is truncated to
   3 — confirm nothing else referenced a removed tier.)
5. Inspect `DeNelle-URP.asset` against the §1.6 table above.
6. Re-run **VillageSceneBuilder.BuildVillage**; in the generated scene confirm
   ground/wall/building objects show **both** `Batching Static` and
   `Navigation Static` ticked. Use the Frame Debugger to confirm ground tiles
   now batch.
7. Do an **IL2CPP/ARM64 Android build** — confirm it compiles and the `.apk`
   builds (the audit's Week-8 build gate).
8. On device/emulator, check the console for the `[SeekerBootstrap]` log line —
   confirm the selected tier and `targetFrameRate` match the hardware.
