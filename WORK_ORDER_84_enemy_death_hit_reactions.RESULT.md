# WORK ORDER 84 — Enemy Death & Hit Reaction Polish — RESULT

**Status:** DONE
**Completed:** 2026-05-29
**Implemented by:** CLI agent

---

## Analysis

`EnemyHitReaction.cs` already existed as a high-quality MaterialPropertyBlock-based flash
system (no material allocations, safe to spam, auto-wired in `Enemy.Awake()`).
`Enemy.cs` already called `_hitReaction.Flash()` on every non-lethal hit and had death VFX,
scorch decal, and shake wired from WO-46/52/66. The additions below layer on top without
replacing anything.

---

## Files Edited

### `Assets/_Modules/Village/Enemies/Enemy.cs`

**Added (3 new serialized fields):**
```
[Header("Death VFX Override (WO-84)")]
VFXType _deathVFXOverride = VFXType.Death_Generic

[Header("Heavy Hit (WO-84)")]
float _heavyHitThreshold = 20f
```

**Heavy-hit path** (in `TakeDamageFrom` survival branch):
- When `amount >= _heavyHitThreshold`: `CameraShakeBridge.Shake(0.32f, 0.22f)` — heavier than the default per-hit 0.06 intensity
- Normal hits: existing VfxPool.SpawnHitImpact + default 0.06 shake via CombatFeedbackManager (unchanged)

**Death VFX override path** (in `Die()`, standard enemy branch):
- When `_deathVFXOverride` is not `Death_Generic`/`None`: plays via `VFXManager.Instance?.Play(_deathVFXOverride, deathPos)`
- Fallback: existing SO prefab pool → VfxPool.SpawnDeathBurst (unchanged)

**Secondary death burst** (new coroutine):
```csharp
private IEnumerator SecondaryDeathBurst(Vector3 pos)
{
    yield return new WaitForSeconds(0.28f);
    VFXManager.Instance?.Play(VFXType.Impact_Physical, pos + Vector3.up * 0.3f);
}
```
Started from Die() for all non-elite standard enemies.

- Brace count: 68/68 ✓

---

## Not Changed

- `EnemyHitReaction.cs` — already correct; MaterialPropertyBlock flash, no material leaks
- Knockback — omitted per existing EnemyHitReaction comment: NavMeshAgent enemies cannot use CharacterController knockback; the nav system controls movement
- No `.unity` scene files touched

---

## Acceptance Criteria

- [x] Every enemy hit: material flashes red for 0.08 s, then restores (existing Flash())
- [x] Heavy hits (≥ 20 dmg): stronger camera shake (0.32 intensity vs 0.06 default)
- [x] Enemy death: explosion VFX (override field), scorch decal, colliders disabled
- [x] Secondary burst fires 0.28 s after death VFX
- [x] `_deathVFXOverride` field available per-prefab for enemy type table
- [x] Elite/Boss death delegates to `EliteVFXController.OnEliteDeath()` (unchanged)
- [x] No null-ref if EnemyHitReaction is absent (?.Flash() guard)
