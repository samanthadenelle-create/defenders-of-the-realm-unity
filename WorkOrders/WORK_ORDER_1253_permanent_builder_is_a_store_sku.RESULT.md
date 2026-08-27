# WO-1253 RESULT — Permanent builder is a store SKU

**Status:** IMPLEMENTED (not committed; Unity / COMPILE_GATE / DataRegression not run — task forbade Unity).
**Crystal sink ruling:** KEEP BOTH (WO recommendation, owner ruling not received). Crystals still buy DEPTH via `TryBuySlot`. Real money buys a BUILDER (concurrency). Crystal path was not deleted.

## SKU

| Field | Value |
|---|---|
| id | `permanent-builder` |
| name | Permanent Builder |
| usd | 9.99 (wallet required; reinstall-safe entitlement) |
| skr authored | 120 (peg; live SKR is server-quoted) |
| band | patronage |
| storeVisible | true (not born hidden; WO-1246) |
| grant | SKU ownership in `GameState.OwnedItemIds` = +1 concurrent Builder |
| not granted | queue depth (`queueDepthPerLine` stays 5) |

Authored in both canonical `packs.json` copies (byte-identical) and `api/_lib/purchase-catalog.js` `USD_ANCHORS['permanent-builder']=9.99`. No existing SKU ids deleted or renumbered. Settlement is the existing `purchase_entitlements` / `PackStoreVM.ApplyPackContents` / `RestoreFulfilledOwnership` rail.

## Manage copy

| Surface | Copy | Width pin |
|---|---|---|
| Button | `Buy builder` | 11 chars / 176 px at 16px/glyph vs 194 px button on 640-wide canvas |
| Label (unowned) | `Permanent builder +1` | 20 chars / 320 px vs 360 px column |
| Label (owned) | `You own this builder` | 20 chars |
| Upgrade queue-full (crystal DEPTH, KEEP BOTH) | `Buy a queue slot - {0} Crystals` | renamed so crystals do not sell a "builder" |
| ObsidianQueueHud crystal affordance | `+queue` | was `+slot` |

Words carry the product. No hue. ASCII.

## Concurrency path

1. Player taps Manage **Buy builder**.
2. `ManageScreenVM.BuySlot` -> `PackStore.RequestFocusSku("permanent-builder")` -> `PanelRouter.Open(PanelId.RealmStore)`.
3. Payment settles on the existing quote/verify rail (currency SKR).
4. `PackStoreVM.ApplyPackContents` records the SKU in `OwnedItemIds` (idempotent `RecordOwned`).
5. FlowTrace.Step `"player bought builder"` then `"player bought builder applied"` or `"player bought builder already-had"`. Never logs a wallet.
6. Reflection hook `BuildTimerService.OnPermanentBuilderEntitlement` pulls pending jobs into the new crew slot.
7. Live concurrency: `SlotCount(Builder) = ConcurrencyOf(freeBuildSlots, BoughtSlots, ownsPermanentBuilder)` = 2 + crystal-bought + (owns SKU ? 1 : 0).
8. Live depth: `QueueDepthLimit = DepthOf(queueDepthPerLine, BoughtSlots)` — **SKU is not an input**. Defaults stay 2 and 5.

A player without the entitlement is unchanged (crew 2, depth 5). A re-settle cannot grant a second crew (ownership is a flag, not a GearInventory stack).

Crystal `TryBuySlot` remains on BuildingUpgradeVM (queue-full DEPTH remedy) and ObsidianQueueHud (`+queue`).

## Regression

`Assets/Editor/Regression/BuilderSkuRegression.cs` wired as `[builder-sku]` in `DataRegression.RunAll`.

Proves: catalog+USD mirror; concurrency +1 and depth unchanged; idempotent re-settle; unrelated SKUs do not grant a crew; Manage does not name `TryBuySlot`; crystal path still present; label width budgets. If `PackCatalog.Find("permanent-builder")` is null the suite `RegressionOutcome.Skip`s (never quiet green). Settle through real `ApplyPackContents` PartialSkips if GameStateService cannot install.

## Not done (per task)

- No commit, no Unity, no `COMPILE_GATE_OK` / `REGRESSION_OK` on a fresh log.
- WO Status line left READY TO IMPLEMENT.
- Purchase rail, HudKitController, LoginPanel, BackendRequestSigner untouched.
