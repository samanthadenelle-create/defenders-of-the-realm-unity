<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-373: CRITICAL Regression Gates — Hard Blockers Before Build Shipping

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Estimated Effort:** P0 (Testing/Verification Only — 0.5 day per code change)  
**Priority:** BLOCKER (no code ships without passing these)  
**Lane:** 0 Verify

---

## MANDATE

**Before ANY work order is merged to main/build:**

These tests **MUST PASS**. Failure = immediate revert + work order bounces back.

---

## Hard Gate #1: Tree of Life at Origin (0,0,0)

### The Rule

**Heart of Elarion (the world tree / stone reliquary) MUST be positioned at world origin (0, 0, 0).**

- [ ] Tree position: X=0, Y=0, Z=0
- [ ] Tree rotation: Unchanged (if rotated, revert)
- [ ] No parent transforms affecting position
- [ ] No scene offset/pivot changes
- [ ] Verified in Editor: Select Heart → Inspector shows Transform (0, 0, 0)

### Why

The entire game layout depends on this anchor:
- Village perimeter defined relative to tree
- Camera behavior calculated from tree center
- Enemy spawn points positioned relative to tree
- Build mode grid aligned to tree
- Player pathfinding centered on tree
- Scene rebuilds use tree as reference point

**If tree moves off origin:** Everything cascades — spawn points wrong, camera wrong, grid misaligned, gates broken.

### Verification Command

```csharp
// Before shipping ANY code change:
public static bool VerifyTreeOrigin()
{
    var heart = FindObjectOfType<HeartController>();
    if (heart == null) return false;
    
    Vector3 pos = heart.transform.position;
    Vector3 euler = heart.transform.eulerAngles;
    
    bool positionOK = (pos.x == 0 && pos.y == 0 && pos.z == 0);
    bool rotationOK = (Mathf.Abs(euler.y) < 1f);  // Allow minor floating point error
    
    if (!positionOK)
        Debug.LogError($"REGRESSION: Heart at {pos}, NOT at origin!");
    if (!rotationOK)
        Debug.LogError($"REGRESSION: Heart rotated {euler.y}°, should be 0°!");
    
    return positionOK && rotationOK;
}
```

**Before pushing code:**
```
✓ Run VerifyTreeOrigin() in console
✓ Result: TRUE
✓ Clear to ship
```

---

## Hard Gate #2: Player Movement Works

### The Rule

**Player MUST respond to WASD/Arrow keys in all contexts (town, exploration, battle).**

- [ ] Move UP (W/↑) → Player moves north, faces north
- [ ] Move DOWN (S/↓) → Player moves south, faces south
- [ ] Move LEFT (A/←) → Player moves west, faces west
- [ ] Move RIGHT (D/→) → Player moves east, faces east
- [ ] Move DIAGONAL (W+A, etc.) → Player moves diagonal, faces diagonal
- [ ] Works in town (not just battle)
- [ ] Works in exploration (not just town)
- [ ] Facing direction matches input direction (WO-363 validation)

### Why

If player can't move, game is **unplayable**. Full stop.

Recent regression (WO-367/368): Camera distance change broke movement direction mapping. Movement is a fundamental gate.

### Verification Steps

**Manual test (60 seconds):**
1. Start game → Town/Village
2. Press W → Player moves north (away from camera). ✓
3. Press A → Player moves west (left). ✓
4. Press S → Player moves south (toward camera). ✓
5. Press D → Player moves east (right). ✓
6. Press W+D diagonal → Player moves northeast. ✓
7. Every input facesirection matches move direction. ✓
8. No stuttering, smooth locomotion. ✓

**Automated test:**
```csharp
public static bool VerifyPlayerMovement()
{
    var hero = FindObjectOfType<HeroLocomotion>();
    if (hero == null) return false;
    
    // Test WASD inputs
    bool canMoveUp = TestInput(KeyCode.W);      // World +Z
    bool canMoveDown = TestInput(KeyCode.S);    // World -Z
    bool canMoveLeft = TestInput(KeyCode.A);    // World -X
    bool canMoveRight = TestInput(KeyCode.D);   // World +X
    
    if (!canMoveUp || !canMoveDown || !canMoveLeft || !canMoveRight)
    {
        Debug.LogError("REGRESSION: Player movement broken!");
        return false;
    }
    
    return true;
}

private static bool TestInput(KeyCode key)
{
    // Simulate input for 1 frame
    Input.GetKey(key); // Framework handles it
    
    // Verify hero moved in expected direction
    // (Implementation depends on your Hero class)
    return true;  // Placeholder
}
```

**Before pushing code:**
```
✓ Manual test: All 8 directions work
✓ Facing matches input (WO-363 validator)
✓ Clear to ship
```

---

## Hard Gate #3: Scene Doesn't Crash on Load

### The Rule

**Village scene loads without errors, black screens, or infinite loops.**

- [ ] Scene loads in < 5 seconds
- [ ] No console errors on load (warnings OK)
- [ ] Camera renders village properly
- [ ] Hero spawns and is visible
- [ ] No missing asset references
- [ ] No null reference exceptions

### Verification

**In Editor:**
```
✓ Open Village scene
✓ Press Play
✓ Wait 5 seconds
✓ No red errors in console
✓ Game is playable (can move, see HUD)
```

**In Build:**
```
✓ Run WebGL build
✓ Scene loads
✓ No hang/crash
```

---

## Hard Gate #4: Camera Doesn't Break Movement

### The Rule (Post-WO-367/368)

**Changing camera distance/angle MUST NOT break player movement input mapping.**

This was the regression that triggered this mandate.

**Test:**
- [ ] Camera at original angle (45° pitch) + closer distance
- [ ] Player movement still world-absolute (not camera-relative)
- [ ] Facing direction independent of camera angle
- [ ] WO-363 orientation validation passes all tests

**Before any camera-related PR:**
```
✓ Run WO-363 (Orientation Validation) tests
✓ All tests pass (UP→UP, LEFT→LEFT, etc.)
✓ Clear to ship
```

---

## Pre-Shipping Checklist (MANDATORY)

**Every work order completion must verify:**

- [ ] Tree of Life at (0, 0, 0) — verified
- [ ] Player movement works — tested all 8 directions
- [ ] Scene loads without errors — tested in editor + build
- [ ] Camera changes don't break movement — WO-363 passes
- [ ] No new null reference exceptions — console clean
- [ ] No performance regression — FPS stable
- [ ] Works in WebGL build — tested target platform

**If ANY gate fails:**
- [ ] Revert the change
- [ ] Create bug report (triage)
- [ ] Fix root cause
- [ ] Re-test all gates
- [ ] Re-submit for merge

---

## Regression Testing Workflow

### Before CLI Pushes Code

1. **CLI runs full test suite:**
   ```bash
   # VerifyTreeOrigin()
   # VerifyPlayerMovement()
   # VerifySceneLoad()
   # VerifyWO363Validation()
   # ... etc
   ```

2. **All tests must pass:**
   ```
   ✓ Tree origin: PASS
   ✓ Player movement: PASS
   ✓ Scene load: PASS
   ✓ Camera/movement coupling: PASS
   ```

3. **Only then push to build**

4. **If any test fails:**
   ```
   ✗ [BLOCKER] Tree origin FAIL at (5.2, 0, -3.1)
   ✗ Fix required. Revert work order. Retry.
   ```

---

## Critical Regression History

| Date | Issue | Impact | Root Cause |
|---|---|---|---|
| 2026-06-08 | WO-367 broke movement | Game unplayable | Camera change made movement camera-relative (bad) |
| — | Discovered in user test | Player couldn't move in town | No regression test caught it before shipping |

**Solution:** This mandate ensures it never happens again.

---

## Work Order Dependencies

**This gate applies to ALL future work orders:**

- Build mode UI improvements (WO-352–357)
- Character systems (WO-360–366)
- Arena monument (WO-369–370)
- Battle music (WO-371–372)
- **Any camera/input/movement changes: EXTRA scrutiny**

---

## No Exceptions

**Not even for:**
- "Quick bug fix"
- "Tiny change"
- "It's just audio"
- "Just testing"

**Every merge requires all gates passing.**

---

## Setup for Next Work Orders

### Template for CLI

Before marking any WO as **RESULT.md** (complete):

```markdown
## Regression Gates

- [x] Tree of Life at (0,0,0): PASS
- [x] Player movement (WASD): PASS
- [x] Scene load time: 3.2s (< 5s) PASS
- [x] Console errors: 0, warnings: 2 (acceptable) PASS
- [x] WO-363 validation: PASS (10/10 tests)
- [x] WebGL build: OK

**Status: READY TO SHIP** ✓
```

### Template for UI (Samantha)

When reviewing results:

```
✓ All regression gates passing
✓ No new bugs introduced
✓ Safe to deploy
✓ Approved for live build
```

---

## Future Enhancements to This Gate

- [ ] Automated regression test suite (CI/CD pipeline)
- [ ] Performance benchmarking (FPS regression detection)
- [ ] Asset validation (missing references)
- [ ] Memory profiler (leak detection)
- [ ] Audio sync validation (music/SFX alignment)

---

## Acceptance Sign-Off

This work order has **no code changes**. It establishes **testing discipline.**

**Acceptance:**
- [ ] All future work orders include regression gate verification
- [ ] CLI knows to run these before pushing
- [ ] No code ships without passing
- [ ] Game remains playable at all times
- [ ] No "oops we broke movement" again

---

## Contact / Escalation

**If regression gate fails:**
1. CLI: Don't push. File bug. Revert WO.
2. Samantha: Review failure + root cause.
3. Triage: Decide if gate needs adjustment or code needs fix.

**This is non-negotiable.**

---

## Final Note

> "I didn't think I'd have to say this..."

Neither did we. But now we have a safety net. Every build ships knowing:
- Tree is where it should be
- Player can move
- Game is playable
- No regressions snuck in

Ship with confidence. ✅

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `RegressionSuite.cs:709-774,944,960` — four pre-ship gates. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
