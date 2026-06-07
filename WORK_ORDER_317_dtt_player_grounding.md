# WORK_ORDER_317 — Defend the Tower: player not standing on anything (grounding)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 2 (Combat/AI) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `PatriciaLightController`, hero spawn/grounding, `HeroBodySwapper` foot-grounding

## Problem
In Defend the Tower the hero floats — not standing on the tower platform/ground. Spawn Y / grounding is wrong
(hero parked mid-air against the tower face).

## Goal
The hero is correctly grounded on the tower platform (feet on the surface), stable across the wave.

## Where to look
- `PatriciaLightController` hero spawn position/platform raycast; the foot-grounding path in `HeroBodySwapper`
  (the WO-286 Read/Write fix enabled vertex reads — confirm grounding actually runs here).
- Ensure a ground/platform collider exists under the spawn and the hero snaps to it (raycast-down to place).

## Note (secondary)
- The dev console shows **NullReferenceException spam** in this mode ("reference not set to an instance of an
  object") — investigate + null-guard the DTT update/spawn path; an exception mid-spawn can skip grounding.

## Acceptance criteria
- [ ] Hero spawns standing on the tower platform (feet on surface), not floating.
- [ ] Stays grounded through the wave (no drift/sink/float).
- [ ] No NullReferenceException spam in the DTT console on entry/play.
- [ ] Brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in a play session.

## Do NOT touch
- No `.unity` edits (DTT scene changes via its builder/controller). Don't fork HeroBodySwapper.
