'use strict';

// WO-1255 Lane C.  This module contains the Google Play proof boundary, but it
// does not activate that boundary.  The routed handler must additionally pass
// configurationReady(); an absent flag/credential/account binding therefore
// fails closed before a purchase can be recorded or granted.
const crypto = require('crypto');

const PLAY_SCOPE = 'https://www.googleapis.com/auth/androidpublisher';
const TOKEN_URL = 'https://oauth2.googleapis.com/token';
const API_ROOT = 'https://androidpublisher.googleapis.com/androidpublisher/v3';
const PRODUCT_PREFIX = 'com.denellestudios.echoesofelarion.';
const TOKEN_RE = /^[A-Za-z0-9._~+/=-]{20,4096}$/;
const SKU_RE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const PACKAGE_RE = /^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+$/;

// Explicit by design. A product that includes a permanent entitlement cannot be
// silently treated as a repeatable consumable merely because it also contains
// currency. These classifications must match Play Console before activation.
const PRODUCT_TYPES = Object.freeze({
    'hearth-spark': 'consumable', 'keepers-satchel': 'consumable',
    'folks-thanks': 'consumable', 'patron-of-elarion': 'consumable',
    'founders-vow': 'consumable', 'starters-hand': 'consumable',
    'impulse-wood-small': 'consumable', 'impulse-wood-medium': 'consumable',
    'impulse-wood-large': 'consumable', 'impulse-iron-small': 'consumable',
    'impulse-iron-medium': 'consumable', 'impulse-iron-large': 'consumable',
    'impulse-stone-small': 'consumable', 'impulse-stone-medium': 'consumable',
    'impulse-stone-large': 'consumable', 'impulse-crystals-small': 'consumable',
    'impulse-crystals-medium': 'consumable', 'impulse-crystals-large': 'consumable',
    'frostfall-bundle': 'non_consumable', 'embergrove-bundle': 'non_consumable',
    'bloomtide-bundle': 'non_consumable', 'echo-patron-pack': 'non_consumable',
    'hero-wardrobe-pack': 'non_consumable', 'realm-defender-bundle': 'non_consumable',
    'builders-cache': 'non_consumable', 'permanent-builder': 'non_consumable',
});

const PurchaseState = Object.freeze({
    CREATED: 'created', PENDING: 'pending', PURCHASED: 'purchased',
    VERIFIED: 'verified', GRANTED: 'granted', CONSUMED: 'consumed',
    ACKNOWLEDGED: 'acknowledged', CANCELLED: 'cancelled',
    VOIDED: 'voided', REFUNDED: 'refunded',
});

const ALLOWED_TRANSITIONS = Object.freeze({
    created: new Set(['pending', 'purchased', 'cancelled']),
    pending: new Set(['pending', 'purchased', 'cancelled']),
    purchased: new Set(['verified', 'cancelled', 'voided', 'refunded']),
    verified: new Set(['granted', 'voided', 'refunded']),
    granted: new Set(['consumed', 'acknowledged', 'voided', 'refunded']),
    consumed: new Set(['voided', 'refunded']),
    acknowledged: new Set(['voided', 'refunded']),
    cancelled: new Set(), voided: new Set(), refunded: new Set(),
});

function productIdForSku(sku) {
    const value = String(sku || '').trim();
    return SKU_RE.test(value) ? PRODUCT_PREFIX + value.replace(/-/g, '_') : null;
}

function productTypeForSku(sku) {
    return PRODUCT_TYPES[String(sku || '')] || null;
}

function validRequest(input) {
    return !!input && PACKAGE_RE.test(String(input.packageName || '')) &&
        !!productTypeForSku(input.sku) && TOKEN_RE.test(String(input.purchaseToken || '')) &&
        String(input.productId || '') === productIdForSku(input.sku);
}

function configurationReady(env) {
    const e = env || {};
    if (String(e.GOOGLE_PLAY_BILLING_ENABLED || '').toLowerCase() !== 'true')
        return { ok: false, code: 'play_billing_disabled' };
    if (!PACKAGE_RE.test(String(e.GOOGLE_PLAY_PACKAGE_NAME || '')))
        return { ok: false, code: 'play_package_missing' };
    if (!String(e.GOOGLE_PLAY_ACCOUNT_BINDING_KEY || '').trim())
        return { ok: false, code: 'play_account_binding_missing' };
    let credential;
    try { credential = JSON.parse(String(e.GOOGLE_PLAY_SERVICE_ACCOUNT_JSON || '')); }
    catch (_) { return { ok: false, code: 'play_credential_missing' }; }
    if (!credential || credential.type !== 'service_account' ||
        !String(credential.client_email || '').includes('@') ||
        !String(credential.private_key || '').includes('BEGIN PRIVATE KEY'))
        return { ok: false, code: 'play_credential_invalid' };
    return { ok: true, credential };
}

function base64url(value) {
    return Buffer.from(value).toString('base64url');
}

async function serviceAccountAccessToken(credential, options) {
    const fetchFn = (options && options.fetchFn) || fetch;
    const now = Math.floor(((options && options.nowMs) || Date.now()) / 1000);
    const header = base64url(JSON.stringify({ alg: 'RS256', typ: 'JWT', kid: credential.private_key_id }));
    const claims = base64url(JSON.stringify({ iss: credential.client_email, scope: PLAY_SCOPE,
        aud: TOKEN_URL, iat: now, exp: now + 3600 }));
    const unsigned = header + '.' + claims;
    const signature = crypto.sign('RSA-SHA256', Buffer.from(unsigned), credential.private_key)
        .toString('base64url');
    const body = new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer',
        assertion: unsigned + '.' + signature });
    const response = await fetchFn(TOKEN_URL, { method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: body.toString() });
    if (!response.ok) throw Object.assign(new Error('google_oauth_rejected'), { code: 'play_api_unavailable' });
    const payload = await response.json();
    if (!payload || !payload.access_token) throw Object.assign(new Error('google_oauth_no_token'), { code: 'play_api_unavailable' });
    return String(payload.access_token);
}

async function fetchProductPurchase(input, accessToken, options) {
    if (!validRequest(input)) throw Object.assign(new Error('invalid_play_purchase_request'), { code: 'bad_payload' });
    const fetchFn = (options && options.fetchFn) || fetch;
    const url = API_ROOT + '/applications/' + encodeURIComponent(input.packageName) +
        '/purchases/products/' + encodeURIComponent(input.productId) + '/tokens/' +
        encodeURIComponent(input.purchaseToken);
    const response = await fetchFn(url, { headers: { Authorization: 'Bearer ' + accessToken,
        Accept: 'application/json' } });
    if (response.status === 404) return { state: PurchaseState.CANCELLED, reason: 'token_not_found' };
    if (!response.ok) throw Object.assign(new Error('google_play_api_rejected'), { code: 'play_api_unavailable' });
    return classifyProductPurchase(await response.json());
}

// Call only after the durable ledger has atomically moved VERIFIED -> GRANTED.
// A consumable is consumed; a durable product is acknowledged. Both satisfy
// Google's acknowledgement deadline, but they are intentionally not interchangeable.
async function finalizeGrantedPurchase(input, accessToken, productType, options) {
    if (!validRequest(input)) throw Object.assign(new Error('invalid_play_purchase_request'), { code: 'bad_payload' });
    const action = finalizationAction(productType);
    if (!action) throw Object.assign(new Error('invalid_play_product_type'), { code: 'bad_payload' });
    const fetchFn = (options && options.fetchFn) || fetch;
    const base = API_ROOT + '/applications/' + encodeURIComponent(input.packageName) +
        '/purchases/products/' + encodeURIComponent(input.productId) + '/tokens/' +
        encodeURIComponent(input.purchaseToken);
    const url = base + ':' + action;
    const response = await fetchFn(url, { method: 'POST', headers: {
        Authorization: 'Bearer ' + accessToken, Accept: 'application/json',
        'Content-Type': 'application/json' }, body: action === 'acknowledge' ? '{}' : undefined });
    if (!response.ok) throw Object.assign(new Error('google_play_finalize_rejected'),
        { code: 'play_api_unavailable', status: response.status });
    return action === 'consume' ? PurchaseState.CONSUMED : PurchaseState.ACKNOWLEDGED;
}

function classifyProductPurchase(purchase) {
    if (!purchase || typeof purchase.purchaseState === 'undefined')
        return { state: PurchaseState.CANCELLED, reason: 'malformed_google_proof' };
    const n = Number(purchase.purchaseState);
    if (n === 2) return { state: PurchaseState.PENDING };
    if (n !== 0) return { state: PurchaseState.CANCELLED };
    return { state: PurchaseState.PURCHASED,
        acknowledgementState: Number(purchase.acknowledgementState),
        consumptionState: Number(purchase.consumptionState),
        obfuscatedExternalAccountId: String(purchase.obfuscatedExternalAccountId || ''),
        orderId: purchase.orderId ? String(purchase.orderId) : null,
        purchaseTimeMillis: Number(purchase.purchaseTimeMillis) || null };
}

function accountBinding(playerId, key) {
    return crypto.createHmac('sha256', String(key)).update(String(playerId)).digest('hex');
}

function proofDecision(proof, expectedBinding) {
    if (!proof || proof.state !== PurchaseState.PURCHASED)
        return { grant: false, state: proof ? proof.state : PurchaseState.CANCELLED };
    const observed = Buffer.from(String(proof.obfuscatedExternalAccountId || ''));
    const expected = Buffer.from(String(expectedBinding || ''));
    if (!expected.length || observed.length !== expected.length ||
        !crypto.timingSafeEqual(observed, expected))
        return { grant: false, state: PurchaseState.CANCELLED, reason: 'account_binding_mismatch' };
    if (proof.consumptionState === 1)
        return { grant: false, state: PurchaseState.CANCELLED, reason: 'already_consumed' };
    return { grant: true, state: PurchaseState.VERIFIED };
}

function canTransition(from, to) {
    const allowed = ALLOWED_TRANSITIONS[String(from || '')];
    return !!allowed && allowed.has(String(to || ''));
}

function finalizationAction(productType) {
    if (productType === 'consumable') return 'consume';
    if (productType === 'non_consumable' || productType === 'subscription') return 'acknowledge';
    return null;
}

function safeProofSnapshot(proof) {
    return { acknowledgementState: Number(proof && proof.acknowledgementState) || 0,
        consumptionState: Number(proof && proof.consumptionState) || 0,
        purchaseTimeMillis: Number(proof && proof.purchaseTimeMillis) || null };
}

function persistedRowMatches(row, input) {
    return !!row && String(row.player_id) === String(input.playerId) &&
        String(row.package_name) === String(input.packageName) &&
        String(row.product_id) === String(input.productId) &&
        String(row.sku) === String(input.sku) &&
        String(row.product_type) === String(input.productType);
}

/**
 * Atomically claims a globally unique Play token for its one player/product.
 * The CTE returns either our insert/update or the conflict owner from the same
 * database snapshot. Callers grant only when persistedRowMatches is true and
 * state is verified. The purchase token is never returned to the client/log.
 */
async function persistVerifiedProof(sql, input, proof, expectedBinding) {
    const decision = proofDecision(proof, expectedBinding);
    if (!decision.grant) return { ok: false, state: decision.state, reason: decision.reason || null };
    const snapshot = JSON.stringify(safeProofSnapshot(proof));
    const purchaseTime = proof.purchaseTimeMillis ? new Date(proof.purchaseTimeMillis).toISOString() : null;
    const rows = await sql`
        WITH claimed AS (
            INSERT INTO google_play_purchases
                (purchase_token, player_id, package_name, product_id, sku, product_type,
                 state, obfuscated_account_id, google_order_id, purchase_time,
                 verified_at, last_google_state)
            VALUES (${input.purchaseToken}, ${input.playerId}, ${input.packageName},
                    ${input.productId}, ${input.sku}, ${input.productType}, 'verified',
                    ${expectedBinding}, ${proof.orderId}, ${purchaseTime}, NOW(),
                    ${snapshot}::jsonb)
            ON CONFLICT (purchase_token) DO UPDATE
               SET state = 'verified', verified_at = COALESCE(google_play_purchases.verified_at, NOW()),
                   last_google_state = EXCLUDED.last_google_state, updated_at = NOW()
             WHERE google_play_purchases.player_id = EXCLUDED.player_id
               AND google_play_purchases.package_name = EXCLUDED.package_name
               AND google_play_purchases.product_id = EXCLUDED.product_id
               AND google_play_purchases.sku = EXCLUDED.sku
               AND google_play_purchases.product_type = EXCLUDED.product_type
               AND google_play_purchases.state IN ('created','pending','purchased','verified')
            RETURNING player_id, package_name, product_id, sku, product_type, state
        )
        SELECT player_id, package_name, product_id, sku, product_type, state FROM claimed
        UNION ALL
        SELECT player_id, package_name, product_id, sku, product_type, state
          FROM google_play_purchases
         WHERE purchase_token = ${input.purchaseToken}
           AND NOT EXISTS (SELECT 1 FROM claimed)
        LIMIT 1`;
    const row = rows && rows[0];
    if (!persistedRowMatches(row, input)) return { ok: false, state: 'conflict', reason: 'token_reused' };
    if (row.state !== PurchaseState.VERIFIED && row.state !== PurchaseState.GRANTED &&
        row.state !== PurchaseState.CONSUMED && row.state !== PurchaseState.ACKNOWLEDGED)
        return { ok: false, state: row.state, reason: 'not_grantable' };
    return { ok: true, state: row.state, sku: input.sku };
}

async function persistPendingProof(sql, input, expectedBinding) {
    const rows = await sql`
        INSERT INTO google_play_purchases
            (purchase_token, player_id, package_name, product_id, sku, product_type,
             state, obfuscated_account_id)
        VALUES (${input.purchaseToken}, ${input.playerId}, ${input.packageName},
                ${input.productId}, ${input.sku}, ${input.productType}, 'pending', ${expectedBinding})
        ON CONFLICT (purchase_token) DO NOTHING
        RETURNING state`;
    return { ok: true, state: rows && rows.length ? PurchaseState.PENDING : PurchaseState.PENDING };
}

async function claimGrantAcknowledgement(sql, input) {
    const rows = await sql`
        WITH moved AS (
            UPDATE google_play_purchases
               SET state = 'granted', granted_at = COALESCE(granted_at, NOW()), updated_at = NOW()
             WHERE purchase_token = ${input.purchaseToken}
               AND player_id = ${input.playerId} AND package_name = ${input.packageName}
               AND product_id = ${input.productId} AND sku = ${input.sku}
               AND state IN ('verified','granted')
            RETURNING player_id, package_name, product_id, sku, product_type, state
        )
        SELECT player_id, package_name, product_id, sku, product_type, state FROM moved
        UNION ALL
        SELECT player_id, package_name, product_id, sku, product_type, state
          FROM google_play_purchases
         WHERE purchase_token = ${input.purchaseToken} AND NOT EXISTS (SELECT 1 FROM moved)
        LIMIT 1`;
    const row = rows && rows[0];
    return persistedRowMatches(row, input) &&
        [PurchaseState.GRANTED, PurchaseState.CONSUMED, PurchaseState.ACKNOWLEDGED].includes(row.state)
        ? { ok: true, row } : { ok: false, state: row ? row.state : 'missing' };
}

async function markFinalized(sql, input, finalState) {
    if (finalState !== PurchaseState.CONSUMED && finalState !== PurchaseState.ACKNOWLEDGED)
        throw new Error('invalid final state');
    const rows = await sql`
        UPDATE google_play_purchases
           SET state = ${finalState}, finalized_at = COALESCE(finalized_at, NOW()), updated_at = NOW()
         WHERE purchase_token = ${input.purchaseToken} AND player_id = ${input.playerId}
           AND sku = ${input.sku} AND state IN ('granted', ${finalState})
        RETURNING state`;
    return !!(rows && rows.length);
}

module.exports = { PLAY_SCOPE, TOKEN_URL, API_ROOT, PRODUCT_PREFIX, PRODUCT_TYPES, PurchaseState,
    ALLOWED_TRANSITIONS, productIdForSku, productTypeForSku, validRequest, configurationReady,
    serviceAccountAccessToken, fetchProductPurchase, finalizeGrantedPurchase, classifyProductPurchase,
    accountBinding, proofDecision, canTransition, finalizationAction, safeProofSnapshot,
    persistedRowMatches, persistVerifiedProof, persistPendingProof, claimGrantAcknowledgement,
    markFinalized };
