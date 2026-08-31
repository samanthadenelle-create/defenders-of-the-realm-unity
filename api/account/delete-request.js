'use strict';

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticate, isGuestId, isPlayId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject, logApiEvent } = require('../_lib/audit');
const { normalizeDeletionRequest, createDeletionRequest } = require('../_lib/account-deletion');

const MAX_BODY_BYTES = 16 * 1024;

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;
    const ref = newRef();

    if (req.method !== 'POST') return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);

    let rawBody;
    let exact;
    try {
        const read = await readBodyExact(req, MAX_BODY_BYTES);
        rawBody = read.buffer;
        exact = read.exact;
    } catch (err) {
        const code = err && err.code === 'BODY_TOO_LARGE' ? AuthCode.PAYLOAD_TOO_LARGE : AuthCode.BAD_PAYLOAD;
        return quietFail(res, 400, code, ref);
    }

    let parsed;
    try {
        parsed = JSON.parse(rawBody.toString('utf8'));
    } catch (_) {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    const normalized = normalizeDeletionRequest(parsed);
    if (!normalized.ok) return quietFail(res, 400, normalized.code, ref);

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[account-delete-request] DB init failed:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    const hasSession = !!(req.headers && req.headers['x-session']);
    if (!exact && !isGuestId(normalized.playerId) && !isPlayId(normalized.playerId) && !hasSession) {
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR,
            ref,
            identity: normalized.playerId,
            mode: 'wallet',
            detail: { reason: 'raw_body_unavailable_bodyparser_active' },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    let auth;
    try {
        auth = await authenticate(sql, req, rawBody, normalized.playerId);
    } catch (err) {
        console.error('[account-delete-request] Auth failed:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
    if (!auth.ok) {
        await logAuthReject(sql, req, {
            code: auth.code,
            ref,
            identity: auth.identity,
            mode: auth.mode,
            detail: auth.detail,
        });
        const badArgument = auth.code === AuthCode.PLAYER_ID_MISSING || auth.code === AuthCode.PLAYER_ID_BAD_SHAPE;
        return quietFail(res, badArgument ? 400 : 401, auth.code, ref);
    }

    try {
        const row = await createDeletionRequest(sql, normalized, auth.mode);
        await logApiEvent(sql, auth.identity, 'account_deletion_requested', {
            requestId: row.request_id,
            scope: normalized.scope,
            categoryCount: normalized.categories.length,
        });
        return res.status(200).json({
            ok: true,
            requestId: row.request_id,
            status: row.status,
            requestedAt: row.requested_at,
        });
    } catch (err) {
        console.error('[account-delete-request] Insert failed:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
