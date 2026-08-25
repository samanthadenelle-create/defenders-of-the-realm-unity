# WORK ORDER 814 — Gear ability unlock at max level (Lv 5)

**Status:** READY — ⭐ owner APPROVED the shape 2026-08-24 (batch 2, ruling 11): per-rarity generic · weapons first · locked ability line visible from Level 1. ⛔ The ability identities stay hers, authored later; the slots may be built now. *(Prior line: SPEC DRAFT — owner floated 2026-07-30 "add ability at lvl 5?", read as needing her creative pick of abilities before READY.)*
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

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 11: **APPROVED — per-rarity generic, weapons first, locked line visible from Level 1.**

**Recorded by the UI seat from `OWNER_RULINGS_OWED_2.md` §11.**

1. ✅ **Per-RARITY generic, not per-item.** Per-item authoring scales with the catalog forever.
2. ✅ **Weapons first, armour after.** Weapons already have the on-hit proc seam the talent system
   uses; armour rides the mitigation path and is a second piece of engineering.
3. ✅ **Show the locked ability from Level 1** — *"Lv 5: <ability>"* on the Improve preview — so the
   goal is visible the whole way up instead of a surprise at the end.

⛔ **The ability IDENTITIES stay hers.** The slots may be built; the list is authored later, by her.

### ⭐ Her design caution — record it, it constrains what may be proposed

> **Favour abilities that CHANGE PLAYSTYLE, not *"+35% MORE DAMAGE."*** Frost **slows**, fire
> **burns**, arcane **chains / echoes**, holy **wards / sustains**.
>
> *"Different behavior gives rarity character."*

⭐ Mirrors the shipped troop pattern exactly (`troop-upgrades.json` `specialAbilities` with a level
threshold) — no new machinery.

**Status → READY.**
