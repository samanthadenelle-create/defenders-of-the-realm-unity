'use strict';

// WO-1073 architecture slice. The server owns lifetime patronage; clients may
// render the resolved tier later, but never calculate spend or grant a tier.
// SPL settlement is irreversible, so the aggregate is monotonic by design.

const PATRONAGE_TIERS = Object.freeze([
    Object.freeze({
        id: 'patron',
        label: 'Patron',
        thresholdUsdCents: 5000,
        unlocks: Object.freeze([
            Object.freeze({ rail: 'cosmetic', capability: 'patron_crest' }),
            Object.freeze({ rail: 'cosmetic', capability: 'profile_border' }),
            Object.freeze({ rail: 'cosmetic', capability: 'banner_component' }),
        ]),
    }),
    Object.freeze({
        id: 'high_patron',
        label: 'High Patron',
        thresholdUsdCents: 15000,
        unlocks: Object.freeze([
            Object.freeze({ rail: 'cosmetic', capability: 'kingdom_decoration' }),
            Object.freeze({ rail: 'cosmetic', capability: 'animated_heraldry' }),
            Object.freeze({ rail: 'cosmetic', capability: 'premium_heart_aura' }),
        ]),
    }),
    Object.freeze({
        id: 'founder_benefactor',
        label: 'Founder / Benefactor',
        thresholdUsdCents: 50000,
        unlocks: Object.freeze([
            Object.freeze({ rail: 'cosmetic', capability: 'patron_monument' }),
            Object.freeze({ rail: 'cosmetic', capability: 'player_house_inscription' }),
            Object.freeze({ rail: 'cosmetic', capability: 'animated_kingdom_marker' }),
        ]),
    }),
]);

/** Parse Postgres NUMERIC text without floating-point threshold drift. */
function usdToCents(value) {
    const text = String(value == null ? '0' : value).trim();
    const match = /^(\d+)(?:\.(\d+))?$/.exec(text);
    if (!match) throw new TypeError(`invalid lifetime USD numeric: ${text}`);
    const fraction = (match[2] || '').padEnd(2, '0');
    if (fraction.slice(2).replace(/0/g, '') !== '')
        throw new RangeError(`lifetime USD has sub-cent precision: ${text}`);
    const cents = (BigInt(match[1]) * 100n) + BigInt(fraction.slice(0, 2) || '0');
    if (cents > BigInt(Number.MAX_SAFE_INTEGER))
        throw new RangeError('lifetime USD exceeds safe patronage range');
    return Number(cents);
}

function resolvePatronageTier(lifetimeUsd) {
    const cents = usdToCents(lifetimeUsd);
    let tier = null;
    for (const candidate of PATRONAGE_TIERS) {
        if (cents < candidate.thresholdUsdCents) break;
        tier = candidate;
    }
    return tier;
}

/**
 * Server-side lifetime aggregate. Every durable entitlement row participates;
 * there is deliberately no SKU, date-window, or fulfillment-status filter.
 * A NULL usd_anchor (the pinned canary protocol) contributes zero via SUM.
 */
async function readLifetimePatronage(sql, wallet) {
    if (typeof sql !== 'function') throw new TypeError('sql tagged-template function is required');
    if (typeof wallet !== 'string' || wallet.trim() === '')
        throw new TypeError('wallet is required');

    const rows = await sql`
        SELECT COALESCE(SUM(usd_anchor), 0)::text AS lifetime_usd
        FROM purchase_entitlements
        WHERE wallet = ${wallet}`;
    const lifetimeUsd = rows && rows[0] ? rows[0].lifetime_usd : '0';
    const lifetimeUsdCents = usdToCents(lifetimeUsd);
    const tier = resolvePatronageTier(lifetimeUsd);
    return Object.freeze({
        wallet,
        lifetimeUsdCents,
        tierId: tier ? tier.id : null,
        tierLabel: tier ? tier.label : null,
    });
}

module.exports = {
    PATRONAGE_TIERS,
    readLifetimePatronage,
    resolvePatronageTier,
    usdToCents,
};
