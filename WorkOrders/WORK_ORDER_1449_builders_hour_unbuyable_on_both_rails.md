# WO-1449: the builders-hour pack is unbuyable on BOTH rails and two node suites fail at HEAD

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:00:42, build 2026.09.07.359076). PRIOR STATUS: FIXED - 2026-09-06: builders-hour mirrored into USD_ANCHORS + google-play PRODUCT_TYPES (consumable) + GooglePlayProductCatalog; node --test 345/345 green; needs a vercel --prod (HELD behind WO-1446)
**Silo:** `api/_lib/purchase-catalog.js` + `Assets/_Modules/Wallet/GooglePlayProductCatalog.cs` + the two node
suites. Reopens the shipped half of WO-1388 (CLOSED 2026-09-06).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1449 -> 1450 in the same edit).

## 1. EVIDENCE

Commit `9b47c9ad9` added the SKU to the canonical catalog:

```
Assets/StreamingAssets/Data/Canonical/packs.json   builders-hour  storeVisible  featured  usd 1.99
```

It is absent from both purchase rails:

```
api/_lib/purchase-catalog.js:83-110              USD_ANCHORS - no builders-hour
GooglePlayProductCatalog.cs:20-21,36             no builders-hour
```

At HEAD:

```
node --test test/purchases.quote.test.js                       31 of 32 fail on 'builders-hour'
node --test test/google-play-payment-provider-surface.test.js   7 of 9 fail
```

WO-1388 was closed on an owner felt-test of the STORE CARD. The card renders; the purchase cannot complete on
either rail.

## 2. FIX SHAPE

- Mirror the SKU into `USD_ANCHORS` (usd 1.99) and into `GooglePlayProductCatalog` as a CONSUMABLE, matching
  the shape of the neighbouring resource packs.
- Add both node suites to the gate that signs off any `packs.json` change, so a SKU added to the canonical
  catalog and to neither rail fails the gate instead of the store.

## 3. WHAT NOT TO DO
- Do not hide the card to make the tests pass. The owner approved the pack.
- Do not hand-edit `packs.json` in text mode (memory `canonical-json-edits-binary-only-verify-newlines`).

## 4. ACCEPTANCE
- [ ] `builders-hour` present in `USD_ANCHORS` and `GooglePlayProductCatalog` (file:line in the RESULT).
- [ ] `node --test test/purchases.quote.test.js` and `test/google-play-payment-provider-surface.test.js`
      both green; paste the counts.
- [ ] The packs.json gate runs both suites; prove it by adding a bogus SKU locally and showing the gate red.
- [ ] `node --test` green across `test/`.
