# WO-1477: the rumor board needs a PREVIOUS button

**Status:** DONE - already landed at HEAD (090be8066, 486cd7b17, suite 086ce14fd): RumorBoardVM.PrevPage + RumorBoardPanel.BuildPreviousButton + RumorBoardLayoutRegression Case5_Previous; verified at source 2026-09-06; owner frame owner-screen-20260906-201850.png shows PREVIOUS on device. Open ruling: PrevPage wraps (approved WO-1192 v3) vs this ticket asking no-wrap.
**Silo:** the rumor board panel (the WO-1192 redesign surface).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1477 -> 1478 in the same edit).

## 1. EVIDENCE

Owner validation note on the WO-1192 Pass, verbatim:

```
A previous button would be nice
```

**RESOLVED at source 2026-09-06 - see the Status line: PREVIOUS already ships.** The audit fleet reached the
same conclusion independently from `Logs/device/screens/owner-screen-20260906-201850.png` (build 358574,
20:18), which shows `PREVIOUS / NEXT / CLOSE` on Brom's Rumor Board. WO-1521 cites the same frame.
Only the wrap-vs-no-wrap ruling remains open.

The original premise, kept for the record: the board paginates forward only, so a player who reads past a
rumor has to close and reopen the board to find it again. That premise was wrong.

## 2. FIX SHAPE

- Add a PREVIOUS face beside the existing NEXT, built through the kit `ButtonPack` so it inherits the board's
  chrome rather than being hand-drawn.
- Disable (not hide) PREVIOUS on the first page, so the control's position never shifts under the thumb.
- Measured case: both faces present, inside the board plate, above the touch floor, not overlapping the
  ACCEPT row (the WO-1189 defect on this same panel).

## 3. WHAT NOT TO DO
- Do not wrap around from the first page to the last; the owner asked for previous, not a carousel.

## 4. ACCEPTANCE
- [ ] The RESULT states, with file:line, whether a PREVIOUS button already existed and whether it worked.
- [ ] PREVIOUS present via `ButtonPack`, disabled on page one (exactly one such button on the panel).
- [ ] Measured layout case covering both faces; RED proof stated.
- [ ] A fresh rumor board capture opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.
