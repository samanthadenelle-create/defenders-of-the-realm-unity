// =============================================================================
// api/admin/cleanup.js — WO-685: web-trace retention / TTL sweep (2026-07-12)
// -----------------------------------------------------------------------------
// The web-trace pipe (WO-443) writes every WebGL diagnostic batch into
// analytics_events (event_name = 'web_trace') and NEVER deletes it. The client
// header + WebTrace.cs both promise a "7-day TTL" (WebTrace.cs:8, :35) that has
// NO server-side enforcer — the SECURITY_AUDIT_2026-07-12 finding H1 ("the
// 7-day web_trace TTL cron DOES NOT EXIST") + M4 (auth_nonces pruned only
// per-wallet opportunistically). This function IS that sweep.
//
// It runs a bounded, idempotent DELETE:
//   1. web_trace rows in analytics_events older than 7 days (received_at cutoff).
//   2. auth_nonces that are spent (used = TRUE) or expired (expires_at < NOW()).
//
// INVOKED BY (either is sufficient; both are constant-time checked):
//   • Vercel Cron — the scheduled invocation carries
//       Authorization: Bearer <CRON_SECRET>
//     (Vercel injects this automatically when the CRON_SECRET env var is set).
//   • Manual/admin run — header  X-Admin-Key  matching ADMIN_DASH_KEY (the same
//     key the read-only db viewer uses, api/admin/db.js).
// Anything else → 400 (never fail open; never widen exposure).
//
// Driver: @neondatabase/serverless (same as api/trace.js / api/admin/db.js).
// All SQL is parameterized / static tagged-template — no user input reaches SQL.
// Status codes: 200 | 400 | 500 (project constraint — no others).
//
// GOES LIVE only on the owner's next deploy (the crons key in vercel.json is
// read at deploy time). This file does NOT deploy itself.
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const crypto = require('crypto');

// Retention window for web_trace rows. Matches the 7-day TTL the client promises.
const RETENTION_DAYS = 7;

// Constant-time secret compare (hash both sides so timingSafeEqual is length-safe
// and never leaks length). Same shape as api/admin/db.js:adminKeyOk.
function secretOk(given, expected) {
    if (!given || !expected) return false;
    const a = crypto.createHash('sha256').update(String(given)).digest();
    const b = crypto.createHash('sha256').update(String(expected)).digest();
    return crypto.timingSafeEqual(a, b);
}

// Is this request an authorized cron or admin invocation?
function isAuthorized(req) {
    // 1) Vercel Cron: Authorization: Bearer <CRON_SECRET>
    const cronSecret = process.env.CRON_SECRET;
    if (cronSecret) {
        const auth = req.headers['authorization'] || '';
        const bearer = auth.startsWith('Bearer ') ? auth.slice(7) : '';
        if (bearer && secretOk(bearer, cronSecret)) return true;
    }
    // 2) Manual admin run: X-Admin-Key == ADMIN_DASH_KEY
    const adminKey = process.env.ADMIN_DASH_KEY;
    if (adminKey && secretOk(req.headers['x-admin-key'], adminKey)) return true;
    return false;
}

module.exports = async (req, res) => {
    // No CORS surface — this is a server-to-server / admin endpoint, not a
    // browser-called one. Do NOT set Access-Control-Allow-Origin (never widen).

    // Vercel Cron issues GET; allow POST for a manual admin run too.
    if (req.method !== 'GET' && req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    if (!isAuthorized(req)) {
        // Not a valid cron secret or admin key → refuse (never fail open).
        return res.status(400).json({ error: 'Unauthorized' });
    }

    try {
        const sql = neon(process.env.DATABASE_URL);

        // 1) web_trace retention sweep. received_at is the server receive time
        //    (analytics_events DEFAULT NOW()), so it is the trusted TTL clock —
        //    never the client-supplied client_ts. Parameterized interval days.
        const tracePurge = await sql`
            DELETE FROM analytics_events
            WHERE event_name = 'web_trace'
              AND received_at < NOW() - (${RETENTION_DAYS} * INTERVAL '1 day')
            RETURNING event_id
        `;

        // 2) auth_nonces sweep — burned or expired challenges are dead weight
        //    (folds in audit M4). Safe: a live, unused, unexpired nonce is kept.
        const noncePurge = await sql`
            DELETE FROM auth_nonces
            WHERE used = TRUE OR expires_at < NOW()
            RETURNING nonce
        `;

        const result = {
            success: true,
            ran_at: new Date().toISOString(),
            retention_days: RETENTION_DAYS,
            deleted_web_trace_rows: tracePurge.length,
            deleted_auth_nonces: noncePurge.length,
        };
        console.log('[admin/cleanup] ' + JSON.stringify(result));
        return res.status(200).json(result);
    } catch (err) {
        console.error('[admin/cleanup] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
