# WORK ORDER 991 — The Healing Caravan: mobile (very slow) + an unlockable heal field

**Status:** SPEC — needs design detail before implementation
**Minted:** 2026-08-14 (CLI)
**Silo:** Support structures / town defense
**Source:** OWNER DESIGN, 2026-08-14

---

## The design, verbatim

> *"the healing tower idea is what caravans replaced. this way they can eventually be unlocked to
> recover damage like for tree of life and nearby troops"*
>
> *"by a caravan its mobile, but very slow"*

## What this establishes

**1. The caravan is the Healer Tower's successor, by design.** `tower_healer` is being retired
(WO-990) not as cleanup but as **supersession** — its role is filled. The healing *idea* was never
abandoned; it moved to a better container.

**2. Mobility is the reason the container changed.** A tower is a fixed point. A caravan trades
placement permanence for **reach** — and *very slow* movement is the cost that balances a heal field
which can go where it is needed. That trade is the feature. A fast caravan would be strictly better
than a tower and would make placement meaningless; a static caravan is just a tower with different art.

**3. The heal field is an UNLOCK, not a base capability.** The owner's framing is *"eventually be
unlocked"* — so the caravan earns this, it does not start with it.

**4. What it heals: the Tree of Life (the Heart of Elarion) and nearby troops.** Note this is
explicitly a **field** — an area effect around the caravan — as distinct from `HealingFountain`'s
bespoke job of topping the Heart up out of battle.

## ⚠ NONE OF THIS IS SHIPPED

`healing_caravan` currently carries `behaviorId: HealingFountain` — a static, bespoke singleton.

**Mobility and the heal field are both design intent today.** No doc, catalog note, or commit message
may state that the caravan moves or heals a field. This banner exists because undated aspirational
copy is exactly how canon rots (CLAUDE.md §15) — and because a reader seeing "Healing Caravan" plus a
`HealingFountain` behaviour will reasonably assume one of the two is a bug.

## Build on the pattern that already exists — do not reinvent it

`StructureFactory.cs:935` `case "HealerTower":` is retained by WO-990 **specifically** because it is:

> *"WO-891. The FIRST instance of the general support/offensive FIELD pattern, and the proof of its
> thesis: a new structure is stats plus TWO TAGS. It copies range / fireRate / magnitude off entry.repo
> exactly the way DefenseTower's case above does, then hands SupportFieldStructure an element tag
> (presentation) and an effect tag (gameplay)."*

That is the worked example of the exact mechanism this feature needs. `:925` also holds a
commented-out `case "SlowFieldTower":`, a sibling of the same pattern.

⛔ **Do not resurrect `tower_healer`** to deliver this. The row is retired; the *pattern* is what
survives. Adding a heal field to the reachable caravan is the goal — reintroducing an unreachable
tower is not.

## Open design questions (owner input needed before this leaves SPEC)

These are genuine forks, not implementation details:

1. **How does it move?** Player-commanded relocation (pick it up, it walks there slowly), a patrol
   between set points, or follow-the-hero? Each is a different control surface and a different UI.
2. **Can it heal while moving**, or only while parked? This decides whether slowness is a *repositioning
   cost* or an *uptime cost* — very different balance levers.
3. **How slow is very slow** — relative to hero walk speed? A number is not needed yet; a comparison is
   (e.g. "half hero walk", "so slow you plan around it").
4. **What unlocks the heal field?** A perk, a building tier, an Echo, a research line? Note the
   arcane-wellspring perk already gates the caravan itself.
5. **Does it heal structures, troops, or both?** The ruling names the Tree of Life **and** nearby
   troops — confirm whether other buildings are in scope, since that decides whether it reads as a
   repair vehicle or a medic.
6. **Does mobility interact with the placement grid at all?** A moving structure that claims grid cells
   is a different problem from one that does not (see WO-986 on footprint claims).

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
