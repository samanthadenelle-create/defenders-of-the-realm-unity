# WORK ORDER 98 — Mending Salve: Heals Hero Instead of Tower (Defend the Tower Mode)

**Status:** BUG FIX — READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical — wrong game mechanic
**Scope:** Small — mode-aware target routing in SalveAbility + button label update
**Observed:** In Defend the Tower scene, pressing Mending Salve heals the hero.
             Intended behavior: Mending Salve heals the Tower (HeartHealth) in
             this mode, not the caster.

---

## Root Cause

`SalveAbility.Use()` (WO-89) always calls `heroHealth.Heal(healAmount)`.
There is no mode check — the ability behaves identically in Last Stand and
Defend the Tower despite having different design intent in each context.

In **Defend the Tower**, Mending Salve is a tactical tower-repair action, not
a self-heal. The hero maintains the tower; the salve restores tower HP.

---

## Fix

### Option A — Mode-aware target routing (preferred)

Update `SalveAbility.Use()` to check the active scene and route the heal
to `HeartHealth` when in Defend the Tower mode:

```csharp
// SalveAbility.cs — updated Use() method

public void Use()
{
    if (Time.time < _nextUseTime) return;
    _nextUseTime = Time.time + cooldown;

    _animator?.SetTrigger(_healTrigger);

    // Route heal target based on scene / mode
    if (IsDefendTowerMode())
        HealTower();
    else
        HealHero();

    VFXManager.Instance?.Play(healVFX,
        transform.position + Vector3.up * 0.8f);

    GetComponent<AbilityCooldownUI>()?.StartCooldown(cooldown);
    CombatLogUI.Instance?.Log($"Mending Salve — +{healAmount} Tower HP");
}

private bool IsDefendTowerMode()
{
    return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        == "DefendTowerScene";   // Match exact scene name in Build Settings
}

private void HealHero()
{
    if (heroHealth == null) return;
    heroHealth.Heal(healAmount);
    Debug.Log($"[SalveAbility] Hero healed {healAmount}. HP: {heroHealth.currentHealth}");
}

private void HealTower()
{
    var heart = FindObjectOfType<HeartHealth>();
    if (heart == null)
    {
        Debug.LogWarning("[SalveAbility] No HeartHealth found in scene.");
        return;
    }
    heart.Heal(healAmount);
    Debug.Log($"[SalveAbility] Tower healed {healAmount}. HP: {heart.currentHealth}");
}
```

### Required: Add `Heal()` to `HeartHealth.cs`

`HeartHealth` currently only has `TakeDamage()`. Add:

```csharp
// HeartHealth.cs — add after TakeDamage()

public void Heal(int amount)
{
    if (_isDead) return;
    currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

    VFXManager.Instance?.Play(VFXType.Impact_Heal,
        transform.position + Vector3.up * 1f);

    Debug.Log($"[HeartHealth] Healed {amount}. HP: {currentHealth}/{maxHealth}");
}
```

---

### Option B — Two separate ability prefabs (alternative, more scalable)

Keep `SalveAbility` as the hero self-heal. Create a separate
`TowerRepairAbility.cs` for the Defend the Tower ability bar:

```csharp
// TowerRepairAbility.cs
public class TowerRepairAbility : MonoBehaviour
{
    public int   repairAmount = 30;
    public float cooldown     = 10f;
    private float _nextUseTime;

    public void Use()
    {
        if (Time.time < _nextUseTime) return;
        _nextUseTime = Time.time + cooldown;

        var heart = FindObjectOfType<HeartHealth>();
        heart?.Heal(repairAmount);

        VFXManager.Instance?.Play(VFXType.Impact_Heal,
            FindObjectOfType<HeartHealth>()?.transform.position
            ?? transform.position);

        GetComponent<AbilityCooldownUI>()?.StartCooldown(cooldown);
        CombatLogUI.Instance?.Log($"Tower Repaired — +{repairAmount} HP");

        Debug.Log($"[TowerRepair] Tower repaired {repairAmount} HP.");
    }
}
```

Replace the Mending Salve button in the Defend the Tower scene with a
`TowerRepairAbility` button. Label it **"Repair"** or **"Mend Tower"**.

**Recommendation:** Use Option B — it keeps ability logic clean and avoids
branching on scene name. Option A is acceptable for a fast fix.

---

## Button Label Update

In the Defend the Tower scene, rename the Mending Salve button:

```
Old label: "Mending\nSalve"
New label: "Mend\nTower"   (or "Repair")
```

Update the TMP_Text on the button child in the Inspector.

---

## Files to Edit

| File | Action |
|---|---|
| `SalveAbility.cs` | **Edit** — add `IsDefendTowerMode()` routing (Option A) |
| `HeartHealth.cs` | **Edit** — add `Heal(int amount)` method |
| `TowerRepairAbility.cs` | **Create** — standalone tower repair (Option B) |
| Defend the Tower HUD Canvas | **Edit** — update button label to "Mend Tower" / "Repair" |
| Defend the Tower scene | **Edit** — swap Mending Salve component for TowerRepairAbility (Option B) |

---

## Acceptance Criteria

- [ ] In Defend the Tower: pressing the Salve/Repair button increases `HeartHealth.currentHealth`
- [ ] In Defend the Tower: pressing the button does NOT increase `HeroHealth.currentHealth`
- [ ] In Last Stand / Village: Mending Salve still heals the hero (unaffected)
- [ ] Heal VFX plays at the tower position (not hero position)
- [ ] CombatLog shows "Mending Salve — +X Tower HP" (or "Tower Repaired")
- [ ] Button goes on cooldown after use
- [ ] Button label clearly reads "Mend Tower" or "Repair" — not "Mending Salve"
- [ ] `HeartHealth.Heal()` is clamped to `maxHealth` (no overheal)
