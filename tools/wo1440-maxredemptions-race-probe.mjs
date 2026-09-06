// =============================================================================
// WO-1440 — measure the OTHER cap path: `max_redemptions` (step 4 of redeem.js).
// -----------------------------------------------------------------------------
// The tiered ordinal path is proven atomic (wo1440-concurrency-proof.mjs). This
// probe measures the SEPARATE, count-based gate that a code with max_redemptions
// set goes through — a `SELECT COUNT(*)` followed later by an INSERT, i.e. two
// statements and therefore two transactions on the Neon HTTP driver.
//
// It exists because the remedy recommended to the owner for the FIRSTWATCH tail is
// `UPDATE promo_codes SET max_redemptions = 500`, and a recommendation must not
// rest on an assumption about how tightly that bound holds. Measured, not assumed.
//
// Scratch code only; never touches FIRSTWATCH. Cleaned up at the end.
// Run: node tools/wo1440-maxredemptions-race-probe.mjs
// =============================================================================
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const url = env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, '');
const sql = neon(url);

const CODE = 'WO1440-MAXRACE';
const MAX = 20;
const ACTORS = 50;

async function cleanup() { await sql`DELETE FROM promo_codes WHERE code = ${CODE}`; }

await cleanup();
await sql`
    INSERT INTO promo_codes
        (code, reward_crystals, reward_coins, message, active,
         max_redemptions, per_player_limit, expires_at, tier1_limit, redemption_count)
    VALUES (${CODE}, 500, 500, 'max race probe', TRUE, ${MAX}, NULL, NULL, NULL, 0)`;

// BEFORE (the shape this file was written to indict): step 4's count, then a bare
// INSERT. Two statements, therefore two transactions. Kept so the fix can be shown
// to fix something rather than merely asserted to.
async function redeemOnceOld(playerId) {
    const countRows = await sql`SELECT COUNT(*)::int AS n FROM promo_redemptions WHERE code = ${CODE}`;
    if (countRows[0].n >= MAX) return 'ALREADY_REDEEMED';
    await sql`
        INSERT INTO promo_redemptions (code, player_id, crystals, coins, pack_sku, contents, ip_hash)
        VALUES (${CODE}, ${playerId}, 500, 500, NULL, NULL, 'maxrace')`;
    return 'GRANTED';
}

// AFTER (WO-1440): the claim and the insert are ONE statement, serialising on the
// promo_codes row — the plain-currency branch of api/promo/redeem.js, verbatim in shape.
async function redeemOnceNew(playerId) {
    const rows = await sql`
        WITH claimed AS (
            UPDATE promo_codes
               SET redemption_count = redemption_count + 1
             WHERE code = ${CODE}
               AND (max_redemptions IS NULL OR redemption_count < max_redemptions)
             RETURNING redemption_count
        ), recorded AS (
            INSERT INTO promo_redemptions
                (code, player_id, crystals, coins, pack_sku, contents, redemption_ordinal, ip_hash)
            SELECT ${CODE}, ${playerId}, 500, 500, NULL, NULL, redemption_count, 'maxrace'
              FROM claimed
            RETURNING crystals, coins
        )
        SELECT crystals, coins FROM recorded`;
    return rows.length === 1 ? 'GRANTED' : 'ALREADY_REDEEMED';
}

const MODE = process.argv[2] === 'old' ? 'old' : 'new';
const redeemOnce = MODE === 'old' ? redeemOnceOld : redeemOnceNew;

const out = await Promise.allSettled(
    Array.from({ length: ACTORS }, (_, i) =>
        redeemOnce('guest-local-' + String(i).padStart(2, '0').repeat(32).slice(0, 64))));

const granted = out.filter(r => r.status === 'fulfilled' && r.value === 'GRANTED').length;
const refused = out.filter(r => r.status === 'fulfilled' && r.value === 'ALREADY_REDEEMED').length;
const rows = await sql`SELECT COUNT(*)::int AS n FROM promo_redemptions WHERE code = ${CODE}`;

console.log('--- WO-1440 max_redemptions race probe --- mode:', MODE);
console.log('max_redemptions:  ', MAX);
console.log('concurrent actors:', ACTORS);
console.log('GRANTED returned: ', granted);
console.log('refused:          ', refused);
console.log('ledger rows:      ', rows[0].n, ' <-- OVERSHOOT =', rows[0].n - MAX);

await cleanup();
console.log(rows[0].n > MAX
    ? 'WO1440_MAXREDEMPTIONS_IS_NOT_ATOMIC (overshoot measured)'
    : 'WO1440_MAXREDEMPTIONS_HELD_IN_THIS_RUN');
