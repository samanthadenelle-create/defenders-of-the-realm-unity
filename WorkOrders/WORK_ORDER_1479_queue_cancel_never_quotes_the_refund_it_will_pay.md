# WO-1479: CANCEL in the Obsidian queue never quotes the refund it is about to pay

**Status:** READY TO IMPLEMENT
**Silo:** `ManageScreenPanel` queue drawer + `ManageScreenVM`. Manage 2000-block adjacent.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1479 -> 1480 in the same edit).

## 1. EVIDENCE

```
Builds/ui-capture/ManageFlow_BUILD_queue_2670x1200.png   (18:39)   the face reads a bare "CANCEL"
BuildTimerService.cs:1770-1822                           refunds 100% of the paid basket
ManageScreenVM.cs:2558                                   `refunded` is ALREADY composed
```

So the model already knows the exact figure, the service already pays it in full (the WO-911 Q1 ruling), and
the player is asked to press CANCEL with no idea whether they get everything back or nothing. Players do not
press a destructive button that will not tell them the price.

## 2. FIX SHAPE

- The face reads the refund: `CANCEL - returns 240 wood, 120 iron`. Take the figure from the existing
  `refunded` on the VM; do not recompute it in the View.
- Where a pre-v37 job refunds zero (no paid basket recorded), say so on the face rather than showing an empty
  list.
- Run the face through `FitSingleLine`; it is a long string on a narrow row.

## 3. WHAT NOT TO DO
- Do not put the figure in a confirm dialog only. The information belongs where the decision is made.

## 4. ACCEPTANCE
- [ ] The CANCEL face quotes the refund, sourced from `ManageScreenVM.refunded`.
- [ ] A pre-v37 job shows the zero-refund wording.
- [ ] Fresh `ManageFlow_BUILD_queue` PNG opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.
