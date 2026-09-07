# WO-1477: the rumor board needs a PREVIOUS button

**Status:** READY TO IMPLEMENT
**Silo:** the rumor board panel (the WO-1192 redesign surface).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1477 -> 1478 in the same edit).

## 1. EVIDENCE

Owner validation note on the WO-1192 Pass, verbatim:

```
A previous button would be nice
```

The board today paginates forward only: NEXT advances and there is no way back, so a player who reads past a
rumor has to close and reopen the board to find it again.

## 2. FIX SHAPE

- Add a PREVIOUS face beside the existing NEXT, built through the kit `ButtonPack` so it inherits the board's
  chrome rather than being hand-drawn.
- Disable (not hide) PREVIOUS on the first page, so the control's position never shifts under the thumb.
- Measured case: both faces present, inside the board plate, above the touch floor, not overlapping the
  ACCEPT row (the WO-1189 defect on this same panel).

## 3. WHAT NOT TO DO
- Do not wrap around from the first page to the last; the owner asked for previous, not a carousel.

## 4. ACCEPTANCE
- [ ] PREVIOUS present via `ButtonPack`, disabled on page one.
- [ ] Measured layout case covering both faces; RED proof stated.
- [ ] A fresh rumor board capture opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.
