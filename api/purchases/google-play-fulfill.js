'use strict';

// Client acknowledgement that the verified basket was durably applied. Only
// after this transition does the backend consume/acknowledge with Google.
const { neon } = require('@neondatabase/serverless');
const { verifySession, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logApiEvent } = require('../_lib/audit');
const play = require('../_lib/google-play-purchases');

async function fulfillPurchase(sql, input, configured, deps) {
    const claimed = await play.claimGrantAcknowledgement(sql, input);
    if (!claimed.ok) return claimed;
    if (claimed.row.state === play.PurchaseState.CONSUMED ||
        claimed.row.state === play.PurchaseState.ACKNOWLEDGED)
        return { ok: true, state: claimed.row.state };
    const token = await (deps.serviceAccountAccessToken || play.serviceAccountAccessToken)(
        configured.credential, deps);
    const finalState = await (deps.finalizeGrantedPurchase || play.finalizeGrantedPurchase)(
        input, token, claimed.row.product_type, deps);
    if (!await play.markFinalized(sql, input, finalState))
        return { ok: false, state: 'record_failed' };
    return { ok: true, state: finalState };
}

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;
    const ref = newRef();
    if (req.method !== 'POST') return quietFail(res, 400, 'METHOD_NOT_ALLOWED', ref);
    const configured = play.configurationReady(process.env);
    if (!configured.ok) return quietFail(res, 503, 'PLAY_BILLING_UNAVAILABLE', ref);
    let rawBody, body;
    try { rawBody = (await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer;
        body = JSON.parse(rawBody.toString('utf8')); }
    catch (_) { return quietFail(res, 400, 'BAD_PAYLOAD', ref); }
    const input = { playerId: String(body.playerId || '').trim(),
        packageName: String(process.env.GOOGLE_PLAY_PACKAGE_NAME),
        sku: String(body.sku || '').trim(), productId: String(body.productId || '').trim(),
        purchaseToken: String(body.purchaseToken || '').trim() };
    input.productType = play.productTypeForSku(input.sku);
    if (!input.playerId || !play.validRequest(input)) return quietFail(res, 400, 'BAD_PAYLOAD', ref);
    let sql;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    let auth;
    try { auth = await verifySession(sql, String(req.headers['x-session'] || ''), input.playerId); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    if (!auth.ok) return quietFail(res, 401, auth.code || 'AUTH_REQUIRED', ref);
    let result;
    try { result = await fulfillPurchase(sql, input, configured, {}); }
    catch (_) {
        await logApiEvent(sql, input.playerId, 'google_play_finalization_pending',
            { ref, sku: input.sku });
        return quietFail(res, 503, 'PLAY_FINALIZATION_PENDING', ref);
    }
    if (!result.ok) return quietFail(res, 409, 'PLAY_ENTITLEMENT_NOT_VERIFIED', ref);
    await logApiEvent(sql, input.playerId, 'google_play_entitlement_fulfilled',
        { ref, sku: input.sku, state: result.state });
    return res.status(200).json({ success: true, state: result.state, sku: input.sku });
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { fulfillPurchase };
