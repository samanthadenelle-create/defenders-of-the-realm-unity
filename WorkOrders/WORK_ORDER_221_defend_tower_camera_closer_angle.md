# WO-221: Defend the Tower — Camera Closer to Tower for Better Sightlines

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟠 MEDIUM (battle UX improvement)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None

---

## Problem

Defend the Tower battle camera is too far / angled incorrectly. Current angle doesn't let player see far enough down the approach to enemies. Can't see incoming troops early enough to react.

**Solution:** Move camera closer to the tower, adjust angle so player can see **further down the approach path**. Should give clearer sightlines to incoming enemies.

---

## Current State

Tower defense battle uses isometric or top-down camera. Camera position/angle needs adjustment.

**Files to check:**
- `Assets/_Modules/BattleATB/Camera/` (or similar)
- `Assets/Scenes/DefendTheTower.unity` (scene camera settings)
- `Assets/Generated/Cameras/BattleCamera.cs` (or CinemachineVirtualCamera)

---

## Solution

### Audit Current Setup
1. Load DefendTheTower scene in Unity editor
2. Play and observe camera angle/distance
3. Identify: can you see enemies approaching from far away?
4. If not: camera is too high, too far, or wrong pitch angle

### Adjustments to Try

**Move camera closer to tower:**
- Reduce camera distance (zoom in slightly)
- Keep tower roughly centered (don't make it off-screen)

**Adjust pitch angle:**
- Current: probably ~45° looking down
- Better: ~30–40° looking down (shallower angle lets you see further)
- Trade: tower gets smaller, but approach visibility increases

**Example adjustment:**
```
Current: Distance=50, Pitch=-45°
Target:  Distance=35-40, Pitch=-30 to -35°
(numbers are estimates — adjust based on feel)
```

### Implementation

**If using Cinemachine:**
1. Find the BattleCamera virtual camera
2. Adjust Transposer → Position offset (closer to tower)
3. Adjust Aim target + pitch (shallower angle)
4. Test in Play mode

**If custom script:**
1. Locate HeroOverShoulderCamera or equivalent
2. Modify camera.position = tower + offset
3. Reduce offset magnitude or change angle
4. Compile + test

---

## Acceptance Criteria

- [ ] Camera moved closer to tower (distance reduced by ~20–30%)
- [ ] Pitch angle adjusted to ~30–35° (shallower for better sightlines)
- [ ] Can see enemy approach from ~50–60m away
- [ ] Tower still visible on screen (not off-edge)
- [ ] No clipping through terrain
- [ ] Battle playable with better awareness
- [ ] WebGL tested: enemy visibility improved
- [ ] Commit: "WO-221: adjust defend tower camera (closer, better sightlines)"

---

## Testing

1. Load DefendTheTower, play battle
2. Watch enemies approach from spawn point
3. Can you see them coming? If yes, camera is good. If no, move it closer/adjust angle.
4. Does tower look right? Should still be prominent on screen.

---

## Notes

- This is separate from WO-214 (village/overworld dual camera)
- Battle camera is scene-specific
- Adjustment should be small (20–30% closer) to avoid disorientation

---

**Estimate:** 10–15 min (locate camera, adjust, test, verify)
