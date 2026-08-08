> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: no pallet stack view exists anywhere in the tree; every `storageResource` consumer is caps / build-mode only.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 903 — Storage pallet fill stacks (logs / sacks / ingots ~5%)

**Status: NOT STARTED** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-04 (CLI / Grok — owner: pallets show items as bank fills)  
**Silo:** Village presentation / storage  
**Size:** **SMALL** — reuse collector stack pattern; no economy rewrite  
**Depends on:** bank max readable for wood/iron/food (901/857 storage caps — if max is still “uncapped,” use a large soft max or wait for Phase F/cap; prefer wire to real Max when present)  
**Adjacent:** 901 collector loop · 900 collector full tell · `docs/ART_BRIEF_storage_containers.md`

---

## Goal

On **pallets** (storage containers), diegetically show fill:

| Building (catalog id) | Resource | Prop as fill rises |
|----------------------|----------|--------------------|
| `lumberyard` | wood | **logs** (~1 per 5%) |
| `foundry` | iron | **ingots** |
| `silo` | food | **grain sacks** |

```
fill = current(resource) / max(resource)   // village bank
steps = floor(fill * StepCount)            // StepCount = 20 → ~5% each
```

Empty pallet = frame only (0 props). Full = 20 props stacked. Colorblind-safe by **count/height**, not hue.

**Not** collector pending — that stays on farm/lumbermill/forge via `CollectorStackView`.

---

## Reuse (do not rebuild)

| Existing | Use |
|----------|-----|
| `CollectorStackView` + `CollectorStackPropCatalog` | Same prop map (Wood/Iron/Food) and step/pooling idea |
| `RepoProps.storageCapacity` + `storageResource` | Identify pallets |
| Wallet / ResourceLedger + storage max (901) | `current` / `max` |

Prefer: **generalize** stack view to accept a fill provider **or** thin `StorageStackView` that copies Attach/pool pattern and loads the **same** `CollectorStackPropCatalog`.

**Do not** invent a second prop catalog unless the SO cannot be shared.

---

## Scope

1. **Attach** on placed/live lumberyard, foundry, silo (StructureFactory / place commit / scene load — same place collectors get their view once wired).
2. **Fill driver:** poll or subscribe when resources change; recompute steps; toggle props.
3. **Props:** ensure catalog asset at `Resources/Collectors/CollectorStackPropCatalog` has Wood/Iron/Food prefabs (polyperfect log/crate/sack if missing — one-time assign, not new art pipeline).
4. **Fallback:** abstract bar if prop missing (collector pattern).
5. **No** tap-to-collect on pallets (bank only). Optional later: select building shows `current/max` text.

### Out of scope

- Wallet clamp / grant rules (901 Phase F)  
- Collector icons (900/858)  
- Jeweler  
- Crystals pallet (unless a container exists)  
- Full Tripo multi-mesh LODs from art brief (optional follow-up)

---

## Acceptance

- [ ] Place lumberyard; grant wood → logs appear stepwise as bank fill rises  
- [ ] Foundry + iron → ingots; silo + food → sacks  
- [ ] Spend resource → steps drop  
- [ ] 0% = no props; ~100% = full stack  
- [ ] Dual-copy untouched unless only docs; no combat changes  
- [ ] COMPILE_GATE_OK; brace-check any .cs  

---

## Paste for Claude / CLI

```text
Implement WORK_ORDER_903_storage_pallet_fill_stacks.md (SMALL).
Reuse CollectorStackView / CollectorStackPropCatalog pattern on lumberyard/foundry/silo.
Drive fill from bank current/max (~20 steps, ~5% each): logs / ingots / sacks.
No collect-on-pallet; no economy rewrite. COMPILE_GATE_OK; brace-check .cs.
```
