# WO-1502 RESULT - npm test runs and two endpoints are covered; CORS and the RPC guard were not done

**Status:** PARTIALLY FIXED. Two of five acceptance criteria met. The CORS wrapper and the loud
`SOLANA_MAINNET_RPC_URL` failure were in the fix shape and are absent from the tree.
**Commit:** `f957bdbaa` (2026-09-06 20:34) for the scripts block, `game/load` tests and env docs;
`321b753c4` (2026-09-06 20:12) for the nonce tests.
**Files:**
- `package.json:9-11` - `"scripts": { "test": "node --test test/*.test.js" }`. The broken `node --test test/`
  form is gone; `npm test` now exists.
- `test/game.load.test.js` - 11 cases for the load path WO-1447 implicates.
- `test/auth.nonce.budget.test.js` - 6 cases for the nonce path WO-1452 implicates.
- `docs/ACCESS_AND_SECRETS.md:76-79` - `COMMUNITY_SHOWCASE_VOTING_ENABLED`, `INSTALL_BRAG_CRYSTALS`,
  `SOLANA_RPC_URL` and `SOLANA_DEVNET_SKR_MINT` documented with a source file:line each.
  `:81-83` - `GOOGLE_PLAY_ACCOUNT_BINDING_KEY` and `GOOGLE_PLAY_PACKAGE_NAME` are described in a note that
  explicitly places them OUTSIDE this lane's scope, awaiting the owner.
- `docs/CLI_OPERATIONS_RUNBOOK.md` - the quoted command corrected in the same commit.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. The wave-two gate is owed. Separately, and this ticket is the one
that owns the fact: NO GATE IN THIS REPO INVOKES `node --test`. The 424 tests below pass only because a seat
ran them by hand.

## Acceptance

- [x] `npm test` runs the suite and the runbooks quote it - `package.json:9-11`, run 2026-09-06 21:00:
      tests 424, pass 424, fail 0.
- [x] `game/load` and `auth/nonce` covered - 11 and 6 cases respectively, both green this session.
      The untested-endpoint count before and after was NOT restated by the lane, so the ticket's "23" is
      unverified at HEAD; treat it as unmeasured rather than reduced.
- [ ] Unset `SOLANA_MAINNET_RPC_URL` fails loudly - NOT DONE. `api/purchases/verify.js:50` still reads
      `String(process.env.SOLANA_MAINNET_RPC_URL || '').trim() || null`, which silently disables mainnet
      verification exactly as the ticket describes.
- [ ] CORS wrapper on showcase / leaderboard / profile - NOT DONE. Counted this session: `applyCors` is
      absent from `api/leaderboard/get.js`, `api/leaderboard/submit.js`, `api/profile/get.js`,
      `api/profile/social.js`, `api/profile/username.js`, `api/showcase/get.js`, `api/showcase/top.js` and
      `api/showcase/vote-counts.js`. Four showcase routes have it; eight routes do not.
- [x] All six env vars in `ACCESS_AND_SECRETS.md` - four documented, two deliberately deferred with the
      reason written down at `:81-83`.

**Still owed:** no device capture. What is owed is the CORS pass, the loud RPC guard, a stated
untested-endpoint count, and a gate that actually runs `node --test`.
