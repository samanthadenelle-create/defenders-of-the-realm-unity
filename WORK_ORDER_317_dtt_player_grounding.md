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

## Root cause (triage 2026-06-06)
**Confidence: Likely.** The hero spawn has **no ground-snap**:
- `PatriciaLightController.SpawnHero` places the hero at `_heroLedgePos`
  (`Assets/_Modules/Village/PatriciaLight/PatriciaLightController.cs:658-659`).
- `_heroLedgePos` = the baked `"HeroSpawn"` marker position if present, else the fixed fallback
  `TowerPos + _balconyPos` where `_balconyPos.y = BalconyHeight = 8 m` (`:517-518`, `:77-81`).
- There is **no down-raycast** to seat the hero's feet on the platform collider. If the baked HeroSpawn marker
  Y (or the balcony top) doesn't exactly match the actual platform surface, the hero floats — exactly the
  reported symptom.
- The file already has a down-raycast floor finder used for enemies (`:1327-1333`).

**Suggested minimal fix:** after computing `perchPos`, raycast straight down onto the platform/arena collider
and snap the hero root so feet rest on the surface (reuse the existing down-raycast at `:1327-1333`).
**On the secondary NRE note:** `PatriciaLightController.Update` is guarded (`:267` `if(!_running) return;`); the
plausible DTT throw is in the spawn path (`VisualFactory.Skin` on a missing class resource, `:694`) — see WO-328.

## Do NOT touch
- No `.unity` edits (DTT scene changes via its builder/controller). Don't fork HeroBodySwapper.
