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
