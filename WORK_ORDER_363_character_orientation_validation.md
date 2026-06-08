# WO-363: Character Orientation Validation — Hard Deployment Gate

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.5 days — testing harness + assertion)  
**Priority:** CRITICAL (regression prevention)  
**Lane:** QA / Build Verification

---

## Overview

Establish a **hard deployment gate**: character orientation (facing direction) must match movement direction before ANY build ships.

**Rule:** For every character deployment:
- ✓ Moving **UP** → Character model faces UP
- ✓ Moving **DOWN** → Character model faces DOWN
- ✓ Moving **LEFT** → Character model faces LEFT
- ✓ Moving **RIGHT** → Character model faces RIGHT
- ✓ **IDLE** → Last-known facing direction maintained

**Why:** Misaligned facing direction breaks immersion and feels buggy. Players expect visual feedback. This is non-negotiable — if orientation is wrong, the build doesn't ship.

---

## Acceptance Criteria

- [ ] Character orientation assertion runs on every movement input
- [ ] Assertion compares: `character.facing == input.direction`
- [ ] Assertion fails loudly (error log + pause in dev, warning in player build)
- [ ] Rotation animates smoothly (no instant 180° snaps)
- [ ] Idle state preserves last-known facing
- [ ] Backwards movement rotates to face intended direction (not strafe)
- [ ] Diagonal movement (UP+LEFT) rotates to 45° (matches animation)
- [ ] No tolerance for mismatch (not "close enough")
- [ ] Works in all movement contexts (village, battle, exploration)
- [ ] Can be toggled off for debug/testing (but ON by default)

---

## Implementation

### CharacterOrientationValidator.cs (New)

```csharp
/// <summary>
/// HARD GATE: Character orientation must match movement direction.
/// This assertion runs every frame and NEVER skips validation.
/// If orientation is wrong, the build is considered broken.
/// </summary>
public sealed class CharacterOrientationValidator : MonoBehaviour
{
    [SerializeField] private HeroLocomotion _hero;
    [SerializeField] private bool _enabled = true;  // Toggle for debug
    
    private Vector3 _lastValidFacing = Vector3.forward;

    private void Update()
    {
        if (!_enabled) return;
        ValidateOrientation();
    }

    private void ValidateOrientation()
    {
        var inputDir = GetInputDirection();
        var heroFacing = _hero.transform.forward;
        
        // Idle: preserve last facing
        if (inputDir == Vector3.zero)
        {
            heroFacing = _lastValidFacing;
        }
        else
        {
            // Movement: must face input direction
            float angle = Vector3.Angle(heroFacing, inputDir);
            
            // Allow small tolerance for smooth rotation (e.g., 15°)
            // but flag if misaligned
            if (angle > 30f)  // > 30° = significant mismatch
            {
                LogOrientationError(inputDir, heroFacing, angle);
            }
            
            _lastValidFacing = heroFacing;
        }
    }

    private Vector3 GetInputDirection()
    {
        var input = Input.GetAxis("Horizontal");
        var inputV = Input.GetAxis("Vertical");
        var dir = new Vector3(input, 0, inputV).normalized;
        return dir;
    }

    private void LogOrientationError(Vector3 expected, Vector3 actual, float angleDiff)
    {
        #if UNITY_EDITOR
            Debug.LogError(
                $"[ORIENTATION GATE FAILED] " +
                $"Character facing {actual} but input demands {expected}. " +
                $"Angle mismatch: {angleDiff:F1}°. " +
                $"BUILD CANNOT SHIP.",
                _hero.gameObject);
            Time.timeScale = 0f;  // Pause in editor for inspection
        #else
            Debug.LogWarning(
                $"[ORIENTATION GATE] Character facing {actual} vs input {expected}. " +
                $"Mismatch: {angleDiff:F1}°",
                _hero.gameObject);
        #endif
    }
}
```

### HeroLocomotion Integration

```csharp
public class HeroLocomotion : MonoBehaviour
{
    private void OnMovementInput(Vector3 direction)
    {
        // Rotate to face input direction
        if (direction != Vector3.zero)
        {
            var targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed);  // Smooth rotation
        }

        // ASSERTION: Facing must match input
        AssertOrientationMatches(direction);
    }

    private void AssertOrientationMatches(Vector3 inputDirection)
    {
        if (inputDirection == Vector3.zero) return;  // Idle is OK

        float angleDiff = Vector3.Angle(transform.forward, inputDirection);
        if (angleDiff > 30f)  // Tolerance for smooth rotation in progress
        {
            Debug.LogError($"[ORIENTATION ASSERTION FAILED] " +
                          $"Input: {inputDirection}, Facing: {transform.forward}, " +
                          $"Angle diff: {angleDiff}°");
        }
    }
}
```

### Unit Test Example

```csharp
[Test]
public void CharacterFacesUpWhenMovingUp()
{
    // Arrange
    var hero = Instantiate(heroPrefab);
    var validator = hero.AddComponent<CharacterOrientationValidator>();

    // Act
    SimulateInput(Vector3.forward);  // UP input
    hero.transform.forward = Vector3.forward;
    
    // Assert
    Assert.AreEqual(Vector3.forward, hero.transform.forward,
                   "Character must face UP when moving UP");
}

[Test]
public void CharacterFacesLeftWhenMovingLeft()
{
    // Act
    SimulateInput(Vector3.left);  // LEFT input
    hero.transform.forward = Vector3.left;
    
    // Assert
    Assert.AreEqual(Vector3.left, hero.transform.forward,
                   "Character must face LEFT when moving LEFT");
}

[Test]
public void CharacterFacesDownWhenMovingDown()
{
    // Act
    SimulateInput(Vector3.back);  // DOWN input
    hero.transform.forward = Vector3.back;
    
    // Assert
    Assert.AreEqual(Vector3.back, hero.transform.forward,
                   "Character must face DOWN when moving DOWN");
}

[Test]
public void CharacterPreservesLastFacingWhenIdle()
{
    // Arrange: Hero moved UP, now idle
    SimulateInput(Vector3.zero);  // Idle
    
    // Assert: Last facing (UP) is preserved
    Assert.AreEqual(Vector3.forward, hero.transform.forward,
                   "Character must preserve last-known facing when idle");
}
```

---

## Deployment Gate Logic

### Pre-Deployment Checklist (in CLI / Editor)

```csharp
// Before shipping any build:
public static bool ValidateCharacterOrientationBeforeShip()
{
    var validator = FindObjectOfType<CharacterOrientationValidator>();
    if (validator == null)
    {
        Debug.LogError("[DEPLOYMENT GATE] CharacterOrientationValidator not in scene!");
        return false;
    }

    // Run full test cycle (all directions)
    var directions = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
    foreach (var dir in directions)
    {
        if (!TestDirection(dir))
        {
            Debug.LogError($"[DEPLOYMENT GATE] Failed on direction: {dir}");
            return false;
        }
    }

    Debug.Log("[DEPLOYMENT GATE] ✓ Character orientation validation PASSED. Safe to ship.");
    return true;
}
```

### Build Script Hook (Batchmode)

```csharp
// In Editor prebuild script:
[PreBuildMethod]
public static void EnsureCharacterOrientationValid()
{
    var scene = SceneManager.GetActiveScene();
    var validator = FindObjectOfType<CharacterOrientationValidator>();
    
    if (validator == null || !validator.enabled)
    {
        throw new System.Exception(
            "DEPLOYMENT GATE FAILED: CharacterOrientationValidator must be enabled. " +
            "Character orientation is not validated. Build aborted.");
    }
}
```

---

## Testing Scenarios

| Scenario | Expected | Gate Result |
|----------|----------|-------------|
| Move UP | Face UP | ✓ PASS |
| Move DOWN | Face DOWN | ✓ PASS |
| Move LEFT | Face LEFT | ✓ PASS |
| Move RIGHT | Face RIGHT | ✓ PASS |
| Move UP+LEFT (diagonal) | Face 45° NW | ✓ PASS |
| Idle after UP | Face UP (preserved) | ✓ PASS |
| Rotate 180° during move | Face new direction (smooth) | ✓ PASS |
| Input says UP, facing DOWN | MISMATCH | ✗ FAIL |
| Model skips rotation frames | Angle > 30° | ✗ FAIL |

---

## Regions Where This Applies

- ✓ Village (hero free movement)
- ✓ Battle (hero movement during combat)
- ✓ Exploration / World (hero entering outposts)
- ✓ All character types (hero, companions, pets that move)
- ✓ Enemies (should face direction of movement for consistency)

---

## What This BLOCKS

If orientation validation fails, the build **cannot ship**. Period.

This is a **hard gate**, not a warning. Violation = rejected build.

---

## Configuration

**In CharacterOrientationValidator:**
- `enabled`: Toggle validation on/off (always ON for shipping)
- `toleranceAngle`: 30° (smooth rotation tolerance)
- `rotationSpeed`: How fast character turns (tune for feel)

---

## No Exceptions

This rule applies to:
- Every frame of every scene
- Every character type
- Every movement system
- Every deploy

**There is no "close enough"** when it comes to orientation. Either the character faces the right direction or the build is broken.

---

## Acceptance Sign-Off

- [ ] CharacterOrientationValidator implemented and enabled
- [ ] Assertion logic works (UP/DOWN/LEFT/RIGHT tested)
- [ ] Unit tests pass (all 4 directions + idle + diagonal)
- [ ] Pre-deployment gate prevents bad builds
- [ ] Works in WebGL build (no skipped frames)
- [ ] No character can ship facing wrong direction
