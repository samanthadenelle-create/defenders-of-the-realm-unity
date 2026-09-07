# WO-1533 - The owner account bypasses promo guards (per-player limit, redemption cap, cooldowns)

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:05:59, build 2026.09.07.359076). PRIOR STATUS: FIXED - 2026-09-06: owner identity (MAINNET_CANARY_OWNER) bypasses per-player limit and redemption cap inside all four claim statements, still records + audits mode=owner-bypass; 11/11; UNPROVEN that the device redeems under that wallet (one SELECT on promo_redemptions closes it); deploy HELD behind WO-1446
**Silo:** api / backend (Node, Vercel serverless). No Unity, no scene, no client code.
**Minted:** 2026-09-06 (CLI_LANES_WO_NUMBERS.md, ninety-eighth pass; 1533 -> 1534 in the same edit)
**Owner ruling, verbatim (2026-09-06 20:45):** "im the one account that should have no guards"

---

## 1. What happened

The owner redeemed `LINK01` on device and got the client's line
**"You have reached the promo code limit for this account"**. That string maps to
`PLAYER_LIMIT_REACHED`, which `api/promo/redeem.js` step 5 returns when the player's count of
DISTINCT redeemed codes has reached the code's `per_player_limit`.

She is the operator. She authors these codes, and she is the one account that must be able to
redeem them repeatedly to test them. Her own guard rails were stopping her from testing the
feature she was shipping.

## 2. The ruling, scoped

The owner account - and ONLY the owner account, on a PROVEN wallet - is exempt from the
ANTI-ABUSE gates on `/api/promo/redeem`. Nothing else is weakened, on this route or any other.

## 3. What is bypassed, and what is deliberately NOT

**Bypassed for the owner:**

| Gate | Where | Why it may be bypassed |
|---|---|---|
| `max_redemptions` early-out | step 4 (`SELECT COUNT(*)`) | a global campaign cap is an abuse control, not a correctness one |
| `max_redemptions` atomic predicate | the claiming `UPDATE ... WHERE (max_redemptions IS NULL OR redemption_count < max_redemptions)` in all four grant statements at step 6 | **this is where the cap actually lives** (WO-1440). Skipping step 4 alone would change nothing. |
| `per_player_limit` | step 5 (`COUNT(DISTINCT code)`) | the gate that actually refused her |
| cooldowns | see below | |

**On cooldowns - measured, not assumed.** The only rate-shaped gates reachable from this file are
the step-5b IP budget (`reserveIpBudget`, guarded by `auth.unproven === true`) and
`wallet-auth.touchGuestRate` inside the guest rail. **Both are guest-only**, so a proven wallet
already never meets a cooldown here. Nothing new has to be skipped for that clause; it is
satisfied by the existing shape and this WO records that rather than inventing a branch.
The `/api/auth/nonce` budget (WO-1456) is a DIFFERENT route and is out of scope, untouched.

**NOT bypassed (so "no guards" is not over-read):**

- **Step 3 / `UNIQUE(code, player_id)`** - she still redeems each individual code ONCE. The
  ledger row is the record of the grant; making it optional would mean granting without a
  record. To re-test one code she deletes her row, or authors a new code.
  (`LINK01` was refused BEFORE step 6, so it is still unredeemed for her and will now work.)
- `active = false` / missing row -> `INVALID_CODE`
- `expires_at` -> `EXPIRED`
- `bound_wallet` mismatch -> `INVALID_CODE` (a private code bound to someone else stays theirs)
- the zero-reward and pack-sku REFUSED-UNBURNED backstops - these protect her FROM a burn,
  they are not guards against her.

## 4. Implementation

### 4a. `api/_lib/owner-identity.js` (NEW FILE)

Exports `isOwnerIdentity(playerId)` and `OWNER_IDENTITY`.

- **One authority for "who is the owner".** The project already names the owner wallet exactly
  once, as `MAINNET_CANARY_OWNER` in `api/_lib/purchase-catalog.js:32`, used by
  `walletAllowed()` (`purchase-catalog.js:185-196`) to let the owner buy any SKU on mainnet.
  This helper **imports that constant**. It must never carry a second copy of the address and
  must never grow into a list.
- **DEVIATION FROM THE DISPATCH, recorded per CLAUDE.md 11B.** The dispatch said "reads the same
  env var". **There is no env var** - measured: `grep -rn "OWNER_WALLET" api/` returns nothing,
  and the owner-wallet authority is a hardcoded `const`. Introducing `OWNER_WALLET` here would
  create the second source of truth the instruction exists to prevent, so the helper reuses the
  constant. If the owner later wants it env-driven, `purchase-catalog.js` is the one file to
  change and this helper follows for free.
- `api/_lib/wallet-auth.js` is **NOT edited** - it carries another lane's uncommitted work.
  The helper is a new file precisely so that constraint holds.
- `isOwnerIdentity` is an exact string comparison against the trimmed constant. A guest id
  (`guest-local-<64 hex>`) cannot collide with a base58 wallet, and the function returns false
  for null/empty/non-string.

### 4b. `api/promo/redeem.js`

1. `const { isOwnerIdentity } = require('../_lib/owner-identity');`
2. After the auth gate:
   `const ownerBypass = auth.unproven !== true && isOwnerIdentity(playerId);`
   **The `auth.unproven !== true` half is load-bearing** - the bypass is only ever reachable on
   a proven rail (an ed25519 signature over the exact bytes, or a session). It is not enough
   that a body claims the owner's id; `playerId` at that line has already been through
   `authenticatePromoRedeem`.
3. Step 4: `if (!ownerBypass && promo.max_redemptions != null) { ... }`
4. Step 5: `if (!ownerBypass && promo.per_player_limit != null) { ... }`
5. All four claim statements at step 6 carry `(${ownerBypass}::boolean OR max_redemptions IS
   NULL OR redemption_count < max_redemptions)`. Explicit `::boolean` cast - do not rely on the
   driver's parameter type inference inside an `OR`.
6. Every `capReached(...)` call is guarded with `!ownerBypass`, and the plain path's lost-claim
   branch answers `REWARD_UNAVAILABLE` (a fault, retryable, unburned) rather than
   `ALREADY_REDEEMED` when bypassing - for the owner the cap cannot be the reason it lost.
7. Step 7 audit: when `ownerBypass`, `logApiEvent(sql, playerId, 'promo_owner_bypass_redeem',
   { ..., mode: 'owner-bypass' })`. The redemption row is recorded exactly as for anyone else;
   nothing about the grant becomes invisible. A bypassed grant must be MORE visible, not less.

## 5. Acceptance criteria

`test/promo.owner-bypass.test.js`, driving the real handler with a faked Neon driver and a
stubbed auth result:

1. The owner wallet redeems a code whose `per_player_limit` is already reached -> `success: true`.
2. The owner wallet redeems a code whose `max_redemptions` is already reached -> `success: true`,
   and the claim statement carries the bypass boolean as `true`.
3. A NON-owner wallet, same fixtures, still gets `PLAYER_LIMIT_REACHED`, and its claim statement
   carries the bypass boolean as `false`.
4. A guest presenting the owner's id (`auth.unproven === true`) does NOT bypass.
5. The successful owner grant emits an audit event whose properties carry `mode: 'owner-bypass'`.
6. The helper is a single authority - the address is not re-typed in `owner-identity.js`.
7. Whole suite green: `node --test test/*.test.js`.

## 6. What NOT to touch

- `api/_lib/wallet-auth.js` (another lane's uncommitted work).
- Any other route. The wallet-only rule and every other guard stand everywhere else.
- The guest rail, the IP budget, `api/_lib/ip-budget.js`, `/api/auth/nonce`.
- No schema change, no migration.

## 7. Open / unproven

**UNPROVEN, and it decides whether this fixes her:** that the identity her device redeemed
`LINK01` under is `CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC`. That address is named "Owner
wallet" in `WorkOrders/MON_ACTIVATION_IMPLEMENTATION_HANDOFF_2026-08-22.md:62` and is the only
owner identity in the codebase, but nothing captured this session proves it is the id on the
refused request. One read closes it:

```sql
SELECT player_id, code, created_at FROM promo_redemptions ORDER BY created_at DESC LIMIT 20;
```

If her device redeems as a guest or a different wallet, this WO is a no-op for her and the
answer is to bind that identity - NOT to widen the bypass to guests.
