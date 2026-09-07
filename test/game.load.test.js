'use strict';

// =============================================================================
// WO-1502 - /api/game/load had NO test at all.
// -----------------------------------------------------------------------------
// load.js is the path that hands a returning player their whole town back. It is
// the endpoint WO-1447 shows dropping the base layout, and it had zero coverage:
// every claim about it was a code-read. This file drives the REAL handler and
// asserts the three properties that decide whether a player keeps their save:
//
//   1. AUTH IS REQUIRED. An unauthenticated read of another player's row is the
//      worst outcome this endpoint has, so the refusal is proven for both the
//      401 class and the 400 (malformed identity) class - they are distinct
//      statuses in the handler and a client branches on the difference.
//   2. NO ROW IS A 404 'NO_SAVE', NOT AN EMPTY 200. The client treats a non-2xx
//      as "keep what is local"; a 200 carrying an empty object is exactly how a
//      first-run response overwrites a real town with nothing.
//   3. A STORED ROW COMES BACK WHOLE - the state verbatim, plus schemaVersion
//      and serverLastSeenMs. serverLastSeenMs is the anchor save.js measures an
//      offline-accrual window against, so a null there is a payout bug, and the
//      handler must tolerate BOTH shapes the Neon HTTP driver returns for a
//      TIMESTAMPTZ (a Date, or an ISO string).
//
// memory: prove-the-success-path-not-just-the-refusal - a handler that refused
// every request would satisfy (1) and (2) alone, so (3) is not optional.
//
// ZERO NETWORK, ZERO DATABASE. `@neondatabase/serverless` is replaced in
// require.cache BEFORE load.js is required, so `neon()` hands back a tagged
// template that answers from a variable in this file. `authenticate` and
// `logAuthReject` are swapped on their (plain-object) module exports; NO api/
// source file is modified by this suite.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const Module = require('node:module');

// ── The database stub, installed before load.js can capture the real neon ────

let nextRows = [];          // what the SELECT answers with
let lastQuery = null;       // the assembled SQL text, for shape assertions
let dbThrows = null;

function sqlTag(strings, ...values) {
    lastQuery = strings.raw.join('?');
    if (dbThrows) return Promise.reject(dbThrows);
    return Promise.resolve(nextRows);
}

const neonId = require.resolve('@neondatabase/serverless');
require.cache[neonId] = new Module(neonId, null);
require.cache[neonId].filename = neonId;
require.cache[neonId].loaded = true;
require.cache[neonId].exports = { neon: () => sqlTag };

const walletAuth = require('../api/_lib/wallet-auth.js');
const audit = require('../api/_lib/audit.js');
const { AuthCode } = walletAuth;

// ⭐ ORDER IS LOAD-BEARING: load.js DESTRUCTURES `authenticate` and `logAuthReject`
// at require time, so the swap must land BEFORE that require or the real,
// database-backed gate runs instead of the stub.
//
// The refusal rows are the loud half of "quiet for the player, loud in the db".
// They are captured, not suppressed, so a test can prove the refusal was logged.
let rejects = [];
audit.logAuthReject = async (sql, req, row) => { rejects.push(row); };

let authResult = { ok: true, identity: 'p1', mode: 'guest' };
walletAuth.authenticate = async () => authResult;

const load = require(path.join(__dirname, '..', 'api', 'game', 'load.js'));

// ── A request/response pair the real applyCors/quietFail can drive ───────────

function makeRes() {
    const res = {
        statusCode: null, body: null, headers: {}, ended: false,
        setHeader(k, v) { this.headers[k] = v; },
        status(c) { this.statusCode = c; return this; },
        json(b) { this.body = b; return this; },
        end() { this.ended = true; return this; },
    };
    return res;
}

async function call({ method = 'GET', query = { playerId: 'p1' } } = {}) {
    const req = { method, query, headers: {} };
    const res = makeRes();
    await load(req, res);
    return res;
}

function reset() {
    nextRows = [];
    lastQuery = null;
    dbThrows = null;
    rejects = [];
    authResult = { ok: true, identity: 'p1', mode: 'guest' };
}

// ── 1. AUTH IS REQUIRED ──────────────────────────────────────────────────────

test('an UNAUTHENTICATED read is refused 401 and never touches the table', async () => {
    reset();
    authResult = { ok: false, code: AuthCode.SESSION_INVALID, identity: 'p1', mode: 'wallet' };
    nextRows = [{ game_state: { crystals: 999 }, schema_version: 38, updated_at: new Date() }];

    const res = await call();

    assert.equal(res.statusCode, 401, 'a failed authenticate() did not refuse the read');
    assert.equal(res.body.ok, false);
    assert.equal(res.body.code, AuthCode.SESSION_INVALID,
        'the refusal must name a stable code the client can branch on');
    assert.equal(res.body.data, undefined, "a refused read leaked the row's state");
    assert.equal(lastQuery, null,
        'the SELECT ran anyway - the auth gate must sit BEFORE the query, not beside it');
    assert.equal(rejects.length, 1, 'the refusal was not written to the audit table');
});

test('a MALFORMED identity is a 400, distinguishable from an expired session', async () => {
    // Same rail, different class: 400 means "your id is not a shape we accept"
    // and retrying with a fresh session will never help. Collapsing the two
    // statuses makes a permanently-broken client look like a transient one.
    for (const code of [AuthCode.PLAYER_ID_BAD_SHAPE, AuthCode.WALLET_MALFORMED]) {
        reset();
        authResult = { ok: false, code, identity: 'p1', mode: 'wallet' };
        const res = await call();
        assert.equal(res.statusCode, 400, `${code} answered ${res.statusCode}, expected 400`);
        assert.equal(res.body.code, code);
    }
});

test('a missing playerId is refused before auth or the database', async () => {
    reset();
    const res = await call({ query: {} });
    assert.equal(res.statusCode, 400);
    assert.equal(res.body.code, AuthCode.PLAYER_ID_MISSING);
    assert.equal(lastQuery, null);
});

test('a non-GET method is refused', async () => {
    reset();
    const res = await call({ method: 'POST' });
    assert.equal(res.statusCode, 400);
    assert.equal(res.body.code, AuthCode.METHOD_NOT_ALLOWED);
});

test('OPTIONS is answered as a preflight, so a cross-origin GET can happen at all', async () => {
    reset();
    const res = await call({ method: 'OPTIONS' });
    assert.equal(res.statusCode, 204);
    assert.equal(res.ended, true);
    assert.ok(res.headers['Access-Control-Allow-Origin'], 'no CORS header on the preflight');
});

// ── 2. NO ROW IS A 404 NO_SAVE ───────────────────────────────────────────────

test('no stored row answers 404 NO_SAVE - never an empty 200', async () => {
    reset();
    nextRows = [];

    const res = await call();

    assert.equal(res.statusCode, 404,
        'a first-run player got a 2xx; the client would merge an empty server record ' +
        'over a real local town');
    assert.equal(res.body.ok, false);
    assert.equal(res.body.code, 'NO_SAVE');
    assert.ok(res.body.ref, 'the refusal carries no correlation ref');
});

// ── 3. A STORED ROW COMES BACK WHOLE ─────────────────────────────────────────

test('a stored row returns the state VERBATIM with schemaVersion and serverLastSeenMs', async () => {
    reset();
    const updatedAt = new Date('2026-09-01T12:00:00.000Z');
    nextRows = [{
        game_state: {
            crystals: 1234,
            baseLayout: [{ id: 'towncenter', x: 3, y: 7 }],
            buildQueue: [{ jobId: 'j1', paidWood: 40 }],
            heroLevel: 12,
        },
        schema_version: 38,
        updated_at: updatedAt,
    }];

    const res = await call();

    assert.equal(res.statusCode, 200);
    assert.equal(res.body.ok, true);
    assert.equal(res.body.schemaVersion, 38, 'the row schema_version was not surfaced');
    assert.equal(res.body.serverLastSeenMs, updatedAt.getTime(),
        'serverLastSeenMs is the anchor save.js measures an offline window against');
    assert.equal(typeof res.body.serverNowMs, 'number');
    assert.equal(res.body.mode, 'guest');

    // The whole object, not a hand-listed subset - the WO-1447 failure shape.
    assert.equal(res.body.data.crystals, 1234);
    assert.deepEqual(res.body.data.baseLayout, [{ id: 'towncenter', x: 3, y: 7 }]);
    assert.deepEqual(res.body.data.buildQueue, [{ jobId: 'j1', paidWood: 40 }]);
    assert.equal(res.body.data.heroLevel, 12, 'a key outside the legacy list was dropped');

    // Legacy keys absent from the row are backfilled as explicit nulls, never
    // omitted, so an older client does not trip over a missing member.
    assert.equal(res.body.data.bestWave, null);
    assert.ok('starterPetId' in res.body.data);

    assert.match(lastQuery, /FROM\s+player_data/i);
    assert.match(lastQuery, /schema_version/);
});

test('serverLastSeenMs survives BOTH shapes the Neon driver returns for a TIMESTAMPTZ', async () => {
    const iso = '2026-09-01T12:00:00.000Z';
    for (const ts of [new Date(iso), iso]) {
        reset();
        nextRows = [{ game_state: {}, schema_version: 38, updated_at: ts }];
        const res = await call();
        assert.equal(res.body.serverLastSeenMs, Date.parse(iso),
            `updated_at as ${ts instanceof Date ? 'Date' : 'string'} did not convert`);
    }
});

test('an UNPARSEABLE updated_at is reported as null, never as the epoch', async () => {
    reset();
    nextRows = [{ game_state: {}, schema_version: 38, updated_at: 'not a timestamp' }];
    const res = await call();
    assert.equal(res.body.serverLastSeenMs, null,
        'a parse failure came back as a number - the client would read it as 1970 and ' +
        'pay out an offline window of 56 years');
});

test('a null game_state answers 200 with an object, not a crash or a null body', async () => {
    reset();
    nextRows = [{ game_state: null, schema_version: 38, updated_at: new Date() }];
    const res = await call();
    assert.equal(res.statusCode, 200);
    assert.equal(typeof res.body.data, 'object');
    assert.notEqual(res.body.data, null);
});

test('a database fault is a quiet 500 with a ref, and a loud audit row', async () => {
    reset();
    dbThrows = new Error('connection reset');
    const res = await call();
    assert.equal(res.statusCode, 500);
    assert.equal(res.body.code, AuthCode.SERVER_ERROR);
    assert.ok(res.body.ref);
    assert.doesNotMatch(JSON.stringify(res.body), /connection reset/,
        'the raw driver error was echoed to the client');
    assert.equal(rejects.length, 1, 'the fault was not recorded server-side');
});
