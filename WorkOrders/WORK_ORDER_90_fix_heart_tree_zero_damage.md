# WORK ORDER 90 — Fix Heart/Tree Taking 0 Damage from Enemies

**Status:** BUG FIX — READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Small — create HeartHealth.cs + targeted edit to EnemyBrain
**Observed:** Screenshot annotation — enemies attack the world tree / The Heart
             structure at 30% HP threshold but structure takes 0 damage.

---

## Root Cause

`EnemyBrain.TryAttack()` calls:

```csharp
if (_currentTarget.TryGetComponent<HeroHealth>(out var heroHealth))
    heroHealth.TakeDamage(damage);
```

When enemies switch to attacking The Heart structure, `_currentTarget` is the
Heart GameObject. It has no `HeroHealth` component, so `TryGetComponent`
returns false and **no damage is ever dealt**.

Secondary cause: The Heart may have no health component at all — no
`HeartHealth` or `StructureHealth` script exists — so there is nothing to
receive damage even if targeting was correct.

---

## 1. Create `HeartHealth.cs`

**Path:** `Assets/_Modules/Village/HeartHealth.cs`

```csharp
using UnityEngine;
using UnityEngine.Events;

public class HeartHealth : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth    = 200;
    public int currentHealth { get; private set; }

    [Header("Trigger Threshold")]
    [Tooltip("The 'Heart is Breached' event fires when HP drops to this fraction.")]
    [Range(0.1f, 0.9f)]
    public float breachThreshold = 0.30f;   // 30% as shown in screenshot

    [Header("Events")]
    public UnityEvent onTakeDamage;
    public UnityEvent onBreached;            // Fires at breach threshold
    public UnityEvent onDestroyed;

    private bool _breachFired;
    private bool _isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (_isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        onTakeDamage.Invoke();

        Debug.Log($"[HeartHealth] Took {amount} dmg. HP: {currentHealth}/{maxHealth}");

        // Fire breach event once when threshold is crossed
        if (!_breachFired && currentHealth <= maxHealth * breachThreshold)
        {
            _breachFired = true;
            onBreached.Invoke();
            Debug.Log("[HeartHealth] BREACHED — triggering Last Stand dialog.");
        }

        VFXManager.Instance?.Play(VFXType.Impact_Physical,
            transform.position + Vector3.up * 1f);

        if (currentHealth <= 0)
            Destroy();
    }

    private void Destroy()
    {
        _isDead = true;
        onDestroyed.Invoke();
        VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);
        CameraShakeManager.Instance?.Shake(ShakeTier.Heavy);
        Debug.Log("[HeartHealth] Heart destroyed — game over.");
    }

    // Optional: show HP in Inspector during play
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
```

---

## 2. Update `EnemyBrain.TryAttack()` — target HeartHealth as well

**Edit** `EnemyBrain.cs` (WO-69 canonical version). In `TryAttack()`:

```csharp
public void TryAttack()
{
    if (Time.time < _nextAttackTime) return;
    _nextAttackTime = Time.time + attackCooldown;

    _animator.SetTrigger("Attack");

    if (_currentTarget == null) return;

    // Try HeroHealth first
    if (_currentTarget.TryGetComponent<HeroHealth>(out var hero))
    {
        hero.TakeDamage(damage);
    }
    // Fall back to HeartHealth if attacking the structure
    else if (_currentTarget.TryGetComponent<HeartHealth>(out var heart))
    {
        heart.TakeDamage(damage);
    }

    VFXManager.Instance?.Play(VFXType.Impact_Physical,
        transform.position + Vector3.up * 1.2f);
}
```

---

## 3. Update `EnemyBrain.FindClosestTarget()` — include The Heart

The current `FindClosestHero()` only returns `HeroLocomotion` transforms.
Add a fallback that returns the Heart if no hero is found or if the enemy
has breached the inner ring:

```csharp
private Transform FindClosestTarget()
{
    // 1. Try to find the hero
    var heroes  = FindObjectsOfType<HeroLocomotion>();
    Transform closest = null;
    float minDist = float.MaxValue;

    foreach (var h in heroes)
    {
        if (!h.gameObject.activeInHierarchy) continue;
        float d = Vector3.Distance(transform.position, h.transform.position);
        if (d < minDist) { minDist = d; closest = h.transform; }
    }

    if (closest != null) return closest;

    // 2. No hero found — target The Heart as fallback
    var heart = FindObjectOfType<HeartHealth>();
    return heart != null ? heart.transform : null;
}
```

Update the `Update()` method to call `FindClosestTarget()` instead of
`FindClosestHero()`.

---

## 4. Wire in scene

On the Heart / World Tree GameObject:

1. Add component: `HeartHealth`
2. Set `maxHealth = 200`, `breachThreshold = 0.30`
3. Wire `onBreached` UnityEvent → the method that shows the
   "Heart is Breached" dialog (currently fires via some trigger — replace
   with this event)
4. Wire `onDestroyed` UnityEvent → Game Over handler
5. Set the Heart GameObject's Layer to `Enemy` or a new `Structure` layer
   so `EnemyBrain.attackRange` overlap sphere can detect it

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/HeartHealth.cs` | **Create** |
| `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | **Edit** — update `TryAttack()` + `FindClosestTarget()` |
| Heart / World Tree GameObject in scene | **Edit** — add `HeartHealth`, wire events |

---

## Acceptance Criteria

- [ ] Enemy attacks the Heart (world tree) GameObject and HP drops visibly
- [ ] `HeartHealth.currentHealth` decreases by the enemy's `damage` value on each hit
- [ ] Impact VFX plays on the Heart on every hit
- [ ] `onBreached` event fires exactly once when HP crosses 30% — triggers the
      "Heart is Breached" dialog
- [ ] `onBreached` does NOT fire again on subsequent hits
- [ ] `onDestroyed` fires when HP reaches 0, plays death VFX + Heavy shake
- [ ] If no hero is in range, enemies path to and attack the Heart instead
- [ ] Hero attacks still work normally — `HeroHealth.TakeDamage()` not affected
