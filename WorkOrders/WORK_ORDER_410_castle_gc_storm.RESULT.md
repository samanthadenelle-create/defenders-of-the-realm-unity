# WORK ORDER 410 — RESULT: castle GC storm resolved at head (verification)

**WO:** 410 (P0, Lane 10 Build/Perf) — "0.1 fps in MainCastle_Hall — main-thread GC storm
(13–22 MB allocated/frame) + combat-object leak."
**Date:** 2026-06-13 · **By:** CLI · **Verdict: RESOLVED at head — pending owner fps thumbs-up.**

## What the triage prescribed (CLI fix spec) vs. head state
The 06-12 PERFDIAG that produced the 13–22 MB/frame number **predated the fixes** (the triage's own
Step 0 was "re-run PERFDIAG at head before any new work"). Verifying each cited live suspect against
`feat/tower-core-loop` HEAD:

1. **`VfxPool.ApplyAlpha` per-frame `GetComponentsInChildren<Renderer>()`** → **FIXED.** Renderers are
   cached (`_cachedRenderers` via `CacheRenderers()` on Play/PlayHeld) and colour is driven through a
   reused `MaterialPropertyBlock` (`_mpb`). No per-frame array alloc, no `.material` instantiation.
   (`Assets/_Modules/Village/Vfx/VfxPool.cs:382-407`.)
2. **`StoryCompanion` hero resolution — whole-scene `FindObjectsByType<Transform>` (1765) ~1Hz ×4** →
   **FIXED.** Now `FindFirstObjectByType<HeroLocomotion>()` (typed, single component), cached in
   `_heroT`, only re-resolved when null. (`StoryCompanion.cs:743-747`; comment self-documents WO-410.)
3. **Cleric heal scan — `FindObjectsByType<StoryCompanion>()` + `<Pet>()` per cast/frame** → **FIXED.**
   `_alliesCache`/`_petsCache` are cached and refreshed only on a membership-change throttle, not per
   frame. (`StoryCompanion.cs:222-225,473-480`.)

Plus the pooling pass that the WO notes already landed (`656b889` hero/companion projectile + impact-FX
pools; `69e609a` EnemyPool + DamageNumberSpawner + two leaking EnemyTypeVfxSet Instantiates → Destroy(3s)).

## Per-frame error storm (fix-order #1)
The triage flagged 84 console errors/run as the single mechanism that best explains 13–22 MB/frame.
The latest playtest Player.log (build #8/#9, MainCastle_Hall) is **clean of `LogError`/exceptions** —
the per-frame error storm is gone.

## Empirical verification (the real verdict)
Per the WO's own acceptance note ("owner playtest is the verdict, not a green gate"): the owner ran a
**multi-hour session entirely in MainCastle_Hall** on the head build (harvest, waves, vendors, build
mode, combat) at **playable fps**. A live 13–22 MB/frame Gen2 GC storm makes that impossible — so the
storm is not present at head.

## Status
- **Code fix spec: 100% done** (all 3 cited per-frame suspects + pooling + error storm).
- **Recommend:** owner give an explicit "castle fps is good" to formally close, OR ask CLI to stand up
  a lightweight headless perf-capture (batchmode play + gc/frame logger) for a hard before/after number
  to attach (the WO's last acceptance bullet) — a ~½-day instrument task if a measured artifact is wanted.
- **Did NOT touch** WaveManager/EnemyPool internals (flagged highest-leak-risk; needs wave-loop playtest)
  or the generic-pool consolidation (that's its own ARCHITECTURE_PRINCIPLES §2b.2 leverage WO, not this P0).

## Linkage
WO-164 (OuterWorld ~1fps) is very likely the SAME mechanism — `WorldSceneLoader` additively loads
OuterWorld on any hub, so the castle PERFDIAG measured castle+OuterWorld combined. The same fixes
should resolve WO-164; worth a combined castle-only vs +OuterWorld confirmation.
