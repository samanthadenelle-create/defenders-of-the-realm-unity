# WORK ORDER 85 — World Atmosphere & Immersion Pass (Phase 3)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Large — terrain, lighting, WeatherManager, foliage, performance pass
**Depends on:** WO-52 (WeatherManager), WO-55 (TorchFireController), WO-54 (LOD), WO-51 (AnimatorCullingController)

> **Extends WO-71** with additional details on wave-reactive weather, specific
> foliage counts, and WeatherManager hookup to WaveManager. Implement
> WO-71 first, then layer these additions on top.

---

## Goal

The world must feel worth defending. Replace the flat test-map with a living,
breathing village — warm lighting, rolling hills, foliage clusters, dynamic
torches, and weather that reacts to wave intensity. 60 FPS on mid-range mobile
throughout.

---

## 1. Terrain & Ground Polish (extends WO-71 §1)

Follow WO-71 §1 for the base terrain setup (200 × 200, height 30, three texture
layers). Add these extra steps:

- **Path texture:** Paint a narrow dirt/gravel strip along each enemy lane using
  Layer 1 (dirt). Path width ≈ 4–5 units — just enough to be readable.
- **Subtle undulation inside paths:** Use Raise/Lower Terrain at 2–3 unit height
  variation under open ground (not the paths themselves) so the ground never
  looks perfectly flat even when zoomed out.
- **Castle courtyard:** Flatten to exactly height 0 inside the castle perimeter.

---

## 2. Lighting & Sky (extends WO-71 §3)

### Sun

```
Name:              Sun
Rotation X/Y/Z:    40 / 35 / 0
Intensity:         1.1 – 1.3 (start at 1.2, tune in scene)
Color:             Warm amber (#FFF0D0) for daytime feel
Shadow Type:       Soft Shadows
Shadow Resolution: Low (mobile) → Medium (PC)
Shadow Distance:   60 m
```

### Skybox

Use a two-colour procedural shader or the built-in gradient sky from URP:

```
Sky:      Deep blue-purple  (#2B1A4F)
Horizon:  Warm peach-orange (#E89060)
Ground:   Dark olive         (#1A1A0A)
```

### URP Global Volume — add these overrides

| Override | Setting |
|---|---|
| Bloom | Intensity 1.2, Threshold 0.8, Scatter 0.7 |
| Vignette | Intensity 0.25, Smoothness 0.4 (subtle) |
| Color Adjustments | Post Exposure +0.1, Saturation +12 |
| Fog (Linear) | Start 80 m, End 180 m, Color #C8A0B8 |

### Ambient occlusion

Enable SSAO in URP renderer settings — strength 0.4, radius 0.25. Cheap and
adds huge depth to the village.

---

## 3. WeatherManager Integration + Wave-Reactive Weather

After placing `WeatherManager` at scene root (WO-52), hook it into `WaveManager`:

```csharp
// In WaveManager.cs — add fields:
[Header("Weather Reactions")]
public WeatherManager weatherManager;
public float          bigWaveRainIntensity = 0.6f;

// When a "big" wave starts (e.g. multiples of 5):
public void OnWaveStart(int waveNumber)
{
    if (waveNumber % 5 == 0 && weatherManager != null)
    {
        weatherManager.SetRain(bigWaveRainIntensity);
        weatherManager.SetWind(0.4f);
    }
}

// When wave ends:
public void OnWaveComplete(int waveNumber)
{
    weatherManager?.SetRain(0f);
    weatherManager?.SetWind(0.1f);
}
```

Shooting stars should fire 2–4 times during the calm period between waves.
Wire `WeatherManager.TriggerShootingStar()` on a random 15–30 s timer in the
inter-wave phase.

---

## 4. Foliage & Details (using Lana Studio / Casual RPG VFX assets)

Placement rules from WO-71 apply. Additional guidance:

### Tree clusters (target: 24–32 trees total)

```
4 tight clusters (5–7 trees each) at map corners
2 mid-edge clusters (3–4 trees) on the long sides
No trees within 3 units of any NavMesh path
Vary Y-rotation 0–359° and scale 0.75–1.25× on every instance
```

### Rocks (target: 18–24 total)

```
Small groups of 2–3 rocks together — not singletons scattered randomly
Good positions: base of hills, outer path edges, castle perimeter gaps
```

### Shrubs / bushes (target: 35–45 total)

```
Line the path edges just outside the clear lane (keep lane itself clear)
A few scattered in open ground between lanes
```

### Torches (target: 10–16 total)

```
4 at castle gate and corners
2–4 along each enemy lane (evenly spaced)
Add TorchFireController (WO-55) to each
```

---

## 5. Performance Polish

### LOD Groups (WO-54) — re-verify all items placed in §4

| Asset type | LOD0 dist | LOD1 dist | LOD2 dist |
|---|---|---|---|
| Trees | 0–15 m | 15–35 m | 35 m+ (billboard) |
| Rocks | 0–18 m | 18–40 m | Culled |
| Buildings | 0–20 m | 20–45 m | 45 m+ |

### Occlusion Culling bake

1. Mark all terrain, trees, rocks, and buildings as **Occluder Static** +
   **Occludee Static**.
2. `Window → Rendering → Occlusion Culling → Bake`.
3. Smallest Occluder: 2 m. Re-bake after any geometry change.

### Invisible boundary walls

Four thin `BoxCollider` GameObjects around the perimeter, Layer `Boundary`,
Tag `Wall`. Dimensions: ~0.5 × 10 × 200 units (height × width × length).
Place just outside the terrain edge.

### `AnimatorCullingController` audit

Verify every animated object in the scene has `AnimatorCullingController`
attached (WO-53). A quick script to find missing ones:

```csharp
// Assets/Editor/AnimatorCullingAuditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AnimatorCullingAuditor
{
    [MenuItem("Defenders/Audit – Missing AnimatorCullingController")]
    static void Audit()
    {
        var animators = Object.FindObjectsOfType<Animator>();
        int missing = 0;
        foreach (var a in animators)
        {
            if (a.GetComponent<AnimatorCullingController>() == null)
            {
                Debug.LogWarning($"Missing AnimatorCullingController: {a.gameObject.name}", a.gameObject);
                missing++;
            }
        }
        Debug.Log($"Audit complete — {missing} missing.");
    }
}
#endif
```

### Ambient dust mote particles (2–3 systems in village centre)

```
Emission rate:   4 / s
Particle size:   0.05 – 0.12
Lifetime:        7 – 12 s
Velocity:        Y: +0.1 (gentle drift)
Color:           White / warm gold, alpha 0.20 – 0.30
Renderer mode:   Billboard
Culling mode:    Automatic (Pause and Catch-up)
```

---

## Files to Create / Edit

| Element | Action |
|---|---|
| Terrain | **Create** (per WO-71 §1) with added path texture and undulation |
| Directional Light "Sun" | **Edit** — update rotation, color, shadow settings |
| Global URP Volume | **Edit** — add/tune Bloom, Vignette, Color Adjustments, Fog |
| `WaveManager.cs` | **Edit** — add weather reaction hooks |
| Foliage, rocks, torches in scene | **Place** per §4 counts and rules |
| All tree/rock/building prefabs | **Edit** — confirm LOD Groups per §5 table |
| Occlusion Culling | **Bake** after all geo is placed |
| 4× boundary wall BoxColliders | **Create** at scene perimeter |
| 2–3 dust particle systems | **Create** in village centre |
| `Assets/Editor/AnimatorCullingAuditor.cs` | **Create** — run once to find missing controllers |

---

## Acceptance Criteria

- [ ] Flat teal plane is gone — terrain with 3 texture layers is visible
- [ ] Paths have distinct dirt texture; outer edges have gentle 4–8 unit rises
- [ ] Sun at ~40°/35°, warm amber tint, soft shadows
- [ ] Fog visible starting at ~80 m, fully opaque by ~180 m
- [ ] SSAO adds subtle depth to buildings and foliage
- [ ] 24+ trees placed in clusters, no two identical in scale/rotation
- [ ] 18+ rocks and 35+ shrubs placed, clear of all NavMesh paths
- [ ] 10+ torches with TorchFireController active
- [ ] Rain + wind increase on every 5th wave; clear after wave ends
- [ ] Shooting stars appear 2–4× per inter-wave calm period
- [ ] Occlusion culling baked and active (confirm via Scene → Debug → Overdraw)
- [ ] All trees and buildings have correct 3-level LOD Groups
- [ ] AnimatorCullingAuditor reports 0 missing controllers
- [ ] Hero cannot walk past boundary walls
- [ ] 60 FPS on mid-range mobile during a 10-enemy wave with full VFX + weather
