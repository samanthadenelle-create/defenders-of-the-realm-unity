# WORK ORDER 285 — RESULT

**Status:** DONE (hero combat) — committed `bac3fd9`, pushed `feat/tower-core-loop`.
**Date:** 2026-06-06 · **Verified by:** CLI (CompileGate + controller rebake + Windows build).
**Note:** WO-284 was NOT actually on this branch when assigned (the driver didn't exist);
built it first this session, then this WO on top. See `WORK_ORDER_284_*.RESULT.md`.

## Delivered (real 3D combat now plays the library)

- **A. Hero melee → real attack clips.** `PlayerAttackController` routes the swing through
  `ActorAnimator.PlayAttack(combo)`. **Knight cycles a 3-swing combo** (Attack0→1→2, resets
  after a 1.1 s idle gap) from the sword-and-shield set; **Mage/Cleric cast** instead of
  swinging (`PlayCast`); **Ranger** plays its aim/attack. The hero controllers gained the
  Attack state(s) they previously lacked (WO-283 built only Locomotion/Cast/Victory), so the
  existing `Attack` trigger now actually animates.
- **B. Hero hit + death.** `HeroHitReaction` plays `Shared_Hit_Reaction` on damage (was
  screen-flash only). `HeroHealth` death/revive latch the **`Dead` bool** (`Shared_Death`,
  holds — no idle flicker) and `Revive()` clears it on respawn.
- **C. Enemy clip set.** Enemy.cs already drives windup→attack→hit(+HitDir)→Dead with the
  matching param names; it resolves against the controller once the enemy controller carries
  those states. (Enemy controller state-build / injured-Humanoid set = WO-283 deferred item.)
- **D. Block.** Knight holds Block (RMB / Shift / LT) → `Shared_Block` via `SetBlocking`.
- **E. Responsiveness.** Damage still lands on the swing's impact frame (the existing
  perfect-hit window — unchanged). Upper-body layer now also fires on `Attack` so a swing
  overlays while moving. Triggers fire on input (no delayed coroutine); transitions are short.

## Controllers rebuilt (clean, no missing clips)
Knight `Attack(3) + Hit + Death + Block`; Mage/Ranger/Cleric `Attack(1) + Hit + Death + Block`
— all on top of Locomotion(3) + Cast(+UpperBody) + Victory.

## Deferred / polish (flagged)
- Directional hit/death blends (HitDir/DeathDir declared but single-clip this pass).
- Enemy/Pet/Dragon migration to `ActorAnimator` (WO-284 RESULT) and enemy injured-Humanoid
  controller wiring (WO-283 RESULT).
- Knight full combo trees (the other ~95 Knight clips remain unwired).

## Gates
CompileGate `COMPILE_GATE_OK` · braces balanced · Windows build SUCCESS.
**Play smoke test pending owner/Tricia** — verify in village-defend + open-world: each class
swings/casts its real clip, hero flinches on damage + plays a death and respawns clean,
Knight combo cycles, block holds; no T-pose, no stuck/sliding poses.
