# WO-1453: the wallet SIGNATURE rail 500s on promo redeem - raw_body_unavailable_bodyparser_active

**Status:** READY TO IMPLEMENT
**Silo:** `api/promo/redeem.js` + `api/_lib/http.js` (+ audit `api/game/save.js` for the same guard).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1453 -> 1454 in the same edit). WO-1440's RESULT explicitly deferred
this to "its own WO" and none was ever minted.

## 1. EVIDENCE

```
api/promo/redeem.js:300-303      throws raw_body_unavailable_bodyparser_active
api/_lib/http.js:172             the guard that cannot see the raw bytes
```

Prod proof, `WORK_ORDER_1440_..._blocker.RESULT.md:225-252`:

```
RAIL 1 (signature) -> HTTP 500
```

Signature verification needs the exact request bytes; the platform body parser has already consumed the
stream, so the rail throws rather than verifying. Only the session rail works on prod today, which is why
WO-1440's proof went through that rail and the signature rail was never re-tested.

## 2. FIX SHAPE

- Verify against the bytes RECONSTRUCTED from the parsed body using the same canonical serialization the
  client signs, or disable the body parser for this route so the raw stream survives - whichever the platform
  supports; name the choice in the RESULT.
- Tag the failure detail so a 500 here is distinguishable from a genuine bad-signature 401.
- Audit `api/game/save.js` for the identical guard and fix both through one helper, not two copies.

## 3. WHAT NOT TO DO
- Do not remove the signature rail because the session rail works. It is the rail a fresh device uses.

## 4. ACCEPTANCE
- [ ] The signature rail returns 200 for a valid signature and 401 for an invalid one (prod responses quoted).
- [ ] `save.js` checked and fixed or documented as unaffected, with the file:line read this session.
- [ ] `node --test` green across `test/`.
