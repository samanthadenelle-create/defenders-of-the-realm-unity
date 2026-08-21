<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 46 — Tower Combat (towers actually target, fire, and kill)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-26
**Author:** Owner playtest ("never seen defend-the-tower work") + code audit
**Priority:** CRITICAL — towers are the **PRIMARY method of defending**, not a
side feature. The hero and pets are *support*; the loop is built around placing
towers + walls to hold the line for the Heart.

---

## Primary-defense directive (owner)

> "That should all be tied into [the] primary method of defending."

Defending the Heart must be driven by **towers + walls**, with the hero/pets as
support. For that to be a real loop, three pieces have to work **together** — this
WO covers the tower piece and explicitly ties in the two companions so they ship
as one coherent system, not scattered fragments:

1. **Towers fight** (this WO, §Design/§Implementation) — auto-target + fire + kill.
2. **The Heart can be lost** (companion §A) — a breach must *drain Heart HP*, or
   there is no stake and no reason to build towers.
3. **Walls shape the fight** (companion §B) — walls must block/slow enemy pathing
   so tower placement + walls create kill lanes.

Towers (1) are the backbone; without (2) defending has no consequence; without (3)
placement has no strategy. Build (1) first; (2) and (3) complete the primary loop.

---

## Problem

Towers can be **built** but they are **inert decorations**. The audit confirmed
there is no tower-combat code anywhere:

- No `Tower.cs`, no `Projectile.cs`, no targeting/firing logic exists.
- `Building.cs` (`Assets/_Modules/Village/Buildings/Building.cs:48`) implements
  `IDamageableStructure` — it only *takes* damage (`ApplyDamage`/`Repair`). It has
  no `Fire()` / `Attack()` / target acquisition.
- `BuildMenu` places a tower (`TryPlace` →
  `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs:954` → `Instantiate(prefab)` →
  `Building.Configure(_armed)`), deducting crystals, but nothing makes it shoot.

Net effect for the player: build a tower, watch enemies walk past it untouched;
only the hero and pets deal damage. The central mechanic does not exist.

**Good news — the data is already there.** `TowerVariantDef`
(`BuildMenu.cs:161`) already carries per-variant combat stats:

| Variant | Element | `Dps` | `Hp` | Crystal | Wood | Stone |
|---|---|---|---|---|---|---|
| Flame Tower  | Flame    | 30 | 200 | 150 | 20 | 5  |
| Ice Tower    | Ice      | 26 | 220 | 150 | 20 | 5  |
| Aether Tower | Aether   | 34 | 190 | 180 | 20 | 8  |
| Stone Tower  | Physical | 24 | 260 | 120 | 15 | 10 |

What's missing is (1) a component that uses `Dps`/`Hp`/`Element`, (2) two new
tuning fields (`Range`, `AttackInterval`), and (3) wiring the chosen variant's
stats onto the placed object.

---

## Design

A tower is a stationary auto-attacker that mirrors how the **Pet** already fights
(`Assets/_Modules/Pets/Pet.cs`): sweep an enemy `LayerMask` with
`Physics.OverlapSphereNonAlloc`, pick the nearest live hostile `IDamageable`, and
on a cooldown call `IDamageable.TakeDamage(dmg, element)`. This is the project's
established combat seam — `IDamageable` lives in `DeNelle.Core.Combat`, and
`Tower` (in `DeNelle.Village`) may reference the concrete `Enemy` indirectly
through it, exactly like Pet and HeroAbilities do. **No new asmdef coupling.**

**Hitscan first, projectiles later.** Ship instant-hit damage with a short
element-coloured tracer/muzzle flash (reuse `AbilityVfxKit`'s element colours).
A travelling `Projectile` is a clean Phase-2 follow-up; it should not block a
working, killable wave.

**Damage cadence:** `damagePerShot = Dps * AttackInterval`. Default
`AttackInterval = 1.0s` and `Range = 18m` for all variants (tunable per variant
later). So a Flame Tower (Dps 30) hits for 30 every 1s within 18m.

**Element mapping** (`TowerElement` → `DamageElement`): Flame→Flame, Ice→Ice,
Aether→Aether, **Physical→None** (`DamageElement` has no Physical member; see
`Pet.ParseElement`). Ice towers should also apply a brief `StatusEffect.Slow`
(matches the Ice Wolf pet perk), Flame a small burn if/when status DOT exists.

**HP / destructibility:** towers already are `IDamageableStructure` via `Building`,
and enemies already contact-attack structures (`Enemy.ProbeForStructure`). Just
ensure the placed tower's max HP is set from the variant's `Hp`. When HP hits 0
the Building self-destroys; the `Tower` component dies with it.

**XP integration (important, do NOT break it):** towers are NOT `IXpEarner` and
must **not** call `DamageAttribution.Record`. A kill scored purely by towers then
has an empty damage ledger, and `ProgressionManager.Distribute` already falls back
to crediting the hero (`ProgressionManager.cs`). That's the intended behaviour —
towers help clear waves but don't themselves level. (If we later want tower XP,
register a per-tower `IXpEarner`; out of scope here.)

---

## Implementation

### 1. New file — `Assets/_Modules/Village/Buildings/Tower.cs` (`DeNelle.Village`)

A `MonoBehaviour` (sits on the same GameObject as `Building`, or a child):

- Fields configured by `Configure(float range, float attackInterval, float damagePerShot, DamageElement element, LayerMask enemyMask)`.
- `Update()`: tick a cooldown; when ready, `AcquireTarget()` (nearest live
  `CombatFaction.Hostile` `IDamageable` within `range` via
  `Physics.OverlapSphereNonAlloc` + `GetComponentInParent<IDamageable>()`, copy
  Pet's `NearestHostile`), then `Fire(target)`:
  `target.TakeDamage(damagePerShot, element)`, reset cooldown, spawn a tracer +
  muzzle flash, and `DamageNumberSpawner` already shows the hit number from inside
  `Enemy.TakeDamage`.
- Ice: `target.ApplyStatus(StatusEffect.Slow, 1f)` on hit.
- Reuse a cached `Collider[]` buffer (OverlapSphereNonAlloc) — no per-frame GC.
- `OnDrawGizmosSelected`: draw the range sphere (like Pet).

### 2. `TowerVariantDef` — add `Range` + `AttackInterval`

`BuildMenu.cs:161` — add `public float Range; public float AttackInterval;` and
set them in the four variant rows (default `Range = 18`, `AttackInterval = 1.0`;
e.g. give Aether a longer 22m range, Stone a shorter 14m hard-hitter later).

### 3. Carry the chosen variant onto the placed tower

Today `BuildTowerScreen` arms the canonical arcane-tower `BuildingDef` and the
shared `TryPlace` pipeline forgets which *variant* was chosen
(`BuildMenu.cs:494`). Fix:

- Stash the selected `TowerVariantDef` (e.g. `_armedTowerVariant`) when the
  player confirms a tower build.
- In `TryPlace` (`BuildMenu.cs:954`), after `Instantiate(prefab)` +
  `building.Configure(_armed)`, if a tower variant is armed:
  - set the Building's max HP from `variant.Hp` (add a `Building.SetMaxHp(int)` or
    a `Configure` overload — Building already owns HP),
  - `go.AddComponent<Tower>().Configure(variant.Range, variant.AttackInterval, variant.Dps * variant.AttackInterval, MapElement(variant.Element), _enemyMask)`.
- `BuildMenu` needs the enemy `LayerMask` — add a serialized `_enemyMask` set by
  the integrator (same value Pet/HeroAbilities use, e.g. `LayerMask.GetMask("Enemy")`).

### 4. Fire VFX (small)

Add `AbilityVfxKit.SpawnTowerBolt(Color elementColor, Vector3 from, Vector3 to)`
(or reuse the Ranger arrow streak) for a cheap tracer + impact spark. Element
colours already exist in `Pet.ElementColor` / `AbilityVfxKit`.

### 5. (Phase 2, optional) Upgrade wiring

`BuildMenu`'s Upgrade button is a stub
(`BuildMenu.cs` "Upgrade stub — Week 6"). Wire it to bump the placed `Tower`'s
`damagePerShot`/`range` and the Building HP, spending `UpgradeCrystalCost`.

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/Village/Buildings/Tower.cs` | **New** — targeting + firing auto-attacker |
| `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` | Add `Range`/`AttackInterval` to `TowerVariantDef`; stash armed variant; attach + `Configure` a `Tower` on placement; add serialized `_enemyMask`; set placed HP from `variant.Hp` |
| `Assets/_Modules/Village/Buildings/Building.cs` | Add `SetMaxHp(int)` (or a `Configure` overload) so a placed tower's HP comes from the variant |
| `Assets/_Modules/Village/Hero/AbilityVfxKit.cs` | Add a cheap tower-bolt tracer/impact (reuse element colours) |

No scene re-bake required. Compile-check with `run-unity-method.ps1`, then
`build-windows.ps1`.

---

## Acceptance Criteria

- [ ] A placed Flame/Ice/Aether/Stone tower **auto-targets** the nearest enemy in
      range and **fires on its cooldown**, dealing damage (floating damage numbers
      appear over the hit enemy).
- [ ] Towers can **kill** enemies; a wave can be cleared by towers alone (hero idle).
- [ ] Ice tower **slows** the enemies it hits.
- [ ] Each tower's element shows a distinct hit colour.
- [ ] Towers have HP from their variant and can be **destroyed** by enemies that
      reach them (Building HP → 0 → removed).
- [ ] Tower kills do NOT break XP: a tower-only kill credits the hero (the
      `ProgressionManager` fallback), and towers do not appear as XP earners.
- [ ] Crystal/material cost is still spent on placement; balance reads the live
      `GameStateService` (after WO economy fix, dev "+Crystals" funds tower builds).
- [ ] No new cross-module asmdef references (Tower talks to enemies only through
      `IDamageable`, like Pet/HeroAbilities).

## Out of scope (later)

- Travelling projectiles with arc/lead (Phase 2).
- Tower upgrade tiers (Phase 2 — stub exists).
- Per-tower target priority modes (nearest vs lowest-HP vs strongest).
- Heart taking damage on breach (separate concern — Heart already has `Hp`/`SetHp`
  in `HeartController`; wiring breach→Heart damage is its own small WO).

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `DefenseTower.cs, ArcaneTower.cs, ProjectilePool.cs` — premise false now. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
