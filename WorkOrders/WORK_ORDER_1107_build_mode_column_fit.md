# WORK ORDER 1107 — Build mode's right edge did not fit the Seeker, and never had

**Status:** DONE 2026-08-16 (`8e7ce0090`) — gate green (`Builds/data-regression-wave14.log`, 183/187, 4 known reds, none in this lane); RESULT filed; pending PO felt-verify — banner bumped 1107 -> 1108
in the same edit (⚠ and 1106's missed bump reconciled there; see §5).
**Lane:** Build-mode layout (`BuildHudController`, `BuildPaletteUI`). Source tagged `COLUMN-FIT 2026-08-16`.
**Provenance:** owner F8 seq 2503 (*"the done should match same style and stack above defense and town
button"*) → the restyle lane measured the column while seating Done and found it over-subscribed →
owner approved the fix verbatim: *"Get it done. Do it that way. That's fine with me."*

---

## 1. The measurement that started it

The right-edge column claims, in 1920x1080 reference pixels:

```
114 (resource strip + gap) + 384 (D14 verb rail) + 9 + 428 (quick-tab stack)
  + 9 + 112 (Done) + 24 (top inset)                                  = 1080 exactly
```

But the canvas is **not** 1080 tall on the device. At the Seeker's 2670x1200 the scale factor is
`sqrt(1.390625 x 1.111111) = 1.24304`, giving a canvas of **2148 x 965.4** reference px — the same
965.4 `EndStateVM.cs:592` already records. **The column overflowed by ~115 px.**

⚠ **This is PRE-EXISTING, not introduced by the restyle.** The old 76px corner Done already sat in
the tabs' band at that aspect (x 52..128 vs the tabs' 72..332, y 829..941). The restyle widened the
overlap to the full box and, for the first time, *instrumented* it.

## 2. The fix (both halves were required)

1. **D14 verb rail laid out HORIZONTALLY** above the resource strip — the band constants swap axis
   (`RailBandW` 132 -> 384, `RailBandH` 384 -> 132); verb offsets `(0,±step)` -> `(∓step,0)`; reading
   order [OK][Rot][X] preserved so cancel stays two slots from confirm. All three keep the 112px
   MinTouch floor. Row owns y 114..246.
2. **Quick-tab height 132 -> 112** (`ElarionUiKit.MinTouchPx`), which also makes a tab literally the
   same 260x112 box as Done — what the WO-1035 comment already wanted.

⚠ **MEASURED, AND IT CONTRADICTS THE ORIGINAL PLAN: the rail move ALONE does not close the gap.**
With the rail horizontal but tabs still 132, the binding tenant becomes the **carousel dock**
(98..401 in the PICK phase), forcing the stack bottom to 410 and Done's top to 959 — still 17.6px
over the 941.4 line, so the clamp would keep firing. Both changes together, or neither.

**Result:** required 923 vs available 941.4 = **42.4 px headroom**; the Done clamp never fires at
2670x1200. Verified disjoint at 2670x1200 and 1080x2340. The dock (98..401) and the verb row
(114..246) overlap in Y but never in TIME — `BuildModeController` collapses the palette before
entering Placing (`:2202`, `:2271`, gate at `:662`).

## 3. Also fixed while in there

- **A hand-copied constant deleted.** `BuildHudController` carried its own `QuickTabStackTopPx = 935f`
  duplicating a number `BuildPaletteUI` owns; it now reads the palette's published value. Duplicated
  geometry is how the two files disagreed about the same column in the first place.
- **The first-run hint** (860px, centred, a PLACING sibling) would print through the horizontal row on
  a narrow canvas; it now seats BESIDE the row when the canvas is wide enough and LIFTS above it
  otherwise, traced either way.
- **Stale comments corrected**, including `BuildPaletteUI`'s D21 band-math block, which reasoned
  against an assumed 1080-tall canvas — the very assumption that hid this defect. It now carries an
  explicit "the canvas is NOT 1080 tall; never seat off 1080" warning.
- **One trace now answers the whole question**: `Build()` prints the measured canvas height, the
  required column sum and the headroom, so a capture says "does it fit on THIS device" without
  re-deriving from three files.

## 4. ⚠ Known remaining overflow — recorded, not hidden

**Ultrawide desktop 21:9 still overflows**: 2560x1080 -> canvas 935.3 (short by ~11.7px) and
3440x1440 -> 931.7 (~15px). The clamp fires there and Done sits slightly into the top tab. Neither is
a shipping target (the product is mobile-first; desktop is the dev proxy). Stated plainly rather than
left to be rediscovered.

## 5. ⚠ Process note — the banner rule broke, and it was this seat

WO-1106 was minted as a FILE with **no banner bump**, hours after this same seat wrote the note
naming mint-without-bump as the cause of five collisions in one day. No collision resulted because
only one seat is minting today, which is luck, not process. Both numbers are reconciled in the banner
in one edit. **The bump rides the same edit as the mint — including when the seat is in a hurry.**

## 6. Acceptance

- Build mode at 2670x1200: resource strip, horizontal verb row, quick-tab stack and Done are all
  visible and mutually disjoint; the `COLUMN-FIT` trace reports positive headroom and `clamp NOT needed`.
- Confirm / rotate / cancel behave exactly as before (positions moved, semantics unchanged).
- Owner felt-verify on device — this is a layout change, and headless captures cannot judge it
  (batchmode has no GameView; a resolution in a PNG filename is a label, not a layout).
