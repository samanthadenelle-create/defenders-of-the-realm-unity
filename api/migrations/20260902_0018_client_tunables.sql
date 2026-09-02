-- =============================================================================
-- PROD-022 — client_tunables, the remote knobs the Pi crash loop is bisected with.
-- -----------------------------------------------------------------------------
-- ⛔ RUN THIS BEFORE DEPLOYING THE BUILD THAT READS IT. The ship chain proved
-- exactly why this file has to exist: command-centre step 3 refused with
--     SCHEMA_PARITY_FAIL 1 problem(s)  /  FAIL  TABLE MISSING: client_tunables
-- because the table was added to api/schema.sql with NO migration beside it.
-- schema.sql describes the shape; a migration is the only thing that MAKES it.
-- Adding one without the other is how a deployed backend and its database drift.
--
-- Idempotent and purely additive: it creates one new table and touches nothing
-- that exists. No column is dropped, narrowed or re-typed, so it cannot reject a
-- row any current writer could produce, and re-running it is a no-op.
--
-- ⚠ CREATE TABLE IF NOT EXISTS REPORTS SUCCESS AND DOES NOTHING when a table of
--   that name already exists in ANY shape - including a stale one from an earlier
--   attempt. Never take its exit code as proof. Verify by SHAPE:
--       node tools/schema-parity.mjs        -> SCHEMA_PARITY_OK
--   That check compares against api/schema.sql, which is the point.
--
-- FAIL-TO-DEFAULT, and deliberately neither fail-open nor fail-closed, because
-- nothing here is a seal. An unreachable table, a timeout, a malformed row or a
-- missing key leaves the client at its SHIPPING DEFAULT - today's behaviour,
-- byte for byte. Pinned by Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs
-- [tunable-defaults], which drives seven failure paths and re-asserts all eight
-- knobs on each. An EMPTY table is therefore the correct and expected state: it
-- means every knob is at its default. Rows are the exception, not the norm.
--
-- Written by: docs/PROD022_TUNABLE_FLAGS.md (the owner-facing flag table),
--             api/_lib/tunables.js (TUNABLE_KEYS - the server-side key domain),
--             tools/client-tunables.mjs / command-centre.ps1 -Tunables (the lever).
-- Read by:    api/client-tunables.js (public unauthenticated GET, 10s edge cache).
-- =============================================================================

CREATE TABLE IF NOT EXISTS client_tunables (
    key        TEXT        PRIMARY KEY,
    value      TEXT        NOT NULL,
    updated_by TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- No seed rows, on purpose. A seeded row would mean the build ships with a knob
-- already overridden, and the whole design is that a fresh database reproduces
-- today's behaviour exactly. The bisect starts from "everything default".
