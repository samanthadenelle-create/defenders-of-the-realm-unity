# WORK ORDER 1172 — Build palette grouping: SELF-CONTAINED MOCKUP BRIEF

**Status:** READY FOR EXTERNAL MOCKUP. Hand this whole file to a design AI — it needs no repo access.
Implementation follows WO-1167 once a layout is chosen.

**Minted:** 2026-08-24 (CLI), banner bumped 1172 → 1173 in the same edit.
**Provenance:** owner — *"What about the buildings, is that done grouping by collector producer
storage defense? For build menu and UI"* · *"Can you create a work order? … so I can take it over to
AI to have them generate the mock up and framework."*

> **EVERY NUMBER BELOW IS READ FROM THE LIVE CODE OR DATA, NOT ESTIMATED.** Source files named per
> section so anything here can be re-verified.

---

## 1. THE PROBLEM, IN ONE LINE

The town build palette is **ONE FLAT HORIZONTAL STRIP of 12 cards** with no grouping, no headers and
no ordering logic. The player cannot tell a resource *producer* from its *storage* from a *shop*.

## 2. THE SURFACE AS IT EXISTS TODAY

Source: `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs`

| Property | Value |
|---|---|
| Orientation | **LANDSCAPE**, mobile (Solana Seeker, **2670 x 1200**) |
| Palette shape | Horizontal scrolling strip of fixed-width cards, docked to the BOTTOM |
| Card width | **260 px** |
| Card gap | **10 px** |
| Strip side padding | **24 px** |
| Tray height | **259 px** |
| Crystals band under it | **44 px** |
| Total dock height | **303 px** |
| Right-edge quick-tabs | 3 vertical tabs — **Town / Defenses / Castle Structures** — each **112 px** tall, 16 px gutter, 72 px edge inset |
| Minimum touch target | **112 px** (`ElarionUiKit.MinTouchPx`) — a hard floor, not a suggestion |

⚠ **12 cards x 260 px + gaps ≈ 3,150 px of content in a ~2,500 px wide tray.** It already scrolls;
grouping must not make it scroll *more* without giving something back.

## 3. WHAT MUST BE GROUPED — the 12 town buildings (owner-ruled 2026-08-24, WO-1168)

| Group | Buildings | What the group means to the player |
|---|---|---|
| **PRODUCERS** | Lumber Mill · Quarry · Iron Mine · Cathedral of Magic | the nodes that GENERATE a resource |
| **STORAGE** | Lumberyard · Stone Yard · Foundry | raise the CAP on how much you can hold |
| **TRADE / CRAFT** | Store · Smithy · Crafting Station | spend and make things |
| **CIVIC** | Barracks · Echo Hollow | troops, and the Echo home |

*(Defenses and Castle Structures are SEPARATE tabs already and are out of scope.)*

⚠ **Producer/Storage is the pairing that matters most** and is the least visible today: each producer
has exactly one matching store (Lumber Mill→Lumberyard, Quarry→Stone Yard, Iron Mine→Foundry), and a
player who does not grasp that pairing cannot reason about the economy at all.

## 4. ⛔ HARD CONSTRAINTS — a mockup violating any of these cannot ship

1. **LANDSCAPE, ONE-HANDED, TOUCH.** No hover states. Nothing smaller than **112 px** may be tappable.
2. ⛔ **NEVER ENCODE MEANING IN COLOUR ALONE. The owner is colourblind.** Group identity must be
   carried by **text, icon, shape or position**. A palette that reads correctly in GREYSCALE is the
   gate. Colour may reinforce; it may never be the signal.
3. **The tray band must stay OPAQUE.** It sits over the live 3D town; any bare band shows the world
   through the UI (a defect already fixed once — do not reintroduce it).
4. **Vertical budget is 303 px total** for dock + crystals band. Headers must come out of the
   existing budget or justify raising it — the 3D town needs the rest of the screen.
5. **The three right-edge quick-tabs stay.** Town / Defenses / Castle Structures is the top-level
   split; grouping happens INSIDE the Town tab.
6. **12 items today, and the list GROWS.** Buildings are added by data with no code change, so the
   layout must not assume a fixed count or a fixed number of groups.
7. **Do not hide anything behind a collapsed group by default.** A building the player cannot see is
   a building they do not know exists.

## 5. WHAT TO PRODUCE

1. **2–3 distinct layout options** for grouping 12 cards in a 303 px-tall horizontal landscape dock.
   Candidate directions (not exhaustive — better ideas welcome):
   - inline group headers as narrow vertical dividers between card runs
   - a second-level segmented control above the strip (All / Producers / Storage / Trade / Civic)
   - stacked mini-rows, two shorter rows instead of one tall one
2. For each: a **greyscale** version proving it works with zero colour information.
3. The **empty and single-item** states for a group (a group can legitimately hold one card).
4. A note on what happens as the roster grows to ~20 buildings.

## 6. HOW IT WILL BE IMPLEMENTED (so the mockup is buildable)

Group membership is **authored data, never code** — a `paletteGroups` block in
`build-categories.json` listing role names, and every building carries a `role` string. Adding a
building, or a whole new group, must require **no recompile** (standing owner rule). So:

- Group **labels are data** — design for variable-length text, not a fixed word.
- Group **count is variable** — do not design for exactly four.
- Card content itself is unchanged: icon, name, cost. **This ticket adds grouping, not a card redesign.**

## 7. WHAT THIS TICKET IS NOT

Not a card redesign, not a re-sort of cards inside a group, not a cost/balance change, not the
Defenses or Castle Structures tabs. **It adds structure to a flat list.**
