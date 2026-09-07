# WO-1453 RESULT - the signature rail verifies against reconstructed bytes; prod responses still unquoted

**Status:** FIXED IN CODE, TWO ACCEPTANCES OPEN (prod 200/401 not quoted; `api/referral/claim.js` still
carries the old 500 guard). Deploy held behind WO-1446.
**Commit:** `0f35490ad` (2026-09-06 20:42).
**Files:**
- `api/_lib/http.js:226-231` - `bodyBytesDetail(exactBytes)`, the ONE shared helper: returns `{}` when the
  bytes were read raw, and `{ bytes: 'reconstructed', reason: 'raw_body_reconstructed_bodyparser_active' }`
  when they had to be rebuilt. Rationale at `:199-220`. Exported at `:241`.
- `api/promo/redeem.js:296` - the tag merged into the auth-reject detail; `:206` reads via `readBodyExact`;
  the retired 500 guard is documented at `:255-267`.
- `api/game/save.js:253` - identical call, same helper; `:154` `readBodyExact`; note at `:215,233`.
- `test/auth.rawbody.session.test.js` - rewritten for the new behaviour.

**Choice named, as the ticket required:** RECONSTRUCT, not disable-the-body-parser. `http.js:211-214`
records why it is safe - the sha256 of the payload is bound into the signed message, so a wrong
reconstruction fails CLOSED as a 401 rather than opening a hole.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the working tree -
the wave-two gate is owed. This ticket is api-only, so the node rail below is the one that bears on it.

## Acceptance

- [ ] Signature rail returns 200 for a valid signature and 401 for an invalid one, PROD RESPONSES QUOTED -
      OPEN. Nothing is deployed; the only evidence is local. `node --test test/auth.rawbody.session.test.js`
      run 2026-09-06 21:00: tests 8, pass 8, fail 0. That is a local proof, not the prod proof the ticket asks for.
- [x] `save.js` checked and fixed through the same helper - `api/game/save.js:253`, read at source.
- [x] `node --test` green across `test/` - `npm test` run 2026-09-06 21:00: tests 424, pass 424, fail 0.

**Follow-up the tree confirms:** `api/referral/claim.js:129` still emits
`detail: { reason: 'raw_body_unavailable_bodyparser_active' }` on the old path. The WO Status line already
names this; it is unfixed at HEAD.

**Still owed:** no device capture. What is owed is the `vercel --prod` deploy and a real promo redeem on the
Seeker returning 200 on the signature rail, with the response quoted.
