# WORK_ORDER_308 — Ability/skill action bar with cooldown rings + symbols (active hero)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 4 (UI/HUD) · **Origin:** owner playtest 2026-06-06
**Depends on:** WO-307 (HUD shell) · **Reads:** HeroAbilities

## Problem
The ability slots are empty black boxes. The player can't see the active hero's spells/skills, what they do,
or their cooldown state.

## Goal
A clear ability bar showing the **active hero's** abilities as icons, each with a **cooldown ring** and a
symbol reflecting what it does. Owner wants these readable on the right side of the screen (relocate from the
empty bottom bar) — confirm placement with the HUD shell (WO-307).

## Scope
- Pull the active hero's ability set from `HeroAbilities` (DeNelle.Village) via a HUD bridge (HUD → Core only).
- Each slot: ability icon/symbol + radial **cooldown ring** (fill 0→1 as it recharges) + key hint + disabled tint when on cd.
- Icons/symbols per ability (damage/heal/AoE/buff) — data-driven; placeholder symbol set if art not ready.
- Updates live; mobile-tappable (slot doubles as the cast button on touch).

## Files
- New `Assets/_Modules/HUD/AbilityBarPanel.cs` (DeNelle.HUD, code-built) + bridge in Village feeding it.

## Acceptance criteria
- [ ] Active hero's abilities show as icons with symbols indicating effect.
- [ ] Each shows a live cooldown ring + disabled state while recharging; ready state is obvious.
- [ ] Tapping a slot (mobile) / key (web) casts the ability.
- [ ] Swapping active hero updates the bar.
- [ ] HUD→Core only; code-built; brace check; CompileGate OK; build SUCCESS.

## Root cause (triage 2026-06-06)
**Confidence: Likely.** The ability bar + radial cooldown rings ALREADY EXIST and are functional in code —
this is not a greenfield build:
- `VillageHudController.BuildSkillBar` builds 4 cells, each with a radial cooldown overlay
  (`_slotCooldown[i]` = `Image.FillMethod.Radial360`, `Assets/_Modules/HUD/VillageHudController.cs:266-273`),
  per-class glyph + accent disc.
- It is fed live by `HeroAbilitiesHudBridge.Update` → `SetAbilityCooldown` / `SetAbilitySlot`
  (`Assets/_Modules/Village/Hero/HeroAbilitiesHudBridge.cs:132-143`, `VillageHudController.cs:421-445`).

So "empty black boxes" is a WIRING/visibility problem, two likely roots:
1. **`HUDManager` overlaps it** (WO-307) — its Canvas at sortingOrder 200 sits above the real bar.
2. **`HeroAbilitiesHudBridge._hud` is an UNSET serialized field** (`HeroAbilitiesHudBridge.cs:17`). Unlike the
   other bridges (which `FindObjectsByType`/`CoreServices.Hud`), this one relies on a baked inspector ref; if
   it's null, `OnEnable` returns early (`:54`) and NOTHING pushes mana/cooldown/glyphs → bar stays at default
   Mage glyphs with no cooldown sweep.

**Suggested minimal fix:** resolve `_hud` the same way the other bridges do (CoreServices.Hud /
FindObjectsByType) instead of a serialized ref, and resolve the HUDManager overlap (WO-307). Then relocate per
WO-307. Largely a verification + wiring WO, not new code.

## Do NOT touch
- No `.unity` edits. Don't fork HeroAbilities — read it through a bridge.
