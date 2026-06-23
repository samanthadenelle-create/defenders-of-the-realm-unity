# WORK_ORDER_470 — HUD battle-context state (combat vs. peace)

**Status: READY TO IMPLEMENT** · Drafted by read-only agent (2026-06-21), reconciled to code.

## Problem
The village/world HUD shows the same chrome whether the player is in a wave/raid or wandering in peace.
The HUD should switch into a **battle context** when combat is live (wave active, enemies engaged) — surfacing
combat-relevant controls (troop deploy, retreat, wave timer) and quieting peace-only affordances (Talk/Build),
then revert when the threat clears.

## Files (reconcile, don't greenfield)
- `VillageHudController.cs` (`DeNelle.HUD`) — add a `HudContext { Peace, Battle }` field + `SetContext()`;
  show/hide button groups by context. Passive display ONLY (CLAUDE.md §5: HUD → Core only, never references Village).
- `IVillageHud.cs` (`DeNelle.Core.HUD`) — add `void SetBattleContext(bool active)` to the interface so Village
  drives it via `CoreServices.Hud` (the one legal cross-module seam).
- `WaveManager.cs` (`DeNelle.Village`) — call `CoreServices.Hud?.SetBattleContext(true)` on wave-start /
  first-enemy-engaged and `(false)` on wave-clear / all-enemies-dead. Null-conditional (§10).

## Acceptance
- Wave starts → HUD enters Battle context (combat controls visible, Talk/Build hidden) within 1 frame.
- Wave clears → HUD reverts to Peace context.
- No Village→HUD direct ref introduced; goes through `IVillageHud` / `CoreServices.Hud` only.

## NOT to touch
The MVVM seam direction (Village pushes, HUD never pulls); existing HUD layout for peace; ATB battle scene HUD (separate).

## INSTRUMENT-FIRST (§12 hard gate)
`FlowTrace.Step("Hud", "context→battle/peace", …)` at every SetBattleContext call + at WaveManager's
start/clear edges. Headless fleet: run a wave, capture `[Flow:Hud]` + `[Flow:Wave]` — PROVE the context flips
on wave-start and reverts on clear (no stuck-battle after victory). Confirm by data, not by eyeballing.
