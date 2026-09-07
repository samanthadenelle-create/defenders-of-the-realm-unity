'use strict';

// =============================================================================
// WO-1506 — /api/events/track accepted a CLIENT-ASSERTED playerId with no auth
// and no rate limit.
// -----------------------------------------------------------------------------
// The route wrote `analytics_events.player_id` straight from the request body, so
// anyone could write unbounded rows attributed to any wallet — and those rows feed
// the retention/funnel numbers the owner makes business decisions from.
//
// The fix, and what this file pins:
//   1. The row is bound to the CALLER, never to the body. A verified session
//      (X-Session) names the wallet; an X-Guest-Id binds to that guest id; with
//      neither, the row lands under the literal id `unverified` so one entry in
//      ANALYTICS_EXCLUDED_PLAYER_IDS removes the whole unproven bucket.
//   2. The shared IP budget (api/_lib/ip-budget.js, WO-1456) rate-limits the
//      route — FAIL-OPEN, because analytics must never take the game down.
//   3. The success path still works: ordinary events still land (memory
//      `prove-the-success-path-not-just-the-refusal`).
//
// ⚠ WO §4 acceptance 2 asked for a "server-minted guest id" for anonymous events.
//   No such minting helper exists in this project (guest ids are minted on the
//   DEVICE), and a per-request server id is either unbounded cardinality or
//   forgeable. Per the lane instruction, anonymous traffic is instead TAGGED
//   `unverified`. Recorded here rather than quietly reinterpreted (§11B).
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const trackSrc = fs.readFileSync(path.join(root, 'api/events/track.js'), 'utf8');
const track = require('../api/events/track.js');

const WALLET_A = 'B1oNqzGevRmYh6Ntcx2p9Y1oPd3TzZ7vGkR4sQwJc2aE';
const WALLET_B = 'C7uKpMtA4wLdNq8Rr2fY6HbVzXe1JmS9TgW3vPc5DkQn';
const GUEST = 'guest-local-' + 'a'.repeat(64);   // wallet-auth.GUEST_RE, read at source

/**
 * One fake Neon client serving BOTH call shapes the route uses:
 *   - tagged template  → wallet-auth.verifySession, ip-budget's UPSERT
 *   - sql(text, params) → the multi-row analytics insert
 * Every statement is recorded so a test can assert what did NOT happen.
 */
function fakeSql(opts = {}) {
    const calls = [];
    const fn = (a, ...rest) => {
        const tagged = Array.isArray(a) && Array.isArray(a.raw);
        const text = tagged ? a.join('?') : String(a);
        const values = tagged ? rest : (rest[0] || []);
        calls.push({ text, values, tagged });

        if (/auth_sessions/.test(text)) {
            return Promise.resolve(opts.sessionRows || []);
        }
        if (/promo_ip_budget/.test(text)) {
            if (opts.budgetError) return Promise.reject(opts.budgetError);
            const g = opts.grants == null ? 1 : opts.grants;
            return Promise.resolve([{ grants: g, total_grants: g }]);
        }
        return Promise.resolve([]);
    };
    fn.calls = calls;
    fn.inserts = () => calls.filter((c) => /INSERT INTO analytics_events/.test(c.text));
    return fn;
}

function makeReq(events, headers = {}) {
    return { method: 'POST', headers: Object.assign({ 'x-forwarded-for': '203.0.113.7' }, headers), body: { events } };
}

function makeRes() {
    const res = {
        statusCode: null, body: null, headers: {},
        setHeader(k, v) { this.headers[k.toLowerCase()] = v; },
        status(c) { this.statusCode = c; return this; },
        json(b) { this.body = b; return this; },
        end() { return this; },
    };
    return res;
}

async function run(sql, req) {
    const handler = track._test.makeHandler({ getSql: () => sql });
    const res = makeRes();
    await handler(req, res);
    return res;
}

/** player_id is parameter 1 of each 4-parameter row tuple. */
function insertedPlayerIds(sql) {
    const ins = sql.inserts();
    if (ins.length === 0) return [];
    const p = ins[0].values;
    const out = [];
    for (let i = 0; i < p.length; i += 4) out.push(p[i]);
    return out;
}

function insertedProps(sql) {
    const ins = sql.inserts();
    if (ins.length === 0) return [];
    const p = ins[0].values;
    const out = [];
    for (let i = 2; i < p.length; i += 4) out.push(JSON.parse(p[i]));
    return out;
}

// ── 1. THE HOLE: a client-asserted wallet id ─────────────────────────────────

test('an asserted wallet id with NO auth headers is overridden, never written', async () => {
    const sql = fakeSql();
    const res = await run(sql, makeReq([{ playerId: WALLET_A, eventName: 'session_start', clientTs: 1 }]));

    assert.equal(res.statusCode, 200);
    const ids = insertedPlayerIds(sql);
    assert.deepEqual(ids, ['unverified'],
        'the body-asserted wallet reached analytics_events — anyone can still attribute rows to any wallet');
    assert.equal(insertedProps(sql)[0]._auth, 'unverified',
        'the row is not self-describing; a reader cannot tell proven traffic from asserted traffic');
});

test('a valid session OVERRIDES the asserted id — the token names the player, the body never does', async () => {
    const sql = fakeSql({ sessionRows: [{ wallet: WALLET_B, revoked: false, expired: false }] });
    const res = await run(sql, makeReq(
        [{ playerId: WALLET_A, eventName: 'wave_completed', clientTs: 2 }],
        { 'x-session': 'a'.repeat(48) },
    ));

    assert.equal(res.statusCode, 200);
    assert.deepEqual(insertedPlayerIds(sql), [WALLET_B],
        'a session for wallet B wrote a row for the wallet the BODY named — the token is not binding');
    assert.equal(insertedProps(sql)[0]._auth, 'session');
});

test('an unknown/expired session does not grant a wallet id — it degrades to unverified', async () => {
    const sql = fakeSql({ sessionRows: [] });
    await run(sql, makeReq(
        [{ playerId: WALLET_A, eventName: 'session_start', clientTs: 3 }],
        { 'x-session': 'b'.repeat(48) },
    ));
    assert.deepEqual(insertedPlayerIds(sql), ['unverified'],
        'an unknown session token still bought the asserted wallet id');
});

test('X-Guest-Id binds the row to that guest id', async () => {
    const sql = fakeSql();
    await run(sql, makeReq(
        [{ playerId: WALLET_A, eventName: 'session_start', clientTs: 4 }],
        { 'x-guest-id': GUEST },
    ));
    assert.deepEqual(insertedPlayerIds(sql), [GUEST]);
    assert.equal(insertedProps(sql)[0]._auth, 'guest');
});

test('a MALFORMED guest header buys nothing', async () => {
    const sql = fakeSql();
    await run(sql, makeReq(
        [{ playerId: WALLET_A, eventName: 'session_start', clientTs: 5 }],
        { 'x-guest-id': WALLET_A },
    ));
    assert.deepEqual(insertedPlayerIds(sql), ['unverified'],
        'a wallet-shaped string in X-Guest-Id was accepted as an identity');
});

test('the guest rail never spends the SAVE budget (guest_rate_limit is keyed on guest id and shared with save/load)', async () => {
    const sql = fakeSql();
    await run(sql, makeReq([{ playerId: GUEST, eventName: 'session_start', clientTs: 6 }], { 'x-guest-id': GUEST }));
    assert.equal(sql.calls.filter((c) => /guest_rate_limit/.test(c.text)).length, 0,
        'analytics is spending the guest save budget — a busy funnel would 429 the player\'s own saves');
});

// ── 2. The success path (memory: prove-the-success-path-not-just-the-refusal) ─

test('an ordinary batch still lands, one row per event', async () => {
    const sql = fakeSql({ sessionRows: [{ wallet: WALLET_A, revoked: false, expired: false }] });
    const res = await run(sql, makeReq([
        { playerId: WALLET_A, eventName: 'session_start', properties: '{"k":1}', clientTs: 10 },
        { playerId: WALLET_A, eventName: 'wave_completed', properties: '{"k":2}', clientTs: 11 },
    ], { 'x-session': 'c'.repeat(48) }));

    assert.equal(res.statusCode, 200);
    assert.equal(res.body.success, true);
    assert.equal(res.body.inserted, 2, 'the success path stopped inserting');
    assert.deepEqual(insertedPlayerIds(sql), [WALLET_A, WALLET_A]);
    const props = insertedProps(sql);
    assert.equal(props[0].k, 1, 'client properties were dropped by the auth stamp');
    assert.equal(props[1]._auth, 'session');
});

// ── 3. The IP budget (shared helper, fail-open) ──────────────────────────────

test('a caller past its IP budget is refused and writes NOTHING', async () => {
    const sql = fakeSql({ grants: 999 });
    const res = await run(sql, makeReq([{ playerId: 'x', eventName: 'session_start', clientTs: 20 }]));

    assert.equal(sql.inserts().length, 0, 'a rate-limited caller still wrote analytics rows');
    assert.equal(res.statusCode, 200,
        'a non-2xx makes EventTracker.FlushWithRetry retry the batch 4x — a refusal must not become a storm');
    assert.equal(res.body.success, false);
    assert.equal(res.body.error, 'RATE_LIMITED');
});

test('an UNREADABLE budget table must not take analytics down (fail-open)', async () => {
    const sql = fakeSql({ budgetError: new Error('relation "promo_ip_budget" does not exist') });
    const res = await run(sql, makeReq([{ playerId: 'x', eventName: 'session_start', clientTs: 21 }]));
    assert.equal(res.statusCode, 200);
    assert.equal(res.body.success, true);
    assert.equal(sql.inserts().length, 1, 'a missing budget table stopped every analytics write');
});

test('a malformed request never costs a household a unit of budget', async () => {
    const sql = fakeSql();
    const res = await run(sql, { method: 'POST', headers: {}, body: { nope: true } });
    assert.equal(res.statusCode, 400);
    assert.equal(sql.calls.filter((c) => /promo_ip_budget/.test(c.text)).length, 0,
        'the budget is spent before the free shape checks');
});

// ── 4. One limiter, one implementation ───────────────────────────────────────

test('the route uses the SHARED budget helper, keyed on the one signal a client cannot choose', () => {
    assert.match(trackSrc, /require\(['"]\.\.\/_lib\/ip-budget['"]\)/,
        'events/track.js does not import the shared budget helper');
    assert.match(trackSrc, /hashIp\(req\)/);
    assert.match(trackSrc, /reserveIpBudget\(/);
    const executable = trackSrc.replace(/^\s*\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
    assert.doesNotMatch(executable, /INSERT INTO promo_ip_budget/,
        'a second limiter was inlined into the route — duplicated state');
    assert.doesNotMatch(executable, /ev\.playerId/,
        'the route still reads a playerId off the event body');
});

test('the CORS preflight admits the identity headers it now reads', async () => {
    const handler = track._test.makeHandler({ getSql: () => fakeSql() });
    const res = makeRes();
    await handler({ method: 'OPTIONS', headers: {} }, res);
    const allow = String(res.headers['access-control-allow-headers'] || '');
    assert.match(allow, /X-Session/i, 'the browser preflight would strip X-Session');
    assert.match(allow, /X-Guest-Id/i, 'the browser preflight would strip X-Guest-Id');
});
