# WORK_ORDER_293 — Crafting tiers (Common/Fine/Master/Legendary) + legendary recipe system

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 6 (Economy/Progression) · **Depends on:** 290 (QuestService) for legendary gating
**Design source:** `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` §4, `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md`

## Context
Gear today (WO-106) has weapons/armor JSON + `GearCatalog`/`GearLoadout` + `CraftingRecipeCatalog` +
`VillageInventory`, all spending through `EconomyService`. We need explicit quality tiers and a legendary
recipe that requires the four crafters' components (the "interdependent crafts" mechanic).

## Goal
A `Tier` enum (Common→Fine→Master→Legendary) on gear + recipes whose ingredients can require **other
crafters' outputs**, with Legendary gated behind the Forgemasters' Saga (Act IV).

## Files to edit / create
- `Assets/_Modules/Village/Hero/GearCatalog.cs` — add `Tier` to WeaponDef/ArmorDef + tier-based stat scaling.
- `CraftingRecipeCatalog` — recipes can list ingredient item ids (incl. components: reforged steel,
  Oathweld plate, Heartwood, Last-Pressing quench) + a catalyst; gate flag `requiresQuest`/`minTier`.
- `Assets/Data/Canonical/` — add tier fields to weapons.json/armor.json + a `recipes_legendary.json`.
- Shop/craft UI (`ShopPanel`/crafting panel) — show tier, ingredient requirements, locked state.

## Scope
- Common/Fine from raw resources; Master requires a second crafter's output; Legendary requires all four
  components + catalyst + `QuestService` says saga Act IV complete.
- All costs/outputs via `EconomyService` + `VillageInventory` (no new economy).

## Acceptance criteria
- [ ] Gear displays its tier; tier raises stats per a data-driven curve.
- [ ] A Master recipe correctly requires another crafter's output as an ingredient (blocked without it).
- [ ] Legendary recipe is locked until QuestService flag set; once unlocked, consumes the 4 components + catalyst.
- [ ] All transactions go through EconomyService/VillageInventory; no duplicate ledgers.
- [ ] Brace check passes; CompileGate OK; Windows build SUCCESS.

## Do NOT touch
- Monetization/shop payment (WO-72–80). No `.unity` edits. Reconcile with GearLoadout, don't fork it.
