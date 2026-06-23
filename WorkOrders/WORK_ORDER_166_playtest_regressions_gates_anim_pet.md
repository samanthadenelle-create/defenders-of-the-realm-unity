# WORK ORDER 166 — Playtest Regressions: missing gates, walk animation, mid stairs, pet T-pose

**Status: READY TO IMPLEMENT**
**Priority:** HIGH — playtest blockers from the 2026-05-30 build (owner video). Some are regressions from the recent castle/animator bakes.
**Date:** 2026-05-30
**Lane:** mixed — `VillageSceneBuilder` (gates/stairs, architect single-writer) + animation code (walk/pet). CLI; UI spec.
**Source:** owner playtest video (2026-05-30 23:36) — four callouts below.

---

## The four callouts (owner video)

### 1. ⚠ Gates MISSING at all points (worse than WO-158 "impassable")
Owner: *"north gate and all gates missing."* Earlier (WO-158) the gates were *present but impassable*;
now the **gate structures aren't appearing at all**. Likely causes (CLI verify):
- **Polyperfect gate prefab not loading** — `Gate_Medieval_Medium/Small` load via `AssetDatabase` and
  log a warning + skip if missing (polyperfect is **gitignored** — may not be imported on the build
  machine; re-run `Defenders/Art/Fix Polyperfect URP Materials` / re-import). If the prefab is null, the
  gate silently doesn't spawn → "all gates missing."
- OR a recent bake/skip-guard (WO-150/157 strip lists) over-matched and removed gate objects.
- **North gate** was never built by design (WO-158 owner decision = add it) — so north needs adding
  regardless; but if **all four** are gone, it's the prefab-load / strip issue above, not just north.
**Fix:** confirm the gate prefabs load (import check) + the gate-build path runs; **add the north gate**
(WO-158: mesh gap + split north barrier + north drawbridge). Verify all 4 gates render + are passable.
**This is the top blocker — no gates = can't exit + the castle reads broken.**

### 2. Walk/locomotion animation not playing ("skip walking animation")
Owner: *"skip walking on animation"* — the hero (and/or NPCs) slide without a walk cycle. Same family as
the **WO-163 AmbientNPC animator-param spam** + the **WO-140 Humanoid-rig change**: the controller is
being driven with a param it doesn't have, OR the locomotion clip/param isn't wired, so `Speed` never
animates the walk. **Fix:** ensure the hero/NPC/pet animator controllers have the locomotion param the
code drives (`Speed`) and a walk state bound to it; guard `SetFloat` with `HasParameter` (WO-163). Verify
the hero plays a walk cycle when moving. **Reconcile with WO-140/163 — likely the same fix.**

### 3. Pet still in T-POSE
Owner: *"pet still in T pose."* `Pet.cs` does `GetComponentInChildren<Animator>()` and drives
`Speed/Attack/Hit/Dead` (hashes "must match AnimatorSetup.cs's names"). T-pose = **no controller assigned**
(or a controller lacking those params) on the pet mesh after the recent rig/animator changes. **Fix:**
ensure the pet rig gets its shared controller (route through the animator factory / `AnimatorSetup` pet
controller) so `Speed` etc. resolve; same `HasParameter`-guard + controller-assignment fix as #2/WO-163.
Verify the pet idles/moves animated, not T-posed.

### 4. Stairs in the MIDDLE (misplaced rampart stairs)
Owner: *"the stps in middle"* — the rampart climb stairs (`BuildRamparts` Ramp/Stairs, WO-136) are
appearing in the **middle of the village** instead of against the wall. Likely the stair/ramp placement
coords (`Ramp-South/North/East/West` at `±6` offsets from the wall edge) are resolving to interior
positions, or the stair visual prefab seats at the wrong origin. **Fix:** reposition the rampart stairs
flush **against the wall** (interior side, at the wall line), not mid-courtyard; confirm they still link
ground→walkway on the NavMesh.

---

## Priority within this WO
1. **Gates (#1)** — top blocker; can't exit/play without them. (Folds in WO-158's north-gate add.)
2. **Walk anim (#2) + Pet T-pose (#3)** — same animator-param root cause; fix together (with WO-163).
3. **Stairs placement (#4)** — visual/nav correctness, lower urgency.

## Reconcile / overlaps
- **#2/#3 = WO-163 + WO-140** (animator param contract) — do as one animation-fix pass; don't double-implement.
- **#1 = WO-158** (gates) — this supersedes/absorbs it with the "missing entirely" severity + prefab-load check.
- All scene changes → `VillageSceneBuilder` single-writer + a rebake (editor closed). Brace-gate.

## Acceptance criteria
1. All 4 gates (N/E/W/S) **render** and are **passable** (prefabs load; north gate added).
2. Hero plays a **walk cycle** when moving (no sliding); animator params resolve (no Hash-missing spam).
3. **Pet is animated** (idle/move), not T-posed.
4. Rampart stairs sit **against the wall**, link ground→walkway on NavMesh — not mid-courtyard.
5. No animator `Parameter 'Hash …' does not exist` spam (ties WO-163).
6. Brace balance; single-writer builder; editor-closed rebake; spawn→Heart + interior→exterior paths intact.

## Done checklist (CLAUDE.md §10)
- [ ] 4 gates render + passable (prefab-load confirmed; north added)
- [ ] Hero walk anim plays; pet animated (not T-pose); no Hash-missing spam (WO-163/140 reconciled)
- [ ] Rampart stairs against the wall, nav-linked
- [ ] Brace balance; editor-closed rebake; paths verified
- [ ] `WORK_ORDER_166_playtest_regressions_gates_anim_pet.RESULT.md` when complete
