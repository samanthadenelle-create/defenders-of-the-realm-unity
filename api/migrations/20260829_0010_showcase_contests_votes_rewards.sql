BEGIN;

-- 0009 may already exist without this denormalized opaque key. Backfill it from
-- the internal owner row before tightening constraints; no public/player data is
-- invented, and an inconsistent orphan fails the migration instead of guessing.
ALTER TABLE public_town_snapshot_versions
    ADD COLUMN IF NOT EXISTS showcase_id TEXT;

UPDATE public_town_snapshot_versions v
   SET showcase_id = s.showcase_id
  FROM public_town_showcases s
 WHERE v.owner_wallet = s.owner_wallet
   AND v.showcase_id IS NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public_town_snapshot_versions WHERE showcase_id IS NULL) THEN
        RAISE EXCEPTION 'public town snapshot has no owning showcase id';
    END IF;
END $$;

ALTER TABLE public_town_snapshot_versions
    ALTER COLUMN showcase_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'public.public_town_snapshot_versions'::regclass
           AND conname = 'public_town_snapshot_versions_showcase_id_fkey'
    ) THEN
        ALTER TABLE public_town_snapshot_versions
            ADD CONSTRAINT public_town_snapshot_versions_showcase_id_fkey
            FOREIGN KEY (showcase_id) REFERENCES public_town_showcases(showcase_id) ON DELETE CASCADE;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_public_town_snapshot_showcase_version
    ON public_town_snapshot_versions (showcase_id, snapshot_version);

CREATE TABLE IF NOT EXISTS showcase_contests (
    contest_id TEXT PRIMARY KEY CHECK (contest_id ~ '^[a-z0-9][a-z0-9_-]{2,63}$'),
    title TEXT NOT NULL,
    starts_at TIMESTAMPTZ NOT NULL,
    voting_ends_at TIMESTAMPTZ NOT NULL,
    finalized_at TIMESTAMPTZ,
    finalized_by TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (voting_ends_at > starts_at),
    CHECK ((finalized_at IS NULL AND finalized_by IS NULL) OR
           (finalized_at IS NOT NULL AND finalized_by IS NOT NULL))
);

CREATE TABLE IF NOT EXISTS showcase_contest_candidates (
    contest_id TEXT NOT NULL REFERENCES showcase_contests(contest_id) ON DELETE RESTRICT,
    showcase_id TEXT NOT NULL REFERENCES public_town_showcases(showcase_id) ON DELETE RESTRICT,
    snapshot_version BIGINT NOT NULL CHECK (snapshot_version >= 1),
    eligible BOOLEAN NOT NULL DEFAULT FALSE,
    entered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (contest_id, showcase_id),
    FOREIGN KEY (showcase_id, snapshot_version)
        REFERENCES public_town_snapshot_versions(showcase_id, snapshot_version) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS showcase_contest_votes (
    contest_id TEXT NOT NULL,
    voter_wallet TEXT NOT NULL,
    showcase_id TEXT NOT NULL,
    cast_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (contest_id, voter_wallet),
    FOREIGN KEY (contest_id, showcase_id)
        REFERENCES showcase_contest_candidates(contest_id, showcase_id) ON DELETE RESTRICT
);

CREATE OR REPLACE FUNCTION reject_showcase_vote_mutation() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'showcase votes are immutable';
END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS showcase_votes_immutable ON showcase_contest_votes;
CREATE TRIGGER showcase_votes_immutable
    BEFORE UPDATE OR DELETE ON showcase_contest_votes
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_vote_mutation();

CREATE TABLE IF NOT EXISTS showcase_contest_reward_tiers (
    contest_id TEXT NOT NULL REFERENCES showcase_contests(contest_id) ON DELETE RESTRICT,
    tier_id TEXT NOT NULL CHECK (tier_id ~ '^[a-z0-9][a-z0-9_-]{1,31}$'),
    placement_from INTEGER NOT NULL CHECK (placement_from >= 1),
    placement_to INTEGER NOT NULL CHECK (placement_to >= placement_from),
    cosmetic_sku TEXT NOT NULL REFERENCES catalog_items(sku) ON DELETE RESTRICT,
    duration_days INTEGER CHECK (duration_days IS NULL OR duration_days > 0),
    PRIMARY KEY (contest_id, tier_id),
    UNIQUE (contest_id, placement_from),
    UNIQUE (contest_id, placement_to)
);

CREATE INDEX IF NOT EXISTS idx_showcase_votes_count
    ON showcase_contest_votes (contest_id, showcase_id);

COMMIT;
