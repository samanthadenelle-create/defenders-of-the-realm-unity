# WO-368: Camera Distance Fix — Movement Regression & Orientation Validation

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at CameraModeController.cs:364-371.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Estimated Effort:** P0 (0.5 days — debug + fix)  
**Priority:** CRITICAL (movement broken)  
**Lane:** Build/Perf

---

## Overview

WO-367 (camera 65% closer) broke movement in town. 

**Issue:** Movement system is coupled to camera distance/angle. Changing camera broke hero movement.

**Fix:** 
1. Revert camera angle to original (45°)
2. Keep closer distance (65% closer from original)
3. Fix movement logic (decouple from camera)
4. WO-363 (Orientation Validation Gate) catches any remaining regressions

---

## Acceptance Criteria

- [ ] Camera angle reverted to 45° (original pitch)
- [ ] Camera distance reduced 65% (closer view, per WO-367)
- [ ] Movement works correctly (WASD/arrows respond)
- [ ] Character faces correct direction (up/down/left/right)
- [ ] WO-363 orientation validation passes all tests
- [ ] No camera-relative movement bugs
- [ ] Works in town, battle, exploration

---

## Root Cause Analysis

Movement is likely broken because:
- Movement input is calculated relative to camera direction (BAD)
- Camera angle change altered input-to-movement mapping
- Character doesn't face input direction anymore

**Expected issue:** Camera at different distance/angle → input direction doesn't match output movement direction → WO-363 catches it

---

## Files to Debug/Fix

### Check These:
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` — Is movement calculated relative to camera?
  ```csharp
  // BAD (camera-relative):
  var forward = _camera.transform.forward;
  var right = _camera.transform.right;
  moveDir = (forward * inputY) + (right * inputX);
  
  // GOOD (world-absolute):
  moveDir = new Vector3(inputX, 0, inputY).normalized;
  ```

- `Assets/_Modules/SmartMobileCamera.cs` — Is camera angle being applied to movement?

### Fix Strategy:
Movement should be **world-absolute**, not camera-relative:
- UP input → Vector3(0, 0, 1) — always "north"
- DOWN input → Vector3(0, 0, -1) — always "south"
- LEFT input → Vector3(-1, 0, 0) — always "west"
- RIGHT input → Vector3(1, 0, 0) — always "east"

Camera angle/distance should NOT affect input mapping.

---

## Validation Gate (WO-363)

After fix, WO-363 (Character Orientation Validation) **MUST PASS:**

```
✓ Move UP → Face UP
✓ Move DOWN → Face DOWN
✓ Move LEFT → Face LEFT
✓ Move RIGHT → Face RIGHT
✓ Move diagonal → Face 45°
✓ Idle → Preserve last facing
```

If any test fails → movement is still broken.

---

## Camera Final Values

**After fix:**
- Pitch: 45° (original, reverted)
- Height: ~13–14m (65% closer, per WO-367)
- Distance: ~12–15m back (65% closer)
- Angle preserved, just pulled closer

---

## Testing Checklist

- [ ] WASD moves hero correctly (not camera-relative)
- [ ] Character faces input direction (up/down/left/right)
- [ ] Diagonal movement works (UP+LEFT = 45° facing)
- [ ] WO-363 orientation validation passes all tests
- [ ] Works in town, exploration, battle
- [ ] Camera is closer (65% reduction) but angle unchanged
- [ ] No jittering or weird movement behavior

---

## What NOT to Touch

- Camera rendering (just adjust distance/angle values)
- Animation system (movement is separate)
- Input handling (just fix the calculation)

---

## Regression Prevention

WO-363 (Character Orientation Validation) is now the **hard gate**:
- Every frame, checks: facing direction matches input direction
- If mismatch > 30° → BUILD FAILS
- This prevents similar regressions in the future

**Before shipping any camera/movement changes, WO-363 MUST PASS.**

---

## Acceptance Sign-Off

- [ ] Movement works (world-absolute, not camera-relative)
- [ ] Character faces correct direction (validated by WO-363)
- [ ] Camera closer (65%) but angle unchanged (45°)
- [ ] WO-363 orientation tests all pass
- [ ] No movement regressions in any context
