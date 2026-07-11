# Knight Skill-Tree VFX Mapping (WO-VFX-003)

**Status:** IMPLEMENTED (code + data wired 2026-07-10). Catalog rows author in-editor via
`Defenders/VFX/Generate Hovl VFX Catalog` (`DeNelle.Editor.HovlVfxCatalogGenerator.Generate`).
Until the `.asset` is (re)generated, every `PlayKey` call no-ops safely (throttled log) — nothing breaks.

## How it works (data-driven)

Each ability in `abilities.json` carries up to four **string keys**:
`vfxCast` / `vfxProjectile` / `vfxImpact` / `vfxResidual`. `HeroAbilities` reads them and calls
`VFXManager.PlayKey(key, pos, …, color: def.UnityColor)` at the matching beat:

- **Cast** — `HeroAbilities.CastResolved` → `PlayCastVfxKey` (fires for EVERY active, Q/W/E/R + the assignable EXTRA bar), at the hero, chest height.
- **Projectile** — the ranged throws. For the **Knight** (melee-instant, no travelling body) a cosmetic Hovl projectile flies muzzle→target via `FlyCosmeticProjectile` (visual only; damage still lands on the existing instant timing). Mage/Ranger keep their existing `RangedAttackVFX` travelling body.
- **Impact** — at the connection point: inside the `LaunchProjectile` arrival closure (strike/snare/dot), at the `Blast` centre (cleave/aoe/meteor), or at the foe (dash/knockback/taunt).
- **Residual** — a looping aura/DoT/HoT/shield on the hero or struck foe, auto-stopped after the effect's duration (`StopHandleAfter`).

**Adding VFX to a NEW ability = pure data:** add the `vfx*` keys to `abilities.json` (+ a catalog row for any new key). No code change.

**Tints (element by `def.color`, applied as HDR StartColor):** fire = orange (`#fb923c`), ice/slow = blue (`#7dd3fc`), lightning = violet (`#a5b4fc`), arcane = violet (`#b388ff`), heal = green/gold (`#86efac` / `#ffd27a`), physical = amber (`#fbbf24`). **Owner is red/green colorblind** — heal, shield, and taunt read by SHAPE + MOTION (ring / bubble / outward roar), never hue alone. Heal / shield rows are authored `recolorable:false` so they keep their designed gold/holy read.

## The 16 actives → keys

| # | Ability (id) | effect | Cast | Projectile | Impact | Residual |
|---|---|---|---|---|---|---|
| 1 | knight.thunderbolt | strike | `Thunderbolt_Cast` * | `Thunderbolt_Projectile` | `Thunderbolt_Impact` | — |
| 2 | knight.emberbrand-throw | dot | `Fireball_Cast` | `Fireball_Projectile` | `Fireball_Impact` | `Ember_Burn` * |
| 3 | knight.wardens-roar | taunt | `Taunt_Roar` * | — | `Melee_Impact` * | `Taunt_Aura` * |
| 4 | knight.sweeping-cut | cleave | `Melee_Slash` * | — | `Cleave_Impact` * | — |
| 5 | knight.oathmend | healOverTime | `Heal_Cast` * | — | — | `Heal_Aura` * |
| 6 | knight.eternal-aegis | invuln | `Aegis_Cast` * | — | — | `Aegis_Shield` * |
| 7 | knight.second-wind | heal | `Heal_Cast` * | — | — | `Heal_Aura` * |
| 8 | knight.champions-combo | cleave | `Melee_Slash` * | — | `Cleave_Impact` * | — |
| 9 | knight.ranged-poke (Throwing Spear) | strike | `Melee_Slash` * | `Spear_Projectile` * | `Spear_Impact` * | — |
| 10 | knight.mending-salve | heal | `Heal_Cast` * | — | — | `Heal_Aura` * |
| 11 | knight.snare-arrow (Pinning Spear) | snare | `Melee_Slash` * | `Spear_Projectile` * | `Frost_Impact` | — |
| 12 | knight.suppressing-volley | cleave | `Melee_Slash` * | — | `Cleave_Impact` * | — |
| 13 | knight.shield-bash | snare | `Melee_Slash` * | — | `Melee_Impact` * | — |
| 14 | universal.arcane-bolt | strike | `Arcane_Cast` | `Arcane_Projectile` | `Arcane_Impact` | — |
| 15 | universal.mend | heal | `Heal_Cast` * | — | — | `Heal_Aura` * |
| 16 | universal.dash | blink | `Dash_Blink` * | — | — | — |

`*` = **NEW key added by WO-VFX-003** (13 total). Unmarked keys were already registered by WO-VFX-002.

Notes:
- **Cleave abilities** (sweeping-cut, champions-combo, suppressing-volley) resolve via `Blast`, not `LaunchProjectile`, so they get no travelling projectile — the slash reads through Cast (`Melee_Slash`) + Impact (`Cleave_Impact`) at the blast centre. Suppressing Volley is a ranged volley in flavour but a cleave in code; a projectile key is intentionally omitted to avoid dead data.
- **Snare-arrow** impact uses `Frost_Impact` (blue) to read the slow/pin, tinted by its blue accent.
- **Shield-bash** is close-range (3.6 m) — no projectile; Cast slash + `Melee_Impact` at the foe.

## New catalog keys (13) → exact Hovl prefab

Authored by `HovlVfxCatalogGenerator` (idempotent; re-point any line and re-run). All under `Assets/Hovl Studio/`.

| Key | Prefab | Loop | Recolor |
|---|---|---|---|
| `Thunderbolt_Cast` | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Flash 2 electro.prefab` | N | Y |
| `Spear_Projectile` | `AAA Projectiles Vol 1/Prefabs/Projectile VFX loop/Projectile 11 orange arrow.prefab` | Y | Y |
| `Spear_Impact` | `AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 11 orange arrow.prefab` | N | Y |
| `Melee_Slash` | `AOE Magic spells Vol.1/Prefabs/Flower slash.prefab` | N | Y |
| `Melee_Impact` | `RPG VFX Bundle/Random effect prefabs/Punch Hit.prefab` | N | Y |
| `Cleave_Impact` | `AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab` (scale 1.3) | N | Y |
| `Heal_Cast` | `Magic circles/Prefabs/Magic circle sun.prefab` | N | N (gold) |
| `Heal_Aura` | `RPG VFX Bundle/Random effect prefabs/Buff heal.prefab` | Y | N (green/gold) |
| `Taunt_Roar` | `AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab` | N | Y |
| `Taunt_Aura` | `Magic circles/Prefabs/Loop version/Magic circle blood loop.prefab` | Y | Y |
| `Aegis_Cast` | `Magic circles/Prefabs/Magic shield holy.prefab` | N | N (holy) |
| `Aegis_Shield` | `Magic circles/Prefabs/Loop version/Magic shield holy loop.prefab` | Y | N (holy) |
| `Ember_Burn` | `RPG VFX Bundle/Random effect prefabs/Debuff 1.prefab` | Y | Y |
| `Dash_Blink` | `RPG VFX Bundle/Random effect prefabs/Buff white twist.prefab` | N | Y |

**Reused from WO-VFX-002 (already in the catalog):** `Fireball_Cast/_Projectile/_Impact`,
`Thunderbolt_Projectile/_Impact`, `Arcane_Cast/_Projectile/_Impact`, `Frost_Impact`.

All 14 prefab paths verified present on disk 2026-07-10.

## Files changed

- `Assets/_Modules/Village/Hero/AbilityCatalog.cs` — `AbilityDef` gains `VfxCast/VfxProjectile/VfxImpact/VfxResidual`.
- `Assets/_Modules/Village/Hero/HeroAbilities.cs` — cast/impact/residual/projectile `PlayKey` wiring + helpers (`PlayCastVfxKey`, `PlayImpactVfxKey`, `PlayResidualLoop`, `StopHandleAfter`, `FlyCosmeticProjectile`, `ProjectileMuzzle`); `LaunchProjectile` gains optional `projectileKey`/`tint`.
- `Assets/Resources/Data/Canonical/abilities.json` + `Assets/StreamingAssets/Data/Canonical/abilities.json` — the 16 actives get their `vfx*` keys (kept identical).
- `Assets/Editor/HovlVfxCatalogGenerator.cs` — 13 new key→prefab rows.
- `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs` — authoring-table doc-comment extended with the 13 keys.
