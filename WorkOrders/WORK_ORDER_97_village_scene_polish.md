# ⚠ WORK ORDER 97 — Village Scene Polish: World-Space "9", Purple Gate Material, Cooldown HUD, Debug Compass — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** Some issues reference the removed Defend-the-Tower system. Verify any remaining Village issues against current canon before re-implementing.

**Status:** PARTIALLY OBSOLETE — review before implementation
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small–Medium — four targeted fixes in the Village scene
**Observed:** Screenshot (Wave 1 pre-wave) —
  • "9" renders in world space ON the dungeon entrance gate structure
  • Dungeon entrance gate is entirely purple (unassigned URP material)
  • Q/F/E/R ability buttons have no cooldown fill or timer
  • "E (89°)" debug compass overlay renders at top-centre of screen

---

## Bug 1 — World-Space "9" on the Dungeon Gate

### Root Cause

Same class as WO-91: a `TextMesh` or `TextMeshPro` component is on a child
GameObject of the dungeon entrance gate, rendering the countdown (or building
level/ID) in world space. Because the camera is zoomed out for the village
view, the text renders enormous on the face of the gate.

### Fix

#### Step 1 — Find the text component

```
Hierarchy: expand the dungeon gate GameObject tree
Look for child named: "CountdownText", "GateNumber", "WaveNumber", "Label",
                      or any GO with a TextMesh / TextMeshPro component
```

Or search by script:
```
grep -r "countdown\|gateLabel\|waveNumber\|TextMesh" \
    Assets/_Modules/Village/ --include="*.cs" -l
```

#### Step 2 — Disable world-space text

1. Select the child GO with the TextMesh/TMP component
2. **Disable** the component (or delete the GO entirely)
3. The countdown rendering should already be handled by `WaveCountdownUI`
   (WO-91 screen-space overlay). Confirm `WaveCountdownUI.Instance` exists
   in the Village scene.

#### Step 3 — Confirm WaveManager uses WaveCountdownUI

In `WaveManager.cs`, before any wave starts:
```csharp
WaveCountdownUI.Instance?.StartCountdown(wave.prewaveDelay,
    () => StartCoroutine(SpawnWave(wave)));
```

If `WaveCountdownUI` is not present on the Village HUD Canvas, add it now:
1. Select the HUD Canvas in the Village scene
2. Add component: `WaveCountdownUI`
3. Assign the `WaveCountdownText` TMP_Text reference

---

## Bug 2 — Dungeon Entrance Gate is Purple (No Material)

### Root Cause

The dungeon entrance gate GameObject (large arch structure at village centre)
has no material assigned to its `MeshRenderer`. URP renders unassigned
materials as magenta/purple.

### Fix

#### Step 1 — Assign a material immediately

1. Select the gate root GameObject (or mesh child) in the Hierarchy
2. Open Inspector → `MeshRenderer` → `Materials`
3. Assign an existing material from the project. Good candidates:
   - `Assets/Materials/Stone_Wall.mat`
   - `Assets/Materials/Dungeon_Brick.mat`
   - Any existing URP Lit material with a stone/dark texture

If no appropriate material exists, create one:
```
Right-click Assets/Materials/ → Create → Material
Shader: Universal Render Pipeline/Lit
Base Map color: #2A2030  (dark stone purple — intentional dark gate feel)
Metallic: 0.0
Smoothness: 0.15
```

Name it `DungeonGate.mat`.

#### Step 2 — Apply to all gate mesh children

The gate likely has multiple mesh children (arch, pillars, base). Select each
and confirm all have `DungeonGate.mat` (or appropriate material) assigned.
No child should show URP magenta/purple.

#### Step 3 — Check prefab vs scene override

If the gate is a prefab instance:
1. Select the gate → Inspector → **Overrides → Apply All** (to persist the fix)
   OR apply only the material override so it propagates to all instances

---

## Bug 3 — No Cooldown Indicators on Village Ability Buttons (Q/F/E/R)

### Root Cause

The ability buttons in the Village scene bottom bar (Q = Shot, F = Snare Trap,
E = Mending Salve, R = Storm of Arrows) have no `AbilityCooldownUI` component
or no `CooldownFill` Image child. Same root cause as WO-96 Defend the Tower
scene.

### Fix

This is the same fix as WO-96 Bug 4 — apply identically to the Village scene
ability buttons.

#### Step 1 — Add CooldownFill Image to each button

For each of the four ability buttons (Q, F, E, R):
```
AbilityButton (Button)
├── Icon (Image)
├── Label (TMP_Text — "Shot", "Trap", etc.)
├── CooldownFill (Image)         ← ADD THIS
│     Image Type: Filled
│     Fill Method: Radial360
│     Fill Origin: Top
│     Fill Amount: 0
│     Color: #00000088
│     Raycast Target: false
└── CooldownTimer (TMP_Text)     ← ADD THIS (optional)
      Font Size: 18
      Alignment: Centre
      Color: White
```

#### Step 2 — Attach `AbilityCooldownUI` to each button

Attach `AbilityCooldownUI.cs` (created in WO-96) to each of the four buttons.
Assign `cooldownFill` → the CooldownFill Image child.

#### Step 3 — Wire ability scripts

In each ability's `Use()` / `Fire()` method, after executing the ability:
```csharp
// Q — Shot / basic attack
GetComponent<AbilityCooldownUI>()?.StartCooldown(shotCooldown);

// F — Snare Trap
GetComponent<AbilityCooldownUI>()?.StartCooldown(snareCooldown);

// E — Mending Salve (already in SalveAbility.Use() per WO-89)
GetComponent<AbilityCooldownUI>()?.StartCooldown(cooldown);

// R — Storm of Arrows
GetComponent<AbilityCooldownUI>()?.StartCooldown(stormCooldown);
```

---

## Bug 4 — "E (89°)" Debug Compass Overlay

### Root Cause

A debug or navigation compass UI element renders at the top-centre showing
a cardinal direction and bearing angle. This is either:
1. A debug `OnGUI()` call left in a script
2. A Navigation/Minimap component displaying bearing to the nearest waypoint
3. An `EnemyBrain` or pathfinding diagnostic printing bearing to the Console
   UI (should only go to `Debug.Log`)

### Fix

#### Step 1 — Find the source

```
grep -r "OnGUI\|GUI.Label\|bearing\|compass\|degrees\|89" \
    Assets/ --include="*.cs" -l
```

Also check: any `Update()` that sets a `TMP_Text.text` to a direction string.

#### Step 2 — Wrap in `#if UNITY_EDITOR`

If this is intentional debug output, gate it so it never appears in builds:
```csharp
#if UNITY_EDITOR
    compassText.text = $"{cardinal} ({bearing:F0}°)";
#else
    compassText.gameObject.SetActive(false);
#endif
```

#### Step 3 — Disable the UI element

If the compass UI is a GameObject in the HUD Canvas named "CompassDebug",
"BearingIndicator", or similar — **disable** it in the Inspector for now.
It can be re-enabled later if a proper minimap/compass feature is scoped.

---

## Scene Wiring Checklist

| Step | Action |
|---|---|
| World-space "9" | Disable TextMesh child on dungeon gate |
| `WaveCountdownUI` | Confirm present on Village HUD Canvas; assign TMP reference |
| Gate material | Assign `DungeonGate.mat` to all gate mesh children |
| Q/F/E/R buttons | Add CooldownFill Image + `AbilityCooldownUI` component to each |
| Ability scripts | Call `AbilityCooldownUI.StartCooldown()` in each ability |
| Compass overlay | Wrap in `#if UNITY_EDITOR` or disable GO |

---

## Files to Create / Edit

| File | Action |
|---|---|
| Village scene — dungeon gate GO | **Edit** — disable world-space TextMesh; assign DungeonGate.mat |
| `Assets/Materials/DungeonGate.mat` | **Create** if no suitable material exists |
| Village HUD Canvas | **Edit** — confirm/add `WaveCountdownUI`, add CooldownFill Images to Q/F/E/R buttons |
| `AbilityCooldownUI.cs` | **Reuse** from WO-96 — attach to all four Village ability buttons |
| Shot, SnareTrap, SalveAbility, StormOfArrows scripts | **Edit** — call `AbilityCooldownUI.StartCooldown()` |
| Debug compass script | **Edit** — wrap in `#if UNITY_EDITOR` or disable |

---

## Acceptance Criteria

- [ ] No "9" (or any number) floats in world space in the Village scene
- [ ] Wave countdown renders as screen-space overlay via `WaveCountdownUI` (WO-91)
- [ ] Dungeon entrance gate has no purple/magenta surfaces — correct material applied
- [ ] All four ability buttons (Q/F/E/R) show a radial cooldown fill after use
- [ ] Buttons are non-interactable and fill drains visibly during cooldown
- [ ] Fill resets to 0 when ability is ready — button becomes interactable
- [ ] "E (89°)" compass text does NOT appear in a build (editor-only or disabled)
- [ ] No other `OnGUI` or debug text renders in the HUD during normal play
