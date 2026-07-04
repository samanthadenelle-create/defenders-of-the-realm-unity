# ⚠ WORK ORDER 100 — Defend the Tower: Camera Drift, Broken Targeting, Geometry Clipping — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09 (see `PIPELINE_STATE.md` §2). This bug report is historical.

**Status:** CLOSED — **SUPERSEDED** (system removed 2026-06-09)
**Date:** 2026-05-28
**Priority:** Critical — scene is unplayable
**Scope:** Medium — camera setup, targeting system, scene geometry scale
**Observed:** Screenshot (Wave 2/5) —
  • Camera is at a near-horizontal low angle, clipping through scene geometry
  • Camera keeps drifting / not locked to a stable position
  • Enemies (pill shapes) are visible but cannot be targeted
  • Scene geometry fills the entire viewport — camera appears to be INSIDE
    the placeholder block tower structure
  • "18" still floating in world space

---

## Root Cause Summary

The scene has no properly configured camera rig. The camera is likely:
1. Attached to the hero and following them after they fell through the floor
   (no ground collider / NavMesh floor), OR
2. A free Cinemachine virtual camera with no look-at / follow target, drifting
   from its initial transform, OR
3. The placeholder block tower is at massive scale — camera spawns inside it

The targeting failure is a direct consequence: `Physics.OverlapSphere` /
`FindClosestTarget()` can't resolve enemies because the camera's position data
is used for screen-space raycasting and the scene has no valid ground plane.

---

## Bug 1 — Camera Drift / Wrong Angle

### Fix

#### Step 1 — Identify camera setup

```
grep -r "CinemachineVirtualCamera\|vcam\|CameraFollow\|MainCamera" \
    Assets/_Modules/DefendTower/ --include="*.cs" -l
```

Check whether the scene uses:
- A Cinemachine Virtual Camera (correct approach — WO-87)
- A plain `Camera` component with a follow script
- Nothing — camera is just at its default scene position

#### Step 2 — Create a proper Cinemachine camera for Defend the Tower

Add a Cinemachine Virtual Camera to the scene configured for top-down
tower defense view:

```
GameObject → Cinemachine → Virtual Camera
Name: VC_DefendTower

Settings:
    Follow:     Hero root transform
    Look At:    Hero root transform
    Lens:
        Field of View:  55
        Near Clip:      0.3
        Far Clip:       200
    Body:
        Binding Mode:   World Space
        Follow Offset:  (0, 18, -12)   ← behind and above hero
    Aim:
        Tracked Object Offset: (0, 0, 0)
        Lookahead Time: 0
        Horizontal/Vertical Damping: 0.5
```

This gives a classic 3rd-person top-down angle — far enough back to see
the arena, stable enough to aim at enemies.

#### Step 3 — Disable or delete the drifting camera

If a second `Camera` component or a free-floating `CinemachineVirtualCamera`
exists in the scene with no Follow/LookAt target, **disable it**. Two active
cameras blending will cause exactly the drifting behavior observed.

In Unity: `Window → Cinemachine → CM Brain` — confirm only ONE camera is
active at a time in this scene.

#### Step 4 — Verify camera is not parented to geometry

If `Main Camera` is a child of a tower block or any placeholder GO:
1. Drag it to the scene root in the Hierarchy (un-parent it)
2. Assign it to the Cinemachine Brain instead

---

## Bug 2 — Cannot Target Enemies

### Root Cause

Targeting fails because one or more of:
1. No valid ground plane — hero/enemies fell through the floor; their
   `transform.position` is at Y = -infinity
2. `FindClosestTarget()` in `EnemyBrain` uses `FindGameObjectsWithTag("HeroTarget")`
   — if the hero is below the scene, distance calculations skip it
3. Screen-space raycasting for click-to-target uses `Camera.main` — if the
   camera is inside geometry, the ray never exits into open space

### Fix

#### Step 1 — Add a ground plane

The scene must have a floor collider. Add one immediately:

```
GameObject → 3D → Plane
Name:       DefendTower_Ground
Position:   (0, 0, 0)
Scale:      (6, 1, 6)   → 60×60 unit floor
Layer:      Ground
Material:   any opaque URP material (arena floor — dark stone)
Collider:   MeshCollider (auto from Plane)
```

If a floor already exists but has no collider, add `MeshCollider` to it.

#### Step 2 — Bake NavMesh over the floor

After adding the floor:
1. `Window → AI → Navigation → Bake`
2. Confirm the NavMesh covers the full floor area (blue overlay in Scene view)
3. Confirm enemy and hero spawn points are ON the NavMesh surface

#### Step 3 — Fix hero spawn height

If hero spawns above the floor and falls:
```csharp
// In DefendTowerSceneController.Start() or wherever hero spawns:
// Force hero to ground level
var heroPos = heroSpawnPoint.position;
heroPos.y = 0f;   // or sample NavMesh height
hero.transform.position = heroPos;
```

Or use `NavMesh.SamplePosition()`:
```csharp
if (NavMesh.SamplePosition(heroSpawnPoint.position, out var hit, 2f, NavMesh.AllAreas))
    hero.transform.position = hit.position;
```

#### Step 4 — Fix click-to-target (if used)

If enemies are targeted by mouse click, the raycast must use the correct camera:
```csharp
// In targeting input handler:
var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
if (Physics.Raycast(ray, out var hit, 100f, enemyLayerMask))
{
    if (hit.collider.TryGetComponent<EnemyHealth>(out var e))
        SetTarget(e);
}
```

Confirm `Camera.main` resolves to the Cinemachine-driven camera and not
the drifting one. Tag the correct camera as `MainCamera`.

---

## Bug 3 — Scene Geometry at Wrong Scale (Camera Inside Tower)

### Root Cause

The placeholder block tower is scaled so large that the camera spawn point
is inside it. Any geometry larger than ~8–10 units on an axis at position (0,0,0)
will contain a camera positioned at a typical follow offset.

### Fix

#### Step 1 — Rescale or reposition the tower blocks

If using the cylinder/block placeholder (pre-WO-99):
1. Select the tower root GO
2. Reduce scale so the tower base footprint is ≤ 4 units wide and ≤ 10 units tall
3. Or: set Y position so the camera follow offset (0, 18, -12) clears the top

#### Step 2 — Move tower to correct position

The central tower should be at the back of the arena, not the centre where
the hero spawns. A recommended layout:

```
Arena centre:   (0, 0, 0)     ← Hero spawn, enemies path here
Tower position: (0, 0, 15)    ← Tower at back of arena (enemies must cross
                                  full arena to reach it)
Camera follow:  Hero at (0, 0, 0) — camera looks slightly toward tower
```

This gives the player visual context of what they're protecting.

#### Step 3 — WO-99 tower asset (when ready)

Once `DefendTowerBuilding.fbx` is imported (WO-99), replace the placeholder
at position `(0, 0, 15)`, scale `(1, 1, 1)`.

---

## Bug 4 — Ability Names Changed (Frost Nova / Healing Beacon / Meteor Strike)

**Observation only** — the ability set in this build is different from the
previous screenshot (Snare Trap / Mending Salve / Storm of Arrows vs.
Frost Nova / Healing Beacon / Meteor Strike). This may be intentional (
different character loadout) or a scene initialization bug loading the wrong
ability set.

**If unintentional:** Check `DefendTowerAbilityLoader.cs` or
`PlayerAbilityController.LoadAbilities()` — confirm it loads the
correct `AbilityData[]` for this scene and this hero.

**Healing Beacon** (new name) is the correct re-naming per WO-98:
tower-heal ability should be called "Healing Beacon" or "Mend Tower" rather
than "Mending Salve". If this was intentionally renamed, confirm the heal
target routes to `HeartHealth` (not `HeroHealth`) per WO-98.

---

## Scene Setup Checklist

| Step | Action |
|---|---|
| Cinemachine VC | Add `VC_DefendTower`, Follow + LookAt = Hero, offset (0, 18, -12) |
| Camera Brain | Confirm only ONE active Virtual Camera at a time |
| Un-parent camera | Main Camera must be at scene root, not child of geometry |
| Ground plane | Add Plane at (0,0,0), scale (6,1,6), Layer=Ground |
| NavMesh bake | Bake after adding ground — confirm blue overlay |
| Hero spawn height | Clamp hero Y to NavMesh surface on scene load |
| Tower position | Move tower to (0, 0, 15) — behind the arena, not inside it |
| Tower scale | Max ~4 units wide at base, ≤10 units tall |
| Ability set | Confirm correct AbilityData[] loaded for Defend Tower |
| Healing Beacon → HeartHealth | Confirm WO-98 routing is applied to new ability name |

---

## Files to Create / Edit

| File | Action |
|---|---|
| Defend the Tower scene | **Edit** — add VC_DefendTower Cinemachine camera; add ground plane; reposition tower to (0,0,15) |
| `DefendTowerSceneController.cs` | **Edit** — clamp hero spawn to NavMesh surface in Start() |
| Targeting input handler | **Edit** — confirm `Camera.main` resolves correctly |
| Tower placeholder GO | **Edit** — rescale to ≤4 wide, ≤10 tall; move to (0,0,15) |
| `DefendTowerAbilityLoader.cs` (or equivalent) | **Edit** — confirm correct ability set loads |

---

## Acceptance Criteria

- [ ] Camera is stable — no drift between frames
- [ ] Camera angle shows arena from above-behind (can see hero + enemies + tower)
- [ ] Camera does NOT clip through any geometry
- [ ] Hero spawns on the ground plane at Y ≈ 0 (not falling)
- [ ] Enemies spawn on the NavMesh (no floating/falling pill shapes)
- [ ] Clicking / auto-targeting an enemy registers a valid hit
- [ ] Tower is visible at back of arena, not blocking the camera
- [ ] No geometry fills the entire camera view
- [ ] `[EnemyBrain] No target found` warnings gone after hero is on NavMesh
- [ ] Healing Beacon routes heal to `HeartHealth`, not `HeroHealth` (WO-98)
