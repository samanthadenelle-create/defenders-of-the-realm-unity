**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 442 — Build mode: validate pay per wall segment (no free segments)

**Status: READY TO IMPLEMENT** (owner 2026-06-17). Editor-closed (gate + felt-test).
Lane: Build Mode / Economy. Economy-integrity bug — a wall drag must not hand out segments unpaid.

## Problem
Single-structure placement is safe: `BuildModeController.Place()` (BuildModeController.cs:715-752,
WO-131) re-checks `CanAfford(cost)` AT commit and `ChargeLedger(cost)` atomically — never spawns unpaid.
BUT a **wall DRAG** lays a RUN of N segments in one gesture. The risk (owner: "validate TryPay before
giving each wall segment, or adjust value for the whole wall and try"): the drag-fill loop places
segments WITHOUT routing each through `Place()`'s validate+charge — so you can drag a long wall and get
more segments than you paid for (or only the first is charged).

## Fix — CHOSEN UX (owner 2026-06-17): whole-wall cost, red ghost when unaffordable
As the player drags a wall, the ghost shows the WHOLE run's running cost (perSegment × N). While the
total is affordable, the ghost reads normal/green = buildable. **The moment the drag total exceeds the
player's funds, the ENTIRE wall ghost turns RED = UNBUILDABLE — the commit is blocked.** The player drags
back (shorter wall) until it's green/affordable, then releases to build. On commit, a single
`TrySpend(total)` charges the whole run atomically and places all N segments. No partial walls, no free
segments — the red ghost IS the "you can't afford this" signal.

## Implementation
1. Find the wall-drag / line-fill path (likely `BuildModeController` drag-commit or
   `DesktopBuildInput`/`LeanTouchBuildDriver`/`IBuildInput` → the run between drag-start and drag-end) and
   the ghost preview (`GhostPreview`/`BuildPaletteUI`).
2. As the drag grows, compute `total = CostFor(_armed) × segmentCount`; check `CanAfford(total)` each frame.
3. **Tint the whole wall ghost RED when `!CanAfford(total)`** (and block release/commit while red);
   normal/buildable tint when affordable. Reuse the existing ghost invalid/blocked tint if one exists.
4. On a valid (green) release: `TrySpend(total)` ONCE (atomic), then place all N segments (route through
   `Place()` per cell with the charge already taken, or place + single charge — don't double-charge).
   Keep the entry armed (CoC re-place).
5. Show the running total + an "unaffordable" hint on the ghost/HUD so the red reads as a money issue
   (vs an invalid-placement red — keep them distinguishable or share with a tooltip).

## Acceptance
- [ ] Compile gate green; owner felt-test a wall drag.
- [ ] Dragging a wall shows the running total; when the drag exceeds funds the WHOLE wall ghost turns RED
      and cannot be committed; dragging back to affordable returns it to buildable.
- [ ] On commit, the whole wall is charged once (atomic) — no free segments, no double-charge.
- [ ] Single-structure placement unchanged (still WO-131 validate+charge).
- [ ] No way to obtain a wall segment without a corresponding ledger charge.

## What NOT to touch
- Don't change `Place()`'s single-placement WO-131 path. Don't change non-wall structures. §0: Windows path.

*Cross-ref:* `BuildModeController.cs:715-752` (Place/WO-131), build-mode input drivers, F8 note "build
broken cannot select unit to place" (separate — verify build-mode selection too).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
