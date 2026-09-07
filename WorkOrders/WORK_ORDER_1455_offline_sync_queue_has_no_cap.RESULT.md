# WO-1455 RESULT - the queue latches on the crossing and coalesces to a cap; ungated

**Status:** IMPLEMENTED, UNGATED. Uncommitted in the working tree as of 2026-09-06 21:00, awaiting the
wave-two gate.
**Commit:** none. `Assets/_Modules/Core/State/GameStateService.cs` is modified in the working tree
(+449 lines against HEAD, `git diff --stat` read this session).
**Files:**
- `Assets/_Modules/Core/State/GameStateService.cs:2976-2991` - the depth warning now latches:
  `if (queue.Count >= OfflineQueueDepthWarn)` guarded by `_offlineQueueDepthWarned`, reset at `:2991` when
  the queue falls back below the threshold. The retired exact-multiple test and the 112-deep live session
  are recorded in the code at `:2961-2962`.
- `:3060` - `OfflineQueueDepthWarn = 25`; `:3062-3065` - `OfflineQueueMaxDepth`, the hard bound, enforced by
  coalescing rather than blind trimming (reasoning at `:2972`).
- `:3006` and `:3046` - the coalescer, and the `FlowTrace.Warn` fired when the cap is still exceeded after
  coalescing, so a drop is never silent.
- `:3128` - the latch is reset on drain as well.
- `Assets/Editor/Regression/BackendSaveAuthRegression.cs:99-115` - the WO-1455 oracle: the warning must
  latch per crossing (`:109`), the coalescer must keep the NEWEST marker per identity (`:112`), a cap must
  exist (`:114`), and a drop must carry a `FlowTrace.Warn` (`:115`).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or this uncommitted
change - nothing here has compiled or executed. The wave-two gate is owed.

## Acceptance

- [x] Depth warning fires once per crossing - `GameStateService.cs:2976-2991`, latch plus reset, read at source.
- [x] Queue bounded by coalescing, drops traced - `:3006`, `:3046`, `:3060-3065`, read at source.
- [ ] The regressions that DRIVE 24 -> 26 -> 24 -> 26 and enqueue 200 - NOT WRITTEN. What exists
      (`BackendSaveAuthRegression.cs:99-115`) is a SOURCE oracle: it asserts the code contains the latch, the
      cap and the trace. It never runs the queue. The ticket asked for behavioural cases; this is not that.
- [ ] `REGRESSION_OK n/n` on a fresh log - OPEN. The newest regression log says `REGRESSION_FAIL`.

**Still owed:** the wave-two gate, the two behavioural regressions named above, and a device capture showing
the crossing warning firing once on a real offline session instead of the silence at depth 112.
