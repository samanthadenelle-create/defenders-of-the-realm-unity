-- =============================================================================
-- test-data.sql — load sample rows + read them back, to prove the backend persists
-- the client's JSON and enforces types. Run AFTER schema.sql, in Neon SQL Editor
-- (Dashboard → SQL Editor → paste → Run). All test rows use the 'test-' / 'TEST'
-- prefix so the CLEANUP block at the bottom removes exactly them.
-- =============================================================================

-- ── 1. A full player save (the /api/game/save path) ───────────────────────────
-- game_state mirrors the client's SyncDeltaPayload (snake_case, all keys optional).
-- Upsert so re-running is idempotent (same as save.js's ON CONFLICT DO UPDATE).
INSERT INTO player_data (player_id, schema_version, game_state)
VALUES (
  'test-wallet-0001',
  10,
  '{
     "bestWave": 17,
     "crystals": 1490,
     "food": 60, "coins": 230, "voidshards": 4,
     "stone": 88, "iron": 41, "wood": 173,
     "towers":         [1,0,2,0,1,3,0,0,1],
     "towerAbilities": [0,0,1,0,0,2,0,0,0],
     "pets":      [ { "id": "Aether", "level": 3, "bondRank": 1 } ],
     "ownedPets": [ "Aether", "Flame", "Ice" ],
     "starterPetId": "Aether"
   }'::jsonb
)
ON CONFLICT (player_id) DO UPDATE
  SET game_state = EXCLUDED.game_state,
      schema_version = EXCLUDED.schema_version,
      updated_at = NOW();

-- ── 2. One row per feature table (the other 6 endpoints) ──────────────────────
INSERT INTO analytics_events (player_id, event_name, properties, client_ts)
VALUES ('test-wallet-0001', 'wave_cleared', '{"wave":17,"timeSec":92}'::jsonb, 1717180000000)
ON CONFLICT DO NOTHING;

-- promo: TEST10 already seeded by schema.sql; record a redemption for our test player.
INSERT INTO promo_redemptions (code, player_id, reward)
VALUES ('TEST10', 'test-wallet-0001', '{"crystals":1000}'::jsonb)
ON CONFLICT (code, player_id) DO NOTHING;

INSERT INTO referrals (player_id, code)
VALUES ('test-wallet-0001', 'TESTREF1')
ON CONFLICT (player_id) DO NOTHING;

INSERT INTO referral_claims (claimer_id, code)
VALUES ('test-wallet-0002', 'TESTREF1')
ON CONFLICT (claimer_id) DO NOTHING;

INSERT INTO tower_swaps (player_id, wave_id, from_tower, to_tower, currency, cost_usdc, tx_sig, client_ts)
VALUES ('test-wallet-0001', 17, 2, 5, 'USDC', 0.50, 'test-txsig-0001', 1717180000)
ON CONFLICT DO NOTHING;

INSERT INTO bug_reports (description, context)
VALUES ('test: terrain bump at village edge', '{"route":"Village","appVersion":"dev"}'::jsonb)
ON CONFLICT DO NOTHING;

-- ── 3. READ EVERYTHING BACK (prove persistence + JSON round-trip + types) ──────
-- Whole save blob + a few extracted/typed fields (note ->> is text, the casts prove types).
SELECT player_id, schema_version,
       (game_state->>'bestWave')::int   AS best_wave,
       (game_state->>'crystals')::int   AS crystals,
       jsonb_array_length(game_state->'towers')   AS tower_slots,
       jsonb_array_length(game_state->'ownedPets') AS owned_pets,
       game_state->'pets'->0->>'id'     AS first_pet,
       updated_at
FROM player_data WHERE player_id = 'test-wallet-0001';

SELECT 'analytics_events'  AS tbl, count(*) FROM analytics_events  WHERE player_id='test-wallet-0001'
UNION ALL SELECT 'promo_redemptions', count(*) FROM promo_redemptions WHERE player_id='test-wallet-0001'
UNION ALL SELECT 'referrals',         count(*) FROM referrals         WHERE player_id='test-wallet-0001'
UNION ALL SELECT 'referral_claims',   count(*) FROM referral_claims   WHERE code='TESTREF1'
UNION ALL SELECT 'tower_swaps',       count(*) FROM tower_swaps       WHERE player_id='test-wallet-0001'
UNION ALL SELECT 'bug_reports',       count(*) FROM bug_reports       WHERE description LIKE 'test:%';

-- Full listings (the "load whole listings from tables" check) — uncomment to dump:
-- SELECT * FROM player_data       WHERE player_id LIKE 'test-%';
-- SELECT * FROM analytics_events  WHERE player_id LIKE 'test-%';
-- SELECT * FROM tower_swaps       WHERE player_id LIKE 'test-%';

-- ── 4. CLEANUP (run this block to remove all test rows) ───────────────────────
-- DELETE FROM referral_claims   WHERE claimer_id LIKE 'test-%' OR code='TESTREF1';
-- DELETE FROM referrals         WHERE player_id  LIKE 'test-%';
-- DELETE FROM promo_redemptions WHERE player_id  LIKE 'test-%';
-- DELETE FROM analytics_events  WHERE player_id  LIKE 'test-%';
-- DELETE FROM tower_swaps       WHERE player_id  LIKE 'test-%';
-- DELETE FROM bug_reports       WHERE description LIKE 'test:%';
-- DELETE FROM player_data       WHERE player_id  LIKE 'test-%';
