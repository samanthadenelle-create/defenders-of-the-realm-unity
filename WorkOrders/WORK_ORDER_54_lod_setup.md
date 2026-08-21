<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 54 — LOD Setup for Characters (Mobile Performance)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — prefab editing across all character types; no new scripts
**Depends on:** WO-53 (AnimatorCullingController must be present on each LOD)

---

## Goal

Add proper LOD Groups to every animated character (Heroes, Enemies, Pets, NPCs)
so distant characters cost almost nothing. Combined with WO-53's
`AnimatorCullingController`, this is the single highest-impact mobile
performance win available without changing gameplay.

---

## 1. LOD Group setup — per prefab

For every hero, enemy, and pet prefab:

### Step-by-step

1. Open the prefab.
2. Add a **LOD Group** component to the **root** GameObject.
3. The root should have **three child GameObjects** for each LOD level.
   If the prefab only has one mesh, duplicate it for LOD1/LOD2 (simplified
   versions can be created in the modeling tool or via Unity's Mesh
   Simplification package).
4. Wire each child into the corresponding LOD slot in the LOD Group.
5. Set transition distances (screen-relative height):

| Slot | Screen height | Distance equiv. | Animator culling |
|---|---|---|---|
| LOD0 (closest) | 100% → ~17% | 0 – 18 m | `AlwaysAnimate` |
| LOD1 (medium)  | ~17% → ~9%  | 18 – 35 m | `CullUpdateTransforms` |
| LOD2 (far)     | ~9% → 0%    | 35 m+     | `CullCompletely` |

6. On the `Animator` of **each LOD child**:

| LOD | `Culling Mode` |
|---|---|
| LOD0 | Always Animate |
| LOD1 | Cull Update Transforms |
| LOD2 | Cull Completely |

---

## 2. Mesh LOD guidelines

If no simplified meshes exist yet, use these targets:

| Character | LOD0 tris | LOD1 tris | LOD2 tris |
|---|---|---|---|
| Hero | ~8 000 | ~3 000 | ~800 |
| Enemy (regular) | ~4 000 | ~1 500 | ~400 |
| Pet | ~3 000 | ~1 200 | ~300 |
| NPC / Villager | ~2 000 | ~800 | ~200 |

Use Unity's **LOD Group** auto-calculation or the free **Mesh Simplify** package
if no art pipeline LODs exist. Even aggressive polygon reduction is invisible at
LOD2 distances.

---

## 3. AnimatorCullingController on LOD children

Each LOD child (not just the root) should have its own `AnimatorCullingController`
so the distance checks run against the correct Animator. Set distances to match
the LOD slot:

```
LOD0 child: updateTransformsDistance = 18, cullCompletelyDistance = 35
LOD1 child: updateTransformsDistance = 35, cullCompletelyDistance = 60
LOD2 child: updateTransformsDistance = 5,  cullCompletelyDistance = 10   (nearly always culled)
```

---

## 4. Priority order

If art time is limited, LOD the characters in this order (biggest win first):

1. Regular enemies (most instances per wave)
2. Pets (persistent aura cost)
3. Background NPCs / Villagers
4. Heroes (already optimised via WO-53 hero override)
5. Bosses (rare, worth high-quality LOD0 only)

---

## Files to Create / Edit

| File | Action |
|---|---|
| All enemy prefabs | **Edit** — add LOD Group + 3 LOD children + per-LOD culling |
| All hero prefabs | **Edit** — add LOD Group |
| All pet prefabs | **Edit** — add LOD Group |
| All NPC/Villager prefabs | **Edit** — add LOD Group |

---

## Acceptance Criteria

- [ ] LOD Group visible in Inspector on every character prefab
- [ ] LOD transitions are not visually jarring within normal camera range
- [ ] Profiler shows animator thread cost drops >50% when 10+ enemies are beyond 35 m
- [ ] No T-pose or missing-mesh errors at LOD2 distance
- [ ] `AnimatorCullingMode.CullCompletely` confirmed on LOD2 Animators in play mode

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `zero LODGroup under Assets/Prefabs` — no LODs on characters. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
