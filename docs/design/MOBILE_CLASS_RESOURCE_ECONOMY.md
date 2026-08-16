# Mobile class resource economy — Warcraft / StarCraft vibe

**Status:** LOCKED creative design (owner granted full creative decision 2026-08-15)  
**Implements via:** WO-999 · parent plumbing WO-997  
**Platform:** mobile-first (thumb combat, short sessions, glanceable HUD)

---

## Design north star

Play should feel like **Warcraft unit spells** + **StarCraft ability economy**:

| Principle | WC/SC parallel | Mobile consequence |
|-----------|----------------|--------------------|
| Auto-attack is free | WC worker/marine fire, WC autos | **Q never costs resource** — always something to do with a thumb |
| Specials cost a pool | WC mana on spells | **W/E/R drain the bar** so big buttons are decisions |
| Identity by race/class | Race tech trees | **Mana / Vigor / Focus** — three readable names, one bar |
| Rebuild mid-fight | WC regen, WC hunter focus | **Fast enough regen** that empty ≠ 30s of dead thumbs |
| Full restore at home | WC inn / fountain | **Town full restore** (already ships) |
| Clarity over depth | SC resource counts | **Cost digit on the face** + bar that moves when you spend |

Not a desktop MMO spreadsheet. Not infinite spam. **CD is the primary gate; resource is the secondary “can I afford the big button now?” check.**

---

## Locked numbers (v5 economy)

### Pools

| Class | Name | Max | Regen | On-hit restore |
|-------|------|----:|------:|----------------|
| **Mage** | Mana | **24** | **1.4/s** | 0 |
| **Knight** | Vigor | **12** | **2.0/s** | 0 |
| **Ranger** | Focus | **15** | **0.8/s** | **+1.5 per free basic that lands** |

*Why mobile:* full Mage bar ~17s; can still W twice in a wave. Knight recovers a charge every ~1.5–2s. Ranger is **weave-to-spend** like WC hunter focus.

### Kit costs (Q free forever)

| Class | Q | W | E | R |
|-------|--:|--:|--:|--:|
| Mage | 0 | **5** | **7** | **12** |
| Knight | 0 | **3** | **4** | **7** |
| Ranger | 0 | **4** | **5** | **9** |

*Feel targets (mage):* Shell ~20% bar · Drain ~30% · Poison ~50% (save for the spike).  
*Feel targets (knight):* Grace/Charge affordable every few seconds · Radiant is the save.  
*Feel targets (ranger):* Skills cost Focus; Quick Shot rebuilds it so thumb-fire is the refuel.

### Other locks

| Ruling | Decision | Why |
|--------|----------|-----|
| Q costs resource? | **Never** | WC autos free; mobile always has a safe press |
| Ranger Quick Shot restores Focus? | **Yes (+1.5)** | Archer fantasy = shoot to rebuild, like focus/energy |
| Universal skills cost? | **Stay free (0)** | Shared escape/heal/dash for all classes; WC potions/hearth feel |
| Barracks vigor structure? | **Later** | Cathedral already gives mage identity; don’t block V1 |
| Names | **Mana / Vigor / Focus** | Instant class read (WC mana bar mental model) |

### Skill-pool (talent-unlocked) costs

Leave as WO-997 authored unless a cost **> class max** (oracle fails). Mid talent skills stay in the 1–6 band; ultimates ≤ pool max.

---

## Presentation (mobile HUD law)

1. **Bar moves on spend** (already: float fill + flash) — keep.  
2. **Label = resource name** (Mana/Vigor/Focus) near the plate row.  
3. **Cost digit on W/E/R faces** (shape + number; colourblind-safe). Free Q blank.  
4. **Unaffordable = dim face + non-interactable** until regen covers cost (same as CD gate).  
5. Never gate only by hue.

---

## What this is not

- Not a second pool system  
- Not ATB mana  
- Not “pay for every auto” (anti-mobile)  
- Not empty-bar softlock — regen + town restore + free Q always exist  

---

## Acceptance (felt)

- Mage: 20s of real combat and you **choose** Shell vs Drain vs save for Poison.  
- Knight: Vigor is a **burst budget**, not a second HP.  
- Ranger: holding Quick Shot **fills** Focus; dumping Storm **empties** it.  
- Ten-year-old test: “blue bar is magic; I can’t press the big spell until it fills.”
