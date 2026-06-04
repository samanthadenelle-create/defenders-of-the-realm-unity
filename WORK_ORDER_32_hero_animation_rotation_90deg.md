# WORK ORDER 32 — Hero Animation Facing 90° Off

**Status:** READY TO IMPLEMENT — single-line fix
**Date:** 2026-05-26
**Author:** Bug triage — playtest report
**Priority:** High — hero visually faces the wrong direction when moving

---

## Problem

> "Motion animations are off 90 degrees — if I press left player faces up,
> if down faces left, so on."

Every directional input causes the hero's visible body to face 90° counter-clockwise
from the intended direction:

| Key pressed | Expected facing | Actual facing |
|---|---|---|
| Left (A) | West | North |
| Down (S) | South | West |
| Right (D) | East | South |
| Up (W) | North | East |

---

## Root Cause

`HeroBodySwapper.cs` applies a Y-axis rotation to the hero mesh body to correct
for the Tripo FBX forward-direction:

```csharp
// HeroBodySwapper.cs line 62
float yaw = NeedsForwardFlip(cls) ? 180f : 0f;
body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
```

The comment says: *"Tripo FBXs export with their forward along -Z, so they need a
180° correction."* However, the actual exported FBXs used in this build have their
visual forward along **+X** (90° from Unity's standard +Z), not -Z. This means:

- The correct offset to align the model's +X visual forward with Unity's +Z is **-90°**
- The current code applies **180°**, which over-rotates by 90°
- Net error: every frame the body faces 90° CW of where it should face (equivalently,
  input appears rotated 90° CCW to the player)

`HeroLocomotion` drives the root transform correctly via `Quaternion.LookRotation(Velocity)` —
the issue is only in the body child's correction offset applied by `HeroBodySwapper`.

---

## Fix

### `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` — line 62

```csharp
// BEFORE (wrong — over-rotates, produces 90° CCW visual error):
float yaw = NeedsForwardFlip(cls) ? 180f : 0f;

// AFTER (correct — aligns +X visual forward with root +Z):
float yaw = NeedsForwardFlip(cls) ? -90f : 0f;
```

Also update the comment on `NeedsForwardFlip` (line 287–291) to reflect the actual
Tripo export axis:

```csharp
// BEFORE:
// All three heroes are Tripo AI exports (-Z forward) — they need a
// 180° yaw correction so they face the move direction instead of the camera.

// AFTER:
// All three heroes are Tripo AI exports (+X forward, confirmed 2026-05-26) —
// they need a -90° yaw correction to align with Unity's +Z forward convention.
private static bool NeedsForwardFlip(HeroClass cls) => true;
```

### Verification step

After making the change, play the scene and press each direction key:
- W / Up arrow → hero faces away from camera (North / +Z)
- D / Right arrow → hero faces right (East / +X)
- S / Down arrow → hero faces toward camera (South / -Z)
- A / Left arrow → hero faces left (West / -X)

If the result is still off by 90° in the opposite direction, the actual forward in the
FBX is -X rather than +X — use `+90f` instead of `-90f`.

---

## Files to Edit

- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs`
  - Line 62: `180f` → `-90f`
  - Lines 287–291: update comment

---

## Notes

- `HeroLocomotion.cs` does NOT need to change — its input mapping and
  `LookRotation` call are correct.
- This fix applies to all three hero classes (Mage, Knight, Ranger) since
  `NeedsForwardFlip` returns `true` for all of them.
- **No scene re-bake required** — `HeroBodySwapper` runs at runtime (in `Start()`),
  so the fix is live after recompile without rebuilding the village scene.

---

## Acceptance Criteria

- [ ] Pressing A/Left → hero walks and faces West
- [ ] Pressing D/Right → hero walks and faces East
- [ ] Pressing W/Up → hero walks and faces North (away from default camera)
- [ ] Pressing S/Down → hero walks and faces South (toward default camera)
- [ ] Animation transitions (Idle → Walk) still fire correctly
- [ ] Fix applies for all three hero classes in the character select screen
