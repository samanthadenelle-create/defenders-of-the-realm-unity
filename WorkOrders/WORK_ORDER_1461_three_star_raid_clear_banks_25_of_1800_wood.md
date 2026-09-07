# WO-1461: a 3-star raid clear banks 25 wood of the 1800 the deploy screen promised

**Status:** READY TO IMPLEMENT
**Silo:** raid reward settle + `RaidDeployScreen` spoils line + the WO-1434 pending-retention store.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1461 -> 1462 in the same edit).

## 1. EVIDENCE

`troop-ai-blind-2026-09-06.log`:

```
14:37:40.331  loot settled ... 1800w 1100i
              repeat-clear multiplier x0.25 -> 450w / 275i
14:37:40.333  [Flow:Bank] BANK FULL [Grant] Wood: requested 450, banked 25, LOST 425
```

The deploy screen for that same camp reads `Spoils: ~1800 wood, ~1100 iron`.

Two separate defects stacked into one felt experience: the promised figure ignores BOTH the repeat-clear
multiplier and the bank cap, and the overflow is then BURNED - which contradicts the WO-1434 law that capped
yield is recoverable, not burned.

## 2. FIX SHAPE

- The deploy screen shows the amount that will ACTUALLY bank: cap-aware and repeat-clear-aware. If the cap
  bites, say so on the card rather than quoting a number the player will not receive.
- Spoils above the cap are RETAINED as pending on the same store WO-1434 made the popup read - not lost.
- Keep the `[Flow:Bank]` line; change `LOST` to the retained amount so the log stays honest.

## 3. WHAT NOT TO DO
- Do not raise the bank cap to make the number fit. The cap is the progression (memory `stockpiles-cap-capacity`).
- Do not add a second pending store.

## 4. ACCEPTANCE
- [ ] Deploy screen figure equals the banked+pending figure for a repeat clear against a full bank.
- [ ] Regression: full bank + 3-star repeat clear -> nothing burned, pending carries the remainder.
- [ ] `REGRESSION_OK n/n` on a fresh log; the deploy PNG opened.
