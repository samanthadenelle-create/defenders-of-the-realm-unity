# WO-234: ATB Hot-Fix — CLI-Ready Specification

**Priority:** 🔴 CRITICAL (Phase 0 blocker)  
**Time Estimate:** 1–2 hours  
**Owner:** CLI  
**Status:** READY TO IMPLEMENT

---

## Problem

When you play ATBBattle.unity scene:
- HUD doesn't appear (blank screen)
- You only see capsule enemies (no UI)
- No ATB bars, no command buttons
- No error messages (silent failure)

**Root causes:**
1. BattleVfx.cs is referenced but doesn't exist → BindUi() crashes silently
2. BindUi() has no logging → impossible to debug
3. Render() doesn't log null conditions → silent failures
4. RenderCommandBar() has no early-return logging → command bar mysteriously empty

**Solution:** Create BattleVfx.cs + add debug logging to all failure points.

---

## What to Do (4 Fixes)

### Fix 1: Create BattleVfx.cs

**File path:** `Assets/_Modules/BattleATB/BattleVfx.cs`

**Full code:**
```csharp
// =============================================================================
// BattleVfx — WO-170 retro VFX presenter (STUB)
// =============================================================================
// Minimal stub so BattleController.BindUi() doesn't crash.
// Full implementation (WO-170 P1) comes later.
// =============================================================================

using UnityEngine;
using DeNelle.BattleATB.State;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// WO-170 — retro 2D VFX presenter layered over the HUD.
    /// Stub for now; full implementation pending.
    /// </summary>
    public class BattleVfx
    {
        private BattleHud _hud;

        public void Bind(BattleHud hud)
        {
            _hud = hud;
            Debug.Log("[BattleVfx] Successfully bound to HUD.");
        }

        public void Reset()
        {
            Debug.Log("[BattleVfx] Reset for new battle.");
        }

        public void OnActionSubmitted(BattleState state)
        {
            if (state == null) return;
            // TODO: WO-170 P1 — play hero lunge / cast animation
        }

        public void OnTurnResolved(BattleState state)
        {
            if (state == null) return;
            // TODO: WO-170 P1 — replay log entries as VFX
        }
    }
}
```

**Done?** File created, compiles, zero errors. ✓

---

### Fix 2: Update BattleController.BindUi()

**File:** `Assets/_Modules/BattleATB/BattleController.cs`  
**Method:** `BindUi()` (around line 634–661)

**REPLACE the entire method with this:**

```csharp
/// <summary>
/// Build the dynamic, code-built HUD into the UIDocument root and wire its
/// action callback to the engine. Returns false only if there is no document to draw into.
/// </summary>
private bool BindUi()
{
    if (_hudDocument == null) _hudDocument = GetComponent<UIDocument>();
    if (_hudDocument == null)
    {
        Debug.LogError("[BattleController] ❌ No UIDocument found on this GameObject! Add UIDocument component.");
        return false;
    }

    VisualElement root = _hudDocument.rootVisualElement;
    if (root == null)
    {
        Debug.LogError("[BattleController] ❌ UIDocument.rootVisualElement is null. UI Toolkit not ready?");
        return false;
    }

    Debug.Log($"[BattleController] ✓ UIDocument root found with {root.childCount} existing children.");

    try
    {
        _hud = new BattleHud();
        _hud.OnAction = SubmitPlayerAction;
        _hud.OnControlModeToggled = HandleControlModeToggled;

        Debug.Log("[BattleController] Building HUD frame...");
        _hud.Build(root);
        Debug.Log($"[BattleController] ✓ HUD.Build() succeeded. Root now has {root.childCount} children.");

        // WO-170 — bind the retro VFX presenter to the freshly-built HUD.
        _vfx = new BattleVfx();
        _vfx.Bind(_hud);
        Debug.Log("[BattleController] ✓ BattleVfx bound.");

        return true;
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[BattleController] ❌ Exception during HUD binding: {ex}");
        return false;
    }
}
```

**Done?** Method replaced, compiles. ✓

---

### Fix 3: Update BattleHud.Render()

**File:** `Assets/_Modules/BattleATB/BattleHud.cs`  
**Method:** `Render()` (around line 261–274)

**REPLACE the entire method with this:**

```csharp
public void Render(ATBRuntimeState runtime)
{
    BattleState state = null;
    if (runtime == null)
    {
        Debug.LogWarning("[BattleHud.Render] ATBRuntimeState is null");
        return;
    }
    
    state = runtime.Battle;
    if (_root == null)
    {
        Debug.LogError("[BattleHud.Render] _root is null — HUD not built?");
        return;
    }
    
    if (state == null)
    {
        // Silent — battle not started yet, or engine initialization failed
        return;
    }

    // Cards include the dead — iterate ALL units per side in engine order
    BindSide(state, Side.Enemy, _enemyColumn, EnemyBg);
    BindSide(state, Side.Party, _partyColumn, PartyBg);

    RenderLog(state);
    RenderStatus(runtime, state);
    RenderCommandBar(runtime, state);
}
```

**Done?** Method replaced, compiles. ✓

---

### Fix 4: Update RenderCommandBar() with Early-Return Logging

**File:** `Assets/_Modules/BattleATB/BattleHud.cs`  
**Method:** `RenderCommandBar()` (search for the method name)

**Find the beginning of RenderCommandBar() and add logging:**

```csharp
private void RenderCommandBar(ATBRuntimeState runtime, BattleState state)
{
    _commandBar.Clear();

    BattleUnit activeUnit = FirstActiveUnit(state);
    if (activeUnit == null)
    {
        Debug.Log("[BattleHud] No active unit — command bar empty.");
        return;
    }

    if (!activeUnit.IsPlayerControlled)
    {
        Debug.Log($"[BattleHud] {activeUnit.Name} is AI-controlled — command bar hidden.");
        return;
    }

    // ... rest of method continues as before
}
```

**Done?** Logging added, compiles. ✓

---

## Testing Checklist (After All 4 Fixes)

### Step 1: Open ATBBattle.unity Scene
```
File → Open Scene → Scenes/ATBBattle.unity
```

### Step 2: Play the Scene (Press Play)
```
Watch console output. You should see:
  [BattleController] ===== STARTING BATTLE SETUP =====
  [BattleController] ✓ UIDocument root found...
  [BattleController] Building HUD frame...
  [BattleController] ✓ HUD.Build() succeeded...
  [BattleController] ✓ BattleVfx bound.
  [BattleVfx] Successfully bound to HUD.
  [BattleController] ✓ Battle started, calling Render()
  [BattleController] ✓ First render complete
  [BattleController] ===== BATTLE SETUP COMPLETE =====
```

### Step 3: Verify HUD Appears on Screen
- [ ] Title "The Last Stand" visible at top
- [ ] Enemy capsule card visible on right
- [ ] Hero capsule card visible on left
- [ ] ATB bars visible and animating (filling over time)
- [ ] Command bar shows Attack/Skills/Item/Defend buttons

### Step 4: Test Interaction
- [ ] Click "Attack" button → action submits
- [ ] Battle progresses (enemies take turns)
- [ ] No crashes, no exceptions in console

### Step 5: Check Console
- [ ] No red errors (all messages are info or warnings)
- [ ] No "missing reference" errors
- [ ] No namespace warnings

---

## Acceptance Criteria

- [ ] BattleVfx.cs created and compiles
- [ ] BattleController.BindUi() updated with logging
- [ ] BattleHud.Render() updated with logging
- [ ] RenderCommandBar() has early-return logging
- [ ] Play ATBBattle scene
- [ ] Console shows success path (all ✓ logs)
- [ ] HUD appears on screen (not blank)
- [ ] ATB bars animate
- [ ] Command bar clickable
- [ ] No errors in console
- [ ] Brace check passes on all edited files (see CLAUDE.md rule)

---

## Brace Check (Required Before Submitting)

Run this for each file you touched:

```python
python3 -c "
import sys
path = 'Assets/_Modules/BattleATB/BattleVfx.cs'
content = open(path).read()
opens  = content.count('{')
closes = content.count('}')
if opens != closes:
    print(f'BRACE MISMATCH in {path}: {opens} open vs {closes} close')
    sys.exit(1)
print(f'Braces balanced ({opens}) ✓')
"
```

Run for all 4 files:
- [ ] BattleVfx.cs — braces balanced
- [ ] BattleController.cs — braces balanced
- [ ] BattleHud.cs — braces balanced

---

## Commit Message

```
WO-234: add BattleVfx stub + debug logging to ATB system — fixes silent failures in HUD binding
```

---

## If Tests Fail

**Blank HUD (no cards, no command bar)?**
- Check console for red errors
- If `_root is null`, HUD.Build() didn't add elements
- If `No UIDocument`, add component to BattleController GO

**ATB bars not animating?**
- Check console for `_runtimeState is null`
- Drag ATBRuntimeState.asset into BattleController inspector field

**Command bar empty?**
- Check console for `No active unit` or `AI-controlled`
- If no active unit, FirstActiveUnit() is null — check BattleState construction

**Any exception in console?**
- Copy exact error message
- Report with: file name, line number, what code triggered it

---

## Summary

**4 simple changes:**
1. Create 1 new file (BattleVfx.cs)
2. Replace 1 method (BindUi)
3. Replace 1 method (Render)
4. Add logging to 1 method (RenderCommandBar)

**Time:** 1–2 hours  
**Risk:** Low (all changes are additive or logging-only)  
**Unblocks:** Everything else (validation that ATB architecture works)

---

**Ready to execute. No blockers.**
