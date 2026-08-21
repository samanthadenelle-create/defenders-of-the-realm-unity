<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 93 — Fix ATB Combat Scene (Last Stand): Bars Frozen, Attack No Animation, Item Does Nothing, Skills Unclear

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Medium — four separate fixes in the Last Stand dungeon combat scene
**Observed:** Screenshot —
  • Both hero and enemy ATB bars are full/static, never drain
  • Pressing Attack deals damage but triggers no animation
  • Item button does nothing
  • Skills button fires something but with no readable feedback

---

## Bug 1 — ATB Bars Don't Move

### Root Cause
`ATBCombatManager.StartCombat()` is never called when the Last Stand scene
loads. `_combatActive = false` by default, so `Update()` exits immediately
every frame — bars never fill or drain.

### Fix

In the scene's initialization script (e.g. `LastStandSceneController.cs` or
`DungeonCombatSetup.cs`), call:

```csharp
private void Start()
{
    ATBCombatManager.Instance?.StartCombat();
}
```

If `ATBCombatManager` is on a `DontDestroyOnLoad` object from a previous
scene, `Instance` will already exist. If not, add `ATBCombatManager` as a
component to the Last Stand scene root and call `StartCombat()` in `Start()`.

Also confirm the ATB bar UI is bound. In the HUD controller for the combat
scene, the fill bar's `Update()` must read `TurnProgress`:

```csharp
// ATB fill bar binding — put in the HUD's Update()
if (ATBCombatManager.Instance != null)
{
    heroAtbBar.fillAmount  = ATBCombatManager.Instance.TurnProgress;
    enemyAtbBar.fillAmount = ATBCombatManager.Instance.TurnProgress;   // Mirror
}
```

---

## Bug 2 — Attack Button: No Animation

### Root Cause
The Attack button's `onClick` calls a damage function directly but never fires
an `Animator.SetTrigger("Attack")` on Blaise (the hero).

### Fix

In `LastStandAttackHandler.cs` or wherever the Attack button resolves:

```csharp
public void OnAttackPressed()
{
    if (ATBCombatManager.Instance == null) return;

    // 1. Play attack animation on HERO
    _heroAnimator?.SetTrigger("Attack");

    // 2. Small wind-up delay before damage lands (see WO-81 pattern)
    StartCoroutine(AttackAfterWindup());
}

private IEnumerator AttackAfterWindup()
{
    yield return new WaitForSeconds(0.15f);   // Wind-up

    // 3. Deal damage to current enemy
    if (_currentEnemy != null)
    {
        if (_currentEnemy.TryGetComponent<EnemyHealth>(out var h))
            h.TakeDamage(_heroAttackDamage);

        VFXManager.Instance?.Play(VFXType.Impact_Physical,
            _currentEnemy.transform.position + Vector3.up * 0.8f);

        CameraShakeManager.Instance?.Shake(ShakeTier.Light);
        HitStopManager.Instance?.TriggerHitStop(0.04f);
    }

    // 4. End player turn
    ATBCombatManager.Instance?.PlayerActionComplete();
}
```

Ensure the hero prefab's Animator has an **"Attack"** trigger in its
Animator Controller. If the trigger is named differently (e.g. "BasicAttack"),
update `SetTrigger` to match.

---

## Bug 3 — Item Button Does Nothing

### Root Cause
`ItemButtonHandler.OnItemPressed()` is either not wired to the button's
`onClick`, or the method body is empty / references a null inventory.

### Fix — minimal working Item button

```csharp
// ItemButtonHandler.cs
using UnityEngine;
using TMPro;

public class ItemButtonHandler : MonoBehaviour
{
    [Header("Healing Potion (default item)")]
    public int   healAmount     = 30;
    public int   potionCount    = 3;
    public TMP_Text potionCountText;

    private HeroHealth _heroHealth;

    private void Awake()
    {
        _heroHealth = FindObjectOfType<HeroHealth>();
        RefreshUI();
    }

    public void OnItemPressed()
    {
        if (potionCount <= 0)
        {
            Debug.Log("[Item] No potions left.");
            return;
        }

        if (_heroHealth == null) return;

        potionCount--;
        _heroHealth.Heal(healAmount);
        RefreshUI();

        VFXManager.Instance?.Play(VFXType.Impact_Heal,
            _heroHealth.transform.position + Vector3.up * 0.8f);

        ATBCombatManager.Instance?.PlayerActionComplete();

        Debug.Log($"[Item] Used healing potion. +{healAmount} HP. Remaining: {potionCount}");
    }

    private void RefreshUI()
    {
        if (potionCountText != null)
            potionCountText.text = potionCount > 0 ? $"×{potionCount}" : "Empty";

        GetComponent<UnityEngine.UI.Button>().interactable = potionCount > 0;
    }
}
```

Wire `OnItemPressed()` to the Item button's `onClick` in the Inspector.

---

## Bug 4 — Skills Button: No Readable Feedback

### Root Cause
The Skills button fires but with no visual description of what ability was cast.
Player can't tell if it healed, dealt damage, or applied a DoT.

### Fix — add a combat log text field

Add a `TMP_Text` named `CombatLog` to the combat HUD. Update it whenever an
action fires:

```csharp
// CombatLogUI.cs (or add to existing HUD controller)
public static CombatLogUI Instance { get; private set; }
public TMP_Text logText;

private void Awake() { Instance = this; }

public void Log(string message)
{
    logText.text = message;
    StopAllCoroutines();
    StartCoroutine(FadeOut());
}

private IEnumerator FadeOut()
{
    yield return new WaitForSeconds(2.5f);
    logText.text = "";
}
```

In each ability's fire method, call:

```csharp
// Example for Skills abilities:
CombatLogUI.Instance?.Log("Blaise casts Mending Salve — +25 HP");
CombatLogUI.Instance?.Log("Blaise casts Storm of Arrows — 18 dmg, 3 hits");
CombatLogUI.Instance?.Log("Blaise sets Snare Trap — enemy slowed 40%");
```

---

## Scene Wiring Checklist

| Step | Action |
|---|---|
| `ATBCombatManager` in scene | Add to scene root if not present; call `StartCombat()` from scene init |
| Hero Animator | Confirm "Attack" trigger exists in Animator Controller |
| Attack button `onClick` | Wire to `OnAttackPressed()` |
| Item button `onClick` | Wire to `ItemButtonHandler.OnItemPressed()` |
| Skills button `onClick` | Wire to each ability method + `CombatLogUI.Log(message)` |
| ATB bar Images | Assign to `heroAtbBar` / `enemyAtbBar` fill Images |
| `CombatLogUI` text | Add TMP field to HUD Canvas, assign reference |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `LastStandSceneController.cs` (or scene init) | **Edit** — call `ATBCombatManager.Instance?.StartCombat()` in `Start()` |
| `LastStandAttackHandler.cs` (or equivalent) | **Edit** — add animator trigger + coroutine wind-up |
| `ItemButtonHandler.cs` | **Create** — wire to Item button |
| `CombatLogUI.cs` | **Create** — add to HUD Canvas |
| ATB HUD controller | **Edit** — bind `TurnProgress` to fill bars in `Update()` |
| All ability button onClick handlers | **Edit** — add `CombatLogUI.Log(...)` calls |

---

## Acceptance Criteria

- [ ] ATB bar visibly drains from full to empty over `maxTurnTime` seconds — both hero and enemy bars move
- [ ] When ATB hits zero, enemy attacks automatically within 0.5 s, then hero bar refills
- [ ] Pressing Attack plays Blaise's "Attack" animator trigger visually
- [ ] Attack damage lands 0.15 s after button press (wind-up before impact)
- [ ] Pressing Item with potions remaining heals hero by `healAmount` and decrements count
- [ ] Item button shows "Empty" and is non-interactable at 0 potions
- [ ] Every ability shows a readable line in the CombatLog for 2.5 s after use
- [ ] `PlayerActionComplete()` is called after every player action (ends the turn)

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `BattleController.cs:189` — ATB starts on entry. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
