# WORK ORDER 66 — Boss / Special Enemy VFX Differentiation

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at Enemy.cs:720,2705.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-28
**Priority:** Medium-High
**Scope:** Small — EliteVFXController + EnemyBrain flag + spawn dramatic
**Depends on:** WO-50 (VFXManager), WO-61 (CameraShakeManager), WO-69 (EnemyBrain final version)

---

## Goal

Bosses and elite enemies look and feel significantly more dangerous than
regular mobs. Pulsing aura, stronger death explosion, unique attack VFX,
and a dramatic spawn sequence make it immediately clear when a special enemy
has arrived.

---

## 1. Create `EliteVFXController.cs`

**Path:** `Assets/_Modules/Village/Enemies/EliteVFXController.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// Add to any enemy prefab alongside EnemyBrain to give it elite/boss visuals.
/// Set isElite or isBoss in the Inspector.
/// </summary>
public class EliteVFXController : MonoBehaviour
{
    public bool isElite = false;
    public bool isBoss  = false;

    [Header("Aura")]
    public ParticleSystem auraParticles;
    [Range(1f, 3f)] public float aurapulseSpeed = 1.2f;

    [Header("Spawn")]
    public float spawnDramaticDelay = 0.5f;

    private Light _auraLight;

    private void Start()
    {
        _auraLight = GetComponentInChildren<Light>();
        if (auraParticles != null) auraParticles.Play();

        if (isBoss || isElite)
        {
            StartCoroutine(PulseAura());
            StartCoroutine(DramaticSpawnRoutine());
        }
    }

    // ── Aura ─────────────────────────────────────────────────────────────────

    private IEnumerator PulseAura()
    {
        float baseIntensity = _auraLight != null ? _auraLight.intensity : 0f;
        while (true)
        {
            float pulse = Mathf.Sin(Time.time * aurapulseSpeed * Mathf.PI) * 0.5f + 0.5f;
            if (_auraLight != null)
                _auraLight.intensity = Mathf.Lerp(baseIntensity * 0.6f,
                    baseIntensity * (isBoss ? 1.8f : 1.3f), pulse);
            yield return null;
        }
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    private IEnumerator DramaticSpawnRoutine()
    {
        yield return new WaitForSeconds(spawnDramaticDelay);

        VFXType spawnVfx = isBoss ? VFXType.Boss_Spawn : VFXType.Elite_Spawn;
        VFXManager.Instance?.Play(spawnVfx, transform.position);

        if (isBoss)
        {
            CameraShakeManager.Instance?.Shake(ShakeTier.Heavy, 0.5f);
            // AudioService.Instance?.PlaySfx(SfxId.BossSpawn);
        }
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    /// <summary>Call from EnemyHealth.Die() instead of the normal death VFX.</summary>
    public void OnEliteDeath()
    {
        VFXType deathVfx = isBoss ? VFXType.Boss_Death : VFXType.Elite_Death;
        VFXManager.Instance?.Play(deathVfx, transform.position);

        if (isBoss)
        {
            CameraShakeManager.Instance?.Shake(ShakeTier.Heavy, 0.7f);
            // AudioService.Instance?.PlaySfx(SfxId.BossDeath);
        }
        else
        {
            CameraShakeManager.Instance?.Shake(ShakeTier.Medium, 0.3f);
        }
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    /// <summary>Call from EnemyBrain.TryAttack() for the elite attack VFX.</summary>
    public void OnEliteAttack(Vector3 hitPos)
    {
        VFXType attackVfx = isBoss ? VFXType.Boss_AttackImpact : VFXType.Impact_ExplosionAether;
        VFXManager.Instance?.Play(attackVfx, hitPos);

        if (isBoss)
            CameraShakeManager.Instance?.Shake(ShakeTier.Medium, 0.25f);
    }
}
```

---

## 2. Edit `EnemyHealth.cs` — elite death hook

```csharp
private void Die()
{
    if (animator != null) animator.SetTrigger("Die");
    GetComponent<NavMeshAgent>().isStopped = true;

    var elite = GetComponent<EliteVFXController>();
    if (elite != null)
        elite.OnEliteDeath();
    else
        VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);

    GetComponent<EnemyBrain>().enabled = false;
    Invoke(nameof(DisableEnemy), 2.8f);
}
```

---

## 3. Add `VFXType` entries

```csharp
Elite_Spawn, Elite_Death,
Boss_Spawn, Boss_Death, Boss_AttackImpact,
```

Register prefabs in VFXCatalog:
- `Elite_Spawn`: rising dark energy, lifetime 1 s.
- `Boss_Spawn`: dark energy + lightning bolts, lifetime 2 s.
- `Boss_Death`: massive explosion + lingering smoke + debris, lifetime 4 s.
- `Boss_AttackImpact`: oversized shockwave ring, lifetime 1.5 s.

---

## 4. Prefab setup

For each boss/elite prefab:
1. Add `EliteVFXController`.
2. Set `isBoss = true` (or `isElite = true`).
3. Add a pulsing aura PS child (Mirza Beig dark energy loop recommended).
4. Add a `Light` child (intense, dark purple or red).

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Enemies/EliteVFXController.cs` | **Create** |
| `Assets/_Modules/Village/Enemies/EnemyHealth.cs` | **Edit** — elite death hook |
| `Assets/_Modules/Village/Enemies/EnemyBrain.cs` | **Edit** — call `OnEliteAttack` in `TryAttack` |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add 5 new VFXType entries |
| Boss/elite enemy prefabs | **Edit** — add `EliteVFXController`, wire aura + light |

---

## Acceptance Criteria

- [ ] Boss spawn triggers dark energy burst + heavy camera shake
- [ ] Boss aura pulses visibly during combat
- [ ] Boss death explosion is 2× larger than normal enemy death
- [ ] Elite enemies (non-boss) have a smaller but still noticeable spawn and death VFX
- [ ] Player can immediately distinguish boss from regular enemies on screen
