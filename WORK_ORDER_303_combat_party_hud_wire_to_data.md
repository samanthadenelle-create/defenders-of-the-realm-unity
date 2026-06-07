# WORK_ORDER_303 — Wire combat party HUD (HUDManager) to live data

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 4 (UI/HUD) · **Depends on:** 301 (party roster) preferred, not required

## Context
`Assets/_Modules/HUD/HUDManager.cs` is a code-built landscape combat HUD: a dynamic party panel (grows as
members join, HP red + Mana blue per row) + a target frame. It currently shows DEMO values via
`SetParty(...)`. It is passive display (DeNelle.HUD → Core only) with a public API:
`SetParty`, `AddMember`, `UpdateMember(i, hp, maxHp, mana, maxMana)`, `SetTarget(name, hp, maxHp)`.

## Goal
The party panel reflects the real party (heroes/companions) and updates live; the target frame reflects the
hero's current target.

## Files to edit / create
- `Assets/_Modules/HUD/HUDManager.cs` (consume real data; keep it passive)
- New small bridge in `DeNelle.Village` (e.g. `Assets/_Modules/Village/Hero/CombatHudBridge.cs`) that reads
  the party/combat state and pushes into `HUDManager` via events — **HUD must not reference Village**; the
  bridge lives in Village and calls the HUD's public API (mirror existing `*HudBridge.cs` pattern).

## Scope
- Source party from the roster (GameState, see WO-301) or, until that lands, from the active hero +
  companions present in scene. Map each member's HP (HeroHealth) and mana/resource.
- Source target from `HeroTargetIndicator.CurrentTarget` (DeNelle.Village) → `SetTarget` with name + HP.
- Subscribe to change events (HP/mana/target changed); avoid per-frame string allocation.

## Acceptance criteria
- [ ] Party rows match the real party; adding/removing a member updates the panel.
- [ ] HP and mana bars track real values live (damage/heal/cast visibly move the bars).
- [ ] Target frame shows the hero's current target and updates on target change/death.
- [ ] No Village references inside DeNelle.HUD; bridge lives in Village and uses null-conditional service calls.
- [ ] Brace check passes; CompileGate OK; Windows build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't duplicate VillageHudController; this is the combat/party HUD only.
