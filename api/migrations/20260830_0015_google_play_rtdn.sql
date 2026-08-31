-- Secure, replay-safe Google Play RTDN inbox. This records lifecycle evidence;
-- it does not activate Billing and does not pretend that marking a row voided
-- reverses an entitlement already applied to the player's save.
CREATE TABLE IF NOT EXISTS google_play_rtdn_messages (
    message_id          TEXT PRIMARY KEY,
    package_name       TEXT NOT NULL,
    notification_kind  TEXT NOT NULL CHECK (notification_kind IN
        ('oneTimeProductNotification','subscriptionNotification',
         'voidedPurchaseNotification','pendingRefundReviewNotification','testNotification')),
    event_time          TIMESTAMPTZ NOT NULL,
    status              TEXT NOT NULL CHECK (status IN
        ('processing','processed','quarantined','retry')),
    quarantine_reason   TEXT,
    purchase_token      TEXT,
    google_order_id     TEXT,
    pending_refund_token TEXT,
    attempts            INTEGER NOT NULL DEFAULT 1 CHECK (attempts > 0),
    processed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_google_play_rtdn_attention
    ON google_play_rtdn_messages (status, updated_at)
    WHERE status IN ('quarantined','retry');
