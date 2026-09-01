BEGIN;

CREATE TABLE IF NOT EXISTS public_town_showcases (
    owner_wallet           TEXT        PRIMARY KEY,
    showcase_id            TEXT        NOT NULL UNIQUE,
    public_owner_id        TEXT        NOT NULL UNIQUE,
    current_version        BIGINT      NOT NULL DEFAULT 0 CHECK (current_version >= 0),
    published              BOOLEAN     NOT NULL DEFAULT FALSE,
    published_at           TIMESTAMPTZ,
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (showcase_id ~ '^sh_[A-Za-z0-9_-]{16,93}$'),
    CHECK (public_owner_id ~ '^po_[A-Za-z0-9_-]{16,93}$')
);

CREATE TABLE IF NOT EXISTS public_town_snapshot_versions (
    owner_wallet           TEXT        NOT NULL,
    showcase_id            TEXT        NOT NULL,
    snapshot_version       BIGINT      NOT NULL CHECK (snapshot_version >= 1),
    schema_version         INTEGER     NOT NULL CHECK (schema_version = 1),
    catalog_version        INTEGER     NOT NULL CHECK (catalog_version >= 1),
    minimum_client_version TEXT        NOT NULL,
    structures             JSONB       NOT NULL,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (owner_wallet, snapshot_version),
    FOREIGN KEY (owner_wallet) REFERENCES public_town_showcases(owner_wallet) ON DELETE CASCADE,
    FOREIGN KEY (showcase_id) REFERENCES public_town_showcases(showcase_id) ON DELETE CASCADE,
    UNIQUE (showcase_id, snapshot_version),
    CHECK (jsonb_typeof(structures) = 'array'),
    CHECK (jsonb_array_length(structures) <= 300)
);

CREATE INDEX IF NOT EXISTS idx_public_town_showcases_directory
    ON public_town_showcases (published, updated_at DESC)
    WHERE published = TRUE;

COMMIT;
