# WO-1448 RESULT - the cloud apply is recency-gated and both branches trace; the gate run is owed

**Status:** IMPLEMENTED, UNGATED. Uncommitted in the working tree as of 2026-09-06, awaiting the
wave-two compile + regression gate.
**Commit:** none. `Assets/_Modules/Core/State/GameStateService.cs` is `M` in `git status`;
`Assets/Editor/Regression/CloudLoadRestoreRegression.cs` is untracked (new, 335 lines).
**Note:** written by the board-reconciliation lane from the ticket + the `WO-1448` diff hunks; the
implementing lane left no RESULT.

## 1. WHAT LANDED

- **A local-save recency stamp.** `GameStateService.LastLocalSaveUnixMs` (`:201`), unix-ms of the most
  recent LOCAL save. One writer, `StampLocalSaveClockFromEnvelope` (`:539-558`), parses the envelope's
  `exportedAt` inside a `Guard.Try` and degrades to a `FlowTrace` warning (`:558`) rather than failing an
  otherwise valid load. Two callers (Save + Load), so it survives a process restart.
- **The apply is a public seam.** `ApplyBackendState(...)` (`:2239`) takes the three values
  `LoadFromBackend` parses off the response (called at `:2181`) and RETURNS a `BackendApplyOutcome`
  (`:2188`) rather than only logging.
- **The recency gate** (`:2256-2278`), all three rules stated in-code: server newer -> APPLY; equal
  vintage -> SKIP; server undated -> APPLY only when this device has never saved (the reinstall case
  WO-1447 serves), otherwise SKIP.
- **Both branches trace permanently** (CLAUDE.md sec.12, never strip):
  `:2273` - `backend load: server=... local=... winner=LOCAL ... NOTHING applied (local resources + town
  untouched, WO-1448)`; `:2384` - the matching `winner=SERVER - APPLIED the full row` line.
- **Beyond the ticket:** a fail-closed identity check (`:2280`) rejects the whole row when the server
  row's `boundWallet` names a different owner, because `ApplyPersisted` would otherwise install it.
- **Oracle:** `Assets/Editor/Regression/CloudLoadRestoreRegression.cs` (new, untracked, 335 lines),
  markers `CLOUDLOAD_RESTORE_OK` / `CLOUDLOAD_RESTORE_FAIL`, registered at `DataRegression.cs:1697`. It
  asserts against `ApplyBackendState`, not `LoadFromBackend`, and says why at `:29-38`: the transport is
  a live UnityWebRequest and would be flaky evidence; the defect was entirely in the apply.

## 2. ACCEPTANCE

- [x] Recency gate in place, both branches traced - `:2256-2278` and `:2384`.
- [x] Two regression cases: newer-server -> APPLIED is case A (`:171`, `now + 60000`), older-server ->
      skipped is case B (`:229`, `now - 60000`); undated variant `:242`, identity refusal `:275`.
      **RED proof is REASONED, NOT OBSERVED** - the suite says so at `:42-49`; it could not run Unity.
- [ ] `REGRESSION_OK n/n` on a fresh log - **OWED.** No gate run exists for this change.

## 3. STILL OWED

The wave-two compile + regression gate on a fresh log, judged by the marker, not an exit code. Until
then this is proven only by reading the source.
