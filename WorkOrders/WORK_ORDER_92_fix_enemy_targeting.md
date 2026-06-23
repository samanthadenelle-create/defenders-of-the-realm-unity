# WORK ORDER 92 — Fix EnemyBrain Targeting: Enemies Ignore Hero and Towers

**Status:** BUG FIX — READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical — game-breaking blocker
**Scope:** Medium — targeted edit to EnemyBrain + new EnemyTarget tag system
**Observed:** Enemies walk past the hero, ignore towers, and proceed without
             engaging anything. Combat never starts despite enemies being alive.

---

## Root Cause Diagnosis

`EnemyBrain.FindClosestHero()` uses:

```csharp
var heroes = FindObjectsOfType<HeroLocomotion>();
```

This fails silently (returns empty array, target = null) when any of these
conditions is true:

| Condition | Cause |
|---|---|
| `HeroLocomotion` not on hero | Component missing or on wrong prefab |
| Hero is on `DontDestroyOnLoad` scene, enemy is in a different scene | `FindObjectsOfType` only searches the active scene |
| Hero was deactivated or destroyed | `activeInHierarchy == false` |
| No hero in the village scene at all | Wrong scene loaded |
| `NavMeshAgent` has no valid path | NavMesh not baked, enemy can't reach the target |

When target = null, `_state = State.Idle` every frame — the enemy appears to
walk in a straight line (following its initial spawn velocity) and never engages.

---

## Fix

### 1. Replace `FindClosestHero()` with a tag-based `FindClosestTarget()`

**Tags to add in Unity:** `HeroTarget`, `HeartTarget`

On the Hero root GameObject: Tag = `HeroTarget`
On the Heart/World Tree: Tag = `HeartTarget`

```csharp
// In EnemyBrain.cs — replace FindClosestHero() entirely

private Transform FindClosestTarget()
{
    Transform best = null;
    float bestDist = float.MaxValue;

    // 1. Search by tag — faster and cross-scene safe
    SearchByTag("HeroTarget", ref best, ref bestDist);
    SearchByTag("HeartTarget", ref best, ref bestDist);

    // 2. Fallback: component search (slower, only if tags aren't set yet)
    if (best == null)
    {
        var heroLoco = FindObjectOfType<HeroLocomotion>();
        if (heroLoco != null && heroLoco.gameObject.activeInHierarchy)
            best = heroLoco.transform;
    }

    if (best == null)
        Debug.LogWarning($"[EnemyBrain] {name}: No target found. " +
            "Ensure Hero has tag 'HeroTarget' and HeroLocomotion component.", this);

    return best;
}

private void SearchByTag(string tag, ref Transform best, ref float bestDist)
{
    try
    {
        var objs = GameObject.FindGameObjectsWithTag(tag);
        foreach (var obj in objs)
        {
            if (!obj.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, obj.transform.position);
            if (d < bestDist) { bestDist = d; best = obj.transform; }
        }
    }
    catch (UnityException)
    {
        // Tag not defined in TagManager — safe to ignore during setup
    }
}
```

Update `Update()` to call `FindClosestTarget()`:

```csharp
private void Update()
{
    if (_isDead) return;
    _currentTarget = FindClosestTarget();
    // ... rest of state machine unchanged
}
```

---

### 2. NavMesh guard — log when enemy can't path

Add this to the Chase state so silent pathfinding failures are visible:

```csharp
else if (dist <= detectionRange)
{
    _state           = State.Chase;
    _agent.isStopped = false;
    _agent.speed     = chaseSpeed;

    var path = new UnityEngine.AI.NavMeshPath();
    if (_agent.CalculatePath(_currentTarget.position, path) &&
        path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
    {
        _agent.SetDestination(_currentTarget.position);
    }
    else
    {
        Debug.LogWarning($"[EnemyBrain] {name}: No NavMesh path to target. " +
            "Verify NavMesh is baked and enemy spawns ON the NavMesh.", this);
        _state = State.Idle;
    }
}
```

---

### 3. Scene setup checklist

Run this **before** testing:

- [ ] Open **Window → AI → Navigation → Bake** — confirm NavMesh covers all enemy paths
- [ ] Select the Hero root GameObject → Inspector → Tag → set to **`HeroTarget`**
- [ ] Select the Heart/World Tree → Tag → set to **`HeartTarget`**
- [ ] In **Edit → Project Settings → Tags and Layers**: add tags `HeroTarget` and `HeartTarget`
- [ ] Enemy prefabs spawn **on** the NavMesh (not above or outside it)
- [ ] Hero prefab has `HeroLocomotion` component on the **root** (not a child)
- [ ] Confirm `EnemyBrain` is enabled on enemy prefabs (not disabled by default)

---

### 4. `EnemyDiagnostic.cs` — temporary debug helper

**Path:** `Assets/Editor/EnemyDiagnostic.cs`  
Add this temporarily to an enemy in the scene to get a live readout:

```csharp
#if UNITY_EDITOR
using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class EnemyDiagnostic : MonoBehaviour
{
    private EnemyBrain _brain;

    private void Awake() => _brain = GetComponent<EnemyBrain>();

    private void Update()
    {
        // Only log every 2 seconds to avoid spam
        if (Time.frameCount % 120 != 0) return;

        var heroes = GameObject.FindGameObjectsWithTag("HeroTarget");
        Debug.Log($"[Diag] {name} | State: {_brain._state} | " +
                  $"HeroTargets found: {heroes.Length} | " +
                  $"NavMesh: {GetComponent<UnityEngine.AI.NavMeshAgent>()?.isOnNavMesh}");
    }
}
#endif
```

Attach to one enemy prefab in the scene, play, watch Console — the log will
immediately show whether targets are found and whether the NavMesh is valid.
Remove `EnemyDiagnostic` from the prefab after the bug is confirmed fixed.

---

## Files to Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | **Edit** — replace `FindClosestHero()` with `FindClosestTarget()`, add NavMesh path guard |
| Hero root GameObject in scene | **Edit** — set Tag to `HeroTarget` |
| Heart / World Tree in scene | **Edit** — set Tag to `HeartTarget` |
| Unity Tags (Edit → Project Settings → Tags and Layers) | **Edit** — add `HeroTarget`, `HeartTarget` |
| `Assets/Editor/EnemyDiagnostic.cs` | **Create** — temporary; delete after fix confirmed |

---

## Acceptance Criteria

- [ ] Enemies detect and chase the hero within `detectionRange` (confirmed via Gizmo spheres)
- [ ] `EnemyDiagnostic` logs show `HeroTargets found: 1` when hero is in scene
- [ ] Enemies stop at `attackRange` and enter the Attack state
- [ ] `HeroHealth.TakeDamage()` is called — hero HP visibly drops
- [ ] If hero is absent, enemies path toward and attack the Heart instead
- [ ] No `[EnemyBrain] No target found` warnings in Console after tags are set
- [ ] No `No NavMesh path` warnings after NavMesh is baked
- [ ] Fix works in both the Village scene and the "Defend the Tower" scene
