# WORK_ORDER_108 — Player Build Mode — RESULT

**Status:** VERIFIED — already complete. Wallet reconciliation confirmed unified; **no code change required.**
**Date:** 2026-06-08
**Branch:** feat/tower-core-loop
**Scope of this pass:** verify the full build loop + close the wallet-reconciliation gap that the wallet merge (CrystalEconomy → GameState.Resources.Crystals façade) might have left. Outcome: the spend path was **already unified**; nothing to fix.

---

## What exists (the full place→preview→validate→spend→persist→replay loop)

| Piece | File | Role |
|---|---|---|
| Build mode driver | `Assets/_Modules/Village/BuildMode/BuildModeController.cs` | Enter/Exit/Toggle, ghost-follow place, grid snap, `IsValidPlacement`, charge-after-commit `Place()`, upgrade/sell, `CommitLayout` persist |
| Runtime replay | `Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs` | Reads `GameState.BaseLayout`, rebuilds via `StructureFactory.Create`, `_loadedOnce` guard, NavMesh carve |
| One creation path | `Assets/_Modules/Village/Catalog/StructureFactory.cs` | single `Create(entry, pose, root)` |
| Buckets | `Assets/_Modules/Core/Catalog/CatalogRegistry.cs` | catalog lookup by id |
| Economy ledger | `Assets/_Modules/Village/EconomyService.cs` | multi-resource `CanAfford`/`TrySpend`/`Grant`; Crystals+Food GameState-backed, Wood/Iron in-session |
| Crystal façade | `Assets/_Modules/Village/CrystalEconomy.cs` | thin shim over `GameState.Resources.Crystals` (save v18 fold) |
| Build button wire | `Assets/_Modules/Village/BuildMode/BuildButtonBridge.cs` | HUD Build button → `BuildModeController.Toggle()` (reflection, Village→HUD safe) |
| Persistence | `Assets/_Modules/Core/State/SaveSchema.cs` | `baseLayout` (v14), crystal fold (v18) |
| Supporting | PlacementGrid, GhostPreview, BuildPaletteUI, PlacedStructure, IBuildInput/DesktopBuildInput/LeanTouchBuildDriver, BuildPreviewModal | input + UI + grid |

---

## Wallet reconciliation outcome — UNIFIED (already correct, no fix)

The cost/spend code in `BuildModeController.cs` (lines ~1081–1124) routes **entirely through the unified persisted wallet**:

- `CostFor(entry)` → multi-resource `ResourceCost` (multi-cost wins; crystals-only fallback for legacy/cost-less rows). **Multi-resource (wood/food/iron/crystals) honored — not crystal-only.**
- `CanAfford(cost)` → `EconomyService.Instance.CanAfford(ToEconomy(cost))` — checks all four pools. Service-less fallback reads `CrystalBalance` (= `GameState.Resources.Crystals`).
- `ChargeLedger(cost)` → `EconomyService.Instance.TrySpend(ToEconomy(cost))` — atomic multi-resource spend. Crystals/Food deducted via `GameStateService.AddCrystals/AddFood` (persisted), Wood/Iron from in-session pool. Service-less fallback charges `GameStateService.AddCrystals(-crystals)` directly.
- `RefundLedger(cost)` → `EconomyService.Grant` / `GameStateService.AddCrystals(+…)` fallback.

`EconomyService` (verified) is GameState-backed for Crystals (`GameState.Resources.Crystals`) and Food (`GameState.Resources.Food`); `TrySpend` deducts crystals/food through `GameStateService` (persisted) and wood/iron in-session. `CrystalEconomy` is a pure façade over the same field.

**No stale session-only int. No double-charge fallback** — the `GameStateService.AddCrystals` branches in `ChargeLedger`/`RefundLedger` are mutually exclusive with the `EconomyService` branch (`if (econ != null) { … return; }`), so the persisted wallet is never charged twice. Charge happens **once, after** a confirmed valid spawn (`Place()` line 687, post-`Spawn` null-check), per WO-131 charge-after-commit.

**Verdict: already unified. No edit made.**

---

## Acceptance criteria

| # | Criterion | Status | Note |
|---|---|---|---|
| 1 | Enter/Exit/Toggle build mode | PASS | `BuildModeController` Enter/Exit/Toggle; ghost/grid/palette self-create |
| 2 | Ghost follows cursor, grid snap | PASS | code present (GhostPreview + PlacementGrid snap) — NEEDS-PLAYTEST for feel |
| 3 | Valid/invalid placement gate | PASS | `IsValidPlacement` + `CanAfford` gate before commit |
| 4 | Spend resources on place (multi-resource) | PASS | `ChargeLedger` → `EconomyService.TrySpend`; wood/food/iron/crystals all honored |
| 5 | Charge only after valid commit (no double-charge) | PASS | `Place()` charges once post-spawn; mutually-exclusive ledger branches |
| 6 | Spend routes through unified persisted wallet | PASS (verified this pass) | EconomyService GameState-backed; CrystalEconomy is a façade |
| 7 | BaseLayout persists across restart | PASS | `SaveSchema.baseLayout` v14; `Place()` appends, `CommitLayout`/Exit persists |
| 8 | Replay rebuilds via the one Create path | PASS | `BaseLayoutLoader.Spawn` → `StructureFactory.Create(entry, pose, Root)` |
| 9 | Double-build trap guarded | PASS | `_loadedOnce` early-return in `LoadFromState()`; later placements add one piece via `Spawn` |
| 10 | Build button toggles build mode | PASS | `BuildButtonBridge` wires HUD `BuildRequested` → `BuildModeController.Toggle()`, idempotent per scene load |

---

## Remaining play-mode-only checks (for Tricia)

1. Tap **Build** on the village HUD → build mode opens (palette appears, ghost spawns).
2. Place a structure → resources tick down in the HUD by the correct amount (try a multi-resource item, not just a crystal item — confirm wood/iron/food drop too).
3. Try to place when broke → placement refused, nothing spawned, no resource change.
4. Place a few, exit, **restart the game** → the placed structures reappear at the same cells (BaseLayout replay), and the default village does NOT double-up.
5. Sell a structure → ~50% refund returns to the wallet and persists.
6. Confirm the move-joystick zone doesn't fire a placement (exclusion already handled in BuildModeController).

---

## Files edited

**None.** Verification pass only — the wallet path was already unified through `EconomyService`/`GameStateService`. No brace check needed (no `.cs` touched). Read-only confirmations (a)–(d) all PASS.
