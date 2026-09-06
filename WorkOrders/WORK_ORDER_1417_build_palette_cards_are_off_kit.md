# WO-1417: the build palette's item cards are off-kit - flat navy boxes inside a medieval frame

**Status:** FIXED - ON THE SEEKER in build 2026.09.06.357453 (chain 00:31-00:38: APK_OK 463MB, R2_PARITY_OK objects=271; installed 00:41, versionCode 357453 read off dumpsys; Firebase App Distribution release 0kka4h6t9u400); owner felt-test closes 2026-09-05 21:45 - kit cards landed + pinned, COMPILE_GATE_OK + REGRESSION_OK 385/385, headless capture opened (RESULT file); device build tonight, owner felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the owner's live screen on build 2026.09.05.356468)*

## Owner, verbatim (2026-09-05 10:3x)
> "also the X below seems unpolished coma=pared to rest of UI"

⚠ The live capture taken seconds later shows the STORAGE palette, and no X control is on that frame.
The unpolished surface below the title on this screen is the CARD ROW, and that is what this ticket
covers. **If the owner meant a literal X (close) glyph on another screen, that is a second ticket -
ask which screen before assuming this one.** Do not fold two defects into one fix.

## Evidence
`logs/f8-inbox/device/live-x-button.png` (device, 2026-09-05 10:34, Build > Storage):

The panel frame, `BACK`, the three `PLACE` buttons and `CLOSE` are all kit-styled - obsidian
plates, gold medieval bezel. Sitting inside that frame are **three flat navy-blue rectangles** with
plain left-aligned body text and no bezel, no plate, no corner treatment:

```
   Lumberyard          Stoneyard            Foundry
   [thumbnail]         [thumbnail]          [thumbnail]
   Raises how much     Raises how much      Raises how much
   Wood your town      Stone your town      Iron your town
   can hold.           can hold.            can hold.

   COST: NO COST       COST: NO COST        COST: NO COST
   [READY] AVAILABLE   [READY] AVAILABLE    [READY] AVAILABLE
```

Three separate defects in one row:
1. **The card is not a kit surface.** A flat `#1e2a44`-ish rectangle against `ElarionUi` obsidian +
   gold. It is the only element on the screen that does not belong to the kit.
2. **`COST: NO COST`** - a label whose value contradicts its own key, and it is very likely WRONG:
   WO-947 rules regular structures cost wood+iron, and WO-1108b prices the storage ladder
   (1k/2k/4k/8k/16k/32k, costs doubling per step). A container that costs nothing needs proving or
   fixing. **Read the catalog before touching the copy** - if the first container really is free by
   the founding grace, the card should say so in words ("First one is free"), not `NO COST`.
3. **`[READY] AVAILABLE`** - a bracket glyph plus a synonym. One state word, no brackets.

## Fix shape
- The card becomes a kit surface (the same obsidian plate + bezel the Manage rows and the deck cards
  use). No new primitive - `ElarionUiKit` already has the plate the rest of the screen is drawn with.
- Cost line: the real basket, or a plain sentence when it is genuinely free. Never `COST: NO COST`.
- State line: one word, no brackets, no synonym pair.
- Nothing about layout, order or membership changes (WO-1082 owns the palette's order and says so).

## Acceptance
- [ ] Headless capture of the Storage palette at 2670x1200: the cards read as the same material as
      the frame; no bracket glyphs; the cost line names a real basket or a real sentence.
- [ ] RED-first pin: the card builder calls the kit plate (source lint) and no palette string
      contains `[` or the literal `NO COST`; name the mutation.
- [ ] Owner felt-test on the device.

## Not in scope
The palette's order/membership (WO-1082), the storage ladder's numbers (WO-1108b), and whatever
"the X below" turns out to be if it is a different screen.
