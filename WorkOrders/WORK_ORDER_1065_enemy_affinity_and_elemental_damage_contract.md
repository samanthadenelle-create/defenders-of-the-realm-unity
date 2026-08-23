# WORK ORDER 1065 — Enemy affinity and elemental damage contract

**Status:** IMPLEMENTED — AWAITING COMBINED OWNER GATE  
**Parent:** WO-1063 · **Silo:** Core combat contract and enemy data/runtime

## Current truth

`DamageElement` supports None/Physical, Aether, Flame and Ice. `EnemyDamageable` forwards raw damage
because resistance math does not exist. Melee weapons still deliver `DamageElement.None`; weapon
element currently chiefly selects presentation.

## Design

One pure resolver returns base amount, source element, target affinity/vulnerability/traits,
multiplier, final amount and reason (`Neutral`, `Vulnerable`, `Resisted`). Initial tuning:

- Vulnerable: **x1.25**.
- Matching/resisted: **x0.75**.
- Neutral: **x1.0**.
- No immunity; multipliers clamped to safe owner-tunable limits.

Affinity applies exactly once after source damage is resolved and before HP removal. Every
`IDamageable` implementation uses the same resolver or explicitly documents exclusion.

## Enemy schema

```json
"affinity": "ice",
"vulnerableTo": ["flame"],
"traits": ["armored", "undead"]
```

- One affinity/resistance and one elemental vulnerability maximum in V1.
- Traits use a bounded vocabulary.
- Never infer at runtime from ids, names, models or region.
- Region `elementBias` may recommend loadouts but never changes enemy combat truth.
- Null/legacy data is explicitly neutral.

Owner-review starting hypothesis only: Hollow generally Aether-vulnerable; regenerating Trolls
generally Flame-vulnerable; Flame and Ice creatures resist self and oppose each other; Orcs mostly
neutral unless individually authored.

## Damage/status rules

- Weapon basics stamp their authored element.
- Existing elemental ability/pet sources keep theirs.
- Poison remains a status, not a new element.
- Decide and pin DoT affinity evaluation; recommended: each tick carries source element and resolves
  once, never at both application and tick.
- Publish a presentation-neutral result event; combat never constructs UI text or selects VFX.

## Regression matrix

Test neutral/vulnerable/resisted for basic weapon, ability, pet, DoT and boss target; assert exact
x1/x1.25/x0.75, no immunity, legacy-neutral behavior, one application only, and emitted reason parity.
Device evidence must show all three outcomes.

## Do not

- Do not create a large element wheel.
- Do not add immunity or name heuristics.
- Do not couple prefab keys to arithmetic.
- Do not rebalance enemy HP in the same change.
