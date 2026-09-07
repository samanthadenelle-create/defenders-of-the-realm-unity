# WO-1506 RESULT - the analytics row is bound to the server's view of the caller, never the client's claim

**Status:** FIXED SERVER-SIDE. The route no longer trusts a client-asserted wallet id. Two follow-ups are
named in the code and remain open. Not yet deployed - held behind WO-1446.
**Commit:** `f957bdbaa` (2026-09-06 20:34).
**Files:**
- `api/events/track.js:90-110` - the identity resolver. An `X-Session` header is checked against
  `wallet-auth.verifySession` and only the wallet the SERVER names is used; a valid `x-guest-id` (`:105`)
  binds to itself; anything else falls to the literal id `unverified` (`:81`, returned at `:110`). A broken
  auth table degrades to `unverified` rather than throwing, per the doc comment at `:90`.
- `:22-24` - the three-way mapping written down as canon: session to `_auth:'session'`, guest to
  `_auth:'guest'`, neither to `_auth:'unverified'`.
- `:72` - `const { reserveIpBudget } = require('../_lib/ip-budget');`, the SAME helper WO-1456 adopted for
  the nonce route. The comment at `:45` states that this is the project's one budget helper. It is applied
  fail-open on this rail, which is the right choice for analytics.
- `test/events.track.test.js` - 12 cases.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. The wave-two gate is owed. This ticket is api-only, so the node
rail below is the one that bears on it.

## Acceptance

- [x] A client-asserted wallet id is refused or overridden - `track.js:90-110`, read at source. The claim is
      discarded; the row takes the server's answer.
- [x] Anonymous events still land, under a server-decided id - the `unverified` bucket at `:81`/`:110`.
      Proven together with the refusal by `node --test test/events.track.test.js` run 2026-09-06 21:00:
      tests 12, pass 12, fail 0.
- [x] IP budget applied via the shared helper - `track.js:72`, the same `_lib/ip-budget.js` as the nonce route.
- [x] `node --test` green across `test/` - `npm test` run 2026-09-06 21:00: tests 424, pass 424, fail 0.

**Two open follow-ups, both recorded in the file header and both real:**
1. `:57-58` - no client sends `X-Session` or `X-Guest-Id` yet, so EVERY row currently lands as `unverified`.
   That is correct behaviour and useless data until the client WO lands (`EventTracker.cs:293`).
2. `:60-62` - `unverified` is not auto-excluded, so `api/admin/stats.js` will count it as one player in
   retention until `ANALYTICS_EXCLUDED_PLAYER_IDS=unverified` is set. The owner's funnel numbers are wrong
   in a new way until then.

**Still owed:** no device capture for the server half. What is owed is the deploy, the client header WO, and
the exclusion env var.
