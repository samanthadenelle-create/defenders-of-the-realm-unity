# WORK ORDER 249 — Oversized Villager NPC Scale Fix
**Status: READY TO IMPLEMENT**
**WO:** 249 | **Lane:** CODE (parallel safe)
**Closes:** DEF-148

---
## Problem

One villager NPC is rendering at a large scale — clipping into the camera near-plane and merging with the player model during Wave 2. Visible in owner playtest screenshots.

---
## Fix

**File:** Wherever `AmbientNPC` or villager GameObjects are placed (`VillageSceneBuilder.cs` or a prefab).

1. Find the oversized NPC — likely scale is set to (3,3,3) or similar instead of (1,1,1)
2. Set `transform.localScale = Vector3.one` on all AmbientNPC placements
3. Add a guard in the spawn logic:

```csharp
// After instantiating any AmbientNPC:
var npc = Instantiate(npcPrefab, position, rotation);
npc.transform.localScale = Vector3.one;   // force correct scale — never trust prefab scale
```

4. If the issue is in the prefab itself: open the prefab, reset scale to (1,1,1), save.

---
## Acceptance criteria
- [ ] All AmbientNPC GameObjects have `localScale` of (1,1,1) after scene build
- [ ] NPC bounding box does not intersect the main camera near-plane during normal gameplay
- [ ] No visible geometry interpenetration with player model during Wave 2
- [ ] Fix confirmed in Play mode on mobile WebGL — walk up to and past the villager
- [ ] Brace balance check passed

## What NOT to touch
- `Village.unity` — do not hand-edit (fix via code/prefab only)
- `WaveManager`, enemy scripts
