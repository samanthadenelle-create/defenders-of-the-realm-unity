# WORK ORDER 814 — Gear ability unlock at max level (Lv 5)

**Status: SPEC DRAFT (owner floated 2026-07-30: "add ability at lvl 5?" — needs her
creative pick of abilities before READY)**
**Lane:** Gear / hero progression (rides WO-808 Option A, shipped 2026-07-30)

## The idea

Reforging a weapon/armor to its MAX level (5) unlocks a special ABILITY on that item —
the reward for full investment is a fantasy beat, not just the last +6%. ARPG-style;
mirrors the shipped troop pattern exactly (troop-upgrades.json `specialAbilities` with
`levelThreshold` -> ability id + status kind + description).

## Design sketch

- Data: gear-levels.json bands (or per-item rows if abilities should be item-specific —
  OWNER CALL: per-rarity generic vs per-item authored) gain
  `abilityAt: { level: 5, abilityId, description }`.
- Engine: GearStatResolver exposes `AbilityFor(def, level)` (null below threshold);
  combat hook at the existing on-hit proc seam (WO-566 talent procs precedent in
  PlayerAttackController) for weapons; armor abilities ride the mitigation path.
- UI: WO-808 surfaces show the locked ability line ("Lv 5: <ability>") on the Improve
  preview so the goal is visible from level 1; unlock toast on reaching max.
- Oracle: [gear-levels] extends — every abilityAt level is within the band's max.

## Needs from owner before READY

- [ ] Per-rarity generic abilities or per-item authored? (per-item = richer, more authoring)
- [ ] The ability list itself (creative canon — CLI never picks).
- [ ] Weapons only, or armor too?
