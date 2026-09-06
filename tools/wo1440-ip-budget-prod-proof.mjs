// =============================================================================
// WO-1440 — prove the IP budget is LIVE on production, against the real endpoint.
// -----------------------------------------------------------------------------
// A concurrency proof shows the cap holds; this shows the OTHER control actually
// runs in the deployed function rather than only in the source. It creates a
// THROWAWAY promo code (never FIRSTWATCH — proving the campaign by spending 21 of
// its ordinals would be the test destroying the thing it certifies), redeems it
// from 21 distinct guest ids off ONE machine, and asserts:
//     grants 1..20  -> success:true
//     grant  21     -> success:false, error:RATE_LIMITED  (and NOT consumed)
// then deletes the code (FK ON DELETE CASCADE takes only its own rows) and the
// budget row it created.
//
// Run: node tools/wo1440-ip-budget-prod-proof.mjs
// =============================================================================
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));

const BASE = 'https://defenders-of-the-realm-v2.vercel.app';
const CODE = 'WO1440-IPPROOF';
const ATTEMPTS = 21;

async function cleanup() {
    await sql`DELETE FROM promo_codes WHERE code = ${CODE}`;
    await sql`DELETE FROM promo_ip_budget WHERE code = ${CODE}`;
}

await cleanup();
await sql`
    INSERT INTO promo_codes
        (code, reward_crystals, reward_coins, message, active,
         max_redemptions, per_player_limit, expires_at, tier1_limit, redemption_count)
    VALUES (${CODE}, 1, 0, 'ip budget proof', TRUE, NULL, NULL, NULL, NULL, 0)`;

const results = [];
for (let i = 0; i < ATTEMPTS; i++) {
    // A fresh, well-formed guest id per attempt — exactly the Sybil the IP budget
    // exists to price. Deterministic so the run is reproducible.
    const id = 'guest-local-' + (i + 1).toString(16).padStart(2, '0').repeat(32);
    const body = JSON.stringify({ playerId: id, code: CODE, supportsInlinePackRewards: true });
    const res = await fetch(BASE + '/api/promo/redeem', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Guest-Id': id },
        body,
    });
    const json = await res.json().catch(() => ({}));
    results.push({ n: i + 1, status: res.status, body: json });
    console.log(`attempt ${String(i + 1).padStart(2)}  HTTP ${res.status}  ${JSON.stringify(json)}`);
}

const granted = results.filter(r => r.body && r.body.success === true).length;
const limited = results.filter(r => r.body && r.body.error === 'RATE_LIMITED').length;
const ledger = await sql`SELECT COUNT(*)::int AS n FROM promo_redemptions WHERE code = ${CODE}`;
const budget = await sql`SELECT grants, total_grants FROM promo_ip_budget WHERE code = ${CODE}`;

console.log('\n--- WO-1440 IP budget PROD proof ---');
console.log('attempts:            ', ATTEMPTS);
console.log('granted:             ', granted, '(expect 20)');
console.log('RATE_LIMITED:        ', limited, '(expect 1)');
console.log('ledger rows written: ', ledger[0].n, '(expect 20 — the refused attempt was NOT consumed)');
console.log('budget row:          ', JSON.stringify(budget));

const ok = granted === 20 && limited === 1 && ledger[0].n === 20;
await cleanup();
const left = await sql`SELECT COUNT(*)::int AS n FROM promo_ip_budget WHERE code = ${CODE}`;
console.log('scratch budget rows after cleanup:', left[0].n);
console.log(ok && left[0].n === 0 ? 'WO1440_IP_BUDGET_LIVE_OK' : 'WO1440_IP_BUDGET_LIVE_FAILED');
