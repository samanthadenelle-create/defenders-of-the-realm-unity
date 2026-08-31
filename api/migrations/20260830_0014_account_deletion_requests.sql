BEGIN;

CREATE TABLE IF NOT EXISTS account_deletion_requests (
    request_id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id          TEXT        NOT NULL,
    identity_kind      TEXT        NOT NULL CHECK (identity_kind IN ('wallet', 'google', 'guest')),
    request_scope      TEXT        NOT NULL CHECK (request_scope IN ('account', 'associated_data')),
    request_categories TEXT[]      NOT NULL DEFAULT ARRAY[]::TEXT[],
    status             TEXT        NOT NULL DEFAULT 'requested'
                                  CHECK (status IN ('requested', 'in_progress', 'completed', 'rejected', 'cancelled')),
    requested_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at       TIMESTAMPTZ,
    operator_note      TEXT,
    CHECK (
        (request_scope = 'account' AND cardinality(request_categories) = 0)
        OR
        (request_scope = 'associated_data' AND cardinality(request_categories) > 0)
    ),
    CHECK (request_categories <@ ARRAY[
        'cloud_saves', 'gameplay_analytics', 'diagnostics', 'bug_reports'
    ]::TEXT[])
);

CREATE UNIQUE INDEX IF NOT EXISTS account_deletion_requests_one_active_per_player
    ON account_deletion_requests (player_id)
    WHERE status IN ('requested', 'in_progress');

CREATE INDEX IF NOT EXISTS account_deletion_requests_status_requested
    ON account_deletion_requests (status, requested_at);

COMMIT;
