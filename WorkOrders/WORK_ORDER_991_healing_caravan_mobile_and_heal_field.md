# WORK ORDER 991 — The Healing Caravan: mobile (very slow) + an unlockable heal field

**Status:** IMPLEMENTED — 2026-08-15 HealingCaravanMobility (slow follow + glass HP); heal field unlock still later
**Minted:** 2026-08-14 (CLI)
**Silo:** Support structures / town defense / siege units
**Source:** OWNER DESIGN, 2026-08-14 + movement ruling 2026-08-15

---

## OWNER RULING 2026-08-15 (movement + role)

> **Offensive/defensive unit.** Slow-rolls **following hero movement**.  
> **Too slow to be useful for an entire siege** — so it **must** be slow, and **very easily damagable**.

### Closed forks

| # | Answer |
|---|--------|
| How does it move? | **Follow-the-hero** (slow roll / lag behind the player), not free patrol or instant relocate |
| How slow? | **Useless as a full-siege escort** — feel: deliberate crawl; player must **place the fight**, not drag the caravan through every lane |
| Fragility | **Very easily damaged** — glass support unit; enemies and siege pressure can kill it if left exposed |
| Role | **Offensive/defensive support unit** (not a static building-only prop) — heals/support presence that moves with the campaign of the hero |

### Still open (implementer defaults OK; pin if wrong)

| # | Suggested default |
|---|-------------------|
| Heal while moving? | **Yes, reduced** (e.g. 50% field strength while rolling; full while hero is near + caravan not moving) — keeps follow mode useful without making it a mobile fortress |
| Unlock heal field | Building tier or research after caravan is placed (base = mobile shell; field unlocks later) |
| Heal targets | **Heart + nearby troops** first; other structures later if feel asks |
| Grid claim while moving | **No fixed grid claim while rolling** — freestanding unit (like a slow troop), re-claims only if “parked” / siege-mode later |

---

## The design, earlier (still valid)

> *"the healing tower idea is what caravans replaced… recover damage like for tree of life and nearby troops"*  
> *"by a caravan its mobile, but very slow"*

**1.** Healer tower successor (WO-990 retired the row; keep `HealerTower` field pattern).  
**2.** Mobility is the point — slow is the balance cost.  
**3.** Heal field is an unlock.  
**4.** Field heal: Tree + troops.

## ⚠ NONE OF THIS IS SHIPPED YET

`healing_caravan` still uses `behaviorId: HealingFountain` (static). Do not claim it moves until code lands.

## Build on existing pattern

`StructureFactory` `HealerTower` / `SupportFieldStructure` — field tags, not a new engine.

## Constraints already known

- **Strategic placement is ALWAYS ON** and the live monetization model is the player-built town, so a
  movable functional structure fits the existing direction rather than fighting it.
- **The owner is red/green colourblind** — a heal field's area indicator must read by shape, motion or
  luminance, never by hue alone.
- **VFX loop budget:** a persistent heal-field effect is a LOOP. A fire-and-forget loop permanently
  consumes a global slot. It must be a retained handle released when the field ends — never one per
  tick, never one per healed target. (This is the WO-955 / WO-983 lesson; the budget is real and has
  caused a P0.)
- **Cost basket:** `healing_caravan` was ruled **MAGICAL** on 2026-08-14 (owner pin 2, *"yes AoE
  healing"*) — crystals + iron, currently food 60 / iron 100 / crystals 190. Any tier added here
  follows that basket.

## What NOT to do

- Do not implement any of this from this SPEC as written — the six questions above change the shape.
- Do not state anywhere that the caravan currently moves.
- Do not delete or "clean up" the retained `HealerTower` case; this ticket is its reason to exist.
