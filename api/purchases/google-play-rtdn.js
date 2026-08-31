'use strict';

// Authenticated Google Cloud Pub/Sub push receiver. Deliberately has no CORS:
// this is server-to-server only, and the Pub/Sub OIDC identity is mandatory.
const { neon } = require('@neondatabase/serverless');
const { readBodyExact } = require('../_lib/http');
const rtdn = require('../_lib/google-play-rtdn');

async function handler(req, res) {
    if (req.method !== 'POST') return res.status(405).end();
    const configured = rtdn.configurationReady(process.env);
    if (!configured.ok) return res.status(503).end();
    const identity = await rtdn.verifyPushIdentity(req.headers.authorization, configured);
    if (!identity.ok) return res.status(401).end();
    let raw;
    try { raw = (await readBodyExact(req, rtdn.MAX_BODY_BYTES)).buffer; }
    catch (_) { return res.status(400).end(); }
    if (raw.length > rtdn.MAX_BODY_BYTES) return res.status(400).end();
    const decoded = rtdn.decodeEnvelope(raw, configured.packageName);
    if (!decoded.ok) return res.status(400).end();
    let sql;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { return res.status(503).end(); }
    try {
        await rtdn.processNotification(sql, decoded, configured);
        // Pub/Sub acknowledges any 2xx. Durable quarantine is intentional: an
        // operator/reconciliation job must resolve it, not an infinite redelivery.
        return res.status(204).end();
    } catch (_) { return res.status(503).end(); }
}

module.exports = handler;
module.exports.config = { api: { bodyParser: false } };
