<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-26
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-26) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 515 — Tower Target-Selection Priority (defense-smart targeting)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Date:** 2026-06-26 · **Silo:** Combat/AI (code only) · **File:** `Assets/_Modules/Village/Buildings/TowerCombat.cs`
**Origin:** surfaced during the targeting-sweep verification — towers today are pure closest-pick. Design
contributed by Grok (grounded in the real `FindNearestTarget`/`IDamageable` API via the sync-pack brief).

## Problem
`TowerCombat.FindNearestTarget` picks the **closest** live hostile, full stop — the code itself notes
*"NO line-of-sight gate, NO defense/Heart priority today."* So a tower will happily plink a far trash mob
while a boss/elite walks past, and it wastes shots into friendly walls.

## Goal
Score-based selection among in-range, alive, hostile, `CanHit` enemies: **boss > high-HP/elite >
threat-to-core > closest (tie-break only)**, with a cheap LoS gate so a tower never fires into a friendly
wall. Preserve the existing air/ground `CanHit` matrix and the `LiveApexBoss` separate handling.

## Design (from Grok, reviewed — implement against the REAL API)
- Boss (`_wave.LiveApexBoss`) wins if in range + LoS (separate high-priority pool).
- Score terms (weights are bones; owner felt-tunes later):
  - HP/elite term (~60 pts) — **DESIGN DECISION OPEN:** Grok's pseudocode `(1 - hpFraction)*60` prioritizes
    **low-HP** (finishing blows), but his rationale text says "beefier first." Pick one: focus-fire the
    biggest threat (`hpFraction*60`) OR finish the nearly-dead (`(1-hpFraction)*60`). **Owner/PO call.**
  - Threat-to-core term (~40 pts) — enemy proximity to the defended core `Vector3` (pass it in as a param;
    resolve from `HeartController`/the tower's defended target).
  - Distance tie-break (~8 pts, inverse-square) — never dominates.
- LoS gate: `Physics.Linecast(_firePoint.position, target.WorldPosition + up*0.8f, _structureLayerMask, …)`
  — hard filter, rejects a target with a **friendly** structure/wall between. Mask to structures only so
  enemy colliders don't self-block.

## Implementation notes (real API — do NOT invent)
- Targets are `IDamageable` via `enemy.GetComponent<EnemyDamageable>()`; validity `IsAlive`, faction
  `Faction == CombatFaction.Hostile`, position `WorldPosition`, health `Hp`.
- `maxHp` is **not** on `IDamageable` — expose `MaxHp` on `EnemyDamageable` (or pass a per-enemy max) for
  the HP fraction; do not hardcode 100.
- Add a serialized `LayerMask _structureLayerMask` (friendly structures/walls) for the LoS Linecast.
- Allocation-free, runs on the fire tick only (not every frame). Keep `CanHit` as the first hard filter.
- Gate behind a feature flag (e.g. `ff.towertargetpriority`) — reversible, A/B against closest-pick.

## Acceptance
- With a boss + trash in range, the tower fires on the boss (LoS permitting).
- A wall between tower and target removes that target from selection (no wasted shot).
- Flag OFF = exact current closest-pick behavior (reversible).
- Deterministic oracle in `DataRegression` (mirror the structure-sweep oracle): construct enemies at
  varied HP/positions + a blocking wall, assert the scored pick.

## NOT in scope
Enemy-side targeting (that's `Enemy.cs` ff.enemystructureaware — separate). Tuning the exact weights/colors
(owner felt-pass after the mapping lands).
</content>

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
