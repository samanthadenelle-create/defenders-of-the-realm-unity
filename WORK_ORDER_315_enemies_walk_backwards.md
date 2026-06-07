# WORK_ORDER_315 — Enemies walk backwards (facing / locomotion orientation)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `Enemy.cs` locomotion, `EnemyBrain`, ActorAnimator; mirrors WO-255 (hero backwards walk)

## Problem
Enemy mobs (e.g. the wizard family) move **backwards** — the walk/run animation or the model's facing is
reversed relative to travel direction. Same class of bug as the hero backwards-walk (WO-255): NavMeshAgent
`updateRotation=false` + manual rotation, or the model's forward axis vs. nav velocity, or a negative `Speed`.

## Goal
Enemies face and animate in their direction of travel — no moonwalking.

## Where to look
- `Enemy.cs` movement: how facing is set (manual `Slerp` toward velocity / target), the body local-rotation
  correction, and the `Speed` value fed to the animator (RAW world u/s per AnimParams, never negative).
- The enemy model's forward axis vs. the rig (the People-orc/wizard family rig may face -Z; add the same
  body local-rotation correction used for heroes).
- Confirm the WO-255 fix pattern and apply the equivalent to enemies (shared convention if possible).

## Acceptance criteria
- [ ] Walking/running enemies face their travel direction; the walk anim plays forward (no backwards/moonwalk).
- [ ] Holds for the wizard family + the other enemy families, in village-defend and open-world.
- [ ] Turning toward hero/target still works; no spin/jitter.
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Do NOT touch
- No `.unity` edits. Reuse the hero facing-correction convention (WO-255); don't fork Enemy locomotion.
