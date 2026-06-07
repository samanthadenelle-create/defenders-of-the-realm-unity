# WO-212: Fix Gate Z-Fighting with Ground Plane

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟠 MEDIUM (visual polish, scene glitch)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None  
**Can Run In Parallel:** WO-213, WO-214 (all visual polish work can run together after WO-196 + WO-211)  

---

## Problem

Gate bottom edges don't align flush with the map ground plane. Creates visible seams/z-fighting at the gate base where gates meet the terrain.

Expected: Gate bottom should sit exactly on the ground plane (same Y as the rest of the Village terrain).

---

## Solution

**In VillageSceneBuilder.cs:**
Audit the gate placement code. Gates are likely positioned at (x, y, z) where `y` is slightly above or below the ground mesh.

1. Measure the ground plane Y value (typically 0 or a consistent height)
2. Update all gate placement to use that same Y value
3. Verify no gates clip through or float above ground
4. Rebake the scene

**Acceptance Criteria:**
- [ ] All 5 gates sit flush with ground plane
- [ ] No visible seams or z-fighting at gate bases
- [ ] Scene rebakes cleanly
- [ ] Commit: "WO-212: align gate bottom edges to ground plane"

---

## Files to Check

- `Assets/Editor/VillageSceneBuilder.cs` — gate placement logic
- `Assets/Scenes/Village.unity` — verify gate Y positions after rebuild
