> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit 4c1da079; SupportFieldStructure.cs was created.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 891 — VFX + behavior: healer structure + the reusable structure pattern

**Status:** DONE (reconciled 2026-08-08) · **Silo:** Structures/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` §7 · `VFX_CREATIVE_PICKS_REGISTRY.md` §6f. Enum LANDED — healer field reuses `Aura_Healer` (no new value).
**Depends on:** WO-884 Phase 0 platform + WO-888 (heal recipes).

## Scope
1. A **Healer structure** that heals units in-radius on a tick, presented as **continuous casting** (each tick visibly "casts"/telegraphs, then heals).
2. Lock in the **general reusable pattern** so new support/offensive structures are stats + two tags (proves the whole common-class thesis).

## Design
Slots in as **one new `case "HealerTower"`** in `StructureFactory.AttachBehaviorImpl` (:682), copying `range`/`fireRate`/`element` off `entry.repo` like `DefenseTower`, running a radius tick that HEALS (clone `HealingFountain`'s proven tick+aura-hold body, retargeted Heart→units-in-radius).

Beat kit (Holy — rising shape, colourblind-safe):
| Beat | Recipe | Map | Family |
|---|---|---|---|
| idle heal-field AURA | RisingSteam low/wide | `Aura_Healer` | A loop |
| per-tick CAST pulse (**telegraphs-as-casting**) | FireFlies upward burst | `Impact_Heal` | B |
| heal CONTACT on unit | FireFlies | `Impact_Heal` | B |

**General pattern to document + implement generically:** `VfxEmitter{ Family=Aura, Element=X }` for the field + `Vfx.On(this).AddImpact(X).At(pos/unit).Play()` per tick/contact. Element tag re-skins: Healer=Holy · Slow-field=Ice · Damage-aura=Shadow · Buffer=Arcane — each already resolved by `VfxElementTables`.

## Files to touch
- `Assets/_Modules/Village/Catalog/StructureFactory.cs` — `AttachBehaviorImpl` new `case`; the tick body.
- `Assets/_Modules/Village/Buildings/HealingFountain.cs` — reference (clone, don't edit).
- A `RepoProps` row / structure data entry authoring the `behaviorId` + stats.

## Acceptance criteria
**Engineering:**
- [ ] Healer heals allies within `range` at `fireRate`; scales stats off the repo like DefenseTower.
- [ ] Each tick plays a visible CAST pulse (telegraph) THEN the heal contact — not an instant silent heal.
- [ ] Idle heal-field aura holds via `VfxEmitter`/`PlayAura`, `Stop()` on disable; reuses `Aura_Healer` (no new enum).
- [ ] Adding a second element variant (e.g. Slow-field=Ice) requires only a new `behaviorId`+`case`+tag — NO new VFX code (demonstrate in the RESULT).
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] The healer building visibly "casts" a heal each tick (a pulse you can see it wind up), then allies around it get a rising heal.
- [ ] Reads as a support structure (rising shape), not a damage tower.
- [ ] Headless healer-structure screenshot (idle field + a heal tick) opened.

## RESULT
`WorkOrders/WORK_ORDER_891_vfx_structures_healer.RESULT.md` — include the "new structure in 2 tags" proof.
