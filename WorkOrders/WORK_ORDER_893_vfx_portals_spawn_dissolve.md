> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit 4c1da079; PortalVFXController.cs carries 9 WO-893 markers.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 893 — VFX: portals + spawn tiers + materialize/dissolve

**Status:** DONE (reconciled 2026-08-08) · **Silo:** World/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` §7 · `VFX_CREATIVE_PICKS_REGISTRY.md` §7. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform.

## Scope
Portal open/enter/exit, enemy spawn tiers (including the missing STANDARD spawn tell), and materialize/despawn via the pack's one scripted recipe.

## Recipes (registry §7)
| Moment (VFXType) | Recipe | Family |
|---|---|---|
| Env_DungeonPortal (open mouth loop) | **keep procedural vortex** + **SECONDARY** MediumFlames accent | A loop |
| Portal_Enter | EnergyExplosion (outward) + ParticlesLight | B |
| Portal_Exit | EnergyExplosion (inward, mirror) | B |
| Enemy_Spawn *(the missing standard tell)* | Respawn via `SpawnEffect` cutoff (bottom-up) — **one-shot, no demo loop** | scripted |
| Elite_Spawn | EnergyExplosion (upward, dark) | B |
| Boss_Spawn | BigExplosion + LightningStormCloud accent | B |
| Summon (necromancer/pet) | Respawn cutoff + `Area_generic` ground swell | scripted+A |
| Despawn_Dissolve (blink/unsummon) | Dissolve via `SpawnEffect` reversed — **one-shot** | scripted |

## Files to touch
- Builders: EnergyExplosion → `Resources/VFX/Portal/`; commit `SpawnEffect.cs` shader + a `_cutoff` material + Respawn/Dissolve prefabs → `Resources/VFX/Spawn/` (missing-on-clone must degrade to a plain burst).
- `Assets/_Modules/Village/Dungeon/PortalVFXController.cs` — enter/exit bursts + the flame accent (keep the procedural vortex).
- Spawn path (`WaveManager` standard spawn — **add the missing `Enemy_Spawn` call**; `EliteVFXController.DramaticSpawnRoutine` elite/boss).
- `SpawnEffect.cs` — a **one-shot play wrapper** (no auto-repeat pause loop) for gameplay use.

## Acceptance criteria
**Engineering:**
- [ ] Portal keeps its procedural vortex; the flame accent is SECONDARY (portal never re-skins to FlameThrower / never blurs with a fireball).
- [ ] `SpawnEffect`-driven dissolve/materialize plays ONCE, then Stop/return-to-pool — NO demo pause-loop dragged into combat.
- [ ] Standard enemy spawn now fires `Enemy_Spawn` (previously no VFX at all).
- [ ] Portal_Enter vs Portal_Exit distinguishable by MOTION vector (outward vs inward), not colour.
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] Stepping into a portal reads as "consumed"; arriving reads as "materialized" — mirrored motion.
- [ ] Mobs no longer pop from nothing — they materialize in.
- [ ] Boss spawn is a scale jump (big burst + lightning); elite is a rung below.
- [ ] Headless portal enter/exit + standard/elite/boss spawn screenshots opened.

## RESULT
`WorkOrders/WORK_ORDER_893_vfx_portals_spawn_dissolve.RESULT.md`.
