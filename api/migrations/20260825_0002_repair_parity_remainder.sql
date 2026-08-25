-- =============================================================================
-- 20260825_0002_repair_parity_remainder.sql
-- The four drifts that survived 20260824_0001, and WHY they survived.
-- -----------------------------------------------------------------------------
-- Run 0001 first. Then this. Then prove it:
--     node tools/schema-parity.mjs      -> want SCHEMA_PARITY_OK
--
-- WHY THERE IS A SECOND MIGRATION AT ALL - this is the useful part:
--
--   0001 reported SUCCESS and left four problems standing. Two of them are the
--   `CREATE TABLE IF NOT EXISTS` trap, exactly as predicted: purchase_quotes and
--   purchase_entitlements ALREADY EXISTED, so the CREATE was skipped whole - and
--   the CHECK constraints written inside those CREATE statements were skipped
--   with it. The tables looked right; their constraints were never born.
--
--   The other two columns live on tables 0001 never touched at all.
--
--   This is why the exit code of a migration is not evidence. The shape query is.
--
-- STOP ADDING A CHECK VALIDATES EVERY EXISTING ROW.
--   If any live row violates one of these, that ALTER fails and the whole
--   transaction rolls back - nothing is half-applied. That is the correct
--   outcome, not a bug: it means real data disagrees with api/schema.sql and a
--   human has to decide which is wrong. The error will name the constraint.
--   STOP Do NOT "fix" that by widening the CHECK to fit the bad rows. On the money
--   path a widened CHECK is how an unexpected currency gets accepted silently.
--
-- Additive only: zero DROP, DELETE or TRUNCATE. Never deletes application rows.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. promo_codes.reward_pack_sku  (api/schema.sql:340)
--    NULL = use reward_crystals/coins; SET = grant that pack's whole contents.
--    Nullable by design, so no backfill and no default.
-- ---------------------------------------------------------------------------
ALTER TABLE promo_codes
    ADD COLUMN IF NOT EXISTS reward_pack_sku TEXT;

-- ---------------------------------------------------------------------------
-- 2. tower_swaps.verification  (api/schema.sql:564)
--    'onchain' | 'client-claimed'. Existing deployments predate the column and
--    every pre-existing row was written with NO on-chain check at all, so
--    'client-claimed' is the TRUTHFUL backfill - not a convenient one.
--    STOP NEVER read a tower_swaps row as proof of payment without checking it.
-- ---------------------------------------------------------------------------
ALTER TABLE tower_swaps
    ADD COLUMN IF NOT EXISTS verification TEXT NOT NULL DEFAULT 'client-claimed';

-- ---------------------------------------------------------------------------
-- 3. purchase_entitlements.rail CHECK  (api/schema.sql:918)
--    The table exists; its CHECK does not, because CREATE TABLE IF NOT EXISTS
--    skipped the whole statement. Postgres has no ADD CONSTRAINT IF NOT EXISTS,
--    so guard on pg_constraint. The parity tool matches a CHECK by the COLUMN it
--    constrains, not by constraint name, so the name here is free.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_class rel ON rel.oid = c.conrelid
        WHERE c.contype = 'c'
          AND rel.relname = 'purchase_entitlements'
          AND pg_get_constraintdef(c.oid) ILIKE '%rail%'
    ) THEN
        ALTER TABLE purchase_entitlements
            ADD CONSTRAINT purchase_entitlements_rail_check
            CHECK (rail IN ('solana'));
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 4. purchase_quotes.currency CHECK  (api/schema.sql:997)
--    Same cause as 3. STOP SKR only - this is the quote the wallet is asked to
--    sign against, and a wider set here means a quote could be issued in a
--    currency /verify does not price.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_class rel ON rel.oid = c.conrelid
        WHERE c.contype = 'c'
          AND rel.relname = 'purchase_quotes'
          AND pg_get_constraintdef(c.oid) ILIKE '%currency%'
    ) THEN
        ALTER TABLE purchase_quotes
            ADD CONSTRAINT purchase_quotes_currency_check
            CHECK (currency IN ('SKR'));
    END IF;
END $$;

COMMIT;

-- =============================================================================
-- AFTERWARDS: node tools/schema-parity.mjs
-- The migration completing is NOT the proof. SCHEMA_PARITY_OK is.
-- =============================================================================
