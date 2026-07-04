# ⚠ WORK ORDER 99 — Defend the Tower: Enemy Pill Placeholders + Gothic Tower Asset Import — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09 (see `PIPELINE_STATE.md` §2). This WO is historical.

**Status:** CLOSED — **SUPERSEDED** (system removed 2026-06-09)
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — enemy prefab materials + import Blender tower asset
**Observed:** Screenshot annotations —
  • All enemies render as red/colored sphere-pill shapes ("All Pills")
  • Tower geometry is stacked cylinder blocks — intended asset is the
    detailed gothic tower (spires, battlements, arched door) from Blender

---

## Bug 1 — All Enemies Are Pill/Sphere Placeholders

### Root Cause

Enemy prefabs in the Defend the Tower scene have no mesh or material
assigned — Unity renders them as default sphere primitives. This is the
same root cause as WO-94 (Skeleton purple capsule) but affects all enemy
types spawned in this scene.

Likely causes (check in order):
1. `EnemyData.prefab` fields are null on the Defend the Tower enemy assets
2. The ObjectSpawner is instantiating enemies but the prefab has a default
   sphere with no material
3. The enemy prefab mesh renderers reference a deleted or renamed material

### Fix

#### Step 1 — Identify which enemies spawn in Defend the Tower

```
grep -r "DefendTower\|defendTower\|WaveData" Assets/_Data/Waves/ --include="*.asset"
```

Open the WaveData assets for the Defend the Tower scene. List every
`EnemyData` asset referenced in `enemySpawnEntries`.

#### Step 2 — Check each EnemyData.prefab

For each referenced `EnemyData` asset:
1. Open in Inspector
2. Confirm `prefab` field is assigned (not null)
3. If null — assign the correct prefab from `Assets/_Modules/Village/Enemies/Prefabs/`

#### Step 3 — Check each enemy prefab material

For each enemy prefab:
1. Select the root (or mesh child)
2. `MeshRenderer.materials[0]` must not be None/Empty
3. If missing, assign a temporary URP Lit material per enemy type:

```
Goblin / basic enemy:    color #8B4513  (brown)
Skeleton:                color #D4C5A0  (bone white)
Armored enemy:           color #708090  (steel grey)
Boss / heavy:            color #1A1A2E  (dark armored)
```

Name materials `Enemy_[Type].mat` in `Assets/Materials/Enemies/`.

#### Step 4 — Guard in ObjectSpawner (already in WO-94)

Ensure `ObjectSpawner.Spawn()` logs an error when `data.prefab == null`
so missing prefabs are immediately visible in Console.

---

## Bug 2 — Tower Is Stacked Cylinder Blocks (Wrong Geometry)

### Root Cause

The scene currently uses manually-placed cylinder/block GameObjects as a
stand-in for the actual tower. The intended asset is a detailed gothic
wizard/castle tower (spires, crenellations, arched doorway) modeled in
Blender and ready for export.

### Fix

#### Step 1 — Export from Blender

In Blender, with the tower model selected:

```
File → Export → FBX (.fbx)
Settings:
    Scale:              0.01  (Blender units → Unity meters)
    Apply Scalings:     FBX All
    Forward:            -Z Forward
    Up:                 Y Up
    Apply Unit:         ✓
    Mesh → Smoothing:   Face
    Include:            Limit to Selected Objects ✓
    Armature:           uncheck (static mesh, no rig)
```

Save as: `DefendTowerBuilding.fbx`

Place in: `Assets/_Models/Environment/DefendTowerBuilding.fbx`

#### Step 2 — Unity import settings

Select `DefendTowerBuilding.fbx` in the Project window → Inspector:

```
Model tab:
    Scale Factor:       1
    Import Normals:     Calculate
    Normals Mode:       Area And Angle Weighted

Rig tab:
    Animation Type:     None  (static mesh)

Materials tab:
    Material Creation:  Import via MaterialDescription
    Location:           Use External Materials
```

Click **Apply**.

#### Step 3 — Assign material

Create `Assets/Materials/DefendTower_Building.mat`:
```
Shader:       Universal Render Pipeline/Lit
Base Map:     Assign stone texture if available; otherwise color #2A2535
Metallic:     0.0
Smoothness:   0.1
Normal Map:   Assign if baked from Blender (DefendTowerBuilding_Normal.png)
```

Assign this material to all mesh renderers on the imported FBX.

#### Step 4 — Replace placeholder in scene

1. Open Defend the Tower scene
2. Select and **delete** the cylinder/block tower GameObject(s)
   (or the parent named `[PLACEHOLDER_TOWER]` or similar)
3. Drag `DefendTowerBuilding.fbx` (or its Prefab) into the scene
4. Position at scene centre: `(0, 0, 0)` (adjust Y so base sits on ground)
5. Scale: `(1, 1, 1)` — if too large/small, fix the Blender export scale
   rather than scaling non-uniformly in Unity

#### Step 5 — Add components to tower GameObject

```
Tag:            HeartTarget        ← enemies path toward it (WO-90/92)
Layer:          Default (or Structure)
Components:
  ├── HeartHealth (maxHealth=200, breachThreshold=0.30)
  ├── MeshCollider (Convex=false, for physics queries)
  └── BoxCollider  (for enemy attack range detection — simpler)
```

Wire `HeartHealth.onBreached` → Last Stand dialog trigger.
Wire `HeartHealth.onDestroyed` → Game Over handler.

#### Step 6 — Create tower prefab

Once positioned and wired:
1. Drag the tower GO from Hierarchy → `Assets/_Prefabs/Environment/`
2. Name: `DefendTower_Building.prefab`
3. This prefab can be reused in any Defend the Tower variant scene

---

## Scene Wiring Checklist

| Step | Action |
|---|---|
| EnemyData prefabs | Assign prefab on every EnemyData used in Defend the Tower WaveData |
| Enemy materials | Assign URP Lit material on every enemy prefab's MeshRenderer |
| Export tower from Blender | FBX, Scale=0.01, -Z Forward, Y Up |
| Import into Unity | `Assets/_Models/Environment/DefendTowerBuilding.fbx` |
| Assign material | `DefendTower_Building.mat` — stone, no metallic |
| Replace placeholder | Delete cylinder blocks; place FBX in scene at (0,0,0) |
| Add HeartHealth | maxHealth=200, wire onBreached + onDestroyed events |
| Add BoxCollider | For enemy attack range |
| Tag HeartTarget | So EnemyBrain targets it (WO-92) |
| Create prefab | Save to `Assets/_Prefabs/Environment/` |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Data/Enemies/*.asset` | **Edit** — assign prefab fields on all Defend Tower enemies |
| `Assets/Materials/Enemies/Enemy_*.mat` | **Create** — per-type placeholder materials |
| `Assets/_Models/Environment/DefendTowerBuilding.fbx` | **Import** — export from Blender |
| `Assets/Materials/DefendTower_Building.mat` | **Create** |
| `Assets/_Prefabs/Environment/DefendTower_Building.prefab` | **Create** |
| Defend the Tower scene | **Edit** — replace block tower with FBX prefab, add HeartHealth |

---

## Acceptance Criteria

- [ ] All enemies in Defend the Tower render with correct meshes and materials (no pill/sphere shapes)
- [ ] No `[ObjectSpawner] Prefab is null` errors in Console
- [ ] Gothic tower asset renders in scene — spires, battlements, arched doorway visible
- [ ] Tower sits on the ground plane at scene centre with correct scale
- [ ] No purple/magenta surfaces on tower mesh
- [ ] `HeartHealth` component present on tower; HP decreases when enemies attack
- [ ] Tower tagged `HeartTarget` — enemies path toward and attack it
- [ ] `onBreached` fires at 30% HP; `onDestroyed` fires at 0 HP
- [ ] Tower prefab saved to `Assets/_Prefabs/Environment/` for reuse
