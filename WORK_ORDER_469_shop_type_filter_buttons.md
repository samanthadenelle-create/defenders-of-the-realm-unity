# WORK_ORDER_469 — Shop type-filter BUTTONS (mobile), scoped to the vendor's allowed types

**Status: READY TO IMPLEMENT** · Drafted by read-only agent (2026-06-21), reconciled to code.

## Problem
In the vendor shop, filter-by-type should be **mobile-friendly BUTTONS (not a dropdown)**, scoped to the
vendor's *allowed* gear types (staff/dagger/shield/bow/etc. per the gear json) — finer than the existing
coarse Weapon/Armor/Potion split.

Current (reconcile, don't greenfield): a `FilterBar` already exists in `ShopPanel.cs` (`BuildFilterBar`
~384-408, 4 buttons calling `_vm.SetFilter(GearKind)`) but `ShopVM.FilterBarVisible => Mode==Sell` — so it
only shows on SELL, and only filters by coarse GearKind. Per-category data exists on `WeaponDef.category`
(`GearCatalog.cs:59`) but **weapons.json rows don't populate `category` yet** (only `job`/`rarity`).

## Files
- `weapons.json` (Resources + StreamingAssets copies, in sync) — populate `category` on each row (staff/sword/bow/…).
- `ShopVM.cs` — add `_typeFilter` + `SetTypeFilter(category)` + `BuyFilterTypes` (distinct categories in the
  vendor's allowed stock); make `FilterBarVisible` true on BUY; narrow `BuildBuy()` rows by `_typeFilter`
  (match `WeaponDef.category`, fall back to `job` when empty so nothing vanishes).
- `ShopPanel.cs` — render one `ElarionUiKit.ButtonPack` per `_vm.BuyFilterTypes` (+ "All"), large tap targets,
  `onClick → _vm.SetTypeFilter`; update `HighlightFilter()`.

## Acceptance
- BUY tab shows a row of TYPE BUTTONS (one per category the vendor stocks); tapping filters; "All" restores.
- Buttons are large mobile tap targets. Vendor lock (`VendorStockContract.AllowedFor`) still holds.
- Un-migrated rows (empty category) still appear via `job` bucket (WO-406 never-empty guard).

## NOT to touch
VendorStockContract mapping; the scroll-list render (ShopPanelRowRenderTests); MVVM seam (logic in VM, View dumb); economy/gold.

## INSTRUMENT-FIRST (§12 hard gate)
Add `FlowTrace.Step("Shop", …)` logging active `_typeFilter` + derived `BuyFilterTypes` + post-filter row
count. Headless fleet (run-defenders): open each vendor on BUY, tap each type button, capture `[Flow:Shop]`
— prove button set matches allowed categories, each filter narrows (never built-but-blank), "All" restores.
