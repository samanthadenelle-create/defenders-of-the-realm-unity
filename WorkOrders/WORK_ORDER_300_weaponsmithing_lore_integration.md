# WORK_ORDER_300 — Elarion weaponsmithing lore integration (flavor, marks, appraisal)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** 293 (tiers) for item text; 291 (vendor Yarn) for barks
**Design source:** `LORE_ELARION_WEAPONSMITHING.md` §5

## Context
Elarion's "singing steel" renown should be felt, not just told: item flavor, maker's marks, trader
recognition, and the prestige of crafting Master/Legendary now.

## Goal
Thread the lore through gear text, vendor barks, an authentication mini-loop, and crafting reactions.

## Files to edit / create
- Gear data (`Assets/Data/Canonical/weapons.json`/`armor.json`) — flavor text per tier + a `makerMark` field;
  Master/Legendary text references the Bright Centuries + crafters.
- Vendor barks: Coppin/visiting-trader Yarn lines that recognize Elarion marks; old-timer reminiscing.
- Appraisal mini-loop: real vs counterfeit marks (Sable/Coppin authenticate) — data flag + simple UI hook.
- Crafting reaction: first Master and first Legendary craft → a village bark/small ceremony (event + SFX/VFX).

## Acceptance criteria
- [ ] Gear shows tier-appropriate flavor + maker's mark; Legendary names the crafters.
- [ ] At least one trader/NPC recognizes Elarion marks in dialogue.
- [ ] Authentication distinguishes real vs counterfeit (price/prestige differs).
- [ ] First Master/Legendary craft triggers the village reaction once (persisted so it doesn't repeat).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Reuse existing AudioService/VFXManager for the ceremony; don't fork.
