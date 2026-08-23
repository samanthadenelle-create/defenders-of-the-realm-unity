**Status:** READY TO IMPLEMENT — but take the DUMP first (see §4). The evidence now exists; do not fix this from the static read alone.

# WORK ORDER 1155 — A projectile torn down in flight strands its VFX loop slot forever

**Minted:** 2026-08-23 (CLI, banner bumped 1155 -> 1156 in this SAME edit)
**Lane:** VFX / lifecycle. **Class:** A LEAK WITH NO RELEASE PATH.
**Found by:** the WO-1057 lane, 2026-08-23, while verifying the loop registry.
**Parent:** WO-1057 (the registry that makes this visible). §7 of that ticket anticipated this split.

## THE FINDING

`ProjectileMover.Arrive()` is the **only** caller of `_onArrive`, and the class has **no timeout, no
`OnDisable`, and no `OnDestroy`.** So a projectile that is destroyed mid-flight — the enemy dies, the
scene changes, the pool recycles it — never releases the loop slot it took.

Two known offenders: **`PP_FireBall`** (every ranged enemy) and **`icebasedprojectile_Projectile`**.

The slot is not merely leaked, it is **unreclaimable**: the owner GameObject is gone, so nothing can
call `Stop()` on it. It occupies a slot against the loop cap until the session ends, and once the cap
is reached `VFXManager` starts emitting `SKIPPED — active loops N/M` and **real effects stop
appearing**. A player experiencing this sees combat VFX quietly stop working the longer they play.

## ⛔ 4. WHY THIS IS NOT "READY TO IMPLEMENT" IN THE USUAL SENSE — TAKE THE DUMP FIRST

WO-1057 shipped the live loop registry and its F8 dump. **That instrument exists precisely so this
class of fix is built on captured data rather than a code read** (CLAUDE.md §12).

Before editing, get a real dump: play until ranged enemies have fired and died, then F8. The audit
line will name the stranded handles by owner and age:

```
[Flow:Vfx]   PP_FireBall  owner='ArcherSkeleton(Clone)'#-31844  age=1892s  <-- OWNER DESTROYED, nothing can Stop() it
```

**That line is the proof.** It tells you which prefabs actually strand, how many slots they hold, and
how fast they accumulate — all of which the static read can only guess at. It may also reveal
offenders beyond the two named above, and a fix scoped to two prefabs would silently miss them.

## SCOPE

1. Take the dump (§4). Record it in the RESULT.
2. Give the projectile a release path that does not depend on arrival: `OnDisable`/`OnDestroy`, and a
   lifetime timeout for the case where it is neither destroyed nor arrives.
3. Make release **idempotent** — arrive-then-destroy must not double-release, or the count goes wrong
   in the other direction.
4. Re-take the dump and show the strand no longer accumulates.

## ⛔ CONSTRAINTS

- **NEVER add a second spawner, pool or lifecycle owner.** Special-case the PRESENTATION, never the
  ownership (memory `sequenced-vfx-special-cases-for-special-events`). The registry OBSERVES — do not
  make it release things on the projectile's behalf; fix the projectile.
- ⛔ Do NOT re-introduce a `Mathf.Max(0, ...)` clamp on the loop count. It was deliberately deleted in
  the WO-1057 rewrite: a clamp HIDES a negative count, and a negative count is the symptom of exactly
  this kind of double-release. The count must be free to go wrong so the oracle can see it.
- Instrumentation is PERMANENT (§12) — never strip the audit or the dump.
- Hot paths use `FlowTrace.Throttle`/`Once`, never per-frame logging (memory
  `logcat-ring-buffer-destroys-evidence`).

## ACCEPTANCE

- [ ] A dump taken BEFORE the fix names the stranded handles (attach it)
- [ ] Killing a ranged enemy mid-flight releases the slot; the dump shows no orphan
- [ ] Arrive-then-destroy releases exactly once (no negative count, and no clamp hiding one)
- [ ] A long session no longer walks the loop count upward; no `SKIPPED — active loops` from strand
- [ ] `[Flow:Vfx]` instrumentation intact
