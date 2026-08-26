# WORK ORDER 1228 - The TREASURE FOUND panel overlaps its own title, clips its list, and buries its footer

**Status:** FIXED 2026-08-26 - `COMPILE_GATE_OK` + `REGRESSION_OK 292/292`; post-fix APK visual/greyscale verification queued
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


---

## UI SEAT DELIVERABLE (2026-08-26) - APPROVED LAYOUT SPEC + MOCKUP + OVERFLOW RULING

**Owner approved the design this session ("go").**
**Mockup (diff target for the acceptance screenshot):**
`WorkOrders/WORK_ORDER_1228_mockup_2670x1200.png` (also `tmp/treasure_mockup_2670x1200.png`).

Modal footprint on the 2670x1200 screen: x 0.210-0.790, y 0.167-0.833 (screen fractions,
y BOTTOM->top). Bands below are fractions OF THE MODAL rect (y bottom->top), five exclusive
bands - no element may enter another band:

| Band                          | xMin  | yMin  | xMax  | yMax  | notes |
|-------------------------------|-------|-------|-------|-------|-------|
| 1. Title "TREASURE FOUND"     | 0.10  | 0.888 | 0.90  | 0.975 | gold, alone |
| 2. Subtitle "The cache holds:"| 0.10  | 0.813 | 0.90  | 0.875 | parchment, own band |
| 3. Loot list well (inset)     | 0.039 | 0.298 | 0.961 | 0.788 | lines at 64px pitch @1200 |
| 4. First-clear note           | 0.10  | 0.213 | 0.90  | 0.275 | full sentence, never overlaid |
| 5. Take CTA                   | 0.339 | 0.035 | 0.661 | 0.190 | 500x124px @2670x1200, single exit |

**OVERFLOW RULING (design lane, owner-approved):** the list well is FIXED HEIGHT showing up to
SIX lines; at 7+ lines it becomes a kit scroll INSIDE the same well. The modal footprint never
grows and Take never moves. At 10 lines: 6 visible, scrollbar, zero clipping. Do NOT grow the
panel with the roll - growth is how this defect class re-appears.

Greyscale rule: title vs subtitle separable by SIZE + WEIGHT (52px bold gold vs 34px regular
parchment), not hue alone. The RESULT must still state, with the measurement, whether the
close-band reservation was the cause (required by Acceptance 4 above).

## MOCKUP REVIEWED 2026-08-26 (CLI) - APPROVED as the layout direction, with ONE open item

`WorkOrders/WORK_ORDER_1228_mockup_2670x1200.png`, 2670x1200. All three reported collisions are
resolved:

1. `TREASURE FOUND` sits in its own band ABOVE `The cache holds:` - no overlap.
2. All five cache lines sit inside the list container with vertical headroom - nothing clipped.
3. `Take` occupies an exclusive band BELOW `First clear -- this cache is remembered.` - the footer
   is fully legible.

**Greyscale: PASSES.** The title separates from the subtitle by SIZE and WEIGHT, not by gold-vs-grey,
so it survives without colour. That was acceptance criterion 3 and it is met by construction.

### ⚠ OPEN - the overflow case is still unanswered

The ticket asked: *"a cache can hold more than five lines, so a fixed height that happens to fit today
is not a fix. State what happens at 10 lines."* **The mockup answers for five.** There is no scroll
affordance or truncation rule shown.

**Resolve it CONSISTENTLY WITH WO-1230**, which solved the identical problem with a
`+ N more (scroll)` line under its list. Two reward/list panels inventing two different overflow
conventions is the divergence this ticket and 1230 were explicitly written to prevent. Either adopt
that affordance here or state why this panel differs.

**Implementation may proceed on everything else.** The overflow rule is the only item that must be
settled before the list container's height is authored, since it determines whether the height is
fixed or content-driven.
## LANDED-WORK AUDIT (2026-08-26)

The panel implementation and oracle landed in `b303c4fbf`. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83583` `DUNGEON TREASURE OK` proves five exclusive bands,
six-then-scroll overflow, and a 3/3-collision historical RED control; `:83814` is
`REGRESSION_OK 291/291`. Source inspection established this panel does not consume `chrome.layout`,
so the close-band reservation was not the cause. **Post-FIXED APK checklist:** five-line and deliberately longer
2670x1200 device captures opened and inspected, greyscale check, and owner close.
