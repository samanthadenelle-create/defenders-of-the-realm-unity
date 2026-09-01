'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const Module = require('node:module');
const { readActiveEntitlements } = require('../api/_lib/sku-entitlement-read');

const WALLET_A = '11111111111111111111111111111111';
const WALLET_B = '22222222222222222222222222222222';

function sqlWith(rows) {
    const calls = [];
    const sql = async (strings, ...values) => { calls.push({ text: strings.join('?'), values }); return rows; };
    sql.calls = calls; return sql;
}

function response() {
    const out = { headers: {} };
    const res = { setHeader(k,v){ out.headers[k.toLowerCase()] = v; }, status(c){ out.status=c; return res; },
        json(v){ out.body=v; return res; }, end(){ return res; } };
    return { out, res };
}

function loadEndpoint(neonSql, authResult) {
    const originalLoad = Module._load;
    Module._load = function(request, parent, isMain) {
        if (request === '@neondatabase/serverless') return { neon: () => neonSql };
        if (request === './_lib/wallet-auth' && parent && /api[\\/]entitlements\.js$/.test(parent.filename)) {
            return { verifySession: typeof authResult === 'function' ? authResult : async () => authResult };
        }
        return originalLoad.call(this, request, parent, isMain);
    };
    delete require.cache[require.resolve('../api/entitlements')];
    const endpoint = require('../api/entitlements');
    Module._load = originalLoad;
    return endpoint;
}

test('read query is wallet-isolated, active-only, server-expiry filtered and bounded', async () => {
    const sql = sqlWith([]);
    await readActiveEntitlements(sql, WALLET_A);
    assert.deepEqual(sql.calls[0].values, [WALLET_A]);
    assert.match(sql.calls[0].text, /WHERE wallet = /);
    assert.match(sql.calls[0].text, /state = 'active'/);
    assert.match(sql.calls[0].text, /expires_at IS NULL OR expires_at > NOW\(\)/);
    assert.match(sql.calls[0].text, /LIMIT 500/);
    assert.doesNotMatch(sql.calls[0].text, /purchase_entitlements/);
});

test('restore response contains only safe entitlement fields', async () => {
    const out = await readActiveEntitlements(sqlWith([{
        sku: 'gate_stone', quantity: 1, source_kind: 'progression',
        granted_at: '2026-08-29T00:00:00Z', expires_at: null,
        wallet: WALLET_A, grant_id: 'private', source_ref: 'private', metadata: { private: true },
    }]), WALLET_A);
    assert.deepEqual(out, [{ sku:'gate_stone', quantity:1, source:'progression',
        granted_at:'2026-08-29T00:00:00Z', expires_at:null }]);
    for (const forbidden of ['wallet', 'grant_id', 'source_ref', 'metadata', 'entitlement_id']) {
        assert.equal(Object.prototype.hasOwnProperty.call(out[0], forbidden), false);
    }
});

test('endpoint refuses missing/failed session and never queries entitlements', async () => {
    const sql = sqlWith([]);
    const endpoint = loadEndpoint(sql, { ok:false, code:'AUTH_SESSION_UNKNOWN' });
    const { out, res } = response();
    await endpoint({ method:'GET', headers:{}, query:{ playerId:WALLET_A } }, res);
    assert.equal(out.status, 401);
    assert.equal(out.body.code, 'AUTH_SESSION_UNKNOWN');
    assert.equal(sql.calls.length, 0, 'read must not run after auth refusal');
});

test('authenticated caller receives only their server-active rows with restore time anchor', async () => {
    const sql = sqlWith([{ sku:'healing_caravan', quantity:1, source_kind:'progression',
        granted_at:'2026-08-29T00:00:00Z', expires_at:null }]);
    const endpoint = loadEndpoint(sql, { ok:true, wallet:WALLET_A });
    const { out, res } = response();
    await endpoint({ method:'GET', headers:{ 'x-session':'opaque' }, query:{ playerId:WALLET_A } }, res);
    assert.equal(out.status, 200);
    assert.equal(out.body.success, true);
    assert.equal(out.body.entitlements[0].sku, 'healing_caravan');
    assert.equal(Number.isFinite(out.body.serverNowMs), true);
    assert.match(out.headers['cache-control'], /no-store/);
});

test('session verification binds the opaque token to the requested wallet', async () => {
    const calls = [];
    const sql = sqlWith([]);
    const endpoint = loadEndpoint(sql, async (seenSql, token, wallet) => {
        calls.push({ seenSql, token, wallet });
        return { ok:true, wallet };
    });
    const { res } = response();
    await endpoint({ method:'GET', headers:{ 'x-session':'opaque-session' }, query:{ playerId:WALLET_A } }, res);
    assert.equal(calls.length, 1);
    assert.equal(calls[0].seenSql, sql);
    assert.equal(calls[0].token, 'opaque-session');
    assert.equal(calls[0].wallet, WALLET_A);
});

test('source contains no grant, mutation, metadata, wallet response, or purchase authority', () => {
    const endpoint = fs.readFileSync(path.join(__dirname, '..', 'api', 'entitlements.js'), 'utf8');
    const lib = fs.readFileSync(path.join(__dirname, '..', 'api', '_lib', 'sku-entitlement-read.js'), 'utf8');
    const code = endpoint + '\n' + lib;
    assert.doesNotMatch(code, /\b(INSERT INTO|UPDATE\s+sku_entitlements|DELETE FROM)\b/);
    assert.doesNotMatch(code, /purchase_entitlements/);
    assert.doesNotMatch(lib, /SELECT[^;]*(grant_id|source_ref|metadata|wallet\s*,)/s);
});

test('player isolation is explicit: another wallet is a distinct bound query value', async () => {
    const a = sqlWith([]), b = sqlWith([]);
    await readActiveEntitlements(a, WALLET_A);
    await readActiveEntitlements(b, WALLET_B);
    assert.notEqual(a.calls[0].values[0], b.calls[0].values[0]);
});
