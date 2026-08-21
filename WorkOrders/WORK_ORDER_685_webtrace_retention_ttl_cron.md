**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 685 — Web-trace retention / TTL cron (the 7-day sweep that does not exist)

**Status: READY TO IMPLEMENT** (implemented ahead in this WO — see "Implemented in this WO" below;
owner deploy pending). **Lane:** Persistence/Backend (Lane 7). **Type:** EXISTING (the write pipe is
live; the retention half was never built). **Silo:** `api/` serverless + `vercel.json` (file-disjoint
from all Unity lanes).

**Numbering note:** minted **685** from the `CLI_LANES_WO_NUMBERS.md` banner (next-free 685 → bumped to
688 in the same edit; 685/686/687 = the web-trace lifecycle trio). Do not mint from the frozen per-lane
history.

## Symptom

The web-trace pipe (WO-443) writes a Neon row for **every** WebGL diagnostic batch and **never deletes
one**. Both the client header and `WebTrace.cs` promise a "7-day TTL" that has **no server-side
enforcer**, so `analytics_events` grows unbounded for the life of the deployment — a cost + privacy +
DB-bloat liability that compounds every play session.

## RCA / evidence (cited)

- **`docs/SECURITY_AUDIT_2026-07-12.md:11`** — H1: *"The 7-day web_trace TTL cron DOES NOT EXIST (no
  `crons` in vercel.json, no cleanup fn)."* Prescribed fix at `:12-14`:
  `DELETE FROM analytics_events WHERE event_name='web_trace' AND received_at < NOW() - INTERVAL '7 days'`
  (+ `auth_nonces` sweep).
- **`docs/SECURITY_AUDIT_2026-07-12.md:27`** — M4: `auth_nonces` are pruned "only per-wallet
  opportunistically — fold into the H1 cron."
- **`Assets/_Modules/Core/Diagnostics/WebTrace.cs:8`** — client comment: *"writes them to a Neon table
  with a 7-day TTL"* — a promise nothing keeps. Reiterated `:35-36`: *"the 7-day cron … lives in the
  React/Vercel repo — out of scope for this client"* — i.e. explicitly deferred, never done.
- **`api/trace.js:73-81`** — the only writer: `INSERT INTO analytics_events (…event_name='web_trace'…)`.
  No matching DELETE anywhere in `api/`.
- **`api/schema.sql:159-162`** — schema author already anticipated the sweep: *"Sweep expired/used nonces
  cheaply (a cron or the next issue call can run: `DELETE FROM auth_nonces WHERE expires_at < NOW() OR
  used = TRUE`)"* — the index `idx_auth_nonces_expires` exists FOR this; the cron was never written.
- **`api/schema.sql:195`** — `received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()` is the trusted server clock
  (the retention cutoff must use this, never the client-supplied `client_ts`).
- **`vercel.json`** — no `crons` key present (confirmed).

## Exact steps

1. **Create `api/admin/cleanup.js`** — a serverless function that, when authorized, runs two bounded,
   idempotent DELETEs against `DATABASE_URL` (env only; `neon()` HTTP driver, mirroring `api/trace.js`):
   - web_trace retention:
     `DELETE FROM analytics_events WHERE event_name='web_trace' AND received_at < NOW() - (7 * INTERVAL '1 day')`
     (parameterized interval; `received_at` = server clock).
   - nonce sweep (folds M4):
     `DELETE FROM auth_nonces WHERE used = TRUE OR expires_at < NOW()`.
   - Return `200` `{ success, deleted_web_trace_rows, deleted_auth_nonces, ran_at }`. Status codes
     **200 | 400 | 500 only.**
2. **Gate it — two accepted invokers, constant-time checked, never fail open:**
   - Vercel Cron: `Authorization: Bearer <CRON_SECRET>` (Vercel injects this when the `CRON_SECRET`
     env var is set). Compare with hashed `crypto.timingSafeEqual` (same shape as
     `api/admin/db.js:adminKeyOk`).
   - Manual admin run: `X-Admin-Key` == `ADMIN_DASH_KEY` (the db-viewer key).
   - Anything else → `400 Unauthorized`. **No CORS header** (server-to-server; do not widen exposure).
3. **Add the `crons` key to `vercel.json`** (preserve everything else):
   `{ "crons": [ { "path": "/api/admin/cleanup", "schedule": "0 4 * * *" } ] }` — daily 04:00 UTC.
4. **Env prerequisites for the owner (deploy-time):** set `CRON_SECRET` on the Vercel project (any
   long random string; Vercel then signs cron calls with it). `ADMIN_DASH_KEY` + `DATABASE_URL` already
   exist for `api/admin/db.js`.

## Implemented in this WO (reversible, awaiting owner deploy)

- `api/admin/cleanup.js` — created (the two DELETEs + the dual gate above).
- `vercel.json` — `crons` key added pointing at `/api/admin/cleanup`, daily `0 4 * * *`.
- **GOES LIVE ONLY ON THE OWNER'S NEXT DEPLOY** — Vercel reads `crons` at deploy time; nothing schedules
  until then. This WO did **not** deploy.

## Acceptance

- [ ] `api/admin/cleanup.js` returns `200` with delete counts when called with a valid `CRON_SECRET`
      bearer OR `X-Admin-Key`; returns `400` for any unauthenticated call (verify both).
- [ ] After deploy, one manual admin invocation deletes web_trace rows with `received_at` older than 7d
      and all `used`/expired `auth_nonces`; a live unused nonce and a <7d trace row survive.
- [ ] `vercel.json` still valid JSON; the WebGL `headers`/`outputDirectory`/`git` blocks unchanged.
- [ ] Cron appears in the Vercel dashboard Crons tab after deploy; next-run scheduled.
- [ ] Owner confirms `CRON_SECRET` is set on the project before relying on the schedule.

## What NOT to touch

- `api/trace.js` (the writer is correct — this WO only adds the reaper).
- The `analytics_events` schema, the `web_trace` `event_name`, or any non-`web_trace` analytics rows
  (the DELETE is scoped to `event_name='web_trace'` — never purge gameplay analytics).
- `api/admin/db.js` (the read surface — separate WO-687).
- Any live/unused/unexpired nonce (the nonce sweep must not touch a valid challenge).
- Deploy — the owner deploys; CLI does not.

*Proof source: `docs/SECURITY_AUDIT_2026-07-12.md` H1/M4; `api/trace.js`, `api/schema.sql`,
`WebTrace.cs`, `vercel.json` as cited above.*

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
