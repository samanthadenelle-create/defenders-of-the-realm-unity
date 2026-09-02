# Database Setup — Defenders of the Realm v2 (Neon Postgres)

Run notes for standing up the backend database that `api/schema.sql` defines.
This is **infra setup only** — it creates tables. The serverless functions that
read/write them live in `api/` (two exist today; the rest are listed below as
**TODO — function not yet written**).

---

## 1. Prerequisites

1. **A Neon project + database.** Sign in at <https://console.neon.tech>, create
   a project (or reuse the existing one), and open a database (the default
   `neondb` is fine).
2. **The connection string.** Neon gives you a `postgres://...` URL. The
   functions read it from the env var **`DATABASE_URL`** — confirmed in:
   - `api/game/save.js` → `const sql = neon(process.env.DATABASE_URL);`
   - `api/game/load.js` → `const sql = neon(process.env.DATABASE_URL);`
   Use the **pooled** connection string for serverless (Neon's dashboard labels
   it "Pooled connection"). The HTTP driver `@neondatabase/serverless` works
   with either, but pooled is correct for Vercel's many short-lived invocations.
3. **Vercel project env var.** In the Vercel dashboard for the deployment
   (`defenders-of-the-realm-v2`): **Settings → Environment Variables → add
   `DATABASE_URL`** = the Neon pooled connection string, for Production (and
   Preview if you test there). Redeploy after adding it.
4. **npm deps for the functions** (already noted in `save.js`'s header):
   `@neondatabase/serverless` and `@msgpack/msgpack`. Ensure they're in the
   Vercel project's `package.json`.
5. **npm deps for wallet-signature auth (WO-120 §D — NEW):** `tweetnacl` and
   `bs58`. Used by `api/_lib/wallet-auth.js` to verify the Solana/ed25519
   signature on every save/load. Add both to the Vercel project's
   `package.json` and redeploy:
   `npm install tweetnacl bs58`
   > **WALLET-SCHEME ASSUMPTION (flagged):** the verify path is Solana/ed25519
   > because the whole client is Solana (base58 wallet = player_id, Solana Pay,
   > Solana tx sigs). If the chain is ever EVM, swap the verify helper for
   > `ecrecover` — the `auth_nonces` table + challenge flow are scheme-agnostic.

---

## 2. Run the schema (exact steps)

1. Open the Neon console → your project → **SQL Editor**.
2. Open `api/schema.sql` from this repo, **select all, copy**.
3. **Paste** into the SQL Editor and click **Run**.
4. It is **idempotent** (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT
   EXISTS`, seed uses `ON CONFLICT DO NOTHING`) — safe to re-run any time, and
   safe to run against a DB that already has the original `player_data` table.

> The `player_data` block is **unchanged** from the original schema, so running
> this will not disturb existing saves.

---

## 3. Verify each table exists

Run this in the SQL Editor after the schema runs:

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
```

Expected eight tables:

```
analytics_events
bug_reports
player_data
promo_codes
promo_redemptions
referral_claims
referrals
tower_swaps
```

Spot-check the seed promo code:

```sql
-- Expect ZERO rows on a plain apply (2026-08-18: the TEST10 seed is opt-in — see
-- note 9). TEST10 appears only if you ran `SET dotr.seed_test_codes = 'on';` first.
SELECT code, reward_crystals, active FROM promo_codes;
```

Check indexes landed:

```sql
SELECT indexname FROM pg_indexes WHERE schemaname='public' ORDER BY indexname;
```

---

## 4. Endpoint → table map (what writes/reads what)

| Endpoint | Method | Table(s) | Function in `api/`? | Client source |
|---|---|---|---|---|
| `/api/game/load` | GET | `player_data` | **Yes** — `api/game/load.js` | `GameStateService.LoadFromBackend` |
| `/api/game/save` | POST | `player_data` | **Yes** — `api/game/save.js` | `GameStateService.SendDelta` |
| `/api/events/track` | POST | `analytics_events` | **No (TODO)** | `Core/Analytics/EventTracker.cs` |
| `/api/promo/redeem` | POST | `promo_codes` (read), `promo_redemptions` (write) | **No (TODO)** | `Core/Promo/PromoCodeService.cs` |
| `/api/referral/generate` | POST | `referrals` (upsert) | **No (TODO)** | `Core/Referral/ReferralService.cs` |
| `/api/referral/claim` | POST | `referrals` (read+bump), `referral_claims` (write) | **No (TODO)** | `Core/Referral/ReferralService.cs` |
| `/api/tower-swap/log` | POST | `tower_swaps` | **No (TODO)** | `Village/Buildings/TowerSwapService.cs` |
| `/api/bug-report` | POST | `bug_reports` | **No (TODO)** | `HUD/HelpMenu.cs` |

**"No (TODO)"** = the Unity client already POSTs to this URL, but the matching
serverless function does **not exist in `api/` yet**. The tables are defined now
so that when each function is written it drops straight onto an aligned schema.
Only `game/save.js` and `game/load.js` exist today.

---

## 5. Per-table field provenance (column ⟷ client payload)

### `player_data` (unchanged)
Upsert merges the client's full camelCase `PersistedState` snapshot (+ `playerId`)
into `game_state` JSONB; `save.js` promotes a whitelist (crystals, food, coins,
voidshards, stone, iron, wood, towers, towerAbilities, bestWave, pets, ownedPets,
starterPetId). `load.js` reads those keys back out as `data{...}`.

### `analytics_events`
Client sends `{ events: [ { playerId, eventName, properties, clientTs } ] }`.
`properties` arrives as a **JSON string** — the function should `::jsonb`-parse
it into the `properties` column. `clientTs` is **unix MILLIS**. One row per event.

### `promo_codes` (operator-populated catalog)
Not written by the client — the **owner inserts codes by hand**. Columns drive
the redeem response (`reward_crystals`/`reward_coins`/`message`) and the gates
(`active`, `max_redemptions`, `per_player_limit`, `expires_at`).

### `promo_redemptions`
Written by redeem on success: `(code, player_id, crystals, coins)`. `UNIQUE
(code, player_id)` = one-time-use. Client sends `{ playerId, code }` (code is
uppercased client-side — store/compare uppercase).

### `referrals`
`generate` upserts one row per `playerId` and returns `{ code, referralUrl }`.
Must return the **same** code on repeat calls (client caches it).

### `referral_claims`
`claim` sends `{ playerId, code }`; resolves code → referrer via `referrals`.
`UNIQUE (claimer_id)` = one claim per player ever. Bumps `referrals.claim_count`.

### `tower_swaps`
Fire-and-forget log: `{ playerId, waveId, fromTower, toTower, currency, costUsdc,
txSig, timestamp }`. `timestamp` is **unix SECONDS** (NOT millis — differs from
analytics' `clientTs`). `tx_sig` has a partial-unique index to dedup re-posts.

### `bug_reports`
`{ description, context:{ route, appVersion } }`. No `playerId` is sent today.

---

## 6. Assumptions / GUESSES the owner must verify

1. **`DATABASE_URL` is the env var name** — confirmed by reading `save.js` /
   `load.js`. No `NEON_*` vars are used. If you add more functions, keep them on
   `DATABASE_URL` for consistency.

2. **Host mismatch on bug-report (action needed).** `HelpMenu.cs` posts to
   `https://defenders-of-the-realm.vercel.app/api/bug-report` — the **old**
   (no `-v2`) host. Every other client service uses
   `https://defenders-of-the-realm-v2.vercel.app`. So as written, bug reports go
   to a *different* Vercel project (the original React app), which may have its
   own DB. **Decide one of:**
   - point `HelpMenu.BugReportEndpoint` at the `-v2` host (a `.cs` change — out
     of scope here, flag to the gatekeeper), so `bug_reports` lives in this DB; or
   - create `bug_reports` in whatever DB the old project uses instead.
   The `bug_reports` table is defined here either way.

3. **`promo_redemptions` reward columns are an audit snapshot.** I store the
   granted `crystals`/`coins` on the redemption row (not just a code ref) so a
   later edit to `promo_codes` can't rewrite history. If you'd rather always read
   the reward live from `promo_codes`, those two columns are redundant — keep or
   drop, your call. (Recommended: keep, for dispute/audit.)

4. **`per_player_limit` semantics are inferred.** The client error
   `PLAYER_LIMIT_REACHED` implies a cap on how many *distinct promo codes* one
   player may redeem, but the client never sends a number. I modelled it as a
   per-code column `per_player_limit` (the function checks the player's total
   redemption count against it). If the cap is meant to be **global** (one
   policy for all codes) rather than per-code, move it to a config/env value and
   ignore this column. **Owner to confirm the intended policy.**

5. **`referrals.reward_cap` / `claim_count` are inferred.** The client lists
   "Referrer reward cap per period" as a backend rule but sends no number and no
   period. I added a simple lifetime `reward_cap` + denormalised `claim_count`.
   If "per period" must mean per-week/month, you'll need a period column or to
   count `referral_claims.claimed_at` in a window instead. **Owner to confirm.**

6. **No hard FK from feature tables → `player_data`.** Deliberate: a player can
   fire analytics / bug reports / promo redemptions while still `"anonymous"` or
   before their first save creates the `player_data` row. A FK would reject those
   inserts. Join on `player_id` in queries instead. (FKs ARE used where the
   relationship is guaranteed: `promo_redemptions.code → promo_codes`,
   `referral_claims.referral_code → referrals`.)

7. **`analytics_events.player_id` may be the literal string `"anonymous"`** —
   `EventTracker` uses that when no wallet is bound. That's intentional; don't
   add a NOT-NULL-references constraint that would block it.

8. **`tower_swaps.client_ts` is SECONDS, `analytics_events.client_ts` is
   MILLIS.** Verified against the two client files. Both are stored as `BIGINT`
   raw; convert in queries with the right factor. The trustworthy ordering
   column on every table is the server-side `*_at` / `received_at` timestamp.

9. **Seed `TEST10` is OPT-IN as of 2026-08-18** — it used to run unconditionally.
   ⚠ This note previously read: *"Seed `TEST10` is included ... Remove the seed
   `INSERT` block at the bottom of `schema.sql` for production if you don't want a
   live working code."* Corrected, not deleted, because that instruction is how a
   public, uncapped, never-expiring free-crystal code got into a **published**
   game's schema: the game is live, the redeem door is in the Realm Store
   (`PackStore.cs:207-213`, deliberately outside the purchase flag), and the
   safety step was a sentence someone had to remember. It is now a default.
   The seed only runs when the SAME session first does:

   ```sql
   SET dotr.seed_test_codes = 'on';
   ```

   A plain paste of `schema.sql` leaves `TEST10` unseeded. Even when opted in the
   row is capped (25 redemptions) and expires (30 days). **Dev/staging only.**

   To check whether an EARLIER run already put it in production (read-only):

   ```sql
   SELECT code, active, reward_crystals, reward_coins, max_redemptions,
          per_player_limit, expires_at, bound_wallet, created_at
     FROM promo_codes WHERE code = 'TEST10';
   SELECT COUNT(*) AS burned FROM promo_redemptions WHERE code = 'TEST10';
   ```

   If present and active, prefer the kill-switch over a delete — `promo_redemptions.code`
   is FK'd `ON DELETE CASCADE`, so deleting the code erases the redemption audit trail:

   ```sql
   UPDATE promo_codes SET active = FALSE WHERE code = 'TEST10';
   ```

10. **Codes are uppercase.** Both `PromoCodeService` and `ReferralService`
    uppercase the code client-side before sending. The redeem/generate/claim
    functions must compare uppercase, and operator-inserted `promo_codes` rows
    must be uppercase, or lookups will miss.

---

## 7. After setup — smoke test order

Once a function exists for an endpoint, test in this order (each depends on the
prior table being populated):

1. `player_data` — already live; load/save round-trip from the game.
2. `analytics_events` — boot the game; `session_start` should appear.
3. `promo_codes`/`promo_redemptions` — redeem `TEST10`; a redemption row appears,
   a second redeem returns `ALREADY_REDEEMED`. **Two 2026-08-18 caveats:** the code
   must have been seeded opt-in (note 9), and `/api/promo/redeem` is now **wallet
   rail only** — a guest (`X-Guest-Id`) is refused with `AUTH_WALLET_REQUIRED`, so
   smoke-test it from a wallet-connected client, not a guest one.
4. `referrals` — call generate twice; same code both times.
5. `referral_claims` — claim from a *second* player; `SELF_REFERRAL` from the
   same player; `ALREADY_CLAIMED` on a second claim. **2026-08-18:** `/api/referral/claim`
   is now **wallet rail only** (it pays crystals), so both players must be
   wallet-connected; a guest claimer is refused with `AUTH_WALLET_REQUIRED`.
   `/api/referral/generate` still accepts guests — minting your own code grants nothing.
6. `tower_swaps` — perform a paid swap; one row, re-post is deduped by `tx_sig`.
7. `bug_reports` — file a bug from the Help menu (resolve the host mismatch first).

---

## Functions (plumbing)

The six client-called endpoints that previously had **no serverless function**
are now implemented. All follow the exact conventions of `game/save.js` /
`game/load.js`: `const { neon } = require('@neondatabase/serverless')`,
`const sql = neon(process.env.DATABASE_URL)`, `module.exports = async (req, res)`,
`res.status(<code>).json(...)`, status codes constrained to **200 / 400 / 500**
(401/404 unused here), parameterized tagged-template SQL only (no string concat),
and the `::jsonb` cast on JSON params (the Neon HTTP driver sends params as
strings). File path = Vercel route (e.g. `api/tower-swap/log.js` → `/api/tower-swap/log`).

**Body parsing:** only `game/save.js` sets `bodyParser:false` (it needs raw
MsgPack). The six new functions are JSON, so they use Vercel's **default** body
parser (`req.body`), with a `typeof body === 'string'` JSON.parse fallback for
safety. No global parser config exists, so this is correct.

**Business-failure status convention:** `promo/redeem` and `referral/claim`
return **HTTP 200** with `{ success:false, error:"<CODE>" }` for *business*
rejections (bad/expired/used code, self-referral, cap reached). This matches the
clients, which read `resp.Success` + `resp.Error` from the JSON body and only
treat a non-2xx `UnityWebRequest.Result` as "couldn't reach server". 400/500 are
reserved for malformed requests / server faults.

### `POST /api/events/track` → `analytics_events`
- **Client:** `Core/Analytics/EventTracker.cs`
- **Body:** `{ "events": [ { "playerId", "eventName", "properties", "clientTs" }, … ] }`
  — `properties` is a **JSON string**; `clientTs` is unix **millis**.
- **Response:** `{ "success": true, "inserted": <n> }` (client is fire-and-forget; only needs 2xx).
- **Logic:** loops the array, one INSERT per event. Parses `properties` string →
  JSONB (`::jsonb`); if it isn't valid JSON, stores `{ "_raw": "<string>" }`
  rather than dropping the event. Skips events with no `eventName` and a null/
  blank batch is a valid 200 (`inserted:0`). `playerId` defaults to `"anonymous"`.

### `POST /api/promo/redeem` → `promo_codes` (read) + `promo_redemptions` (write)
- **Client:** `Core/Promo/PromoCodeService.cs`
- **Body:** `{ playerId, code }` (code uppercased client-side; we re-uppercase server-side).
- **Success:** `{ success:true, reward:{ crystals, coins }, message }`
- **Failure (200):** `{ success:false, error:"INVALID_CODE"|"ALREADY_REDEEMED"|"EXPIRED"|"PLAYER_LIMIT_REACHED" }`
- **Logic / gates (in order):** missing row OR `active=false` → `INVALID_CODE`;
  `NOW() > expires_at` → `EXPIRED`; this player already redeemed this code →
  `ALREADY_REDEEMED`; global count `>= max_redemptions` → `ALREADY_REDEEMED`;
  player's distinct redeemed codes `>= per_player_limit` → `PLAYER_LIMIT_REACHED`.
  On success: INSERT into `promo_redemptions` snapshotting `(crystals, coins)`;
  a `UNIQUE(code, player_id)` race (Postgres `23505`) is caught → `ALREADY_REDEEMED`
  (idempotent, never double-grants).

### `POST /api/referral/generate` → `referrals`
- **Client:** `Core/Referral/ReferralService.cs` (`EnsureCodeAsync`)
- **Body:** `{ playerId }`
- **Response:** `{ success:true, code, referralUrl }`
- **Logic:** generate-or-reuse. If the player already has a `referrals` row,
  returns the **same** code+url (client caches it, calls repeatedly). Otherwise
  mints an **8-char uppercase** code from an unambiguous alphabet (no `0/O/1/I`),
  `referralUrl = https://defenders-of-the-realm-v2.vercel.app/r/<code>`, INSERT
  with `ON CONFLICT (player_id) DO NOTHING` (handles concurrent first-calls by
  the same player → re-selects), and retries up to 6× on a `UNIQUE(code)`
  collision with another player.

### `POST /api/referral/claim` → `referrals` (read+bump) + `referral_claims` (write)
- **Client:** `Core/Referral/ReferralService.cs` (`ClaimAsync`)
- **Body:** `{ playerId, code }`
- **Success:** `{ success:true, reward:{ crystals }, message }`
- **Failure (200):** `{ success:false, error:"INVALID_CODE"|"SELF_REFERRAL"|"ALREADY_CLAIMED"|"CAP_REACHED" }`
- **Logic / gates (in order):** code not in `referrals` → `INVALID_CODE`;
  referrer == claimer → `SELF_REFERRAL`; claimer already has a row (any code) →
  `ALREADY_CLAIMED`; `claim_count >= reward_cap` (only when `reward_cap` set) →
  `CAP_REACHED`. On success: INSERT into `referral_claims` (`UNIQUE(claimer_id)`
  race → `ALREADY_CLAIMED`), then **best-effort** `claim_count + 1` bump on
  `referrals` (a failed bump is logged, not rolled back — the claim/reward stand;
  the counter is recomputable from `referral_claims`).
- **Claimer reward amount:** the client sends NO amount — the backend decides.
  Implemented as env `REFERRAL_CLAIM_CRYSTALS` (**default 25**). **ASSUMPTION —
  owner must confirm the intended grant** (and whether the *referrer* also gets a
  reward — see assumption 12 below; no referrer payout is implemented yet).

### `POST /api/tower-swap/log` → `tower_swaps`
- **Client:** `Village/Buildings/TowerSwapService.cs` (fire-and-forget)
- **Body:** `{ playerId, waveId, fromTower, toTower, currency, costUsdc, txSig, timestamp }`
  — `timestamp` is unix **seconds** (NOT millis). `costUsdc` → `NUMERIC(12,4)`.
- **Response:** `{ "success": true, "deduped": <bool> }` (client ignores the body).
- **Logic:** straight INSERT with `ON CONFLICT (tx_sig) WHERE tx_sig IS NOT NULL
  DO NOTHING` (matches the partial unique index `uq_tower_swaps_tx_sig`) so a
  re-posted on-chain payment is silently deduped (`deduped:true`). Null `tx_sig`
  rows are never deduped (partial index excludes them), per schema intent.

### `POST /api/bug-report` → `bug_reports`
- **Client:** `HUD/HelpMenu.cs` (`PostBugReport`)
- **Body:** `{ "description", "context":{ "route", "appVersion" } }` — **no playerId** today.
- **Response:** `{ "success": true }` (client only checks 2xx; logs the body).
- **Logic:** straight INSERT. Stores `description` (capped 4000 chars
  defensively), `route`, `app_version`, the full `context` object as JSONB, and a
  nullable `player_id` (accepted if a future caller adds one). Empty/blank
  description → 400.
- **⚠ HOST MISMATCH (owner action):** `HelpMenu.BugReportEndpoint` posts to the
  **OLD** host `https://defenders-of-the-realm.vercel.app/api/bug-report` (NO
  `-v2`). Every other service uses `-v2`. So **as written, this function will
  NOT be hit on the `-v2` deployment** — the call lands on the old React project.
  Two options:
  1. **Repoint the client** — change `HelpMenu.BugReportEndpoint` to the `-v2`
     host (a `.cs` edit, out of scope here → flag to gatekeeper). Then this
     `api/bug-report.js` on `-v2` receives it and writes to this DB. **Recommended.**
  2. **Deploy this function on the OLD project** and ensure that project's DB has
     the `bug_reports` table (and a `DATABASE_URL` pointing at it).

---

## Additional assumptions the owner must verify (functions layer)

These extend §6's table-layer assumptions with function-behaviour decisions:

11. **`promo/redeem` & `referral/claim` return HTTP 200 on business failures.**
    Confirmed against the clients (they branch on the JSON `success`/`error`
    fields, not the HTTP status). If you later add an API gateway / monitoring
    that flags non-success by HTTP code, those rejections won't be visible to it.

12. **Referral reward semantics — claimer only, fixed amount.** The claimer gets
    `REFERRAL_CLAIM_CRYSTALS` (default **25**) crystals. The schema/client mention
    a **referrer** reward too ("triggers the referrer's reward"), but the client
    has no path to receive it (the referrer is offline at claim time) and no
    table column captures a pending referrer payout. **Not implemented** — the
    referrer's `claim_count` is tracked but no crystals are granted to them. Owner
    must decide how/when the referrer is paid (e.g. a `referrer_reward` column +
    a credit applied on the referrer's next `game/load`/`save`).

13. **Generated referral code shape.** 8 chars, alphabet `A-Z2-9` minus
    `0/O/1/I` (~30 bn combinations). Collision retry is 6×. The `referralUrl`
    points at `…/r/<code>` on the `-v2` host — there is **no route/handler for
    `/r/<code>` yet** (it's just a shareable string today). Owner to add a landing
    page / deep-link if the URL must actually resolve.

14. **`events/track` keeps malformed `properties` instead of dropping.** If the
    `properties` string fails to JSON-parse, it's stored as
    `{ "_raw": "<original>" }` rather than discarded, so no event is lost. Change
    to a hard skip if you'd rather not persist unparseable props.

15. **`tower-swap/log` accepts null `tx_sig`.** Per schema, only non-null sigs are
    deduped. A swap logged without a signature always inserts a new row (no dedup
    possible). The current client always sends `result.TxSignature`, so null is an
    edge case only.

---

## 16. 2026-08-02 — cloud save was DEAD; what has to be run

Verified against **live production**, not inferred. Four independent breaks were
sitting on top of each other; fixing any one alone changes nothing.

### What the live database actually said (2026-08-03 02:23 UTC)

`GET /api/admin/db?view=overview` with `X-Admin-Key`:

| table | rows | latest |
|---|---|---|
| `player_data` | **2** (`Test123`, `test-wallet-0001`) | **2026-05-31** |
| `analytics_events` | 80,748 | 2026-08-02 21:26 |
| `bug_reports` | **0** | never |
| `auth_nonces` | **MISSING TABLE** | — |
| `player_profiles`, `leaderboard_scores`, `achievement_grants` | **MISSING TABLE** | — |

No real player's progress has ever reached Neon. No bug report ever has either.
`analytics_events` proves the DB and the connection are fine — it is specifically
save / load / nonce / bug-report that were broken.

### The four breaks

1. **`auth_nonces` does not exist on the live database.**
   `GET /api/auth/nonce?wallet=<valid base58>` → **HTTP 500**. `issueNonce`
   INSERTs into a table that is not there, so the client can never obtain a
   nonce, so it can never sign, so the wallet rail is unreachable **no matter
   what the client does**. A nonce table nobody can get a nonce from.

2. **The client sends no auth headers.** `POST /api/game/save` unauthenticated
   → `401 {"reason":"missing_auth_headers"}` — which is exactly the client's
   behaviour, and exactly the **1,039 `auth_failed` rows recorded on 2026-08-02
   alone**. `BackendAuthConfig.Enforced` defaults false and the
   `BACKEND_AUTH_ENFORCED` scripting define is set on **no** platform.

3. **There was no guest path at all.** The APK front door is "Connect Wallet OR
   Play as Guest"; the server required a wallet signature unconditionally. Fixed
   — see `guest_rate_limit` in `schema.sql` and `_lib/wallet-auth.verifyGuest`.

4. **`bug_reports` is missing columns.** Captured from production:
   `NeonDbError: column "player_id" of relation "bug_reports" does not exist`
   (SQLSTATE 42703, `api/bug-report.js:124`). Every bug report a tester has ever
   submitted from Settings returned 500 and was thrown away.

### What the owner must run

1. **Apply `api/schema.sql` against Neon** (idempotent — safe to re-run):
   ```bash
   psql "$DATABASE_URL" -f api/schema.sql
   ```
   This creates `auth_nonces`, `guest_rate_limit`, `player_profiles`,
   `leaderboard_scores`, `achievement_grants`, and adds the missing
   `bug_reports` / `player_data` columns. **Nothing works until this is run.**
2. **Deploy `api/`.** The live deployment is STALE — it does not even have the
   `view=bugreports` added by WO-846, so repo-side fixes are not live.
3. Optional env: `GUEST_SAVE_ENABLED=false` kills the guest rail with no code
   change. Absent/empty = ON.
4. **Google Play identity rail (WO-1282 PIN-1b) — DORMANT until switched on.**
   Apply `api/migrations/20260830_0013_auth_sessions_identity_kind.sql` (additive:
   one `auth_sessions.identity_kind` column, defaulted `'wallet'`), then set:

   | Env var | Required | What it is |
   |---|---|---|
   | `GOOGLE_IDENTITY_ENABLED` | yes, `true` to arm | Default OFF. While unset, `POST /api/auth/google-session` answers **503** and no `play-` id can authenticate anywhere. |
   | `GOOGLE_IDENTITY_KEY` | yes | HMAC-SHA256 key that derives `play-<64 hex>` from the Google `sub`. ⛔ **Treat as permanent** — rotating it is the only way a player's id can change, and `resolveStablePlayerId` will pin any player holding `google_play_purchases` rows to the old key rather than re-key them. |
   | `GOOGLE_IDENTITY_AUDIENCES` | yes | Comma-separated OAuth client-id allowlist checked against the token's `aud`. Empty = UNCONFIGURED = the rail refuses. There is no "allow any". |
   | `GOOGLE_IDENTITY_KEY_PREVIOUS` | only during a rotation | The key being rotated away from, so the no-re-key guard can see the id a player used to have. |
   | `GOOGLE_IDENTITY_JWKS_URL` | no | JWKS override for tests/staging. Defaults to Google's `https://www.googleapis.com/oauth2/v3/certs`. |

   ⛔ **The wallet remains the SOLE identity on the Seeker / dApp-Store artifact**
   (owner ruling 2026-08-30). This rail is for the Google Play / AAB artifact only,
   and `auth/nonce.js` / `auth/session.js` / `verifyWallet` / `verifySession` are
   untouched by it.

5. **Pi (U2A) payment rail (WO-1318) — DORMANT until the key is set.**

   ⛔ **APPLY THE MIGRATION BEFORE THE FIRST PI PAYMENT, NOT AFTER.**
   `POST /api/pi/complete` runs with the Pioneer's Pi **already moved** and there is
   no refund route, so a schema fault on that path is found with the money gone —
   the WO-1173 lesson (a real 391 SKR payment settled and could not be recorded)
   arriving on a new rail.

   ```bash
   psql "$DATABASE_URL" -f api/migrations/20260902_0017_pi_payments.sql
   node tools/schema-parity.mjs          # must print SCHEMA_PARITY_OK
   ```

   It is additive and idempotent: it widens `purchase_entitlements.rail` to
   `('solana','pi')` (plus `network`/`currency`), widens `purchase_quotes` the same
   way, drops NOT NULL from the three **Solana-only** quote columns
   (`mint`, `recipient`, `recipient_ata`), and creates `pi_payments` — the rail's
   lifecycle ledger, the Pi twin of `google_play_purchases`. **There is no second
   quote table and no second grant path:** a Pi purchase quotes out of
   `purchase_quotes` and grants into `purchase_entitlements`, exactly like SKR.

   | Env var | Required | What it is |
   |---|---|---|
   | `PI_NETWORK_API_KEY` | yes, to arm | The Pi app's server API key. **Server-only.** Sent solely as `Authorization: Key <key>` to `api.minepi.com`; never returned, never logged, never committed. While unset, `/api/pi/quote`, `/api/pi/approve` and `/api/pi/complete` all answer **503 `PI_NOT_CONFIGURED`** and no Pi payment can be approved. |

   ⛔ **There is deliberately NO fallback Pi price.** The amount is derived
   server-side from the USD anchor and CoinGecko's `low_24h` for `pi-network` (the
   owner's ruling, same as SKR). Oracle down ⇒ **503 `PURCHASE_RATE_UNAVAILABLE`**
   and nothing is sold. Charging a made-up number is worse than refusing to sell.

### Reading the failures (the new read paths)

Every auth refusal now returns a **stable code + a `ref`** to the player and
writes a full row to the db under the same `ref`.

```bash
KEY=$(cat .admin-dash-key)
BASE=https://defenders-of-the-realm-v2.vercel.app

# what is failing, and how much of it — also reads the LEGACY auth_failed rows
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=authrejects&since_hours=24"

# every instance of one failure class
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=authrejects&code=AUTH_NONCE_REPLAYED"

# a player quoted a ref from their screen -> the exact row
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=authrejects&ref=3f9a21c8"

# did a save land, and is it a real save or a husk? (state_keys ~60 = full)
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=players&limit=10"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=players&player=<playerId>"

# bug reports: list, then ONE in full (traceTail + optional screenshot)
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=bugreports&limit=20"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=bugreport&id=42"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/db?view=bugreport&id=42&shot=1"
```

Failure codes are listed in `api/_lib/wallet-auth.js` (`AuthCode`). The response
body a player's client receives is only `{ok:false, code, ref}` — quiet on the
screen, loud in the database.


---

## 17. 2026-08-17 — the OPERATOR DASHBOARD (WO-1116 phase 1): what it reads, and what each number means

`api/admin/stats.js` + `site/admin.html`. `api/admin/db.js` stays the RAW-TABLE triage tool
(row counts, one save, one trace, auth rejects); `stats.js` is the AGGREGATE view over
`analytics_events` — who is playing, who comes back, where the tutorial loses them, what sells.

**Where it lives.** The page is deployed with the marketing site (Vercel project
`echoes-of-elarion`, `https://echoes-of-elarion.vercel.app/admin`); the endpoint is on the API
project (`defenders-of-the-realm-v2`). It is therefore always a cross-origin caller — the page asks
for the API host, prefilled with the v2 URL. **Nothing links to `/admin` from `index.html`, and the
page is `noindex`: it is unlisted and key-gated, nothing more.** `ADMIN_DASH_KEY` must be set on the
**v2** project, not on the site project.

**The key is never stored.** The page holds it in one JS variable for the life of the tab —
not localStorage, not sessionStorage, not a cookie, not the URL. `site/` is a PUBLIC deployment;
anything persisted or hardcoded there is published. Reload = enter it again, on purpose.

```bash
KEY=$(cat .admin-dash-key)
BASE=https://defenders-of-the-realm-v2.vercel.app
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/stats?view=overview&days=30"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/stats?view=retention&days=90"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/stats?view=funnel&days=30"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/stats?view=economy&days=30"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/stats?view=players&limit=50"
curl -s -H "X-Admin-Key: $KEY" "$BASE/api/admin/stats?view=players&ref=<12hex handle>"
```

### The metrics

| Metric | Definition (exactly what is counted) |
|---|---|
| **Active today / 7d / 30d** | DISTINCT `player_id` with a `session_start` in that window. `session_start` fires once per app boot (`EventTracker.Start`), so this is "opened the game", not "was online then". |
| **App opens (sessions)** | The `session_start` ROW count over the same window. Several per player per day is normal. |
| **New players per day** | Players whose FIRST-EVER event of any kind landed that day. "First-ever" is computed over the whole table, so a June installer never re-counts as new in August. |
| **Day-N retention** | Of players whose first-ever event was on `cohort_day`, the share that fired a `session_start` on **exactly** `cohort_day + N` (N = 1, 7, 30). Exact-day, not "within N days". |
| **Tutorial funnel** | Per step, from `tutorial_step_enter` / `_complete` / `_skip` / `_drop`, ordered by the `order` field on the enter event. |
| **STUCK (auto-advanced)** | `tutorial_step_drop` — TutorialFlow's watchdog rescued a player who sat on that step until it auto-advanced. **A step with drops is a step players cannot get past on their own.** This is the column that measures the live FTUE defect (WO-1036). |
| **Lost before next step** | Players who entered step *i* and never entered step *i+1*. |
| **Purchases / buyers** | `purchase_completed` rows and distinct buyers, by pack and currency. |
| **Views → buys** | Distinct `bundle_viewed` viewers vs distinct `purchase_completed` buyers per pack. `bundleId` and `packId` are both `pack.Sku` (PackStore), so they join cleanly. |
| **Promo codes** | From `promo_codes` LEFT JOIN `promo_redemptions` — the ledger, not the client's claim about it. A bound code shows only THAT it is bound; the wallet it is bound to is never displayed. |

### ⚠ The caveats. Read these before acting on a number.

1. **Small N.** Every retention and conversion percentage is returned **with the cohort size it came
   from**, and anything under **n=10** is flagged `low_n` and rendered with a "low n" chip. *1 of 2 is
   not 50% retention — it is two players.* Never quote a percentage from this page without its n.
2. **Immature cohorts are not 0%.** A cohort three days old cannot have day-7 data. Those cells read
   "too early / needs 7 days"; they are excluded from the pooled rollup entirely rather than dragging
   it to zero.
3. **The "anonymous" blind spot.** Every player with no bound wallet shares the single literal id
   `"anonymous"` (`EventTracker.cs:168` — `BoundWallet ?? "anonymous"`). That is one bucket for many
   people, so it can never be a distinct-player count. It is EXCLUDED from every player metric and
   reported separately on the Overview tab. **If the anonymous volume dwarfs the player counts, this
   dashboard is reporting on a minority of the playerbase** — that is the honest reading, not "one
   very busy player".
4. **There is no revenue here, only counts.** `purchase_completed` carries
   `{packId, packName, currency, txSig}` and **no amount** (`PackStore.cs:582`); the `price` in
   `EventTracker`'s doc comment is an example, not a live field. A null "amount sent by client" means
   the client never sent one, NOT that the sale was free. Revenue must come from the chain or from
   `tower_swaps.cost_usdc` (and read that table's `verification` column first — see §7 of `schema.sql`).
5. **Zero vs never-fired.** The Overview tab lists every event name seen in the window precisely so a
   metric reading zero because nobody did it can be told apart from one whose event never fires at all.
6. **Player ids are masked** to `first4…last4` in every list, alongside `player_ref`, a stable 12-hex
   SHA-256 handle used to open one player without a full address ever being on screen. The single
   drill-down (`?player=` / `?ref=`) is the ONE place a full wallet appears, because the operator needs
   the real address to bind a promo code to that player or answer a support ticket.
7. **Not verified against live data.** These queries were written from `api/schema.sql` and from the
   client's actual `EventTracker.Track` call sites; **no seat has run them against the production
   database.** The shapes are right; the numbers are unproven until the owner opens the page.

### Phase 2 (issuing codes/grants from the panel) is SPEC ONLY

Not built. See `WorkOrders/WORK_ORDER_1116_admin_dashboard_and_grants.md`: POST-only, same
`ADMIN_DASH_KEY`, an `admin_audit` row per write in the same transaction, and **issue a
wallet-BOUND promo code** (`promo_codes.bound_wallet`, enforced at `api/promo/redeem.js:172`) rather
than writing resources into a save row — a direct save write bypasses every ledger, cap and expiry
the game has, and races the client's own delta-merge upsert.

---

## 18. 2026-08-27 - THE COMMAND CENTER CONSOLE (WO-1244), and the SECOND KEY

`api/admin/console.js` serves one self-contained HTML page - no framework, no build step, no CDN.
It is the surface WO-1169 specced and WO-1244 built, and it is a PHONE tool: the owner sees an
exploit or a failed purchase on her phone, not at a desk.

**Open it at** `https://defenders-of-the-realm-v2.vercel.app/api/admin/console` and type the admin
key into the gate. No query string, no header - a browser navigation cannot send one, so the SHELL
is public and carries no data at all; every byte of data arrives afterwards over `X-Admin-Key`.

### ⛔ TWO KEYS, AND THE SECOND ONE IS THE POINT

| Env var | Header | What it buys |
|---|---|---|
| `ADMIN_DASH_KEY` | `X-Admin-Key` | READS. `api/admin/db.js`, `api/admin/stats.js`. Already set. |
| `ADMIN_OPS_KEY`  | `X-Admin-Ops-Key` | WRITES. `api/admin/ops.js` ONLY. **NEW - must be set on the `defenders-of-the-realm-v2` project or every write is refused.** |

The read key gets typed into a phone in public and ends up in screenshots of the dashboard. That is
an acceptable exposure for a read surface and NOT acceptable for one that can seal the whole game or
mint free currency. A second secret means a leaked read key buys a reader exactly what it says on
the tin: reading.

⚠ **`api/admin/ops.js` FAILS CLOSED.** With `ADMIN_OPS_KEY` unset it answers
`OPS_WRITE_NOT_CONFIGURED` and writes nothing. That is the deliberate OPPOSITE of
`api/_lib/maintenance.js`, which fails OPEN: there an unreadable table must not cost a player their
session; here "we could not check who you are" must never resolve to "go ahead and change the money
tables". Availability there, correctness here. Do not unify them.

### The read/write boundary is at the ENDPOINT, not in the UI

```
READ    GET  /api/admin/stats?view=purchases   money, SERVER-settled + the client disagreement
        GET  /api/admin/stats?view=ops         toggles, promo catalog, player issues, write history
WRITE   POST /api/admin/ops                    four actions, second key, attributable + timestamped
```

`db.js` and `stats.js` remain SELECT-only BY CONSTRUCTION and `test/command-center.test.js` lints
them, with the lint itself proven able to see a real violation. The write endpoint knows exactly
four actions - `maintenance.seal`, `maintenance.open`, `promo.create`, `promo.set_active` - and
**names neither money table anywhere in its source.** There is no refund, no grant and no edit of
`purchase_quotes` / `purchase_entitlements` from any admin surface.

`POST /api/admin/ops` sets **no CORS header at all**, the same choice `api/admin/cleanup.js` makes:
the console is served from this deployment, so it is same-origin. A write endpoint has no business
being callable from a page we did not serve.

### What it shows

* **Toggles** - the six WO-1243 kill switches with state as a WORD (`CLOSED` / `open`), the banner
  text, and WHO flipped it WHEN. Sealing requires a banner message; opening clears it.
* **Money** - the client/server disagreement FIRST, as an ALERT with a count: a `purchase_completed`
  event with no entitlement row for its `txSig` is a grant that may have been handed out with no
  verified settlement behind it. Client-reported and server-settled are shown side by side and are
  **never blended into one number** - the disagreement IS the signal (WO-1169 section 3).
* **Player issues** - `bug_reports`, with identity as `verified` / `unverified` and never an address.
  A burst of `unverified` means auth is broken, which is itself the triage signal.
* **Promos** - the catalog with live redemption counts, plus an authoring form.
* **Tickets** - a pointer, not a second board. `BOARD.html` is DEV work generated from
  `WorkOrders/*.md`; player issues are the tab above. ⛔ Never fold one into the other: the board is
  generated, so anything written there is overwritten on the next `tools/board_build.py` run.

### Privacy, and it is absolute

No wallet, no email, no real name is rendered or logged anywhere in this surface. `bound_wallet`
is reduced to the boolean `is_bound` in SQL and the column is never selected; the bug-report wallet
becomes `wallet_verified`; player ids arrive pre-masked. **Wallet-BOUND promo codes are deliberately
NOT authorable from the console** - authoring one means typing an address into a page and reading it
back out of a list. Use the SQL editor for those; the console can see THAT a code is private and
never to whom.

### One optional migration (idempotent, additive, safe on the live table)

```sql
ALTER TABLE promo_codes ADD COLUMN IF NOT EXISTS created_by TEXT;
```

Without it, promo authoring still works: the insert falls back one shape and says so in its
response, and the durable history row in `analytics_events` (`event_name = 'admin_ops_write'`)
carries the attribution regardless. With it, the operator label lands on the code's own row.
⚠ This column is added by ALTER, not inside the `CREATE TABLE` body, so `tools/schema-parity.mjs`
does not read it as declared-but-missing drift and no deploy is blocked while it is unrun.
