<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 242 — Mobile-First HUD & Interaction System

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 242
**Date:** 2026-06-02
**Triggered by:** Mobile-first QA sweep — DEF-137 (no touch for node interactions), DEF-129 (web input)

---

## The problem

Every "Press E / Press F" prompt in the game is a keyboard-only interaction with no mobile equivalent. The game targets mobile and WebGL — neither has a keyboard as primary input. This WO replaces all keyboard interaction prompts with a unified, thumb-friendly touch system.

---

## Screen layout — thumb zones (portrait + landscape)

```
┌─────────────────────────────────────────────┐
│  [Health]    [Heartwood HP]    [Wave / Zone] │  ← top bar (safe zone, not interactive)
│                                             │
│              GAME WORLD                     │
│                                             │
│                                             │
│  [Virtual    [INTERACT]  [SPRINT]  [BUILD]  │  ← bottom row (thumb zone)
│   Joystick]                                 │
│  [Ability1] [Ability2] [Ability3] [Ability4]│  ← ability row (right thumb)
└─────────────────────────────────────────────┘
```

All interactive elements sit in the bottom 35% of screen. Nothing important above 65% except non-interactive status info.

---

## Components

### 1. Virtual Joystick (bottom-left)

```csharp
// Assets/_Modules/HUD/VirtualJoystick.cs  (new — code-built, no UXML)
// Floating joystick: touch-down anywhere in left half of screen creates the joystick at that point.
// Outer ring: 80px radius. Inner knob: 30px radius.
// Outputs: Vector2 Direction (normalized), float Magnitude (0–1)
// Routes into HeroLocomotion.SetInput(direction, magnitude) each frame.
```

**Direction:** touch delta from anchor point, clamped to outer ring radius, normalised.
**Dead zone:** 8px — prevents jitter from resting thumb.
**Visual:** semi-transparent white ring + knob, alpha 0.4 at rest, 0.7 active. No labels.

---

### 2. Universal Interact Button (bottom-centre)

Replaces every "Press E" and "Press F" in the game. Single button. Context-sensitive label.

```csharp
// Assets/_Modules/HUD/InteractButton.cs  (new — code-built, no UXML)
// One canvas button, always visible when an interaction is available.
// Hidden otherwise.
//
// API:
//   InteractButton.Instance.Show(string label, System.Action onTap)
//   InteractButton.Instance.Hide()
//
// All "Press E" and "Press F" calls → replace with:
//   InteractButton.Instance.Show("Claim Iron Camp", () => node.Claim());
//   InteractButton.Instance.Show("Build", () => node.ShowBuildPanel());
//   InteractButton.Instance.Show("Upgrade", () => panel.Show(building));
//   InteractButton.Instance.Show("Enter Dungeon", () => portal.Enter());
```

**Visual:** 120×52px rounded pill, dark background (0.1 alpha), white label 14px, gold border accent.
**Position:** bottom-centre, 140px above screen bottom.
**Tap area:** extends 20px beyond visual bounds (accessibility padding).

**Replace these keyboard paths:**
| Old | New |
|---|---|
| `Input.GetKeyDown(KeyCode.E)` in ClaimableNode | `InteractButton.Instance.Show(...)` |
| `Input.GetKeyDown(KeyCode.F)` in BuildingInteractable | `InteractButton.Instance.Show(...)` |
| `Input.GetKeyDown(KeyCode.E)` in DungeonPortal proximity | `InteractButton.Instance.Show(...)` |
| `InteractionPrompt.Instance.Show("Press E to...")` | `InteractButton.Instance.Show(...)` |

---

### 3. Ability Bar (bottom-right, 4 buttons)

```csharp
// Assets/_Modules/HUD/AbilityBar.cs  (new — code-built, no UXML)
// Four circular buttons, 64px diameter, spaced 12px apart.
// Maps to hero abilities (Q/W/E/R on keyboard = slot 0/1/2/3 on touch).
// Shows: ability icon (or placeholder colour), cooldown arc overlay, cost label.
```

**Cooldown:** filled arc sweeps anti-clockwise as cooldown ticks down. Greyed when on cooldown.
**Position:** bottom-right, stacked 2×2 or 1×4 based on screen width.
**Tap:** fires `HeroAbilities.UseSlot(int index)` — same path as keyboard.

---

### 4. Build Button

```csharp
// Bottom-right, above ability bar. 56×56px square with hammer icon (ti-hammer or sprite).
// Toggles BuildMode. Highlights amber when active.
// Existing BuildMenu keyboard shortcut (B) stays — button is the mobile path.
```

---

### 5. Sprint / Dodge Button

```csharp
// Bottom-right of joystick, 52×52px.
// Fires HeroLocomotion.Dash() or sprint toggle.
// Only visible when hero has dash/sprint ability unlocked.
```

---

### 6. Resource Bar (top-left strip, non-interactive)

Small horizontal row: `Wood 🪵 120  |  Iron ⚙ 45  |  Crystal 💎 20  |  Food 🌾 80`
Compact, icon + number only. 12px text. Tapping does nothing — display only.
Subscribes to `EconomyService.OnChanged` and updates labels.

---

### 7. Party Bar (bottom-centre, above Interact button)

Four small portrait circles (40px diameter each). Active hero highlighted. Others show HP arc.
Tapping a party member portrait — reserved for future party management.
Subscribe to `HeroHealth.OnHealthChanged` per member.

---

### 8. Wave / Intel Alert strip (top-right)

Shows wave number + countdown during waves.
`AlertIntelSystem` raid warnings pop here as a sliding banner.
Dismissible by tap. Auto-dismisses after warning time.

---

## Migration — remove all keyboard-gated interactions

Search and replace across the codebase:

```
grep -rn "Input.GetKeyDown(KeyCode.E)\|Input.GetKeyDown(KeyCode.F)\|InteractionPrompt.Instance.Show" Assets/_Modules --include="*.cs"
```

For each hit, replace with `InteractButton.Instance.Show(label, callback)` pattern.
`InteractionPrompt.Instance` can stay as a fallback for PC builds but `InteractButton` is the canonical mobile path.

---

## Assembly

All new files: `DeNelle.HUD` namespace. No UXML. No UIDocument. Code-built Canvas only.

---

## Acceptance criteria

- [ ] Virtual joystick spawns on left-half touch, moves hero correctly
- [ ] Interact button appears/disappears based on nearby interactable context
- [ ] All "Press E / Press F" interactions completable via Interact button on touch devices
- [ ] Ability bar fires correct ability per slot, shows cooldown arc
- [ ] Build button toggles build mode
- [ ] Resource bar updates live from EconomyService
- [ ] Party bar shows HP state per member
- [ ] Raid warning banner appears from AlertIntelSystem, dismissible by tap
- [ ] No UXML / UIDocument
- [ ] Tested on WebGL (itch.io) — all interactions completable without keyboard

## What NOT to touch
- Keyboard bindings (keep as PC fallback — don't remove, just add touch path alongside)
- `WaveManager`, `EnemyBrain`, ATB scripts
- `Village.unity` — do not hand-edit
