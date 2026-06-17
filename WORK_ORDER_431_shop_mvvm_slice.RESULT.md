# WORK ORDER 431 — RESULT: Shop MVVM Vertical Slice ✅ DONE (gate + tests green)

**Commit:** `ab41f07a` on `feat/tower-core-loop` (local, unpushed — owner felt-retest first).
**Gate (ARCHITECTURE_PRINCIPLES.md §2c permission gate):** `COMPILE_GATE_OK :: scripts compiled clean`.
**Tests:** 7/7 `ShopVMTests` pass; 308/312 EditMode total (the 4 failures are pre-existing
`buildings.json` drift + a `VillageStrayCleanup.cs` lint flag — none in shop/economy, none touched here).

## Outcome
`ShopPanel` no longer pulls from state. The View went **-912 / +287 lines** — from a 1331-line
weld to a pure `IPanelView` binder. It constructs a `ShopVM` (injecting `EconomyService.Instance`
as the `IEconomy` seam), `Bind()`s it, and `Render()`s from `vm.*` on every `vm.Changed`, routing
tabs / filters / rows / buttons to VM commands. The only `EconomyService` reference left is the
single inject-at-open-site.

## Files
- **CREATE** `Assets/_Modules/Village/Hero/ShopVM.cs` — pure C# ViewModel (`IPanelViewModel` +
  `IDisposable`); all economy/affordability, catalog→row building, `VendorStockContract` intersection
  + type filter, WO-406 never-empty fallback, buy/sell/equip execution + verbatim status strings,
  vendor-gold pools, 50/50/30% sell rates, `CurrentStock`. No UnityEngine UI types. Icons carried as
  keys (`IconRole`=kind, `IconName`=id); the View resolves sprites via `ItemIconCatalog`.
- **CREATE** `Assets/_Modules/Village/IEconomy.cs` — minimal economy seam in `DeNelle.Village` (it
  speaks `ResourceCost`, which Core can't see). `EconomyService` implements it (1-line additive).
- **MODIFY** `Assets/_Modules/Village/EconomyService.cs` — `: MonoBehaviour, IEconomy` (additive only).
- **MODIFY** `Assets/_Modules/Village/Hero/ShopPanel.cs` — now the View/binder. `Open`/`CurrentStock`/
  `VendorContext` kept public so AutoPilot + DialogueCommandBridge callers are unchanged.
- **CREATE** `Assets/Tests/EditMode/ShopVMTests.cs` — 7 tests (fake `IEconomy`): items/prices per
  mode, affordability flips with balance, Buy spends + raises Changed, unaffordable no-spend + status,
  SetMode resets selection, Dispose unsubscribes.

## Judgment calls (preserved behavior)
- **IEconomy** lives in Village (ResourceCost dep); event is `Action<ResourceSnapshot>` to match
  EconomyService's existing event.
- **Tests** placed at `Assets/Tests/EditMode/` (the existing `DeNelle.Tests.EditMode` asmdef already
  references Village) rather than the path literally in the WO — no new asmdef needed.
- **Equip** via `IShopEquipTarget` + a `LoadoutEquipTarget` adapter over `GearLoadout`, so the pure VM
  reads equipped names/stats + equips-by-id without touching a GameObject.
- **Detail pane** via a `ShopDetail` struct (name/desc/stats/cost + icon keys); the View resolves the
  Sprite (presentation, not a state-pull).

## Remaining
- Owner felt-retest each vendor (Forge/Armorer/Jeweler/Arcane/Market/Lumber + default), Buy/Equip/Sell,
  affordability colors, vendor-gold messages — then push.
- NEXT (UI_MVVM_BINDING_MAP.md §5 step 3): generalize the bound `Slot` card unit, then roll the same
  pattern through inventory → equipment → crafting → quests.
