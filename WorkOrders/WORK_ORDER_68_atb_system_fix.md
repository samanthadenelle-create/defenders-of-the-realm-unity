# WORK ORDER 68 — Fix ATB System + Enemy Engagement in Dungeon Combat

**Status:** CLOSED — SUPERSEDED by WO-130 (owner-approved sweep 2026-08-09: newer WO-130 owns the ATB-broken surface)
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Medium — ATBCombatManager overhaul + EnemyBrain engagement fix
**Depends on:** WO-69 (EnemyBrain full version with `TryAttack` public method)
**Superseded by (enemy AI portion):** WO-69 replaces the EnemyBrain sections here

---

## Current Problems

- ATB bars on hero and enemy not moving
- No automatic enemy attack when player turn expires
- Enemies walk past the hero instead of engaging
- Combat feels stalled and unresponsive

---

## 1. Create / Replace `ATBCombatManager.cs`

**Path:** `Assets/_Modules/ATB/ATBCombatManager.cs`

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ATBCombatManager : MonoBehaviour
{
    public static ATBCombatManager Instance { get; private set; }

    [Header("Turn Settings")]
    public float maxTurnTime = 8f;

    [Header("Events")]
    public UnityEvent onPlayerTurnStart;
    public UnityEvent onPlayerTurnEnd;
    public UnityEvent onEnemyAutoAttack;

    // ── State ─────────────────────────────────────────────────────────────────
    private float _currentTime  = 0f;
    private bool  _playerTurn   = true;
    private bool  _combatActive = false;

    // ── Exposed for UI binding ─────────────────────────────────────────────────
    public float TurnProgress => _currentTime / maxTurnTime;   // 0–1, drive UI fill

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartCombat()
    {
        _combatActive = true;
        StartNewPlayerTurn();
    }

    public void StopCombat()
    {
        _combatActive = false;
        _playerTurn   = false;
        _currentTime  = 0f;
    }

    private void Update()
    {
        if (!_combatActive || !_playerTurn) return;

        _currentTime += Time.deltaTime;

        // UI: ATBBarController.Instance?.SetFill(TurnProgress);

        if (_currentTime >= maxTurnTime)
            EndPlayerTurn();
    }

    // ── Turn Flow ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from Attack / Skills / Item buttons after the action completes.
    /// </summary>
    public void PlayerActionComplete() => EndPlayerTurn();

    public void EndPlayerTurn()
    {
        _playerTurn  = false;
        _currentTime = 0f;
        onPlayerTurnEnd.Invoke();
        StartCoroutine(EnemyAutoAttackRoutine());
    }

    private IEnumerator EnemyAutoAttackRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        onEnemyAutoAttack.Invoke();

        // Find the current enemy and trigger its attack.
        var enemy = FindObjectOfType<EnemyBrain>();
        if (enemy != null)
            enemy.TryAttack();   // public method — see WO-69

        yield return new WaitForSeconds(1.4f);
        StartNewPlayerTurn();
    }

    private void StartNewPlayerTurn()
    {
        _playerTurn  = true;
        _currentTime = 0f;
        onPlayerTurnStart.Invoke();
    }
}
```

---

## 2. ATB UI bar binding

In the ATB HUD controller (wherever the fill bars live):

```csharp
private void Update()
{
    if (ATBCombatManager.Instance != null)
        heroAtbBar.fillAmount = ATBCombatManager.Instance.TurnProgress;
}
```

---

## 3. Scene wiring

1. Add `ATBCombatManager` to the dungeon combat scene root.
2. Wire `onPlayerTurnStart` → enable action buttons in UI.
3. Wire `onPlayerTurnEnd` → disable action buttons.
4. Wire `onEnemyAutoAttack` → play enemy attack animation / warning FX.
5. Each action button's `onClick` calls `ATBCombatManager.Instance.PlayerActionComplete()` after the action resolves.
6. On combat scene load: call `ATBCombatManager.Instance.StartCombat()`.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/ATB/ATBCombatManager.cs` | **Create/Replace** |
| ATB HUD controller | **Edit** — bind `TurnProgress` to fill bar |
| Action buttons (Attack, Skills, Item) | **Edit** — call `PlayerActionComplete()` |
| Dungeon combat scene | **Edit** — add `ATBCombatManager` GO, wire UnityEvents |

---

## Acceptance Criteria

- [ ] ATB fill bar visibly moves from 0 → 1 over `maxTurnTime` seconds
- [ ] At fill = 1, player turn ends and enemy attacks automatically within 0.5 s
- [ ] Action buttons are disabled during enemy turn
- [ ] After enemy attack, player turn resets and bar refills from 0
- [ ] `StopCombat()` cleanly freezes the bar (game-over / flee scenarios)
- [ ] `TurnProgress` can drive both hero and enemy UI bars from one place
