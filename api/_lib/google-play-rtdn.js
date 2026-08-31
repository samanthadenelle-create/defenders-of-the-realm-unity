'use strict';

// Secure Google Cloud Pub/Sub boundary for Google Play RTDN. Notifications are
// change hints, never purchase proof: callers must re-query Android Publisher
// before moving purchase state and must never grant an entitlement from this
// payload alone.
const { OAuth2Client } = require('google-auth-library');
const play = require('./google-play-purchases');

const MAX_BODY_BYTES = 64 * 1024;
const MESSAGE_ID_RE = /^[A-Za-z0-9._:-]{1,256}$/;
const EMAIL_RE = /^[^\s@]+@[^\s@]+$/;
const NOTIFICATION_KEYS = Object.freeze([
    'oneTimeProductNotification', 'subscriptionNotification',
    'voidedPurchaseNotification', 'pendingRefundReviewNotification', 'testNotification',
]);

function configurationReady(env) {
    const billing = play.configurationReady(env);
    if (!billing.ok) return billing;
    const e = env || {};
    if (String(e.GOOGLE_PLAY_RTDN_ENABLED || '').toLowerCase() !== 'true')
        return { ok: false, code: 'play_rtdn_disabled' };
    if (!String(e.GOOGLE_PLAY_RTDN_AUDIENCE || '').trim())
        return { ok: false, code: 'play_rtdn_audience_missing' };
    if (!EMAIL_RE.test(String(e.GOOGLE_PLAY_RTDN_SERVICE_ACCOUNT_EMAIL || '')))
        return { ok: false, code: 'play_rtdn_identity_missing' };
    return { ok: true, credential: billing.credential,
        audience: String(e.GOOGLE_PLAY_RTDN_AUDIENCE),
        serviceAccountEmail: String(e.GOOGLE_PLAY_RTDN_SERVICE_ACCOUNT_EMAIL).toLowerCase(),
        packageName: String(e.GOOGLE_PLAY_PACKAGE_NAME) };
}

async function verifyPushIdentity(authorization, configured, options) {
    const match = /^Bearer\s+([^\s]+)$/i.exec(String(authorization || ''));
    if (!match) return { ok: false, code: 'missing_bearer' };
    try {
        const client = (options && options.oauthClient) || new OAuth2Client();
        const ticket = await client.verifyIdToken({ idToken: match[1], audience: configured.audience });
        const claims = ticket.getPayload() || {};
        const issuer = String(claims.iss || '');
        if (issuer !== 'accounts.google.com' && issuer !== 'https://accounts.google.com')
            return { ok: false, code: 'wrong_issuer' };
        if (claims.email_verified !== true ||
            String(claims.email || '').toLowerCase() !== configured.serviceAccountEmail)
            return { ok: false, code: 'wrong_identity' };
        return { ok: true, claims };
    } catch (_) { return { ok: false, code: 'invalid_token' }; }
}

function decodeEnvelope(rawBody, expectedPackage) {
    let envelope, notification;
    try {
        envelope = JSON.parse(Buffer.from(rawBody).toString('utf8'));
        if (!envelope || !envelope.message ||
            !MESSAGE_ID_RE.test(String(envelope.message.messageId || '')) ||
            typeof envelope.message.data !== 'string') throw new Error('bad envelope');
        const decoded = Buffer.from(envelope.message.data, 'base64');
        if (!decoded.length || decoded.length > MAX_BODY_BYTES) throw new Error('bad data');
        // Reject non-canonical/truncated base64 rather than accepting Buffer's permissive decoder.
        const canonical = envelope.message.data.replace(/=+$/, '');
        if (decoded.toString('base64').replace(/=+$/, '') !== canonical) throw new Error('bad base64');
        notification = JSON.parse(decoded.toString('utf8'));
    } catch (_) { return { ok: false, code: 'bad_envelope' }; }
    const eventMillis = Number(notification && notification.eventTimeMillis);
    if (!notification || String(notification.version || '') !== '1.0' ||
        String(notification.packageName || '') !== String(expectedPackage || '') ||
        !/^\d{1,20}$/.test(String(notification.eventTimeMillis || '')) ||
        !Number.isSafeInteger(eventMillis) || eventMillis < 0 ||
        !Number.isFinite(new Date(eventMillis).getTime()))
        return { ok: false, code: 'wrong_notification' };
    const present = NOTIFICATION_KEYS.filter(key => notification[key] != null);
    if (present.length !== 1) return { ok: false, code: 'ambiguous_notification' };
    return { ok: true, messageId: String(envelope.message.messageId),
        subscription: String(envelope.subscription || ''), kind: present[0], notification };
}

function internalSkuForProductId(productId) {
    for (const sku of Object.keys(play.PRODUCT_TYPES))
        if (play.productIdForSku(sku) === productId) return sku;
    return null;
}

async function claimMessage(sql, decoded) {
    const rows = await sql`
        INSERT INTO google_play_rtdn_messages
            (message_id, package_name, notification_kind, event_time, status)
        VALUES (${decoded.messageId}, ${decoded.notification.packageName}, ${decoded.kind},
                ${new Date(Number(decoded.notification.eventTimeMillis)).toISOString()}, 'processing')
        ON CONFLICT (message_id) DO UPDATE
           SET status = 'processing', attempts = google_play_rtdn_messages.attempts + 1,
               updated_at = NOW()
         WHERE google_play_rtdn_messages.status = 'retry'
        RETURNING message_id`;
    return !!(rows && rows.length);
}

async function finishMessage(sql, decoded, status, reason, purchaseToken, orderId, pendingRefundToken) {
    await sql`
        UPDATE google_play_rtdn_messages
           SET status = ${status}, quarantine_reason = ${reason || null},
               purchase_token = ${purchaseToken || null}, google_order_id = ${orderId || null},
               pending_refund_token = ${pendingRefundToken || null},
               processed_at = NOW(), updated_at = NOW()
         WHERE message_id = ${decoded.messageId} AND status = 'processing'`;
}

async function processOneTime(sql, decoded, configured, deps) {
    const n = decoded.notification.oneTimeProductNotification;
    const token = String(n && n.purchaseToken || '');
    const productId = String(n && n.sku || '');
    const sku = internalSkuForProductId(productId);
    if (!n || ![1, 2].includes(Number(n.notificationType)) ||
        !play.TOKEN_RE.test(token) || !sku)
        return { status: 'quarantined', reason: 'invalid_one_time_product', purchaseToken: token };
    const accessToken = await deps.serviceAccountAccessToken(configured.credential, deps);
    const proof = await deps.fetchProductPurchaseV2(configured.packageName, token, accessToken, deps);
    if (proof.productId && proof.productId !== productId)
        return { status: 'quarantined', reason: 'google_product_mismatch', purchaseToken: token };
    const existing = await sql`
        SELECT player_id, package_name, product_id, sku, product_type, state,
               obfuscated_account_id
          FROM google_play_purchases
         WHERE purchase_token = ${token} AND package_name = ${configured.packageName} LIMIT 1`;
    const row = existing && existing[0];
    if (!row || String(row.product_id) !== productId || String(row.sku) !== sku)
        return { status: 'quarantined', reason: 'unclaimed_or_mismatched_token', purchaseToken: token };
    const snapshot = JSON.stringify(play.safeProofSnapshot(proof));
    if (proof.state === play.PurchaseState.PURCHASED &&
        String(proof.obfuscatedExternalAccountId || '') === String(row.obfuscated_account_id || '')) {
        await sql`UPDATE google_play_purchases SET state = CASE
                    WHEN state IN ('created','pending','purchased') THEN 'verified' ELSE state END,
                    verified_at = CASE WHEN state IN ('created','pending','purchased')
                        THEN COALESCE(verified_at, NOW()) ELSE verified_at END,
                    last_google_state = ${snapshot}::jsonb, updated_at = NOW()
                  WHERE purchase_token = ${token} AND package_name = ${configured.packageName}`;
        return { status: 'processed', purchaseToken: token };
    }
    if ([play.PurchaseState.PENDING, play.PurchaseState.CANCELLED].includes(proof.state)) {
        await sql`UPDATE google_play_purchases SET state = CASE
                    WHEN ${proof.state} = 'cancelled' AND state IN ('created','pending','purchased')
                        THEN 'cancelled' ELSE state END,
                    last_google_state = ${snapshot}::jsonb, updated_at = NOW()
                  WHERE purchase_token = ${token} AND package_name = ${configured.packageName}`;
        return { status: 'processed', purchaseToken: token };
    }
    return { status: 'quarantined', reason: 'google_proof_binding_rejected', purchaseToken: token };
}

async function processNotification(sql, decoded, configured, options) {
    if (!await claimMessage(sql, decoded)) return { ok: true, duplicate: true };
    const deps = Object.assign({ serviceAccountAccessToken: play.serviceAccountAccessToken,
        fetchProductPurchaseV2: play.fetchProductPurchaseV2 }, options || {});
    try {
        let result;
        if (decoded.kind === 'testNotification') result = { status: 'processed' };
        else if (decoded.kind === 'oneTimeProductNotification')
            result = await processOneTime(sql, decoded, configured, deps);
        else if (decoded.kind === 'voidedPurchaseNotification') {
            const n = decoded.notification.voidedPurchaseNotification || {};
            const token = String(n.purchaseToken || '');
            if (!play.TOKEN_RE.test(token) || Number(n.productType) !== 2 || ![1, 2].includes(Number(n.refundType)))
                result = { status: 'quarantined', reason: 'invalid_voided_purchase', purchaseToken: token };
            else if (Number(n.refundType) === 2)
                result = { status: 'quarantined', reason: 'partial_refund_requires_reversal',
                    purchaseToken: token, orderId: String(n.orderId || '') };
            else {
                const rows = await sql`UPDATE google_play_purchases
                    SET state = 'voided', updated_at = NOW()
                    WHERE purchase_token = ${token} AND package_name = ${configured.packageName}
                      AND state NOT IN ('cancelled','voided','refunded') RETURNING purchase_token`;
                result = rows && rows.length
                    ? { status: 'quarantined', reason: 'full_void_requires_entitlement_reversal',
                        purchaseToken: token, orderId: String(n.orderId || '') }
                    : { status: 'quarantined', reason: 'voided_token_not_found_or_terminal',
                        purchaseToken: token, orderId: String(n.orderId || '') };
            }
        } else if (decoded.kind === 'pendingRefundReviewNotification') {
            const n = decoded.notification.pendingRefundReviewNotification || {};
            result = { status: 'quarantined', reason: 'refund_review_requires_24h_operator_action',
                orderId: String(n.orderId || ''), pendingRefundToken: String(n.pendingRefundToken || '') };
        } else result = { status: 'quarantined', reason: 'unsupported_notification' };
        await finishMessage(sql, decoded, result.status, result.reason,
            result.purchaseToken, result.orderId, result.pendingRefundToken);
        return { ok: true, status: result.status, reason: result.reason || null };
    } catch (error) {
        await sql`UPDATE google_play_rtdn_messages SET status = 'retry', updated_at = NOW()
                   WHERE message_id = ${decoded.messageId} AND status = 'processing'`;
        throw error;
    }
}

module.exports = { MAX_BODY_BYTES, NOTIFICATION_KEYS, configurationReady, verifyPushIdentity,
    decodeEnvelope, internalSkuForProductId, claimMessage, finishMessage, processOneTime,
    processNotification };
