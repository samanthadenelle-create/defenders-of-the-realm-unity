# WORK ORDER 1195 - a resource is named by its ICON, never by a letter

**Status:** READY TO IMPLEMENT (one design decision inside it, see section 4)
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1195 -> 1196 in the same edit)
**Silo:** UI / consistency
**Origin:** owner, 2026-08-25.

---

> *"The same thing with the one in builder's mode on the bottom - it doesn't give you a chip, it
> gives you a letter, which I've always hated. If we're gonna use a chip, use a chip everywhere.
> There needs to be the consistency you would expect. In builder's mode it just says WIS. It should
> be the chip in all of those - even in the ones talking about the price of things. When you're
> talking about building an item I don't want to see `30W 140I 10C`. Looks like I'm reading a
> formula. I'd like to see a little wood symbol, 140."*

## The law

⭐ **Wherever a quantity of a resource is shown to the player, the resource is identified by its
ICON. A single-letter abbreviation is never acceptable.**

The player should read *[wood icon] 140*, never `140W`, never `WIS`, never `30W 140I 10C`.

## Confirmed at source - and it is the SAME FORMATTER WRITTEN THREE TIMES

| Site | Shape |
|---|---|
| `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:1618-1621` | `CompactNumber(c.wood) + "W"`, `+ "F"`, `+ "I"`, `+ "C"` |
| `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs:345-347` | `c.wood + "W"`, `+ "F"`, `+ "I"` - and it does NOT use `CompactNumber` |
| `Assets/_Modules/Village/Hero/BarracksPanelVM.cs:406` | `c.Wood + "W"` ... |

⚠ Note the second one already drifted from the first: it skips the kit's compact formatter, so a
five-digit cost renders differently in two places today. **One fact written three times, and the
copies have already diverged** - this repo's dominant failure mode, showing up in the exact code the
owner is complaining about.

⛔ **So the fix is ONE formatter, not three edits.** Every site routes through it. A fourth caller
must be unable to reinvent the letter form.

⚠ **"WIS" in the build-mode bottom bar is NOT located yet.** The owner names it explicitly. Find it
before implementing and add it to the table above - it may be a separate letter-strip rather than
this formatter.

## The engineering question this raises (section 4 - decide it, and say which you chose)

An icon beside a number inside a single text label means either:

- **(a) a TMP inline sprite** - `<sprite=...>` in the string, requiring a TMP sprite asset built from
  the same art the chips use; or
- **(b) built layout** - an `Image` plus a text element per resource, laid out as a row.

⭐ **Whichever is chosen, the ICON MUST RESOLVE THROUGH THE EXISTING DATA PATH.** The chips already
resolve their icons through the **CurrencyChip concept resolver from `concept-icons.json`**
(`gold/wood/iron/food/crystal` -> `Icons_Obsidian`). ⛔ Do NOT hardcode a sprite reference and do NOT
introduce a second icon registry - the icon choice is DATA and there is exactly one source for it.

⚠ (a) is cheaper at every call site but adds a sprite-asset build step and interacts with font
fallback; (b) is more code per site but reuses what already renders. **State the trade-off and the
choice in your report.**

## Constraints

- ⛔ **ASCII-only strings.** A `<sprite=N>` tag is ASCII and legal; a literal emoji or a non-ASCII
  glyph is NOT - it renders as a tofu box on device.
- ⛔ **Never meaning by colour alone** - the owner is red/green colourblind. The icon is the identity;
  it must be distinguishable by SHAPE, not by tint. ⚠ If wood/stone/iron icons differ mainly by
  colour, say so - that is a finding, not something to work around.
- ⛔ A cost that cannot render its icon must degrade to the **full word** (`Wood 140`), never back to
  the letter. State the fallback.
- Any tappable element stays at or above `ElarionUiKit.MinTouchPx` (112). ⚠ Most of these are LABELS,
  not controls - do not inflate a label to a touch target.

## ⚠ One canon conflict to surface, not to silently override

`RULES.md` QR-5.11 uses **`NEED 80W 30I`** as its worked example of "give every state a word + shape,
never a colour alone." That example is now superseded on the letter form - the principle (never
colour alone) stands, the abbreviation does not. ⛔ Fix that example in the same change, or a future
seat will cite canon against this ruling.

## Acceptance criteria

1. No player-facing surface renders a resource quantity with a single-letter suffix. Prove it with a
   repo-wide search for the pattern, and quote the search.
2. All cost/price surfaces route through ONE formatter. A new caller cannot produce the letter form.
3. The two formatters that already diverged (compact vs non-compact) are reconciled - one behaviour.
4. An oracle pins it: a resource quantity is rendered with an icon (or the full word fallback), never
   a letter. ⛔ Register it in `DataRegression.cs` - an unregistered oracle never runs.
   ⭐ It must go RED if someone re-adds `+ "W"` at any call site. State what makes it fail.
5. Icons resolve through `concept-icons.json`; no second registry, no hardcoded sprite.

## Related, do not conflate

**WO-1194 Part 2** is the ambient resource readout (three thin lines, current-of-capacity, a Harvest
button). This ticket is the *naming convention everywhere else* - costs, prices, build cards, barracks
training. They share the icon resolver and should agree, but they are different surfaces.
⚠ **WO-1163 retires food for stone.** Do not hardcode a four-resource list in the new formatter -
enumerate, so the set can change without touching every call site again.
