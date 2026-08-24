# PROD-017 — `bug_reports` is at an old schema, so in-game bug reports cannot be written

**Status:** READY — ⛔ blocks the player-facing bug channel. **Silo:** Backend/schema.
**Found:** 2026-08-24, while checking whether the owner's remote-session reports had landed. They had not — and this is one reason why.

## Evidence

```
GET /api/admin/db?view=bugreports  →  500
[admin/db] error: NeonDbError: column "report_id" does not exist
bug_reports rows = 0        (all-time)
```

`api/admin/db.js` selects `report_id, created_at, description, route, app_version, player_id` — the deployed table has no `report_id`. ⚠ **Zero rows all-time** is consistent with writes *failing*, not with nobody reporting.

## ⚠ THE FIFTH SCHEMA DRIFT FOUND IN ONE DAY

`dungeon_status` (missing) · `auth_sessions` (missing) · `purchase_quotes` (missing) · `purchase_entitlements` (**old version — cost a real 391 SKR payment that settled and could not be recorded**) · and now `bug_reports` (old version).

⛔ **This one has a second-order cost the others don't.** `BugReportVM` POSTs to `/api/bug-report`, and CLAUDE.md §14's entire premise is that **the owner is never the bug detector**. A dead report table means the player-facing channel silently swallows reports — so the only working path back becomes the owner personally sending screenshots, which is exactly the situation §14 exists to prevent. The channel designed to remove her from the loop had quietly put her back in it.

## Fix

1. Diff the deployed `bug_reports` against `api/schema.sql`; `ALTER TABLE … ADD COLUMN IF NOT EXISTS` for the gap — the same pattern used for `purchase_entitlements` in the 2026-08-24 pass-4 repair.
2. ⚠ Confirm what `api/bug-report.js` actually INSERTs. If it writes columns the table lacks, **the endpoint has been 500ing too** — check the runtime log rather than assuming the read view was the only casualty.
3. **Then submit one report from the device and see the row**, rather than declaring it fixed on a schema match. A schema that matches is not evidence that a write succeeds.

## ⭐ Why WO-1173 blocks go-live

`SCHEMA_PARITY_OK` would have caught all five of these at once, before any of them cost anything. Every other gate was green throughout — they validate the artifact, never the database the artifact talks to.

## Acceptance

- [ ] Columns match `api/schema.sql`
- [ ] A real in-game bug report produces a row, verified in ops
- [ ] Covered by `tools/schema-parity.mjs`
