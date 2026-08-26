# WORK ORDER 1205 - The resource rows read as chip + count, and the icon stops sitting on the number

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1205 -> 1206 in the same edit)
**Silo:** HUD / presentation
**Ruled by:** the owner, 2026-08-25, felt-testing build `2026.08.25.341262` on the Seeker.

---

## Owner ruling, verbatim

> "recourse we should remove the /2000"
> "just the count and not the wood name just the chip"

Target row: **[icon] 80**. No cap text, no word label.

## Proving evidence - a captured device screen, not a theory

`tmp/wo970/staff-drawn-190533.png` (Seeker, 2670x1200, 19:05:35) and the crop
`tmp/wo970/crop-resources.png`. Read off the pixels, three separate facts:

1. Every row renders `<name>` then a plate reading `<icon><n> of 2000` - the cap text the owner
   is retiring.
2. **The icon is drawn ON TOP OF the digits.** Wood's log sits over its `0`; Iron's ingot sits
   over its `0`; **Stone's icon is almost entirely occluded by the `80`** - only a few green and
   orange pixels survive at the glyph edges. This is a real layout defect and it is in scope: the
   row cannot be "chip + count" while the chip is underneath the count.
3. Stone is present and Food is gone, so WO-1163's conversion is visible here and must not regress.

## The seams - exact, verified at source

| What | Where |
|---|---|
| The `" of "` cap string | `Assets/_Modules/HUD/Kit/HudKitController.cs:1931` in `SetCappedResourceValue` |
| The name label ("Wood"/"Iron"/"Stone") | same file, ~`:1633-1640`, built by `ElarionUiKit.Label` into the row's left sub-rect `0.02f -> 0.44f` |
| The chip that overlaps | same file, ~`:1645-1648`, `ElarionUiKit.CurrencyChip(row.transform, kinds[i], new Vector2(0.46f, 0f), new Vector2(1f, 1f), primary: false, tag: names[i])` |
| The row geometry constants | `ResRowHeightPx`, `ResRowGapPx`, `ResPanelPadPx`, `RailPanelWidthPx` |

## ⛔ THE CONFLICT THIS TICKET MUST NOT SILENTLY RESOLVE

The name label is **not decoration**. Its own comment at `:1633-1635` calls it *"the colourblind-safe
identity carrier ... so the icon-first rule inside CurrencyChip cannot drop it"*, and the block above
it records why: **if the icon art fails to resolve, an icon-only row is unidentifiable**, which is a
straight breach of the standing colourblind rule (the owner is red/green colourblind; identity is
never carried by hue alone).

The owner has ruled the name off the row. That ruling is implemented **as written**, and the guard is
**re-pointed, not deleted** - the WO-1159 lesson: when a ruling moves, the oracle moves with it and
gets stricter, never softer.

**The ruled shape:**
- Icon resolves -> **[icon] 80**, name absent. Exactly what was asked for.
- Icon does **NOT** resolve -> the name label **automatically returns** as the fallback identity, in
  its own sub-rect. A naked number with no identity must never ship.

That fallback is a mechanism, not a second opinion about the ruling. If the owner wants the row naked
even when the art is missing, she says so and the fallback comes out.

## What to build

1. **Drop the cap text.** `SetCappedResourceValue` writes the count only. Keep the `TownBankCapacity`
   read - it still gates whether the row is a capped resource at all - and keep `CompactNumber`.
2. **Drop the name label** on the ruled path, and widen the chip's sub-rect to the full row now that
   nothing sits to its left.
3. **Separate the icon from the digits.** The chip's icon gets its own left sub-rect inside the row;
   the amount starts to its right. No overlap at any resolution in the capture set.
4. **Wire the no-art fallback** described above.

## Acceptance criteria

- A fresh headless capture at the device resolution shows every row as `[icon] <count>` with the
  icon fully visible and no glyph overlap - **and the PNGs are opened, not merely produced**
  (`UI_CAPTURE_OK` proves a panel rendered, never that it looks right).
- `" of "` appears nowhere in the resource rows.
- With the icon art forced unresolvable, each row still names its resource.
- Greyscale check: Wood, Iron and Stone remain distinguishable by icon SHAPE.
- `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs, judged by marker, never exit code.

## What NOT to touch

- ⛔ **The cap itself.** Capacity still exists and still bites; only its display on this row is
  retired. WO-1191's collect toasts are the surviving voice of the cap - at-cap keeps its loss
  language, over-cap keeps its non-loss wording. Do not edit either.
- ⛔ The Gold collapsed chip (`_resGoldOnly`) and the WO-697 icon-first rule it carries.
- ⛔ The owner's 2026-07-15 uniformity ruling at `:1641-1645`: in this strip every chip is a peer -
  same size, same colour, `primary: false`. Identity comes from the icon, never from colour or size.
- ⛔ Anything Stone-related from WO-1163. It is closed and felt-verified.

## Open question for the owner (do not answer it in code)

With the cap text gone from the row, **at-cap and over-cap are no longer visible at a glance** - the
only signal becomes the toast at collect time. If she wants a persistent tell (the chip changing
state at cap), that is a separate ruling and a separate ticket.
