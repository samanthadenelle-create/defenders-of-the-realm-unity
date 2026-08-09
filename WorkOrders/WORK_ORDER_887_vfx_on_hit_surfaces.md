> ## RECONCILED 2026-08-08 - true status is PARTIAL - surface half NEEDS-OWNER-RULING
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the element half is real (commit 4ef2d532); the surface half was correctly REFUSED with measurements - no SurfaceType, MaterialType or HitSurface enum exists anywhere in the tree. That half is an owner design task, not engineering debt.
> The previous Status line read "ELEMENT HALF LANDED 2026-08-05 (4ef2d532) - SURFACE HALF REFUSED WITH MEASUREMENTS" and was substantially correct; it is restated here in the reconciled vocabulary, with the surface half routed to the owner rather than left open as engineering work.

# WORK ORDER 887 — VFX: on-hit surface + element impacts

**Status:** BLOCKED - surface half NEEDS OWNER RULING (reconciled 2026-08-09 - the element half landed in `4ef2d532` (element now decides flavour, tier decides size); the surface half was refused with measurements because no SurfaceType, MaterialType or HitSurface enum exists anywhere in the tree - that half is an owner design task, not engineering debt)

**Status:** PARTIAL - surface half NEEDS-OWNER-RULING (reconciled 2026-08-08) — ELEMENT HALF LANDED 2026-08-05 (`4ef2d532`) · SURFACE HALF REFUSED WITH MEASUREMENTS — gate
`COMPILE_GATE_OK`. **What landed:** `TowerCombat.OnProjectileImpact` computed the projectile's element
EIGHT LINES BELOW the impact pick and never used it, so **every empowered tower detonated as
`Impact_ExplosionAether`**; element now decides flavour, tier decides size, and the paired `SfxId` follows.
Also replaced `FireAt`'s use of `Projectile_TowerArcane` (a projectile-BODY row with `IsLoop` TRUE) as a
muzzle flash. **What is REFUSED, and why nobody should re-attempt the copy:** the five surface rows carry
**demo geometry on the prefab ROOT** (built-in primitive mesh + pack material + a **SPHERE COLLIDER**), all
five **emit 5/sec on loop at the derivation authority**, and there is **no enum home**
(`Impact_Flesh/Metal/Stone/Wood/Dirt` do not exist). ⚠ **THE SURFACE SIGNAL DOES NOT EXIST — verified, not
assumed:** no `SurfaceType` field, no physic-material read, no per-material tag; wood palisades, stone
walls and steel gates share one `Structure` layer, and both footstep implementations play a single clip
with no surface query. **Defining a surface taxonomy is DESIGN work and belongs to the owner.**
Also refused: `GoopSpray` can never be selected — `DamageElement` is `{None, Aether, Flame, Ice}` and this
game has **no nature element**. Full ledger: `docs/reference/SESSION_INDEX_2026-08-06.md` §5.2, §6.11-6.12, §7.
*(original header: READY TO IMPLEMENT · **Silo:** Combat/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05)*
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
