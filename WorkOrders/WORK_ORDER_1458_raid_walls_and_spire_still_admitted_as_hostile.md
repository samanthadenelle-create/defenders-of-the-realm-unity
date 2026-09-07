# WO-1458: raid walls and the spire crown are still admitted to the hostile target set, 320 times a session

**Status:** READY TO IMPLEMENT
**Silo:** `Assets/_Modules/Village/Combat/` targeting admit rule + the raid base prefab collider layers.
Reopens the class WO-1047 closed on 2026-08-22.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1458 -> 1459 in the same edit).

## 1. EVIDENCE

Device log, 320 hits between 12:59:00.878 and 14:37:11.366:

```
[hostile-admit] NON-ENEMY ADMITTED TO THE HOSTILE TARGET SET (WO-1047).
  path='RaidBase_raider_camp_small/Wall_Outer_*' impl=DeNelle.Village.WallSegment
```

and

```
ADMITTED-VIA='.../RaidSpire/Crown' layer='Enemy'
```

The second line names the mechanism: a CHILD collider sits on layer `Enemy`, so the parent structure is
admitted through it. The WO-1047 guard is doing its job - it is logging, not blocking - and nobody has acted
on what it logs since it was closed.

## 2. FIX SHAPE

- Fix the child colliders' layer on the raid base prefabs (`Wall_Outer_*`, `RaidSpire/Crown`) so a structure
  never presents an `Enemy`-layer collider. Prefer this over widening the admit rule.
- Then promote the WO-1047 guard from a warning into a HARD oracle: a regression that scans the raid base
  prefabs and FAILS on any non-enemy component reachable through an `Enemy`-layer collider.

## 3. WHAT NOT TO DO
- Do not special-case `WallSegment` in the admit rule. The layer is wrong; patching the consumer leaves the
  next structure with the same bug and the guard blind to it.

## 4. ACCEPTANCE
- [ ] A full raid device session records ZERO `[hostile-admit]` lines.
- [ ] The prefab-scan oracle exists and goes red when a child collider is put back on `Enemy`.
- [ ] `REGRESSION_OK n/n` on a fresh log.
