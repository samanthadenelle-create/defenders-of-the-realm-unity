<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_295 — Legendary set "Aegis of Elarion" + Oathweld ward effect

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
**Branch:** feat/tower-core-loop · **Lane:** 6/3 · **Depends on:** 293 (tiers/recipes), 290 (quest gate)
**Design source:** `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` §3

## Context
The legendary reforge yields the **Aegis of Elarion** — shared armor + one class weapon each (Knight
Emberbrand, Archer Heartwood Longbow, Mage Aetherstaff, Cleric Hallowed Censer). Crafted via WO-293's
legendary recipe.

## Goal
Define the Legendary items as data + implement the **set effect** ("Oathweld" ward) and per-weapon perks.

## Files to edit / create
- `Assets/Data/Canonical/recipes_legendary.json` + weapons/armor entries (Tier=Legendary) with saga flavor text.
- `Assets/_Modules/Village/Hero/GearLoadout.cs` (+ a small `AegisSetEffect` component) — detect full set,
  apply ward: a portion of damage taken is refunded as a short ward that protects the Heart/structures.
- Per-class weapon perks (Emberbrand combo shock; Longbow pierce+mark; Aetherstaff cost-down up close;
  Censer heal-also-wards) — hook into existing combat (PlayerAttackController/HeroAbilities).
- Item flavor text naming the four crafters (lore callback, see LORE doc).

## Acceptance criteria
- [ ] Crafting the legendary recipe grants the correct per-class weapon + Aegis armor with Legendary tier visuals/text.
- [ ] Full Aegis set equipped → ward effect active and visibly protects the Heart/structures on damage.
- [ ] Each class weapon perk fires in combat as specified.
- [ ] Flavor text references the crafters; no placeholder strings.
- [ ] Brace check passes; CompileGate OK; Windows build SUCCESS.

## Do NOT touch
- No `.unity` edits. Real gear meshes optional (can reuse current visual path + GearVisualApplier when
  EnablePrimitiveGear is re-enabled). Don't fork combat — extend existing controllers.
