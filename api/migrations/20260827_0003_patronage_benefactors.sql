-- =============================================================================
-- 20260827_0003_patronage_benefactors.sql
-- WO-1073 -- the Benefactors of the Realm wall gets its table.
-- -----------------------------------------------------------------------------
-- Run AFTER 0001 and 0002. Then prove it, because the exit code is not evidence:
--     node tools/schema-parity.mjs      -> want SCHEMA_PARITY_OK
--
-- ⛔ READ THIS BEFORE RUNNING ANYTHING ELSE TODAY.
-- api/schema.sql now declares patronage_benefactors. Until this migration has
-- been applied to the provisioned database, `node tools/schema-parity.mjs` will
-- report SCHEMA_PARITY_FAIL with `TABLE MISSING: patronage_benefactors`, and the
-- push gate that consumes that marker will refuse. That failure is CORRECT and
-- is the whole point of the gate: the declaration and the deployed database have
-- genuinely diverged until this file runs.
--
-- ⚠ AND THIS IS THE TRAP 0002 WAS WRITTEN ABOUT, one layer along. api/schema.sql
-- uses CREATE TABLE IF NOT EXISTS and its seeds use ON CONFLICT DO NOTHING, so
-- re-running schema.sql against a live database DOES NOT back-fill anything that
-- already exists in a different shape -- it reports success and does nothing.
-- That already shut two dungeons in production this week. For a brand-new table
-- the CREATE below genuinely does the work; for the INDEXES it does not matter
-- either way. What matters is that nobody concludes "schema.sql was re-run, so
-- we are fine". Verify by SHAPE QUERY, at the bottom of this file.
--
-- AMENDED 2026-08-27, BEFORE FIRST APPLICATION, for the owner's per-patron
-- monument ruling ("being it will be a custom fbx i will work with them one on to
-- create and then add in game"). This file had NOT been run against any database
-- when the three monument columns were added to it, so amending it is correct and
-- a 0004 would have been ceremony. If you believe you already ran an earlier
-- draft, run this file again anyway: every statement in it is guarded and a
-- second run is a no-op, and the ALTER ... ADD COLUMN IF NOT EXISTS block at the
-- bottom exists precisely for that case.
--
-- Additive only: zero DROP, DELETE or TRUNCATE. Touches no existing table, no
-- existing row, and nothing on the money path -- purchase_entitlements is READ
-- by the feature and never written by it.
-- =============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS patronage_benefactors (
    wallet          TEXT        PRIMARY KEY,
    tier_id         TEXT        NOT NULL CHECK (tier_id IN ('founder_benefactor')),
    patron_name     TEXT        NOT NULL,
    patron_name_ci  TEXT        GENERATED ALWAYS AS (lower(patron_name)) STORED,
    name_edits_used INTEGER     NOT NULL DEFAULT 0 CHECK (name_edits_used >= 0),
    monument_asset_id    TEXT   CHECK (monument_asset_id <> 'monument_founder_standin'),
    monument_assigned_at TIMESTAMPTZ,
    monument_verified_at TIMESTAMPTZ,
    granted_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    name_updated_at TIMESTAMPTZ,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ⛔ THE CREATE ABOVE IS SKIPPED WHOLE IF THE TABLE ALREADY EXISTS, and its
-- CHECK constraints are skipped with it -- exactly how purchase_entitlements
-- ended up with a constraint that had never been born (see 0002's header). If a
-- patronage_benefactors table exists from an earlier hand-run, these two ALTERs
-- give it the constraints regardless. They are written so a second run is a
-- no-op rather than an error.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'patronage_benefactors_tier_id_check'
    ) THEN
        ALTER TABLE patronage_benefactors
            ADD CONSTRAINT patronage_benefactors_tier_id_check
            CHECK (tier_id IN ('founder_benefactor'));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'patronage_benefactors_name_edits_used_check'
    ) THEN
        ALTER TABLE patronage_benefactors
            ADD CONSTRAINT patronage_benefactors_name_edits_used_check
            CHECK (name_edits_used >= 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'patronage_benefactors_monument_asset_id_check'
    ) THEN
        ALTER TABLE patronage_benefactors
            ADD CONSTRAINT patronage_benefactors_monument_asset_id_check
            CHECK (monument_asset_id <> 'monument_founder_standin');
    END IF;
END
$$;

-- The per-patron monument columns, added the SAME way for the SAME reason: if an
-- earlier draft of this file already created the table, the CREATE above was
-- skipped whole and these three columns were never born with it.
ALTER TABLE patronage_benefactors ADD COLUMN IF NOT EXISTS monument_asset_id    TEXT;
ALTER TABLE patronage_benefactors ADD COLUMN IF NOT EXISTS monument_assigned_at TIMESTAMPTZ;
ALTER TABLE patronage_benefactors ADD COLUMN IF NOT EXISTS monument_verified_at TIMESTAMPTZ;

CREATE UNIQUE INDEX IF NOT EXISTS uq_patronage_benefactors_name_ci
    ON patronage_benefactors (patron_name_ci);

CREATE INDEX IF NOT EXISTS idx_patronage_benefactors_wall
    ON patronage_benefactors (tier_id, granted_at ASC);

COMMIT;

-- =============================================================================
-- VERIFY BY SHAPE, NOT BY EXIT CODE. Expect 11 rows from the first query and
-- 3 rows from the second.
-- =============================================================================
-- SELECT column_name, data_type
--   FROM information_schema.columns
--  WHERE table_schema = 'public' AND table_name = 'patronage_benefactors'
--  ORDER BY ordinal_position;
-- (11 rows: the three monument columns are part of the expected shape.)
--
-- SELECT conname, pg_get_constraintdef(oid)
--   FROM pg_constraint
--  WHERE conrelid = 'patronage_benefactors'::regclass AND contype = 'c';
--
-- Then, the gate that actually matters:
--   node tools/schema-parity.mjs      -> SCHEMA_PARITY_OK
-- =============================================================================
