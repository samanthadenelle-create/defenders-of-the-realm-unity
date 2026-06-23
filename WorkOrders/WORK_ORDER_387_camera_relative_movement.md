# WORK_ORDER_387 — Camera-relative movement in 3rd-person follow mode

**Status:** DONE — owner-playtested working 2026-06-09 (commit acb2c80; compiled + ran in editor).
**Lane:** Camera / Hero (DeNelle.Village.Hero) — `HeroLocomotion.cs`. Code-only.
**Source:** This session. **Related:** WO-385 (camera occluder-fade — sibling castle-camera fix), WO-368/367 (the world-absolute history this reconciles).

## Problem
With the orbiting `SmartMobileCamera`, movement was **world-absolute** (WO-368: Up=+Z always, ignores camera). As the player walked around the castle and the camera faced a different way, WASD no longer matched the view — "I have to change my logic as I walk; the camera fights it." (Owner-reported, all-session pain.)

## Why it had been world-absolute (the history)
WO-367 made movement camera-relative GLOBALLY → a camera-MODE change re-rotated the input mapping and broke town. WO-368 reverted to world-absolute for a fixed Up=+Z contract across town/battle. That fixed the mode-break but left controls fighting the orbiting camera.

## Fix (reconciliation — mode-aware, curl-safe)
`HeroLocomotion` now rotates the world input by the follow camera's yaw:
```csharp
if (_smartCamera == null) _smartCamera = Object.FindObjectOfType<SmartMobileCamera>();
float yaw = _smartCamera != null ? _smartCamera.CameraYaw : 0f;
Quaternion cameraRotation = Quaternion.Euler(0f, yaw, 0f);
Vector3 move = cameraRotation * new Vector3(input.x, 0f, input.y);
```
The reconciliation hinges on `SmartMobileCamera.CameraYaw` (`SmartMobileCamera.cs:283` — `=> _orbitBehind ? _panYaw : 0f`):
- **3rd-person follow mode** → returns the player-pan yaw → input is camera-relative (UP = where the camera faces). ✓ the fix.
- **Top-down / legacy / build mode** → returns **0** → input is unrotated → collapses to the WO-368 world-absolute contract. So a camera-mode change STILL can't break input (WO-363 intent preserved) — the thing that sank WO-367 is structurally avoided.
- **Curl-safe:** `CameraYaw` is pan-driven only, never velocity-driven, so the `{yaw←velocity}` feedback edge that caused the old "always-turn-left" spiral stays absent.
- `_smartCamera` cached in `Start`; lazy re-fetched if null so the fix engages even when the camera wires up after the hero.

## Result
Owner playtested in `MainCastle_Hall`: **"YES that worked!"** Controls now follow the camera; no more remapping while walking.

## What NOT to touch / carry forward
- Do NOT revert `HeroLocomotion` to pure world-absolute — that's the bug this fixes. The `CameraYaw`-returns-0-in-top-down design is what keeps town safe; keep it.
- Keep `CameraYaw` pan-driven (never velocity) or the curl returns.
- Sibling lever if the camera should also auto-swing behind the hero's facing: `SmartMobileCamera._facingRecenterEnabled` (currently off).
