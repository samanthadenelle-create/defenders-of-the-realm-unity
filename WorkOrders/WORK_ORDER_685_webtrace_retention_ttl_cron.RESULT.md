# WO-685 RESULT — Web-trace retention / TTL cron (DONE — PO-closed 2026-07-13)

**Committed:** `1f4235fb` (2026-07-13 early, "WO-685 retention cron + WO-685/686/687 lifecycle
work orders"). Deployed with the 07-12 21:26 preview chain (any `vercel deploy` from C:\EOA ships
`api/`). **PO closed the ticket 2026-07-13** (Notion row Done). RESULT written during the sync
handoff.

- Closes security audit H1's missing-TTL half: scheduled cleanup deletes `web_trace` rows past
  the 7-day retention (+ spent/expired `auth_nonces` sweep) so the open `POST /api/trace`
  endpoint can't grow the DB unboundedly.
- Verification path: db-viewer Overview row counts fall after the cron window (owner-visible);
  runtime logs carry the cron's execution echo.
- Siblings still READY (not implemented): WO-686 ingestion hardening (rate limits — audit H1's
  other half), WO-687 read/triage surface. The rate-limit exposure remains OPEN until WO-686.
