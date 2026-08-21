# WORK_ORDER_390 — Battle Potion Loadout (3 unlockable slots)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** 6 Economy + 2 Combat/AI.
**Source:** Owner, 2026-06-09. Companion to WO-389 (defender pre-places troops; this is the *attacker's* pre-raid loadout).

## Concept
The player can **carry up to 3 potions into battle** (a raid / combat). The **slots are UNLOCKABLE** — start with fewer, unlock more via progression — so it's a bounded loadout with a reward hook, NOT an open potion economy (owner scope-discipline: `scope-discipline-not-an-mmo`).

Symmetry with WO-389: defender sets a plan (placed troops); attacker sets a loadout (potions). Both = "spend a limited budget before the fight, then it plays out."

## Scope (deliberately bounded)
- **Max 3 potion slots.** Hard cap.
- **Slots unlock via progression** — slot 1 from the start; slots 2 & 3 unlocked by `<TBD: level / quest / purchase>`. Make the unlock condition + the slot count **easy to change** (`// TODO data-driven: move to potions/loadout config (JSON)`).
- Potions are chosen/equipped **before** a raid/battle; consumed **during** combat (heal / buff / shield — effects TBD).

## REUSE (no new system)
- **Consumables + inventory already exist** — `docs/ITEM_DROPS_CONSUMABLES_DESIGN.md` + the ATB `BattleState.Inventory`. Build the loadout on top of those: a "potion belt" = up to 3 references into the existing consumable catalog/inventory. Do NOT build a new consumable system or new combat-effect logic — reuse the existing consume/effect path.
- Equip UI: reuse an existing slot/loadout UI pattern where possible (don't bespoke a new one).

## Phasing
- **Defer build until WO-389's defense → raid → win/lose loop is playable.** Then layer potions in — by then you'll *know* whether combat wants in-the-moment buffs (don't pre-build it).

## Data-driven (comment everything)
- Slot count (3), unlock conditions, potion effects/values → all hardcoded now, every value commented `// TODO data-driven (JSON/config)`.

## What NOT to do
- No new consumable/inventory system, no new combat-effect engine — reuse what exists.
- Don't exceed the 3-slot cap or turn it into an open potion economy (scope line).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
