# WORK ORDER 1319 — Action-bar face labels overlap into an unreadable run at narrow aspect

**Status:** READY TO IMPLEMENT
**Silo:** HUD / Layout
**Minted:** 2026-09-02 (CLI) from an owner screenshot during a desktop web felt-test.
**Severity:** P2 — legibility, not function. The bar still works.

## Owner evidence

Screenshot, `echoes-of-elarion.vercel.app`, build `2026.09.02.352005`, narrow desktop browser window.
The bottom action bar renders its labels as one unbroken run:

```
BUILDTALKHERO...QUEUE MANAGE
```

Five separate face labels printed with no gap, overlapping each other.

## Why this is a real defect and not just "the window is too narrow"

The clipping of OTHER surfaces at this aspect is expected — the game's UI is authored for landscape
and this was a tall, narrow window. **The labels are different.** Text that runs into its neighbour
is a layout failure at ANY width: the correct degradation is to ellipsize, shrink to a floor, or drop
the label and keep the icon. Overlapping is the one outcome that is never right, and it means the
label widths are not constrained by their slot.

⚠ Do NOT "fix" this by declaring the aspect unsupported. A Pi Browser phone in portrait, before the
WO-1312 rotation engages or if its fail-safe fires, lands in exactly this shape.

## Where to look

`HudActionBarModel` / its View own the slot geometry. Canon facts, so they are not re-derived:
- `MaxVisibleFaces = 6` is a MAXIMUM, never the count. `ButtonCount` stays **7** (enum identity /
  array bound). The bar is normally FIVE faces in open town — `Talk` is added only while a talkable
  NPC is in range and `Raids` only when `RaidCapable`. **A five-face bar is the feature working.**
- `ActionBarButtonId.Map` is dormant at ordinal 4 — never renumber, the face arrays are ordinal-indexed.
- Touch targets: `MinTouchPx = 112`.

So the slot count is VARIABLE at runtime, which is very likely the mechanism: a width divided for six
faces, or labels sized for a fixed slot, will collide once the real count and the real width disagree.
**Establish that from the layout code before changing a number.**

## Acceptance criteria

1. At the owner's aspect, every face label is readable and none overlaps its neighbour.
2. The degradation is explicit and authored — ellipsis, a size floor, or icon-only below a width
   threshold. Not "it happens to fit now".
3. Correct at 5 faces AND at 6 (NPC in range). Prove both; the variable count is the suspected cause.
4. `MinTouchPx = 112` still honoured — do not shrink the tap target to make text fit.
5. Verified from a CAPTURE, not from reasoning. A screenshot is the primary evidence for a visual
   defect; `UI_CAPTURE_OK` proves pixels were written, not that they are legible — that marker went
   green over a panel carrying four visible defects on 2026-09-01.

## What NOT to touch

- ⛔ Do not renumber `ActionBarButtonId` or change `ButtonCount` (7). Both are load-bearing.
- ⛔ Do not "restore" a sixth always-on face to make the maths even. Five is correct in open town.
- ⛔ Do not carry meaning by colour. The owner is red/green colourblind.
- ⛔ ASCII-only strings — non-ASCII renders as tofu in TMP.
