# WORK ORDER 955 — VFXManager.Acquire NRE: the pool's free list hands back a DESTROYED host

**Status:** READY TO IMPLEMENT — remaining scope only. The Acquire guard LANDED 2026-08-10 (same
wave, CLI direct, committed): dead slots are evicted with a `FlowTrace.Warn` naming WO-955 and
Acquire self-heals via fresh instantiate; the NRE class is closed. REMAINING (this ticket's live
scope): (a) the teardown-destroyer hunt — the new Warn IS the instrument, the next capture that
fires it names the poisoning window; (b) the pool-shape regression case.
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 955 → 956 in the same edit)
**Silo:** Village/Vfx (VFXManager pool) — no overlap with live lanes
**Origin:** captured exception, owner session 2026-08-10 16:12:25 UTC (break_12_exception), during
repeated arena deaths (the near-death aura churning across arena teardowns).

## 1. The proving stack (§12 satisfied)

```
NullReferenceException
UnityEngine.GameObject.get_transform ()
DeNelle.Village.VFXManager.Acquire (VFXType, VFXCatalog+Entry&) at VFXManager.cs:876
DeNelle.Village.VFXManager.PlayLoop (...) at :537
DeNelle.Village.VFXManager.PlayAura (...) at :359
DeNelle.Village.HeroHpStateAura.Apply (...) at HeroHpStateAura.cs:285
```

A pooled host GameObject was DESTROYED (arena/scene teardown window) while still sitting in the
pool's free list; the next Acquire dereferenced its transform.

**Second proving stack, same session, 16:15:45 UTC — DIFFERENT caller, DIFFERENT scene:**
`EnemyAuraVFX.StartHeld (:219) → PlayAura (:359) → PlayLoop (:537) → Acquire (:876)` in
**`dg_ember_deep`** (she had left the overworld entirely). Confirms: (a) the poisoned free list
PERSISTS ACROSS SCENE LOADS (the pool is session-long), (b) every Acquire caller is exposed — the
fix belongs at the pool seam, never per-caller. Context: the reclaim path's known
asymmetries (the 08-06 loop-cap RCA: "the only reclaim frees DESTROYED hosts — pooled objects are
never destroyed" — an invariant this session proves CAN be violated across scene/arena teardown).

## 2. Fix shape (verify at source)

- `Acquire` must treat a Unity-null host in the free list as a dead slot: skip + evict + replace
  (rebuild the host) with ONE `FlowTrace.Warn("VFX", "pool slot host destroyed — evicted (who/where)")`
  — never throw, never silently shrink capacity. Audit the same dereference in the oneshot/light/unit
  sub-pools (`pool hosts free=16 inUse=0/16 units free=32 lights free=16` families).
- Find WHO destroys pooled hosts on the arena return/teardown path (the invariant-breaker) — if a
  scene unload legitimately destroys DDOL-less pool roots, the pool must re-home or rebuild on scene
  change; cite the destroyer in the RESULT.
- Regression: a pool-shape case that destroys a free-list host then Acquires — asserts no throw, a
  Warn, and a working handle. Note the ONESHOT 40/40 saturation stays a SEPARATE open item — do not
  bundle (standing rule).

## 3. What NOT to touch

The loop-cap derivation (08-06) · VFXType ordinal rules · HeroHpStateAura's accessibility recipe
(the pulse/gutter design is owner-ruled) · the ONESHOT saturation investigation.
