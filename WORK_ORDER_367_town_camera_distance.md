# WO-367: Town Camera Distance — Move 65% Closer

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.25 days — config only)  
**Priority:** HIGH (immediate visual adjustment)  
**Lane:** Build/Perf

---

## Overview

Camera in town is too far away. Move **65% closer** to hero.

**Current:** Camera at 22m height, ~35–40m back  
**Target:** Camera at ~13–14m height, ~12–15m back (65% closer)

---

## Acceptance Criteria

- [ ] Town camera distance reduced by 65%
- [ ] Hero takes up more of viewport (closer view)
- [ ] Camera still shows full village (no clipping into buildings)
- [ ] Angle/pitch unchanged (45° angled view preserved)
- [ ] Smooth transition when entering town (0.5s blend)
- [ ] Works in all town contexts (building, exploring, waiting)

---

## Files to Modify

- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — `_buildModeHeight` and distance values
- OR: `Assets/_Modules/Village/VillageController.cs` if town camera is separate

**Current values (approximate):**
```csharp
_buildModeHeight = 22f;  // Change to ~13-14f
_camFocus = [distance];  // Reduce by 65%
```

**Math:** If current distance is 35m, new distance = 35 * 0.35 = ~12m

---

## Configuration

**Before:**
- Height: 22m
- Distance: 35–40m back
- Pitch: 45°

**After (65% closer):**
- Height: 13–14m
- Distance: 12–15m back
- Pitch: 45° (unchanged)

**Test:** Hero should be prominently visible but still see surrounding buildings/gates.

---

## Testing

- [ ] Camera in town is closer (65% reduction)
- [ ] Hero is more prominent in frame
- [ ] Full village still visible (no excessive clipping)
- [ ] Smooth transition (no jerky camera movement)
- [ ] Pan/zoom still works normally
- [ ] Works in WebGL build

---

## REGRESSION ALERT

**WO-367 broke movement — see WO-368 for fix.**

Movement logic is coupled to camera angle. Changing camera distance broke hero movement direction mapping. WO-368 decouples movement from camera and validates with WO-363 (Orientation Validation Gate).
