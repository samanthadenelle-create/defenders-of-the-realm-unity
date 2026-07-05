# ⚠ WORK ORDER 331 — DTT Ability Bar Keyboard Hotkeys — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09.

**Status:** CLOSED — SUPERSEDED (system removed 2026-06-09)  
**Lane:** 2 (Combat/AI) — code-only, parallel-safe  
**Scene:** PatriciaLight_TD  
**Priority:** MEDIUM — Windows play-feel; currently requires clicking small buttons mid-combat

---

## Problem

The Defend the Tower ability bar (bottom of screen) has four buttons:
- Snare Trap
- Mercury Salve
- Storm of Arrows
- ATTACK (right-most, red)

There are no keyboard bindings. On Windows/Mac, players must click each button during combat
which breaks flow. Need hotkey bindings so the game is playable without leaving the mouse
on the camera.

---

## Desired Hotkey Layout

| Key | Action |
|-----|--------|
| `1` | Snare Trap (slot 0) |
| `2` | Mercury Salve (slot 1) |
| `3` | Storm of Arrows (slot 2) |
| `Space` or `E` | ATTACK (slot 3 / primary action) |
| `R` | Reload / confirm (if applicable) |

These should be **serialized** (`[SerializeField] private KeyCode[] _abilityKeys`) so Samantha
can change the bindings in the Inspector without a code change.

---

## Acceptance Criteria

- [ ] Pressing `1`/`2`/`3`/`Space` triggers the matching ability (same as clicking the button)
- [ ] Hotkeys are shown as small labels on each ability button (e.g. `[1]` in corner)
- [ ] Key bindings are serialized inspector fields — not hardcoded
- [ ] Hotkeys are disabled when a panel/menu is open (no firing while typing in a UI field)
- [ ] No regression to the existing click-to-fire path

---

## Files to Edit

```
Assets/_Modules/BattleATB/PatriciaLight_TD/   ← find the ability bar controller here
  (likely: DTTAbilityBar.cs, PatriciaHudController.cs, or similar)
```

If no existing hotkey handler exists, add a `DTTInputHandler.cs` MonoBehaviour on the
same GameObject as the ability bar, using `Input.GetKeyDown` each `Update`. Do NOT use
the new InputSystem — the project uses legacy `Input`.

## What NOT to Touch

- Village scene, WaveManager, TowerSwapService
- Global AudioService or EventTracker
