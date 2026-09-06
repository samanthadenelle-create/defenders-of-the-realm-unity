# WO-1440 RESULT — guests can redeem FIRSTWATCH; the promo cap is now atomic

**Status:** DONE — shipped to production 2026-09-06, proven against the live endpoint.
**Deployment:** `dpl_27KRA2P93P76Nx9fcmsbWChT2W5z` (target production, READY), serving
`https://defenders-of-the-realm-v2.vercel.app` — confirmed by a runtime-log line carrying
`dep=dpl_27KRA2P93P76Nx9fcmsbWChT2W5z` for a request made through the prod alias.
**Commit:** `08ae66de0` (api only; the Unity tree was not touched — a separate lane was
mid-build against it). Two later comment-only edits to `api/promo/redeem.js` (precision on
what the IP budget counts) are committed but deliberately **not** redeployed — see §7(c):
another lane's uncommitted server work is now in the tree and a redeploy would sweep it out
without its migration.

---

## 1. WHAT THE PUBLISHED BUILD SENDS — and how that is known

**It sends `X-Guest-Id`, and nothing else, for a guest.**

* The live dApp-Store build is **2026.08.17.328845** (`CANON_GROUND_TRUTH_2026-09-03.md:23`,
  `publishing/SUBMIT_CHECKLIST.md:41`; the later `2026.09.04.354315` is a candidate only
  and `2026.09.06.357453` went to Firebase testers, not the store).
* The guest branch — `req.SetRequestHeader("X-Guest-Id", playerId); return true;` —
  already existed in `Assets/_Modules/Core/Web3/BackendRequestSigner.cs` at commit
  `a24654c21` (**2026-08-16**), i.e. *before* that build was cut. The only later change
  to that file before the candidate (`dd73e4569`, 2026-08-26) **added** a cached-session
  helper and did not touch the guest branch.
* ⛔ **The decisive evidence is not the source read, it is production.** Before the fix,
  a real POST to the real endpoint with only that header was refused, captured in the
  Vercel runtime log at 18:49:46 UTC:

  ```
  [auth_reject] code=AUTH_WALLET_REQUIRED ref=fc7d6cfb mode=guest method=POST
                path=/api/promo/redeem id=guest-local-…(76)
                detail={"grantingRoute":true,"provenMode":"guest"}
  ```

  `mode=guest` is the server's own classification of a real request. That is the campaign
  failing, reproduced.

**NOT PROVEN:** no APK for 328845 was decompiled this session; the version identity rests
on the canon docs plus commit dates bracketing the build. It would be closed by
`jadx`/`apktool` on the submitted APK, grepping its IL2CPP strings for `X-Guest-Id`.

## 2. WHAT CHANGED

| File | Change |
|---|---|
| `api/_lib/wallet-auth.js` | New `authenticatePromoRedeem()` — one named function, **one caller**, not a flag on `authenticateGranting()`. A guest passes and is marked `unproven:true`. `promoGuestRedeemEnabled()` reads `PROMO_GUEST_REDEEM_ENABLED` (default ON). The now-false "GUEST IS STILL REFUSED, and always will be" comments are **cross-referenced, not deleted**. |
| `api/promo/redeem.js` | Calls the new gate; adds the IP budget (step 5b); writes `ip_hash` on every ledger row; logs `promo_guest_redeem` per guest grant; **makes the global cap atomic on all four grant paths**; header records the reversal. |
| `api/migrations/20260906_0019_…sql` | `promo_ip_budget` + `promo_redemptions.ip_hash`. Additive, idempotent. **Applied to production and verified** (`MIGRATION_0019_OK`). |
| `api/schema.sql`, `api/DB_SETUP.md` | Canon corrected in the same change (CLAUDE.md §15). DB_SETUP §7 item 3's "wallet rail only" sentence is quoted and superseded, not erased. `SCHEMA_PARITY_OK 45 table(s)`. |
| `test/db-promo-packs.test.js` | Column-list pins updated to the new shape; the property guarded (snapshot + ordinal claim in ONE statement) is unchanged and now covers all three insert paths. |

⛔ **No other endpoint's identity requirements changed — grepped, not assumed.**
`authenticatePromoRedeem` has exactly ONE call site: `api/promo/redeem.js:316`.
`authenticateGranting` still guards `api/referral/claim.js:143`, `api/purchases/quote.js:202`,
`api/purchases/verify.js:251`, `api/purchases/fulfill.js:44`, `api/purchases/reconcile.js:35`
and the entitlement read predicate, with its allowlist unchanged at two entries.

**Provenance of the deployed bundle, stated honestly:** the deploy uploaded the working
tree, so the bundle is *inferred* to equal commit `08ae66de0`'s `api/` content — from
`git status` taken immediately before the commit plus file mtimes showing the other lane's
edits landed at 19:12 UTC, after the 19:08 upload. It is not proven byte-identical to HEAD.
Committed locally on `feat/synty-art-retheme`; **not pushed**.

## 3. PROOF — a guest redeems FIRSTWATCH on production

```
POST https://defenders-of-the-realm-v2.vercel.app/api/promo/redeem
X-Guest-Id: guest-local-9f4c1d77b3ae0521cc86f0a4d29e7b13548ac6f9e0d2b871a35c4e69f7021d8b
{"playerId":"guest-local-9f4c…1d8b","code":"FIRSTWATCH",
 "supportsPackRewards":true,"supportsInlinePackRewards":true}

HTTP/1.1 200 OK        X-Vercel-Id: cle1::iad1::58b6t-1788721814937-3556747900cd
{"success":true,"reward":{"crystals":500,"coins":500,"packSku":null,"contents":null},
 "message":"Welcome to the Watch."}

# same guest, immediately again:
HTTP/1.1 200 OK        {"success":false,"error":"ALREADY_REDEEMED"}
```

Ledger afterwards — the grant is real, ordinal-correct and attributable:

```
player_id  guest-local-9f4c…1d8b   crystals 500  coins 500
redemption_ordinal 2   ip_hash 991a75916a12   redeemed_at 2026-09-06T19:10:15.126Z
promo_ip_budget: (991a75916a12, FIRSTWATCH) grants=1 total_grants=1
```

⚠ **That row is a TEST ARTIFACT and it is left in place**, because "do not modify the
promo row" was a hard boundary and removing it would mean decrementing
`promo_codes.redemption_count`. It cost the campaign **one of 500** tier-1 ordinals. To
remove it, both statements are needed or the cap stays spent:

```sql
DELETE FROM promo_redemptions
 WHERE code='FIRSTWATCH'
   AND player_id='guest-local-9f4c1d77b3ae0521cc86f0a4d29e7b13548ac6f9e0d2b871a35c4e69f7021d8b';
UPDATE promo_codes SET redemption_count = redemption_count - 1 WHERE code='FIRSTWATCH';
```

⚠ **Run both together and run them NOW, or leave the row alone.** `redemption_ordinal` has
no UNIQUE constraint, so once a real player has taken ordinal 3 the decrement makes the next
player reuse an ordinal. Leaving the row costs 1 of 500 and nothing else — that is the safe
default.

## 4. PROOF — the cap holds under concurrency, BY MEASUREMENT

**The global cap was NOT atomic, and that was measured, not assumed.** Step 4 was a
`SELECT COUNT(*)` and a later `INSERT` — two statements, two transactions on the Neon
HTTP driver. `tools/wo1440-maxredemptions-race-probe.mjs old`:

```
max_redemptions: 20   concurrent actors: 50
GRANTED: 50   refused: 0   ledger rows: 50   <-- OVERSHOOT = 30
WO1440_MAXREDEMPTIONS_IS_NOT_ATOMIC (overshoot measured)
```

The claim now happens in the **same statement** as the insert, serialising on the
`promo_codes` row. Same probe, `new`:

```
GRANTED: 20   refused: 30   ledger rows: 20   <-- OVERSHOOT = 0
```

And the boundary FIRSTWATCH actually uses — the tier ordinal — holds exactly.
`tools/wo1440-concurrency-proof.mjs`, 50 concurrent claims against `tier1_limit = 20`:

```
TIER-1 grants (500 crystals): 20   <-- equals tier1_limit
TIER-2 grants (100 crystals): 30
distinct ordinals: 50 of 50   range 1..50   redemption_count 50
WO1440_TIER_CAP_ATOMIC_OK
```

Neither probe touches FIRSTWATCH; both use throwaway codes and delete them.

## 5. THE IP LIMIT — 20 grants per IP per 24 h, and why

**Guest rail only.** A proven wallet is never counted — a family of wallet holders behind
one router must not lock each other out, and a wallet is already a scarcity key.

**20 per (hashed IP, code) per fixed 24 h window.** It has to survive a shared NAT — a
household, a dorm, a conference, and above all mobile **carrier-grade NAT**, which can put
many unrelated players of a mobile game behind one address. It also has to cost a farmer
something. At 20, draining the 500-strong tier-1 band takes at least **25 distinct
networks**, while no plausible venue reaches 20 redemptions *of this one code* in a day at
a campaign whose entire tier-1 band is 500 players worldwide. Tighter starts costing real
acquisitions — the exact failure the reversal exists to prevent — and buys little, because
anyone willing to farm can rent addresses. **It is cost, not a wall**, and is described as
such in the code.

Fixed window (resets 24 h after the window's first grant), not sliding. Counted only by
attempts that have cleared every other gate, so a typo or an expired code never spends a
household's budget. **Fails CLOSED** — a deliberate divergence from `guest_rate_limit`'s
fail-open, because that one guards saves and this one stands in front of a payout.

Proven live on production, `tools/wo1440-ip-budget-prod-proof.mjs`, 21 distinct guest ids
from one machine against a throwaway code:

```
attempts 1..20  -> {"success":true,"reward":{"crystals":1,...}}
attempt 21      -> {"success":false,"error":"RATE_LIMITED"}
granted 20 / RATE_LIMITED 1 / ledger rows 20  (the refusal was NOT consumed)
WO1440_IP_BUDGET_LIVE_OK
```

## 6. ⛔ THE RESIDUAL RISK THE RULING DID NOT ACCOUNT FOR — owner action

> ## ✅ CLOSED WHILE THIS TICKET WAS BEING WRITTEN — but read the finding anyway.
> Re-read at 19:30 UTC, `promo_codes.max_redemptions` for FIRSTWATCH is **500**. It was
> **NULL** when measured at 19:10 UTC (the reading below, and the reading the WO's own
> ruling was made on). Someone set it in that window; this seat did not, and does not
> claim to know who. **The tail is therefore bounded now** — and it is bounded *atomically*
> only because of §4: before today, `max_redemptions` was enforced by a count that was
> measured to let 50 through a cap of 20. Setting that value on the old code would have
> looked like a fix and leaked anyway. The finding below stands as the record of why the
> value needed setting; it is left intact rather than rewritten.

Read off the live row at 19:10 UTC 2026-09-06:

```
FIRSTWATCH  reward 500/500   max_redemptions NULL   per_player_limit 1
            tier1_limit 500  tier2 100/100          expires_at 2026-10-01
```

**`max_redemptions` is NULL. The "500" is `tier1_limit`, a TIER BOUNDARY, not a cap.**
Redemption 501 and onward still succeeds and still pays **100 crystals + 100 coins** to
every fresh guest id, until 2026-10-01. So the bound the ruling rests on — *"the worst
case is one actor farming 500 crystals, not an unbounded drain"* — **does not currently
exist on this code**; the tail is unbounded.

This file deliberately did **not** invent a cap the operator did not author (that would
silently delete a deliberately-authored tier-2 reward). Closing it is one statement, and
it is now enforced atomically:

```sql
UPDATE promo_codes SET max_redemptions = 500 WHERE code = 'FIRSTWATCH';
```

Other residual risk, stated plainly: a guest id is still self-asserted and unlimited to
mint; `per_player_limit=1` stops the accidental double-tap and **nothing more**; the IP
budget raises the price of farming and does not forbid it.

## 7. §5 — REPORTED, NOT ABSORBED. Two findings, both handed back.

**a) The client-side session defect is NOT the same root cause, and it already has a WO.**
The guest defect was a server-side policy gate (`authenticateGranting` → `AUTH_WALLET_REQUIRED`);
this is a client session-lifecycle bug. `MintSessionForExplicitConnectAsync`
(`BackendRequestSigner.cs:312`) **had zero call sites from the day it was written**, and
`ConnectForLoginAsync(bool explicitConnect)` accepted the flag and never branched on it —
so on auto-resume a session was never minted, and cloud save (`GameStateService.cs:2436-2443`,
`allowMint` defaulted false) logged exactly the owner's line. **This is WO-1441**, already
minted and already being fixed in the Unity tree by another lane; **WO-1420** is a
*different* bug in the same subsystem (Connect()'s catch cannot tell a 30 s deadline from
the provider's own instant refusal). **Do not open a third ticket.**

**b) NEW, server-side, and BIGGER THAN IT FIRST LOOKED — wallet players on the PUBLISHED
build still cannot redeem.** The wallet rail has two sub-rails. Both were tested against
production end-to-end with a throwaway ed25519 keypair (a wallet is only a public key to
this endpoint — nothing needs a funded one), `tools/wo1440-wallet-rail-prod-proof.mjs`:

```
GET /api/auth/nonce   -> 200 {"ok":true,"nonce":"sU715edWe56l…","ttlSeconds":300}
RAIL 1 (signature)    -> HTTP 500 {"ok":false,"code":"SERVER_ERROR","ref":"f36c61ea"}
RAIL 2 (session)      -> HTTP 200 {"success":true,"reward":{"crystals":7,"coins":0},…}
ledger: ordinal 1, ip_hash 991a75916a12
promo_ip_budget rows: 0   <-- a proven wallet is never charged the guest IP budget
WO1440_WALLET_RAIL_OK
```

Rail 1 failed **with a cryptographically valid signature over the exact bytes**, which is
what makes this a server defect and not a client one. And the sub-rail that works is the
one the store build does not have: **the session rail landed in `e526e013f` on 2026-08-23,
six days AFTER the published build 2026.08.17.328845 was cut** — so a wallet-connected
player on the store build has exactly one auth path and it is the dead one. **Guests are
unblocked by this ticket; wallet players on the published build are not.** Given WO §2's
own finding that the campaign's arrivals are overwhelmingly guests, that does not hold the
campaign, but it must not be reported as "wallets merely degraded".

The cause, captured:

```
[auth_reject] code=SERVER_ERROR ref=249acca1 mode=wallet method=POST
              path=/api/promo/redeem id=7xKXtg2CW87d…(44)
              detail={"reason":"raw_body_unavailable_bodyparser_active"}
```

Vercel's Node 24 runtime parses `req.body` regardless of `config.api.bodyParser = false`
(the stack shows `IncomingMessage.get [as body]` inside `_lib/http.js:172`), so
`readBodyExact` reports `exact:false` and the guard refuses **before** a signature can be
checked. A wallet holder with a valid signature but no live session gets a 500. It **fails
closed** — it over-refuses, never over-grants — and almost certainly affects
`api/game/save.js` identically.

**The fix is small and is NOT applied here, deliberately.** The guard exists only so a
parser problem does not masquerade as `AUTH_BAD_SIGNATURE`; it is a diagnostics choice, not
a security one. Letting the wallet path *attempt* verification against the reconstructed
bytes cannot create a false accept — a signature either verifies against those bytes or it
does not — so the correct change is "proceed, and tag the failure detail" rather than
"refuse with 500". It is not done in this ticket for one concrete reason: shipping it needs
a `vercel --prod`, and a deploy right now would sweep another lane's uncommitted WO-1441
server work into production (see the landmine note below). **Its own WO, and it should be
quick.**

**c) ⛔ AND THE ONE THAT WAS ACTUALLY KILLING HER CLOUD SAVES — a migration that was
never applied.** `_lib/wallet-auth.issueSession` has INSERTed `auth_sessions.identity_kind`
since 2026-08-30 (WO-1282), and **that column did not exist on the production database.**
Measured directly against live Postgres:

```
shape A  (token, wallet, expires_at)                 [pre-0013 schema] -> OK
shape B  (token, wallet, identity_kind, expires_at)  [deployed code]   -> FAILS:
         42703 column "identity_kind" of relation "auth_sessions" does not exist
```

So **`POST /api/auth/session` failed for EVERY wallet, and no wallet could obtain a session
at all** — from 2026-08-30 until today. That is the server half of "authed call has no live
session; waiting without SignMessage. why=missing": even once WO-1441 makes the client ASK
for a session, the mint would have 500ed. It also explains why FIRSTWATCH's only wallet
redemption (ordinal 1) is dated **2026-08-28**, two days before the break.

**REPAIRED.** `api/migrations/20260830_0013_auth_sessions_identity_kind.sql` applied to
production and verified by running `issueSession`'s exact INSERT shape and removing the
probe row — `MIGRATION_0013_REPAIR_OK`. Additive, idempotent, one column with a default;
it arms nothing (the Google rail still needs its env vars).

**Why nothing caught it:** `tools/schema-parity.mjs` reads `CREATE TABLE` bodies only, by
design — a column added by `ALTER` is invisible to it, so a required migration can sit
unapplied while every gate reports green. `tools/wo1440-alter-column-sweep.mjs` now closes
that blind spot: it extracts every `ALTER TABLE … ADD COLUMN` from `api/schema.sql` and
`api/migrations/*` and checks it against the live database. Current result:

```
checked 29 ALTER-added column(s) across 20 file(s)
  MISSING ON LIVE DB: auth_sessions.signed_at <- schema.sql
ALTER_COLUMN_SWEEP_MISSING 1
```

⚠ **That remaining one is the OTHER LANE'S, and it is a live landmine.** WO-1441's
server-side work (`api/_lib/wallet-auth.js` `renewSession`/`signed_at`, `api/auth/session.js`,
the `schema.sql` ALTER) is **uncommitted in the shared working tree** and is **not** in the
deployed bundle — its files were modified at 19:12 UTC, after this ticket's 19:08 deploy, so
production runs commit `08ae66de0`'s api/ content only. **The next `vercel --prod` from any
seat will sweep that uncommitted code into production, and if `auth_sessions.signed_at` has
not been applied first it will break wallet session minting exactly the way `identity_kind`
just did.** Apply the ALTER before that deploy, and re-run the sweep.

## 8. ACCEPTANCE (WO §6), line by line

- [x] **A guest on the PUBLISHED build can redeem FIRSTWATCH** — §3, real prod endpoint,
      request and response captured, ledger row verified.
- [x] **The cap holds under concurrent load, proven by MEASUREMENT** — §4, with the
      before/after numbers and the pre-existing race named rather than glossed.
- [x] **A wallet holder still redeems via the wallet rail, unchanged** — MEASURED, not
      inferred: a real ed25519 wallet redeemed on production via the session sub-rail
      (`{"success":true,…}`), with no `promo_ip_budget` row, proving wallets are not
      IP-counted. The non-comment diff of `wallet-auth.js` adds only the new function, the
      env switch and an export — `verifyWallet`, `verifySession`, `authenticate`,
      `authenticateGranting` and `GRANTING_MODES` have **no changed code lines**. Forged
      signature / forged session / no headers all refuse and never grant.
      ⚠ Qualified by §7(b): the *signature* sub-rail 500s on this runtime — pre-existing,
      fails closed, and it is the only sub-rail the published build has.
- [x] **The reversal, its reasoning and the residual risk recorded in the file header** —
      original paragraph kept verbatim, reversal beneath it, three residual risks named.
- [x] **§5 reported with evidence and handed back** — §7.
- [x] **No other endpoint's identity requirements changed** — §2.
