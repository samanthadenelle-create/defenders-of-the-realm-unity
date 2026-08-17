<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-215: Wire Up Build Mode Click-to-Place + Green/Red Grid Preview

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (build mode broken — no input response)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** All build/construction feature testing  
**Can Run In Parallel:** None — this is blocking other build work  

---

## Problem

Builder mode is visible on screen but **click-to-place doesn't work**. When user clicks on a grid spot:
- ❌ No visual feedback
- ❌ No building placed
- ❌ No validation (green for valid, red for invalid spots)

Expected behavior:
- Click on ground → shows grid preview
- Hover over spots → shows green (valid) or red (invalid) highlight
- Click valid spot → building placed
- Click invalid spot → rejected with feedback

---

## Current State

From PIPELINE_STATE.md: **WO-108 (player_build_mode)** marked STUB — class exists, no runtime wiring.

**Missing:**
1. Input detection (raycast click on grid/ground)
2. Grid preview visualization (green/red overlay on valid/invalid spots)
3. Building placement logic (instantiate building on confirmed spot)
4. Validation (check if spot is clear, within bounds, etc.)

---

## Solution

### Step 1: Locate build mode code
```
- Assets/_Modules/Village/Build/BuildModeManager.cs (or similar)
- Assets/_Modules/Village/Build/GridPlacementValidator.cs
- Assets/_Modules/Village/UI/BuildMenuController.cs
```

### Step 2: Implement click input
```csharp
// Pseudocode
void OnMouseClick(Vector3 worldPos) {
    // Raycast to ground/grid
    // Get grid cell at position
    // Check if valid
    // Show preview (green/red)
}
```

### Step 3: Add grid preview visuals
- **Valid spot (green):** Transparent green square/highlight on the grid
- **Invalid spot (red):** Transparent red square/highlight on the grid
- **Building footprint preview:** Show building outline where it will be placed

### Step 4: Implement placement
```csharp
// On confirmed click
if (IsValidPlacement(gridCell)) {
    Building building = Instantiate(selectedBuildingPrefab, gridCell.Position);
    building.Configure(...)
    // Update UI, resources, etc.
}
```

---

## Acceptance Criteria

- [ ] Click on grid spot registers (raycast works)
- [ ] Hover shows green/red preview (valid/invalid)
- [ ] Click valid spot → building placed
- [ ] Click invalid spot → rejected (visual + audio feedback)
- [ ] Grid preview updates in real-time as mouse moves
- [ ] Building appears with correct orientation/scale
- [ ] WebGL build tested: builder mode fully playable
- [ ] Commit: "WO-215: implement build mode click-to-place + validation grid"

---

## Risk

Build mode is foundational for construction pillar. If placement validation is wrong, players can build in bad spots. Audit collision/bounds checking.

---

## Notes

- User is currently blocked on testing build features
- This unblocks WO-104 (castle + moat visual build) and all subsequent construction work
- Pair with visual feedback (UI toast/"invalid spot" sound)

---

**Estimate:** 45–60 min (locate code, implement input, add visuals, test)
