# WORK ORDER 772 — Shared Enemy System: classes, families, equippable armor & weapons

**Status:** SPEC (firmed) — PHASE 1 SHIPPED (EnemyResolver + EnemyTaxonomy live, EnemyResolverRegression green); Wildlands deferred. New shared data layer consumed by **both** dungeons (WO-770.11
enemy placement, encounters) **and** raids (WO-771 defenders + wave enemies). Prerequisite
for WO-770.11; referenced by WO-771.13.
**Date:** 2026-07-26
**Author:** enemy-systems design pass.
**Owner ask (2026-07-26):** "enemies get armor, weapons, classes, family structure."
**Canon source:** `docs/enemy-codex.md` — a **complete, authored roster/design codex** that
already defines the families, classes, roster table, canon locks, and model packs. This WO
**operationalizes the ratified codex** into a data model + resolver; it does not re-invent
the roster. **Codex is "review-and-approve before implementation" — owner ratifies first.**

## The codex already gives us the taxonomy (don't re-derive it)
- **Families (factions):** **The Hollow Ones** (primary — risen Folk / undead, *skeleton-based*,
  KayKit **Skeletons 1.1** is their whole model set; appear in village waves AND every dungeon's
  dark); **The Wildlands** (secondary — living: orcs/beasts/cavemen, from the Mystery Monthly
  slate, realm-2+ + dungeon variety); **Set-piece bosses** (8 named — 2 canon-locked, 6 agent-
  authored). (`enemy-codex.md` §1.1–1.3, §4.)
- **Canon locks (must not break):** `Alduin the Mournful`/the Necromancer (final antagonist,
  ends in dialogue not a fight), `The Apprentice of the Apothecary` (Healer's Cottage mini-boss,
  `Defs.cs hollow-apprentice`: `BaseHp 175 / BaseAttack 24 / "Tincture"`), the Hollow Ones tone
  ("grief that walks"). (`enemy-codex.md` §0.)
- **Classes:** the codex roster table's role column (Walker / Warrior / Rogue-Skirmisher /
  Caster / …) — layer onto `enemy-roles.json` (25 creatures × `role/hpScale/atkScale/behavior`).

## This WO also fixes a live bug
**Generic-skeleton spawns (both audits):** dungeon (and mini-boss) fights spawn *identical
skeletons* because enemy ids don't resolve to distinct enemies through the factory. The
`EnemyResolver` + a corrected id→factory mapping (below) is the fix — so `hollow-warrior`,
`hollow-rogue`, and the `hollow-apprentice` boss each spawn as their own def + model, not a
generic skeleton.

## Reuse (verified) vs new
| Need | Real reuse | New |
|---|---|---|
| Family/role taxonomy | `enemy-codex.md` (roster) + `enemy-roles.json` (25 roles + scalars) | typed `EnemyFamily` tree |
| Combat stat block | ATB `EnemyDef` (`BattleATB/Engine/Types.cs:235`) + `Defs.cs ENEMY_DEFS` (real stats, incl. `hollow-apprentice`) + `EnemyDefSO` (`CombatantDefSO.cs`) | armor/weapon stat modifiers |
| Spawn / id→enemy | the existing **EnemyFactory / EnemyCatalog** path (`WaveData.cs`, `Enemy.cs`) | **fix the id mismatch** so ids resolve to distinct defs+models |
| Modular art | KayKit **Skeletons 1.1** (Hollow Ones) + **Adventurers** (Wildlands/troops) | part-key → mesh resolver (via WO-771.13) |

## Data model (new — `_Modules/Core/Enemies/`, shared by Dungeons + Raid + Village)
1. **`EnemyFamily`** — `{ id, displayName, parentId?, faction (HollowOnes|Wildlands|Boss),
   sharedTraits (element bias, base behavior), memberClassIds[] }`. Nesting via `parentId`.
2. **`EnemyClass`** — `{ id, roleKey (enemy-roles), statArchetype (hp/atk/spd/def mult),
   preferredWeapon, preferredArmor, abilityIds[] (ATB `AbilityDef`) }`. (Walker/Warrior/Rogue/
   Caster/… from the codex.)
3. **`ArmorDef`** — `{ id, tier, defenseBonus, hpBonus, modelPartKeys[] (KayKit chest/helmet) }`.
4. **`WeaponDef`** — `{ id, attackBonus, element, reach, attackSpeed, modelPartKey (KayKit weapon) }`.
5. **`EnemyDef` (extended/unified)** — existing ATB fields **+** `familyId, classId, armorId,
   weaponId, level`. `EnemyResolver.Effective(EnemyDef)` composes base (class archetype ×
   `enemy-roles` scalars) + armor + weapon → an ATB `EnemyDef`/`BattleUnit` for the fight **and**
   a KayKit part-key set for the mesh.

## Canonical JSON (mirror `enemies.json`/`enemy-roles.json` conventions)
`enemy-families.json` (Hollow Ones + Wildlands + boss clades), `enemy-classes.json`,
`armor.json`, `weapons.json`; extend `enemies.json` entries with
`familyId/classId/armorId/weaponId/level`. Loaders follow the `WaveDataLoader`/`PetCatalog`
async Newtonsoft pattern.

## Resolver + factory + art
- `EnemyResolver.Effective(EnemyDef) → { ATB EnemyDef, KayKit part-key set }` — pure, unit-testable.
- **Fix the id→spawn mapping** in the EnemyFactory/spawn path so a placed/wave enemy id resolves
  through the resolver to the correct def + KayKit parts (kills the generic-skeleton bug).
- The part-key set feeds the **WO-771.13** `KayKitUnitBuilder` (Skeletons/Adventurers modular
  assembly on the shared rig + Animator). Missing part → graceful placeholder (WO-23 rule).

## Acceptance
1. Owner has ratified the `enemy-codex.md` roster (or a subset) this WO builds from.
2. `enemy-families.json`/`enemy-classes.json`/`armor.json`/`weapons.json` load; the **Hollow
   Ones** family resolves with its member classes; the canon-locked `hollow-apprentice` keeps
   its `Defs.cs` stats. (EditMode test.)
3. `EnemyResolver.Effective` composes base+class+armor+weapon into correct effective stats
   (fixture test) and emits the expected KayKit part keys.
4. **Generic-skeleton bug is gone:** in a dungeon fight, `hollow-warrior`/`hollow-rogue`/the
   mini-boss each spawn as their own def **and distinct model**, not identical skeletons
   (headless assert on the id→def mapping + an on-device eyeball).
5. One resolver serves a **dungeon** placement (WO-770.11) **and** a **raid/wave** enemy — no
   duplication.
6. `WORK_ORDER_772_*.RESULT.md`.

## Seams to verify on canonical (line numbers here are read-only-tree)
- The **EnemyFactory / EnemyCatalog** API and where the id→spawn mismatch lives (the generic-
  skeleton origin).
- Whether canonical already has partial family/class data (it's more evolved).
- KayKit Skeletons 1.1 staging (Hollow Ones models) — confirm in the staged asset set.

## Key files
`_Modules/Core/Enemies/EnemyFamily.cs`, `EnemyClass.cs`, `ArmorDef.cs`, `WeaponDef.cs`,
`EnemyDef.cs` (extended), `EnemyResolver.cs`; canonical `enemy-families.json`,
`enemy-classes.json`, `armor.json`, `weapons.json`; the EnemyFactory/spawn path (id fix).
Reads `enemy-roles.json`, `Defs.cs ENEMY_DEFS`, `docs/enemy-codex.md`. Consumed by WO-770.11,
WO-771.13, ATB battle roster, village waves.
