# PROD-017 — `bug_reports` is at an old schema, so in-game bug reports cannot be written

**Status:** READY — ⛔ blocks the player-facing bug channel. **Silo:** Backend/schema.
**Found:** 2026-08-24, while checking whether the owner's remote-session reports had landed. They had not — and this is one reason why.

## Evidence

```
GET /api/admin/db?view=bugreports  →  500
[admin/db] error: NeonDbError: column "report_id" does not exist
bug_reports rows = 0        (all-time)
```

`api/admin/db.js` selects `report_id, created_at, description, route, app_version, player_id` — the deployed table has no `report_id`.

## ⚠ CORRECTION — what is PROVEN, and what I initially over-claimed

This ticket first said zero rows meant **writes are failing**. That was inference, and checking it weakened it:

- ⭐ **`/api/bug-report` has ZERO requests in the runtime-log window.** So zero rows is equally consistent with **the form simply never having been used** — the owner reports through screenshots and remote sessions, not the in-game form.
- ⛔ **What IS proven:** the `report_id` column is missing, so the admin READ view 500s outright, and **4 of the endpoint's 5 INSERT shapes end in `RETURNING report_id`** and would fail.
- ⭐ **But the endpoint is more resilient than that suggests.** It cascades through five shapes — `full` → `no_player_id` → `description_context` → `description_only` → **`description_only_no_returning`** — and that last one needs neither `report_id` nor the optional columns. So a write may well still succeed on the final fallback.

**The honest state: the READ path is definitely broken; the WRITE path is unproven in either direction.** That distinction decides the acceptance test below — a schema match is not evidence a write succeeds, and this ticket must not be closed on one.

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

## ⛔ 2026-08-24 - DO NOT CLOSE YET, AND THE REASON IS THE MOST IMPORTANT PART OF THIS TICKET

Owner: *"think we just fixed this."* The fix is **written, not run** - and this ticket has been
"just fixed" once before.

⭐ **`api/schema.sql` line 615 already carries a reconcile for this exact failure**, dated
**2026-08-02**:

```
-- DRIFT RECONCILE (2026-08-02) - THE REASON bug_reports HAS 0 ROWS.
--     NeonDbError: column "player_id" of relation "bug_reports" does not exist
ALTER TABLE bug_reports ADD COLUMN IF NOT EXISTS report_id BIGINT GENERATED ALWAYS AS IDENTITY;
```

⚠ **Those ALTERs were never executed against Neon.** Proof: on 2026-08-24 the read view still
returned `500 NeonDbError: column "report_id" does not exist`. If the block had run, that column
would exist. So the repair was authored into the schema file, committed, and **never reached the
database** - and nothing noticed for **22 days**, because every gate we run validates the ARTIFACT
and none of them looks at the database the artifact talks to.

That is the whole case for **WO-1173 / `SCHEMA_PARITY_OK`**, which is currently built and
**not wired into any ship chain** - i.e. itself a "complete but uncalled" mechanism, in a repo
where that is the recurring failure. A gate nobody calls would have caught this one on 08-02.

### What actually has to be true before this closes

1. `tmp/neon-repair-pass5b-bugreports.sql` is RUN (STEP 0 must return **0** first).
2. ⚠ **A real submission is proven to land.** The SQL file says it in its own STEP 3: *"a schema
   match is NOT evidence a write succeeds."* `/api/bug-report` currently shows **ZERO requests**, so
   the very first thing to confirm is that the in-game form reaches the server at all - a perfect
   table with no caller is still zero rows.
3. The endpoint code side (five-shape INSERT cascade + swallowed catches) is verified against the
   rebuilt shape - in flight, WO-1169 troubleshoot pillar.
4. `schema-parity` reports `SCHEMA_PARITY_OK` for `bug_reports` - the check that would have made the
   08-02 miss impossible.

⛔ Closing on the strength of the file being correct is exactly what happened on 2026-08-02.
