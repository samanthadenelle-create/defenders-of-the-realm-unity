> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit 4c1da079; HarvestAura.cs was created.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 890 — VFX: harvest resource auras + ready-to-collect beacon

**Status:** DONE (reconciled 2026-08-08) · **Silo:** Economy/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` (Step 1–8) · `VFX_CREATIVE_PICKS_REGISTRY.md` §6e. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform + WO-889 nearest-N guard (harvest auras are loops — gate them).

## Scope
1. A per-resource **harvest aura** (5 resources) that reads as its resource by **motion vector** (colourblind — the sparkle trio Iron/Crystal/Gold split by motion).
2. A **ready-to-collect beacon** — build ON the existing full-tell (`CollectorStackView`, already wired at `StructureFactory:767`), do NOT rebuild it.

## Recipes (registry §6e)
| VFXType | Recipe | Reads-as (motion) |
|---|---|---|
| Harvest_Iron | DustMotesEffect + SparksEffect | heavy dust **settling** + metal spark glint |
| Harvest_Wood | DustMotesEffect (flat drift) | flat sideways-drifting chip motes |
| Harvest_Food | FireFlies (sparse) | light motes **rising** slowly (pollen) |
| Harvest_Crystal | FireFlies (dense) | **suspended twinkle**, no travel |
| Harvest_Gold | SparksEffect (short) | glint pops that **fall** |
| Collector_Ready | FireFlies rising bob | rising = "come pick me up" (reuse `SfxId.LevelUp`) |

## Files to touch
- Builders: DustMotesEffect, SparksEffect, FireFlies → `Resources/VFX/Harvest/`.
- `VFXCatalogGenerator.cs` Map rows (all Family A, IsLoop=true; auto-detect lifetime).
- `Assets/_Modules/Village/Harvest/NodeFillIndicator.cs` — host the harvest aura on the node's `collecting` state; stop on `idle`/`depleted`.
- `Assets/_Modules/Village/Buildings/Progression/CollectorStackView.cs` — add the `Collector_Ready` beacon when `IsFull`/cap reached (decorate the existing tell — the "!" + glint stay).

## ⚠ SUBTLETY RULING (owner felt-test 2026-08-07) — read before wiring
Owner: *"the VFX is nice but the coloring should be MUCH more subtle — you cannot see what node type it is."*
The first pass rendered a **huge saturated flame plume** that swallowed the node. **Harvest/node VFX is a SUBTLE ACCENT, never a plume.** Binding tuning rules:
- **The node + its resource type MUST stay clearly visible** through/around the VFX at all times. If the effect hides the mesh, it's wrong.
- **Low emission, small scale, low opacity/tint.** The aura sits at/around the node base (≈ node footprint), not a tall column. Cap height ≈ the node's own height, not a screen-filling flame.
- **Colour is a faint hint, not a floodlight** — a light tint + sparse motes, not a solid glow. (Iron = faint dust + occasional glint, NOT a fire.) Read the resource by the node mesh first; the VFX only *reinforces* it.
- Reuse the ratified subtle recipes (DustMotes / sparse FireFlies / short Sparks) at **reduced rate + scale + alpha** — do not swap in a big Fire/flame recipe. If a recipe reads too hot, dial rate/startSize/startColor-alpha down before anything else.

## Acceptance criteria
**Engineering:**
- [ ] Each resource aura resolves to its committed Resources prefab; loops start on harvest, `Stop()` on idle/depleted.
- [ ] Harvest auras respect the nearest-N gate (WO-889) — a town of nodes doesn't blow the loop cap.
- [ ] Ready beacon fires exactly when the collector/node is ready; does NOT double the existing full-tell (reuses it).
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] The five resources read distinct in greyscale purely by MOTION: iron settles (dust+spark), wood drifts flat, food rises, crystal hangs & twinkles, gold pops & falls.
- [ ] A full collector reads "collect me" from across the town by the rising bob beacon.
- [ ] Headless harvest-node + ready-collector screenshots opened.

## RESULT
`WorkOrders/WORK_ORDER_890_vfx_harvest_economy.RESULT.md`.
