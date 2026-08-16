# WORK ORDER 53 — Animator Culling Strategies (Mobile Performance)

**Status:** READY — PARTIAL - remainder named by the 2026-08-14 phantom sweep

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Most of this WO is present in HEAD; a named
> remainder is outstanding. No per-WO path:line was recorded here: see the 2026-08-14 phantom sweep for the
> implementation site and the remainder. Do not re-implement the shipped part.
> (Any prior dated reconciliation note on this file stands - see the preserved line below.)
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-28
**Priority:** High
**Scope:** Small — one script, prefab wiring across all character types
**Depends on:** None (standalone; WO-51 calls `ApplyTier()` defined here)

---

## Goal

Dramatically reduce CPU animation cost on mobile by replacing Unity's
`Always Animate` default with distance-based culling. Proper culling cuts
animation overhead by 60–90% during busy waves without any visible change
near the camera.

---

## 1. Create `AnimatorCullingController.cs`

**Path:** `Assets/_Modules/Animation/AnimatorCullingController.cs`

```csharp
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatorCullingController : MonoBehaviour
{
    [Tooltip("Distance at which animator switches to CullUpdateTransforms.")]
    public float updateTransformsDistance = 25f;

    [Tooltip("Distance at which animator is fully culled (no updates at all).")]
    public float cullCompletelyDistance   = 45f;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Camera.main == null) return;

        float sqrDist = (transform.position -
                         Camera.main.transform.position).sqrMagnitude;

        float sqrCull    = cullCompletelyDistance   * cullCompletelyDistance;
        float sqrUpdate  = updateTransformsDistance  * updateTransformsDistance;

        if (sqrDist > sqrCull)
            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
        else if (sqrDist > sqrUpdate)
            _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        else
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void OnBecameInvisible()
    {
        if (_animator.cullingMode != AnimatorCullingMode.CullCompletely)
            _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    private void OnBecameVisible()
    {
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    /// <summary>
    /// Called by PerformanceManager (WO-51) when the quality tier changes.
    /// </summary>
    public void ApplyTier(PerformanceTier tier)
    {
        // Decide which distance table to use based on this object's tag.
        if (CompareTag("Hero") || CompareTag("Player"))
        {
            updateTransformsDistance = tier.heroCullUpdateDistance;
            cullCompletelyDistance   = tier.heroCullUpdateDistance * 2f;
        }
        else if (CompareTag("Enemy"))
        {
            updateTransformsDistance = tier.enemyCullUpdateDistance;
            cullCompletelyDistance   = tier.enemyCullUpdateDistance * 1.8f;
        }
        else // Pet, NPC, Villager
        {
            updateTransformsDistance = tier.npcCullUpdateDistance;
            cullCompletelyDistance   = tier.npcCullUpdateDistance * 2.3f;
        }
    }
}
```

---

## 2. Apply to all character prefabs

For every prefab that has an Animator (Heroes, Enemies, Pets, NPCs, Villagers):

1. Open the prefab.
2. Add `AnimatorCullingController` to the root GameObject.
3. Set distances per character type:

| Character type | `updateTransformsDistance` | `cullCompletelyDistance` |
|---|---|---|
| Hero (player-controlled) | 80 | 160 |
| Enemy (regular) | 25 | 45 |
| Enemy (boss) | 40 | 80 |
| Pet | 30 | 60 |
| Background NPC / Villager | 15 | 35 |

4. On the `Animator` component itself: set **Culling Mode** to
   `Cull Update Transforms` as the new static default. The controller
   overrides this at runtime based on distance.

5. Set **Update Mode** to `Normal` unless unscaled time is specifically needed
   (e.g. a pause-screen animation).

---

## 3. Hero override in `HeroLocomotion.cs`

Player-controlled heroes should almost never have animation culled.
Add to `HeroLocomotion.cs`:

```csharp
private void OnEnable()
{
    var culling = GetComponent<AnimatorCullingController>();
    if (culling != null)
    {
        culling.updateTransformsDistance = 80f;
        culling.cullCompletelyDistance   = 160f;
    }
}
```

---

## 4. Updated `EnemyBrain.cs` (requires `AnimatorCullingController`)

Replace the WO-49 version with this one that enforces the culling dependency:

```csharp
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AnimatorCullingController))]
public class EnemyBrain : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 15f;
    public float attackRange    = 2.5f;

    [Header("Movement")]
    public float chaseSpeed  = 4.2f;
    public float patrolSpeed = 2.2f;

    private NavMeshAgent             agent;
    private Animator                 animator;
    private AnimatorCullingController cullingController;
    private Transform                currentTarget;

    private enum State { Idle, Patrol, Chase, Attack }
    private State currentState = State.Idle;

    private void Awake()
    {
        agent             = GetComponent<NavMeshAgent>();
        animator          = GetComponent<Animator>();
        cullingController = GetComponent<AnimatorCullingController>();
    }

    private void Update()
    {
        currentTarget = FindClosestHero();

        if (currentTarget == null)
        {
            currentState    = State.Idle;
            agent.isStopped = true;
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
            currentState = State.Chase;
            agent.isStopped = false;
            agent.speed     = chaseSpeed;
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
        // Use static registry from HeroLocomotion if available (see WO-49 §3).
        var heroes  = FindObjectsOfType<HeroLocomotion>();
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var hero in heroes)
        {
            if (!hero.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, hero.transform.position);
            if (d < minDist) { minDist = d; closest = hero.transform; }
        }
        return closest;
    }

    private void Attack()
    {
        if (animator != null) animator.SetTrigger("Attack");
        // TODO: damage call
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        bool moving = !agent.isStopped && agent.velocity.magnitude > 0.1f;
        animator.SetBool("IsMoving", moving);
        animator.SetFloat("Speed", agent.velocity.magnitude);
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

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Animation/AnimatorCullingController.cs` | **Create** |
| `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | **Edit** — add `[RequireComponent(typeof(AnimatorCullingController))]` + `cullingController` field |
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs` | **Edit** — hero override in `OnEnable` |
| All character prefabs | **Edit** — add component, set distances per table above |

---

## Acceptance Criteria

- [ ] Enemies at >45 m are fully culled (animator makes zero updates per frame)
- [ ] Enemies at 25–45 m use `CullUpdateTransforms` (poses update, no IK/FK)
- [ ] Enemies within 25 m use `AlwaysAnimate` (full fidelity)
- [ ] Hero never culls during normal gameplay (`updateTransformsDistance = 80`)
- [ ] `PerformanceManager.ApplyAnimatorCulling()` successfully adjusts distances at runtime
- [ ] No animation popping when enemies cross distance thresholds
- [ ] 60 FPS maintained on a mid-range Android device during 20+ enemy waves
