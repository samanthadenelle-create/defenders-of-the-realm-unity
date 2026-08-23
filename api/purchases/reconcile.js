'use strict';

// MON-1147 restore path: an authenticated wallet can recover its durable verified
// entitlement after local data loss. This endpoint never creates an entitlement.
const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticateGranting, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject } = require('../_lib/audit');
const { walletAllowed } = require('../_lib/purchase-catalog');

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;
    const ref = newRef();
    if (req.method !== 'POST') return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);

    let rawBody, body;
    try {
        rawBody = (await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer;
        body = JSON.parse(rawBody.toString('utf8'));
    } catch (_) { return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref); }

    const playerId = String(body.playerId || '').trim();
    const sku = String(body.sku || '').trim();
    const network = String(body.network || 'devnet').trim().toLowerCase();
    if (!playerId || !sku || (network !== 'devnet' && network !== 'mainnet-beta'))
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    if (!walletAllowed(network, sku, playerId))
        return quietFail(res, 403, AuthCode.BAD_PAYLOAD, ref);

    let sql;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { return quietFail(res, 500, AuthCode.SERVER_ERROR, ref); }

    let auth;
    try { auth = await authenticateGranting(sql, req, rawBody, playerId); }
    catch (_) { return quietFail(res, 500, AuthCode.SERVER_ERROR, ref); }
    if (!auth.ok) {
        await logAuthReject(sql, req, { code: auth.code, ref, identity: auth.identity,
            mode: auth.mode, detail: auth.detail });
        return quietFail(res, 401, auth.code, ref);
    }

    const rows = await sql`
        SELECT entitlement_id, tx_signature, sku, network, currency, expected_lamports, status
        FROM purchase_entitlements
        WHERE wallet = ${playerId} AND sku = ${sku} AND network = ${network}
          AND status IN ('verified','fulfilled')
        ORDER BY verified_at DESC LIMIT 1`;
    if (!rows.length) return res.status(200).json({ success: true, state: 'none', sku });
    const row = rows[0];
    return res.status(200).json({ success: true, state: row.status, sku: row.sku,
        txSignature: row.tx_signature, network: row.network, currency: row.currency,
        amountLamports: Number(row.expected_lamports), entitlementId: String(row.entitlement_id) });
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
