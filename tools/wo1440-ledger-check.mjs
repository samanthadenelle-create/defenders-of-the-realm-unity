// WO-1440 read-only: current state of the IP budget table and the FIRSTWATCH ledger.
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));

console.log('promo_ip_budget:', JSON.stringify(await sql`SELECT * FROM promo_ip_budget ORDER BY last_grant_at`, null, 1));
console.log('FIRSTWATCH ledger:', JSON.stringify(
    await sql`SELECT player_id, crystals, coins, redemption_ordinal, ip_hash, redeemed_at
                FROM promo_redemptions WHERE code='FIRSTWATCH' ORDER BY redeemed_at`, null, 1));
console.log('FIRSTWATCH row:', JSON.stringify(
    await sql`SELECT redemption_count, max_redemptions, tier1_limit FROM promo_codes WHERE code='FIRSTWATCH'`));
console.log('scratch codes remaining:', JSON.stringify(
    await sql`SELECT code FROM promo_codes WHERE code LIKE 'WO1440%'`));
