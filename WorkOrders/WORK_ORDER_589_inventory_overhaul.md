<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-29
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-29) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 589 — Inventory Overhaul: Doll-Default + Tab-Fold + Stackable Quantities

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Owner directive:** 2026-06-29 felt-test session. The paperdoll/Gear Preview was already wired
(only undiscoverable behind a tiny link — fixed with the big VIEW GEAR ribbon). Owner then asked
to make the doll the *default* view ("the standard view everyone expects anyways") and to fold the
equippable tabs into the doll's slots, plus make potions/items stack by quantity.

## Goal
Restructure the hero Inventory/Character screen around the equipment doll, and stack consumables.

## Scope (four parts — ONE focused, verified pass)
1. **Doll as default landing.** Opening the inventory/character screen lands on the paperdoll
   (central hero + equip slots), not the Weapons grid. The EquipmentPanel/Gear Preview layout is the
   home view (matches the Obsidian CHARACTER-panel canon, memory `character-screen-obsidian-paperdoll-reference`).
2. **Fold equippable tabs into slots.** The Weapons / Armor / Accessories tabs collapse into the
   doll's slots — tapping a slot opens its equippable drawer (already per-slot filtered: off-hand =
   shields only, etc., per memory `gear-preview-design`). Those three tabs are just "equippable by
   type," which the slots already express.
3. **Keep non-equippable tabs.** Consumables + Skills remain as their own tabs (not slot-equippable).
4. **Stackable quantities.** Consumables/materials collapse duplicates into ONE cell with a small
   `×N` badge (bottom-right corner — "5 potions = potion icon + small 5"). Default cap 99.
   - Requires adding `Quantity` (default 1) to `ItemVM` (readonly struct, `Assets/_Modules/Core/UI/Mvvm/ItemVM.cs`)
     — backward-compatible optional ctor param; the contract is shared by shop/inventory/loot/crafting.
   - InventoryVM groups owned consumables by id + count; the grid cell renders the `×N` badge.
   - Equipment stays per-item (each weapon/armor is its own cell — can carry individual offset/state).

## Files (expected)
- `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs` — default view = doll; tab strip = [doll] + Consumables + Skills.
- `Assets/_Modules/Village/Hero/InventoryVM.cs` — stack-group consumables; carry Quantity.
- `Assets/_Modules/Village/Hero/InventoryGrid.cs` — render `×N` badge on stacked cells.
- `Assets/_Modules/Core/UI/Mvvm/ItemVM.cs` — add `Quantity` (default 1).
- `Assets/_Modules/Village/Hero/EquipmentPanel.cs` — doll is the default surface; ensure slot drawers cover all equippable types.

## Acceptance criteria
- Inventory opens to the doll (hero + slots) by default.
- No standalone Weapons/Armor/Accessories tabs; their items reached via slot drawers.
- Consumables + Skills tabs still present and working.
- 5 identical potions show as ONE cell with `×5`; using one decrements to `×4`.
- Equipment unaffected (per-item cells, offsets intact).
- Gate: COMPILE_GATE_OK + REGRESSION_OK; bot screenshot of the doll-default view.

## What NOT to touch
- Don't change the equip/attach offset path (AttachmentOffsetRegistry) — orthogonal.
- Don't restyle the Obsidian chrome — reuse the existing BuildObsidianPanel frame.
- Don't add a manual "equippable" filter toggle — the slot IS the filter.

## Notes
- Prereq fix already shipped this session: gold-bag armor icon → real item art in slots (EquipmentPanel
  `ResolveSlotItemArt`), VIEW GEAR ribbon, Orient no-close z-order.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `ItemVM.cs lacks Quantity` — stacking + tab-fold unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
