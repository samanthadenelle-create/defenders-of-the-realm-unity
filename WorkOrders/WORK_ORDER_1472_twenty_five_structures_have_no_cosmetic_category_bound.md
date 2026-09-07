# WO-1472: 25 structures have no cosmetic category bound, including the Archer Tower and the hero

**Status:** READY TO IMPLEMENT
**Silo:** `Assets/_Modules/Cosmetics/` + the structure catalog category bindings.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1472 -> 1473 in the same edit).

## 1. EVIDENCE

Device log, 104 lines across 25 DISTINCT names, the Archer Tower alone 24 times:

```
Refresh: 'Archer Tower' has no category bound - nothing to resolve.
```

The hero is in the same list. So a quarter of the cosmetic surface silently resolves to nothing, and the only
signal is a log line repeated per refresh - which is also feeding the ring-buffer eviction problem (WO-1450).

## 2. FIX SHAPE

- Bind a cosmetic category for each of the 25 names in the catalog. The 25 names in the log ARE the checklist;
  paste them into the RESULT as the worklist.
- Where a structure genuinely has no cosmetic axis, bind it to an explicit `none` category rather than leaving
  the field empty, so absent and deliberate are distinguishable.
- Make the message `FlowTrace.Once` per name so a residual gap logs once, not once per refresh.

## 3. WHAT NOT TO DO
- Do not silence the message without binding the categories; the log line is the only detector.

## 4. ACCEPTANCE
- [ ] Zero `has no category bound` lines in a full town + raid session, or every remaining one is an explicit
      `none` binding named in the RESULT.
- [ ] Regression: every catalog structure id resolves a category (explicit `none` counts).
- [ ] `REGRESSION_OK n/n` on a fresh log.
