-- WO-1255 Lane C. Dormant Google Play purchase ledger.
-- Applying this migration does NOT enable Billing: the route additionally requires
-- GOOGLE_PLAY_BILLING_ENABLED=true and all server credentials/bindings.
-- Purchase tokens are globally unique and never logged. They are stored only here
-- because Google explicitly designates purchaseToken as the dedupe authority.
CREATE TABLE IF NOT EXISTS google_play_purchases (
    purchase_token      TEXT PRIMARY KEY,
    player_id           TEXT NOT NULL,
    package_name        TEXT NOT NULL,
    product_id          TEXT NOT NULL,
    sku                 TEXT NOT NULL,
    product_type        TEXT NOT NULL CHECK (product_type IN ('consumable','non_consumable','subscription')),
    state               TEXT NOT NULL CHECK (state IN
        ('created','pending','purchased','verified','granted','consumed','acknowledged',
         'cancelled','voided','refunded')),
    obfuscated_account_id TEXT NOT NULL,
    google_order_id     TEXT,
    purchase_time       TIMESTAMPTZ,
    verified_at         TIMESTAMPTZ,
    granted_at          TIMESTAMPTZ,
    finalized_at        TIMESTAMPTZ,
    last_google_state   JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (package_name, product_id, purchase_token)
);

CREATE INDEX IF NOT EXISTS idx_google_play_purchases_player
    ON google_play_purchases (player_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_google_play_purchases_unfinished
    ON google_play_purchases (updated_at)
    WHERE state IN ('created','pending','purchased','verified','granted');

-- State changes must be conditional, for example:
-- UPDATE google_play_purchases SET state='verified', verified_at=NOW(), updated_at=NOW()
--  WHERE purchase_token=$1 AND player_id=$2 AND sku=$3 AND state='purchased';
-- Never mutate ownership/product identity after insert.
