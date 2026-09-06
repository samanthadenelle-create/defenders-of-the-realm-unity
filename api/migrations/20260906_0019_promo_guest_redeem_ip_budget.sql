-- =============================================================================
-- 20260906_0019_promo_guest_redeem_ip_budget.sql   (WO-1440)
-- -----------------------------------------------------------------------------
-- Additive and idempotent. Two objects, both in service of ONE ruling:
-- 2026-09-06 the owner reversed the wallet-only rule on /api/promo/redeem so that
-- GUESTS may redeem a promo code (the FIRSTWATCH acquisition campaign is live on X
-- and points at the PUBLISHED dApp-Store build, which cannot be changed in time).
--
-- A guest id is minted by the client, so it is not a scarcity key: one actor can
-- mint unlimited "players". The two things that ARE scarce are (a) the code's own
-- global cap and (b) the caller's IP. This migration provides the storage for (b),
-- plus the attributability the ruling requires ("every guest redemption is logged
-- and attributable ... enough to spot a farming pattern after the fact").
--
-- ⛔ SCOPED TO PROMO REDEEM. Nothing here touches the wallet rail, the save/load
--    rails, purchases or entitlements. The wallet-only rule stands everywhere else.
--
-- Apply:
--     psql "$DATABASE_URL" -f api/migrations/20260906_0019_promo_guest_redeem_ip_budget.sql
-- =============================================================================


-- 1. promo_ip_budget — a FIXED-WINDOW grant budget per (caller IP, promo code).
--
-- Counted on SUCCESSFUL GRANTS ONLY, and only on the guest rail. A typo, an expired
-- code, or the same guest double-tapping never spends a household's budget: the
-- counter is touched after every other gate has passed and immediately before the
-- reward is written. A proven wallet is never counted at all — a family of wallet
-- holders behind one router must not be able to lock each other out.
--
-- ip_hash is the SAME salted 12-hex digest api/_lib/audit.js already writes
-- (hashIp: sha256(ip + IP_SALT) truncated). Deliberately ONE hashing rule for the
-- whole project — a second one would make the abuse signal unjoinable with the
-- auth-reject rows. A raw IP is never stored anywhere in this schema.
--
-- FIXED window, not sliding: `window_started_at` resets when the current window has
-- aged out, so the budget refills in one step 24h after the window's FIRST grant
-- rather than trickling back. Stated plainly because the two behave differently at
-- the boundary and a reader should not have to infer which one this is.
CREATE TABLE IF NOT EXISTS promo_ip_budget (
    ip_hash           TEXT        NOT NULL,            -- audit.hashIp(req) — 12 hex chars, salted, non-reversible
    code              TEXT        NOT NULL,            -- the promo code this budget is for (uppercase)
    window_started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    grants            INTEGER     NOT NULL DEFAULT 0,  -- successful grants inside the CURRENT window
    total_grants      BIGINT      NOT NULL DEFAULT 0,  -- lifetime, never reset — the farming signal
    last_grant_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (ip_hash, code)
);

-- Sweep/aging reads only. api/admin/cleanup.js may prune rows whose last_grant_at
-- is far past; nothing in the request path scans this index.
CREATE INDEX IF NOT EXISTS idx_promo_ip_budget_last_grant
    ON promo_ip_budget (last_grant_at);

-- The after-the-fact clawback query: which IPs took the most of one campaign.
CREATE INDEX IF NOT EXISTS idx_promo_ip_budget_code_total
    ON promo_ip_budget (code, total_grants DESC);


-- 2. promo_redemptions.ip_hash — attributability on the LEDGER ROW itself.
--
-- Without it a guest redemption is attributable to nothing but an id the client
-- chose, which is precisely the property that made the reversal risky. With it,
-- `SELECT ip_hash, COUNT(*) FROM promo_redemptions WHERE code='FIRSTWATCH'
--  GROUP BY 1 ORDER BY 2 DESC` names a farm in one read, and the rows are still
-- there to claw back from.
--
-- ⚠ ADDED BY ALTER, DELIBERATELY NOT INSIDE THE CREATE TABLE BODY in schema.sql —
--   the same choice promo_codes.created_by made (schema.sql §3). tools/schema-parity.mjs
--   reads the CREATE bodies, so a column declared there but not yet migrated onto the
--   live table reads as drift and BLOCKS A DEPLOY. A nullable column added by ALTER
--   costs nothing and cannot wedge a ship.
ALTER TABLE promo_redemptions ADD COLUMN IF NOT EXISTS ip_hash TEXT;
