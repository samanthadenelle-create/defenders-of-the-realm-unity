# WO-1454 RESULT - only 401/403 clears the session now; the gate that proves it has not run

**Status:** IMPLEMENTED, UNGATED. Uncommitted in the working tree as of 2026-09-06 21:00, awaiting the
wave-two gate.
**Commit:** none. `Assets/_Modules/Core/Web3/BackendRequestSigner.cs` is modified in the working tree
(+119 lines against HEAD, `git diff --stat` read this session).
**Files:**
- `Assets/_Modules/Core/Web3/BackendRequestSigner.cs:607-614` - `if (IsCredentialRefusal(req.responseCode))`
  emits `RenewSessionAsync action=clear status=...` and only then calls `ClearSession()`. The reasoning that
  the ticket asked for is in the code at `:601-606`.
- `:617-621` - the transient branch: `ScheduleRenewalRetry()` then
  `RenewSessionAsync action=keep status=... result=... transient (5xx / timeout / transport)`, naming the
  backoff seconds. The token is kept.
- `:569` backoff-skip keep, `:593` throw keep, `:635` empty-token keep, `:659` parse-failure keep - four more
  `action=keep` paths, all permanent FlowTrace per CLAUDE.md section 12.
- `Assets/Editor/Regression/BackendSaveAuthRegression.cs:78-97` - the WO-1454 oracle. It pins that only
  401/403 may clear (`:90`), that the classifier still names 401 and 403 (`:92-93`), and that a 5xx does not
  clear (`:97`).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or this uncommitted
change - so NOTHING has compiled or exercised the code described above. The wave-two gate is owed.

## Acceptance

- [x] 500/503/timeout leave the token intact and schedule a retry; 401/403 clear it - `BackendRequestSigner.cs:607-621`,
      opened at source.
- [ ] Regression covering all four status classes, RED proof stated - the suite EXISTS
      (`BackendSaveAuthRegression.cs:78-97`) but has never executed: it postdates `reg-quiet.log`. No RED proof
      has been captured, so this reads as written-but-unproven.
- [ ] `REGRESSION_OK n/n` on a fresh log - OPEN. The newest regression log says `REGRESSION_FAIL`.

**Still owed:** the wave-two compile plus regression gate, and then a device capture showing a
`RenewSessionAsync action=keep status=5xx` line surviving a server hiccup without cloud save going dark.
