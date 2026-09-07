# WO-1459: device frame floor - 26 fps median, 11 fps worst in a raid, with timeScale at 1.00

**Status:** READY TO IMPLEMENT (instrument first; NO fix before the data names the cost)
**Silo:** Perf. Read-only profiling lane; touches nothing until the measurement lands.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1459 -> 1460 in the same edit).

## 1. EVIDENCE

237 `LOW fps` samples in the 2026-09-06 device session. Worst:

```
LOW fps=11 ms=87.4 mem=427MB gc=26MB scene=RaidBase_raider_camp_small towers=0 enemies=13
```

`timeScale=1.00` on 1,189 lines - so this is a real frame cost, not the frozen-clock class (WO-988) and not
the `timeScale=0.28` trap that produced a wrong theory on 2026-09-03.

Thirteen enemies and zero towers at 87 ms a frame is not an enemy-count problem.

## 2. FIX SHAPE

- Profile ONE raid on device with a capture attached. Do not edit code first (CLAUDE.md sec.12).
- Named suspects, in order, to be confirmed or eliminated by the capture:
  1. the `ProbeForStructure` log storm with stack frames (WO-1450, same batch) - 320 stack walks/second;
  2. 14 of 24 VFX loop slots held by `ArcaneTower_Aura` (WO-1473, same batch);
  3. per-frame `ProbeForStructure` raycasts from `Enemy:Update()`.
- Then fix the one the data names, and only that one.

## 3. WHAT NOT TO DO
- Do not lower quality settings to raise the number. That hides the cost and ships a worse-looking game.
- Do not act on any of the three suspects above before the capture; two of three are cheap to fix and would
  produce a false "fixed" if the real cost is the third.

## 4. ACCEPTANCE
- [ ] A device profile capture is attached and the dominant cost is NAMED with its measured ms.
- [ ] A fix targeting that cost, with before/after fps from the same raid.
- [ ] `REGRESSION_OK n/n` on a fresh log.
