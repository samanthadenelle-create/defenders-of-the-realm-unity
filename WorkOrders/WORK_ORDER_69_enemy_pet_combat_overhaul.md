# WORK ORDER 69 — Complete Enemy + Pet Combat Overhaul

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: combat pivot + smart-composition rebuild + pets-to-Echoes reframe)
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Large — replaces/supersedes EnemyBrain (WO-49/53), adds PetCombatController
**Note:** This WO is the canonical enemy AI. Previous versions in WO-49 and WO-53
should be replaced with the scripts here.

---

## Goal

Enemies engage the hero, stop and fight at range, use a cooldown attack with a
real damage call, and die properly. Pets scan for enemies and attack
automatically. ATB combat manager hooks into the same `TryAttack()` surface.

---

## 1. `EnemyBrain.cs` — final version

**Path:** `Assets/_Modules/Village/Enemies/EnemyBrain.cs`

```csharp
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AnimatorCullingController))]
public class EnemyBrain : MonoBehaviour
{
    [Header("Stats")]
    public int   maxHealth     = 45;
    public int   damage        = 12;
    public float attackCooldown = 1.8f;

    [Header("Detection")]
    public float detectionRange = 15f;
    public float attackRange    = 2.5f;

    [Header("Movement")]
    public float chaseSpeed  = 4.2f;
    public float patrolSpeed = 2.2f;

    // ── State ─────────────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator     _animator;
    private int          _currentHealth;
    private Transform    _currentTarget;
    private float        _nextAttackTime;
    private bool         _isDead;

    private enum State { Idle, Chase, Attack }
    private State _state = State.Idle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent         = GetComponent<NavMeshAgent>();
        _animator      = GetComponent<Animator>();
        _currentHealth = maxHealth;
    }

    private void Update()
    {
        if (_isDead) return;

        _currentTarget = FindClosestHero();

        if (_currentTarget == null)
        {
            _state = State.Idle;
            _agent.isStopped = true;
            UpdateAnimator();
            return;
        }

        float dist = Vector3.Distance(transform.position, _currentTarget.position);

        if (dist <= attackRange)
        {
            _state = State.Attack;
            _agent.isStopped = true;
            TryAttack();
        }
        else if (dist <= detectionRange)
        {
            _state           = State.Chase;
            _agent.isStopped = false;
            _agent.speed     = chaseSpeed;
            _agent.SetDestination(_currentTarget.position);
        }
        else
        {
            _state           = State.Idle;
            _agent.isStopped = true;
        }

        UpdateAnimator();
    }

    // ── Combat ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Public so ATBCombatManager can trigger the enemy's attack on turn end.
    /// </summary>
    public void TryAttack()
    {
        if (Time.time < _nextAttackTime) return;
        _nextAttackTime = Time.time + attackCooldown;

        _animator.SetTrigger("Attack");

        if (_currentTarget != null &&
            _currentTarget.TryGetComponent<HeroHealth>(out var heroHealth))
        {
            heroHealth.TakeDamage(damage);
        }

        VFXManager.Instance?.Play(
            VFXType.Impact_Physical,
            transform.position + Vector3.up * 1.2f);

        // AudioService.Instance?.PlaySfx(SfxId.EnemyAttack);
    }

    public void TakeDamage(int amount)
    {
        if (_isDead) return;
        _currentHealth -= amount;
        _animator.SetTrigger("Hit");
        if (_currentHealth <= 0) Die();
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        _animator.SetTrigger("Die");
        _agent.isStopped = true;
        enabled = false;

        var elite = GetComponent<EliteVFXController>();
        if (elite != null)
            elite.OnEliteDeath();
        else
            VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);

        KillComboTracker.Instance?.RegisterKill();
        DecalSpawner.Instance?.SpawnScorch(transform.position);

        Invoke(nameof(DisableSelf), 2.8f);
    }

    private void DisableSelf() => gameObject.SetActive(false);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Transform FindClosestHero()
    {
        // Use static registry if available (see HeroLocomotion.All note in WO-49).
        var heroes  = FindObjectsOfType<HeroLocomotion>();
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var h in heroes)
        {
            if (!h.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < minDist) { minDist = d; closest = h.transform; }
        }
        return closest;
    }

    private void UpdateAnimator()
    {
        if (_animator == null) return;
        bool moving = !_agent.isStopped && _agent.velocity.magnitude > 0.1f;
        _animator.SetBool("IsMoving", moving);
        _animator.SetFloat("Speed",   _agent.velocity.magnitude);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
```

---

## 2. `PetCombatController.cs` — final version

**Path:** `Assets/_Modules/Pets/PetCombatController.cs`

```csharp
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PetCombatController : MonoBehaviour
{
    [Header("Stats")]
    public int   damage        = 9;
    public float attackRange   = 9f;
    public float attackCooldown = 2.2f;

    private Animator  _animator;
    private float     _nextAttackTime;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        var target = FindClosestEnemy();
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist <= attackRange && Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + attackCooldown;
            PerformAttack(target);
        }
    }

    private void PerformAttack(Transform target)
    {
        _animator.SetTrigger("Attack");

        if (target.TryGetComponent<EnemyBrain>(out var enemy))
            enemy.TakeDamage(damage);

        VFXManager.Instance?.Play(
            VFXType.Pet_Attack,
            transform.position + Vector3.up * 0.8f);

        // AudioService.Instance?.PlaySfx(SfxId.PetAttack);
    }

    private Transform FindClosestEnemy()
    {
        var enemies  = FindObjectsOfType<EnemyBrain>();
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var e in enemies)
        {
            if (!e.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < minDist) { minDist = d; closest = e.transform; }
        }
        return closest;
    }
}
```

---

## 3. Prefab wiring summary

| Prefab type | Required components |
|---|---|
| Enemy (all) | `EnemyBrain`, `AnimatorCullingController`, Animator, NavMeshAgent |
| Enemy (boss/elite) | + `EliteVFXController` |
| Pet (all) | `PetCombatController`, `AuraController`, `AnimatorCullingController` |

Remove any old movement scripts that call `agent.SetDestination` directly —
they will fight `EnemyBrain` for the agent.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | **Replace** — use full version above |
| `Assets/_Modules/Pets/PetCombatController.cs` | **Create** (or replace stub) |
| All enemy prefabs | **Edit** — ensure components per table above |
| All pet prefabs | **Edit** — add `PetCombatController` |

---

## Acceptance Criteria

- [ ] Enemies detect and chase the hero within `detectionRange`
- [ ] Enemies stop and attack at `attackRange` with `attackCooldown` between attacks
- [ ] `heroHealth.TakeDamage()` is called on each attack — hero HP visibly drops
- [ ] Enemy death: triggers die animation, plays death VFX, disables GO after 2.8 s
- [ ] Kill combo tracker is notified on every death
- [ ] Pets scan, find enemies, and attack every `attackCooldown` seconds
- [ ] `EnemyBrain.TryAttack()` is callable from `ATBCombatManager` (WO-68)
- [ ] No enemies walk past the hero in Village or Dungeon scenes
