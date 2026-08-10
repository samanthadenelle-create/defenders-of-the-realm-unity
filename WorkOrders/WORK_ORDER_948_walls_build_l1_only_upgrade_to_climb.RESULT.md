# RESULT — WO-948: walls build at L1 only; wood→stone rides the upgrade verb

**Verified:** 2026-08-10 (CLI orchestrator; implementing agent + gates)

- Enforcement is DATA: `build-categories.json` Walls row `lockedIds: ["wall_stone"]` (both copies
  byte-equal) + the registry fallback mirror; the palette's existing lockedIds filter does the rest
  (now FlowTraced instead of a silent skip). Exactly one placeable wall card: wall_wood.
  `gate_stone` untouched. BaseLayout replay proven palette-independent (`BaseLayoutLoader.cs:287` —
  saved stone walls render/sell unchanged).
- The rung LANDED per-segment through the EXISTING upgrade verb: `wall_wood` maxLevel 3→2 with
  `upgradeVisualPath` → the stone model; `wall_stone` maxLevel 1 + `wallTierBase: 1`
  (structures-catalog v15→16, both copies). `WallTierData.CurrentWallLevel` now DERIVES from
  BaseLayout via a min rule (weakest wall is the breach — CoC-consistent), capped at stone
  (`MaxReachableWallLevel = 1`; WO-904 lifts it behind raid-steal). `WallSegment.SetTier` applies
  walls.json targetHeight to the blocker (PlacedStructure-scoped; editor bake tools untouched).
- Regression: `WallBuildL1Regression` `[wall-build-l1]` — GREEN in the 2026-08-10 11:38 run
  (placeable set, replay survival, rung authored + capped, derive math + mitigation + height).
  Committer follow-ups applied: DataRegression registration + the BuildEconomyRegression wall
  fixture level 3→2.
- Owner felt-verify: the Castle Structures tab shows one wall; upgrading a placed wood wall turns it
  stone; heart mitigation follows the WEAKEST placed wall.
