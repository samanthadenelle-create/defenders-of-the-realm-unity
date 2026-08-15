# RESULT — WO-935 Phase 1 CombatCast

**Status:** PARTIAL Phase 1 — 2026-08-15

## Change

- `CombatCast.Play(spellId, caster, target?)` — fireball / heal / arcane via SpellVfxFactory + PlayCast.
- Troop mage strikes call `CombatCast.Play(Fireball, …)`.
