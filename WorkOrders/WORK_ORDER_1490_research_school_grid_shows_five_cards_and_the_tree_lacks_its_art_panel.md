# WO-1490: the Research school grid shows 5 cards instead of 4 and leaves a 45% dead band; the tree has no art panel or RESEARCH button

**Status:** READY TO IMPLEMENT
**Silo:** Manage 2000-block (WO-2010, research schools).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1490 -> 1491 in the same edit).

## 1. EVIDENCE

```
Builds/ui-capture/ManageFlow_RESEARCH_gridtop_2670x1200.png
  five cards per row -> Lumber Mill ORPHANS to row 2; ~45% of the panel is dead band below
Builds/ui-capture/ManageFlow_RESEARCH_school
  two rows read "QUEUE FULL"; captions clipped ("Mana")
```

The mockup's panels 6 and 7 show a FOUR-card grid and, on the tree, a left art panel with a gold RESEARCH
button carrying the costs. Neither is present.

## 2. FIX SHAPE

- Four cards per row; card width derived from the plate, so the fifth cannot squeeze in and orphan the sixth.
- Reclaim the dead band: the grid grows to fill the plate rather than sitting in the top 55%.
- Add the left art panel and the gold RESEARCH button with costs, matching mockup panels 6 and 7.
- `FitSingleLine` on captions ("Mana" is a clipped word, not a short one).
- A MEASURED case: cards-per-row == 4, no orphan row, captions not truncated, dead band under a threshold.

## 3. WHAT NOT TO DO
- Do not shrink the cards to fit five. The mockup says four.

## 4. ACCEPTANCE
- [ ] Fresh `ManageFlow_RESEARCH_gridtop` and `_school` PNGs opened; four per row, no orphan, no clip.
- [ ] Art panel + RESEARCH button with costs present.
- [ ] Measured case, RED proof stated.
- [ ] `REGRESSION_OK n/n` on a fresh log.
