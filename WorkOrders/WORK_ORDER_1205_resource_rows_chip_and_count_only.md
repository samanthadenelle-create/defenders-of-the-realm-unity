# WORK ORDER 1205 - The resource rows read as chip + count, and the icon stops sitting on the number

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 303/303 suites` (Builds/w3-c, Builds/w3-r). AWAITING OWNER FELT-VERIFY to close.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1205 -> 1206 in the same edit)
**Silo:** HUD / presentation
**Ruled by:** the owner, 2026-08-25, felt-testing build `2026.08.25.341262` on the Seeker.

---

## ⛔ SCOPE FENCE - TWO resource surfaces exist and only ONE is in this ticket

Owner, 2026-08-25: *"the ones on build screen are correct just should be Stone not food and in bottom
minial is perfect"* and *"but the resources on HUD with the /2000 i hate that"*.

| surface | files | verdict |
|---|---|---|
| **Build-screen strip** (`W 8 \| I 0 \| S 130 \| C 550 \| G 1053`) | `Village/BuildMode/BuildWalletRow.cs`, `LiveWalletSource.cs` | ⛔ **RULED CORRECT - DO NOT TOUCH.** "in bottom minial is perfect". Its only defect was the letter F for retired Food, fixed by the lead (binds `stone`/`S`). Do not convert it to icon chips, restyle or re-space it. |
| **Town HUD resource rail** (the rows carrying "of 2000") | `HUD/Kit/HudKitController.cs` | ✅ **THE WHOLE OF THIS TICKET.** "I hate that." |

An unrequested change to the build strip is a regression to something the owner likes.

## ⛔ AMENDED BY THE OWNER 2026-08-25, MID-IMPLEMENTATION - the SHAPE changes, not just the contents

> "fix the ui issue. I prefered the other way it was, only should gold till clicked then showed all"
> "that was much more astetic"

The always-expanded three-row panel (Wood / Iron / Stone stacked under the rail) is NOT the wanted
shape. **COLLAPSED IS THE RESTING STATE: the GOLD chip alone, and the full set appears only on TAP.**

⭐ **The mechanism already exists - do not build a second one.** `HudKitController` already constructs
`_resGoldOnly` as *"Collapsed variant (calm(explore)): gold chip only; TAP expands the row for 6s"*,
with `_resExpandedRow` / `_resChips` as the expansion. Today the file runs `_resPanelOpen = true;`
and `_resExpandedRow.SetActive(true);` unconditionally, which is the likely reason the panel is
always open. Restore the collapsed default through that existing seam.

The chip+count rulings below still apply **to the expanded rows when they are shown**: no cap text,
no word label, no icon-over-digits, and the no-art name fallback stays.

⚠ TWO OWNER CALLS NOT ANSWERED IN CODE: whether the expansion auto-collapses after 6 s or stays
until tapped again, and whether the collapsed chip shows Gold ONLY or Gold plus a hidden-count. Ship
the existing 6 s behaviour and surface both questions rather than inventing an answer.

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


---

## UI SEAT DELIVERABLE (2026-08-26) - APPROVED ROW DESIGN + CROSS-RULINGS

**Mockup:** `WorkOrders/WORK_ORDER_1221_resource_rail_mockup_2670x1200.png` (shared with WO-1221).

- Row shape confirmed as ruled: `[icon] count` - name label dropped on this path, `of 2000` cap
  text dropped. Uniform chip size/colour every row (2026-07-15 uniformity ruling preserved).
- Icon-first identity by SILHOUETTE: coin / log / ingot / rock / crystal must stay separable in
  the greyscale check of the acceptance capture.
- **Open rulings (a) and (b) of this WO are now CLOSED by the owner (2026-08-26, explicit
  choice via the UI seat):** (a) expansion is a TOGGLE (tap to open, tap to close - the 6 s
  auto-collapse is ruled out); (b) collapsed = Gold + a small `+4` hidden-count hint tag.
  Recorded in full in WO-1221's addendum - the two WOs land on ONE surface and must not diverge.
- Ruling (c) (an at-cap/over-cap persistent tell once cap text is gone) stays a separate ticket.
