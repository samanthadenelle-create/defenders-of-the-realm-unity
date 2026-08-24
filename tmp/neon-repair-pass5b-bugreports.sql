-- =============================================================================
-- Neon repair — 2026-08-24, PASS 5b.  bug_reports: REBUILD, not patch.
--
-- ⛔ STEP 1 SETTLED IT — WRITES HAVE ALWAYS FAILED, and not for the reason first
-- suspected. The deployed table is a DIFFERENT DESIGN, not an older subset:
--
--   DEPLOYED                        api/schema.sql DECLARES
--   id          text  NOT NULL      report_id   BIGINT GENERATED ALWAYS AS IDENTITY PK
--               (NO DEFAULT)
--   created_at  bigint              created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
--   user_agent  text                (not declared)
--   client_ts   text                (not declared)
--
-- `id` is TEXT NOT NULL WITH NO DEFAULT, and ALL FIVE of api/bug-report.js's
-- INSERT shapes omit it. So every insert fails a not-null violation before
-- `RETURNING report_id` is even reached. Zero rows all-time is explained.
--
-- ⭐ AND ZERO ROWS IS WHY THIS IS A REBUILD RATHER THAN A MIGRATION: there is no
-- data to preserve, so aligning to the declared schema costs nothing and leaves
-- the database matching api/schema.sql exactly — which is what
-- tools/schema-parity.mjs will enforce from now on. Patching columns onto a
-- different design would leave a table that matches no known version, which is
-- how this became invisible in the first place.
--
-- ⚠ RUN STEP 0 FIRST. If it returns anything other than 0, STOP and tell me —
-- the whole justification for dropping is that the table is empty.
-- =============================================================================

-- ── 0. PROVE IT IS EMPTY. Must be 0. ───────────────────────────────────────
SELECT COUNT(*) AS rows_that_would_be_lost FROM bug_reports;


-- ── 1. Rebuild to match api/schema.sql exactly. ────────────────────────────
DROP TABLE IF EXISTS bug_reports;

CREATE TABLE IF NOT EXISTS bug_reports (
    report_id    BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    description  TEXT        NOT NULL,               -- "description" (<= 4000 chars)
    route        TEXT,                               -- context.route (active scene name)
    app_version  TEXT,                               -- context.appVersion (Application.version)
    player_id    TEXT,                               -- client-side SALTED HASH of the Pi uid
    wallet       TEXT,                               -- SERVER-VERIFIED wallet, or NULL. Never a claim.
    context      JSONB       NOT NULL DEFAULT '{}',  -- full "context" object, future-proofed
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ⭐ CARRY THE created_at INDEX FORWARD. The dropped table had TWO of them
-- (bug_reports_created_idx and idx_bug_reports_created), which is duplication —
-- but it is also two independent votes that someone wanted this lookup, and the
-- read view orders by created_at. Recreated ONCE, under the name api/admin/db.js
-- and the rest of the codebase would expect.
--
-- ⚠ user_agent and client_ts are DELIBERATELY NOT carried forward: they are not
-- declared in api/schema.sql, api/bug-report.js does not write them, and that
-- endpoint already folds anything it has no column for into `context` — so the
-- information is not lost, it just stops having a bespoke column nothing fills.
CREATE INDEX IF NOT EXISTS idx_bug_reports_created ON bug_reports (created_at DESC);

-- ⭐ `wallet` ADDED TO THE REBUILD RATHER THAN AS A LATER ALTER (owner ruling
-- 2026-08-24). This table was already being rebuilt and the rebuild has NOT been
-- run yet, so the column costs nothing here -- whereas a follow-up ALTER would be a
-- SECOND migration for a human to remember, and the entire reason this ticket
-- exists is that the 2026-08-02 reconcile was authored, committed, and never run.
-- Adding a second file to forget would be repeating the exact failure.
CREATE INDEX IF NOT EXISTS idx_bug_reports_wallet ON bug_reports (wallet) WHERE wallet IS NOT NULL;


-- ── 2. Confirm the shape. Every row should read exists = true. ─────────────
SELECT c.name AS expected_column,
       (EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_name = 'bug_reports' AND column_name = c.name)) AS exists
FROM (VALUES
    ('report_id'), ('description'), ('route'),
    ('app_version'), ('player_id'), ('wallet'), ('context'), ('created_at')
) AS c(name)
ORDER BY exists, c.name;

-- And that the two dropped columns are genuinely gone (they were never declared,
-- and api/bug-report.js folds everything it does not have a column for into
-- `context`, so nothing is lost by their absence):
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'bug_reports'
ORDER BY ordinal_position;


-- ── 3. ⚠ THEN PROVE A WRITE. A schema match is NOT evidence a write succeeds.
-- Submit one report from the device (Settings -> Report a bug) and check:
--
--     SELECT report_id, created_at, route, app_version, left(description, 60)
--     FROM bug_reports ORDER BY created_at DESC LIMIT 5;
--
-- If it is still empty after a real submission, the endpoint is failing for a
-- second reason and the runtime log will name it — /api/bug-report currently
-- shows ZERO requests, so the first thing to confirm is that the form reaches
-- the server at all.
