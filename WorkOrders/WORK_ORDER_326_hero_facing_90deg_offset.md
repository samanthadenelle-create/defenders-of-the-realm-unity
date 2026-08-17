<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

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

## Root cause (triage 2026-06-06)
**Confidence: Confirmed (root) / Likely (exact value).** The forward-axis correction value is wrong:
- `HeroLocomotion` does pure root `LookRotation(Velocity.normalized)` and defers the rig forward-axis
  correction to `HeroBodySwapper` (`Assets/_Modules/Village/Hero/HeroLocomotion.cs:350-358`).
- `HeroBodySwapper` applies a child `LocalRotation` of `forwardYaw = 90f`
  (`Assets/_Modules/Village/Hero/HeroBodySwapper.cs:92`, applied at `:99`). The meshes export forward on +X;
  +90f over-rotates, so walking north the body reads ~90° to the right. The file's own comment says
  "IF STILL OFF: try the opposite sign (-90f) or 180f" (`:91`).
- **Strong corroboration:** the companion path applies **-90f** for the SAME hero FBXs
  (`Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs:160`) — the hero's +90f is inconsistent with it,
  which strongly implies the hero value should be **-90f**.

**Suggested minimal fix:** change `HeroBodySwapper.forwardYaw` (`:92`) to `-90f` (verify in play). Make this the
**single shared facing convention** reused by WO-255 (hero backwards) and WO-315 (enemies have NO correction
AND no path-facing — they need the equivalent on the enemy visual child + a face-velocity in `Enemy.DriveNav`).
Fix once; don't add per-class hacks.

## Do NOT touch
- No `.unity` edits. Don't fork HeroLocomotion — fix the facing correction. Coordinate/merge with WO-255 + WO-315.
