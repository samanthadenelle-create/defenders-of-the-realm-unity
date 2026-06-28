# WORK ORDER 578 — Inventory shows the gear you actually OWN (reconcile with the Forge)

**Status:** IMPLEMENTED (worktree `agent-a62ea88573c0c0de1`, branch base = `wip/village2-and-f8-tickets` tip `4f51e085`). Not gated/committed (per task).
**Date:** 2026-06-28
**Silo:** Inventory / Gear data-layer (store + ViewModels). No scene files, no presentation grid files.

---

## SYMPTOM (owner felt-bug)
The Inventory screen is EMPTY on every tab, yet the Forge shows the hero wielding gear
(e.g. Emberbrand). "I own Emberbrand but my inventory is empty." Inventory and Forge disagree.

## RCA — why they diverged (verified from code, not comments)

**Two different notions of "owned" existed, reading two different stores:**

1. **Inventory owned-projection = VillageInventory only.**
   `InventoryStore.OwnedWeapons()/OwnedArmor()/OwnedConsumables()`
   (`Assets/_Modules/Village/Hero/IInventoryStore.cs`, pre-change ~:110-146) projected
   *only* from `OwnedCounts` → `VillageInventory.Counts` → `GameState.GearInventory`
   (`Assets/_Modules/Village/Crafting/VillageInventory.cs:22,44`). That dict is only ever
   written by an explicit **purchase / loot / craft** (`VillageInventory.Add`, e.g.
   `ShopVM.cs:417/438`, `PartyShopVM.cs:752/769`, WO-556/564/553 grants).

2. **The hero's gear is class+level AUTO-EQUIP — it never touches that ledger.**
   `GearLoadout.Refresh()` (`Assets/_Modules/Village/Hero/GearLoadout.cs:157-187`) sets
   `EquippedWeapon = GearCatalog.BestWeapon(job, level)` /
   `EquippedArmor = GearCatalog.BestArmor(job, level)` straight from the catalog
   (`GearCatalog.cs:223-253`). Nothing is added to `VillageInventory`. So a fresh hero
   auto-equips Emberbrand but `GameState.GearInventory` stays empty → the inventory
   projection is empty.

3. **The Forge's "owned"/"equipped" signal reads the auto-equipped loadout, not the ledger.**
   `PartyShopVM` BUY rows compute `equipped` from `member.EquippedWeapon.id`
   (`PartyShopVM.cs:535-536, 560-561`) — i.e. straight off `GearLoadout` via the
   `IEquipTarget` adapter (`IEquipTarget.cs:171-176`). That is the auto-equipped piece, so
   the Forge shows the hero "owning"/wielding Emberbrand while the inventory (ledger-only)
   shows nothing. **This is the divergence.**

Net: the player effectively OWNS the auto-equipped gear (it is on the hero, the Forge shows
it), but the owned-projection that feeds the Inventory + the Gear Preview drawer never
included it.

## OWNER DECISION (applied)
Make the Inventory show what the player actually owns, matching the Forge. The owned set is
the **UNION of two sources of truth**, reconciled in the store so the Inventory, the Forge,
and the Gear Preview all agree:
- **(1)** `VillageInventory.Counts` — explicitly acquired gear (buys / boss-quest drops / crafts), AND
- **(2)** the gear each party member currently has **auto-equipped** (main-hand + off-hand + chest armor).
Auto-equip is read-only here — its behaviour is untouched.

---

## CHANGES (data/store layer only)

### 1. `Assets/_Modules/Village/Hero/IInventoryStore.cs` — the reconciliation
- `InventoryStore` gains an optional injected `IReadOnlyList<IEquipTarget> equippedSources`
  (new 2-arg ctor; the existing 1-arg ctor delegates with `null` → unchanged behaviour, so
  all tests + any other caller still compile and behave identically).
- `OwnedWeapons()` (now ~:129-154): inventory weapons **∪** each source's
  `EquippedWeapon` + `EquippedOffHand` (deduped by id, case-insensitive; qty = ledger count
  or 1 for the wielded copy).
- `OwnedArmor()` (~:156-180): inventory armor **∪** each source's `EquippedArmor`.
- `OwnedConsumables()` (~:182-198): unchanged set (equipped gear is never a consumable) +
  added the per-tab FlowTrace.
- Helpers `ForEachEquippedSource` / `AddEquippedWeapon` / `SourceCount` added.
- **§12 FlowTrace:** each method logs the resolved count via
  `FlowTrace.Throttle("Inventory", "owned-weapons|owned-armor|owned-consumables", 1f, …)`
  — a capture now proves the projection returns >0 (e.g.
  `[Flow:Inventory] OwnedWeapons resolved 1 (inventory ∪ equipped; sources=1).`).

### 2. Wiring — inject the equip target(s) into the store at every open-site
(reordered so targets build before the store; **no presentation grid files touched**)
- `Assets/_Modules/Village/Hero/HeroInventoryController.cs:273-285` — pass the hero's
  `_equipTarget` into the store (Inventory screen).
- `Assets/_Modules/Village/Hero/EquipmentPanel.cs:108-150` — pass the `targets` list
  (hero + companions) into the store (Gear Preview / EquipVM).
- `Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs:159-201` — pass the `members` list
  into the store (the Forge / PartyShopVM).

### 3. `Assets/_Modules/Village/Hero/PartyShopVM.cs` — keep SELL honest
`BuildSell()` lists from `OwnedArmor()/OwnedWeapons()`. Those now include auto-equipped
(non-ledger) gear, but `SellGear` only removes from the ledger (`OwnedQuantity>0`,
`PartyShopVM.cs:799`). Added `if (_store.OwnedQuantity(id) <= 0) continue;` to both SELL
loops (~:667-699) so SELL stays exactly ledger-only — no phantom "you don't own that" rows.
DISPLAY consumers (Inventory grid, EquipVM drawer) get the enriched union; SELL does not.

---

## RESULT — the three surfaces now AGREE on "owned"
- **Inventory grid** (`InventoryVM` → `OwnedWeapons/OwnedArmor/OwnedConsumables`): populates
  with auto-equipped gear (marked equipped) + everything bought/looted/crafted. Tab counts
  reflect it (`InventoryVM.BuildTabs`).
- **Gear Preview** (`EquipVM.RebuildCompatible` → same store methods): the swap drawer now
  lists the owned set (including the equipped piece), class-filtered as before.
- **Forge** (`PartyShopVM`): the BUY "equipped" badge already read the loadout; the SELL list
  stays ledger-only. Both consistent with the inventory.
- **Newly-granted items** (WO-556 boss drops / WO-564 quest grants / WO-553 jeweler) flow
  through `VillageInventory.Add` → branch (1) of the union → appear immediately.
- **Auto-equip behaviour:** UNCHANGED (the union is read-only; `GearLoadout` not modified).

## Out of scope / notes
- Accessories (rings/amulets): there is no Inventory accessory tab and no `OwnedAccessories`
  in the store; equipped rings/amulets already surface via `EquipVM` slots
  (`EquipVM.BuildSlots`) and the catalog-sourced `AccessoriesForSlot` drawer — left as-is.
- Presentation files `InventoryPaperDoll.cs` / `InventoryGrid.cs` NOT touched (owned by a
  separate fix; they render whatever the store returns).
- The older `ShopVM` equip/sell tab reads `VillageInventory.Counts` directly (not the store)
  and is a legacy surface; not in scope.

## Validation
- Brace check (all balanced): IInventoryStore.cs 24/24, HeroInventoryController.cs 79/79,
  EquipmentPanel.cs 82/82, PartyShopPanelMvvm.cs 139/139, PartyShopVM.cs 97/97.
- No JSON touched.
- Backward-compat: 1-arg `new InventoryStore(inv)` / `new InventoryStore(null)` preserved →
  `InventorySeamTests` / `InventoryVMTests` unaffected (null sources = pre-change projection).

## Owner-decision flags
- "Owned" = inventory ∪ currently auto-equipped (read-only union). Equipped-but-auto-granted
  gear is shown as owned but is **not sellable** (it re-grants on Refresh) — confirm this is
  the intended SELL rule.
- A future option (not done, would be a bigger change): record auto-equipped picks INTO
  `VillageInventory` for a single source of truth — deferred to keep auto-equip untouched and
  avoid save-write side effects on every Refresh/level-up.
