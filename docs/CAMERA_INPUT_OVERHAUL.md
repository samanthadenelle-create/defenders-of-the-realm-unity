# CAMERA + INPUT OVERHAUL — Authoritative Design (DEF-202 / DEF-204)

**Status:** READY TO IMPLEMENT
**Scope:** Village + OuterWorld gameplay only. DTT/PatriciaLight scene is OUT OF SCOPE (it owns its own camera + Lean aim).
**Owner-decision basis:** synthesizes the Camera-Authority, Input/Controller, and Reference-Pattern investigations. The implement phase follows this verbatim.

---

## 0. Goal, in one paragraph

Give the village/open-world hero a true third-person follow camera that the **player** swings (slide-to-pan touch + right-mouse on desktop), with stick/joystick movement **rebased to the camera yaw** ("up = up-screen"), coexisting with the existing bottom-left joystick, the tap-to-attack, and build mode — and with a **hard structural guarantee that the old "always turn left" curl/spiral cannot return**. Every piece is flag-guarded so the owner can A/B it.

We adopt **Architecture A (player-authoritative yaw)** from the Reference Pattern: the player sets camera yaw; movement is rebuilt from that yaw each frame; the camera yaw **never reads hero velocity**. This is the only architecture that gives both camera-relative control AND a non-negotiable stability guarantee.

---

## 1. Camera authority — ONE camera, the competing rig neutralized

### 1.1 The authoritative camera
**`SmartMobileCamera`** (`Assets/_Modules/Village/Hero/SmartMobileCamera.cs`, attached to the Village scene's Main Camera) is and remains the **sole** gameplay camera authority for Village + OuterWorld.

- OuterWorld.unity has **no camera**; it loads additively over Village, so the Village Main Camera is always the gameplay camera. There is exactly one gameplay camera GameObject.
- `SmartMobileCamera.Awake` already disables the legacy `VillageCamera` component on the same GameObject (`SmartMobileCamera.cs:216-223`).
- `EnforceSoleCamera()` (`SmartMobileCamera.cs:541-555`) pins `_cam.depth = 100f` and disables every other non-RenderTexture screen camera each frame (called from `Start` and `LateUpdate`). This is the neutralization mechanism for any rogue rig.
- `SmartMobileCamera.Instance` (static, `:206`) is the live handle the rebased movement reads.

### 1.2 The competing / world-locked rigs — leave dormant, do NOT revive
- `VillageCamera` — disabled at runtime by `SmartMobileCamera.Awake`. Leave it; it is already neutralized.
- `HeroCinemachineRig` — not attached (builder call commented at `VillageSceneBuilder.Characters.cs:118`). Leave dead.
- `CinemachineCameraController` — never added by any builder. Leave dead.
- `HeroOverShoulderCamera` / `ThirdPersonCameraFollow` / `DefenderCameraScript` / `FirstPersonTowerCamera` — PatriciaLight/Defend-mode only, managed by `PatriciaLightController`. **Do not touch.** Our new pan driver must be Village/OuterWorld-scoped and must NOT run in the DTT scene (see §4.4).

**Net:** the architecture is already single-authority. We do **not** add a new camera. All camera changes happen *inside* `SmartMobileCamera`. We do not re-enable any Cinemachine rig.

### 1.3 Why the yaw is world-locked today
The seat is a position-only offset: `Vector3 desired = _target.position + zoomOffset` (`:342`), where `zoomOffset = _followOffset(0,3.5,-6)` is a constant world vector (`:323`). The hero's rotation is never applied. The only block that would rotate it (`:331-340`) is gated on `_orbitBehind`, which **defaults `false`** (`:129`). View rotation comes from `AimAt(_leadPoint)` (`:534-539`), a `LookRotation` that keeps the hero centered but never orbits the seat.

### 1.4 Why the old orbit spiraled (the curl) — the failure we must structurally exclude
The old `_orbitBehind` block chased the hero's **velocity heading**: `heroVelFlat = GetHeroVelocity()` (`:329`, reads `HeroLocomotion.Velocity`), `targetYaw = Atan2(heroVelFlat.x, heroVelFlat.z)` (`:336`), smoothed into `_orbitYaw` (`:337`), offset rotated by `Quaternion.Euler(0,_orbitYaw,0)` (`:339`). Combined with camera-relative movement this is a closed loop: cam yaw → movement basis → hero velocity → cam yaw → diverges into a curl ("always turn left"). The in-code comment at `:124-128` documents exactly this.

**The root-cause rule we enforce forever:** *At most one of `{yaw ← velocity}` and `{move ← yaw}` may exist. Never both.* We choose `{move ← yaw}` (Architecture A) and therefore the camera yaw must be driven by **player input only** (and optionally hero *facing* for recenter), **never** by hero velocity. `GetHeroVelocity()` is removed from the yaw path entirely.

---

## 2. The orbit-behind follow math (player-authoritative, damped, non-spiraling)

We repurpose the existing yaw slot (`:323`/`:331-340`) so the rotation is driven by an explicit, player-controlled `_panYaw`, not velocity.

### 2.1 New state on `SmartMobileCamera`
Add near the runtime-state block (`~:189`):
```csharp
// Player-authoritative camera yaw (deg) — written ONLY by pan input / facing-recenter.
// NEVER a function of hero velocity. This is the single yaw authority for both the
// camera seat and HeroLocomotion's movement basis (read via CameraYaw).
private float _panYaw;
private float _panPitch;          // optional, clamped
private float _timeSinceLastDrag; // for the gentle facing-recenter (off by default)

// Public read for HeroLocomotion (camera-relative movement) — see §3.
public float CameraYaw => _orbitBehind ? _panYaw : 0f;
```
`CameraYaw` returns `0` when orbit is OFF so movement stays world-relative in the legacy mode (A/B parity — §5).

### 2.2 New public API (the single writer entry points)
```csharp
/// <summary>Player drag/right-stick yaw delta (deg). The ONLY way external input rotates the view.</summary>
public void AddYaw(float deg)
{
    _panYaw += deg;
    _timeSinceLastDrag = 0f;
}

/// <summary>Optional pitch from vertical drag; clamped to a safe band.</summary>
public void AddPitch(float deg)
{
    _panPitch = Mathf.Clamp(_panPitch + deg, _panPitchMin, _panPitchMax);
    _timeSinceLastDrag = 0f;
}
```
Serialized tuning fields (with `_orbitBehind` group): `_panPitchMin = -10f`, `_panPitchMax = 35f`, `_facingRecenterEnabled = false`, `_facingRecenterDelay = 2f`, `_facingRecenterSpeed = 90f` (deg/s).

### 2.3 Rewrite the yaw block (`SmartMobileCamera.cs:325-340`)
Replace the velocity-chasing body with:
```csharp
if (_orbitBehind)
{
    if (!_orbitYawInit) { _panYaw = _target.eulerAngles.y; _orbitYawInit = true; }

    // Gentle facing-recenter (OFF by default). Targets the hero's FACING, never velocity,
    // and is suspended while the player is actively dragging. Converges (loop gain < 1)
    // because recenterSpeed*dt is small and gated by an idle delay.
    _timeSinceLastDrag += dt;
    if (_facingRecenterEnabled && _timeSinceLastDrag > _facingRecenterDelay)
        _panYaw = Mathf.MoveTowardsAngle(_panYaw, _target.eulerAngles.y, _facingRecenterSpeed * dt);

    zoomOffset = Quaternion.Euler(_panPitch, _panYaw, 0f) * zoomOffset;
}
```
- Delete the `GetHeroVelocity()` call at `:329` from the yaw path. (It is still used for the lead point at `:362-367`; keep that single call but move it below the yaw block, or guard it — it must NOT feed `_panYaw`.)
- `_orbitYaw`/`_orbitYawSpeed`/`_orbitMoveThreshold` fields become unused for yaw; leave the fields to avoid serialization churn but they no longer drive rotation.
- Everything downstream is unchanged: `desired = _target.position + zoomOffset` (`:342`), `ApplyCollision` (`:352`/`:472`), `SmoothDamp` (`:354`), shake (`:356`), FOV (`:360`), `AimAt(_leadPoint)`.

### 2.4 The stability guarantee — WHY it cannot spiral
`_panYaw` is a pure accumulator of **player input** (`AddYaw`) plus an optional damped pull toward **hero facing** (`MoveTowardsAngle`). It has **zero dependency on hero velocity, position, or `MoveIntent`**. Therefore:
- Hold the stick constant → `CameraYaw` is constant → HeroLocomotion's movement basis (§3) is constant → the hero walks a straight world line → which projects to a straight screen line under a stationary camera. **Fixed point in one step; no rotational state can accumulate.** This is exactly as robust as today's world-relative code — it merely lets the player choose the heading.
- The facing-recenter is the only term that reads anything hero-derived, and it reads **facing, not velocity**, with strong damping, an idle gate, and full suspension during drag — loop gain < 1, so it converges instead of diverging. It ships **OFF** (`_facingRecenterEnabled = false`); the conservative default has the camera hold its angle until the player drags it.

The invariant `{move ← yaw}` holds and `{yaw ← velocity}` is structurally absent. The curl is impossible.

---

## 3. Camera-relative movement (HeroLocomotion rebased to camera yaw)

### 3.1 The change point
`HeroLocomotion.cs:239` currently builds a **world-relative** vector:
```csharp
Vector3 move = new Vector3(input.x, 0f, input.y);
```
Replace with a camera-yaw-relative basis, reading the single yaw authority:
```csharp
// Camera-relative movement (DEF-204): up on stick = away from camera = up-screen.
// We read the CAMERA'S yaw — an explicit player-controlled value — NEVER the hero's
// travel heading. That is the half of the loop we are allowed to keep; the camera yaw
// half (yaw<-velocity) is structurally absent (SmartMobileCamera._panYaw). Together
// these satisfy "at most one of {yaw<-velocity, move<-yaw}", so the old curl cannot return.
var cam = SmartMobileCamera.Instance;
float camYaw = cam != null ? cam.CameraYaw : 0f;   // 0 in legacy/world-locked mode -> identical to old behaviour
Quaternion basis = Quaternion.Euler(0f, camYaw, 0f);
Vector3 move = basis * new Vector3(input.x, 0f, input.y);
if (move.sqrMagnitude > 1f) move.Normalize();
```
- `cam == null` guard is required (DTT scene has no `SmartMobileCamera`); falls back to world-relative.
- When `_orbitBehind == false`, `CameraYaw` returns `0`, so `basis` is identity and behaviour is **byte-identical** to today — this is what makes §5's A/B clean.
- Everything below `:239` (the `MoveTowards` velocity smoothing at `:246-250`, NavMesh move, facing slerp) is unchanged. The hero still faces its `move` direction; because `move` is now camera-relative, the hero faces "up-screen" when you push up — correct TPS feel.

### 3.2 Why this does not reintroduce the loop
The hero's facing/velocity is derived from `move`, and `move` is derived from `CameraYaw`. But `CameraYaw` (`_panYaw`) is NOT derived from facing or velocity (it is player-input + optional damped facing-recenter only). The data flow is strictly one-directional `input → _panYaw → move → hero velocity/facing`. No back-edge exists.

---

## 4. Slide-to-pan touch control (Lean Touch, coexisting)

### 4.1 Touch API choice
Use **Lean Touch** (already referenced by `DeNelle.Village.asmdef:16-18` — no asmdef change). New MonoBehaviour `CameraPanInput` in `DeNelle.Village`, self-bootstrapping (pattern: `LeanTouchAimDriver.cs:39-69`):
- `if (LeanTouch.Instance == null) gameObject.AddComponent<LeanTouch>();`
- `LeanTouch.OnFingerUpdate += HandleFinger;` in `OnEnable`, `-=` in `OnDisable`.
- Bootstrap itself via `[RuntimeInitializeOnLoadMethod]` + DontDestroyOnLoad + only when a `HeroLocomotion` exists in scene (mirror `VirtualJoystick`'s self-spawn so no scene-builder edit is needed). Guard: only spawn in Village/OuterWorld — abort if the active scene is the PatriciaLight/DTT scene (check by scene name) so we never double-drive with `LeanTouchAimDriver`.

### 4.2 Zone / finger split (the contract)
Per `LeanFinger f` in `HandleFinger`:
1. **Reject** if `f.IsOverGui` (HUD/buttons consume first).
2. **Reject** if `BuildModeController.Instance != null && BuildModeController.Instance.IsActive` (`BuildModeController.cs:39`) — build mode owns whole-screen taps + the top-down overview (§4.5).
3. **Reject** if `f.StartScreenPosition` is inside the **joystick zone** — replicate `VirtualJoystick`'s test (`VirtualJoystick.cs:95,126-142`): a circle of radius `_radius*1.7` centred at `(_radius*1.35, _radius*1.35)` px from bottom-left. Expose this as a `static bool VirtualJoystick.IsInZone(Vector2 screenPos)` helper so the math lives in one place and can't drift. (The joystick reads legacy `Input.GetTouch(0)` and will grab finger-0 in its zone regardless; we simply never claim a finger that *started* there.)
4. Otherwise this is a **pan candidate**.

We do NOT hard-split left-40/right-60; instead we use *start-zone exclusion* (joystick zone + GUI + build mode). This makes "slide-to-pan everywhere outside the joystick" true, which is the owner's ask. A finger keeps its role for its lifetime (track by `f.Index`).

### 4.3 Tap-vs-drag (so tap-to-attack survives)
`HeroAbilityInput.cs:64-68` and `PlayerAttackController.cs:137` both fire attack on left-click/tap. A stationary tap must pass through to them; only a real drag pans:
- Accumulate `|f.ScreenDelta|` since the finger's start. Until it exceeds `_dragThresholdPx` (≈ 12px), do nothing (the tap reaches the attack consumers normally).
- Once past threshold, mark the finger "panning" (a `HashSet<int>` of claimed indices) and from then on feed deltas to the camera; the attack consumers see no `GetMouseButtonDown` because a held drag is not a fresh down-edge.

### 4.4 Drag → yaw/pitch (writes the single authority)
```csharp
// only for fingers in the "panning" set:
SmartMobileCamera.Instance?.AddYaw(f.ScreenDelta.x * _dragSensX);   // _dragSensX ~ 0.15 deg/px
SmartMobileCamera.Instance?.AddPitch(-f.ScreenDelta.y * _dragSensY); // _dragSensY ~ 0.10 deg/px, clamped in API
```
Pan and the movement-rebase therefore read/write the **same** `_panYaw` — one yaw authority. `AddYaw` resets `_timeSinceLastDrag`, which is the "drag-overrides-follow" mechanism: while the player drags, the facing-recenter is suspended; after `_facingRecenterDelay` of no drag, the gentle recenter resumes (only if `_facingRecenterEnabled`). This is the FOLLOW → DRAGGING → SETTLING state machine, implemented implicitly via `_timeSinceLastDrag` rather than an explicit enum.

### 4.5 Build mode
While `BuildModeController.IsActive`, `CameraPanInput` is fully suppressed (rejected at step 2). Build mode keeps its own whole-screen tap + top-down overview (`BuildModeController.cs:169-282`, `PullCameraBack:647-690`). No change to BuildModeController.

### 4.6 Desktop parity (free)
In `CameraPanInput.Update`, mirror the `TowerAimSystem.DesktopFallback` pattern: while `Mouse.current.rightButton.isPressed`, feed `Mouse.current.delta` to `AddYaw`/`AddPitch` with the same sensitivities. Right-mouse-drag = orbit on desktop; left-click stays attack. (Right-stick on gamepad can also call `AddYaw` if desired — same single entry point.)

---

## 5. Flag-guards for owner A/B testing

Every piece is independently toggleable; the conservative default = today's shipped feel.

| Flag | Location | Default | OFF behaviour | ON behaviour |
|---|---|---|---|---|
| `_orbitBehind` | `SmartMobileCamera.cs:129` (existing) | **false** | `CameraYaw`=0 → world-locked seat **and** world-relative movement (byte-identical to today) | player-authoritative orbit seat + camera-relative movement |
| `_facingRecenterEnabled` | `SmartMobileCamera` (new) | **false** | camera holds the player's last yaw until next drag | gentle auto-swing-behind toward hero facing after idle delay |
| `_panEnabled` | `CameraPanInput` (new) | **true** (but inert unless `_orbitBehind`) | no touch/mouse pan; view stays where follow math puts it | slide-to-pan + right-mouse-drag active |
| `_dragSensX/Y`, `_dragThresholdPx`, `_panPitchMin/Max`, `_facingRecenterDelay/Speed` | tuning fields | per §2.2/§4 | — | feel tuning, no logic change |

**Key coupling:** because `CameraYaw` gates on `_orbitBehind`, flipping that ONE flag A/Bs the entire feature (orbit seat + camera-relative movement together), which is exactly how the two halves must move in lockstep to preserve the invariant. The owner can ship with `_orbitBehind=false` at any moment and get the known-good world-relative build with zero code revert.

---

## 6. Exact files to change

1. **`Assets/_Modules/Village/Hero/SmartMobileCamera.cs`** — the load-bearing edit.
   - Add state: `_panYaw`, `_panPitch`, `_timeSinceLastDrag` (`~:189`).
   - Add serialized tuning: `_panPitchMin/Max`, `_facingRecenterEnabled/Delay/Speed` (with the `_orbitBehind` group `~:129`).
   - Add public `float CameraYaw => _orbitBehind ? _panYaw : 0f;` and methods `AddYaw(float)`, `AddPitch(float)`.
   - Rewrite the yaw block `:325-340`: seed `_panYaw` from `_target.eulerAngles.y` on first frame; apply facing-recenter (gated/off); `zoomOffset = Quaternion.Euler(_panPitch,_panYaw,0)*zoomOffset`. **Remove velocity from the yaw path** (no `GetHeroVelocity()` feeding `_panYaw`).
   - Leave `ApplyCollision` (`:472`), `AimAt` (`:534`), `EnforceSoleCamera` (`:541`), shake, lead point (`:362-367`) unchanged.

2. **`Assets/_Modules/Village/Hero/HeroLocomotion.cs`** — movement rebase.
   - Replace the world-relative vector at `:239` with the camera-yaw basis (§3.1), reading `SmartMobileCamera.Instance?.CameraYaw` with a null fallback to `0`. Nothing else changes.

3. **`Assets/_Modules/Village/Hero/VirtualJoystick.cs`** — expose zone test.
   - Add `public static bool IsInZone(Vector2 screenPos)` computing the same circle used at `:95,126-142`, so `CameraPanInput` shares one source of truth. No behavioural change to the joystick.

4. **`Assets/_Modules/Village/Hero/CameraPanInput.cs`** — NEW file (`DeNelle.Village`).
   - Self-bootstrap (`[RuntimeInitializeOnLoadMethod]` + DDOL, only with a `HeroLocomotion` present, abort in DTT scene).
   - Lean bootstrap + `OnFingerUpdate` handler with the 4-step zone/GUI/build/joystick rejection (§4.2), tap-vs-drag threshold (§4.3), `AddYaw`/`AddPitch` feed (§4.4).
   - `Update` desktop fallback: right-mouse-drag → `AddYaw`/`AddPitch` (§4.6).
   - `_panEnabled`, `_dragSensX`, `_dragSensY`, `_dragThresholdPx` serialized.
   - **No asmdef change** (`DeNelle.Village.asmdef` already refs LeanTouch/LeanCommon/CW.Common).
   - Brace-balance gate required (CLAUDE.md §1) since it is a new `.cs`.

**Not touched:** any `.unity` scene file (CameraPanInput self-spawns; SmartMobileCamera/VirtualJoystick already attached), `BuildModeController`, any PatriciaLight/DTT camera or `LeanTouchAimDriver`, any Cinemachine rig, `VillageSceneBuilder`.

---

## 7. Owner-playtest-confirmed (feel) vs deterministic

**Deterministic (verify by code/build, not by feel):**
- `SmartMobileCamera` remains the sole camera (`EnforceSoleCamera` + depth=100 unchanged).
- With `_orbitBehind=false`: movement and seat are byte-identical to the current shipped build (`CameraYaw`→0, identity basis).
- The yaw path has no reference to `GetHeroVelocity()`/`MoveIntent` → the `{yaw←velocity}` half is structurally absent → curl is impossible (compile-checkable: grep the yaw block for velocity reads = none).
- Tap-vs-drag threshold means a sub-12px tap still reaches `HeroAbilityInput`/`PlayerAttackController` (attack fires).
- Build mode + GUI + joystick-zone fingers are rejected from pan (no placement-tap theft).
- Brace balance passes on every edited/new `.cs`.

**Owner-playtest-confirmed (feel — tune after first build, Tricia runs it):**
- `_dragSensX/Y` (orbit speed per pixel), `_dragThresholdPx`, `_smoothTime`/`posSmoothTime` (swing smoothness).
- Whether `_facingRecenterEnabled` should ship ON and, if so, `_facingRecenterDelay`/`_facingRecenterSpeed` (the lazy "swing behind me as I round the castle" auto-recenter). Default OFF; enable only if the owner wants auto-recenter and confirms it feels good.
- `_panPitchMin/Max` band (how much vertical look is allowed).
- Final call on `_orbitBehind` default (ship ON for true TPS, or hold OFF as the known-good fallback) — owner decides from the A/B build.

---

## 8. Implementer checklist (CLAUDE.md §10)
- [ ] Brace balance passes on `SmartMobileCamera.cs`, `HeroLocomotion.cs`, `VirtualJoystick.cs`, new `CameraPanInput.cs`.
- [ ] No `.unity` scene hand-edited.
- [ ] No `System.Reflection` introduced.
- [ ] Yaw block contains zero hero-velocity reads (grep-verify).
- [ ] `?.` used on every `SmartMobileCamera.Instance` cross-call.
- [ ] `CameraYaw` gates on `_orbitBehind` so the A/B flag moves both halves together.
- [ ] `CameraPanInput` aborts in the DTT/PatriciaLight scene.
