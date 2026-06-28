# WORK ORDER 572 — Resource HUD gain-flash throttle (stop the per-tick drip strobe)

**Status:** IMPLEMENTED (edit-only; not gated/committed by this agent)
**Date:** 2026-06-28
**Silo:** HUD (presentation only — `DeNelle.HUD`)
**Source:** Owner F8 felt-test 2026-06-28 + captured `[Flow:Eco]` data.

---

## Symptom (owner F8)
"Every tick the lumber goes up it flashes the resources growing in green" — the
top-centre resource HUD strobes green continuously.

## Captured data (the proof — not re-theorized)
Echo workforce / harvest faucet banks resources +1 very frequently:
```
[Flow:Eco] MineNode banked +1 Wood
[Flow:Eco] OnChanged fired W49404...
[Flow:Eco] HUD.SetResources W49404...
   (repeats every tick W49404 -> 49405 -> ...)
```
Each `SetResources` whose value rose drove a green badge flash → passive +1 drip =
a green flash every tick = strobe.

## RCA — flash trigger
`Assets/_Modules/HUD/VillageHudController.cs`
- **`SetTownResource(int idx, int value)`** (was ~line 2570) — on ANY change set
  `_townResFlash[idx]=1f; _townResFlashUp[idx]=value>prev;`. Fired on every +1 drip.
- **`UpdateTownHud` per-frame loop** (~line 934-948) renders `_townResFlash[idx]` as a
  green (`HudTheme.LookoutSafe`, up) / red (`HudTheme.HpRed`, down) glow that fades.

So every wallet increment re-armed the green flash → the strobe.

## Fix — magnitude split + coalesce (presentation-only, in VillageHudController.cs)
Increases are now classified:
- **DISCRETE gain** — a single update delta `>= ResGainBigDelta (8)` (wave reward, sell,
  big/scaled node extract): flash green **instantly, every time** (keeps the nice feedback).
- **DRIP gain** — small per-tick increment (`< 8`): do **not** flash immediately; accumulate
  into `_townResGainAccum[idx]` and (re)arm a `ResGainCoalesceWindow (0.6s)` quiet-gap timer.
  `UpdateTownHud` fires **ONE** green flash only when the trickle **pauses** (window elapses
  with no new gain). A continuous faucet re-arms the window every tick → it never expires
  while flowing → **no per-tick strobe**; one gentle pulse once income settles.
- **SPEND (decrease)** — unchanged: flashes red immediately (and cancels any pending drip).

### Values chosen
- `ResGainBigDelta = 8` — observed drips are +1 (and a few coalesced in one frame stay well
  under 8); real rewards/sells/high-tier extracts are lumps ≥ 8 and flash instantly.
- `ResGainCoalesceWindow = 0.6s` — quiet gap that must elapse before a coalesced drip flashes.

### FlowTrace (throttle is provable)
- `[Flow:Eco] ResFlash discrete gain idx{n} +{delta} -> instant flash (>= 8)`
- `[Flow:Eco] ResFlash coalesced drip gain idx{n} +{accum} -> single flash (per-tick strobe suppressed)`

## Behavior confirmation
- Fast +1 continuous drip (the bug): window keeps re-arming → **no flash while flowing**;
  one calm flash when it pauses. Strobe eliminated.
- Discrete gain (reward / sell, ≥ 8): **still flashes green instantly, every time.**
- Spend: **still flashes red immediately**, exactly as before (resource display untouched).
- Resource numbers themselves update every frame as before (only the flash trigger changed).

## Files modified
- `Assets/_Modules/HUD/VillageHudController.cs`
  - new fields after `_townResFlashUp` (gain-accum + window arrays + 2 consts)
  - `SetTownResource` flash branch split (spend / discrete / drip)
  - `UpdateTownHud` coalesce-flush loop added before the existing flash-fade block

## NOT touched
- `_resourceTexts` battle strip, number formatting, low-warn red outline, WO-563 HUD work,
  HeartHudBridge, EconomyService, MineNode/HarvestSite banking paths.

## Gate
- Brace balance: `Assets/_Modules/HUD/VillageHudController.cs` 258/258 OK, no NUL.
- CompileGate / commit: deferred to CLI (this agent is edit-only).
