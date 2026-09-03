# Vercel log findings 2026-09-03 - two silent server-side defects

Source: owner-supplied Vercel log export, 3,253 rows, project defenders-of-the-realm-v2,
2026-09-03T12:39Z. She said: "These are the logs. Not sure if it helps." It did.

## Status distribution
200: 2900 | 204: 185 | 304: 98 | **400: 66** | blank: 4
Error-level rows: 21 - ALL the same benign Node DEP0169 `url.parse()` deprecation warning,
no application errors. So the 400s are the whole story.

## DEFECT 1 - /api/entitlements rejects EVERY guest player. 53 of 66 calls -> 400.

Every failing request carries a guest id:
    playerId=guest-local-c7d9ec10a396e985cc4a05527768ae6ea873fc77...  -> 400
(the other 13 return 204, i.e. fine.)

ROOT, read at source: `api/entitlements.js:17` calls `validatePlayerId`
(`api/_lib/sku-entitlement-read.js:21`), which requires `isProvenValueId`
(`api/_lib/wallet-auth.js:145`):

    function isProvenValueId(id) { return isWalletId(id) || isPlayId(id); }

A `guest-local-` id is NEITHER, so it throws `PLAYER_ID_BAD_SHAPE` -> 400.

⭐ AND THE CODE'S OWN COMMENT ADMITS THE MISMATCH. `wallet-auth.js:152` documents that
error code as:
    PLAYER_ID_BAD_SHAPE:  // neither a base58 wallet nor A GUEST-LOCAL ID
The comment describes a rule that ALLOWS guest-local ids. The function does not implement
it. One of the two is wrong and they have been disagreeing in silence.

⚠ DO NOT "FIX" THIS BY WIDENING `isProvenValueId`. That predicate is the membership rule
for the PROVEN-IDENTITY rail - its docblock says "proven-by-somebody-else is the membership
rule" (wallet = ed25519 signature, play = Google token + server HMAC). A guest id is proven
by NOTHING, which is exactly why it is excluded. Widening it would silently admit unproven
ids to every consumer of that predicate, including grant-bearing paths. The right question
is whether a guest should be asking for entitlements at all, and if so, through what.

## DEFECT 2 - /api/catalog/collection fails 11 of 11. A 100% failure rate.

    collectionId=build-defenses&clientVersion=2026.09.03.352921    -> 400
    collectionId=build-protection&clientVersion=2026.09.03.352921  -> 400

Every call is a BUILD CAROUSEL collection and every one fails. `readCollection`
(`api/_lib/catalog-read.js`) throws a `CatalogError`, which `collection.js:22` maps to 400.
The response body carries `code`, so the specific reason is recoverable - the log export
does not include response bodies, so READ THE CODE or reproduce the call to get it.

⚠ Likely the same family as PROD-020 (Build Trade collection showed Store only,
Weaponsmith + Armorer wrongly under Crafting). Check before treating it as new.

## WHY BOTH MATTER MORE THAN THE STATUS CODES SUGGEST

**Both fail SILENTLY to the player.** No error on screen, no crash, no visible symptom -
the client asks, gets a 400, and renders whatever its empty state is. The owner's log export
is the ONLY place either was visible, and neither had a ticket. This is the same class as
the R2 push proving the previous build, PRODUCTION_ALIAS_MATCH proving the wrong project,
and the edge serving a stale shell: **an honest system, reporting cleanly, about something
one step away from what the player experiences.**

## Not actioned

Found mid hero-migration; deliberately not fixed in that window. Neither is in the critical
path for the current build. Both need their own ticket and the collection one needs its
response `code` read before anyone theorises (CLAUDE.md sec.12).
