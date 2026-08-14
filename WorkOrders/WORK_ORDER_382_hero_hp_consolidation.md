# WO-382: Hero HP Display — Remove Duplication, Use Party Panel Only

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at HudKitController.cs:259,1455.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Estimated Effort:** P0 (0.25 days — remove UI element)  
**Priority:** HIGH (visual clutter)  
**Lane:** 4 UI/HUD

---

## Issue

**Hero HP displayed twice:**
- ❌ Red health bar (bottom-left) — old/redundant
- ✅ Party panel (top-left) — clean, shows all party members

**Solution:** Remove bottom-left health bar, use party panel as single source of truth.

---

## Fix

### Remove Bottom-Left Health Bar

**In BattleHUD Canvas:**
```
Canvas (Battle)
├── ActionButtons
├── EnemyDisplay
├── PartyPanel ← Keep this
│   ├── HeroCard
│   │   └── HealthBar ✓
│   └── PartyMembers
└── HeroHealthBar ← DELETE this
    └── RedBar
```

**Steps:**
1. Open BattleArena.unity
2. Find "HeroHealthBar" or similar GameObject (bottom-left red bar)
3. Delete it (or disable it)
4. Test: Only party panel shows health

### Verify Party Panel Shows Health

**Party panel should display:**
- Hero name (Sylas)
- Hero health bar (red/green)
- Hero stats (HP/MAX)
- All in clean card format (top-left)

---

## Result

**Before:**
```
[Red Bar]              [Party]
HP: 45/50              Sylas [████░]
                       STR/INT/etc
(Bottom-left)          (Top-left)
DUPLICATION
```

**After:**
```
[Party Panel Only]
Sylas [████░] 45/50
STR/INT/etc
(Top-left)
CLEAN
```

---

## Testing

- [ ] Bottom-left red bar is gone
- [ ] Party panel shows hero health
- [ ] Health bar updates correctly
- [ ] No visual clutter
- [ ] Party info still visible

---

## Files

- `Assets/Scenes/BattleArena.unity` — Remove HeroHealthBar GameObject
- Or: `Assets/_Modules/BattleATB/UI/BattleHUDController.cs` — Disable if code-built

---

## Acceptance

- [ ] Single hero HP display (party panel only)
- [ ] Bottom-left duplication removed
- [ ] HUD is cleaner
