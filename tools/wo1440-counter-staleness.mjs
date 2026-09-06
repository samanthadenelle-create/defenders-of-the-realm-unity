// WO-1440 read-only: is promo_codes.redemption_count in step with the ledger?
// Decides whether the atomic cap claim may safely key off the counter.
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));
const rows = await sql`
    SELECT c.code, c.redemption_count, c.max_redemptions, c.tier1_limit,
           (SELECT COUNT(*)::int FROM promo_redemptions r WHERE r.code = c.code) AS ledger_rows
      FROM promo_codes c ORDER BY c.code`;
console.log(JSON.stringify(rows, null, 1));
console.log('stale codes (counter < ledger):',
    rows.filter(r => Number(r.redemption_count) < Number(r.ledger_rows)).map(r => r.code));
