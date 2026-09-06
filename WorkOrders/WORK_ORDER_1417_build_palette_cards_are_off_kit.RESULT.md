# WO-1417 RESULT - build palette item cards on the kit

**Status:** FIXED 2026-09-05 (gated; headless capture opened; device build follows tonight)

## What landed
- `Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs` - each item card is an obsidian plate (`ElarionUiKit.ObsidianFill`) with a gold perimeter, artwork, name, cost in words via `CostFormat.Words(CostParts(vm.EffectiveCost))` (nothing painted while the freebie is live, WO-1010 D20), and ONE state word (`Locked` / `Built` / `Unaffordable` / `Ready`) - no `COST: NO COST`, no `[READY] AVAILABLE`.
- `Assets/Editor/Regression/BuildCollectionPlayerRegression.cs` - kit-card pins (StringLiterals walker) so the flat navy boxes cannot return.
- `Assets/_Modules/Village/Catalog/Generated/CatalogFallbackData.g.cs` regenerated with the catalog.

## Evidence
- `COMPILE_GATE_OK` (`Builds/c3`, 21:43); `REGRESSION_OK 385/385` (`Builds/r3`, 21:45), `[build-collection-player] BUILD_COLLECTION_PLAYER_OK`.
- Headless capture `UI_CAPTURE_OK 91` (`Builds/cap2`, 21:47) + `REGISTERED_SECONDARY_CAPTURE_OK 36/36 touch=clean` (`cap2s`). `BuildCollections_2670x1200.png` OPENED: the category launcher paints on the kit. **No headless fixture drills into a category's item cards** (measured: `UICaptureLaunch.cs` has no `BuildCard_`/item-card capture), so the item-card visual is proven on the device screencap after install, not here; the composition itself is pinned by the suite. A capture fixture for one open collection is owed (folded into WO-1418 lane C's capture work or a follow-up).

## Follow-up
- WO-1418 lane A promotes this card's gold perimeter into the kit (`ElarionUiKit.GoldPerimeter`); this file migrates to it in a later commit with the `AddGoldPerimeter(card.transform)` pin re-pointed.
- Owner felt-test on the tester build closes it.
