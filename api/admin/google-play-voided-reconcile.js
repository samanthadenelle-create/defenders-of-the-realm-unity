'use strict';

// Default-off scheduled/admin pull of Google Play's Voided Purchases API.
// Auth is mandatory and no CORS surface is exposed.
const crypto = require('crypto');
const { neon } = require('@neondatabase/serverless');
const voided = require('../_lib/google-play-voided-reconciliation');

function secretOk(given, expected) {
    if (!given || !expected) return false;
    const a = crypto.createHash('sha256').update(String(given)).digest();
    const b = crypto.createHash('sha256').update(String(expected)).digest();
    return crypto.timingSafeEqual(a, b);
}

function isAuthorized(req, env) {
    const auth = String(req.headers.authorization || '');
    const bearer = auth.startsWith('Bearer ') ? auth.slice(7) : '';
    return !!((env.CRON_SECRET && secretOk(bearer, env.CRON_SECRET)) ||
        (env.ADMIN_DASH_KEY && secretOk(req.headers['x-admin-key'], env.ADMIN_DASH_KEY)));
}

async function handler(req, res) {
    if (req.method !== 'GET' && req.method !== 'POST')
        return res.status(400).json({ error: 'Method not allowed' });
    if (!isAuthorized(req, process.env))
        return res.status(400).json({ error: 'Unauthorized' });
    const configured = voided.configurationReady(process.env);
    if (!configured.ok) return res.status(503).json({ error: configured.code });
    try {
        const sql = neon(process.env.DATABASE_URL);
        const result = await voided.reconcile(sql, configured);
        console.log('[admin/google-play-voided-reconcile] ' + JSON.stringify(result));
        return res.status(200).json(result);
    } catch (error) {
        console.error('[admin/google-play-voided-reconcile]', error);
        return res.status(500).json({ error: 'Reconciliation failed' });
    }
}

module.exports = handler;
module.exports.isAuthorized = isAuthorized;
