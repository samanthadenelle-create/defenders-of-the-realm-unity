# WORK ORDER 71 — Complete World Implementation & Polish Pass

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Large — terrain, foliage, lighting, occlusion, NavMesh, boundaries
**Depends on:** WO-52 (WeatherManager), WO-55 (TorchFireController), WO-65 (PortalVFXController)

---

## Goal

Replace the flat teal-grid placeholder world with a proper, immersive,
mobile-optimised defense map. Gentle terrain, Lana Studio foliage, warm
lighting, fog, and occlusion culling all ship in this pass.

---

## 1. Replace flat plane with Terrain

1. **Delete** the current large flat plane GameObject.
2. `GameObject → 3D Object → Terrain`.
3. In **Terrain Settings**:
   - **Width / Length:** match your world footprint — typically 200 × 200 for
     this project (adjust to fit existing path layout).
   - **Height:** 30 (gives room for gentle hills at edges without clipping paths).
   - **Material:** assign a URP/Lit terrain material (or the default URP terrain
     shader with grass/dirt layers).
4. **Paint terrain layers** (Terrain Inspector → Paint Texture):
   - Layer 0: grass — use any green tiling texture from Lana Studio or free URP pack.
   - Layer 1: dirt/earth — paint along pathways and near the castle walls.
   - Layer 2: rock — paint on slopes and outer edge rises.
5. **Sculpt gentle hills** (Paint Height / Raise/Lower Terrain):
   - Outer border: raise to height 4–8 units to create a natural bowl.
   - Inside pathways and castle area: keep flat (height 0).
   - A few scattered 1–2 unit rises break up open ground.

---

## 2. Foliage & environment detail

Place using the Terrain tree painter or as scene GameObjects:

| Asset | Location | Count |
|---|---|---|
| Trees (cyan / Lana Studio) | Outer border clusters | 20–30 |
| Rocks / boulders | Scattered — not on paths | 15–20 |
| Small bushes / shrubs | Along path edges | 30–40 |
| Grass patches (particle or terrain detail) | Open ground | Dense |

Rules:
- Vary scale (0.7–1.3×) and Y-rotation on every placed asset — no two identical.
- No foliage on the NavMesh-baked pathways (blocks enemy pathing).
- Add 4–6 tight tree clusters at corners and midpoints to break up sightlines.

---

## 3. Lighting & sky

### Directional Light (Sun)

1. Select the existing Directional Light (or create one).
2. Set:
   - **Name:** `Sun`
   - **Rotation:** X = 45, Y = 30, Z = 0
   - **Intensity:** 1.2
   - **Shadow Type:** Soft Shadows
   - **Shadow Resolution:** Low (mobile) / Medium (PC)
   - **Realtime Shadow Distance:** 60 m

### Skybox

- Assign a simple low-poly gradient sky from the Asset Store or a two-colour
  procedural shader (sky: deep blue-purple, horizon: warm orange/peach).
- Set in `Lighting → Environment → Skybox Material`.

### Fog

`Window → Rendering → Lighting → Environment → Other Settings`:
- **Fog:** enabled
- **Fog Mode:** Linear
- **Fog Color:** soft orange-purple (#C8A0B8 or similar — match your sky horizon)
- **Fog Start Distance:** 80
- **Fog End Distance:** 180

---

## 4. Performance optimisations

### Occlusion Culling

1. `Window → Rendering → Occlusion Culling`.
2. Ensure all static mesh renderers have **Static → Occluder Static** and
   **Occludee Static** checked.
3. Set a small **Smallest Occluder** (e.g. 2 m) so trees and buildings cull
   enemies behind them.
4. Click **Bake**. Re-bake whenever geometry changes.

### LOD Groups on foliage and buildings

For every tree, rock, and building prefab:

| LOD | Poly target | Distance |
|---|---|---|
| LOD0 | Full | 0–15 m |
| LOD1 | ~50% reduced | 15–35 m |
| LOD2 | Billboard or cube | 35 m+ |

Use terrain **Detail Meshes** with billboard crossfade for grass (built into
Unity Terrain — free mobile win).

---

## 5. Integrate existing systems into the world

| System | Placement |
|---|---|
| `WeatherManager` | Empty GO at scene root |
| `VFXManager` | Empty GO at scene root (or persistent via DontDestroyOnLoad) |
| `CameraShakeManager` | Empty GO at scene root |
| Torches / braziers | Inside castle walls + along paths — add `TorchFireController` (WO-55) |
| Dungeon portals | At the far end of each outward pathway — add `PortalVFXController` (WO-65) |

### NavMesh baking

1. Mark all terrain and static buildings **Navigation Static**.
2. `Window → AI → Navigation → Bake`.
3. **Agent Radius:** 0.4 m, **Step Height:** 0.4 m, **Max Slope:** 45°.
4. Verify pathways are fully covered — enemy SpawnPoints should be outside the
   baked area and enemies should path in toward the castle.

---

## 6. Map boundaries & final polish

### Invisible boundary walls

Create 4 long thin `BoxCollider` GameObjects around the playable perimeter.
Layer: `Boundary`. Tag: `Wall`. The hero's `CharacterController` / `Rigidbody`
will stop at these walls without visible geometry.

### Remove floating cube

- If the gray floating cube is a placeholder for a mountain / distant structure:
  replace with a large low-poly rock cluster or a distant Terrain height spike.
- Otherwise delete it.

### Ambient atmosphere

Add 2–3 `ParticleSystem` GameObjects in the village centre:

```
Emission rate:   3–5 / s
Particle size:   0.05–0.12
Lifetime:        6–10 s
Velocity:        Y: +0.15 (slow upward drift)
Color:           white / soft gold, alpha 0.25–0.35
Renderer mode:   Billboard
```

---

## Files to Create / Edit

| Element | Action |
|---|---|
| Flat plane GameObject | **Delete** |
| Terrain | **Create** (see §1) |
| Scene lighting | **Edit** — Sun rotation, fog settings |
| All static scene props | **Edit** — add Static flags, LOD Groups |
| NavMesh | **Bake** (after all geo is placed) |
| Boundary wall colliders | **Create** (4 thin Box Colliders) |
| Torches in village | **Edit** — add `TorchFireController` |
| Dungeon portals at path ends | **Edit** — add `PortalVFXController` |
| Ambient particle systems | **Create** (2–3 dust mote emitters) |

---

## Acceptance Criteria

- [ ] No flat featureless teal ground visible from any normal camera angle
- [ ] Gentle hills at map edges; pathways and castle remain flat
- [ ] Grass, dirt, and rock terrain textures all visible
- [ ] 20+ trees, 15+ rocks placed; no two identical in scale/rotation
- [ ] Sun at 45°/30°, fog active from 80–180 m
- [ ] Occlusion culling baked and active (verify in Scene view Debug → Overdraw)
- [ ] All trees and buildings have LOD Groups
- [ ] NavMesh covers all pathways — enemies path correctly from spawn to castle
- [ ] Hero cannot walk off the map edge
- [ ] Weather, VFX, torches all operational inside the new world
- [ ] 60 FPS on mid-range mobile during a 10-enemy wave
