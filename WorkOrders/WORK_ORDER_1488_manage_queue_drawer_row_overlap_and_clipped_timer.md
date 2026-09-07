# WO-1488: Manage queue drawer - row 2 overlaps the gold frame, the timer clips mid-word, and the flow map FAILS

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate (was: IN PROGRESS - lane handed back edits 2026-09-06 (uncommitted, awaiting the wave-two compile + regression gate); prior: READY TO IMPLEMENT)
**Silo:** Manage 2000-block (WO-2012, global queue and context strip).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1488 -> 1489 in the same edit).

## 1. EVIDENCE

`Builds/ui-capture/ManageFlow_BUILD_queue_2670x1200.png` (18:39):

```
"11m 0s left (0% do..."       timer clipped mid-word
row 2                          overlaps the gold frame
X                              sits as a FOURTH TAB, not top-right
rows                           carry no thumbnails
```

And the flow map does not pass:

```
Builds/flowmap-r24.log (tail)
  MANAGE_FLOW_MAP_FAIL frames=14/16 ledger=2 fidelity=0 geometry=0 touch=0 inventory=4
```

while commit `949e848a0` claims "all nine screens match the owner's mockup - twenty-four capture rounds".
The claim and the marker disagree, and the marker is the evidence.

## 2. FIX SHAPE

- Derive the row band from the drawer PLATE rect, so row 2 cannot cross the gold frame at any row count.
- `FitSingleLine` on the timer string; if it still will not fit, shorten the authored format, not the plate.
- Move `X` to top-right, out of the tab strip.
- Add row thumbnails from the same art path the Build grid uses (one loader, per WO-1444's housekeeping item).
- A MEASURED case on the drawer: rows contained by the plate, timer not truncated, X outside the tab strip.

## 3. WHAT NOT TO DO
- Do not repeat the "all nine screens match" claim until `MANAGE_FLOW_MAP_FAIL` is gone from a fresh
  `flowmap` log. The marker is the authority, not the capture round count.

## 4. ACCEPTANCE
- [ ] `MANAGE_FLOW_MAP_OK` (or the pass marker) on a fresh `flowmap` log, pasted.
- [ ] Measured drawer case, RED proof stated.
- [ ] Fresh `ManageFlow_BUILD_queue` PNG opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.
