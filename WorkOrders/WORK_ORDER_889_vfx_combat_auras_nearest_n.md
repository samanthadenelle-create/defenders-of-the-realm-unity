# WORK ORDER 889 — VFX: persistent combat auras + loop-budget guard (nearest-N)

**Status:** READY TO IMPLEMENT · **Silo:** Combat/VFX/ops · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` §2/§10 · `VFX_CREATIVE_PICKS_REGISTRY.md` §6d. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform. **The loop-budget guard (below) MUST land before the mass aura wiring in this WO.**

## Scope
Two parts, in order:
1. **Loop-budget guard (FIRST):** scene-tiered `_maxActiveLoops` (**village 24 / dungeon 48 / boss 32**) + **nearest-N (6–8 nearest to camera/player) for enemy/pet auras ONLY** (never one-shots), reusing the `PoiCalloutSystem` nearest-N pattern; FlowTrace-throttle-log on cull.
2. **Wire the persistent auras** (all Family A, `PlayAura`→`Stop()`).

## Recipes (registry §6d)
| Aura (VFXType) | Recipe | Note |
|---|---|---|
| Aura_EnemyCaster | ElectricalSparks | crackling conduit |
| Aura_Necromancer | PoisonGas | roiling ground cloud |
| Aura_Healer | RisingSteam low | shares heal language |
| Aura_Flame | TinyFlames | body cling |
| Aura_Ice | DustMotesEffect | **COLD motion: slow drift, settle down/out — NOT firefly upward** |
| Aura_Dust | GroundFog low | foot dust |
| Aura_SmokeReaper | SmokeEffect | best-fit |
| Aura_HeartPulse | FireFlies | combat/raid Hearts ONLY (hub tree withholds) |
| Aura_EmpowerTower | RisingSteam tinted | scales L1→L3 |
| Aura_PetLevel1/2/3 | DustMotes → FireFlies → FireFlies+Sparks | density escalation |
| Pet_Aura_Fire / Ice | TinyFlames / DustMotes cold | |
| Boss_Aura_Phase1/2/3 | RisingSteam → MediumFlames → WildFire | calm→enraged→seething |

## Files to touch
- The loop-cap/nearest-N gate in `VFXManager` (scene-tier cap) + a nearest-N helper (reuse `PoiCalloutSystem`).
- Builders: ElectricalSparks, PoisonGas, RisingSteam, TinyFlames, DustMotesEffect, GroundFog, SmokeEffect, FireFlies, MediumFlames, WildFire → `Resources/VFX/Aura/`.
- Aura attach sites: EnemyBrain/`EliteVFXController`, `PetAuraVFX`, `ArcaneAura` (tower), `HeartAuraController`, DragonBoss phase auras.

## Acceptance criteria
**Engineering:**
- [ ] Scene-tier cap active (24/48/32); nearest-N (6–8) culls enemy/pet auras beyond the ring; a FlowTrace line logs each cull (no SILENT drop).
- [ ] Nearest-N never applies to one-shot impacts.
- [ ] A dressed dungeon + a wolf pack does NOT exhaust the loop budget or blank auras.
- [ ] Ice auras use cold drift motion (verified in the prefab velocity, not firefly upward).
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] Enemy types read by aura (caster crackles, necromancer miasma, reaper smoke) in greyscale.
- [ ] Boss phase auras visibly escalate calm→enraged→seething by scale/motion.
- [ ] Ice reads cold (settling drift), not "dust in a barn" or firefly sparkle.
- [ ] Headless dungeon-with-many-auras screenshot opened; cull log shown.

## RESULT
`WorkOrders/WORK_ORDER_889_vfx_combat_auras_nearest_n.RESULT.md`.
