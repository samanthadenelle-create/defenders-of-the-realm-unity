-- WO-1256: one public code may select an immutable pack by redemption ordinal.
ALTER TABLE promo_codes
    ADD COLUMN IF NOT EXISTS tier1_pack_sku TEXT,
    ADD COLUMN IF NOT EXISTS tier1_limit INTEGER,
    ADD COLUMN IF NOT EXISTS tier2_pack_sku TEXT,
    ADD COLUMN IF NOT EXISTS tier2_reward_crystals INTEGER,
    ADD COLUMN IF NOT EXISTS tier2_reward_coins INTEGER,
    ADD COLUMN IF NOT EXISTS redemption_count INTEGER NOT NULL DEFAULT 0;

ALTER TABLE promo_redemptions
    ADD COLUMN IF NOT EXISTS pack_sku TEXT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'promo_codes_tier1_limit_positive'
    ) THEN
        ALTER TABLE promo_codes ADD CONSTRAINT promo_codes_tier1_limit_positive
            CHECK (tier1_limit IS NULL OR tier1_limit > 0);
    END IF;
END $$;

-- Bring counters forward before any tiered campaign is activated.
UPDATE promo_codes p
   SET redemption_count = counts.n
  FROM (
      SELECT code, COUNT(*)::integer AS n
        FROM promo_redemptions
       GROUP BY code
  ) counts
 WHERE counts.code = p.code
   AND p.redemption_count < counts.n;

-- Owner ruling: first 500 get the large pack; all later valid redemptions get tier 2.
-- Release-sealed until the APK containing both SKUs has reached Seeker. Activation is
-- an explicit operator step after the signed-wallet device smoke, never part of migration.
UPDATE promo_codes
   SET active = FALSE
 WHERE code = 'TEST10';

INSERT INTO promo_codes (
    code, reward_crystals, reward_coins, message, active, max_redemptions,
    per_player_limit, expires_at, bound_wallet, reward_pack_sku,
    tier1_pack_sku, tier1_limit, tier2_pack_sku,
    tier2_reward_crystals, tier2_reward_coins
) VALUES (
    'FIRSTWATCH', 500, 500, 'Welcome to the Watch.', TRUE, NULL,
    NULL, '2026-08-31T04:59:00Z', NULL, NULL,
    NULL, 500, NULL, 100, 100
);
