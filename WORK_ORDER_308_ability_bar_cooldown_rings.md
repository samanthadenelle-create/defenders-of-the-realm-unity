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

## Do NOT touch
- No `.unity` edits. Don't fork HeroAbilities — read it through a bridge.
