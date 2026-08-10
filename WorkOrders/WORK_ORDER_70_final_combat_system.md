# WORK ORDER 70 — Final Combat System (HeroHealth + EnemyHealth + Full Wiring)

**Status:** DONE (reconciled 2026-08-09 from the tree, NOT felt-verified — HeroHealth.cs in tree, wave loop WIRED per canon §8; caveat: 'EnemyHealth' as named absent, enemy health integrated elsewhere)
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Medium — three scripts + prefab wiring guide
**Depends on:** WO-69 (EnemyBrain), WO-68 (ATBCombatManager)

---

## Goal

Complete the combat damage pipeline: heroes take damage from enemies, enemies
have a separate `EnemyHealth` component, and pets deal damage to enemies through
the new `EnemyHealth.TakeDamage()` surface. Everything is connected.

---

## 1. `HeroHealth.cs`

**Path:** `Assets/_Modules/Village/Hero/HeroHealth.cs`

```csharp
using UnityEngine;
using UnityEngine.Events;

public class HeroHealth : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth { get; private set; }

    [Header("Events")]
    public UnityEvent onTakeDamage;
    public UnityEvent onDeath;

    private Animator _animator;

    private void Awake()
    {
        currentHealth = maxHealth;
        _animator     = GetComponent<Animator>();
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;   // already dead

        currentHealth = Mathf.Max(0, currentHealth - amount);
        onTakeDamage.Invoke();

        _animator?.SetTrigger("Hit");

        VFXManager.Instance?.Play(VFXType.Impact_Physical,
            transform.position + Vector3.up * 1f);

        // AudioService.Instance?.PlaySfx(SfxId.HeroHit);

        if (currentHealth <= 0) Die();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        VFXManager.Instance?.Play(VFXType.Impact_Heal, transform.position);
        // AudioService.Instance?.PlaySfx(SfxId.Heal);
    }

    private void Die()
    {
        _animator?.SetTrigger("Die");
        onDeath.Invoke();

        var locomotion = GetComponent<HeroLocomotion>();
        if (locomotion != null) locomotion.enabled = false;

        // AudioService.Instance?.PlaySfx(SfxId.HeroDeath);
        Debug.Log("[HeroHealth] Hero has died — trigger game over logic.");
    }
}
```

---

## 2. `EnemyHealth.cs`

**Path:** `Assets/_Modules/Village/Enemies/EnemyHealth.cs`

Separating health from `EnemyBrain` keeps the brain focused on AI and makes
health accessible to tower projectiles, AOE spells, and pets without going
through `EnemyBrain`.

```csharp
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 45;
    public int CurrentHealth { get; private set; }

    private EnemyBrain _brain;
    private Animator   _animator;
    private bool       _isDead;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        _brain        = GetComponent<EnemyBrain>();
        _animator     = GetComponent<Animator>();
    }

    public void TakeDamage(int amount)
    {
        if (_isDead) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        _animator?.SetTrigger("Hit");
        if (CurrentHealth <= 0) Die();
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        _animator?.SetTrigger("Die");

        if (TryGetComponent<NavMeshAgent>(out var agent))
            agent.isStopped = true;

        if (_brain != null) _brain.enabled = false;

        // Elite/Boss check.
        var elite = GetComponent<EliteVFXController>();
        if (elite != null)
            elite.OnEliteDeath();
        else
            VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);

        KillComboTracker.Instance?.RegisterKill();
        DecalSpawner.Instance?.SpawnScorch(transform.position);
        // AudioService.Instance?.PlaySfx(SfxId.EnemyDeath);

        Invoke(nameof(DisableSelf), 2.8f);
    }

    private void DisableSelf() => gameObject.SetActive(false);
}
```

> **Note:** Update `EnemyBrain.TryAttack()` to target `HeroHealth` directly
> (already done in WO-69). Update `PetCombatController.PerformAttack()` to
> call `EnemyHealth.TakeDamage()` instead of `EnemyBrain.TakeDamage()`.

---

## 3. Updated `PetCombatController` — use `EnemyHealth`

**Edit** `Assets/_Modules/Pets/PetCombatController.cs` `PerformAttack()`:

```csharp
private void PerformAttack(Transform target)
{
    _animator.SetTrigger("Attack");

    // Prefer EnemyHealth; fall back to EnemyBrain for backwards compat.
    if (target.TryGetComponent<EnemyHealth>(out var health))
        health.TakeDamage(damage);
    else if (target.TryGetComponent<EnemyBrain>(out var brain))
        brain.TakeDamage(damage);   // legacy path

    VFXManager.Instance?.Play(VFXType.Pet_Attack,
        transform.position + Vector3.up * 0.8f);
}
```

---

## 4. Tower damage — use `EnemyHealth`

In tower projectile or `TowerCombat.cs` wherever damage is applied:

```csharp
if (hitEnemy.TryGetComponent<EnemyHealth>(out var health))
    health.TakeDamage(towerDamage);
```

---

## 5. Prefab wiring

| Prefab type | Required components |
|---|---|
| Hero | `HeroHealth`, `HeroLocomotion`, Animator |
| Enemy (all) | `EnemyBrain`, `EnemyHealth`, `AnimatorCullingController`, Animator, NavMeshAgent |
| Enemy (boss/elite) | + `EliteVFXController` |
| Pet (all) | `PetCombatController`, `AuraController`, `AnimatorCullingController` |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Hero/HeroHealth.cs` | **Create** |
| `Assets/_Modules/Village/Enemies/EnemyHealth.cs` | **Create** |
| `Assets/_Modules/Pets/PetCombatController.cs` | **Edit** — use `EnemyHealth` |
| `Assets/_Modules/Village/Buildings/TowerCombat.cs` | **Edit** — use `EnemyHealth` |
| All enemy prefabs | **Edit** — add `EnemyHealth` component |
| Hero prefabs | **Edit** — add `HeroHealth` component |

---

## Acceptance Criteria

- [ ] Hero HP drops when enemies attack (visible in a HUD health bar)
- [ ] Hero `Die()` disables `HeroLocomotion` and fires `onDeath` event
- [ ] Enemy HP drops when hero attacks, tower fires, or pet attacks
- [ ] `EnemyHealth.TakeDamage()` is the single damage entry point for all sources
- [ ] Kill combo fires on `EnemyHealth.Die()`, not from multiple callers
- [ ] `Heal(amount)` restores HP and plays heal VFX
