# WORK ORDER 49 — Enemy AI Fix (Immediate Blocker)

**Status:** DONE (reconciled 2026-08-09 from the tree, NOT felt-verified — EnemyBrain.cs rebuilt in Village/Enemies with target eval; canon §7 component targeting; wave loop WIRED per canon §8)
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Small — single script, prefab wiring
**Depends on:** None (standalone fix)

---

## Goal

Stop enemies from walking straight past the hero. Replace the old movement
code with a proper state-machine brain (`EnemyBrain`) that detects the closest
hero, chases at range, and stops to attack when close enough.

---

## 1. Create `EnemyBrain.cs`

**Path:** `Assets/_Modules/Enemies/EnemyBrain.cs`

```csharp
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBrain : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 12f;
    public float attackRange    = 2.2f;
    public float stopDistance   = 2f;

    [Header("Movement")]
    public float chaseSpeed  = 3.8f;
    public float patrolSpeed = 2f;

    private NavMeshAgent agent;
    private Transform    currentTarget;
    private Animator     animator;

    private enum State { Patrol, Chase, Attack }
    private State currentState = State.Patrol;

    private void Awake()
    {
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        currentTarget = FindClosestHero();

        if (currentTarget == null)
        {
            currentState      = State.Patrol;
            agent.isStopped   = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);

        if (distance <= attackRange)
        {
            currentState    = State.Attack;
            agent.isStopped = true;
            Attack();
        }
        else if (distance <= detectionRange)
        {
            currentState        = State.Chase;
            agent.isStopped     = false;
            agent.speed         = chaseSpeed;
            agent.SetDestination(currentTarget.position);
        }
        else
        {
            currentState    = State.Patrol;
            agent.isStopped = false;
            agent.speed     = patrolSpeed;
        }

        UpdateAnimator();
    }

    private Transform FindClosestHero()
    {
        var heroes  = FindObjectsOfType<HeroLocomotion>();
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var hero in heroes)
        {
            if (!hero.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, hero.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = hero.transform;
            }
        }
        return closest;
    }

    private void Attack()
    {
        // TODO: Call attack animation / damage logic here
        if (animator != null)
            animator.SetTrigger("Attack");
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetBool("IsMoving", !agent.isStopped && agent.velocity.magnitude > 0.1f);
        animator.SetFloat("Speed",   agent.velocity.magnitude);
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

## 2. Prefab wiring

For every enemy prefab in `Assets/Resources/Enemies/` (or wherever they live):

1. Open the prefab.
2. Add `EnemyBrain` component to the root GameObject.
3. Remove (or disable) any old component that was calling `agent.SetDestination`
   directly — the old mover script must not fight `EnemyBrain` for the agent.
4. Confirm the prefab has a `NavMeshAgent` component (required by the
   `[RequireComponent]` guard — Unity will add one automatically if missing,
   but double-check `stoppingDistance` matches `stopDistance` ≈ 2 f).
5. If the prefab has an `Animator`, confirm it has:
   - `IsMoving` (bool) parameter
   - `Speed` (float) parameter
   - `Attack` (trigger) parameter
   *(Missing parameters are silently ignored at runtime, but add them now to
   avoid animator warnings.)*

---

## 3. Performance note — `FindObjectsOfType` in Update

`FindObjectsOfType<HeroLocomotion>()` is O(n) over all scene objects and runs
every frame. Fine for now; if frame time becomes an issue, replace with a
static registry:

```csharp
// HeroLocomotion.cs — add:
public static readonly List<HeroLocomotion> All = new();
private void OnEnable()  => All.Add(this);
private void OnDisable() => All.Remove(this);

// EnemyBrain.FindClosestHero — replace FindObjectsOfType call:
foreach (var hero in HeroLocomotion.All) { ... }
```

Make this change in the same commit if `HeroLocomotion.cs` is already being
touched; otherwise defer to a polish pass.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Enemies/EnemyBrain.cs` | **Create** — full source above |
| Enemy prefabs (all) | **Edit** — add `EnemyBrain`, remove old mover |
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs` | **Edit (optional)** — add static `All` registry if perf becomes a concern |

---

## Acceptance Criteria

- [ ] Enemies detect the hero within `detectionRange` and begin chasing
- [ ] Enemies stop and play the Attack trigger when within `attackRange`
- [ ] Enemies do **not** walk past or through the hero
- [ ] No compile errors; `[RequireComponent]` guard prevents missing-agent mistakes
- [ ] Yellow/red gizmos visible in Scene view when an enemy is selected
- [ ] No `NullReferenceException` when no hero is present in the scene
- [ ] Old agent-destination mover scripts are removed from all enemy prefabs
