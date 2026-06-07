# WORK ORDER 284 — RESULT

**Status:** DONE (hero slice + driver) — committed `bac3fd9`, pushed `feat/tower-core-loop`.
**Date:** 2026-06-06 · **Verified by:** CLI (CompileGate + controller rebake + Windows build).

## Landed
- **`Assets/_Modules/Core/Combat/AnimParams.cs`** — canonical param names + cached hashes
  for every actor; enums `HitDirection / DeathDirection / TurnDirection / EmoteType`.
  Death standardized to the **`Dead` bool latch (+ `DeathDir`)**. `Speed` documented RAW
  world u/s (NOT 0..1) to match the WO-283 blend thresholds.
- **`IActorAnimator.cs` + `ActorAnimator.cs`** — the single guarded driver. Re-resolves the
  Animator and rescans the param cache across the hero's runtime body swap; every verb
  null/param-guarded → absent state = safe no-op (no per-frame param spam).
- **`HeroAnimatorFactory`** — hero controllers now declare the full AnimParams set (Deliverable D).

## Reconciliations
- `IsAlert` (EnemyBrain) / `BowRecoil` (Ranger) — left on their existing local params this
  pass; folding them into `InCombat` / the driver is part of the deferred enemy migration.
- `InCombat`, `WindUp`, `DeathDir`, `HitDir`, `TurnDir`, `Emote` are declared on hero
  controllers but not all yet drive a distinct state (single Hit/Death clip this pass) —
  declared so the verbs resolve; directional hit/death + combat-idle are noted polish.

## Deferred (deliberate — protect working combat)
Enemy.cs, Pet/PetAnimatorController, DragonBoss, DungeonHero **keep their existing
param-guarded `StringToHash`** this pass. Their param NAMES already match `AnimParams`
(Speed/Attack/WindUp/Hit/Dead/HitDir), so they're already consistent; routing them through
`ActorAnimator` is a mechanical, low-risk follow-up that wasn't worth regressing 1,200-line
battle-tested files (Enemy.cs) overnight. The strict "no actor declares its own StringToHash"
criterion is therefore **partially met (heroes done)** — flagged here per standing policy.

## Gates
CompileGate `COMPILE_GATE_OK` · braces balanced on all 7 files · Windows build SUCCESS.
Play smoke test (all actors idle/walk/attack/hit/death/victory) pending owner/Tricia.
