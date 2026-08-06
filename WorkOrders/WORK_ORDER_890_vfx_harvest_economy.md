# WORK ORDER 890 — VFX: harvest resource auras + ready-to-collect beacon

**Status:** READY TO IMPLEMENT · **Silo:** Economy/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05
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
