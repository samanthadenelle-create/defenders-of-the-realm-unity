**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 434 — Inventory + Equipment on MVVM, with a live gear-apply VIEWER

**Status: READY TO IMPLEMENT (phased)** · Follow-on to WO-431/432/433 (shop arc proved the pattern).
**Lane:** UI / presentation + a small model seam. Phased so each slice is gate-verifiable.
**Owner vision (2026-06-17):** *"The model is what we own; the rest is a viewer."* Store → inventory →
UI, all pull model data; skins plug into what they need by condition/type. This WO makes inventory a
pure viewer over the owned model, and builds the **live hero viewer** that also serves **troop
creation for raids/defenses** and (via the per-class model) **talent trees by player type**.

---

## Current state (from read-only research — accurate as of this WO)

**TWO overlapping panels exist — unify, don't skin both:**
- `Village/Hero/HeroInventoryController.cs` (+ partials `InventoryUIBuilder/InventoryGrid/InventoryPaperDoll/InventorySidebar`) — HUD-opened (`HeroEquipHud`), `GridLayoutGroup` (5-col landscape/4 portrait, 78×72 cells), tabs Weapons/Armor/Outfits/Consumables, a UI-only paperdoll (medallion + bars, **no 3D**), equips directly on cell tap.
- `Village/Hero/EquipmentPanel.cs` — yarn-opened (`CmdOpenEquip`), vertical list, party-target picker (hero + companions), equips via a row "Equip" button, gold-medallion portrait.
- Both **pull state directly** (the §2 violations a VM cuts): `GearLoadout`, `GearCatalog`, `VillageInventory`, `HeroAbilities`, `HeroProgression`, `GameStateService`. Both equip via `GearLoadout`.
- **Data gap:** inventory currently lists *class-eligible catalog* gear, not *owned* gear (`VillageInventory.Counts`). `EquipmentPanel` already reads owned + falls back to catalog.

**Model API (the source the VMs read — all exist):**
- `VillageInventory.Instance`: `Counts` (id→qty), `Get(id)`, `Add(id,n)`, `TryConsume(id,n)`, `Changed` event. (Owned gear; persisted to `GameState.GearInventory`, Neon-synced.)
- `GearLoadout`: `EquipWeaponById(id)`, `EquipArmorById(id)`, `EquippedWeapon`, `EquippedArmor`, `WeaponMult`, `ArmorDefense`, `OnGearChanged`, `BindOwnerClass(job)`, `Refresh()`. **NO Unequip API — must add.**
- `GearCatalog`: `FindWeapon/FindArmor(id)`, `AllWeapons/AllArmors()`, `WeaponFitsClass/ArmorFitsClass`, `GetBuyCost`. Per-class model (`abilities.json`/gear sets keyed by class — the "model linked to type").
- Equip→world-visual is **automatic**: `EquipWeaponById/ById` → `ApplyStats()` → `OnGearChanged` → `GearVisualApplier`/`EquipmentController` re-attach meshes. (Armor body art is a known NO-OP stub.)

**Viewer: none exists.** No in-UI live hero preview. Build from the proven `Village/UI/TowerPreviewCamera.cs` pattern: prefab on a hidden layer + dedicated Camera + RenderTexture + **manual `camera.Render()` per frame** (URP won't auto-render an off-screen cam) → `RawImage` in the panel.

**Blink target art:**
- `Obsidian_UI/Prefabs_Obsidian/Inventory.prefab` — `Slots` GridLayoutGroup: **79×79 cell, 5px spacing, 2-col, 36 slots**; each `SlotBG` = plate sprite + Icon child + stack-count label.
- `Obsidian_UI/Prefabs_Obsidian/Characters.prefab` — equip **paperdoll**: individually-positioned `WeaponSlot1/2` + armor slots (100×100) around a central portrait, **not a grid**.
- Imported already: `Resources/RpgUi/slot/slot_item.png` (WO-432), panel/icons/bars/potion/button roles. **Missing:** an equip-slot frame (`slot_equip`) — or reuse `slot_item` with a View tint.

---

## MVVM contracts (bind to the existing `DeNelle.Core.UI.Mvvm` seam)

### InventoryVM : IPanelViewModel, IDisposable
- **Data:** `Title`; `IReadOnlyList<ItemVM> Slots` (OWNED items, from `VillageInventory` — closes the data gap); `int SelectedSlotIndex`; `InventoryDetail? Selected` (name/desc/stats/stack + icon keys + CanUse/CanEquip); `IReadOnlyList<InventoryTab> Tabs` (label+count); `int ActiveTabIndex`; `string WeightCapacity` (if used); `string Status`.
- **Commands:** `Select(int)`, `SelectTab(int)`, `Use()`, `Drop()`, `Equip()` (hands selected item to the equip side), `Close()`.

### EquipVM : IPanelViewModel, IDisposable
- **Data:** `(string IconRole,string IconName) Portrait`; `string CharacterLabel` (name/class/level); `IReadOnlyList<EquipStat> Stats` (label + `BarVM`); `IReadOnlyList<SlotVM> EquipSlots` (keyed: mainhand/offhand/chest/…); `int SelectedSlotIndex`; `IReadOnlyList<ItemVM> CompatibleItems` (inventory items valid for the selected slot); `string Status`.
- **Commands:** `SelectSlot(int)`, `Equip(itemId)`, `Unequip()`, `Swap(itemId)`, `SelectTarget(int)` (hero/companions, preserve EquipmentPanel's party picker), `Close()`.

### Model seams (mockable — mirror `IShopEquipTarget`/`IEconomy`)
- `IInventoryStore` — wraps `VillageInventory` + `GearCatalog` (owned items, defs, fit-by-class) so the VM never names the concretes.
- `IEquipTarget` — wraps a `GearLoadout` (equipped names/defs, Equip/Unequip, stats) + target identity (hero/companion). Generalizes `IShopEquipTarget`.

---

## Phasing (each phase = its own gate + commit; do NOT do all at once)

**Phase A — model seam + Unequip.** Add `GearLoadout.UnequipWeapon()/UnequipArmor()` (additive; clears the slot, `ApplyStats`, fires `OnGearChanged`, persists "none"). Add `IInventoryStore`/`IEquipTarget` interfaces; `VillageInventory`/`GearLoadout` implement/adapt. Ships with unit tests for Unequip + the adapters. No UI change.

**Phase B — InventoryVM + EquipVM (pure, tested).** Extract all state/logic from `HeroInventoryController`/`EquipmentPanel` into the two VMs (no UnityEngine UI types). **Unify the two panels' logic into these VMs.** Ship `InventoryVMTests`/`EquipVMTests` (owned-list projection, tab filtering, select→detail, equip/unequip/swap raise `Changed`, compatible-items filter, dispose unsubscribes). This is the §2c permission gate. No view rebind yet.

**Phase C — rebind the views (flag-gated Blink dressing).** Make `HeroInventoryController` (the keeper) an `IPanelView` binding `InventoryVM`; fold `EquipmentPanel` into the equip view binding `EquipVM` (retire the duplicate, keep its yarn entry pointing at the unified panel). Dress with Blink art gated on `FeatureFlags.BlinkChrome` (grid uses `slot_item`; paperdoll slots use `slot_equip` — import it like WO-432, or tint `slot_item`). Behavior/flag-OFF look preserved.

**Phase D — the live gear-apply VIEWER.** Build `HeroPreviewViewer` from the `TowerPreviewCamera` precedent: a hero body on a hidden layer + Camera + RenderTexture + manual render, bound to a `RawImage` in the equip panel. On `EquipVM.Changed`, mirror the equipped loadout onto the preview body so **apply-gear-and-see-it** works live. Lifecycle: instantiate on open, dispose on close. **Design it reusable** — the same component previews any actor, so it serves **troop creation for raids/defenses** (Phase E candidate, separate WO). Note: armor body-art is a NO-OP stub today (weapon shows; armor visual lands when art does).

---

## Acceptance criteria
- [ ] Each phase passes the compile gate (`COMPILE_GATE_OK`) and its unit tests (§2c) before commit.
- [ ] Inventory shows **owned** items from `VillageInventory` (data gap closed), not class-eligible catalog.
- [ ] One unified inventory/equipment surface (no duplicate panel); both former entry points (HUD button + `CmdOpenEquip`) reach it.
- [ ] Equip/Unequip/Swap work via the VM; world hero updates (existing auto-visual path); flag-OFF look unchanged.
- [ ] Flag-ON: Blink grid plates + paperdoll slots read as one Obsidian surface.
- [ ] Phase D: selecting/equipping updates the live hero viewer in-panel.
- [ ] No UnityEngine UI types in either VM; mockable seams; owner felt-retest per phase.

## What NOT to touch
- Do NOT greenfield `GearLoadout`/`GearCatalog`/`VillageInventory` — adapt behind interfaces (additive).
- Do NOT remove the auto equip→visual pipeline (`GearVisualApplier`/`EquipmentController`).
- Do NOT change the flag-OFF appearance; do NOT restyle when flag OFF.
- Do NOT break the scroll/grid mechanism; edit `.cs` via Write/Edit on Windows path only (§0).
- Lead is sole committer (LFS art staged by explicit path).

## Reuse / roadmap notes
- The **viewer** (Phase D) is the troop-creation viewer for raids/defenses — spec that as its own WO once the component lands.
- **Talent trees by player type** ride the same model (`AbilityCatalog`/`abilities.json` per class — confirmed in docs): a future `TalentVM` reads the per-class kit; the tree is a viewer. Separate WO.

*Cross-ref:* `docs/UI_MVVM_BINDING_MAP.md §2/§3/§5`, `ARCHITECTURE_PRINCIPLES.md §2/§2b/§2c`, WO-431/432/433, `TowerPreviewCamera.cs` (viewer precedent), `WORK_ORDER_429` (store-stock-from-DB, parallel backend track).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
