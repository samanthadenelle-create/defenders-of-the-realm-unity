BEGIN;

CREATE TABLE IF NOT EXISTS catalog_items (
    sku TEXT PRIMARY KEY,
    item_kind TEXT NOT NULL CHECK (item_kind IN
        ('building','pack','tower','cosmetic','decoration','offer')),
    definition JSONB NOT NULL DEFAULT '{}'::jsonb,
    version INTEGER NOT NULL CHECK (version > 0),
    active BOOLEAN NOT NULL DEFAULT FALSE,
    min_client_version TEXT,
    packaged_fallback_key TEXT,
    fallback_sku TEXT REFERENCES catalog_items(sku) ON DELETE RESTRICT,
    asset_url TEXT,
    asset_sha256 TEXT CHECK (asset_sha256 ~ '^[0-9a-f]{64}$'),
    asset_size_bytes BIGINT CHECK (asset_size_bytes > 0),
    asset_version INTEGER CHECK (asset_version > 0),
    expiry_behavior TEXT NOT NULL DEFAULT 'lock'
        CHECK (expiry_behavior IN ('hide','lock','fallback')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (
        (asset_url IS NULL AND asset_sha256 IS NULL AND asset_size_bytes IS NULL AND asset_version IS NULL)
        OR
        (asset_url IS NOT NULL AND asset_sha256 IS NOT NULL AND asset_size_bytes IS NOT NULL AND asset_version IS NOT NULL)
    ),
    CHECK (fallback_sku IS NULL OR fallback_sku <> sku)
);

CREATE TABLE IF NOT EXISTS catalog_collections (
    collection_id TEXT PRIMARY KEY,
    context TEXT NOT NULL CHECK (context IN ('build','shop','owned','showcase')),
    title TEXT NOT NULL,
    subtitle TEXT,
    icon_key TEXT,
    icon_url TEXT,
    icon_sha256 TEXT CHECK (icon_sha256 ~ '^[0-9a-f]{64}$'),
    version INTEGER NOT NULL CHECK (version > 0),
    active BOOLEAN NOT NULL DEFAULT FALSE,
    starts_at TIMESTAMPTZ,
    ends_at TIMESTAMPTZ,
    min_client_version TEXT,
    fallback_collection_id TEXT REFERENCES catalog_collections(collection_id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK ((icon_url IS NULL AND icon_sha256 IS NULL) OR
           (icon_url IS NOT NULL AND icon_sha256 IS NOT NULL)),
    CHECK (ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at),
    CHECK (fallback_collection_id IS NULL OR fallback_collection_id <> collection_id)
);

CREATE TABLE IF NOT EXISTS catalog_collection_items (
    collection_id TEXT NOT NULL REFERENCES catalog_collections(collection_id) ON DELETE CASCADE,
    sku TEXT NOT NULL REFERENCES catalog_items(sku) ON DELETE RESTRICT,
    display_order INTEGER NOT NULL CHECK (display_order >= 0),
    badge TEXT,
    visibility_rule JSONB NOT NULL DEFAULT '{}'::jsonb,
    PRIMARY KEY (collection_id, sku),
    UNIQUE (collection_id, display_order)
);

CREATE INDEX IF NOT EXISTS idx_catalog_collections_active
    ON catalog_collections (context, active, starts_at, ends_at);
CREATE INDEX IF NOT EXISTS idx_catalog_collection_items_order
    ON catalog_collection_items (collection_id, display_order);

CREATE TABLE IF NOT EXISTS sku_entitlements (
    entitlement_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    wallet TEXT NOT NULL,
    sku TEXT NOT NULL REFERENCES catalog_items(sku) ON DELETE RESTRICT,
    grant_id TEXT NOT NULL UNIQUE,
    source_kind TEXT NOT NULL CHECK (source_kind IN
        ('progression','tournament','promotion','community','operator','migration')),
    source_ref TEXT,
    quantity INTEGER NOT NULL DEFAULT 1 CHECK (quantity > 0),
    state TEXT NOT NULL DEFAULT 'active' CHECK (state IN ('active','revoked')),
    granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ,
    revoked_at TIMESTAMPTZ,
    revoke_reason TEXT,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (expires_at IS NULL OR expires_at > granted_at),
    CHECK ((state = 'active' AND revoked_at IS NULL AND revoke_reason IS NULL) OR
           (state = 'revoked' AND revoked_at IS NOT NULL AND revoke_reason IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS idx_sku_entitlements_wallet_active
    ON sku_entitlements (wallet, state, expires_at, granted_at DESC);
CREATE INDEX IF NOT EXISTS idx_sku_entitlements_sku
    ON sku_entitlements (sku, granted_at DESC);

COMMIT;
