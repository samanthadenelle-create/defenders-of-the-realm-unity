# WORK ORDER 30 — Dungeon Portals: Move Inside Walls + Reduce Size 20%

**Status:** CLOSED (owner ruling 2026-08-09: portals now behind the coming-soon gate)
**Date:** 2026-05-26
**Author:** Bug triage — playtest screenshots (both portals)
**Priority:** High — portals are at the wall perimeter and visually dominate / block the view

---

## Problem

Both village dungeon portals are positioned at the interior edge of the castle
walls and the `Portal.fbx` arch is too large — it reads as a massive stone cliff
embedded in the wall rather than an inviting dungeon entrance inside the village.

| Portal | Current position | Issue |
|---|---|---|
| Folk's Old Granary | `(18, 0, 6)` | East side — portal arch fills the wall gap, hero at X=18 is essentially at the wall boundary |
| Healer's Cottage | `(-18, 0, 6)` | West side — same problem; large stone arch at the wall edge |

Screenshots show:
- **Folk's Granary**: Hero standing outside/at the wall with `Press F — Folk's Granary` in huge text; the cliff arch towers over the entire wall section
- **Healer's Cottage**: Same — `Press F — Healer's Cottage` filling the screen; arch is at wall perimeter with the village behind it

Owner direction: "move portal and dungeon tag **inside** castle walls and **reduce object 20%**"

---

## Root Cause

`VillageSceneBuilder.BuildDungeonPortals()` places the portals at:
```csharp
BuildOneDungeonPortal(..., new Vector3(-18f, 0f, 6f), "HealersCottage", "Healer's Cottage");
BuildOneDungeonPortal(..., new Vector3( 18f, 0f, 6f), "FolksGranary",   "Folk's Old Granary");
```

With the old wall ring (`WallHalfX = 28`), X=±18 is 10 m inside the wall — but the
`Portal.fbx` arch is normalized to `archHeight = 5.2f` and is very wide, so its
physical geometry protrudes to or past the wall face.

After WO-26 enlarges the wall ring to `WallHalfX = 42`, X=±18 will be 24 m inside —
but the arch is still oversized and reads as a wall feature rather than an interior building.

---

## Fix

### 1. Reposition both portals further inward

Move portals from `X = ±18` to `X = ±12` (6 m further from the wall boundary).
Shift Z from `6` to `8` so they sit in the village interior courtyard space,
away from the wall line.

```csharp
// In VillageSceneBuilder.BuildDungeonPortals():
BuildOneDungeonPortal(..., new Vector3(-12f, 0f, 8f), "HealersCottage", "Healer's Cottage");
BuildOneDungeonPortal(..., new Vector3( 12f, 0f, 8f), "FolksGranary",   "Folk's Old Granary");
```

Verify these coordinates don't conflict with buildings placed by WO-26 and WO-27.
Cross-check against `Buildings[]` entries:
- Workshop at `(16, 0, 12.5)` — clear, portals at ±12 Z=8 are below that
- Farm at `(19, 0, -1)` — clear (south side, Z negative)
- Arcane Tower at `(6, 0, -12.5)` — clear (south-central)

### 2. Reduce portal arch visual size by 20%

The `Portal.fbx` arch is normalized via `NormalizeProp(arch, archHeight)` where
`archHeight = 5.2f`. Reduce by 20%:

```csharp
// Before:
const float archHeight = 5.2f;
// After:
const float archHeight = 4.16f;   // 5.2 × 0.8 = 4.16 m
```

The `BoxCollider` trigger uses `archHeight` for its Y size — it will scale with this
change automatically since both use the same constant.

```csharp
trigger.center = new Vector3(0f, archHeight * 0.5f, 0f);
trigger.size   = new Vector3(archWidth, archHeight, 0.6f);
```

### 3. Scale down the portal disc proportionally

The ground disc is `localScale = new Vector3(3.5f, 0.05f, 3.5f)`. Reduce to match:
```csharp
disc.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);   // 3.5 × 0.8
```

### 4. Lower the floating sign to match new arch height

The sign is placed at `localPosition = new Vector3(0f, 3.2f, 0f)`. With the
smaller arch, bring it down slightly:
```csharp
sign.transform.localPosition = new Vector3(0f, 2.8f, 0f);
```

---

## Files to Edit

- `Assets/Editor/VillageSceneBuilder.cs`
  - `BuildDungeonPortals()` — update both `Vector3` positions (§Fix 1)
  - `BuildOneDungeonPortal()` — change `archHeight` from `5.2f` to `4.16f` (§Fix 2)
  - `BuildOneDungeonPortal()` — disc scale 3.5→2.8 (§Fix 3)
  - `BuildOneDungeonPortal()` — sign Y from 3.2→2.8 (§Fix 4)

---

## Acceptance Criteria

- [ ] Both portals visibly inside the castle walls with clear space between portal and wall
- [ ] Portal arch is noticeably smaller — reads as a door-sized feature, not a cliff
- [ ] "Press F — Folk's Granary" and "Press F — Healer's Cottage" prompts appear at the correct smaller arches
- [ ] Hero can approach and enter both dungeons from the interior side
- [ ] No coordinate conflict with buildings from WO-26 (enlarged city) or WO-27 (spawn world)
- [ ] **Owner-gated re-bake required**: Defenders > Week 3 > Build Village Scene after code changes
