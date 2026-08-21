**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

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

## Root cause (triage 2026-06-06)
**Confidence: Confirmed.** Enemies have **no path-direction facing at all**:
- `Enemy.Configure` sets `_agent.updateRotation = false` with the comment "we control facing (to target on
  attack, or path dir)" (`Assets/_Modules/Village/Enemies/Enemy.cs:390`).
- But `DriveNav()` (`:572-643`) only ever calls `SetDestination` — it **never sets `transform.rotation`
  toward the agent's velocity/path**. The only rotation in the file is `RangedAttack` →
  `Quaternion.LookRotation(toTarget)` (`:854`), i.e. facing is set only when attacking.
- Result: the enemy keeps a stale orientation while the agent slides it along the path, and the visual rig's
  authored forward axis (+X, same as the heroes) is never corrected — reads as backwards/sideways/moonwalk.

**Suggested minimal fix:** in `DriveNav`, when moving, slerp `transform.rotation` toward
`LookRotation(_agent.velocity)` (or re-enable `updateRotation`), AND apply the same visual-child forward-axis
correction the heroes use. **Shared root with WO-255 + WO-326** — fix the facing convention once (see WO-326:
heroes apply it in `HeroBodySwapper.forwardYaw`; enemies need the equivalent on the enemy visual child).

## Do NOT touch
- No `.unity` edits. Reuse the hero facing-correction convention (WO-255); don't fork Enemy locomotion.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
