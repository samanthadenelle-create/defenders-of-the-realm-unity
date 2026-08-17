<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** Village.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/Village.unity` is absent from disk and from `git ls-files`; the WO names `Assets/Scenes/Village.unity` as the file to verify.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WO-212: Fix Gate Z-Fighting with Ground Plane

**Status:** CLOSED — OBSOLETE: Village.unity no longer exists (era sweep 2026-08-17)

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
