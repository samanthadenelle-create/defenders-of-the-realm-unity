'use strict';

// WO-1255 Lane C: Google Play proof endpoint. Dormant unless the explicit flag,
// package, account-binding key and service account are all present. There is no
// fallback to Solana verification and no client receipt is trusted as proof.
const { neon } = require('@neondatabase/serverless');
const { verifySession, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logApiEvent } = require('../_lib/audit');
const play = require('../_lib/google-play-purchases');

async function processPurchase(sql, input, env, deps) {
    const accessToken = await (deps.serviceAccountAccessToken || play.serviceAccountAccessToken)(
        deps.credential, deps);
    const proof = await (deps.fetchProductPurchase || play.fetchProductPurchase)(input, accessToken, deps);
    const binding = play.accountBinding(input.playerId, env.GOOGLE_PLAY_ACCOUNT_BINDING_KEY);
    if (proof.state === play.PurchaseState.PENDING)
        return play.persistPendingProof(sql, input, binding);
    return play.persistVerifiedProof(sql, input, proof, binding);
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
    // Session verification is the current server identity seam. Play activation
    // remains blocked until the client can obtain this session without wallet UI.
    let auth;
    try { auth = await verifySession(sql, String(req.headers['x-session'] || ''), input.playerId); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    if (!auth.ok) return quietFail(res, 401, auth.code || 'AUTH_REQUIRED', ref);

    let result;
    try { result = await processPurchase(sql, input, process.env,
        { credential: configured.credential }); }
    catch (_) {
        await logApiEvent(sql, input.playerId, 'google_play_verification_unavailable',
            { ref, sku: input.sku });
        return quietFail(res, 503, 'PLAY_VERIFICATION_UNAVAILABLE', ref);
    }
    if (!result.ok) {
        await logApiEvent(sql, input.playerId, 'google_play_verification_rejected',
            { ref, sku: input.sku, state: result.state, reason: result.reason });
        return res.status(result.state === play.PurchaseState.PENDING ? 202 : 409)
            .json({ success: false, state: result.state, sku: input.sku, ref });
    }
    if (result.state === play.PurchaseState.PENDING)
        return res.status(202).json({ success: true, state: 'pending', sku: input.sku });
    await logApiEvent(sql, input.playerId, 'google_play_entitlement_verified',
        { ref, sku: input.sku, productType: input.productType });
    return res.status(200).json({ success: true, state: result.state, sku: input.sku });
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { processPurchase };
