# WO-929 — VFX aura reparented during activate/deactivate (real Unity error, 3x in one session)

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated).
The §2c cited seam is fixed at `VFXManager.ReturnToPool`: a return arriving inside a host
(de)activation window silences particles immediately and DEFERS the reparent+enqueue to the next
Update sweep (`_pendingReturns`, duplicate-guarded; a host destroyed mid-defer drops its entry and
the existing prune paths own the counters). One seam, every caller/host class covered by
construction. REMAINING PROOF: the ABSENCE of the "Cannot set the parent" line across a full
session/fleet run (it fired 5+ times in the owner's 2026-08-10 session) + a pool-shape regression
case (rides the WO-955 follow-up — same file, same harness).
**Date:** 2026-08-08 · **Priority:** Medium-High (it is a thrown error, not a cosmetic) · **Lane:** VFX
**Reported by:** the owner's felt-test 2026-08-08 — captured, not reported by hand (F8 seq 2183-2185)

---

## 1. The captured error

Three separate captures inside one session, two distinct auras, both on pooled outpost enemies:

```
Cannot set the parent of the GameObject '[VFX_Aura_EnemyCaster]' while activating or
deactivating the parent GameObject 'OutpostEnemy (hollow-acolyte)'.

Cannot set the parent of the GameObject '[VFX_Aura_Necromancer]' while activating or
deactivating the parent GameObject 'OutpostEnemy (necromancer)'.
```

Files: `logs/f8-inbox/capture-20260808-083114.md`, `-083115.md`, `-083116.md`.

This is a hard Unity restriction: `transform.SetParent` is illegal during the host's `OnEnable`/
`OnDisable`. Unity refuses the reparent — so the aura is **not** attached where the code believes it
is. The failure is silent to the player and loud only in the log.

---

## 2. Where it comes from

The aura wiring landed in **`4c1da079`** (the WO-889/890/891/892/893 mega-commit), which created
`VfxAuraProximityCuller.cs`, `VfxLoopBudget.cs`, `HarvestAura.cs` and `SupportFieldStructure.cs`.
Auras are attached to enemies that are **pooled** — `OutpostEnemy (…)` names come from the pooled
spawner — so the host is enabled/disabled on every spawn and despawn, which is exactly the window
`SetParent` is illegal in.

⚠ **The pooling detail is the whole bug.** A non-pooled enemy is instantiated once and never toggled,
so this never fires. The auras were almost certainly authored and verified against that case.

---

## 2b. ⚠ SCOPE WIDENED — FOUR distinct host classes now proven (added 2026-08-10)

The original proving lines were all `OutpostEnemy (…)` — pooled enemies. Newer captures prove the
same illegal `SetParent` fires from hosts that are NOT pooled enemies, so **a fix scoped to the
pooled-enemy path is incomplete — the defect is in the SHARED aura-attach mechanism itself**:

| Capture | Host class | Aura |
|---|---|---|
| F8 seq 2248, 2026-08-08 13:17 | **`Lumbermill` — a BUILDING** | `[VFX_Harvest_Wood]` |
| `capture-20260810-102444.md` | (host in capture; fired during the death/near-death window) | **`[VFX_Aura_NearDeath]` — a HERO-side aura** |
| `capture-20260810-102534.md` | enemy caster path, re-proven | `[VFX_Aura_EnemyCaster]` |
| F8 seq 2183-2185, 2026-08-08 (original) | `OutpostEnemy (…)` pooled | `[VFX_Aura_EnemyCaster]` / `[VFX_Aura_Necromancer]` |

Acceptance must therefore assert the fix at the attach seam (defer the reparent out of the
activation window, or parent-then-activate), proven against ALL four host classes — not by patching
each caller.

## 2c. ★ THE SEAM, CITED — full stack captured 2026-08-10 (F8 seq 2291, dg_ember_deep)

The first capture of the series with a complete stack, and it pins the shared mechanism — the
offending SetParent is the POOL RETURN running inside the host's OnDisable:

```
UnityEngine.Transform:SetParent
DeNelle.Village.VFXManager:ReturnToPool  (VFXManager.cs:900 — go.transform.SetParent(_poolRoot, false))
DeNelle.Village.VFXHandle:Stop           (VFXHandle.cs:120)
DeNelle.Village.EnemyAuraVFX:StopHeld    (EnemyAuraVFX.cs:244)
DeNelle.Village.EnemyAuraVFX:OnDisable   (EnemyAuraVFX.cs:160)
```

Host `OutpostEnemy (orc-necromancer)` deactivating (pooled despawn) → the aura component's
OnDisable stops its held loop → `ReturnToPool` reparents to `_poolRoot` — illegal while the host
hierarchy is mid-deactivation. **This is the one seam to fix**: `ReturnToPool` must be safe to call
from a (de)activation window — e.g. detect the illegal window (the aura still parented under the
deactivating host) and DEFER the reparent+enqueue one frame (a pending-returns list swept by the
manager's Update), or never parent auras under pooled hosts at all (follow-proxy). Whichever is
chosen: one owner, all callers covered, and the WO-955 dead-slot guard (landed 2026-08-10) already
protects Acquire from any half-returned corpse.

---

## 3. Why it matters more than an error line

1. **The aura may be orphaned.** If the reparent is refused, the effect either stays at the scene root
   or stays on a previous host. An orphan at the root does not follow its enemy and never gets torn
   down with it — which is the same class the `Destructible` component was built to end
   (`WO-753`: "destroy the VFX with the item, no orphaned auras").
2. **It interacts with a known open P0 signature.** `CANON_GROUND_TRUTH_2026-08-06` records that the
   ONESHOT pool saturates 40/40 in three captures and that this is **deliberately NOT closed** by the
   loop-cap fix. A leaked aura per pooled spawn is a plausible contributor to pool pressure. **Do not
   assume they are the same bug — but measure the pool before and after this fix.**

---

## 4. Fix direction (not prescriptive)

The standard resolutions, in order of preference:

- **Defer the parenting by one frame** or to the host's `Start`, so it happens outside the
  enable/disable window.
- **Parent at pool-construction time**, not at spawn. A pooled object's hierarchy should be built once
  and reused — re-parenting on every spawn is the smell here.
- **Attach without reparenting** (position/rotation follow), if the aura does not need to inherit
  scale.

⚠ **Guard against the silent-orphan case regardless of which is chosen**: if a reparent is refused,
that must be a `FlowTrace.Fail`, not a swallowed Unity warning.

---

## 5. Acceptance

- [ ] A full wave with pooled outpost enemies produces **zero** "Cannot set the parent" lines.
- [ ] Auras follow their host and are torn down with it (no scene-root orphans after a despawn).
- [ ] `[Flow:VFX]` pool counts recorded before and after, so the ONESHOT-saturation question is
      answered with data rather than assumed either way.
- [ ] A regression pins the invariant — an aura's parent after spawn IS its host.
- [ ] Owner felt-verifies and closes.
