**Status:** READY — UI (design pass; hand to the UI seat, CLI implements after the design lands)

# WORK ORDER 1133 — Inventory screen: redesign the Bag, and justify the gear view or cut it

**Minted:** 2026-08-21 (CLI seat, banner bumped 1133 -> 1134 in the SAME edit)
**Assigned:** UI seat (Claude UI) for the DESIGN. CLI implements from the ratified design.
**Class:** DESIGN / redesign. Not a defect list — the screen works, it does not *serve*.
**Evidence:** `docs/ui-evidence/2026-08-21_inventory_weapons_seeker.png` — captured live off
the owner's Solana Seeker, Grom Ironhand, landscape 2670x1200.

> **THIS IS THE SCREEN YOU GET WHEN OPENING THE ARMOR / GEAR VIEW** (owner, 2026-08-21).
> That matters for the design: it is not only a bag listing, it is the surface a player
> reaches when they set out to answer *"what am I wearing, and should I change it?"* The
> capture happens to show the Weapons tab, but the entry intent is gear.

> **ALL BROKEN ELEMENTS FOLD INTO THIS TICKET** (owner ruling: *"broken folds into that
> other ticket"*). Do NOT mint separate defect tickets for the empty preview box, the
> clipped tab label, the sub-floor touch targets or the bleed-through. They are listed
> below as design inputs, and they are fixed BY the redesign — not patched around it and
> not tracked in parallel, which would produce two competing sources of truth for one
> screen.

---

## THE OWNER'S VERDICT (2026-08-21, verbal, this session)

> "there is no benefit to the gear view like it is, and opening isnt much better"

Read that twice before designing anything. She is not asking for the same screen with
tidier spacing. She is saying **the screen does not pay for the tap that opens it.** A
redesign that fixes the defects below and keeps the shape would still fail her test.

**The question this WO must answer: what does a player LEARN or DECIDE here that they
could not before opening it?** If the honest answer is "nothing", the right outcome may be
to fold inventory into another surface and delete this one. That is an allowed outcome and
should be proposed if the evidence supports it.

---

## WHAT IS ON SCREEN NOW (observed, not inferred)

Left: a gold hero card overlapping the panel's own ornate frame — an EMPTY dark preview
box above a `VIEW GEAR` button, then `Grom Ironhand / KNIGHT / LV 4`, then HP `123/175`,
mana `12/12`, and an unlabelled purple XP bar.
Centre-top: `INVENTORY` title, then five tabs — Weapons (selected), Armor, Trinkets,
Potions, Skills.
Centre: a ~5x5 item grid holding TWO tiles, each with a tiny letter (`U`, `C`).
Below: a full-width gold hint bar, `Tap an item to inspect it.`
Bottom-right: currency strip — `1230` gold, `291` crystals, `0` (flask).
Bottom-centre: `Close`.

---

## THE CONCRETE PROBLEMS (each is visible in the capture)

1. **~40% of the content area is dead black.** The grid sits right-of-centre with a void to
   its left. Two items float in a five-column grid on a 2670px-wide screen.
2. **The selected-tab treatment eats its own label.** The orange chevron on `Weapons`
   overlaps the word — the trailing letters are obscured. The selected state is destroying
   the thing it selects.
3. **The hero preview box is EMPTY.** A flat dark rectangle where a hero should be. This is
   not a mystery: WO-592 (embedded orbit-rotatable hero viewport) was never built, and F8
   seq 2833 recorded `[Flow:Equip] RT PROBE: the preview render texture is a UNIFORM clear
   colour`. **An empty box is worse than no box** — it reads as broken, and it is the single
   biggest reason the gear view "has no benefit".
4. **Item tiles are far below the touch floor.** Tiny tiles with 1-character rarity letters
   (`U`, `C`) that are near-illegible at arm's length. Canon floor is `MinTouchPx = 112`.
5. **The hint bar outweighs the content.** A full-width gold band saying "Tap an item to
   inspect it" is visually louder than the two items it describes.
6. **The currency strip is orphaned** bottom-right, colliding with the frame and unrelated
   to anything else on the panel.
7. **The hero card breaks the frame**, overlapping the ornate border rather than sitting
   inside the layout.
8. **Background HUD bleeds through** — `2/6` (Echoes) is visible at the right edge behind
   the panel.
9. **Frame vs content mismatch** — heavy ornate grey chrome around flat black content; it
   does not read as the Obsidian kit used elsewhere.

---

## HARD CONSTRAINTS (non-negotiable — a design that breaks these cannot ship)

- **UXML DOES NOT WORK IN PLAYER BUILDS.** Code-built uGUI only. Do not design anything that
  implies a UIDocument.
- **The owner is RED/GREEN COLOURBLIND.** Rarity, state and quality must NEVER be carried by
  colour alone — pair with shape, glyph, border weight or a word. `U`/`C` letters are the
  right instinct; they are just too small.
- **`MinTouchPx = 112`** for every interactive element.
- **Landscape.** The build is landscape-locked (`allowedAutorotateToPortrait: 0`).
- **TMP strings ASCII-only** — non-ASCII renders as tofu.
- Player-facing sentences come from `canon-strings.json` (CLAUDE.md §7), never typed inline.
- Bag reaches this screen from the SIX-face action bar (Build, Talk, Bag, Raids, Quests,
  Manage). Do not propose a seventh face.
- ⚠ There is a **feature-flagged-off Map tab** inside Bag (`FeatureFlags.MapTab`), OFF
  because realm travel is a WO-827 stub. Any tab-row redesign must not assume it away, and
  must not turn it on.

---

## WHAT THE DESIGN MUST DELIVER

1. **A stated purpose.** One sentence: what this screen is FOR. Everything else justifies
   itself against that sentence.
2. **The gear-view decision, argued.** Either:
   (a) make the hero preview genuinely useful — see the equipped set at a glance, compare a
       candidate against what is worn, understand what a swap changes; or
   (b) **cut it** and say what replaces the value it was supposed to add.
   ⛔ Do not specify a live 3D orbit viewport without pricing it: `SeatingEditorOverlay`
   already exists at 745 lines with no such viewer, and the RT probe above shows the render
   texture path is currently blank. If the design needs a rendered hero, it must name how
   that render is produced and why it will not be an empty box again.
3. **A layout that uses the screen.** Landscape, no dead 40%.
4. **Comparison, not just enumeration.** The player's real question at a weapon tile is
   *"is this better than what I have?"* — a grid of icons cannot answer it. Stat deltas
   against the equipped item are the highest-value thing this screen could add, and the
   repo already carries the pattern (`PartyShopPanelMvvm` preview pane, WO-486/501).
5. **Empty-state design.** Two items in twenty-five slots is the NORMAL early-game case, not
   an edge case. Design for a nearly-empty bag deliberately.
6. **Rarity that survives greyscale.** `U`/`C` at a legible size, or shape/border.
7. Mockups or precise geometry the CLI can build without inventing values.

---

## OUT OF SCOPE

- The equip/stat SYSTEM. This is presentation. If the design needs a stat the model does not
  expose, name it and it becomes a separate CLI ticket.
- `WO-1050` (The Night Market — Realm Store presentation) is the sibling storefront redesign;
  keep the two visually coherent but do not merge them.
- The Skills tab's talent tree (WO-1021 shipped its parity pass).

## ACCEPTANCE

The owner opens Bag on the Seeker and can answer, without being told:
**"what do I have, what is worn, and is this one better?"** — and the screen has earned the
tap that opened it.

Captures required before it is called done (greyscale pass included): the near-empty bag,
a bag with 20+ items, and each tab.

---

## RELATED FINDING, recorded here so it is not lost

**No NPC at the armorer.** The owner also reported *"no npc for armor"* on the same
session. Checked at source: `structures-catalog.json` gives **every** vendor storefront
`npcModel: None` — market, lumbermill, forge, armorer, jeweler, collector_* alike, so the
armorer is not specially broken. But `CastleVendorNpcInjector.cs:114` DOES map it:

```
case "armorer": return new Vendor { BodyRes = BodySmith, StructureId = "armorer",
                                    Label = "Armorer", Arch = Archetype.Blacksmith };
```

So the mapping exists and the body is named, yet no NPC appears. That is a RUNTIME
question (does the injector run in this hub scene? does `BodySmith` resolve? was the
armorer built?) and it is **not a design question** — so it is NOT part of this WO's
design work. It is recorded here because the two were reported together and because a
vendor with no vendor is part of why the gear surface feels unstaffed. CLI to instrument
and settle it separately, per §12 — no fix until a captured line names the cause.

