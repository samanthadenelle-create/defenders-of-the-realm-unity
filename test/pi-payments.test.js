'use strict';

// =============================================================================
// WO-1318 — the Pi rail. These cases ARE the acceptance criteria.
// -----------------------------------------------------------------------------
// ⛔ THE ONE THAT MATTERS MOST: a forged amount is REFUSED (AC 2). On Pi the
// amount travels through `Pi.createPayment({ amount })`, which runs in the
// player's browser, so the number Pi shows us is client-supplied by construction.
// The only thing between that and a 0.1 Pi purchase of a $4.99 pack is
// validatePaymentAgainstQuote() refusing before /approve is ever called.
//
// The rest pin the other three money-path invariants: fail-closed pricing (AC 3),
// single-use quotes / no double grant, and the API key never appearing in
// anything a client or a log can see (AC 5).
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const pi = require('../api/_lib/pi-payments');
const catalog = require('../api/_lib/purchase-catalog');

const UID = 'a1b2c3d4e5f6';
const PLAYER = 'pi-' + UID;
const QUOTE_ID = 'b'.repeat(32);
const RATE = { usdPerPi: 0.091171, source: pi.RATE_SOURCE };

/** A persisted purchase_quotes row for the Pi rail, as the endpoints read it back. */
function quoteRow(over = {}) {
    return Object.assign({
        quote_ref: QUOTE_ID, wallet: PLAYER, sku: 'hearth-spark',
        network: 'pi', currency: 'PI',
        amount_base_units: '547400000', decimals: 7,
        usd_anchor: '4.9900', usd_rate: '0.091171000000', rate_source: pi.RATE_SOURCE,
        expires_at: new Date(Date.now() + 300_000).toISOString(),
        consumed_at: null, consumed_tx: null,
    }, over);
}

/** A payment object as Pi's GET /v2/payments/:id reports it. */
function payment(over = {}) {
    return Object.assign({
        identifier: 'pay_0001',
        user_uid: UID,
        amount: 54.74,
        memo: pi.PI_MEMO,
        to_address: 'GA6PIAPPWALLETADDRESSEXAMPLE0000000000000000000000000000',
        metadata: { sku: 'hearth-spark', quoteId: QUOTE_ID, uid: UID },
        status: { developer_approved: false, transaction_verified: false,
            developer_completed: false, cancelled: false, user_cancelled: false },
        transaction: null,
    }, over);
}

// ── The price: the owner's rate ruling, mirrored from SKR ────────────────────

test('the Pi rate source is the 24h LOW on the same CoinGecko endpoint as SKR', () => {
    assert.equal(pi.RATE_SOURCE, 'coingecko:pi-network:low_24h');
    assert.match(catalog.RATE_SOURCE, /low_24h$/);
    const src = fs.readFileSync(path.join(__dirname, '..', 'api', '_lib', 'pi-payments.js'), 'utf8');
    assert.match(src, /coins\/markets\?vs_currency=usd&ids=pi-network/);
    assert.match(src, /rows\[0\] && rows\[0\]\.low_24h/);
});

test('the Pi amount is derived server-side from the USD anchor and rounds UP', () => {
    const built = pi.buildPiQuoteBody('hearth-spark', RATE);
    assert.equal(built.usdAnchor, 4.99);            // packs.json pricing.usd
    // 4.99 / 0.091171 = 54.7327... -> never LESS than spot, at 0.01 Pi precision.
    assert.equal(built.amount, 54.74);
    assert.ok(built.amount >= 4.99 / RATE.usdPerPi);
    assert.equal(built.amountBaseUnits, '547400000');
    assert.equal(built.decimals, 7);
    assert.equal(built.rateSource, pi.RATE_SOURCE);
});

test('every Pi SKU carries a USD anchor in the SHARED catalog (no second price table)', () => {
    for (const sku of pi.PI_SKUS) assert.equal(typeof catalog.usdAnchor(sku), 'number');
    assert.deepEqual(pi.PI_SKUS, ['hearth-spark']);
});

test('the memo is ASCII and identical on both sides', () => {
    assert.equal(pi.PI_MEMO, 'Echoes of Elarion - Hearth Spark');
    // eslint-disable-next-line no-control-regex
    assert.match(pi.PI_MEMO, /^[\x20-\x7E]+$/);
});

// ── AC 3: FAIL CLOSED. No rate, no quote, no invented price ─────────────────

test('no rate means NO quote — never a stale or catalog price', () => {
    assert.equal(pi.buildPiQuoteBody('hearth-spark', null), null);
    assert.equal(pi.buildPiQuoteBody('hearth-spark', { usdPerPi: 0, source: 'x' }), null);
    assert.equal(pi.buildPiQuoteBody('hearth-spark', { usdPerPi: NaN, source: 'x' }), null);
    assert.equal(pi.quotePiAmount(4.99, 0), null);
    assert.equal(pi.quotePiAmount(4.99, -1), null);
});

test('an unreachable oracle resolves to null, not to a guess', async () => {
    const realFetch = global.fetch;
    pi._resetRateCache();
    try {
        global.fetch = async () => { throw new Error('network down'); };
        assert.equal(await pi.fetchPiUsdRate(Date.now()), null);
        global.fetch = async () => ({ ok: true, json: async () => [{ low_24h: 0 }] });
        pi._resetRateCache();
        assert.equal(await pi.fetchPiUsdRate(Date.now()), null);
        global.fetch = async () => ({ ok: false, status: 503, json: async () => ({}) });
        pi._resetRateCache();
        assert.equal(await pi.fetchPiUsdRate(Date.now()), null);
    } finally { global.fetch = realFetch; pi._resetRateCache(); }
});

test('an unsold SKU is refused rather than priced', () => {
    assert.equal(pi.piSkuUsd('folks-thanks'), null);       // real pack, not on the Pi rail
    assert.equal(pi.piSkuUsd('not-a-pack'), null);
    assert.equal(pi.buildPiQuoteBody('folks-thanks', RATE), null);
});

// ── Amounts are compared as INTEGERS, never as floats ───────────────────────

test('amounts convert to base units exactly, whatever shape Pi sends them in', () => {
    assert.equal(pi.amountToBaseUnits(54.74), '547400000');
    assert.equal(pi.amountToBaseUnits('54.74'), '547400000');
    assert.equal(pi.amountToBaseUnits('54.7400000'), '547400000');
    assert.equal(pi.amountToBaseUnits('0.1'), '1000000');
    assert.equal(pi.amountToBaseUnits('nope'), null);
    assert.equal(pi.amountToBaseUnits(''), null);
    assert.equal(pi.amountToBaseUnits(null), null);
    // More precision than Pi itself carries is refused, never rounded into a price.
    assert.equal(pi.amountToBaseUnits('54.74000001'), null);
});

// ══ AC 2 — THE FORGED AMOUNT IS REFUSED. This is the proof. ═════════════════

test('a client that forges a LOWER amount is refused and never approved', () => {
    const result = pi.validatePaymentAgainstQuote(
        payment({ amount: 0.1 }), quoteRow(), QUOTE_ID, PLAYER);
    assert.equal(result.ok, false);
    assert.equal(result.code, 'PI_AMOUNT_MISMATCH');
});

test('a HIGHER forged amount is refused too — the server owns the number, not a floor', () => {
    const result = pi.validatePaymentAgainstQuote(
        payment({ amount: 5474 }), quoteRow(), QUOTE_ID, PLAYER);
    assert.equal(result.ok, false);
    assert.equal(result.code, 'PI_AMOUNT_MISMATCH');
});

test('one base unit off is still off', () => {
    const result = pi.validatePaymentAgainstQuote(
        payment({ amount: '54.7399999' }), quoteRow(), QUOTE_ID, PLAYER);
    assert.equal(result.ok, false);
    assert.equal(result.code, 'PI_AMOUNT_MISMATCH');
});

test('the exact quoted amount passes, and the payee is captured for the ledger', () => {
    const result = pi.validatePaymentAgainstQuote(payment(), quoteRow(), QUOTE_ID, PLAYER);
    assert.equal(result.ok, true);
    assert.equal(result.toAddress, payment().to_address);
});

test('a payment carrying a DIFFERENT quote id cannot spend this quote', () => {
    const bad = payment({ metadata: { sku: 'hearth-spark', quoteId: 'c'.repeat(32), uid: UID } });
    assert.equal(pi.validatePaymentAgainstQuote(bad, quoteRow(), QUOTE_ID, PLAYER).code,
        'PI_QUOTE_NOT_YOURS');
});

test('a payment for a DIFFERENT sku cannot spend this quote', () => {
    const bad = payment({ metadata: { sku: 'founders-vow', quoteId: QUOTE_ID, uid: UID } });
    assert.equal(pi.validatePaymentAgainstQuote(bad, quoteRow(), QUOTE_ID, PLAYER).code,
        'PI_QUOTE_NOT_YOURS');
});

test("Pi's own user_uid is the identity authority — another Pioneer cannot spend this quote", () => {
    // ⭐ This is what makes an unauthenticated quote request harmless: forging a
    // uid at quote time mints a ticket only its rightful owner can ever redeem.
    const bad = payment({ user_uid: 'someoneelse01' });
    assert.equal(pi.validatePaymentAgainstQuote(bad, quoteRow(), QUOTE_ID, PLAYER).code,
        'PI_QUOTE_NOT_YOURS');
});

test('a mismatched memo is refused', () => {
    const bad = payment({ memo: 'Free stuff' });
    assert.equal(pi.validatePaymentAgainstQuote(bad, quoteRow(), QUOTE_ID, PLAYER).code,
        'PI_MEMO_MISMATCH');
});

test('a missing payment object is refused, not treated as empty agreement', () => {
    assert.equal(pi.validatePaymentAgainstQuote(null, quoteRow(), QUOTE_ID, PLAYER).code,
        'PI_PAYMENT_UNKNOWN');
});

// ── The quote row itself: single-use, bound, ours ───────────────────────────

test('an unknown quote is refused', () => {
    assert.equal(pi.evaluatePiQuoteRow(null, PLAYER, 'hearth-spark', 'pay_0001').code,
        'PI_QUOTE_UNKNOWN');
});

test("another player's quote is refused", () => {
    assert.equal(pi.evaluatePiQuoteRow(quoteRow({ wallet: 'pi-otherpioneer' }), PLAYER,
        'hearth-spark', 'pay_0001').code, 'PI_QUOTE_NOT_YOURS');
});

test('a SOLANA quote can never be spent on the Pi rail', () => {
    const skr = quoteRow({ network: 'devnet', currency: 'SKR' });
    assert.equal(pi.evaluatePiQuoteRow(skr, PLAYER, 'hearth-spark', 'pay_0001').code,
        'PI_QUOTE_NOT_YOURS');
});

test('a quote already spent by ANOTHER payment is refused (single-use)', () => {
    const spent = quoteRow({ consumed_tx: 'pay_9999', consumed_at: new Date().toISOString() });
    assert.equal(pi.evaluatePiQuoteRow(spent, PLAYER, 'hearth-spark', 'pay_0001').code,
        'PI_QUOTE_ALREADY_USED');
});

test('the SAME payment re-presenting its quote is an idempotent retry, not a replay', () => {
    // This is what lets onIncompletePaymentFound finish a payment that already
    // consumed its quote on an earlier, interrupted attempt.
    const spent = quoteRow({ consumed_tx: 'pay_0001', consumed_at: new Date().toISOString() });
    assert.equal(pi.evaluatePiQuoteRow(spent, PLAYER, 'hearth-spark', 'pay_0001').ok, true);
});

// ── Identity shape ─────────────────────────────────────────────────────────

test('a Pi subject is prefixed so it can never pass as a wallet or a play- id', () => {
    assert.equal(pi.piPlayerId(UID), PLAYER);
    assert.equal(pi.piUidOf(PLAYER), UID);
    assert.equal(pi.piPlayerId(''), null);
    assert.equal(pi.piPlayerId('short'), null);
    assert.equal(pi.piPlayerId('bad uid with spaces'), null);
});

// ══ AC 5 — the API key is server-only ══════════════════════════════════════

test('PI_NETWORK_API_KEY is read from env only, and never logged or returned', () => {
    const files = ['api/_lib/pi-payments.js', 'api/pi/quote.js', 'api/pi/approve.js',
        'api/pi/complete.js'];
    for (const rel of files) {
        const src = fs.readFileSync(path.join(__dirname, '..', rel), 'utf8');
        // The key is referenced in exactly one way: off process.env.
        const mentions = (src.match(/PI_NETWORK_API_KEY/g) || []).length;
        const envReads = (src.match(/process\.env\.PI_NETWORK_API_KEY/g) || []).length;
        assert.equal(mentions - envReads, rel.endsWith('pi-payments.js') ? 1 : 0,
            rel + ' mentions the key outside a comment/env read');
        // Nothing ever prints it.
        assert.equal(/console\.[a-z]+\([^)]*piApiKey/.test(src), false, rel);
        assert.equal(/piApiKey\(\)[^;]*res\./.test(src), false, rel);
    }
    // It is not exported either — only `configured()` is.
    assert.equal('piApiKey' in pi, false);
});

test('no literal Pi key is committed anywhere in the Pi rail', () => {
    for (const rel of ['api/_lib/pi-payments.js', 'api/pi/quote.js', 'api/pi/approve.js',
        'api/pi/complete.js', 'api/pi/verify.js']) {
        const src = fs.readFileSync(path.join(__dirname, '..', rel), 'utf8');
        assert.equal(/Key\s+[A-Za-z0-9]{20,}/.test(src), false, rel + ' looks like it embeds a key');
    }
});

test('the rail is DORMANT until the key is configured', () => {
    const old = process.env.PI_NETWORK_API_KEY;
    try {
        delete process.env.PI_NETWORK_API_KEY;
        assert.equal(pi.configured(), false);
        process.env.PI_NETWORK_API_KEY = 'test-key';
        assert.equal(pi.configured(), true);
    } finally {
        if (old === undefined) delete process.env.PI_NETWORK_API_KEY;
        else process.env.PI_NETWORK_API_KEY = old;
    }
});

// ── Refusals are worded (quiet is not mute, on the money path) ──────────────

test('every refusal code the Pi endpoints can answer has player-readable wording', () => {
    const codes = new Set();
    for (const rel of ['api/pi/quote.js', 'api/pi/approve.js', 'api/pi/complete.js']) {
        const src = fs.readFileSync(path.join(__dirname, '..', rel), 'utf8');
        for (const m of src.matchAll(/refuse\(res,\s*\d+,\s*'([A-Z_]+)'/g)) codes.add(m[1]);
    }
    assert.ok(codes.size >= 6);
    for (const code of codes)
        assert.equal(typeof pi.PI_MESSAGES[code], 'string', code + ' has no worded message');
});

test('the "you paid and we did not grant" wording tells the player NOT to pay again', () => {
    assert.match(pi.PI_MESSAGES.PI_MANUAL_REVIEW, /do NOT pay again/i);
    assert.match(pi.PI_MESSAGES.PI_RECORD_FAILED, /do not pay again/i);
    // And the pre-payment refusals say plainly that nothing was charged.
    for (const code of ['PI_AMOUNT_MISMATCH', 'PURCHASE_RATE_UNAVAILABLE', 'PI_QUOTE_EXPIRED'])
        assert.match(pi.PI_MESSAGES[code], /[Nn]othing has been charged/);
});

// ── One rail, not a second store ───────────────────────────────────────────

test('the Pi rail reuses the shared quote table, TTL and grant ledger', () => {
    assert.equal(pi.QUOTE_TTL_SECONDS, catalog.QUOTE_TTL_SECONDS);
    const complete = fs.readFileSync(
        path.join(__dirname, '..', 'api', 'pi', 'complete.js'), 'utf8');
    assert.match(complete, /INSERT INTO purchase_entitlements/);
    assert.match(complete, /ON CONFLICT \(tx_signature\) DO NOTHING/);
    const quote = fs.readFileSync(path.join(__dirname, '..', 'api', 'pi', 'quote.js'), 'utf8');
    assert.match(quote, /INSERT INTO purchase_quotes/);
    // No second catalog: the ladder price is never a literal in executable code
    // on this rail. (Comments may quote $4.99 to explain the arithmetic.)
    const strip = (text) => text.replace(/^\s*\/\/.*$/gm, '').replace(/^\s*\*.*$/gm, '');
    for (const rel of ['api/pi/quote.js', 'api/pi/approve.js', 'api/pi/complete.js',
        'api/_lib/pi-payments.js']) {
        const code = strip(fs.readFileSync(path.join(__dirname, '..', rel), 'utf8'));
        assert.equal(/4\.99/.test(code), false, rel + ' hardcodes a ladder price');
    }
});

test('the schema declares everything the Pi rail writes', () => {
    const schema = fs.readFileSync(path.join(__dirname, '..', 'api', 'schema.sql'), 'utf8');
    assert.match(schema, /CREATE TABLE IF NOT EXISTS pi_payments/);
    assert.match(schema, /rail\s+TEXT NOT NULL CHECK \(rail IN \('solana','pi'\)\)/);
    assert.match(schema, /currency\s+TEXT NOT NULL CHECK \(currency IN \('SOL','USDC','SKR','PI'\)\)/);
    assert.match(schema, /currency\s+TEXT NOT NULL CHECK \(currency IN \('SKR','PI'\)\)/);
    const migration = fs.readFileSync(path.join(__dirname, '..', 'api', 'migrations',
        '20260902_0017_pi_payments.sql'), 'utf8');
    assert.match(migration, /CREATE TABLE IF NOT EXISTS pi_payments/);
    assert.match(migration, /ALTER TABLE purchase_quotes ALTER COLUMN mint\s+DROP NOT NULL/);
});
