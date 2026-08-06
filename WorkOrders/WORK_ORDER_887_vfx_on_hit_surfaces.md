# WORK ORDER 887 — VFX: on-hit surface + element impacts

**Status:** READY TO IMPLEMENT · **Silo:** Combat/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` (Step 1–8) · `VFX_CREATIVE_PICKS_REGISTRY.md` §4. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform.

## Scope
The moment a weapon/attack CONNECTS (distinct from a spell's own impact): pick the burst by **surface material** and **element**. All BURST family — `Vfx.On(...).AddImpact(element).At(hit).Play()` — no handle, pool reclaims.

## Recipes (registry §4)
| Hit case | Recipe | SFX |
|---|---|---|
| Physical → flesh (organic) | FleshImpacts | flesh thud |
| Physical → metal/armour | MetalImpacts | metal clang |
| Physical → stone/wall | StoneImpacts | stone |
| Physical → wood (barrel/crate) | WoodImpacts | wood |
| Physical → dirt/sand | SandImpacts | — |
| Generic physical | SmallExplosion | `Shockwave` |
| Fire proc | TinyExplosion + TinyFlames cling | `FireExplosion` |
| Ice proc | IceLance shard burst | — |
| Arcane proc | EnergyExplosion | `ArcaneExplosion` |
| Nature/poison proc | GoopSpray + puddle | — |
| Ranged release (any) | MuzzleFlash (`Cast_MuzzleFlash`) | `TowerShot`/bow |

## Files to touch
- Builders: Flesh/Metal/Stone/Wood/SandImpacts, TinyExplosion, MuzzleFlash → `Assets/Resources/VFX/Impact/`.
- `VFXCatalogGenerator.cs` Map rows (**IsLoop=false** — FleshImpacts etc. are hybrid, force burst per handbook §5.2).
- Surface/element detection at the melee + projectile land sites; `TowerCombat.OnProjectileImpact`; `HeroAbilities` impact site; `Destructible`/surface tag lookup.

## Acceptance criteria
**Engineering:**
- [ ] Correct surface recipe plays per struck material (flesh vs stone vs wood vs metal vs dirt).
- [ ] Element procs override/augment the physical surface hit as specified.
- [ ] Every impact `IsLoop=false` — zero loop-slot leaks (verify no `_maxActiveLoops` growth in a hit-heavy fight via FlowTrace).
- [ ] Paired SfxId fires via VfxToSfx.
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] Hitting a wooden barrel splinters; a stone wall chips; an armoured foe sparks; flesh spatters — reads by shape, not colour.
- [ ] Fire/ice/arcane weapon procs read elemental (up-flame / angular shards / radial ring).
- [ ] Headless hit screenshots opened for flesh / stone / wood / a fire proc.

## RESULT
`WorkOrders/WORK_ORDER_887_vfx_on_hit_surfaces.RESULT.md`.
