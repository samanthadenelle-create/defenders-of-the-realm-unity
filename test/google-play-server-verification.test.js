'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const play = require('../api/_lib/google-play-purchases');
const verifyRoute = require('../api/purchases/google-play-verify');
const fulfillRoute = require('../api/purchases/google-play-fulfill');
const bindingRoute = require('../api/purchases/google-play-binding');

function fakeSql(rows) {
    const calls = [];
    const sql = (strings, ...values) => {
        calls.push({ text: strings.join('?'), values });
        return Promise.resolve(typeof rows === 'function' ? rows(strings.join('?'), values) : rows);
    };
    sql.calls = calls;
    return sql;
}

function inputFor(sku = 'impulse-wood-small') {
    return { playerId: 'player-1', packageName: 'com.denellestudios.echoesofelarion', sku,
        productId: play.productIdForSku(sku), productType: play.productTypeForSku(sku),
        purchaseToken: 'Q'.repeat(30) };
}

test('Google Play verification is disabled by default and requires every secret', () => {
    assert.deepEqual(play.configurationReady({}), { ok: false, code: 'play_billing_disabled' });
    assert.equal(play.configurationReady({ GOOGLE_PLAY_BILLING_ENABLED: 'true' }).code,
        'play_package_missing');
    assert.equal(play.configurationReady({ GOOGLE_PLAY_BILLING_ENABLED: 'true',
        GOOGLE_PLAY_PACKAGE_NAME: 'com.denellestudios.echoesofelarion' }).code,
        'play_account_binding_missing');
});

test('SKU/product mapping is exact and malformed proof requests fail before network', async () => {
    assert.equal(play.productIdForSku('builders-cache'),
        'com.denellestudios.echoesofelarion.builders_cache');
    assert.equal(play.productIdForSku('../bad'), null);
    let fetched = false;
    await assert.rejects(() => play.fetchProductPurchase({ packageName: 'com.good.app',
        sku: 'builders-cache', productId: 'wrong.id', purchaseToken: 'A'.repeat(30) }, 'secret',
    { fetchFn: async () => { fetched = true; } }), /invalid_play_purchase_request/);
    assert.equal(fetched, false);
});

test('product type is explicit and determines consume versus acknowledge', () => {
    assert.equal(play.productTypeForSku('impulse-wood-small'), 'consumable');
    assert.equal(play.productTypeForSku('permanent-builder'), 'non_consumable');
    assert.equal(play.productTypeForSku('hero-wardrobe-pack'), 'non_consumable');
    assert.equal(play.finalizationAction('consumable'), 'consume');
    assert.equal(play.finalizationAction('non_consumable'), 'acknowledge');
    assert.equal(play.finalizationAction('bogus'), null);
    // 27 since WO-1449 added 'builders-hour'. The count is a canary that a SKU was added
    // here without a matching row on the other rails; the mirror suites prove the contents.
    assert.equal(Object.keys(play.PRODUCT_TYPES).length, 27);
});

test('only PURCHASED, unconsumed, account-bound proof becomes VERIFIED', () => {
    const key = 'test-only-key';
    const binding = play.accountBinding('player-1', key);
    const purchased = play.classifyProductPurchase({ purchaseState: 0, consumptionState: 0,
        acknowledgementState: 0, obfuscatedExternalAccountId: binding, orderId: 'ORDER' });
    assert.deepEqual(play.proofDecision(purchased, binding),
        { grant: true, state: play.PurchaseState.VERIFIED });
    assert.equal(play.proofDecision({ ...purchased, consumptionState: 1 }, binding).grant, false);
    assert.equal(play.proofDecision(purchased, play.accountBinding('attacker', key)).grant, false);
    assert.equal(play.proofDecision(play.classifyProductPurchase({ purchaseState: 2 }), binding).state,
        play.PurchaseState.PENDING);
    assert.equal(play.proofDecision(play.classifyProductPurchase({ purchaseState: 1 }), binding).grant,
        false);
});

test('state machine cannot skip verification/grant or resurrect terminal purchases', () => {
    assert.equal(play.canTransition('created', 'purchased'), true);
    assert.equal(play.canTransition('purchased', 'verified'), true);
    assert.equal(play.canTransition('verified', 'granted'), true);
    assert.equal(play.canTransition('granted', 'consumed'), true);
    assert.equal(play.canTransition('purchased', 'granted'), false);
    assert.equal(play.canTransition('verified', 'consumed'), false);
    assert.equal(play.canTransition('refunded', 'granted'), false);
    assert.equal(play.canTransition('voided', 'verified'), false);
});

test('Developer API lookup uses bearer auth and does not put credentials in URL', async () => {
    let seen;
    const proof = await play.fetchProductPurchase({
        packageName: 'com.denellestudios.echoesofelarion', sku: 'builders-cache',
        productId: play.productIdForSku('builders-cache'), purchaseToken: 'T'.repeat(30),
    }, 'access-secret', { fetchFn: async (url, options) => {
        seen = { url, options };
        return { ok: true, status: 200, json: async () => ({ purchaseState: 2 }) };
    } });
    assert.equal(proof.state, play.PurchaseState.PENDING);
    assert.equal(seen.options.headers.Authorization, 'Bearer access-secret');
    assert.doesNotMatch(seen.url, /access-secret/);
    assert.match(seen.url, /builders_cache/);
});

test('finalization consumes consumables and acknowledges durable products only after grant', async () => {
    const input = { packageName: 'com.denellestudios.echoesofelarion', sku: 'impulse-wood-small',
        productId: play.productIdForSku('impulse-wood-small'), purchaseToken: 'P'.repeat(30) };
    const urls = [];
    const fetchFn = async (url, options) => {
        urls.push({ url, options });
        return { ok: true, status: 200 };
    };
    assert.equal(await play.finalizeGrantedPurchase(input, 'bearer', 'consumable', { fetchFn }),
        play.PurchaseState.CONSUMED);
    assert.match(urls[0].url, /:consume$/);
    assert.equal(await play.finalizeGrantedPurchase(input, 'bearer', 'non_consumable', { fetchFn }),
        play.PurchaseState.ACKNOWLEDGED);
    assert.match(urls[1].url, /:acknowledge$/);
    await assert.rejects(() => play.finalizeGrantedPurchase(input, 'bearer', 'unknown', { fetchFn }),
        /invalid_play_product_type/);
});

test('migration globally deduplicates tokens and pins safe states', () => {
    const sql = fs.readFileSync(path.join(__dirname, '..', 'api', 'migrations',
        '20260828_0007_google_play_purchase_state.sql'), 'utf8');
    assert.match(sql, /purchase_token\s+TEXT PRIMARY KEY/);
    assert.match(sql, /CHECK \(state IN[\s\S]*'verified'[\s\S]*'granted'[\s\S]*'consumed'/);
    assert.match(sql, /WHERE state IN \('created','pending','purchased','verified','granted'\)/);
    assert.doesNotMatch(sql, /purchase_entitlements/);
});

test('routed verify core grants only a purchased, bound Google proof', async () => {
    const input = inputFor();
    const binding = play.accountBinding(input.playerId, 'binding-key');
    const sql = fakeSql([{ player_id: input.playerId, package_name: input.packageName,
        product_id: input.productId, sku: input.sku, product_type: input.productType,
        state: 'verified' }]);
    const result = await verifyRoute._test.processPurchase(sql, input,
        { GOOGLE_PLAY_ACCOUNT_BINDING_KEY: 'binding-key' }, {
            credential: {}, serviceAccountAccessToken: async () => 'access',
            fetchProductPurchase: async () => ({ state: 'purchased', consumptionState: 0,
                acknowledgementState: 0, obfuscatedExternalAccountId: binding }) });
    assert.deepEqual(result, { ok: true, state: 'verified', sku: input.sku });
    assert.equal(sql.calls.length, 1);
    assert.match(sql.calls[0].text, /ON CONFLICT \(purchase_token\) DO UPDATE/);
});

test('pending, cancelled, binding mismatch and API failure never create grant authority', async () => {
    const input = inputFor();
    const env = { GOOGLE_PLAY_ACCOUNT_BINDING_KEY: 'binding-key' };
    const binding = play.accountBinding(input.playerId, env.GOOGLE_PLAY_ACCOUNT_BINDING_KEY);
    const pendingSql = fakeSql([{ state: 'pending' }]);
    const pending = await verifyRoute._test.processPurchase(pendingSql, input, env, {
        credential: {}, serviceAccountAccessToken: async () => 'access',
        fetchProductPurchase: async () => ({ state: 'pending' }) });
    assert.equal(pending.state, 'pending');
    assert.doesNotMatch(pendingSql.calls[0].text, /'verified'/);

    for (const proof of [
        { state: 'cancelled' },
        { state: 'purchased', consumptionState: 0, obfuscatedExternalAccountId: '0'.repeat(64) },
        { state: 'purchased', consumptionState: 1, obfuscatedExternalAccountId: binding },
    ]) {
        const sql = fakeSql([]);
        const result = await verifyRoute._test.processPurchase(sql, input, env, {
            credential: {}, serviceAccountAccessToken: async () => 'access',
            fetchProductPurchase: async () => proof });
        assert.equal(result.ok, false);
        assert.equal(sql.calls.some(call => /INSERT INTO|UPDATE google_play_purchases/.test(call.text)),
            false, 'a rejected proof may inspect an existing terminal token but must never grant');
    }
    const failedSql = fakeSql([]);
    await assert.rejects(() => verifyRoute._test.processPurchase(failedSql, input, env, {
        credential: {}, serviceAccountAccessToken: async () => 'access',
        fetchProductPurchase: async () => { throw new Error('API down'); } }), /API down/);
    assert.equal(failedSql.calls.length, 0);
});

test('duplicate token owned by another player or SKU never grants', async () => {
    const input = inputFor();
    const binding = play.accountBinding(input.playerId, 'binding-key');
    const sql = fakeSql([{ player_id: 'other-player', package_name: input.packageName,
        product_id: input.productId, sku: input.sku, product_type: input.productType,
        state: 'verified' }]);
    const result = await play.persistVerifiedProof(sql, input, { state: 'purchased',
        consumptionState: 0, obfuscatedExternalAccountId: binding }, binding);
    assert.deepEqual(result, { ok: false, state: 'conflict', reason: 'token_reused' });
});

test('consumed proof retries only its matching durable terminal ledger owner', async () => {
    const input = inputFor();
    const binding = play.accountBinding(input.playerId, 'binding-key');
    const proof = { state: 'purchased', consumptionState: 1,
        obfuscatedExternalAccountId: binding };
    const terminal = fakeSql([{ player_id: input.playerId, package_name: input.packageName,
        product_id: input.productId, sku: input.sku, product_type: input.productType,
        state: 'consumed' }]);
    assert.deepEqual(await play.persistVerifiedProof(terminal, input, proof, binding),
        { ok: true, state: 'consumed', sku: input.sku });
    const stolen = fakeSql([{ player_id: 'other-player', package_name: input.packageName,
        product_id: input.productId, sku: input.sku, product_type: input.productType,
        state: 'consumed' }]);
    assert.deepEqual(await play.persistVerifiedProof(stolen, input, proof, binding),
        { ok: false, state: 'conflict', reason: 'already_consumed' });
});

test('fulfill transitions verified to granted before server finalization and is retryable', async () => {
    const input = inputFor();
    let call = 0;
    const sql = fakeSql(() => ++call === 1
        ? [{ player_id: input.playerId, package_name: input.packageName, product_id: input.productId,
            sku: input.sku, product_type: input.productType, state: 'granted' }]
        : [{ state: 'consumed' }]);
    const result = await fulfillRoute._test.fulfillPurchase(sql, input, { credential: {} }, {
        serviceAccountAccessToken: async () => 'access',
        finalizeGrantedPurchase: async () => play.PurchaseState.CONSUMED });
    assert.deepEqual(result, { ok: true, state: 'consumed' });
    assert.match(sql.calls[0].text, /state = 'granted'/);
    assert.match(sql.calls[1].text, /state =/);
});

test('fulfill retry after terminal state does not call Google finalization twice', async () => {
    const input = inputFor();
    const sql = fakeSql([{ player_id: input.playerId, package_name: input.packageName,
        product_id: input.productId, sku: input.sku, product_type: input.productType,
        state: 'consumed' }]);
    let finalized = false;
    const result = await fulfillRoute._test.fulfillPurchase(sql, input, { credential: {} }, {
        serviceAccountAccessToken: async () => { throw new Error('must not mint token'); },
        finalizeGrantedPurchase: async () => { finalized = true; } });
    assert.deepEqual(result, { ok: true, state: 'consumed' });
    assert.equal(finalized, false);
    assert.equal(sql.calls.length, 1);
});

test('production route checks disabled flag before reading or calling Google', async () => {
    const old = process.env.GOOGLE_PLAY_BILLING_ENABLED;
    delete process.env.GOOGLE_PLAY_BILLING_ENABLED;
    const req = { method: 'POST', headers: {} };
    const out = {};
    const res = { setHeader() {}, status(n) { out.status = n; return this; },
        json(body) { out.body = body; return this; } };
    try { await require('../api/purchases/google-play-verify')(req, res); }
    finally { if (old == null) delete process.env.GOOGLE_PLAY_BILLING_ENABLED;
        else process.env.GOOGLE_PLAY_BILLING_ENABLED = old; }
    assert.equal(out.status, 503);
    assert.equal(out.body.code, 'PLAY_BILLING_UNAVAILABLE');
});

test('account binding endpoint is independently default-off and never exposes its key', () => {
    assert.equal(bindingRoute._test.bindingConfiguration({}).ok, false);
    assert.equal(bindingRoute._test.bindingConfiguration({ GOOGLE_PLAY_BILLING_ENABLED: 'true' }).ok,
        false);
    const configured = bindingRoute._test.bindingConfiguration({ GOOGLE_PLAY_BILLING_ENABLED: 'true',
        GOOGLE_PLAY_ACCOUNT_BINDING_KEY: 'server-only-key' });
    assert.deepEqual(configured, { ok: true });
    const source = fs.readFileSync(path.join(__dirname, '..', 'api', 'purchases',
        'google-play-binding.js'), 'utf8');
    assert.match(source, /verifySession/);
    assert.match(source, /accountBinding: play\.accountBinding/);
    assert.doesNotMatch(source, /(bindingKey|accountBindingKey|secret)\s*:/i);
});
