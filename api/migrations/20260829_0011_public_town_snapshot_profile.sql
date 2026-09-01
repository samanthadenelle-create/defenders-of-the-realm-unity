BEGIN;

-- WO-1276 v2: additive, owner-opted public profile projection. These columns are
-- deliberately not a save blob and contain only bounded catalog identifiers and
-- aggregate integers. Existing v1 layout-only snapshots remain readable.
ALTER TABLE public_town_snapshot_versions
    DROP CONSTRAINT IF EXISTS public_town_snapshot_versions_schema_version_check;
ALTER TABLE public_town_snapshot_versions
    ADD CONSTRAINT public_town_snapshot_versions_schema_version_check
        CHECK (schema_version IN (1, 2));

ALTER TABLE public_town_snapshot_versions
    ADD COLUMN IF NOT EXISTS equipped_cosmetic_skus JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS public_hero_lineup JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS public_army_lineup JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS selected_echoes JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS echoes_saved INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS banner_sku TEXT,
    ADD COLUMN IF NOT EXISTS title_sku TEXT,
    ADD COLUMN IF NOT EXISTS town_level INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS public_achievement_skus JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS leaderboard_rank INTEGER;

ALTER TABLE public_town_snapshot_versions
    ADD CONSTRAINT public_town_snapshot_cosmetics_shape_check
        CHECK (jsonb_typeof(equipped_cosmetic_skus) = 'array' AND jsonb_array_length(equipped_cosmetic_skus) <= 16),
    ADD CONSTRAINT public_town_snapshot_heroes_shape_check
        CHECK (jsonb_typeof(public_hero_lineup) = 'array' AND jsonb_array_length(public_hero_lineup) <= 4),
    ADD CONSTRAINT public_town_snapshot_army_shape_check
        CHECK (jsonb_typeof(public_army_lineup) = 'array' AND jsonb_array_length(public_army_lineup) <= 12),
    ADD CONSTRAINT public_town_snapshot_echoes_shape_check
        CHECK (jsonb_typeof(selected_echoes) = 'array' AND jsonb_array_length(selected_echoes) <= 4),
    ADD CONSTRAINT public_town_snapshot_achievements_shape_check
        CHECK (jsonb_typeof(public_achievement_skus) = 'array' AND jsonb_array_length(public_achievement_skus) <= 32),
    ADD CONSTRAINT public_town_snapshot_echoes_saved_check CHECK (echoes_saved BETWEEN 0 AND 1000000),
    ADD CONSTRAINT public_town_snapshot_banner_sku_check CHECK (banner_sku IS NULL OR banner_sku ~ '^[a-z0-9][a-z0-9_-]{0,63}$'),
    ADD CONSTRAINT public_town_snapshot_title_sku_check CHECK (title_sku IS NULL OR title_sku ~ '^[a-z0-9][a-z0-9_-]{0,63}$'),
    ADD CONSTRAINT public_town_snapshot_town_level_check CHECK (town_level BETWEEN 1 AND 1000),
    ADD CONSTRAINT public_town_snapshot_leaderboard_rank_check CHECK (leaderboard_rank IS NULL OR leaderboard_rank BETWEEN 1 AND 1000000);

COMMIT;
