BEGIN;

-- WO-1277 category voting v2. This is deliberately additive: the earlier
-- contest-wide prototype remains readable for audit, while all runtime routes
-- use these category-qualified tables. No contest, category, candidate, tier,
-- vote, result, grant, or reversal is seeded by this migration.
CREATE TABLE IF NOT EXISTS showcase_contest_categories (
    contest_id TEXT NOT NULL REFERENCES showcase_contests(contest_id) ON DELETE RESTRICT,
    category_id TEXT NOT NULL CHECK (category_id ~ '^[a-z0-9][a-z0-9_-]{1,31}$'),
    label TEXT NOT NULL CHECK (char_length(label) BETWEEN 1 AND 80),
    vote_weight NUMERIC(8,4) NOT NULL DEFAULT 1 CHECK (vote_weight > 0),
    discovery_salt TEXT NOT NULL CHECK (char_length(discovery_salt) BETWEEN 16 AND 128),
    rules_version INTEGER NOT NULL DEFAULT 1 CHECK (rules_version > 0),
    active BOOLEAN NOT NULL DEFAULT FALSE,
    authored_by TEXT NOT NULL,
    authored_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (contest_id, category_id)
);

CREATE TABLE IF NOT EXISTS showcase_contest_category_candidates (
    contest_id TEXT NOT NULL,
    category_id TEXT NOT NULL,
    showcase_id TEXT NOT NULL,
    snapshot_version BIGINT NOT NULL CHECK (snapshot_version >= 1),
    eligible BOOLEAN NOT NULL DEFAULT FALSE,
    eligibility_reason TEXT,
    authored_by TEXT NOT NULL,
    entered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (contest_id, category_id, showcase_id),
    FOREIGN KEY (contest_id, category_id)
        REFERENCES showcase_contest_categories(contest_id, category_id) ON DELETE RESTRICT,
    FOREIGN KEY (showcase_id, snapshot_version)
        REFERENCES public_town_snapshot_versions(showcase_id, snapshot_version) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS showcase_contest_category_votes (
    contest_id TEXT NOT NULL,
    category_id TEXT NOT NULL,
    voter_wallet TEXT NOT NULL,
    showcase_id TEXT NOT NULL,
    cast_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (contest_id, category_id, voter_wallet),
    FOREIGN KEY (contest_id, category_id, showcase_id)
        REFERENCES showcase_contest_category_candidates(contest_id, category_id, showcase_id)
        ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS showcase_contest_category_reward_tiers (
    contest_id TEXT NOT NULL,
    category_id TEXT NOT NULL,
    tier_id TEXT NOT NULL CHECK (tier_id ~ '^[a-z0-9][a-z0-9_-]{1,31}$'),
    placement_from INTEGER NOT NULL CHECK (placement_from >= 1),
    placement_to INTEGER NOT NULL CHECK (placement_to >= placement_from),
    cosmetic_sku TEXT NOT NULL REFERENCES catalog_items(sku) ON DELETE RESTRICT,
    duration_days INTEGER CHECK (duration_days IS NULL OR duration_days > 0),
    authored_by TEXT NOT NULL,
    authored_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (contest_id, category_id, tier_id),
    FOREIGN KEY (contest_id, category_id)
        REFERENCES showcase_contest_categories(contest_id, category_id) ON DELETE RESTRICT,
    UNIQUE (contest_id, category_id, placement_from),
    UNIQUE (contest_id, category_id, placement_to)
);

CREATE TABLE IF NOT EXISTS showcase_contest_result_runs (
    result_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    contest_id TEXT NOT NULL,
    category_id TEXT NOT NULL,
    rules_version INTEGER NOT NULL CHECK (rules_version > 0),
    finalized_by TEXT NOT NULL,
    finalized_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (contest_id, category_id),
    FOREIGN KEY (contest_id, category_id)
        REFERENCES showcase_contest_categories(contest_id, category_id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS showcase_contest_result_rows (
    result_id BIGINT NOT NULL REFERENCES showcase_contest_result_runs(result_id) ON DELETE RESTRICT,
    showcase_id TEXT NOT NULL REFERENCES public_town_showcases(showcase_id) ON DELETE RESTRICT,
    placement INTEGER NOT NULL CHECK (placement >= 1),
    vote_count BIGINT NOT NULL CHECK (vote_count >= 0),
    weighted_score NUMERIC(20,4) NOT NULL CHECK (weighted_score >= 0),
    PRIMARY KEY (result_id, showcase_id),
    UNIQUE (result_id, placement)
);

CREATE TABLE IF NOT EXISTS showcase_contest_result_reversals (
    result_id BIGINT PRIMARY KEY REFERENCES showcase_contest_result_runs(result_id) ON DELETE RESTRICT,
    reversed_by TEXT NOT NULL,
    reason TEXT NOT NULL CHECK (char_length(reason) BETWEEN 3 AND 500),
    reversed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION reject_showcase_v2_audit_mutation() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'showcase voting audit rows are immutable';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS showcase_category_votes_immutable ON showcase_contest_category_votes;
CREATE TRIGGER showcase_category_votes_immutable BEFORE UPDATE OR DELETE ON showcase_contest_category_votes
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();
DROP TRIGGER IF EXISTS showcase_category_candidates_immutable ON showcase_contest_category_candidates;
CREATE TRIGGER showcase_category_candidates_immutable BEFORE UPDATE OR DELETE ON showcase_contest_category_candidates
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();
DROP TRIGGER IF EXISTS showcase_categories_immutable ON showcase_contest_categories;
CREATE TRIGGER showcase_categories_immutable BEFORE UPDATE OR DELETE ON showcase_contest_categories
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();
DROP TRIGGER IF EXISTS showcase_category_tiers_immutable ON showcase_contest_category_reward_tiers;
CREATE TRIGGER showcase_category_tiers_immutable BEFORE UPDATE OR DELETE ON showcase_contest_category_reward_tiers
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();
DROP TRIGGER IF EXISTS showcase_result_runs_immutable ON showcase_contest_result_runs;
CREATE TRIGGER showcase_result_runs_immutable BEFORE UPDATE OR DELETE ON showcase_contest_result_runs
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();
DROP TRIGGER IF EXISTS showcase_result_rows_immutable ON showcase_contest_result_rows;
CREATE TRIGGER showcase_result_rows_immutable BEFORE UPDATE OR DELETE ON showcase_contest_result_rows
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();
DROP TRIGGER IF EXISTS showcase_result_reversals_immutable ON showcase_contest_result_reversals;
CREATE TRIGGER showcase_result_reversals_immutable BEFORE UPDATE OR DELETE ON showcase_contest_result_reversals
    FOR EACH ROW EXECUTE FUNCTION reject_showcase_v2_audit_mutation();

CREATE INDEX IF NOT EXISTS idx_showcase_category_votes_count
    ON showcase_contest_category_votes (contest_id, category_id, showcase_id);

COMMIT;
