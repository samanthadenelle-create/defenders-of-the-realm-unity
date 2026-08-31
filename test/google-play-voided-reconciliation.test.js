'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const voided = require('../api/_lib/google-play-voided-reconciliation');
const route = require('../api/admin/google-play-voided-reconcile');

const PACKAGE = 'com.denellestudios.echoesofelarion';
const TOKEN = 'V'.repeat(30);

function fakeSql(sequence) {
    const calls = [];
    let index = 0;
    const sql = (strings, ...values) => {
        calls.push({ text: strings.join('?'), values });
        const next = sequence[index++];
        return Promise.resolve(typeof next === 'function' ? next(strings.join('?'), values) : (next || []));
    };
    sql.calls = calls;
    return sql;
}

function item(overrides) {
    return { purchaseToken: TOKEN, orderId: 'GPA.1', purchaseTimeMillis: '1000',
        voidedTimeMillis: '2000', voidedSource: 2, voidedReason: 7,
        voidedQuantity: 1, ...overrides };
}

test('voided reconciliation is independently default-off', () => {
    const env = { GOOGLE_PLAY_BILLING_ENABLED: 'true', GOOGLE_PLAY_PACKAGE_NAME: PACKAGE,
        GOOGLE_PLAY_ACCOUNT_BINDING_KEY: 'binding', GOOGLE_PLAY_SERVICE_ACCOUNT_JSON: JSON.stringify({
            type: 'service_account', client_email: 'play@example.iam.gserviceaccount.com',
            private_key: '-----BEGIN PRIVATE KEY-----\nx\n-----END PRIVATE KEY-----' }) };
    assert.equal(voided.configurationReady(env).code, 'play_voided_reconciliation_disabled');
    assert.equal(voided.configurationReady({ ...env,
        GOOGLE_PLAY_VOIDED_RECONCILIATION_ENABLED: 'true' }).ok, true);
});

test('scheduled route requires the cron secret or explicit admin key', () => {
    const env = { CRON_SECRET: 'cron-secret', ADMIN_DASH_KEY: 'admin-secret' };
    assert.equal(route.isAuthorized({ headers: { authorization: 'Bearer cron-secret' } }, env), true);
    assert.equal(route.isAuthorized({ headers: { 'x-admin-key': 'admin-secret' } }, env), true);
    assert.equal(route.isAuthorized({ headers: { authorization: 'Bearer wrong' } }, env), false);
    assert.equal(route.isAuthorized({ headers: {} }, {}), false);
});

test('window overlaps successful cursor and clamps stale cursors to API 30-day limit', () => {
    const now = 40 * 24 * 60 * 60 * 1000;
    assert.deepEqual(voided.boundedWindow(now - 1000, now, 5000),
        { startTime: now - 6000, endTime: now });
    assert.equal(voided.boundedWindow(1, now, 5000).startTime,
        now - voided.DEFAULT_LOOKBACK_MS);
});

test('API pagination uses token alone after first page and includes partial refunds', async () => {
    const requests = [];
    const responses = [{ voidedPurchases: [], tokenPagination: { nextPageToken: 'next' } },
        { voidedPurchases: [] }];
    const fetchFn = async url => { requests.push(new URL(url)); return {
        ok: true, status: 200, json: async () => responses.shift() }; };
    const first = await voided.fetchVoidedPage(PACKAGE, 'secret',
        { startTime: 10, endTime: 20, pageToken: '' }, { fetchFn });
    await voided.fetchVoidedPage(PACKAGE, 'secret',
        { startTime: 10, endTime: 20, pageToken: first.nextPageToken }, { fetchFn });
    assert.equal(requests[0].searchParams.get('startTime'), '10');
    assert.equal(requests[0].searchParams.get('includeQuantityBasedPartialRefund'), 'true');
    assert.equal(requests[0].searchParams.get('type'), '1');
    assert.equal(requests[1].searchParams.get('token'), 'next');
    assert.equal(requests[1].searchParams.has('startTime'), false);
    assert.doesNotMatch(requests.map(String).join('\n'), /secret/);
});

test('paginated overlap run dedupes observations, quarantines all, and advances cursor last', async () => {
    const sql = fakeSql([
        [{ last_success_end_time_ms: '9000' }],
        [{ purchase_token: TOKEN }], [{ event_fingerprint: 'inserted' }],
        [], [], // unknown token lookup + duplicate event insert
        [], // cursor upsert
    ]);
    const pages = [
        { items: [item()], nextPageToken: 'page-2' },
        { items: [item({ purchaseToken: 'W'.repeat(30), orderId: 'GPA.2' })], nextPageToken: '' },
    ];
    const result = await voided.reconcile(sql, { credential: {}, packageName: PACKAGE }, {
        nowMs: 10000, overlapMs: 1000,
        serviceAccountAccessToken: async () => 'access-token',
        fetchVoidedPage: async (_pkg, access, query) => {
            assert.equal(access, 'access-token');
            if (query.pageToken) assert.equal(query.pageToken, 'page-2');
            return pages.shift();
        },
    });
    assert.equal(result.pages, 2);
    assert.equal(result.observed, 2);
    assert.equal(result.inserted, 1);
    assert.equal(result.quarantined, 2);
    assert.match(sql.calls.at(-1).text, /google_play_voided_cursors/);
    assert.doesNotMatch(sql.calls.map(call => call.text).join('\n'),
        /UPDATE google_play_purchases|DELETE FROM google_play_purchases/);
});

test('malformed API evidence is durably quarantined without a purchase lookup', async () => {
    const sql = fakeSql([[{ event_fingerprint: 'bad' }]]);
    const result = await voided.recordItem(sql, PACKAGE, item({ purchaseToken: 'short' }));
    assert.equal(result.reason, 'malformed_google_void');
    assert.equal(sql.calls.length, 1);
    assert.match(sql.calls[0].text, /google_play_voided_events/);
});

test('missing voided quantity remains null because it denotes a full refund', () => {
    const normalized = voided.normalizeVoidedPurchase(item({ voidedQuantity: undefined }));
    assert.equal(normalized.ok, true);
    assert.equal(normalized.quantity, null);
});

test('a failed later page never advances the successful-window cursor', async () => {
    const sql = fakeSql([[]]);
    let calls = 0;
    await assert.rejects(() => voided.reconcile(sql,
        { credential: {}, packageName: PACKAGE }, {
            nowMs: 10000,
            serviceAccountAccessToken: async () => 'access-token',
            fetchVoidedPage: async () => {
                if (++calls === 1) return { items: [], nextPageToken: 'page-2' };
                throw new Error('transient publisher outage');
            },
        }), /transient publisher outage/);
    assert.equal(sql.calls.some(call => /google_play_voided_cursors/.test(call.text) &&
        /INSERT INTO/.test(call.text)), false);
});

test('migration makes evidence dedupe and quarantine explicit without reversal claims', () => {
    const migration = fs.readFileSync(path.join(__dirname, '..', 'api', 'migrations',
        '20260830_0016_google_play_voided_reconciliation.sql'), 'utf8');
    assert.match(migration, /event_fingerprint\s+TEXT PRIMARY KEY/);
    assert.match(migration, /WHERE status = 'quarantined'/);
    assert.match(migration, /last_success_end_time_ms/);
    assert.doesNotMatch(migration, /(?:DELETE|UPDATE)\s+google_play_purchases/i);
});
