'use strict';

// Returns the server-derived pseudonymous account binding used by Google Play's
// setObfuscatedAccountId. The HMAC key never leaves the server; the binding is
// useful only for correlating this authenticated game account to its purchase.
// Default-off with the rest of the Play rail.
const { neon } = require('@neondatabase/serverless');
const { verifySession, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const play = require('../_lib/google-play-purchases');

function bindingConfiguration(env) {
    const e = env || {};
    if (String(e.GOOGLE_PLAY_BILLING_ENABLED || '').toLowerCase() !== 'true')
        return { ok: false, code: 'play_billing_disabled' };
    if (!String(e.GOOGLE_PLAY_ACCOUNT_BINDING_KEY || '').trim())
        return { ok: false, code: 'play_account_binding_missing' };
    return { ok: true };
}

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;
    const ref = newRef();
    if (req.method !== 'POST') return quietFail(res, 400, 'METHOD_NOT_ALLOWED', ref);
    const configured = bindingConfiguration(process.env);
    if (!configured.ok) return quietFail(res, 503, 'PLAY_BILLING_UNAVAILABLE', ref);
    let rawBody, body;
    try { rawBody = (await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer;
        body = JSON.parse(rawBody.toString('utf8')); }
    catch (_) { return quietFail(res, 400, 'BAD_PAYLOAD', ref); }
    const playerId = String(body.playerId || '').trim();
    if (!playerId) return quietFail(res, 400, 'BAD_PAYLOAD', ref);
    let sql;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    let auth;
    try { auth = await verifySession(sql, String(req.headers['x-session'] || ''), playerId); }
    catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    if (!auth.ok) return quietFail(res, 401, auth.code || 'AUTH_REQUIRED', ref);
    return res.status(200).json({ success: true,
        accountBinding: play.accountBinding(playerId, process.env.GOOGLE_PLAY_ACCOUNT_BINDING_KEY) });
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { bindingConfiguration };
