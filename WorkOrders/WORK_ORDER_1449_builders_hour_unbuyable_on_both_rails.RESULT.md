# WO-1449 RESULT - builders-hour is on both purchase rails; the packs.json gate is still owed

**Status:** FIXED on the code rails, ONE ACCEPTANCE OPEN (the packs.json gate). Not yet deployed - the
vercel --prod push is held behind WO-1446.
**Commit:** `9fb58306f` (2026-09-06 20:11).
**Files:**
- `api/_lib/purchase-catalog.js:99` - `'builders-hour': 1.99` added to USD_ANCHORS, with the provenance
  comment at `:97`.
- `api/_lib/google-play-purchases.js:37` - `'builders-hour': 'consumable'` in PRODUCT_TYPES, comment at `:34`.
- `Assets/_Modules/Core/Payments/Providers/GooglePlay/GooglePlayProductCatalog.cs:29` - the SKU in the client
  catalog. Note the ticket named the path as `Assets/_Modules/Wallet/GooglePlay/...`; the file actually lives
  under `Assets/_Modules/Core/Payments/Providers/GooglePlay/`.
- `test/google-play-server-verification.test.js` - expectations updated for the new SKU.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current working
tree - the wave-two gate is owed. The node suites below are a separate rail and were run this session.

## What landed

The SKU that `9b47c9ad9` added to `packs.json` is now mirrored into the USD anchor table, into the Google
Play product-type table as a consumable, and into the client-side Play catalog. Nothing was hidden and
`packs.json` was not touched.

## Acceptance

- [x] `builders-hour` present in USD_ANCHORS and GooglePlayProductCatalog - `purchase-catalog.js:99`,
      `google-play-purchases.js:37`, `GooglePlayProductCatalog.cs:29`, all opened at source this session.
- [x] The two named node suites green - `node --test test/purchases.quote.test.js
      test/google-play-payment-provider-surface.test.js` run 2026-09-06 21:00: tests 41, pass 41, fail 0
      (the ticket predicted 31/32 and 7/9 failing at the old HEAD).
- [ ] The packs.json gate runs both suites, proven by a bogus SKU going red - NOT DONE. No gate in this repo
      invokes `node --test`; WO-1502's own Status line records the same gap. This is the one open item.
- [x] `node --test` green across `test/` - `npm test` run 2026-09-06 21:00: tests 424, pass 424, fail 0.

**Still owed:** no device capture is required for this ticket. What is owed is a `vercel --prod` deploy
(held behind WO-1446) and then a real purchase of `builders-hour` completing on each rail on the Seeker.
