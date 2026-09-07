# WO-1515: the defense report is an unreadable tan slab with overlapping rows, and its only door is buried under Settings

**Status:** READY TO IMPLEMENT - P1, owner-ask (ruling received 2026-09-06 20:05)
**Silo:** `Assets/_Modules/Village/UI/Defense/DefenseReportPanel.cs` + a new HUD chip.
**LANDS AFTER** the WO-1465 / 1466 / 1468 lane commits - that lane is editing `HudKitController.cs` tonight.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1515 -> 1516 in the same edit).

## 1. EVIDENCE

Owner device frame `Logs/device/screens/owner-defense-report-20260906-200350.png`, build 2026.09.07.358574,
20:03. Her words tonight: *"screenshot of defense report"*.

Right pane is a flat BEIGE rectangle with grey text, all near-invisible:

```
"They never reached your inner ring."   "Defence score 100/100 - Clean hold"
"ATTACKER"                              "Strength 147 - wave 13 - lasted 44s"
```

Only `Hollow Host (raiders)` is legible; the heading `HELD` is gold-on-tan.

Left list: `HOLLOW HOST - 6H AGO` paints OVER the BREACHED row's gold frame - the two rows share one band.

## 2. FIX SHAPE

- Build the detail pane with the kit obsidian plate + gold bezel and `ElarionUi.Ink` on dark, the way
  `ManageScreenPanel` does. No bespoke tan surface.
- Rows laid out by the kit list with a DERIVED pitch (not a fixed offset), `FitSingleLine` on every row.
- A measured layout case at 2670x1200: no row overlaps another, and the detail text meets a contrast ratio.

## 2B. THE DOOR (owner ruling, 2026-09-06 20:05, verbatim)

> "the only way to get to the defense report is buried under settings then realm. should be on screen as a
> button if there is a report that is incoming"

Today's only routes, at source:

```
SettingsController.cs:748        PanelRouter.Open(PanelId.DefenseReport)   -- Settings -> Realm
PlayerDeckWorkspace.cs:736       the same panel as a deck route
```

Required:

- When an UNREAD report exists (`DefenseReportLedger.All()` has an entry newer than the last-read mark), a HUD
  chip appears in the RIGHT COLUMN, same family and same kit as the Builders status chip: derived band,
  `ElarionUiKit.MinTouchPx`.
- It reads `ATTACK REPORT` with the outcome word (`HELD` / `BREACHED`), opens `PanelId.DefenseReport` through
  `PanelRouter`, and DISAPPEARS once read.
- Settings -> Realm stays as the ARCHIVE door.
- The WO-1408 lane already routes the welcome-back ATTACKED row to this panel; this chip is the in-town
  equivalent of that door.

## 3. WHAT NOT TO DO
- Do not leave the chip on screen when there is nothing unread. A permanent chip is a fifth status glance
  competing with the four that earn their place.

## 4. ACCEPTANCE
- [ ] Detail pane on the kit obsidian plate; no row overlap; measured case at 2670x1200 with a contrast assert.
- [ ] One measured case that the chip exists ONLY while an unread report exists.
- [ ] A headless PNG with the chip on screen, opened and looked at.
- [ ] `REGRESSION_OK n/n` on a fresh log.
