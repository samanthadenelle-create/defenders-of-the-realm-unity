// =============================================================================
// api/leaderboard/get.js — Vercel Serverless Function (WO-129 §2.1)
// -----------------------------------------------------------------------------
// Returns a server-authoritative leaderboard board: the TOP-N (with usernames),
// and — when ?wallet= is supplied — the caller's own rank plus the ±N neighbors
// around them ("the next rung always feels reachable", US-2). READ-ONLY and
// PUBLIC: anyone can read any board, no auth (a rank reveals nothing private).
//
// The client NEVER asserts a score or a rank here — rank is computed at read time
// from leaderboard_scores ordered by score DESC (WO-129 §5: no client-authoritative
// scores). Scores are written only by the wallet-gated submit endpoint.
//
// Client : LeaderboardService (Core) — WO-129 §4 (NEW; read-only).
//   GET   /api/leaderboard?metric=<m>&period=<id>&wallet=<addr>&limit=<n>&window=<n>
//     metric   which board (default 'highest_wave'). Whitelisted below.
//     period   'alltime' (default) or a week key 'YYYY-Www' (e.g. '2026-W22').
//     wallet   OPTIONAL — when present, also return that player's rank window.
//     limit    OPTIONAL top-N size (default 50, clamped 1..100).
//     window   OPTIONAL neighbors above/below the caller (default 2, clamped 0..10).
//   Reply : {
//     success: true, metric, period,
//     top: [ { rank, wallet, username, score, updatedAt }, ... ],
//     you: { rank, wallet, username, score, updatedAt } | null   // null if unranked
//     youWindow: [ { rank, ... }, ... ]                          // ±window around you
//   }
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 500   (public read — never 401/404)
// =============================================================================

const { neon } = require('@neondatabase/serverless');

// Whitelisted boards (WO-129 §2.1). A new board needs no migration — just add it
// here. 'arena' is RESERVED (Arena not built); it reads as an empty board today.
const ALLOWED_METRICS = new Set([
    'highest_wave',     // SHIP FIRST
    'longest_hold',     // Phase 1
    'total_resources',  // Phase 2
    'clan',             // Phase 2
    'arena',            // reserved (gated on Arena)
]);
const DEFAULT_METRIC = 'highest_wave';

// 'alltime' or an ISO week key like '2026-W22' (weekly reset, Mon 00:00 UTC).
const PERIOD_RE = /^(alltime|\d{4}-W\d{2})$/;
const DEFAULT_PERIOD = 'alltime';

const DEFAULT_LIMIT = 50;
const MAX_LIMIT = 100;
const DEFAULT_WINDOW = 2;
const MAX_WINDOW = 10;

function clampInt(raw, def, min, max) {
    const v = parseInt(raw, 10);
    if (!Number.isFinite(v)) return def;
    return Math.min(max, Math.max(min, v));
}

module.exports = async (req, res) => {
    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    const q = req.query || {};

    const metric = q.metric != null ? String(q.metric).trim() : DEFAULT_METRIC;
    if (!ALLOWED_METRICS.has(metric)) {
        return res.status(400).json({ error: 'Unknown metric' });
    }

    const period = q.period != null ? String(q.period).trim() : DEFAULT_PERIOD;
    if (!PERIOD_RE.test(period)) {
        return res.status(400).json({ error: 'Malformed period' });
    }

    const wallet = q.wallet != null ? String(q.wallet).trim() : '';
    const limit = clampInt(q.limit, DEFAULT_LIMIT, 1, MAX_LIMIT);
    const windowSize = clampInt(q.window, DEFAULT_WINDOW, 0, MAX_WINDOW);

    try {
        const sql = neon(process.env.DATABASE_URL);

        // ── TOP-N ─────────────────────────────────────────────────────────────
        // ROW_NUMBER over the board gives a dense 1-based rank. Tie-break on
        // updated_at (earlier best ranks higher) then wallet for a stable order.
        const topRows = await sql`
            SELECT
                ROW_NUMBER() OVER (
                    ORDER BY s.score DESC, s.updated_at ASC, s.wallet ASC
                ) AS rank,
                s.wallet,
                p.username,
                s.score,
                s.updated_at
            FROM leaderboard_scores s
            LEFT JOIN player_profiles p ON p.wallet = s.wallet
            WHERE s.metric = ${metric} AND s.period_id = ${period}
            ORDER BY s.score DESC, s.updated_at ASC, s.wallet ASC
            LIMIT ${limit}
        `;

        const top = topRows.map(mapRow);

        // ── CALLER RANK WINDOW (only when ?wallet= given) ─────────────────────
        let you = null;
        let youWindow = [];
        if (wallet) {
            // Compute the caller's rank with the SAME ordering, then pull the
            // ±window rows around it. Done in one CTE so rank + neighbors stay
            // consistent. If the wallet has no score on this board → you = null.
            const ranked = await sql`
                WITH board AS (
                    SELECT
                        ROW_NUMBER() OVER (
                            ORDER BY s.score DESC, s.updated_at ASC, s.wallet ASC
                        ) AS rank,
                        s.wallet,
                        p.username,
                        s.score,
                        s.updated_at
                    FROM leaderboard_scores s
                    LEFT JOIN player_profiles p ON p.wallet = s.wallet
                    WHERE s.metric = ${metric} AND s.period_id = ${period}
                ),
                me AS (
                    SELECT rank FROM board WHERE wallet = ${wallet} LIMIT 1
                )
                SELECT b.rank, b.wallet, b.username, b.score, b.updated_at
                FROM board b, me
                WHERE b.rank BETWEEN me.rank - ${windowSize} AND me.rank + ${windowSize}
                ORDER BY b.rank ASC
            `;

            youWindow = ranked.map(mapRow);
            you = youWindow.find((r) => r.wallet === wallet) || null;
        }

        return res.status(200).json({
            success: true,
            metric,
            period,
            top,
            you,
            youWindow,
        });
    } catch (err) {
        console.error('[leaderboard/get] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};

// Map a DB row to the client-facing shape (camelCase, numeric rank/score).
function mapRow(r) {
    return {
        rank: Number(r.rank),
        wallet: r.wallet,
        username: r.username ?? null,
        score: Number(r.score),
        updatedAt: r.updated_at,
    };
}
