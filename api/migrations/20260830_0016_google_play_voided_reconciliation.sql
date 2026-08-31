-- Default-off Voided Purchases pull reconciliation. Every observation is held
-- for explicit entitlement-reversal handling; this migration grants nothing,
-- revokes nothing, and does not enable Google Play Billing.
CREATE TABLE IF NOT EXISTS google_play_voided_events (
    event_fingerprint  TEXT PRIMARY KEY,
    package_name       TEXT NOT NULL,
    purchase_token     TEXT,
    google_order_id    TEXT,
    purchase_time      TIMESTAMPTZ,
    voided_time        TIMESTAMPTZ,
    voided_source      INTEGER CHECK (voided_source BETWEEN 0 AND 2),
    voided_reason      INTEGER CHECK (voided_reason BETWEEN 0 AND 8),
    voided_quantity    INTEGER CHECK (voided_quantity > 0),
    status             TEXT NOT NULL CHECK (status IN ('quarantined','resolved','ignored')),
    quarantine_reason  TEXT NOT NULL,
    google_payload     JSONB NOT NULL,
    observed_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_google_play_voided_attention
    ON google_play_voided_events (status, observed_at)
    WHERE status = 'quarantined';
CREATE INDEX IF NOT EXISTS idx_google_play_voided_purchase
    ON google_play_voided_events (purchase_token, voided_time DESC);

CREATE TABLE IF NOT EXISTS google_play_voided_cursors (
    package_name                TEXT PRIMARY KEY,
    last_success_start_time_ms  BIGINT NOT NULL CHECK (last_success_start_time_ms >= 0),
    last_success_end_time_ms    BIGINT NOT NULL CHECK (last_success_end_time_ms >= last_success_start_time_ms),
    last_success_at             TIMESTAMPTZ NOT NULL,
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
