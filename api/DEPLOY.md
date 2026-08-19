# Backend deploy — 5-minute checklist

The `api/` folder is a set of **Vercel serverless functions** (CommonJS) on **Neon Postgres**.
The repo root has `vercel.json` + (now) `package.json`, so Vercel auto-installs deps and serves `api/*`.
**Nothing breaks if this stays undeployed** — the Unity client is offline-first and only calls the backend
when `BackendAuthConfig.Enforced` is on (it's off by default).

## Do these in order

1. **🔴 ROTATE the Neon credential first.** The old connection string was pasted in chat — in the Neon
   console, reset the `neondb_owner` password (or roll a new role). Use the NEW string everywhere below.

2. **Create the tables.** Neon console → SQL Editor → paste all of `api/schema.sql` → Run.
   It's idempotent (`CREATE TABLE IF NOT EXISTS`), safe to re-run. Creates: player_data, analytics_events,
   promo_codes/redemptions, referrals/claims, tower_swaps, bug_reports, auth_nonces, player_profiles,
   leaderboard_scores, achievement_grants.

   ⚠ **2026-08-18 — this step used to seed a live free-crystal code.** The bottom of `schema.sql`
   ran an unconditional `INSERT` of promo code **`TEST10`** (10 crystals, `active = TRUE`, no cap,
   no expiry, open to anyone) with a comment telling the operator to remember to remove it. Following
   this checklist on a **published** game therefore put a working free-money code in production, and
   the redeem door is reachable in the Realm Store (`PackStore.cs:207-213`, outside the purchase flag).
   The seed is now **opt-in** and this step no longer creates it. To seed it on purpose in
   dev/staging, run `SET dotr.seed_test_codes = 'on';` in the same session **before** pasting.
   To check whether an earlier run already seeded it in production, and how to defuse it without
   losing the audit trail, see `api/DB_SETUP.md` note 9.

3. **Point Vercel at the repo** (if not already): import this Git repo as a Vercel project. Root dir = repo
   root (where `vercel.json` lives). Framework preset = **Other**. Build runs `npm install` automatically
   (the new `package.json` pulls `@neondatabase/serverless`, `tweetnacl`, `bs58`, `@msgpack/msgpack`).

4. **Set the env var** in Vercel → Settings → Environment Variables:
   `DATABASE_URL` = your **rotated** Neon connection string (with `?sslmode=require`).

5. **Deploy** (push to the branch Vercel watches, or hit "Deploy").

6. **Smoke test** (browser or curl):
   - `GET https://<your-app>.vercel.app/api/leaderboard?metric=best_wave&period=all` → JSON `{ top: [...] }`
   - `GET https://<your-app>.vercel.app/api/auth/nonce?wallet=test` → JSON `{ nonce, expiresAt }`
   A 200 with JSON = live. (Empty arrays are fine on a fresh DB.)

## Value-granting endpoints are WALLET-RAIL ONLY (2026-08-18)

`/api/promo/redeem` and `/api/referral/claim` hand out crystals/coins, so they now require the
**signed wallet rail** (`X-Wallet` + `X-Nonce` + `X-Signature`) via `_lib/wallet-auth.authenticateGranting`.
The guest rail (`X-Guest-Id`) is **refused** there with `AUTH_WALLET_REQUIRED` and audited.

Why: a guest id is `guest-local-<64 hex>` **minted by the client**, carries no signature, and the server
only regex-checks it. An attacker mints unlimited "players", which burns a code's `max_redemptions` and
walks past `per_player_limit`. Guests keep every non-granting route (save, load, referral generate,
tower-swap log) — those act on the caller's own row and grant nothing.

**Operational consequence:** a guest player can no longer redeem a promo code or claim a referral.
If a promo campaign must reach guests, that needs a scarcity key an attacker cannot mint — not the
guest rail. Owner's call.

## What stays OFF until later
- `BackendAuthConfig.Enforced` (Unity) — leave OFF until a real wallet signer (MWA `signMessage`) ships.
  Until then save/load send no auth headers and the server doesn't require them.
- Test data: `api/test-data.sql` (optional) seeds sample rows to see live data in the responses.

## Notes
- Functions are CommonJS (`require`) — `package.json` has **no** `"type": "module"`. Don't add it.
- `crypto` is Node built-in (no package needed).
- Versions pinned CJS-safe: bs58 5.x (6.x is ESM-only), @msgpack/msgpack 2.x (3.x is ESM-only).
