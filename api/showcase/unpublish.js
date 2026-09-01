'use strict';

const { neon } = require('@neondatabase/serverless');
const { verifySession, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const authenticate = deps.verifySession || verifySession;
    return async function handler(req, res) {
        if (applyCors(req, res, 'POST, OPTIONS')) return;
        const ref = newRef();
        if (req.method !== 'POST') return quietFail(res, 400, 'METHOD_NOT_ALLOWED', ref);
        let body;
        try {
            const raw = (await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer;
            body = JSON.parse(raw.toString('utf8'));
        } catch (_) { return quietFail(res, 400, 'BAD_PAYLOAD', ref); }
        if (!body || Object.keys(body).some(k => k !== 'playerId') ||
            typeof body.playerId !== 'string' || !body.playerId.trim())
            return quietFail(res, 400, 'BAD_PAYLOAD', ref);
        const playerId = body.playerId.trim();
        let sql;
        try { sql = getSql(); }
        catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
        let auth;
        try { auth = await authenticate(sql, String(req.headers['x-session'] || ''), playerId); }
        catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
        if (!auth.ok) return quietFail(res, 401, auth.code || 'AUTH_REQUIRED', ref);
        try {
            await sql`UPDATE public_town_showcases
                      SET published = FALSE, published_at = NULL, updated_at = NOW()
                      WHERE owner_wallet = ${playerId}`;
            return res.status(200).json({ success: true, published: false });
        } catch (_) { return quietFail(res, 500, 'SERVER_ERROR', ref); }
    };
}

module.exports = makeHandler();
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { makeHandler };
