-- WO-1173: tracked, idempotent repair for the four production drifts found 2026-08-24.
-- Apply with psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f <this file>, then run
-- node tools/schema-parity.mjs. This migration never deletes application rows.
BEGIN;

-- Repair 1: dungeon_status was absent.
CREATE TABLE IF NOT EXISTS dungeon_status (
    dungeon_id TEXT PRIMARY KEY,
    status TEXT NOT NULL DEFAULT 'open'
        CHECK (status IN ('open','sealed','collapsed','rescue','flooded')),
    headline TEXT,
    body TEXT,
    sigil TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Repair 2: signed wallet sessions had nowhere to persist.
CREATE TABLE IF NOT EXISTS auth_sessions (
    token TEXT PRIMARY KEY,
    wallet TEXT NOT NULL,
    revoked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS auth_sessions_wallet_idx ON auth_sessions (wallet);
CREATE INDEX IF NOT EXISTS auth_sessions_expires_idx ON auth_sessions (expires_at);

-- Repair 3: the quote rail table was absent. This is the current schema shape,
-- including WO-1177's nullable discount audit columns.
CREATE TABLE IF NOT EXISTS purchase_quotes (
    quote_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    quote_ref TEXT NOT NULL UNIQUE,
    wallet TEXT NOT NULL,
    sku TEXT NOT NULL,
    network TEXT NOT NULL CHECK (network IN ('devnet','mainnet-beta')),
    currency TEXT NOT NULL CHECK (currency IN ('SKR')),
    amount_base_units NUMERIC(40,0) NOT NULL CHECK (amount_base_units > 0),
    decimals SMALLINT NOT NULL CHECK (decimals >= 0 AND decimals <= 18),
    mint TEXT NOT NULL,
    recipient TEXT NOT NULL,
    recipient_ata TEXT NOT NULL,
    usd_anchor NUMERIC(12,4) NOT NULL,
    usd_rate NUMERIC(24,12) NOT NULL,
    rate_source TEXT NOT NULL,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    consumed_at TIMESTAMPTZ,
    consumed_tx TEXT,
    discount_bps INT,
    discount_reason TEXT
);
ALTER TABLE purchase_quotes ADD COLUMN IF NOT EXISTS discount_bps INT;
ALTER TABLE purchase_quotes ADD COLUMN IF NOT EXISTS discount_reason TEXT;
CREATE INDEX IF NOT EXISTS idx_purchase_quotes_wallet_sku
    ON purchase_quotes (wallet, sku, issued_at DESC);
CREATE INDEX IF NOT EXISTS idx_purchase_quotes_expiry
    ON purchase_quotes (expires_at) WHERE consumed_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_purchase_quotes_discount
    ON purchase_quotes (wallet, issued_at DESC) WHERE discount_bps IS NOT NULL;

-- Repair 4: purchase_entitlements existed but was narrower than the declaration.
-- CREATE makes this ordered migration usable on a genuinely fresh environment too.
CREATE TABLE IF NOT EXISTS purchase_entitlements (
    entitlement_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tx_signature TEXT NOT NULL UNIQUE,
    wallet TEXT NOT NULL,
    sku TEXT NOT NULL,
    rail TEXT NOT NULL CHECK (rail IN ('solana')),
    network TEXT NOT NULL CHECK (network IN ('devnet','mainnet','mainnet-beta')),
    currency TEXT NOT NULL CHECK (currency IN ('SOL','USDC','SKR')),
    expected_lamports BIGINT NOT NULL CHECK (expected_lamports > 0),
    observed_lamports BIGINT NOT NULL CHECK (observed_lamports > 0),
    recipient TEXT NOT NULL,
    observed_recipient TEXT NOT NULL,
    chain_slot BIGINT,
    status TEXT NOT NULL CHECK (status IN ('verified','fulfilled','manual_review')),
    verified_at TIMESTAMPTZ NOT NULL,
    fulfilled_at TIMESTAMPTZ,
    quote_ref TEXT,
    usd_anchor NUMERIC(12,4),
    usd_rate NUMERIC(24,12),
    rate_source TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (wallet, tx_signature, sku)
);
ALTER TABLE purchase_entitlements ADD COLUMN IF NOT EXISTS quote_ref TEXT;
ALTER TABLE purchase_entitlements ADD COLUMN IF NOT EXISTS usd_anchor NUMERIC(12,4);
ALTER TABLE purchase_entitlements ADD COLUMN IF NOT EXISTS usd_rate NUMERIC(24,12);
ALTER TABLE purchase_entitlements ADD COLUMN IF NOT EXISTS rate_source TEXT;

-- Constraint names can differ across incident-created databases. Replace every
-- CHECK attached to the network column by meaning, then install the declaration.
DO $repair_network_check$
DECLARE constraint_name TEXT;
BEGIN
    FOR constraint_name IN
        SELECT c.conname
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname = 'purchase_entitlements'
          AND c.contype = 'c'
          AND pg_get_constraintdef(c.oid) ~* '\mnetwork\M'
    LOOP
        EXECUTE format('ALTER TABLE purchase_entitlements DROP CONSTRAINT %I', constraint_name);
    END LOOP;

    ALTER TABLE purchase_entitlements
        ADD CONSTRAINT purchase_entitlements_network_check
        CHECK (network IN ('devnet','mainnet','mainnet-beta'));
END
$repair_network_check$;

CREATE INDEX IF NOT EXISTS idx_purchase_entitlements_wallet
    ON purchase_entitlements (wallet, created_at DESC);

COMMIT;
