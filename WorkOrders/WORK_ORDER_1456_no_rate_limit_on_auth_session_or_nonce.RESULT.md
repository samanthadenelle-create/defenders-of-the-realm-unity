# WO-1456 RESULT - nonce got the shared budget; /api/auth/session did NOT

**Status:** HALF DONE. One of the two routes the ticket names is covered. `api/auth/session.js` has no rate
limit at HEAD and none in the working tree.
**Commit:** `321b753c4` (2026-09-06 20:12), whose own subject says "nonce gets the shared IP budget" -
it never claimed the session route.
**Files:**
- `api/_lib/ip-budget.js:2` - the project's ONE per-caller-IP fixed-window budget, extracted from the promo
  rail as the ticket required rather than written a second time. Fail-closed and fail-open paths at
  `:85-88` and `:117-120`.
- `api/auth/nonce.js:34` - `const { reserveIpBudget } = require('../_lib/ip-budget');`, with the provenance
  note at `:41` naming WO-1440 as the origin of the helper.
- `api/promo/redeem.js:128-132` - the original UPSERT moved out to the shared helper, so there is exactly one
  limiter in the repo.
- `test/auth.nonce.budget.test.js` - the nonce cases.
- NOT CHANGED: `api/auth/session.js`. Grepped for `reserveIpBudget` and `ip-budget` this session: zero hits.
  Its only working-tree modification is the WO-1441 renewal cap (`git diff -- api/auth/session.js`, a
  `renewSession` block added after `:67`), which is an absolute-lifetime ceiling on a token chain and is not
  a rate limit on the route.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Neither log postdates `eb161dc98` or the working tree - the
wave-two gate is owed. This ticket is api-only, so the node rail below is the one that bears on it.

## Acceptance

- [ ] BOTH routes call the shared helper - HALF MET. `api/auth/nonce.js:34` does. `api/auth/session.js` does
      not, so the token-driven database write loop the ticket calls out is still unbudgeted.
- [ ] Per-IP AND per-wallet budget on the session route - NOT MET, for the same reason.
- [x] Refusal past budget AND a normal single call still succeeding - `node --test test/auth.nonce.budget.test.js`
      run 2026-09-06 21:00: tests 6, pass 6, fail 0. Both directions covered for the nonce route.
- [x] `node --test` green across `test/` - `npm test` run 2026-09-06 21:00: tests 424, pass 424, fail 0.

**Still owed:** no device capture. What is owed is the session-route half - the shared helper applied to
`api/auth/session.js` per IP and per wallet, with a success-path case - before this ticket can close.
