# WO-1533 RESULT - the owner wallet bypasses the per-player limit and the redemption cap, keyed on the one existing owner authority

**Status:** FIXED and COMMITTED. Deploy HELD behind WO-1446. Unproven that the device redeems under that wallet.
**Commit:** `0cce7d12d` (2026-09-06 20:57:08 -0500).
**Files:** `api/_lib/owner-identity.js` (NEW, +61), `api/promo/redeem.js` (+108/-11),
`test/promo.owner-bypass.test.js` (NEW, +352, 21 cases), `CLI_LANES_WO_NUMBERS.md` (banner bumped in the same
edit, 1533 -> 1534), and the ticket itself.
**Gates:** `node --test` **11/11 GREEN** on the owner-bypass suite (RED 3/10 before the fix), backend suite
**435/435**, both quoted in the commit body. No Unity gate applies - api-only, no client code, no scene. For
completeness: `Builds/cg-quiet.log` `COMPILE_GATE_OK` (20:04) and the RED `Builds/cg-aab.log` (20:54) are unrelated
to this lane and neither is a proof of it. **`435/435` is a `node --test` count, NOT `REGRESSION_OK`.**

## 1. What landed

**One authority for "who is the owner".** `api/_lib/owner-identity.js` exports `isOwnerIdentity(playerId)` and
imports `MAINNET_CANARY_OWNER` from `api/_lib/purchase-catalog.js:32` - the single place the project already names
the owner wallet, used by `walletAllowed()` (`purchase-catalog.js:185-196`) to grant an exemption of exactly the
same shape. It re-types no address and must never grow into a list.

**Recorded deviation, per CLAUDE.md sec.11B.B.** The dispatch said the helper should "read the same env var".
Measured: `grep -rn "OWNER_WALLET" api/` returns nothing - the owner identity is a hardcoded `const`. Adding an
`OWNER_WALLET` env var would have created the second source of truth the instruction exists to prevent, so the
constant is reused. If it should become env-driven later, `purchase-catalog.js` is the one file to change.

**The bypass is bound to a PROVEN identity, not a typed string.** `redeem.js` computes
`const ownerBypass = auth.unproven !== true && isOwnerIdentity(playerId);` - an unauthenticated caller who simply
types the owner's address into `playerId` gets nothing. Guests (`guest-local-<64 hex>`) cannot collide with a
base58 wallet and always return false. The bypass is applied INSIDE the claiming UPDATEs - e.g.
`if (!ownerBypass && promo.max_redemptions != null)` - not as a pre-check, so the cap stays atomic for everyone
else. The grant is still RECORDED and AUDITED with `mode: 'owner-bypass'` (step 7).
`api/_lib/wallet-auth.js` was deliberately NOT edited - it carries another lane's uncommitted work, and the helper
is a new file precisely so that constraint holds.

## 2. Acceptance

- [x] The owner's account is not refused by the per-player limit or the redemption cap - `redeem.js` steps 5/6,
      21 cases in `test/promo.owner-bypass.test.js`, 11/11 green.
- [x] No second owner list - one import from `purchase-catalog.js:32`, asserted by the suite.
- [x] Guests never bypass; an unauthenticated caller asserting the address never bypasses - the
      `auth.unproven !== true` conjunct.  [x] The bypass is auditable - `mode: 'owner-bypass'` on every grant.
- [ ] **UNPROVEN: that the device redeems under that wallet.** One `SELECT` on `promo_redemptions` for the owner's
      `player_id` after a device redeem closes it. Related device evidence already in the tree (WO-1441 RESULT
      sec.4, F8 seq 4686, build 358574): a LINK01 redeem on the Seeker failed at
      `SignMessage ... authorization request failed` - the wallet app refused, so the identity the server would
      have seen on that attempt was never established.
- [ ] Deployed to prod - **HELD** behind WO-1446.

## 3. Owed

WO-1446 lands, then deploy, then one device redeem of LINK01 under the owner wallet with the `promo_redemptions`
row read back and the `mode: 'owner-bypass'` audit event quoted.
