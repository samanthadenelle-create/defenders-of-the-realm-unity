# WORK_ORDER_319 — Defend the Tower: town hero model + firing animations + faster fire rate

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 3 (Combat Feel) · **Origin:** owner playtest 2026-06-06
**Reconcile with:** `PatriciaLightController`, `HeroBodySwapper`, `ActorAnimator` (WO-284/285), HeroAbilities fire path

## Problem
In Defend the Tower the hero (a) uses a **different/old model** than the in-town hero, (b) has **no firing
animation** toward the target, and (c) the **fire rate is too slow** for the spam-fire feel the mode wants.

## Goal
DTT hero matches the town hero (same model/skin via HeroBodySwapper), plays a firing/attack animation aimed
at the target on each shot, and supports a faster spam fire rate.

## Scope
- **Model parity:** spawn the DTT hero through the same `HeroBodySwapper` path as town so it's the identical
  class model/skin (no separate old prefab).
- **Firing animation:** on each shot, drive the attack/cast clip via `ActorAnimator.PlayAttack/PlayCast`
  (WO-285) oriented to the target (works with the head-pivot aim, WO-318).
- **Fire rate:** raise the DTT attack cadence (data-driven cooldown) for responsive spam fire; keep damage
  balanced via the value, not hardcoded.

## Acceptance criteria
- [ ] DTT hero is the same model/skin as the town hero for the selected class.
- [ ] Each shot plays a firing/attack animation aimed toward the target.
- [ ] Fire rate is noticeably faster (spam-capable), tuned via a configurable cooldown value.
- [ ] No T-pose/missing-clip; brace check; CompileGate `COMPILE_GATE_OK`; Windows build SUCCESS; verify in play.

## Do NOT touch
- No `.unity` edits. Reuse HeroBodySwapper + ActorAnimator (don't fork). Coordinate with WO-317/318 (same mode/controller).
