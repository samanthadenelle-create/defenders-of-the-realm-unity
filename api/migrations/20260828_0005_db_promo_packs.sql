BEGIN;

CREATE TABLE IF NOT EXISTS packs (
    sku TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    contents JSONB NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    store_visible BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE promo_redemptions ADD COLUMN IF NOT EXISTS contents JSONB;

COMMIT;

-- Next: generate/review/apply tools/seed-promo-packs.mjs output, then run 0006.
