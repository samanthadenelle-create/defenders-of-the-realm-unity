# WO-258: ATB Battle — Critical Bug Fixes

**Status: READY TO IMPLEMENT (CRITICAL PATH)**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (blocks WO-233 from working)  
**Owner:** CLI  
**Time Estimate:** 1–2 hours  
**Unblocks:** WO-233 (FF-style ATB), playable battle system

---

## Problem Summary

The ATB architecture is solid, but several bugs prevent the battle from displaying or functioning:

1. **BattleVfx.cs is completely missing** — code references it but file doesn't exist
2. **BattleController.BindUi() has no debug logging** — failures are silent
3. **BattleHud.Render() doesn't guard null checks** — can crash mid-battle
4. **RenderCommandBar() logic is complex** — can leave command bar empty

When you open ATBBattle.unity and play, you see only capsules because the HUD binding fails without error messages.

---

## Fix 1: Create Missing BattleVfx.cs

**File:** `Assets/_Modules/BattleATB/BattleVfx.cs`

**Status:** Does not exist (critical dependency)

**Code:**

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

**Why:** BattleController.cs line 658–659 does:
```csharp
_vfx = new BattleVfx();
_vfx.Bind(_hud);
```

If BattleVfx doesn't exist, this crashes and BindUi() fails silently.

---

## Fix 2: Add Debug Logging to BattleController.BindUi()

**File:** `Assets/_Modules/BattleATB/BattleController.cs`

**Method:** BindUi() (line 634–661)

**Change:** Replace the entire method with this improved version:

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

**Why:** Current code fails silently. If BindUi() returns false, you only see capsules and no error tells you why.

---

## Fix 3: Guard Null Checks in BattleHud.Render()

**File:** `Assets/_Modules/BattleATB/BattleHud.cs`

**Method:** Render() (line 261–274)

**Current code:**
```csharp
public void Render(ATBRuntimeState runtime)
{
    BattleState state = runtime != null ? runtime.Battle : null;
    if (_root == null || state == null) return;
    // ...
}
```

**Problem:** This is correct but doesn't log when it's null.

**Improved version:**
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

**Why:** If Render() silently returns early, you won't know if the HUD is built, if state exists, etc.

---

## Fix 4: Add Error Guard in RenderCommandBar()

**File:** `Assets/_Modules/BattleATB/BattleHud.cs`

**Method:** RenderCommandBar() (find by searching for this method name)

**Issue:** This method has complex logic that can leave `_commandBar` empty if:
- No active member
- Active member is AI-controlled
- State is invalid

**Fix:** Add logging before returning early:

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

    // ... rest of method
}
```

**Why:** When command bar is empty and you don't know why, this logging tells you if it's missing unit, AI turn, or state issue.

---

## Acceptance Criteria

- [ ] BattleVfx.cs file created in `Assets/_Modules/BattleATB/`
- [ ] BattleVfx.cs compiles with no errors
- [ ] BattleController.BindUi() updated with try-catch + debug logs
- [ ] BattleHud.Render() updated with null-check logging
- [ ] RenderCommandBar() updated with early-return logging
- [ ] All brace checks pass (use CLAUDE.md rule)
- [ ] Play ATBBattle scene → see console logs showing HUD binding success
- [ ] HUD appears on screen (no longer blank)
- [ ] No "missing reference" errors for BattleVfx

---

## Testing Checklist

After fixes are applied, play the ATBBattle scene and verify:

```
[BattleController.Start] ===== STARTING BATTLE SETUP =====
[BattleController.BindUi] ✓ UIDocument root found...
[BattleController] ✓ HUD.Build() succeeded...
[BattleController] ✓ BattleVfx bound.
[BattleVfx] Successfully bound to HUD.
[BattleController] ✓ Battle started, calling Render()
[BattleController] ✓ First render complete
[BattleController] ===== BATTLE SETUP COMPLETE =====
```

Then:
- HUD title "The Last Stand" appears
- Enemy column shows enemy capsule card
- Party column shows hero capsule card
- ATB bars visible and animating
- Command bar shows Attack/Skills/Item/Defend buttons

If any of the above is missing, the corresponding log message should tell you why.

---

## Commit Message

`"WO-258: add BattleVfx stub + debug logging to ATB system — fixes silent failures in HUD binding"`

---

## Timeline

**1–2 hours:**
1. Create BattleVfx.cs (20 min)
2. Update BattleController.BindUi() (15 min)
3. Update BattleHud.Render() (10 min)
4. Add RenderCommandBar() logging (10 min)
5. Test & verify console output (30 min)
6. Run brace check on all modified files (5 min)

---

**This unblocks WO-233 (FF ATB system) and makes all HUD issues visible in console output.**

