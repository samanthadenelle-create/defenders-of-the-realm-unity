<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** Village.unity + OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/Village.unity` is absent from disk and from `git ls-files` and `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; both halves (DEF-96 tree in the Village bake, DEF-126 Village↔OuterWorld terrain seam) target deleted scenes.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 247 — Village Scene Cleanup: Upside-Down Tree + Terrain Seam
**Status:** CLOSED — OBSOLETE: Village.unity + OuterWorld.unity no longer exists (era sweep 2026-08-17)
**WO:** 247 | **Lane:** VILLAGE (serial — one rebake covers both)
**Closes:** DEF-96, DEF-126

---
## DEF-96 — Upside-down tree reappeared

A tree asset is being placed with an inverted rotation (Y=180 or X=180) somewhere in `VillageSceneBuilder.cs`.

**Fix:**
1. In `VillageSceneBuilder.cs`, search for tree placement calls that use `Quaternion.Euler(180, ...)` or `rotation.x = 180` or similar
2. Change inverted rotation to `Quaternion.identity` or correct upright value
3. If the tree comes from a prefab with baked inverted rotation, add a rotation correction on spawn: `go.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0)`

**Acceptance criteria:**
- [ ] `VillageSceneBuilder.cs` contains no tree placement with inverted X or Z rotation
- [ ] Scene rebaked — no upside-down tree visible from any camera angle in Play mode
- [ ] Tree assets render upright with correct orientation

---
## DEF-126 — Terrain seam: Village ↔ OuterWorld height mismatch

The Village.unity and OuterWorld.unity terrain heights don't match at the boundary, creating a visible ledge.

**Fix:**
In `ExteriorTerrainBuilder.cs` or `VillageSceneBuilder.cs`, ensure the terrain height at the boundary matches the village floor height (Y=0). The OuterWorld terrain edge at the village boundary should be set to exactly Y=0 within a 5-unit blending band.

```csharp
// Sample approach — flatten the boundary band:
// When building OuterWorld terrain, for all terrain points within 20m of the village edge:
//   height = Mathf.Lerp(0f, naturalHeight, (distance - 5f) / 15f)
// This creates a smooth ramp from Y=0 at the seam to natural terrain beyond 20m
```

**Acceptance criteria:**
- [ ] No visible step or height gap at the Village/OuterWorld boundary seam from any approach angle
- [ ] Player character crosses the boundary without a physics bump
- [ ] Terrain height at the seam matches within ±0.05 Unity units
- [ ] Confirmed in WebGL — walking all four cardinal directions across the boundary

---
## Rebake
Run one village rebake after both fixes: `Defenders > Week 3 > Build Village Scene`

## What NOT to touch
- `Village.unity` — do not hand-edit
- Enemy AI, WaveManager, ATB scripts
