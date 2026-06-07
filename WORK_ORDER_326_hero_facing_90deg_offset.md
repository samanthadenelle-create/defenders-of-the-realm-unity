# WORK_ORDER_326 — Hero walks north but model/animation is rotated 90° to the right

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `HeroLocomotion` facing, `HeroBodySwapper` body local-rotation, ActorAnimator; **same root as WO-255 (backwards walk) + WO-315 (enemy backwards)** — fix the facing convention once.

## Problem
When the hero walks **north**, the **model + walk animation face ~90° to the right** of travel direction.
The rig's forward axis is offset 90° from Unity forward, and the locomotion facing doesn't correct it (prior
recurrence: old WO-32 "hero animation rotation 90deg").

## Goal
The hero model + walk animation face the actual direction of travel (north looks north) — no 90° (or 180°) offset.

## Where to look
- `HeroLocomotion`: how facing is applied (NavMesh `updateRotation=false` + manual slerp toward velocity) and
  whether the **body child local-rotation correction** for the rig's forward axis is applied (the AccuRIG/CC_Base
  heroes may face +X or -Z).
- Make the forward-axis correction a **shared convention** with WO-255 (backwards) and WO-315 (enemy), so heroes,
  enemies, pets, companion all use one correct facing offset rather than per-class hacks.

## Acceptance criteria
- [ ] Walking in any direction (esp. north), the hero model + walk anim face the travel direction — no 90°/180° offset.
- [ ] Idle→walk→run transitions keep correct facing; turning is smooth (no snap/spin).
- [ ] Fix is a shared facing-correction convention (reused by WO-255/315), not a one-off per class.
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Do NOT touch
- No `.unity` edits. Don't fork HeroLocomotion — fix the facing correction. Coordinate/merge with WO-255 + WO-315.
