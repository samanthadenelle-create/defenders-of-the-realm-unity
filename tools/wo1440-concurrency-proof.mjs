// =============================================================================
// WO-1440 — CONCURRENCY PROOF for the promo tier cap.
// -----------------------------------------------------------------------------
// The question this answers, by MEASUREMENT and not by reading the code:
//   under N simultaneous redeems of ONE promo code, can the tier-1 band over-issue?
//   (WO-1440 §4.1: "500 simultaneous redeems must not yield 501" — a race here is
//    real money.)
//
// It runs the EXACT tiered-currency statement api/promo/redeem.js executes — the
// single UPDATE…RETURNING + INSERT CTE, copied verbatim in shape — against a
// THROWAWAY promo code with tier1_limit = 20, from 50 concurrent HTTP connections
// (the Neon serverless driver opens one per query, so this is genuine parallelism,
// not a loop pretending to be one).
//
// ⛔ IT NEVER TOUCHES FIRSTWATCH. Consuming live campaign ordinals to test the
//    campaign would be the test breaking the thing it certifies. The scratch code is
//    created here and DELETEd at the end (promo_redemptions.code is FK'd
//    ON DELETE CASCADE, so only this code's own rows go with it).
//
// Run: node tools/wo1440-concurrency-proof.mjs
// =============================================================================
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const url = env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, '');
const sql = neon(url);

const CODE = 'WO1440-RACEPROOF';
const TIER1 = 20;
const ACTORS = 50;

async function cleanup() {
    // CASCADE removes only this code's redemptions.
    await sql`DELETE FROM promo_codes WHERE code = ${CODE}`;
}

await cleanup();
await sql`
    INSERT INTO promo_codes
        (code, reward_crystals, reward_coins, message, active,
         max_redemptions, per_player_limit, expires_at,
         tier1_limit, tier2_reward_crystals, tier2_reward_coins, redemption_count)
    VALUES (${CODE}, 500, 500, 'race proof', TRUE,
            NULL, NULL, NULL,
            ${TIER1}, 100, 100, 0)`;

// The statement under test — the same shape as api/promo/redeem.js's
// `hasTieredCurrency` branch, including the ip_hash column added by WO-1440.
function redeemOnce(playerId, ipHash) {
    return sql`
        WITH claimed AS (
            UPDATE promo_codes
               SET redemption_count = redemption_count + 1
             WHERE code = ${CODE}
               AND (max_redemptions IS NULL OR redemption_count < max_redemptions)
             RETURNING redemption_count, tier1_limit, reward_crystals, reward_coins,
                       tier2_reward_crystals, tier2_reward_coins
        ), recorded AS (
            INSERT INTO promo_redemptions
                (code, player_id, crystals, coins, pack_sku, redemption_ordinal, ip_hash)
            SELECT ${CODE}, ${playerId},
                   CASE WHEN redemption_count <= tier1_limit
                        THEN reward_crystals ELSE tier2_reward_crystals END,
                   CASE WHEN redemption_count <= tier1_limit
                        THEN reward_coins ELSE tier2_reward_coins END,
                   NULL, redemption_count, ${ipHash}
              FROM claimed
            RETURNING crystals, coins, redemption_ordinal
        )
        SELECT crystals, coins, redemption_ordinal FROM recorded`;
}

const started = Date.now();
const results = await Promise.allSettled(
    Array.from({ length: ACTORS }, (_, i) =>
        redeemOnce('guest-local-' + String(i).padStart(2, '0').repeat(32).slice(0, 64), 'raceproof' + (i % 3))),
);
const elapsed = Date.now() - started;

const fulfilled = results.filter(r => r.status === 'fulfilled');
const rejected = results.filter(r => r.status === 'rejected');

const rows = await sql`
    SELECT crystals, coins, redemption_ordinal
      FROM promo_redemptions WHERE code = ${CODE} ORDER BY redemption_ordinal`;
const counter = await sql`SELECT redemption_count FROM promo_codes WHERE code = ${CODE}`;

const tier1Rows = rows.filter(r => Number(r.crystals) === 500);
const tier2Rows = rows.filter(r => Number(r.crystals) === 100);
const ordinals = rows.map(r => Number(r.redemption_ordinal));
const distinctOrdinals = new Set(ordinals);

console.log('--- WO-1440 tier-cap concurrency proof ---');
console.log('actors (concurrent):        ', ACTORS);
console.log('wall clock ms:              ', elapsed);
console.log('statements fulfilled:       ', fulfilled.length);
console.log('statements rejected:        ', rejected.length,
    rejected.length ? rejected.slice(0, 2).map(r => String(r.reason).slice(0, 120)) : '');
console.log('ledger rows written:        ', rows.length);
console.log('promo_codes.redemption_count', Number(counter[0].redemption_count));
console.log('TIER-1 grants (500 crystals):', tier1Rows.length, ' <-- must equal tier1_limit =', TIER1);
console.log('TIER-2 grants (100 crystals):', tier2Rows.length);
console.log('distinct ordinals:          ', distinctOrdinals.size, 'of', ordinals.length);
console.log('ordinal range:              ', Math.min(...ordinals), '..', Math.max(...ordinals));

const ok =
    rows.length === ACTORS &&
    tier1Rows.length === TIER1 &&
    tier2Rows.length === ACTORS - TIER1 &&
    distinctOrdinals.size === ACTORS &&
    Math.min(...ordinals) === 1 &&
    Math.max(...ordinals) === ACTORS &&
    Number(counter[0].redemption_count) === ACTORS &&
    // every tier-1 row must be an ordinal <= TIER1 and every tier-2 row above it
    tier1Rows.every(r => Number(r.redemption_ordinal) <= TIER1) &&
    tier2Rows.every(r => Number(r.redemption_ordinal) > TIER1);

await cleanup();
const left = await sql`SELECT COUNT(*)::int AS n FROM promo_redemptions WHERE code = ${CODE}`;
console.log('scratch rows remaining after cleanup:', left[0].n);

console.log(ok && left[0].n === 0 ? 'WO1440_TIER_CAP_ATOMIC_OK' : 'WO1440_TIER_CAP_ATOMIC_FAILED');
