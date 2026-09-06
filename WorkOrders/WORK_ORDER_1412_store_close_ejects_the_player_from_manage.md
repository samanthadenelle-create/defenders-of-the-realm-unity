# WO-1412: Manage -> store -> CLOSE lands on the HUD, ejecting the player from Manage; BUY BUILDER is unpriced and shows while a slot is free

**Status:** IN PROGRESS - ABSORBED INTO WO-1418 lane D (Codex batch, BATCH_STATE PART 8 / 8.5 ruling 3: the sending tab rides the existing return-door arbiter); lands and flips with 1418. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

## Evidence
- Device walk (build 355952) - SEEN (`REVIEW_MERGED.md` row 11): `docs/qa/UI_REVIEW_2026-09-05/11-research-upgrade-door.png`
  (the store open, reached from Manage) -> `12-hud-after-store-close.png` (CLOSE landed on the town HUD, Manage gone).
- `09-troops-queue-drawer.png` (device): drawer row `Permanent builder +1` with `BUY BUILDER`, no price, while 1 of
  2 builder slots is free.
- Both reviewers: `REVIEW_A_independent.md` A-5, `REVIEW_B_independent.md` A6 / A9.
- CODE: `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:1398` detects the `BUY BUILDER` copy on the drawer
  row; the store is `Assets/_Modules/Wallet/PackStore.cs`. WO-1400 built the deck return door (the arbiter records
  the opener and CLOSE returns to it) - the Manage->store return is the SAME mechanism with a different opener.
  Slot count: `BuildTimerConfig.freeBuildSlots` (2) and the busy count in `BuildTimerService`; the extra-slot
  purchase seam is `BuildTimerService.TryBuySlot`.

## What the player experiences
Mid-management, the player follows a builder upsell into the store, taps CLOSE, and is standing in the town with
Manage closed - the loop they were in is gone. The upsell itself names no price and appears while a builder is
already free, so it reads as noise.

## Fix shape (one mechanism)
- Return door: when Manage opens the store, the opener (PanelId.Manage + the sending tab) is recorded exactly as
  WO-1400 records a deck opener; PackStore CLOSE routes `PanelRouter.Open(PanelId.Manage, "<tab>")` instead of
  falling to the HUD. No second return path - extend the one arbiter.
- `BUY BUILDER` reads its price from the pack VM: `BUY BUILDER - 511 SKR (~$9.99)`; the row is rendered ONLY when
  busy == max slots. When a slot is free the row reads `1 slot free - tap TRAIN to fill it` (no door to the store).

```
Manage[Troops] -> store -> CLOSE -> Manage[Troops]      (opener recorded, one arbiter)
drawer, all slots busy:  Permanent builder +1   [ BUY BUILDER - 511 SKR (~$9.99) ]
drawer, a slot free:     1 slot free - tap TRAIN to fill it
```
Trace: `FlowTrace.Step("Store", "close -> return opener=Manage tab=<tab>")`; `FlowTrace.Step("Manage",
"builder upsell shown=<bool> busy=<n>/<max> price='<text>'")`.

## Acceptance
- [ ] RED first: `StoreReturnToManageRegression` - open Manage Troops, open the store from the drawer, close it:
      the active panel is Manage on tab Troops (trace line), not the HUD; drawer fixture with a free slot: no
      label contains `BUY BUILDER`; all-busy fixture: the label contains `SKR` and `$`. Fails on the current tree.
- [ ] Headless: `ManageQueueTroops_2670x1200.png` (stale 09-01) regenerated for both fixtures
      (`MANAGE_OPERATIONAL_CAPTURE_OK 12/12`), opened.
- [ ] Device: reproduce the 11 -> 12 walk; CLOSE returns to Manage; screencap read.

## Not in scope
The store's contents or wallet-less shelf (WO-1409); deck return (WO-1400, shipped); the slot purchase price itself.

## Owner ruling
- Section 2 #6 Return-to-Manage? - written to the default YES.
- Section 2 #7 Upsell-when? - written to the default busy-only.
