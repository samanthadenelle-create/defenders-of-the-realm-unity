# WO-1495 RESULT - every allowlist block carries WO plus date plus remove-by, and a ratchet suite holds it

**Status:** FIXED for the annotation half and the ratchet. The GROWTH half of the ratchet is
deliberately NOT built and is named below.
**Commit:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files:**
- `Assets/Editor/Regression/AllowlistExpiryRegression.cs` (new, untracked, 18353 bytes) - the ratchet.
  Its header at `:7-41` states the contract: every exemption entry carries a WO pointer, an ORIGIN
  date `YYYY-MM-DD`, and an expiry `remove-by YYYY-MM-DD`, and the remove-by must not already be in
  the past. The origin-date test strips every `remove-by <date>` clause FIRST (`:27-29`), so an
  annotation carrying only an expiry cannot satisfy the origin check with the expiry's own date.
- `Assets/Editor/Regression/DataRegression.cs:1746` - registered as the `allowlist-expiry` suite under
  `Guard.Try`. A suite file no entry point runs is worse than no suite, so registration is the half
  that makes it real.
- Annotated blocks, measured this session: 15 files under `Assets/Editor/Regression/` now carry
  `remove-by` clauses, 29 annotations in total. The four largest the ticket named are all covered -
  `MageAbilityIconRegression.cs` (2), `EnemyPoolResetRegression.cs` (4),
  `UiObsidianConformanceRegression.cs` (1), `ShaderPredicateSingleAuthorityRegression.cs` (2).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the ratchet has NEVER RUN and the wave-two gate is owed.

## What landed

The exemption is now a worklist rather than a hiding place, which is the shape
`ManagePortraitCoverageRegression` (WO-1487) already had and the reason the ticket used it as the
contrast. A block with a date somebody chose gets re-read on that date; a block with none never
expires and the suite reports green forever on exactly the content it was written to cover.

## Acceptance

- [x] The named blocks carry WO plus date plus remove-by - 15 files, 29 annotations, measured at source.
      The ticket's count was thirteen blocks; the measured file count is fifteen, and the discrepancy
      is not reconciled here.
- [ ] The ratchet suite exists, but the RED proof by adding a bare entry is NOT stated, and the suite
      has never executed. The suite file exists at `AllowlistExpiryRegression.cs` and is registered at
      `DataRegression.cs:1746`.
- [ ] The GROWTH half - failing when a block grows - is explicitly OUT OF LANE per the suite's own
      header at `:48`. Section 2 of the ticket asked for it. It is not built.
- [ ] `REGRESSION_OK n/n` on a fresh log, with any newly-red suites fixed or ticketed. Owed with the
      wave-two gate; whether annotating the blocks turned any suite red is UNKNOWN until it runs.

Needs no device capture. It needs the wave-two regression gate so the ratchet executes for the first
time, the RED proof, and a decision on the growth half.
