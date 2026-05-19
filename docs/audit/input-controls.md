# Input Controls Audit — On-Screen Controls & D-Pad Sensitivity

**Date:** 2026-05-19
**Scope:** Mobile input review for *Defenders of the Realm* v2 (Unity 6) — target
device the **Solana Seeker** (Android phone, no physical buttons, no controller).
**Type:** Read-only design/audit. This document drives an implementation pass; it
changes no code.
**Focus (owner directive):** on-screen controls and **D-pad sensitivity**.

---

## 1. Current state — what input exists today

### 1.1 Summary verdict

> **There are NO on-screen controls today.** No virtual joystick, no D-pad, no
> touch ability buttons, no touch movement layer. This is the headline gap.

Everything playable on a phone right now is **raycast tap** (dungeon) or **not
wired at all** (hero abilities, which are keyboard-only by design and have no
keyboard reader either). A Seeker player currently cannot cast Q/W/E/R and cannot
move the village hero at all.

### 1.2 What is wired — file by file

**`Assets/_Modules/Dungeons/DungeonHero.cs`** — dungeon Keeper locomotion.
- `CharacterController`-based; one `_controller.Move()` per frame (planar slide +
  gravity).
- **Two input schemes, keyboard wins** (`ResolveDesiredDirection`):
  - **Desktop:** WASD / arrow keys → `SampleDesktopMove()` reads
    `Keyboard.current` directly, builds a camera-relative unit vector on XZ.
  - **Touch / mouse:** `TryGetTapScreenPosition()` reads
    `Touchscreen.current.primaryTouch.press.wasPressedThisFrame` (or left mouse),
    raycasts onto `_walkableMask`, arms a straight-line tap-to-move walk to the
    hit point. `_arriveDistance` (0.25) stops the jitter on arrival.
  - Any held WASD cancels an in-flight tap target.
- Tuning fields already present: `_moveSpeed` 4.2, `_acceleration` 28,
  `_turnSpeed` 720, `_gravity` 22, `_arriveDistance` 0.25, `_tapRayLength` 200,
  `_walkableMask`.
- **No on-screen control. No analog touch input.** Tap-to-move is the *only*
  touch path, and it is binary (a point, walk there) — there is no D-pad or
  joystick feeding `ResolveDesiredDirection()` a graded vector.
- Input is **low-level device polling**, not an `.inputactions` asset (see 1.4).
- The file's own port note (lines 29–33) names the swap seams:
  `SampleDesktopMove()` and the consumed-tap read in `TryGetTapScreenPosition()`.

**`Assets/_Modules/Village/Hero/HeroAbilities.cs`** — Blaise's Q/W/E/R kit.
- `TryCast(AbilitySlot)` is a clean public method: cooldown + mana gate, then
  effect resolution (Strike / Aoe / Heal / Meteor). `AbilitySlot` enum =
  `Q, W, E, R` (`AbilityCatalog.cs:31`).
- **`TryCast` has NO caller anywhere in the project.** A repo-wide search for
  `qKey/wKey/eKey/rKey` and for `TryCast(` callers returns nothing in runtime
  code — only the enum/catalog plumbing and tests. The Q/W/E/R abilities are
  **completely uninvoked**: no keyboard reader, no touch button, no input layer.
  The kit is fully implemented gameplay with **zero input surface**.

**`Assets/_Modules/HUD/VillageHud.uxml` + `VillageHudController.cs`** — village HUD.
- The HUD is an explicitly **passive display** (port spec Part 2). It owns no
  gameplay state and pushes data only via setters (`SetHeartHp`, `SetCrystals`,
  `SetWave`, `SetAbilityCooldown`, `SetMana`).
- It **does NOT include movement controls.** No D-pad, no joystick element.
- The **ability bar IS NOT interactive.** `BuildAbilityCells()` builds four
  `ability-slot-{i}` cells, but every child is set
  `pickingMode = PickingMode.Ignore` and the slot `VisualElement` itself is a
  bare `VisualElement` (not a `Button`) with no click handler. The bar is a
  **cooldown/mana readout only** — tapping a slot does nothing.
- The **only interactive element in the whole HUD is the Build button**
  (`ui:Button name="build-button"`), which raises the `BuildRequested`
  `UnityEvent`. Touch target: `120 × 56 px` (USS `.build-button`).
- The root and both strips are `picking-mode="Ignore"` so gameplay input passes
  through everywhere except the panels.

**`docs/port-notes/week5-dungeon-foundation.md`** confirms the design intent:
*"smooth tap-to-move on touch; WASD on desktop"* and explicitly flags that the
Input-System-low-level-polling choice (no `.inputactions` asset) *"warrants a
`unity-decisions.md` row"*.

**`docs/port-notes/hud-module.md`** confirms the ability bar is a readout
("a cooldown sweep ... and a seconds-remaining numeral") with no input role.

### 1.3 Input matrix — as it stands

| Action            | Desktop today        | Mobile today                  | Gap |
| ----------------- | -------------------- | ----------------------------- | --- |
| Dungeon move      | WASD / arrows        | Tap-to-move (raycast)         | No analog/held touch movement |
| Village hero move | — (no controller)    | —                             | Village hero has no locomotion controller at all |
| Hero abilities QWER | — (no key reader)  | —                             | `TryCast` never called; no UI, no keys |
| Build menu        | Build button (click) | Build button (tap)            | OK — works on touch |
| HUD readouts      | Display only         | Display only                  | OK |

### 1.4 Package & infrastructure findings

- `com.unity.inputsystem` **1.19.0** is installed (`Packages/manifest.json`).
- **No `.inputactions` asset exists in the project.** The only `.inputactions`
  files are inside `Library/PackageCache` (package samples) — none under
  `Assets/`. All input is hand-polled via `Keyboard.current` /
  `Mouse.current` / `Touchscreen.current`.
- The Input System's **On-Screen Controls** components (`OnScreenStick`,
  `OnScreenButton`) ship inside `com.unity.inputsystem` itself — **no extra
  package install is needed** to use them. They are not referenced anywhere
  today (`OnScreen` search: 0 hits in `Assets/`).
- `EnhancedTouch` / `Touch.activeTouches` is **not** enabled; only
  `Touchscreen.current.primaryTouch` is read. Multi-touch (move + cast at the
  same time) will require `EnhancedTouchSupport.Enable()` or a UI-Toolkit-driven
  control layer (recommended — see 2.6).
- `DeNelle.Dungeons.asmdef` already references `Unity.InputSystem`. A new
  on-screen control layer for the dungeon needs **no asmdef change**. The
  `DeNelle.HUD` asmdef references `DeNelle.Core` + UI Toolkit only — fine for a
  UI-Toolkit-based control layer (recommended), but it would need an
  `Unity.InputSystem` reference if On-Screen Control *components* are used there.

---

## 2. Mobile control design — on-screen scheme for the Seeker

Design target: a one-handed-friendly, two-thumb phone layout. The Seeker is a
~6.4" Android phone; assume a logical canvas around **2400 × 1080** (portrait
controls are NOT assumed — the game is landscape; see 2.7 for orientation).
All sizes below are given in **mm (physical)** and **px** at an assumed
**~400 dpi** Seeker-class panel (`1 mm ≈ 16 px`). The implementation must drive
sizes from real dpi (`Screen.dpi`) so targets stay physically constant across
devices — never hard-code px alone.

### 2.0 Touch-target sizing standard (applies to every control below)

| Tier              | Physical size | px @ 400 dpi | Use |
| ----------------- | ------------- | ------------ | --- |
| **Minimum**       | 9 mm          | ~144 px      | Absolute floor — never smaller |
| **Recommended**   | 11–12 mm      | ~176–192 px  | Standard for ability buttons / Build |
| **Primary / D-pad**| 14–16 mm     | ~224–256 px  | Movement control, most-used target |
| **Spacing (gap)** | ≥ 2 mm        | ≥ 32 px      | Dead gap between any two targets |

These follow the common 9 mm mobile minimum (Android's 48 dp ≈ 9 mm) with a
deliberate bump for game controls, which are pressed fast and without looking.
The current HUD ability cells are **64 px (~4 mm)** — far too small for touch
and **must be enlarged** when made interactive (see 2.3).

### 2.1 Layout overview (landscape)

```
 +-----------------------------------------------------------------------+
 |  [Heart HP]            [Crystals]                       [Wave]        |  <- top strip (readout, unchanged)
 |                                                                       |
 |                                                                       |
 |                          (3D game view)                              |
 |                                                                       |
 |   . - .                                              [Q] [W]         |
 |  ( D-PAD )                                            [E] [R]         |  <- thumb zones
 |   ' - '                                       [Mana]      [BUILD]     |
 +-----------------------------------------------------------------------+
   ^ left thumb: movement                  right thumb: abilities + build ^
```

- **Left thumb cluster:** movement D-pad / joystick (dungeon + village hero).
- **Right thumb cluster:** the four ability buttons (Q/W/E/R) in a 2×2 diamond,
  plus the Build button below them.
- **Top strip:** Heart HP / Crystals / Wave — unchanged readout, stays
  `picking-mode="Ignore"`.
- Both thumb clusters sit **inside the safe area** and **outside** the screen's
  bottom corners (where the OS gesture bar lives).

### 2.2 Movement control — D-pad vs virtual joystick

**Recommendation: an 8-way virtual joystick (floating, fixed-origin hybrid)**,
*styled* as a D-pad. Rationale:
- The dungeon already wants a **graded analog vector** for the eased
  `_planarVelocity` model in `DungeonHero` — a pure 4/8-way D-pad throws away the
  acceleration curve and feels robotic. A joystick gives magnitude → speed for
  free (see §3).
- It still *reads* as a D-pad to the player (the owner asked for "D-pad"): give
  it a square/rounded-cross base art with four directional ticks, but let the
  knob travel continuously inside it.

**Joystick spec:**

| Property            | Value                         | Notes |
| ------------------- | ----------------------------- | ----- |
| Base diameter       | 16 mm (~256 px)               | Primary tier |
| Knob diameter       | 7 mm (~112 px)                | ~44% of base |
| Max knob travel     | 4.5 mm (~72 px) from centre   | Defines the magnitude=1 ring |
| Anchor              | Bottom-left, **floating**     | See below |
| Centre rest pos     | ~18 mm from left edge, ~18 mm from bottom edge of the safe area | |
| Activation region   | Whole bottom-left quadrant of the safe area | Touch-down anywhere there places the joystick origin at the touch point |
| Recenter behaviour  | Origin snaps back to anchor on release; **does not** recenter under the thumb mid-drag | |
| Visual when idle    | 35% opacity ghost at the anchor | So the player knows where it is |

"Floating" = the joystick *origin* is wherever the thumb first touches in the
left activation zone (forgiving — no need to find an exact spot), but it has a
fixed *anchor* it returns to and shows a ghost there. This is the standard
mobile-action-game pattern and pairs with the dead-zone tuning in §3.

If the owner insists on a **true digital D-pad**, use the same base art with 4
or 8 discrete buttons, each a 7–8 mm wedge, and feed `ResolveDesiredDirection()`
a quantised unit vector. The §3 dead-zone still applies (it becomes the
no-press centre); the sensitivity curve degenerates to "full speed or zero."
The joystick is the better fit for this game's eased movement model.

### 2.3 Ability buttons — Q/W/E/R as touch

Replace the **non-interactive** HUD ability cells with **real touch buttons**.

| Property              | Value                        |
| --------------------- | ---------------------------- |
| Button diameter       | 12 mm (~192 px) — circular   |
| Layout                | 2×2 diamond, bottom-right    |
| Gap between buttons   | 3 mm (~48 px)                |
| Cluster anchor        | ~16 mm from right edge, ~16 mm from bottom edge of safe area |
| Visual content        | Ability glyph (existing `SlotGlyphs` ✦ ❄ ✚ ☄), keep the Q/W/E/R badge as a small corner label for desktop parity |
| Cooldown sweep        | Reuse the existing radial/vertical cooldown-fill — already built per cell |
| Disabled state        | When `HeroAbilities.CanCast(slot)` is false (cooling or low mana), dim to ~40% and ignore the tap |
| Press feedback        | Scale to 0.92 + brighten on `pointer-down`; haptic tick (`Handheld.Vibrate` or Android `HapticFeedback`) on a successful cast |

**Wiring:** each button's tap calls `HeroAbilities.TryCast(slot)`. Because the
HUD asmdef cannot see `DeNelle.Village`, follow the existing Build-button
pattern — the HUD raises a per-slot event (e.g. `AbilityRequested(int slot)` as
a `UnityEvent<int>`), and the integrator/`VillageController` hooks it to
`hero.TryCast((AbilitySlot)slot)`. This keeps module isolation intact and mirrors
how `BuildRequested` already works.

The current `.ability-slot` USS (64 px) must grow to ~192 px and the cells must
become `Button`s (or get a `Clickable` manipulator) instead of inert
`VisualElement`s. The cooldown fill / label / key children stay `picking-mode`
ignore so the whole cell is one target.

### 2.4 Build button + HUD touch targets

- **Build button:** already interactive. Bump from `120 × 56 px` to a minimum
  **176 × 144 px** (11 × 9 mm) and move it into the right thumb cluster, below
  the ability diamond, so it is not a corner reach. Keep `BuildRequested`.
- **Top-strip readouts** (Heart / Crystals / Wave): remain display-only,
  `picking-mode="Ignore"`. Do **not** make them touch targets.
- Any future HUD popups (build menu, etc.) must also obey the 9 mm minimum and
  2 mm spacing rules in §2.0.

### 2.5 Dungeon tap-to-move

Keep tap-to-move — it is a good fit for a cozy dungeon-walk and the code already
works. Refinements for the on-screen scheme:
- **Coexdistence with the D-pad:** see §3.5. Short story — the D-pad owns the
  left thumb zone; a tap *outside* both thumb clusters is a move target.
- **Reject taps that land on a control.** `TryGetTapScreenPosition()` currently
  accepts any `primaryTouch` press. It must ignore presses whose screen position
  is inside the D-pad activation zone or any ability/Build button rect (UI
  Toolkit: check `panel.Pick()` is null, or test against the control rects).
  Without this, grabbing the joystick also fires a walk command.
- Add a small **confirmation marker** (a ground decal / ring) at the tapped
  point for touch feedback — the gizmo in `OnDrawGizmosSelected()` is editor-only.
- Keep `_arriveDistance` at 0.25; raise nothing here.

### 2.6 Implementation approach — UI Toolkit, not `OnScreenStick`

Two viable routes:
1. **`OnScreenStick` / `OnScreenButton`** components (built into
   `com.unity.inputsystem`) feeding an `.inputactions` asset.
2. **A UI-Toolkit control layer** (custom `VisualElement`s with pointer
   manipulators) that calls into `DungeonHero` / raises HUD events directly.

**Recommend route 2 (UI Toolkit).** The project already commits to UI Toolkit
for the HUD, ships **no `.inputactions` asset**, and `DungeonHero` is explicitly
designed to be fed through its two swap seams. A UI-Toolkit joystick + buttons:
- reuse the existing HUD `UIDocument` / USS pipeline,
- give precise control over the floating-origin and dead-zone behaviour in §3,
- handle multi-touch cleanly (each `VisualElement` captures its own pointer via
  `PointerCaptureHelper` — move and cast at once with no `EnhancedTouch` setup),
- avoid introducing a half-used `.inputactions` asset for two controls.

Concretely: add a new `MobileControlsController` (in `DeNelle.HUD` or a small
`DeNelle.Input` module) that owns a joystick `VisualElement` and exposes a
`Vector2 MoveVector { get; }` (range −1..1, post-dead-zone, post-curve).
`DungeonHero` gets a new seam — e.g. `SetMoveVector(Vector2)` or an injected
`IMoveSource` — read in `ResolveDesiredDirection()` *before* the tap branch and
*after* the keyboard branch. Desktop keeps WASD untouched.

The Input System's **On-Screen Controls** still need no package install if route
1 is ever chosen later — but route 2 is the recommendation.

### 2.7 Safe-area handling

The Seeker has rounded corners, a front camera cutout, and an Android gesture
navigation bar. **Every interactive control must live inside the safe area.**

- Read `Screen.safeArea` (a `Rect` in pixels) each frame the resolution/orientation
  can change, and inset the HUD root accordingly.
- In UI Toolkit: apply the safe-area inset as `style` padding on a safe-area
  container `VisualElement` that wraps the control clusters (the readout strip
  can also use it). Pattern: `padding-left = safeArea.xMin`,
  `padding-right = Screen.width - safeArea.xMax`,
  `padding-bottom = safeArea.yMin`, `padding-top = Screen.height - safeArea.yMax`,
  converted to UI-Toolkit points via the panel scale.
- **Extra bottom margin:** even inside the reported safe area, keep the D-pad
  and ability cluster **≥ 6 mm above the bottom edge** so the thumb does not
  collide with the Android gesture bar's swipe zone.
- **Corner avoidance:** keep controls out of the extreme bottom-left/right
  rounded corners — the §2.2 / §2.3 anchors (16–18 mm insets) already do this.
- **Orientation:** the game is **landscape**. Lock orientation (Player Settings)
  to landscape so the thumb-cluster layout is stable; if both landscape
  orientations are allowed, re-run the safe-area inset on rotation (the cutout
  swaps sides).
- Test with a notch/cutout simulation (Device Simulator) — do not assume the
  Editor Game view safe area.

---

## 3. D-pad sensitivity — concrete tunable values

The owner called this out specifically. Below are concrete starting values, all
intended as **serialized, inspector-tunable fields** on the new
`MobileControlsController` (joystick side) and consumed by `DungeonHero`.
Treat the numbers as **shipping defaults to playtest from**, not final law.

### 3.1 Dead-zone

| Field                  | Recommended value | Range to expose | Meaning |
| ---------------------- | ----------------- | --------------- | ------- |
| `innerDeadZone`        | **0.18** (18% of max travel) | 0.05–0.35 | Below this knob displacement → output is **zero**. Kills thumb-rest drift and the "did I touch it?" creep. |
| `outerDeadZone`        | **0.92**          | 0.80–1.00       | At/above this displacement → output **clamps to magnitude 1**. Lets the player hit full speed without pinning the knob to the exact rim. |
| `recenterDeadZone`     | **0.10**          | 0.05–0.20       | If the knob is released and within this of centre, snap instantly to zero (no glide-out). |

**Why 0.18:** a touch joystick has no spring, so the thumb rarely returns to true
centre; a small 0–5% dead-zone (good for a physical stick) is too tight here and
produces phantom drift. 18% is a comfortable mobile default — large enough to
absorb thumb wobble, small enough that a deliberate nudge still registers. If a
true digital D-pad is used instead, `innerDeadZone` becomes the no-press
threshold (use ~0.20 there).

**Apply the dead-zone radially**, on the raw knob displacement vector, then
**rescale**: an input just past the inner dead-zone should map to magnitude ≈ 0,
not jump to 0.18. Formula:

```
raw      = knobOffset / maxTravel              // 0..1+ vector
mag      = length(raw)
if mag <= innerDeadZone:        out = 0
else if mag >= outerDeadZone:   out = normalize(raw) * 1
else:
    t   = (mag - innerDeadZone) / (outerDeadZone - innerDeadZone)  // 0..1
    out = normalize(raw) * applyCurve(t)        // see 3.2
```

### 3.2 Sensitivity curve

| Field            | Recommended value | Range | Meaning |
| ---------------- | ----------------- | ----- | ------- |
| `responseCurve`  | **Eased (exponent 1.6)** | linear / 1.2–2.4 | Maps the post-dead-zone `t` (0..1) to output magnitude. |
| `curveExponent`  | **1.6**           | 1.0–3.0 | `out = pow(t, exponent)`. 1.0 = linear. |

**Recommendation: an eased curve, exponent ≈ 1.6** — i.e.
`magnitude = t^1.6`. Rationale:
- A **linear** curve makes small thumb movements feel twitchy near the dead-zone
  edge (the village/dungeon hero is a slow cozy walker, not a twin-stick shooter).
- An eased (power) curve gives a **gentle low end** — fine speed control for
  lining up doorways and lore-stones — while still reaching full speed at the
  rim.
- 1.6 is a mild ease; do not go above ~2.4 or the low end feels mushy/unresponsive.
- Expose this as a Unity `AnimationCurve` field as well as the exponent, so a
  designer can hand-author the shape in the inspector; `t^1.6` is the default
  the curve is initialised to.

Do **not** ease the *direction* — only the magnitude. Direction stays the
normalized raw vector so the player always walks exactly where the thumb points.

### 3.3 Input-magnitude → move-speed mapping

`DungeonHero` currently does `targetVelocity = desired * _moveSpeed` where
`desired` is a **unit** vector (tap-to-move and WASD are both all-or-nothing).
The joystick changes this: `desired` becomes a **graded** vector of length
`0..1`.

| Field             | Recommended value | Notes |
| ----------------- | ----------------- | ----- |
| `_moveSpeed`      | 4.2 (unchanged)   | Top speed at magnitude 1. |
| `walkThreshold`   | **0.0**           | No separate walk/run band needed — the curve already gives a slow low end. Optionally set to ~0.55 to add a discrete walk(<0.55)/run(≥0.55) feel; default off. |
| `minMoveSpeed`    | **0.6 u/s**       | Floor: when output magnitude is non-zero but tiny, still move at ≥ 0.6 u/s so the animator's idle↔walk blend latches and the hero does not "shuffle in place". Below this, treat as zero. |

Resulting mapping (with eased curve, exponent 1.6):

```
moveSpeed = (magnitude == 0) ? 0
          : max(minMoveSpeed, magnitude * _moveSpeed)
targetVelocity = direction * moveSpeed
```

The existing `_acceleration` (28 u/s²) easing in `Update()` stays — it smooths
the joystick's own jitter for free. The animator `Speed` float is already fed
from `_planarVelocity.magnitude`, so a graded walk speed makes the idle↔walk
blend look correct with no extra work.

### 3.4 Diagonal normalization

A true digital D-pad pressed up+right yields `(1,1)` = length 1.41 — a **41%
diagonal speed boost**. A joystick does not have this bug *if* the dead-zone
math in §3.1 normalizes correctly. Rules:

- **Joystick path:** the §3.1 formula already does `normalize(raw) * magnitude`,
  so the output vector length is exactly `magnitude` (0..1) in every direction —
  **diagonals are correctly normalized, no extra work.** The only requirement is
  that `maxTravel` is treated as a **radius** (a circular knob travel limit), not
  a square — clamp `knobOffset` to a circle, not a box.
- **Digital D-pad path (if chosen):** explicitly normalize — when two axes are
  pressed, output `(±0.7071, ±0.7071)` not `(±1, ±1)`. Do this *before* the
  speed mapping.
- `DungeonHero.SampleDesktopMove()` already calls `.normalized` on the WASD
  vector — desktop is fine; this section is purely about the new touch path.

### 3.5 How tap-to-move and the D-pad coexist

Both feed `DungeonHero.ResolveDesiredDirection()`. Priority order (most explicit
wins), extending the existing keyboard-wins rule:

1. **Keyboard (WASD)** — desktop only; wins over everything (current behaviour).
2. **D-pad / joystick** — if `MoveVector` magnitude > `innerDeadZone`, use it and
   **cancel any in-flight tap target** (`_hasMoveTarget = false`), exactly as
   held WASD already cancels it. The D-pad is a continuous deliberate input; it
   must override a stale walk-to-point.
3. **Tap-to-move** — only when neither of the above is active.

Concrete coexistence rules:
- **Spatial separation:** the D-pad owns the bottom-left activation quadrant of
  the safe area. A touch that *begins* inside that quadrant drives the joystick
  and is **never** interpreted as a tap-to-move. A touch beginning outside it
  (and outside the ability/Build clusters) is a tap-to-move target. This is the
  primary disambiguation — implement it as a hit-test in
  `TryGetTapScreenPosition()` (reject the press if it is inside any control
  rect), so the two never fight for the same finger.
- **Hand-off feel:** when the player lifts the joystick thumb, movement coasts to
  a stop via `_acceleration` easing — it does **not** snap. If they then tap
  elsewhere, tap-to-move takes over cleanly because the joystick is now zero.
- **No mid-walk hijack:** a tap-to-move walk in progress is interrupted the
  instant the joystick crosses `innerDeadZone` — the player grabbing the stick
  always means "I'm driving now."
- **Multi-touch:** with the UI-Toolkit route (§2.6) the joystick and an ability
  button each capture their own pointer, so a player can hold a movement
  direction and tap Q at the same time. Ensure the joystick `VisualElement`
  calls `CapturePointer` on `PointerDownEvent` so a second finger elsewhere does
  not steal it.

### 3.6 Sensitivity values — quick-reference card

| Parameter            | Default | Tunable range |
| -------------------- | ------- | ------------- |
| Inner dead-zone      | **0.18**| 0.05–0.35 |
| Outer dead-zone      | **0.92**| 0.80–1.00 |
| Recenter dead-zone   | **0.10**| 0.05–0.20 |
| Response curve       | **Eased** (power) | linear / eased |
| Curve exponent       | **1.6** | 1.0–3.0 |
| Top move speed       | 4.2 u/s | (existing field) |
| Min move speed floor | 0.6 u/s | 0.3–1.0 |
| Knob max travel      | 72 px (~4.5 mm) | 56–96 px |
| Diagonal handling    | Radial normalize (circular clamp) | — |

---

## 4. Implementation punch-list (for the pass this doc drives)

1. Add a `MobileControlsController` (UI Toolkit) — floating joystick + 2×2
   ability diamond + relocated Build button; safe-area container.
2. Add a `Vector2 MoveVector` seam on the joystick; add `SetMoveVector` (or an
   `IMoveSource`) to `DungeonHero` and read it in `ResolveDesiredDirection()`
   between the keyboard and tap branches.
3. Make the village hero a real locomotion controller (it has none today) and
   feed it the same `MoveVector` — out of scope to design here but **flagged**.
4. Make the HUD ability cells interactive: enlarge to ~192 px, convert to
   buttons, raise `AbilityRequested(int slot)`; integrator wires it to
   `HeroAbilities.TryCast`.
5. Reject tap-to-move presses that land on any control rect.
6. Apply `Screen.safeArea` insets to a HUD safe-area container.
7. Add the §3 dead-zone + curve fields as serialized, inspector-tunable values;
   default to the §3.6 card.
8. Add a `unity-decisions.md` row for the UI-Toolkit-control-layer choice (the
   week5 note already flagged the no-`.inputactions` decision).

---

## 5. Headline answers

- **Do on-screen controls exist today?** **No.** No virtual joystick, no D-pad,
  no touch ability buttons. Dungeon movement is tap-to-move only; the Q/W/E/R
  ability kit (`HeroAbilities.TryCast`) is fully implemented but has **no caller
  and no input surface at all**; the HUD ability bar is a non-interactive
  cooldown readout; the only touch-usable control is the Build button.
- **Recommended D-pad dead-zone:** inner **0.18** (radial, of max travel),
  outer **0.92**, recenter **0.10** — applied radially with rescale so output
  ramps from 0 at the inner edge.
- **Recommended sensitivity curve:** an **eased power curve, exponent ≈ 1.6**
  (`magnitude = t^1.6`) on magnitude only — gentle fine control at the low end,
  full `_moveSpeed` (4.2 u/s) at the rim, with a `minMoveSpeed` floor of
  0.6 u/s. Direction is never eased; diagonals are correctly normalized by a
  circular knob-travel clamp.
