-- =============================================================================
-- WO-1318 — the Pi (U2A) payment rail.
-- -----------------------------------------------------------------------------
-- ⛔ RUN THIS BEFORE THE FIRST PI PAYMENT, NOT AFTER. /api/pi/complete runs with
-- the Pioneer's Pi ALREADY MOVED and there is no refund route, so every schema
-- fault on that path is discovered with the money gone. That is the WO-1173
-- lesson (purchase_entitlements drifted, a real 391 SKR payment settled and could
-- not be recorded) arriving on a new rail. Verify with:
--     node tools/schema-parity.mjs        -> SCHEMA_PARITY_OK
--
-- Idempotent and additive: every statement widens or relaxes. Nothing here can
-- reject a row the Solana rail could write before.
-- =============================================================================

-- ── 1. purchase_entitlements: admit the Pi rail into THE SAME grant ledger ──
-- Not a second ledger. Revenue reporting, patronage totals and the replay guard
-- all read this one table; a rail with its own grant rows sits outside all three.
ALTER TABLE purchase_entitlements DROP CONSTRAINT IF EXISTS purchase_entitlements_rail_check;
ALTER TABLE purchase_entitlements ADD  CONSTRAINT purchase_entitlements_rail_check
    CHECK (rail IN ('solana','pi'));

ALTER TABLE purchase_entitlements DROP CONSTRAINT IF EXISTS purchase_entitlements_network_check;
ALTER TABLE purchase_entitlements ADD  CONSTRAINT purchase_entitlements_network_check
    CHECK (network IN ('devnet','mainnet','mainnet-beta','pi'));

ALTER TABLE purchase_entitlements DROP CONSTRAINT IF EXISTS purchase_entitlements_currency_check;
ALTER TABLE purchase_entitlements ADD  CONSTRAINT purchase_entitlements_currency_check
    CHECK (currency IN ('SOL','USDC','SKR','PI'));

-- ── 2. purchase_quotes: one quote table, two rails ─────────────────────────
ALTER TABLE purchase_quotes DROP CONSTRAINT IF EXISTS purchase_quotes_network_check;
ALTER TABLE purchase_quotes ADD  CONSTRAINT purchase_quotes_network_check
    CHECK (network IN ('devnet','mainnet-beta','pi'));

ALTER TABLE purchase_quotes DROP CONSTRAINT IF EXISTS purchase_quotes_currency_check;
ALTER TABLE purchase_quotes ADD  CONSTRAINT purchase_quotes_currency_check
    CHECK (currency IN ('SKR','PI'));

-- mint / recipient / recipient_ata are SOLANA facts. A Pi quote has none of them
-- (its payee is the app's own Pi wallet, which Pi resolves from the API key).
-- Dropping NOT NULL cannot invalidate an existing row and cannot change what the
-- Solana path writes — purchases/quote.js still supplies all three, and
-- contractFromQuoteRow() still refuses a row that is missing them.
ALTER TABLE purchase_quotes ALTER COLUMN mint          DROP NOT NULL;
ALTER TABLE purchase_quotes ALTER COLUMN recipient     DROP NOT NULL;
ALTER TABLE purchase_quotes ALTER COLUMN recipient_ata DROP NOT NULL;

-- ── 3. pi_payments: the rail's lifecycle ledger (NOT an entitlement) ───────
-- The Pi twin of google_play_purchases. It records approve -> complete so a
-- replayed callback is recognisable as a replay, and so a payment that dies
-- between the two is findable by a human.
CREATE TABLE IF NOT EXISTS pi_payments (
    payment_id          TEXT PRIMARY KEY,
    player_id           TEXT NOT NULL,
    pi_uid              TEXT NOT NULL,
    sku                 TEXT NOT NULL,
    quote_ref           TEXT NOT NULL,
    amount_base_units   NUMERIC(40,0) NOT NULL CHECK (amount_base_units > 0),
    decimals            SMALLINT NOT NULL CHECK (decimals >= 0 AND decimals <= 18),
    state               TEXT NOT NULL CHECK (state IN
        ('approved','completed','granted','rejected','manual_review')),
    txid                TEXT,
    to_address          TEXT,
    reject_reason       TEXT,
    approved_at         TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    granted_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_pi_payments_player
    ON pi_payments (player_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_pi_payments_attention
    ON pi_payments (updated_at) WHERE state IN ('approved','completed','manual_review');
