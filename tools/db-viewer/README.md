# DB Viewer — owner-only database & metrics tool

A single local HTML file (`tools/db-viewer/index.html`) that the owner
double-clicks to see the state of the Neon Postgres backend for
`defenders-of-the-realm-v2`: table overview, latest players, 7-day metrics,
and web_trace sessions. It talks to ONE read-only serverless endpoint,
`api/admin/db.js`.

**This is owner-only tooling.** The endpoint is gated by a secret key, ships
only aggregate / latest-N views, and exposes no data beyond what the game's own
tables already hold (player privacy is a standing project rule — no extra PII
is collected or surfaced). Full save blobs are returned only when you request
one explicit player id; the list views show payload size only.

## One-time setup

1. **Add the key on Vercel:** project `defenders-of-the-realm-v2` →
   Settings → Environment Variables → add **`ADMIN_DASH_KEY`** for
   **Production**, marked **Sensitive**. Value: a long random string, e.g.
   from PowerShell:
   `-join ((48..57)+(97..122) | Get-Random -Count 40 | % {[char]$_})`
2. **Redeploy the backend** so `api/admin/db.js` + the env var go live
   (env vars only apply to deployments made after they're added).
3. **Open the viewer:** double-click `tools\db-viewer\index.html`.
4. Paste the same key into the "Admin key" field, click **Save settings**
   (stored in that browser's localStorage), pick a tab.

## What each tab shows

| Tab | Endpoint call | Contents |
|---|---|---|
| Overview | `?view=overview` | Row count + newest timestamp for every table |
| Players | `?view=players&limit=N` / `&player=<id>` | Latest N players (id, schema, save SIZE, timestamps); one explicit id → full record |
| Metrics | `?view=metrics` | Last 7 days: events/day, distinct players/day, trace sessions/day, web_trace error-line count/day, events per event_name per day |
| Traces | `?view=traces` / `&session=<id>` | Recent web_trace sessions; per-session batches with their log lines |

## Security model (plain terms)

- Every request must carry header `X-Admin-Key` matching `ADMIN_DASH_KEY`
  (constant-time compare server-side). Wrong/missing key → 400, no data.
- The endpoint is **GET / read-only**: parameterized SELECTs with hard LIMITs.
  It cannot write, and it cannot be pointed at arbitrary SQL.
- If `ADMIN_DASH_KEY` is not set on Vercel, the endpoint refuses everything
  (fails closed).
- **Rotating the key kills access instantly:** change the env var value on
  Vercel + redeploy; every copy of the old key (including this viewer's
  localStorage) stops working. Do that if the key ever leaks.
- Don't commit the key anywhere; it lives only in Vercel env + your browser.
