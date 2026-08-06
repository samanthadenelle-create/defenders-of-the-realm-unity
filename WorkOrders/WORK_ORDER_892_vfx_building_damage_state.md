# WORK ORDER 892 — VFX: building damage state (smoke → fire → critical-save beacon)

**Status:** READY TO IMPLEMENT · **Silo:** Structures/VFX/UX · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` §7 · `VFX_CREATIVE_PICKS_REGISTRY.md` §6g. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform.

## Scope
Damaged buildings escalate **smoke → fire**, and — the real gap — a distinct **critical-save beacon** so the player unmistakably knows a building is about to be destroyed and must be repaired NOW.
**Re-skin the EXISTING observer — do NOT rebuild it.** `StructureDamageVisuals.cs` (WO-672) is the one self-installing, data-driven (`damage-states.json`) damage-state observer (covers Wall/Building/Tower/Collector; Gate/Heart opt out).

## Recipes (registry §6g)
| State | Recipe |
|---|---|
| Smolder (hp ≤ 0.5) | SmokeEffect low (light smoke) |
| Fire (hp ≤ 0.25) | MediumFlames + SmokeEffect |
| **CRITICAL-save beacon (NEW, at fire threshold)** | **SparksEffect fast-pulse + "!" tag** — alarm cadence, "repair me NOW" |
| Broken (hp = 0) | DustExplosion/BigExplosion one-shot + lingering WildFire/SmokeEffect column |

## Files to touch
- `Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs` — **re-point its recipe keys (data-only, no observer rewrite)** + add the critical beacon at the fire threshold.
- `Assets/Resources/Data/Canonical/damage-states.json` — a `criticalBeacon` key/threshold field if needed (dual-copy, WebGL-safe).
- Builders: SmokeEffect, MediumFlames, SparksEffect, DustExplosion/BigExplosion, WildFire → `Resources/VFX/Damage/`.

## Acceptance criteria
**Engineering:**
- [ ] Observer logic UNCHANGED (no rewrite) — only recipe keys re-pointed + the beacon added.
- [ ] Thresholds still read from `damage-states.json`; per-type overrides + optOut (Gate/Heart) preserved.
- [ ] Burn loops stay worst-first capped (existing `maxBurnLoops`) on top of the scene loop cap.
- [ ] Broken transition fires the one-shot explosion + a persistent smoking column; bar pinned empty (not torn down).
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] A lightly damaged building smokes; a heavily damaged one is on fire — reads by smoke/flame density, not colour.
- [ ] A CRITICAL building is unmistakable from across the base (fast-pulsing beacon + "!") — the player knows to save it before it's destroyed.
- [ ] A destroyed building explodes and leaves a smoking ruin.
- [ ] Headless screenshots opened for smolder / fire / critical / broken.

## RESULT
`WorkOrders/WORK_ORDER_892_vfx_building_damage_state.RESULT.md`.
