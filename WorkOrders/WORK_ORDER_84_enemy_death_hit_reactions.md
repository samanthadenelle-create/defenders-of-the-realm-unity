# WORK ORDER 84 — Enemy Death & Hit Reaction Polish (Phase 2)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at Enemy.cs:2248,2648,2805.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — edits to EnemyHealth + new EnemyHitReaction component
**Depends on:** WO-70 (EnemyHealth), WO-50 (VFXManager), WO-61 (CameraShakeManager, DecalSpawner)

> **Extends WO-70** — do not replace EnemyHealth.cs; add the polish listed here
> on top of the canonical version.

---

## Goal

Every enemy hit feels crisp and every death feels satisfying. No "pop and
disappear." Enemies stagger, bleed spark particles on each hit, and explode
on death with lingering scorch marks.

---

## 1. `EnemyHitReaction.cs` — per-hit visual polish

**Path:** `Assets/_Modules/Village/Enemies/EnemyHitReaction.cs`

Add alongside `EnemyHealth` on every enemy prefab. Wire
`EnemyHealth.OnHit → EnemyHitReaction.React(amount)`.

```csharp
using UnityEngine;
using System.Collections;

public class EnemyHitReaction : MonoBehaviour
{
    [Header("Hit Flash")]
    public Renderer[]  renderers;           // All mesh renderers on the enemy
    public Color       hitFlashColor   = new Color(1f, 0.2f, 0.2f, 1f);
    public float       flashDuration   = 0.08f;

    [Header("Knockback")]
    public float       lightKnockback  = 0.6f;   // Units pushed back on normal hit
    public float       heavyKnockback  = 1.8f;   // On hits > heavyThreshold damage
    public int         heavyThreshold  = 20;
    public float       knockbackTime   = 0.12f;

    [Header("VFX")]
    public VFXType     hitSparkVFX     = VFXType.Impact_Physical;
    public VFXType     heavyHitVFX     = VFXType.Impact_ExplosionFire;

    [Header("Camera")]
    public bool        shakeOnHeavyHit = true;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Material[] _originalMats;
    private Material[] _flashMats;
    private CharacterController _cc;
    private UnityEngine.AI.NavMeshAgent _agent;

    private void Awake()
    {
        _cc    = GetComponent<CharacterController>();
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        // Cache originals and create flash versions
        _originalMats = new Material[renderers.Length];
        _flashMats    = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            _originalMats[i] = renderers[i].material;
            _flashMats[i]    = new Material(renderers[i].material);
            _flashMats[i].color = hitFlashColor;
        }
    }

    // ── Called by EnemyHealth.TakeDamage() ────────────────────────────────────

    public void React(int amount, Vector3 attackerPos)
    {
        bool heavy = amount >= heavyThreshold;

        StartCoroutine(FlashRoutine());

        if (heavy)
        {
            Knockback(attackerPos, heavyKnockback);
            VFXManager.Instance?.Play(heavyHitVFX, transform.position + Vector3.up * 0.8f);
            if (shakeOnHeavyHit)
                CameraShakeManager.Instance?.Shake(ShakeTier.Light);
        }
        else
        {
            Knockback(attackerPos, lightKnockback);
            VFXManager.Instance?.Play(hitSparkVFX, transform.position + Vector3.up * 0.8f);
        }

        // AudioService.Instance?.PlaySfx(heavy ? SfxId.EnemyHitHeavy : SfxId.EnemyHitLight);
    }

    private IEnumerator FlashRoutine()
    {
        // Swap to flash materials
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = _flashMats[i];

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = _originalMats[i];
    }

    private void Knockback(Vector3 fromPos, float distance)
    {
        if (_agent != null && !_agent.isStopped) return;   // Let NavMesh handle movement normally

        Vector3 dir = (transform.position - fromPos).normalized;
        dir.y = 0f;
        StartCoroutine(KnockbackMove(dir * distance));
    }

    private IEnumerator KnockbackMove(Vector3 displacement)
    {
        float elapsed = 0f;
        while (elapsed < knockbackTime)
        {
            elapsed += Time.deltaTime;
            float t   = 1f - (elapsed / knockbackTime);   // Decelerate
            _cc?.Move(displacement * t * Time.deltaTime / knockbackTime);
            yield return null;
        }
    }
}
```

---

## 2. Update `EnemyHealth.cs` — call `EnemyHitReaction.React()`

**Edit** the `TakeDamage` method in `EnemyHealth.cs` (WO-70):

```csharp
// Add field at top of class:
private EnemyHitReaction _hitReaction;

// In Awake():
_hitReaction = GetComponent<EnemyHitReaction>();

// In TakeDamage(int amount):
public void TakeDamage(int amount)
{
    if (_isDead) return;
    CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
    _animator?.SetTrigger("Hit");

    // NEW: trigger visual hit reaction
    _hitReaction?.React(amount, Vector3.zero);   // Pass attacker position if available

    if (CurrentHealth <= 0) Die();
}

// Optional overload that carries attacker position for directional knockback:
public void TakeDamage(int amount, Vector3 attackerWorldPos)
{
    if (_isDead) return;
    CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
    _animator?.SetTrigger("Hit");
    _hitReaction?.React(amount, attackerWorldPos);
    if (CurrentHealth <= 0) Die();
}
```

---

## 3. Enhanced `EnemyHealth.Die()` — satisfying death (no pop-and-disappear)

Replace the `Die()` method body in `EnemyHealth.cs` with:

```csharp
private void Die()
{
    if (_isDead) return;
    _isDead = true;

    _animator?.SetTrigger("Die");

    if (TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
        agent.isStopped = true;

    if (_brain != null) _brain.enabled = false;
    if (_hitReaction != null) _hitReaction.enabled = false;

    // ── Death VFX ─────────────────────────────────────────────────────────────
    var elite = GetComponent<EliteVFXController>();
    if (elite != null)
    {
        elite.OnEliteDeath();                   // Elite/boss handles its own death VFX
    }
    else
    {
        // Standard enemy death
        VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);

        // Lingering particles — small secondary burst after 0.3 s
        StartCoroutine(SecondaryDeathBurst());
    }

    // ── Scorch decal ─────────────────────────────────────────────────────────
    DecalSpawner.Instance?.SpawnScorch(transform.position);

    // ── Gameplay callbacks ────────────────────────────────────────────────────
    KillComboTracker.Instance?.RegisterKill();
    CameraShakeManager.Instance?.Shake(ShakeTier.Light);

    // AudioService.Instance?.PlaySfx(SfxId.EnemyDeath);

    // Disable colliders immediately so hero/pets don't interact with corpse
    foreach (var col in GetComponentsInChildren<Collider>())
        col.enabled = false;

    Invoke(nameof(DisableSelf), 2.8f);
}

private IEnumerator SecondaryDeathBurst()
{
    yield return new WaitForSeconds(0.28f);
    VFXManager.Instance?.Play(VFXType.Impact_Physical,
        transform.position + Vector3.up * 0.3f);
}

private void DisableSelf() => gameObject.SetActive(false);
```

---

## 4. Enemy type–specific death VFX table

Wire in `EliteVFXController.OnEliteDeath()` (WO-66) for elites/bosses.
For standard enemies, map VFX by prefab tag or enemy type field:

| Enemy type | `Death_EnemyExplosion` variant | Scorch? | Shake tier |
|---|---|---|---|
| Grunt (standard) | Small dust burst | Yes | Light |
| Armoured Knight | Metal spark burst | Yes | Light |
| Mage | Magic dissolve + arcane sparks | No (use glow decal) | None |
| Elite | `Elite_Death` VFX (WO-66) | Yes | Medium |
| Boss | `Boss_Death` VFX (WO-66) | Yes — large | Heavy |

Set the correct `VFXType` on the prefab's `TowerVFXController` or via an
`EnemyDeathVFXOverride` field on `EnemyHealth`:

```csharp
[Header("Death VFX Override")]
public VFXType deathVFXOverride = VFXType.Death_EnemyExplosion;

// In Die():
VFXManager.Instance?.Play(deathVFXOverride, transform.position);
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Enemies/EnemyHitReaction.cs` | **Create** |
| `Assets/_Modules/Village/Enemies/EnemyHealth.cs` | **Edit** — add `_hitReaction`, enhanced `Die()`, optional overload |
| All enemy prefabs | **Edit** — add `EnemyHitReaction` component, assign `renderers[]` |

---

## Acceptance Criteria

- [ ] Every enemy hit: material flashes red for 0.08 s, then restores
- [ ] Normal hits: small knockback + Impact_Physical spark VFX
- [ ] Heavy hits (≥ 20 dmg): larger knockback + ExplosionFire VFX + Light camera shake
- [ ] Enemy death: explosion VFX, scorch decal, colliders disabled immediately
- [ ] Secondary burst fires 0.28 s after death VFX
- [ ] `DisableSelf` fires at 2.8 s — no pop-and-disappear
- [ ] Elite/Boss death delegates to `EliteVFXController.OnEliteDeath()`
- [ ] No visual errors or null-ref if `EnemyHitReaction` is absent from a prefab
