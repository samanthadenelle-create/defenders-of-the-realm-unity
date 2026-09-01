'use strict';

const { neon } = require('@neondatabase/serverless');
const { applyCors } = require('./_lib/http');
const { verifySession } = require('./_lib/wallet-auth');
const { EntitlementReadError, readActiveEntitlements, validatePlayerId } = require('./_lib/sku-entitlement-read');

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;
    res.setHeader('Cache-Control', 'private, no-store');
    if (req.method !== 'GET') {
        return res.status(400).json({ success: false, code: 'METHOD_NOT_ALLOWED' });
    }

    let playerId;
    try {
        playerId = validatePlayerId(req.query && req.query.playerId);
    } catch (err) {
        return res.status(400).json({ success: false, code: err.code || 'PLAYER_ID_BAD_SHAPE' });
    }

    try {
        const sql = neon(process.env.DATABASE_URL);
        const auth = await verifySession(sql, String(req.headers['x-session'] || ''), playerId);
        if (!auth.ok) return res.status(401).json({ success: false, code: auth.code });

        const entitlements = await readActiveEntitlements(sql, playerId);
        return res.status(200).json({
            success: true,
            serverNowMs: Date.now(),
            entitlements,
        });
    } catch (err) {
        if (err instanceof EntitlementReadError) {
            return res.status(500).json({ success: false, code: err.code });
        }
        console.error('[entitlements] read failed:', err && err.code);
        return res.status(500).json({ success: false, code: 'SERVER_ERROR' });
    }
};
