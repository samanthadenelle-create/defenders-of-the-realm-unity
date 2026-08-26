# WORK ORDER 1228 - The TREASURE FOUND panel overlaps its own title, clips its list, and buries its footer

**Status:** READY TO IMPLEMENT
**Silo:** UI layout
**Origin:** Owner felt-test, Seeker build `2026.08.26.342290`, 2026-08-26. Owner verbatim:
*"needs cleaned up"*.

## PROOF

Device capture `tmp/test-123851.png`, 2670x1200. Three distinct collisions:

1. **`TREASURE FOUND` is drawn ON TOP of `The cache holds:`** — the title and the subtitle occupy
   the same band; both are legible only because one is gold and one is grey.
2. **`Spring Water x1` is CLIPPED** by the list container's bottom edge. The cache held five lines
   and the box fits four and a half.
3. **`Take` sits ON the footer line** — it renders as `First clear -- [Take] membered.`, the button
   covering the middle of "remembered".

## ⭐ LIKELY THE SAME ROOT AS WO-1083 — check this FIRST

WO-1083's implementer found that `ElarionUiKit.BuildObsidianPanel`'s **close-band reservation**
(`ElarionUiKit.cs:628-677`) raises FrameCore's body-zone floor from the frame-measured **0.075 to
~0.3525** on a landscape canvas. At 2670x1200 that leaves `layout.body` only ~876 px tall with its
bottom edge at screen y=770 of 1200 — which crushed every hero-select element into one band and
produced all five of that screen's overlaps **from one cause**.

This panel has the same signature: stacked text, an overflowing list, a CTA colliding with what is
beneath it. **Read WO-1083's RESULT and its `HeroStageWell` approach before designing anything** —
if it is the same cause, the fix is the same shape (anchor on the frame-measured body rect rather
than the reserved one), and the two screens should not diverge in how they solve it.

⚠ If it is NOT the reservation, say so explicitly with the measurement that rules it out. Do not
assume it because it is convenient.

## Required

- Title and subtitle in separate bands, neither overlapping.
- The list gets a real scroll or a height that fits its content; **a cache can hold more than five
  lines**, so a fixed height that happens to fit today is not a fix. State what happens at 10 lines.
- `Take` in an exclusive band no other element may enter, with the footer legible above it.

## Constraints

- **`MinTouchPx = 112`** on `Take`, and satisfying it may not create a new overlap — that is what
  broke the hero-select screen. ⛔ Do NOT name `ClampMinTouch` as a cause; ruled out at three sites.
- **ASCII-only TMP strings**; never meaning by colour alone (the owner is red/green colourblind —
  today the title and subtitle are separable only by hue, which is itself a defect here).
- **Code-built uGUI via `ElarionUiKit`. NO UXML** — project law.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. ⭐ **A DEVICE SCREENSHOT at 2670x1200 with a FIVE-LINE cache, opened and looked at**, plus one
   with a deliberately longer cache to prove the overflow behaviour. `UI_CAPTURE_OK` alone is not
   acceptance — two broken panels reached the owner behind green markers.
3. A greyscale check: title and subtitle must remain separable without colour.
4. The RESULT states whether the close-band reservation was the cause, with the measurement.
5. Owner felt-verifies and CLOSES.

## What NOT to touch

- ⛔ The loot roll or the table (`barrel-common`) — `[Flow:Loot] Chest_barrel opened -> dropped
  1 loot line(s)` proves the data path works. This is presentation only.
- ⛔ `BuildObsidianPanel`'s close-band reservation itself without reading WO-1083 first — other
  screens depend on it.
