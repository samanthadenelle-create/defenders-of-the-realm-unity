# WO-1081 RESULT - structure effects on build-palette tiles

## Result

Commit `84f91803a` landed the code, data, generated fallback, instrumentation, and oracle slice.
Build-palette tiles now render one bounded, ellipsised effect line without changing the immediate-arm
placement path. `CatalogEntry.description` is the authored authority; `StructureCardVM.DescriptionFor`
returns authored copy first and retains the per-type fallback with a permanent
`desc-unauthored-<id>` trace. The Town catalog authors distinct descriptions, including
`Yields Crystals each time a wave is cleared.` for `mine_crystal`, and the three locked
Resource rows are also authored so the all-Resource/Collector invariant is complete.

Both canonical catalog copies and both build-category copies are byte-identical. The Crystal Mine's
`crystal_producer` role is in the Town `Producers` group. The generated fallback was regenerated from
the canonical catalog and records SHA-256
`3573974c71021326164e1c80e7fab8fb0f2ff3b4ff4d218410bd9a736210214a`, 82,643 bytes, and 28 rows.
No structure id, display order, cost, yield, gesture, or quest file changed.

## Verification completed

- Fresh `CATALOG_FALLBACK_GEN_OK`.
- Fresh `COMPILE_GATE_OK`.
- Registered WO-1081 oracle green inside DataRegression:
  `[structure-descriptions] authored=17 resource/collector rows; 48-char cap, mine copy, authored projection, type-only fallback OK`.
- Canonical mirror hashes, source assertions, quest-file fence, and `git diff --check` green.

## Remaining acceptance and baseline failures

This work order remains `READY - PARTIAL`. Ops still owns the headed proof at two landscape aspects:
open both PNGs and confirm every Town tile's effect and cost remain readable and unclipped, the Crystal
Mine tile visibly says `Crystals` beside its 320 Wood / 200 Iron cost, and the capture trace reports
`Producers=4` with no `Other` bucket.

The full fleet marker was not green in the implementation worktree: DataRegression reported 265/279
registered suites green with 21 lifecycle/font-layout `NullReferenceException` failures in unrelated
HUD and night-market suites. The repository static gate also reported the pre-existing
`BattlePassLevelUpVfxBridge.cs` reflection-allowlist failure. These baseline failures are separate
from the green WO-1081 oracle and are not claimed as WO-1081 acceptance or silently closed here.
