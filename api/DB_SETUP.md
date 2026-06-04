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
SELECT code, reward_crystals, active FROM promo_codes;   -- expect TEST10, 10, true
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

9. **Seed `TEST10` is included** because the client ships an editor menu item
   that redeems it. Remove the seed `INSERT` block at the bottom of `schema.sql`
   for production if you don't want a live working code.

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
   a second redeem returns `ALREADY_REDEEMED`.
4. `referrals` — call generate twice; same code both times.
5. `referral_claims` — claim from a *second* player; `SELF_REFERRAL` from the
   same player; `ALREADY_CLAIMED` on a second claim.
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
