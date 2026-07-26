# WORK ORDER 772 — Shared Enemy System: classes, families, equippable armor & weapons

**Status:** SPEC. New shared data layer consumed by **both** dungeons (WO-770.11 enemy
placement, encounters) **and** raids (WO-771 defenders + wave enemies). Sequenced as a
prerequisite for WO-770.11 and referenced by WO-771.13.
**Date:** 2026-07-26
**Author:** enemy-systems design pass.
**Owner ask (2026-07-26):** "enemies get armor, weapons, classes, family structure."

**Goal.** One canonical, data-driven enemy model that gives every enemy a **family**
(taxonomy/hierarchy), a **class** (combat archetype), and **equippable armor + weapons**
(stat modifiers + KayKit modular mesh parts) — reused everywhere enemies appear, so
dungeons and raids draw from the same roster and the same art.

## Reuse (verified) vs new

| Need | Real reuse | New |
|---|---|---|
| Role taxonomy | `enemy-roles.json` — 25 creatures with roles (`defender/attacker/dps_ranged/dps_caster/healer/cc/swarm/trap/boss_tier`) + `hpScale/atkScale/speedScale/behavior` | family + class layers on top |
| Combat stat block | ATB `EnemyDef` (`BattleATB/Engine/Types.cs:235` — `BaseHp/BaseAttack/Speed/Defense/Element/Special`) + `EnemyDefSO` (`CombatantDefSO.cs`) | armor/weapon stat modifiers |
| Canon families/lore | `docs/enemy-codex.md`, `canon-strings.json` (the **Hollow Ones**, Alduin, Syndrath), `themes.json` (`family`) | typed `EnemyFamily` tree |
| Modular art | KayKit Adventurers + Skeletons (armor/helmet/weapon parts) | part-key → mesh resolver (via WO-771.13 builder) |

## Data model (new — `_Modules/Core/Enemies/` or a shared `DeNelle.Data` location both modules already reference)

1. **`EnemyFamily`** — the taxonomy/hierarchy: `{ id, displayName, parentId?, sharedTraits
   (element bias, base behavior), memberClassIds[] }`. E.g. `hollow-ones` → members
   `hollow-walker/warrior/rogue/captain`, boss line `necromancer`. Families can nest
   (`parentId`) for sub-clades. Canon-sourced from `enemy-codex.md`.
2. **`EnemyClass`** — the combat archetype layered on a `enemy-roles.json` role:
   `{ id, roleKey (enemy-roles), statArchetype (hp/atk/spd/def multipliers), preferredWeapon,
   preferredArmor, abilityIds[] (ATB AbilityDef) }`. E.g. `warrior` (defender/attacker),
   `caster` (dps_caster), `archer` (dps_ranged), `brute` (boss_tier).
3. **`ArmorDef`** — `{ id, tier, defenseBonus, hpBonus, modelPartKeys[] (KayKit chest/helmet
   parts) }`.
4. **`WeaponDef`** — `{ id, attackBonus, element, reach (range), attackSpeed, modelPartKey
   (KayKit weapon mesh) }`.
5. **`EnemyDef` (extended / new unified record)** — the existing ATB `EnemyDef` fields **plus**
   `familyId, classId, armorId, weaponId, level`. `EffectiveStats(EnemyDef)` composes:
   base (class stat archetype × `enemy-roles` scalars) + armor (defense/hp) + weapon
   (attack/element/reach), producing an ATB `EnemyDef`/`BattleUnit` for the fight and a set
   of KayKit part keys for the mesh.

## Canonical JSON (mirror the existing `enemies.json`/`enemy-roles.json` conventions)

- `StreamingAssets/Data/Canonical/enemy-families.json` — the family tree (Hollow Ones + subclades).
- `StreamingAssets/Data/Canonical/enemy-classes.json` — the class archetypes.
- `StreamingAssets/Data/Canonical/armor.json`, `weapons.json` — equipment with stat mods + KayKit part keys.
- Extend existing `enemies.json` entries with `familyId/classId/armorId/weaponId/level`.
- Loaders follow the `WaveDataLoader`/`PetCatalog`/`DungeonLayoutLoader` async Newtonsoft pattern.

## Resolver + art

- `EnemyResolver.Effective(EnemyDef) → { ATB EnemyDef, KayKit part-key set }` — pure,
  data-only; unit-testable.
- The KayKit part-key set feeds the **WO-771.13** `KayKitUnitBuilder` (armor/helmet/weapon
  modular assembly on the shared rig + Animator). Missing part → graceful placeholder (WO-23 rule).

## Acceptance

1. `enemy-families.json`/`enemy-classes.json`/`armor.json`/`weapons.json` load; the Hollow
   Ones family tree resolves with its member classes (EditMode test).
2. `EnemyResolver.Effective` composes base + class + armor + weapon into correct effective
   stats (unit test with a known fixture) and emits the expected KayKit part keys.
3. The same resolved `EnemyDef` produces a working ATB `BattleUnit` (fights) **and** a KayKit
   prefab via WO-771.13 (mesh) — proving one model serves combat + art.
4. Both a **dungeon** placement (WO-770.11) and a **raid** wave/defender consume the resolver
   with no duplication.
5. `WORK_ORDER_772_*.RESULT.md`.

## Key files

`_Modules/Core/Enemies/EnemyFamily.cs`, `EnemyClass.cs`, `ArmorDef.cs`, `WeaponDef.cs`,
`EnemyDef.cs` (extended), `EnemyResolver.cs`; canonical `enemy-families.json`,
`enemy-classes.json`, `armor.json`, `weapons.json`; reads `enemy-roles.json`,
`BattleATB/Engine/Types.cs` (`EnemyDef`), `docs/enemy-codex.md`. Consumed by WO-770.11,
WO-771.13, and the ATB battle roster.

## Consumers (so this doesn't become an orphaned model)
- **WO-770.11** — dungeon enemy placement spawns actors from resolved `EnemyDef`s.
- **WO-771.13** — the KayKit builder assembles armor/weapon parts.
- **WO-771 raid** — defenders + any raid-side enemies draw from the same catalog.
- **ATB battle** — encounter/wave rosters resolve through `EnemyResolver`.
