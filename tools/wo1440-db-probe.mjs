// WO-1440 read-only probe: the FIRSTWATCH row as the DATABASE actually holds it.
// Run: node tools/wo1440-db-probe.mjs
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const m = env.match(/^DATABASE_URL=(.*)$/m);
if (!m) { console.error('no DATABASE_URL'); process.exit(1); }
const url = m[1].trim().replace(/^["']|["']$/g, '');
const sql = neon(url);

const rows = await sql`
    SELECT code, active, reward_crystals, reward_coins, max_redemptions,
           per_player_limit, expires_at, bound_wallet, reward_pack_sku,
           tier1_pack_sku, tier1_limit, tier2_pack_sku,
           tier2_reward_crystals, tier2_reward_coins, redemption_count
      FROM promo_codes WHERE code = 'FIRSTWATCH'`;
console.log('FIRSTWATCH row:', JSON.stringify(rows, null, 2));

const red = await sql`
    SELECT player_id, crystals, coins, redeemed_at
      FROM promo_redemptions WHERE code='FIRSTWATCH' ORDER BY redeemed_at`;
console.log('redemptions:', JSON.stringify(red, null, 2));

const cols = await sql`
    SELECT table_name, column_name, data_type
      FROM information_schema.columns
     WHERE table_schema='public' AND table_name IN ('promo_codes','promo_redemptions','guest_rate_limit')
     ORDER BY table_name, ordinal_position`;
console.log('columns:', JSON.stringify(cols.map(c => `${c.table_name}.${c.column_name}:${c.data_type}`), null, 1));

const idx = await sql`SELECT indexname, indexdef FROM pg_indexes WHERE schemaname='public' AND tablename IN ('promo_codes','promo_redemptions','guest_rate_limit')`;
console.log('indexes:', JSON.stringify(idx, null, 1));
