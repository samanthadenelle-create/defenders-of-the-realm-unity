'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const play = require('../api/_lib/google-play-purchases');
const rtdn = require('../api/_lib/google-play-rtdn');

const PACKAGE = 'com.denellestudios.echoesofelarion';
const TOKEN = 'R'.repeat(30);

function configured() {
    return { credential: {}, audience: 'https://backend.example/api/purchases/google-play-rtdn',
        serviceAccountEmail: 'play-rtdn@example.iam.gserviceaccount.com', packageName: PACKAGE };
}

function envelope(payload, messageId = 'message-1') {
    return Buffer.from(JSON.stringify({ message: { messageId,
        data: Buffer.from(JSON.stringify(payload)).toString('base64') },
        subscription: 'projects/example/subscriptions/play-rtdn' }));
}

function notification(arm, value) {
    return { version: '1.0', packageName: PACKAGE, eventTimeMillis: '1788134400000',
        [arm]: value };
}

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

test('RTDN is independently default-off and requires audience and exact push identity', () => {
    const base = { GOOGLE_PLAY_BILLING_ENABLED: 'true', GOOGLE_PLAY_PACKAGE_NAME: PACKAGE,
        GOOGLE_PLAY_ACCOUNT_BINDING_KEY: 'key', GOOGLE_PLAY_SERVICE_ACCOUNT_JSON: JSON.stringify({
            type: 'service_account', client_email: 'publisher@example.iam.gserviceaccount.com',
            private_key: '-----BEGIN PRIVATE KEY-----\nnot-real\n-----END PRIVATE KEY-----' }) };
    assert.equal(rtdn.configurationReady(base).code, 'play_rtdn_disabled');
    assert.equal(rtdn.configurationReady({ ...base, GOOGLE_PLAY_RTDN_ENABLED: 'true' }).code,
        'play_rtdn_audience_missing');
    assert.equal(rtdn.configurationReady({ ...base, GOOGLE_PLAY_RTDN_ENABLED: 'true',
        GOOGLE_PLAY_RTDN_AUDIENCE: 'aud' }).code, 'play_rtdn_identity_missing');
});

test('Pub/Sub OIDC verifier pins audience, issuer, verified email and exact service account', async () => {
    let request;
    const goodClient = { verifyIdToken: async input => { request = input; return { getPayload: () => ({
        iss: 'https://accounts.google.com', email_verified: true,
        email: configured().serviceAccountEmail }) }; } };
    assert.equal((await rtdn.verifyPushIdentity('Bearer signed.jwt', configured(),
        { oauthClient: goodClient })).ok, true);
    assert.deepEqual(request, { idToken: 'signed.jwt', audience: configured().audience });
    assert.equal((await rtdn.verifyPushIdentity('', configured(), { oauthClient: goodClient })).ok, false);
    const wrong = { verifyIdToken: async () => ({ getPayload: () => ({
        iss: 'https://accounts.google.com', email_verified: true, email: 'attacker@example.com' }) }) };
    assert.equal((await rtdn.verifyPushIdentity('Bearer signed.jwt', configured(),
        { oauthClient: wrong })).code, 'wrong_identity');
});

test('envelope parsing is strict, package-pinned and requires exactly one notification arm', () => {
    const good = rtdn.decodeEnvelope(envelope(notification('testNotification', { version: '1.0' })), PACKAGE);
    assert.equal(good.ok, true);
    assert.equal(good.messageId, 'message-1');
    assert.equal(rtdn.decodeEnvelope(envelope({ ...notification('testNotification', {}),
        voidedPurchaseNotification: {} }), PACKAGE).code, 'ambiguous_notification');
    assert.equal(rtdn.decodeEnvelope(envelope({ ...notification('testNotification', {}),
        packageName: 'com.attacker.app' }), PACKAGE).code, 'wrong_notification');
    assert.equal(rtdn.decodeEnvelope(Buffer.from('{"message":{"messageId":"x","data":"%%%"}}'),
        PACKAGE).ok, false);
});

test('one-time RTDN re-queries Google and only verifies an existing product/account-bound token', async () => {
    const productId = play.productIdForSku('builders-cache');
    const decoded = rtdn.decodeEnvelope(envelope(notification('oneTimeProductNotification', {
        version: '1.0', notificationType: 1, purchaseToken: TOKEN, sku: productId })), PACKAGE);
    const row = { player_id: 'play-user', package_name: PACKAGE, product_id: productId,
        sku: 'builders-cache', product_type: 'non_consumable', state: 'pending',
        obfuscated_account_id: 'bound-account' };
    const sql = fakeSql([[{ message_id: 'message-1' }], [row], [], []]);
    const result = await rtdn.processNotification(sql, decoded, configured(), {
        serviceAccountAccessToken: async () => 'publisher-token',
        fetchProductPurchaseV2: async (_packageName, _token, access) => {
            assert.equal(access, 'publisher-token');
            return { state: 'purchased', productId, acknowledgementState: 0, consumptionState: 0,
                obfuscatedExternalAccountId: 'bound-account' };
        } });
    assert.equal(result.status, 'processed');
    assert.match(sql.calls[2].text, /THEN 'verified'/);
    assert.doesNotMatch(sql.calls.map(call => call.text).join('\n'), /state = 'granted'/);
});

test('current ProductPurchaseV2 lookup is token-only and classifies its named state safely', async () => {
    const productId = play.productIdForSku('builders-cache');
    let request;
    const proof = await play.fetchProductPurchaseV2(PACKAGE, TOKEN, 'publisher-secret', {
        fetchFn: async (url, options) => { request = { url, options }; return { ok: true, status: 200,
            json: async () => ({ purchaseStateContext: { purchaseState: 'PURCHASED' },
                productLineItem: [{ productId, productOfferDetails: { quantity: 1,
                    refundableQuantity: 1,
                    consumptionState: 'CONSUMPTION_STATE_YET_TO_BE_CONSUMED' } }],
                acknowledgementState: 'ACKNOWLEDGEMENT_STATE_PENDING',
                obfuscatedExternalAccountId: 'binding', purchaseCompletionTime: '2026-08-30T00:00:00Z' }) }; } });
    assert.equal(proof.state, 'purchased');
    assert.equal(proof.productId, productId);
    assert.match(request.url, /\/purchases\/productsv2\/tokens\//);
    assert.doesNotMatch(request.url, /publisher-secret/);
    assert.equal(request.options.headers.Authorization, 'Bearer publisher-secret');
});

test('unknown purchase token is re-queried but durably quarantined and never granted', async () => {
    const decoded = rtdn.decodeEnvelope(envelope(notification('oneTimeProductNotification', {
        version: '1.0', notificationType: 1, purchaseToken: TOKEN,
        sku: play.productIdForSku('builders-cache') })), PACKAGE);
    const sql = fakeSql([[{ message_id: 'message-1' }], [], []]);
    let fetched = false;
    const result = await rtdn.processNotification(sql, decoded, configured(), {
        serviceAccountAccessToken: async () => 'access',
        fetchProductPurchaseV2: async () => { fetched = true; return { state: 'purchased',
            productId: play.productIdForSku('builders-cache') }; } });
    assert.equal(result.status, 'quarantined');
    assert.equal(result.reason, 'unclaimed_or_mismatched_token');
    assert.equal(fetched, true);
    assert.match(sql.calls[2].text, /quarantine_reason/);
});

test('full void marks lifecycle terminal but stays quarantined until real entitlement reversal', async () => {
    const decoded = rtdn.decodeEnvelope(envelope(notification('voidedPurchaseNotification', {
        purchaseToken: TOKEN, orderId: 'GPA.1', productType: 2, refundType: 1 })), PACKAGE);
    const sql = fakeSql([[{ message_id: 'message-1' }], [{ purchase_token: TOKEN }], []]);
    const result = await rtdn.processNotification(sql, decoded, configured());
    assert.equal(result.status, 'quarantined');
    assert.equal(result.reason, 'full_void_requires_entitlement_reversal');
    assert.match(sql.calls[1].text, /SET state = 'voided'/);
});

test('pending refund review retains the operator token and remains quarantined for 24h action', async () => {
    const decoded = rtdn.decodeEnvelope(envelope(notification('pendingRefundReviewNotification', {
        version: '1.0', pendingRefundToken: 'refund-review-token', orderId: 'GPA.2',
        refundReason: 7 })), PACKAGE);
    const sql = fakeSql([[{ message_id: 'message-1' }], []]);
    const result = await rtdn.processNotification(sql, decoded, configured());
    assert.equal(result.reason, 'refund_review_requires_24h_operator_action');
    assert.match(sql.calls[1].text, /pending_refund_token/);
    assert.ok(sql.calls[1].values.includes('refund-review-token'));
});

test('duplicate message id is acknowledged without reprocessing and transient failure becomes retryable', async () => {
    const decoded = rtdn.decodeEnvelope(envelope(notification('testNotification', { version: '1.0' })), PACKAGE);
    const duplicateSql = fakeSql([[]]);
    assert.deepEqual(await rtdn.processNotification(duplicateSql, decoded, configured()),
        { ok: true, duplicate: true });
    assert.equal(duplicateSql.calls.length, 1);

    const productId = play.productIdForSku('builders-cache');
    const purchase = rtdn.decodeEnvelope(envelope(notification('oneTimeProductNotification', {
        version: '1.0', notificationType: 1, purchaseToken: TOKEN, sku: productId }), 'message-2'), PACKAGE);
    const row = { player_id: 'p', product_id: productId, sku: 'builders-cache',
        product_type: 'non_consumable', obfuscated_account_id: 'binding' };
    const retrySql = fakeSql([[{ message_id: 'message-2' }], [row], []]);
    await assert.rejects(() => rtdn.processNotification(retrySql, purchase, configured(), {
        serviceAccountAccessToken: async () => { throw new Error('temporary outage'); } }),
    /temporary outage/);
    assert.match(retrySql.calls[1].text, /status = 'retry'/);
});

test('migration provides durable message-id dedupe and explicit attention queue', () => {
    const migration = fs.readFileSync(path.join(__dirname, '..', 'api', 'migrations',
        '20260830_0015_google_play_rtdn.sql'), 'utf8');
    assert.match(migration, /message_id\s+TEXT PRIMARY KEY/);
    assert.match(migration, /'processing','processed','quarantined','retry'/);
    assert.match(migration, /WHERE status IN \('quarantined','retry'\)/);
    assert.doesNotMatch(migration, /entitlement.*(?:delete|revoke)/i);
});
