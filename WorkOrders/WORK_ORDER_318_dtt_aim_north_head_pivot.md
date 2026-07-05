# ⚠ WORK_ORDER_318 — Defend the Tower: aim stays north + head-only pivot (clamp) — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09.

**Status: CLOSED — SUPERSEDED (system removed 2026-06-09)**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06
**Reconcile with:** `PatriciaLightController` aim/targeting, camera rig, `HeroTargetIndicator`/aim override

## Problem
While targeting in Defend the Tower, the hero/camera focus drifts off — it should hold a fixed **north**
facing, and the hero should only **pivot the head left/right** to track targets, not rotate the whole body.

## Goal
DTT is a fixed-position turret-style stance: camera/aim locked facing **north**; the hero tracks targets by
**clamped head yaw (left/right only)**, body stays put.

## Scope
- Lock the DTT camera/aim to a north facing (no free orbit during the defend stance).
- Hero aiming: drive a **head-only** look (clamped yaw range, e.g. ±60–80°) toward the current target; do NOT
  rotate the body/root. Reuse the current-target from the aim system (HeroTargetIndicator / aim override).
- Target acquisition still works within the forward arc; out-of-arc targets aren't auto-faced (body fixed).

## Acceptance criteria
- [ ] Camera/aim holds a north facing during the defend stance (no unwanted drift/orbit).
- [ ] Hero tracks targets with head yaw only, clamped L/R; body/root does not spin.
- [ ] Targeting/firing still resolves to the looked-at target within the arc.
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Root cause (triage 2026-06-06)
**Confidence: Likely.** DTT currently turns the **whole body** to targets and has no head-only clamp or
locked-north camera:
- `SpawnHero` sets a base facing toward the tower and explicitly notes "Combat slerp in
  TickHeroStrafe/TickHeroFire can still turn toward specific targets temporarily"
  (`Assets/_Modules/Village/PatriciaLight/PatriciaLightController.cs:661-683`) — i.e. the root rotates to
  targets, not a clamped head bone.
- Aim is driven via `HeroAimIK` (RightHand IK, `Assets/_Modules/Village/Hero/HeroAimIK.cs`) — there is no
  head-yaw clamp, and nothing pins the camera/aim to north during the stance.

**Suggested minimal fix:** for the DTT stance, (1) lock the camera/aim to a fixed north facing (constrain the
OTS/turret rig, don't fork it), (2) stop the body slerp toward targets in `TickHeroStrafe/TickHeroFire`, and
(3) drive a clamped head-yaw look (±60–80°) toward the current target instead. Same controller as WO-317/319.

## Do NOT touch
- No `.unity` edits. Don't fork the camera rig — constrain it for the DTT stance. Coordinate with WO-317 (same controller).
