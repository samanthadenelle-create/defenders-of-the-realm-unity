// =============================================================================
// api/leaderboard/submit.js — Vercel Serverless Function (WO-129 §2.1 / §5)
// -----------------------------------------------------------------------------
// Records a player's score on a board. WALLET-AUTH GATED (same signed-nonce
// protocol as save/load): the signed wallet MUST equal the score's wallet, so a
// player can only submit for THEMSELVES. The write is a MONOTONIC MAX-MERGE —
// score is only ever RAISED (GREATEST on conflict) — so a stale/replayed submit
// can never lower a standing, and the server is the sole authority on the stored
// value (WO-129 §5: "NO client-authoritative scores").
//
// This is the interim ingestion path. The end-state (WO-129 §2.1, §5) is the
// SERVER deriving the score from authoritative gameplay events (the WO-121
// wave_cleared/run_ended signal) rather than accepting a client number — at which
// point this endpoint either moves behind that derivation or is removed. Until
// the event pipeline lands, the auth gate + monotonic max-merge are the guard:
// the submit is signed by the wallet and can only ratchet a score upward.
//
// Client : LeaderboardService (Core) — WO-129 §4 (NEW).
//   POST  application/json   (raw body — bodyParser disabled, signature is over
//                             the EXACT bytes, same as save.js)
//   Headers: X-Wallet / X-Nonce / X-Signature  (see _lib/wallet-auth)
//   Body  : { wallet, metric, period, score, meta? }
//     wallet  base58 address — MUST equal X-Wallet (enforced by verifyAndConsume).
//     metric  board id (whitelisted below).
//     period  'alltime' or 'YYYY-Www'.
//     score   integer >= 0, <= MAX_SCORE.
//     meta    optional JSON object (run id, hero used, ...).
//   Reply : { success:true, metric, period, score, raised:bool }
//     raised = whether this submit actually moved the stored best up.
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { verifyAndConsume } = require('../_lib/wallet-auth');
// WO-1243 operator kill switches. Fail-OPEN by ruling — see _lib/maintenance.js.
const { enforce: maintenanceEnforce, AREA_ARENA, AREA_SERVER } = require('../_lib/maintenance');

// The signature is over the EXACT raw body bytes, so we must read the unparsed
// Buffer — disable Vercel's body parser (same requirement as api/game/save.js).
module.exports.config = { api: { bodyParser: false } };

const ALLOWED_METRICS = new Set([
    'highest_wave',
    'longest_hold',
    'total_resources',
    'clan',
    'arena',
]);
const PERIOD_RE = /^(alltime|\d{4}-W\d{2})$/;
const MAX_SCORE = 1_000_000_000; // anti-tamper ceiling (mirrors save.js bounds)

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    // ── Read raw body (signature binds to these exact bytes) ───────────────
    let rawBody;
    try {
        rawBody = await readBody(req);
    } catch (err) {
        console.error('[leaderboard/submit] Body read error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString());
    } catch (err) {
        console.error('[leaderboard/submit] Decode error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const wallet = body.wallet != null ? String(body.wallet).trim() : '';
    const metric = body.metric != null ? String(body.metric).trim() : '';
    const period = body.period != null ? String(body.period).trim() : '';
    const score = Number(body.score);
    const meta = body.meta && typeof body.meta === 'object' ? body.meta : {};

    if (!wallet) return res.status(400).json({ error: 'Missing wallet' });
    if (!ALLOWED_METRICS.has(metric)) return res.status(400).json({ error: 'Unknown metric' });
    if (!PERIOD_RE.test(period)) return res.status(400).json({ error: 'Malformed period' });
    if (!Number.isFinite(score) || score < 0 || score > MAX_SCORE) {
        return res.status(400).json({ error: 'Score out of bounds' });
    }
    const scoreInt = Math.floor(score);

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[leaderboard/submit] DB init error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }

    // ── AUTH GATE — verify + burn nonce; signed wallet MUST equal `wallet` ──
    let auth;
    try {
        auth = await verifyAndConsume(sql, req.headers, rawBody, wallet);
    } catch (err) {
        console.error('[leaderboard/submit] Auth check error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
    if (!auth.ok) {
        return res.status(401).json({ error: 'Unauthorized', reason: auth.reason });
    }

    // ── OPERATOR KILL SWITCH: the arena seal (WO-1243) ─────────────────────
    //
    // The `arena` metric is the ONLY arena result that ever becomes
    // server-authoritative, so this row is the one place an arena exploit can
    // actually bank anything. Sealing `arena` therefore stops the exploit's
    // PAYOFF even against a modified client that ignores the client-side gate.
    // The other four metrics are untouched by an arena seal; `server` closes all
    // five, because it closes everything.
    //
    // ⚠ `wallet` is the identity here. It is passed only so the refusal record
    // can carry a SALTED FINGERPRINT of it (_lib/maintenance.fingerprint). The
    // raw address is never written to the audit row.
    if (await maintenanceEnforce(sql, req, res, metric === 'arena' ? AREA_ARENA : AREA_SERVER, wallet, null)) return;

    // ── Monotonic MAX-MERGE upsert ─────────────────────────────────────────
    // On conflict, only RAISE the score (GREATEST). updated_at + meta move only
    // when the score actually went up, so a replayed/stale submit is a no-op.
    // The `raised` flag tells the client whether their best improved.
    try {
        const rows = await sql`
            INSERT INTO leaderboard_scores (wallet, metric, period_id, score, meta, updated_at)
            VALUES (${wallet}, ${metric}, ${period}, ${scoreInt}, ${JSON.stringify(meta)}::jsonb, NOW())
            ON CONFLICT (wallet, metric, period_id) DO UPDATE
            SET
                score      = GREATEST(leaderboard_scores.score, EXCLUDED.score),
                meta       = CASE WHEN EXCLUDED.score > leaderboard_scores.score
                                  THEN EXCLUDED.meta ELSE leaderboard_scores.meta END,
                updated_at = CASE WHEN EXCLUDED.score > leaderboard_scores.score
                                  THEN NOW() ELSE leaderboard_scores.updated_at END
            RETURNING score, (xmax = 0) AS inserted
        `;

        const stored = rows.length > 0 ? Number(rows[0].score) : scoreInt;
        const wasInsert = rows.length > 0 && rows[0].inserted === true;
        const raised = wasInsert || stored === scoreInt;

        return res.status(200).json({
            success: true,
            metric,
            period,
            score: stored,
            raised,
        });
    } catch (err) {
        console.error('[leaderboard/submit] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};

// ── Utility: collect raw request body into a Buffer ────────────────────────
function readBody(req) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        req.on('data', (chunk) => chunks.push(chunk));
        req.on('end', () => resolve(Buffer.concat(chunks)));
        req.on('error', (err) => reject(err));
    });
}
