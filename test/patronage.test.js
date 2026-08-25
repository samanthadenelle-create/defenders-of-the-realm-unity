'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const {
    PATRONAGE_TIERS,
    readLifetimePatronage,
    resolvePatronageTier,
    usdToCents,
} = require('../api/_lib/patronage');

test('the tentative architecture authors exactly the ruled three tiers and stops at $500', () => {
    assert.deepEqual(PATRONAGE_TIERS.map(t => [t.id, t.thresholdUsdCents]), [
        ['patron', 5000],
        ['high_patron', 15000],
        ['founder_benefactor', 50000],
    ]);
    assert.equal(PATRONAGE_TIERS.some(t => t.thresholdUsdCents > 50000), false);
});

test('the patronage table is cosmetic-only and contains zero power or spendable grants', () => {
    const forbidden = /damage|health|power|resource|currency|crystal|coin|wood|stone|iron|timer|speed|tempo|slot|inventory|grant/i;
    for (const tier of PATRONAGE_TIERS) {
        assert.ok(Object.isFrozen(tier));
        assert.ok(Object.isFrozen(tier.unlocks));
        for (const unlock of tier.unlocks) {
            assert.deepEqual(Object.keys(unlock).sort(), ['capability', 'rail']);
            assert.equal(unlock.rail, 'cosmetic');
            assert.doesNotMatch(JSON.stringify(unlock), forbidden);
        }
    }
});

test('tier resolution is monotonic at every exact boundary', () => {
    const cases = [
        ['0', null], ['49.99', null], ['50.00', 'patron'],
        ['149.99', 'patron'], ['150.00', 'high_patron'],
        ['499.99', 'high_patron'], ['500.00', 'founder_benefactor'],
        ['999999.00', 'founder_benefactor'],
    ];
    for (const [usd, expected] of cases)
        assert.equal(resolvePatronageTier(usd)?.id || null, expected, usd);
});

test('USD numeric parsing is exact and rejects float-shaped or sub-cent authority', () => {
    assert.equal(usdToCents('391.0000'), 39100);
    assert.equal(usdToCents('50.00'), 5000);
    assert.throws(() => usdToCents('49.999'), /sub-cent/);
    assert.throws(() => usdToCents('-1'), /invalid/);
    assert.throws(() => usdToCents('NaN'), /invalid/);
});

test('lifetime USD is aggregated server-side across every entitlement for one wallet', async () => {
    let queryText = '';
    let queryValues = [];
    const sql = async (strings, ...values) => {
        queryText = strings.join('?');
        queryValues = values;
        return [{ lifetime_usd: '175.0000' }];
    };
    const result = await readLifetimePatronage(sql, 'wallet-A');

    assert.match(queryText, /SUM\(usd_anchor\)/);
    assert.match(queryText, /FROM purchase_entitlements/);
    assert.match(queryText, /WHERE wallet = \?/);
    assert.doesNotMatch(queryText, /sku|created_at|status\s*=/i);
    assert.deepEqual(queryValues, ['wallet-A']);
    assert.deepEqual(result, {
        wallet: 'wallet-A', lifetimeUsdCents: 17500,
        tierId: 'high_patron', tierLabel: 'High Patron',
    });
});

test('a wallet with no priced entitlements resolves to no tier', async () => {
    const sql = async () => [{ lifetime_usd: '0' }];
    const result = await readLifetimePatronage(sql, 'wallet-empty');
    assert.equal(result.lifetimeUsdCents, 0);
    assert.equal(result.tierId, null);
    assert.equal(result.tierLabel, null);
});

test('the architecture exports status only and cannot flip an entitlement', () => {
    const patronage = require('../api/_lib/patronage');
    assert.deepEqual(Object.keys(patronage).sort(), [
        'PATRONAGE_TIERS', 'readLifetimePatronage',
        'resolvePatronageTier', 'usdToCents',
    ]);
    const source = require('node:fs').readFileSync(
        require.resolve('../api/_lib/patronage'), 'utf8');
    assert.doesNotMatch(source, /INSERT\s+INTO|UPDATE\s+purchase_entitlements|DELETE\s+FROM/i);
});
