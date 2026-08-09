# WORK ORDER 516 — Inventory panel: add Equip action + restyle to match the store

**Status:** DONE (reconciled 2026-08-09 from the tree - `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs:116` now builds the selected item's name, stats and an explicit Equip/Use CTA, and the panel was restyled under the Obsidian conformance program (WO-713/714). NOT felt-verified; no `.RESULT.md`)

**Status:** CAPTURED (F8 ticket, 2026-06-26) · **Silo:** UI/Presentation (code) · **Type:** EXISTING (reuse, not greenfield)
**Source:** owner F8 in `MainCastle_Hall` — *"no equip button and have creative look at this UI to match others"*
(flag_00.png = the INVENTORY panel open).

## Problem (from the F8 screenshot)
1. **No Equip button** — the inventory opens and lists items but you cannot equip from it; the equip action
   currently only lives in the store flow.
2. **Styling mismatch** — the inventory content area is the old dark slab; it does NOT match the framed,
   premium card look of the redesigned store (WO-501).

## Goal
Inventory becomes a first-class equip surface that looks like the rest of the UI: select an item → **Equip**
(honoring the hand-slot rules), with the same framed card / preview styling as the store.

## Reuse (do NOT greenfield)
- **Equip flow:** `HeroLoadout` + `EquipmentController` (`Assets/_Modules/Village/Hero/`) — the real equip
  path; the store already drives it. Hand-slot rules are already enforced + regression-gated
  (`DataRegression.CheckHandSlotRules`, per `docs/STORE_EQUIP_SPEC.md`).
- **Framed styling + buy/sell+equip layout:** the WO-501 store redesign (`PartyShopPanelMvvm` / `PartyShopVM`,
  `Assets/_Modules/Village/Hero/`) — match its card frame + 3D preview + action-button styling.
- **Inventory panel:** locate the live inventory panel class at implementation time (WO-434 inventory-mvvm /
  WO-465 premium-mobile-inventory lineage) and add the Equip button there; do not build a new panel.

## Acceptance
- From the inventory, selecting an equippable item shows an **Equip** button that equips it (and the
  hand-slot rules hold — 2H clears off-hand, etc.).
- Inventory content area visually matches the store's framed look (owner felt-approves the match).
- No regression to the existing store equip flow (`REGRESSION_OK`).

## NOT in scope
The battle HUD (separate). Party/multi-hero selectors (single-hero V1 — STORE_EQUIP_SPEC's party selector
is superseded). Tuning exact colors (owner felt-pass).
</content>
