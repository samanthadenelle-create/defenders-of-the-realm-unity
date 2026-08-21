# Mobile class resource economy — Warcraft / StarCraft vibe

**Status:** LOCKED creative design (owner granted full creative decision 2026-08-15)  
**Implements via:** WO-999 · parent plumbing WO-997  
**Platform:** mobile-first (thumb combat, short sessions, glanceable HUD)

---

## Design north star

Play should feel like **Warcraft unit spells** + **StarCraft ability economy**:

| Principle | WC/SC parallel | Mobile consequence |
|-----------|----------------|--------------------|
| Physical basic is free | WC worker/marine fire, WC autos | Knight/Ranger Q stay free; Mage Fireball is a spell and spends Mana |
| Specials cost a pool | WC mana on spells | **W/E/R drain the bar** so big buttons are decisions |
| Identity by race/class | Race tech trees | **Mana / Vigor / Focus** — three readable names, one bar |
| Rebuild mid-fight | WC regen, WC hunter focus | Regen is visible, while primary attack and consumables prevent dead thumbs |
| Full restore at home | WC inn / fountain | **Town full restore** (already ships) |
| Clarity over depth | SC resource counts | **Cost digit on the face** + bar that moves when you spend |

Not a desktop MMO spreadsheet. Not infinite spam. **CD is the primary gate; resource is the secondary “can I afford the big button now?” check.**

---

## Locked numbers (v6 economy, retuned 2026-08-21)

### Pools

| Class | Name | Max | Regen | On-hit restore |
|-------|------|----:|------:|----------------|
| **Mage** | Mana | **24** | **0.6/s** | 0 |
| **Knight** | Vigor | **12** | **2.0/s** | 0 |
| **Ranger** | Focus | **15** | **0.8/s** | **+1.5 per free basic that lands** |

*Why mobile:* a full Mage bar takes 40s to rebuild passively, while town still restores it instantly. This makes Mana a fight budget and gives Mana Draught/Manaweave real value. Knight recovers a charge every ~1.5–2s. Ranger is **weave-to-spend** like WC hunter focus.

### Kit costs

| Class | Q | W | E | R |
|-------|--:|--:|--:|--:|
| Mage | **3** | **5** | **7** | **12** |
| Knight | 0 | **3** | **4** | **7** |
| Ranger | 0 | **4** | **5** | **9** |

*Feel targets (mage):* Shell ~20% bar · Drain ~30% · Poison ~50% (save for the spike).  
*Feel targets (knight):* Grace/Charge affordable every few seconds · Radiant is the save.  
*Feel targets (ranger):* Skills cost Focus; Quick Shot rebuilds it so thumb-fire is the refuel.

### Other locks

| Ruling | Decision | Why |
|--------|----------|-----|
| Q costs resource? | **Mage only** | Fireball is a rapid spell and must exhaust Mana; Knight/Ranger physical basics remain free |
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
3. **Cost digit on every costed face** (shape + number; colourblind-safe). Free physical Q faces stay blank.
4. **Unaffordable = dim face + non-interactable** until regen covers cost (same as CD gate).  
5. Never gate only by hue.

---

## What this is not

- Not a second pool system  
- Not ATB mana  
- Not “pay for every physical auto” (anti-mobile)
- Not empty-bar softlock — primary attack, passive regen, Mana Draught/Manaweave, and town restore remain available

---

## Acceptance (felt)

- Mage: sustained Fireball permits eight rapid casts and refuses the ninth (~5s), then Mana becomes a visible pacing gate; W/E/R compete for the same budget.
- Knight: Vigor is a **burst budget**, not a second HP.  
- Ranger: holding Quick Shot **fills** Focus; dumping Storm **empties** it.  
- Ten-year-old test: “blue bar is magic; I can’t press the big spell until it fills.”
