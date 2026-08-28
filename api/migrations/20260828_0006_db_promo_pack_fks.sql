BEGIN;

-- Apply only after the one-shot packs.json seed. Splitting this from table creation
-- prevents a pre-existing promo SKU from making the bootstrap migration impossible.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'promo_codes_reward_pack_sku_fk') THEN
        ALTER TABLE promo_codes ADD CONSTRAINT promo_codes_reward_pack_sku_fk
            FOREIGN KEY (reward_pack_sku) REFERENCES packs(sku) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'promo_codes_tier1_pack_sku_fk') THEN
        ALTER TABLE promo_codes ADD CONSTRAINT promo_codes_tier1_pack_sku_fk
            FOREIGN KEY (tier1_pack_sku) REFERENCES packs(sku) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'promo_codes_tier2_pack_sku_fk') THEN
        ALTER TABLE promo_codes ADD CONSTRAINT promo_codes_tier2_pack_sku_fk
            FOREIGN KEY (tier2_pack_sku) REFERENCES packs(sku) ON DELETE RESTRICT;
    END IF;
END $$;

COMMIT;
