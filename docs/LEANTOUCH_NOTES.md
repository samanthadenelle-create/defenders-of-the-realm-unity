# Lean Touch / CW — Touch Input Notes

Carlos Wilkes (CW) cross-platform touch/mouse input framework. Unifies mouse,
touch, and pen into one "finger" model with gestures (tap/swipe/drag/pinch).
Root: `Assets/Plugins/CW/`. Vendored (examples trimmed per project memory).
Sources read: folder tree, `LeanTouch.cs`, project drivers.

## Asmdefs a consumer must reference
Three (all present under `Assets/Plugins/CW/`):
- `LeanTouch` (`LeanTouch/LeanTouch.asmdef`) — namespace `Lean.Touch`
- `LeanCommon` (`LeanCommon/LeanCommon.asmdef`) — namespace `Lean.Common`
- `CW.Common` (`Shared/Common/CW.Common.asmdef`) — shared base

Add all three to the consuming asmdef's references (this is how the Village/PatriciaLight
asmdef pulls it in).

## Core API (what the project actually uses)
- **`LeanTouch`** — drop ONE in the scene; it's the manager. Static C# events
  (subscribe in `OnEnable`, unsubscribe in `OnDisable`):
  `OnFingerDown / OnFingerUpdate / OnFingerUp / OnFingerTap / OnFingerSwipe /
   OnGesture(List<LeanFinger>)`.
- **`LeanFinger`** — one touch/cursor: `ScreenPosition`, `LastScreenPosition`,
  `ScreenDelta`, `StartScreenPosition`, `Age`, `Tap/Swipe` flags, plus
  `GetWorldPosition(distance)` helpers.
- **`LeanTouch.GetFingers(ignoreGui, requireFinger, count)`** — filtered current
  finger list (e.g. `GetFingers(true, true, 2)` = ignore-GUI, exactly 2 fingers → pinch).
- **`LeanGesture`** — static math over a finger list: `GetPinchScale(fingers)`,
  `GetScreenCenter`, `GetScreenDelta`, `GetTwistDegrees`, etc.

### Project examples
- `Assets/_Modules/Village/PatriciaLight/LeanTouchAimDriver.cs` — subscribes to
  `OnFingerUpdate`, drives aim; uses `GetFingers(true,true,2)` + `GetPinchScale` for zoom.
- `BuildMode/LeanTouchBuildDriver.cs`, `Hero/CameraPanInput.cs`,
  `PatriciaLight/HeroOverShoulderCamera.cs`, `TowerAimSystem.cs` — other consumers.
- Architecture rule (project memory): only the `*LeanTouch*Driver` classes touch
  `Lean.Touch`; the core aim/build systems are input-agnostic. Keep it that way.

## Gotchas
- A scene with no `LeanTouch` manager component fires no events — add one (the
  scene builders do this).
- Active Input Handling must include the legacy/old input or the appropriate backend
  LeanTouch expects; this project runs Input Handling = **Both**, which works.
- Examples/demo content was trimmed from the vendored copy — don't expect the sample
  scenes; reference the source scripts directly.

## Doc sources
- `Assets/Plugins/CW/READ ME.txt`, `LeanTouch/Required/Documentation.html`,
  `LeanCommon/Required/Documentation.html`
- Vendor: https://carloswilkes.com / Lean Touch (Unity Asset Store)
