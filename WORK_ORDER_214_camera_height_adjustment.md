# WO-214: Implement Dual-Camera System (Village Overhead + Overworld Over-the-Shoulder)

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟠 MEDIUM (core camera experience, foundational for 3D immersion)  
**Owner:** CLI  
**Depends On:** None  
**Blocks:** None  
**Can Run In Parallel:** WO-212, WO-213 (all visual polish work can run together after WO-196 + WO-211)

---

## Problem

Current camera is **uniformly high/overhead** everywhere — a workaround to see over village walls. But this destroys the point of 3D in the overworld. Overworld is open space with no walls, so overhead view wastes the 3D perspective and immersion.

**Design split:**
- **Village (Elarion):** High overhead camera ✓ (walls block view, building placement visibility needed)
- **Overworld (zones/camps):** Over-the-shoulder ✓ (open space, 3D perspective, immersion)

The high camera was **necessary for village only**. Overworld needs over-the-shoulder to use the 3D environment.

---

## Solution

### Part A: Audit current camera system
Check `HeroOverShoulderCamera.cs` or main Cinemachine setup to understand:
- How camera follows player
- What offset/pitch values are current
- What triggers scene transitions (Village → Overworld)

### Part B: Implement scene-based camera switching
```csharp
// Pseudocode
public class CameraManager : MonoBehaviour
{
    public CameraProfile villageProfile;      // High overhead
    public CameraProfile overworldProfile;    // Over-the-shoulder
    
    public void SetCameraProfile(CameraProfile profile)
    {
        // Smoothly transition Cinemachine offset + pitch
        // to the new profile (lerp over 0.5s)
    }
}
```

### Part C: Village camera profile
- **Y offset:** ~20 units (high, see over buildings)
- **Pitch:** Looking down ~45° (can see feet + buildings ahead)
- **Distance:** Far enough to see walls + gates

### Part D: Overworld camera profile
- **Y offset:** ~3–4 units (over-the-shoulder height)
- **Pitch:** Looking slightly down ~15° (see where you're walking)
- **Distance:** Close enough for immersion, far enough to see enemies

### Part E: Scene transition wiring
- When entering Village scene: use villageProfile
- When entering Zone/Overworld scene: use overworldProfile
- Smooth lerp transition between them (0.3–0.5s)

---

## Acceptance Criteria

- [ ] Audit current camera setup + identify transition hooks
- [ ] Create CameraProfile ScriptableObject or config struct
- [ ] Implement scene-based camera profile switching
- [ ] Village profile tested: high overhead, see buildings + gates
- [ ] Overworld profile tested: over-the-shoulder, immersive, smooth movement
- [ ] Camera smoothly transitions between profiles on scene load (no snap)
- [ ] No clipping through terrain/buildings in either mode
- [ ] WebGL tested: village feels tactical, overworld feels immersive
- [ ] Commit: "WO-214: implement dual-camera system (village overhead + overworld over-shoulder)"

---

## Testing Workflow

1. **Village mode:** Walk around, verify you can see buildings, gates, placement areas clearly
2. **Overworld mode:** Walk toward camp, verify you see the 3D environment, enemies coming toward you
3. **Transition:** Load from village → zone. Camera should smoothly shift from overhead to over-the-shoulder
4. **Reverse:** Load from zone → village. Camera should smoothly shift back to overhead

---

## Notes

- Use Cinemachine's built-in blending for smooth transitions
- The "over-the-shoulder" name is aspirational — exact angle/distance can be tweaked in testing
- This is a **foundational** change that affects player immersion significantly

---

**Estimate:** 30–40 min (audit code, implement switching, test both modes, fine-tune angles)
