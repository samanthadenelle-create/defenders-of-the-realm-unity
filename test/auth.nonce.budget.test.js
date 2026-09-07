'use strict';

// =============================================================================
// WO-1456 — /api/auth/nonce had NO rate limit of any kind.
// -----------------------------------------------------------------------------
// Every call mints a row. Unauthenticated-cheap for the caller, a write for Neon.
// The promo rail already owns the only non-forgeable budget in this project
// (WO-1440, `promo_ip_budget`), so the fix is to REUSE that helper — a second
// limiter would be duplicated state, and duplicated state in this repo is what
// produced the stale WO-number block and the retired dependency table.
//
// The helper therefore had to move OUT of api/promo/redeem.js and into
// api/_lib/ip-budget.js, with the promo route calling the extracted copy. This
// file proves the helper behaves, that BOTH callers use the one implementation,
// and — memory `prove-the-success-path-not-just-the-refusal` — that an ordinary
// single call still gets its nonce.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

const budget = require('../api/_lib/ip-budget.js');

/**
 * A tagged-template stand-in for the Neon driver. `grants` is what the UPSERT
 * would have RETURNED, i.e. the count including this very call.
 */
function fakeSql(grants) {
    const calls = [];
    const fn = (strings, ...values) => {
        calls.push({ text: strings.join('?'), values });
        if (grants instanceof Error) return Promise.reject(grants);
        return Promise.resolve([{ grants: grants, total_grants: grants }]);
    };
    fn.calls = calls;
    return fn;
}

// ── 1. The extracted helper ──────────────────────────────────────────────────

test('the shared helper lives in _lib and is not a second implementation', () => {
    assert.equal(typeof budget.reserveIpBudget, 'function');
    const redeem = read('api/promo/redeem.js');
    assert.match(redeem, /require\(['"]\.\.\/_lib\/ip-budget['"]\)/,
        'promo/redeem.js still carries its own copy of the budget UPSERT');
    const executable = redeem.replace(/^\s*\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
    assert.doesNotMatch(executable, /INSERT INTO promo_ip_budget/,
        'the UPSERT is still inlined in the promo route — two limiters is duplicated state');
});

test('a normal single call is ALLOWED and reserves exactly one unit', async () => {
    const sql = fakeSql(1);
    const r = await budget.reserveIpBudget(sql, 'ab12cd34ef56', 'AUTH_NONCE',
        { windowSeconds: 3600, maxPerWindow: 60 });
    assert.equal(r.ok, true, 'the ordinary first call was refused — the limiter refuses everyone');
    assert.equal(r.grants, 1);
    assert.equal(sql.calls.length, 1, 'the reservation must be ONE atomic statement, not a read then a write');
    assert.match(sql.calls[0].text, /INSERT INTO promo_ip_budget/);
    assert.match(sql.calls[0].text, /ON CONFLICT \(ip_hash, code\) DO UPDATE/);
});

test('the call that lands ON the limit is still allowed; the one past it is refused', async () => {
    const at = await budget.reserveIpBudget(fakeSql(60), 'ab12cd34ef56', 'AUTH_NONCE',
        { windowSeconds: 3600, maxPerWindow: 60 });
    assert.equal(at.ok, true, 'the budget is off by one — the last permitted call was refused');

    const over = await budget.reserveIpBudget(fakeSql(61), 'ab12cd34ef56', 'AUTH_NONCE',
        { windowSeconds: 3600, maxPerWindow: 60 });
    assert.equal(over.ok, false, 'a caller past its budget was still served');
    assert.equal(over.error, 'RATE_LIMITED', 'the refusal code must be the promo rail\'s, not a new one');
});

test('failClosed decides what an UNREADABLE budget table means', async () => {
    // The promo rail guards a payout: "we could not check" must resolve to "do not
    // pay". The nonce rail guards a challenge that grants nothing, so an unreadable
    // table must not take the wallet login offline. One helper, one explicit switch.
    const closed = await budget.reserveIpBudget(fakeSql(new Error('relation does not exist')),
        'ab12cd34ef56', 'FIRSTWATCH', { windowSeconds: 60, maxPerWindow: 20, failClosed: true });
    assert.equal(closed.ok, false);
    assert.equal(closed.degraded, true);

    const open = await budget.reserveIpBudget(fakeSql(new Error('relation does not exist')),
        'ab12cd34ef56', 'AUTH_NONCE', { windowSeconds: 60, maxPerWindow: 60, failClosed: false });
    assert.equal(open.ok, true, 'a missing budget table took the whole wallet rail offline');
    assert.equal(open.degraded, true);
});

test('an unattributable caller (no IP) is refused only on the fail-closed rail', async () => {
    const closed = await budget.reserveIpBudget(fakeSql(1), '', 'FIRSTWATCH',
        { windowSeconds: 60, maxPerWindow: 20, failClosed: true });
    assert.equal(closed.ok, false, 'a caller who suppresses its IP got an unlimited payout budget');

    const open = await budget.reserveIpBudget(fakeSql(1), '', 'AUTH_NONCE',
        { windowSeconds: 60, maxPerWindow: 60, failClosed: false });
    assert.equal(open.ok, true);
});

// ── 2. The nonce route actually uses it ──────────────────────────────────────

test('api/auth/nonce.js budgets per caller IP through the shared helper', () => {
    const nonce = read('api/auth/nonce.js');
    assert.match(nonce, /require\(['"]\.\.\/_lib\/ip-budget['"]\)/,
        'the nonce route does not import the shared budget helper');
    assert.match(nonce, /hashIp\(req\)/,
        'the budget is not keyed on the one signal a client cannot choose');
    assert.match(nonce, /reserveIpBudget\(/);
    assert.match(nonce, /RATE_LIMITED/,
        'the nonce route never refuses — it imports the helper and ignores the answer');
    // The budget must be spent only AFTER the free shape check, so a malformed
    // wallet cannot burn a household's budget (the same placement rule WO-1440
    // wrote into the promo route).
    const shapeGate = nonce.indexOf('isWalletId(wallet)');
    const reserve = nonce.indexOf('reserveIpBudget(');
    assert.ok(shapeGate > 0 && reserve > shapeGate,
        'the budget is spent before the free malformed-wallet check');
});
