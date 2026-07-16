// =============================================================================
// api/admin/db.js — OWNER-ONLY read-only database viewer endpoint (2026-07-12)
// -----------------------------------------------------------------------------
// Backs tools/db-viewer/index.html (local HTML the owner double-clicks).
// GET only. Every request must carry header  X-Admin-Key  matching the Vercel
// env var ADMIN_DASH_KEY (sensitive). Constant-time compare; missing/mismatch
// → 400 (project constraint: status codes are 200 | 400 | 500 only).
//
// READ-ONLY BY CONSTRUCTION: every query is a SELECT with a hard LIMIT; all
// user inputs are parameterized via the neon sql tagged-template (same driver
// + house style as api/trace.js / game/load.js). No writes, no PII beyond what
// the tables already hold; save blobs are size-only unless one explicit
// player id is requested.
//
//   GET /api/admin/db?view=overview
//       per-table row counts + newest timestamp per table
//   GET /api/admin/db?view=players[&limit=N][&player=<id>]
//       latest N player_data rows (id, versions, timestamps, payload SIZE only);
//       player=<id> → that one player's full record
//   GET /api/admin/db?view=metrics
//       last-7-day aggregates from analytics_events (per-event-per-day counts,
//       distinct players/sessions per day, web_trace error-line count per day)
//   GET /api/admin/db?view=traces[&session=<id>][&limit=N]
//       with session: latest N web_trace rows for that session, with lines;
//       without: latest web_trace sessions (summary) so the owner can pick one
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const crypto = require('crypto');

// Constant-time key check. Hashing both sides first makes timingSafeEqual
// usable on unequal lengths without leaking length information.
function adminKeyOk(given, expected) {
    if (!given || !expected) return false;
    const a = crypto.createHash('sha256').update(String(given)).digest();
    const b = crypto.createHash('sha256').update(String(expected)).digest();
    return crypto.timingSafeEqual(a, b);
}

function clampLimit(raw, def, max) {
    const n = parseInt(raw, 10);
    if (!Number.isFinite(n) || n <= 0) return def;
    return Math.min(n, max);
}

// Paging offset for the traces view. Bounded like clampLimit so a hostile/garbled
// value can never become an unbounded scan: non-numeric/negative -> 0, hard ceiling
// so OFFSET stays sane (the largest real session seen is ~2840 batches).
function clampOffset(raw) {
    const n = parseInt(raw, 10);
    if (!Number.isFinite(n) || n <= 0) return 0;
    return Math.min(n, 100000);
}

module.exports = async (req, res) => {
    // CORS: the viewer is a local file (Origin "null") fetching cross-origin.
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Admin-Key');
    if (req.method === 'OPTIONS') { return res.status(204).end(); }

    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    const expected = process.env.ADMIN_DASH_KEY;
    if (!expected) {
        // Not configured yet — refuse everything (never fail open).
        return res.status(400).json({ error: 'Admin access not configured' });
    }
    if (!adminKeyOk(req.headers['x-admin-key'], expected)) {
        return res.status(400).json({ error: 'Unauthorized' });
    }

    const q = req.query || {};
    const view = String(q.view || 'overview');

    try {
        const sql = neon(process.env.DATABASE_URL);

        // ---------------------------------------------------------------- overview
        if (view === 'overview') {
            // Table names cannot be parameterized, so every query below is a
            // fully STATIC tagged-template literal (no user input reaches SQL).
            // Each table is probed independently so a missing table (schema
            // drift) degrades to an error entry instead of failing the view.
            const probes = [
                ['player_data',        'updated_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(updated_at)  AS latest FROM player_data`],
                ['analytics_events',   'received_at', () => sql`SELECT COUNT(*)::bigint AS rows, MAX(received_at) AS latest FROM analytics_events`],
                ['bug_reports',        'created_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(created_at)  AS latest FROM bug_reports`],
                ['auth_nonces',        'created_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(created_at)  AS latest FROM auth_nonces`],
                ['promo_codes',        'created_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(created_at)  AS latest FROM promo_codes`],
                ['promo_redemptions',  'redeemed_at', () => sql`SELECT COUNT(*)::bigint AS rows, MAX(redeemed_at) AS latest FROM promo_redemptions`],
                ['referrals',          'created_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(created_at)  AS latest FROM referrals`],
                ['referral_claims',    'claimed_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(claimed_at)  AS latest FROM referral_claims`],
                ['tower_swaps',        'logged_at',   () => sql`SELECT COUNT(*)::bigint AS rows, MAX(logged_at)   AS latest FROM tower_swaps`],
                ['player_profiles',    'created_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(created_at)  AS latest FROM player_profiles`],
                ['leaderboard_scores', 'updated_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(updated_at)  AS latest FROM leaderboard_scores`],
                ['achievement_grants', 'granted_at',  () => sql`SELECT COUNT(*)::bigint AS rows, MAX(granted_at)  AS latest FROM achievement_grants`],
            ];
            const rows = [];
            for (const [table, tsCol, run] of probes) {
                try {
                    const r = await run();
                    rows.push({ table: table, rows: Number(r[0].rows), latest: r[0].latest, latest_col: tsCol });
                } catch (e) {
                    rows.push({ table: table, rows: null, latest: null, latest_col: tsCol, error: 'missing or unreadable' });
                }
            }
            return res.status(200).json({ view: 'overview', generated_at: new Date().toISOString(), tables: rows });
        }

        // ---------------------------------------------------------------- players
        if (view === 'players') {
            if (q.player) {
                // One explicit player → the full record (the ONLY path that
                // returns a save blob).
                const rows = await sql`
                    SELECT player_id, schema_version, created_at, updated_at,
                           pg_column_size(game_state) AS payload_bytes, game_state
                    FROM player_data
                    WHERE player_id = ${String(q.player)}
                    LIMIT 1`;
                return res.status(200).json({ view: 'players', player: String(q.player), rows: rows });
            }
            const limit = clampLimit(q.limit, 25, 100);
            // List view: payload SIZE only — never dump save blobs in bulk.
            const rows = await sql`
                SELECT player_id, schema_version, created_at, updated_at,
                       pg_column_size(game_state) AS payload_bytes
                FROM player_data
                ORDER BY updated_at DESC
                LIMIT ${limit}`;
            return res.status(200).json({ view: 'players', limit: limit, rows: rows });
        }

        // ---------------------------------------------------------------- metrics
        if (view === 'metrics') {
            // All SQL aggregates over the last 7 days — no raw rows leave the DB.
            const perEventPerDay = await sql`
                SELECT date_trunc('day', received_at)::date::text AS day,
                       event_name,
                       COUNT(*)::bigint AS events
                FROM analytics_events
                WHERE received_at > NOW() - INTERVAL '7 days'
                GROUP BY 1, 2
                ORDER BY 1 DESC, 3 DESC
                LIMIT 500`;
            const perDay = await sql`
                SELECT date_trunc('day', received_at)::date::text AS day,
                       COUNT(*)::bigint AS events,
                       COUNT(DISTINCT player_id)::bigint AS distinct_players,
                       COUNT(DISTINCT properties->>'session')
                           FILTER (WHERE event_name = 'web_trace')::bigint AS distinct_trace_sessions
                FROM analytics_events
                WHERE received_at > NOW() - INTERVAL '7 days'
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 7`;
            const traceErrorsPerDay = await sql`
                SELECT date_trunc('day', e.received_at)::date::text AS day,
                       COUNT(*)::bigint AS error_lines
                FROM analytics_events e,
                     jsonb_array_elements_text(e.properties->'lines') AS line
                WHERE e.event_name = 'web_trace'
                  AND e.received_at > NOW() - INTERVAL '7 days'
                  AND line ~* '(exception|nullreference|softlock|\\yerror\\y|\\yfail)'
                GROUP BY 1
                ORDER BY 1 DESC
                LIMIT 7`;
            return res.status(200).json({
                view: 'metrics', window_days: 7,
                per_day: perDay,
                per_event_per_day: perEventPerDay,
                trace_error_lines_per_day: traceErrorsPerDay,
            });
        }

        // ---------------------------------------------------------------- traces
        if (view === 'traces') {
            const limit = clampLimit(q.limit, 20, 50);
            if (q.session) {
                // PAGING (2026-07-15 — the magenta-ground triage): this view was
                // ORDER BY received_at DESC LIMIT 20 with no offset, so a long session
                // (one real session ran 2840 batches / 153k lines) could only ever be read
                // from its TAIL — which is gameplay spam. The lines that actually diagnose a
                // bug — scene load: TERRAINDIAG, MagentaGuard/FloorDiag, catalog + Resources
                // resolution — are emitted in the FIRST batches and were unreachable. The
                // trace pipe recorded the answer and the reader could not see it.
                // order=asc  -> oldest first = the scene-load head (use this to triage).
                // offset=N   -> page deeper in either direction.
                const offset = clampOffset(q.offset);
                const asc = String(q.order || 'desc').toLowerCase() === 'asc';
                // ORDER BY direction cannot be parameterized in a tagged template (same
                // constraint the table probes above call out), so the two directions are
                // separate literal queries rather than interpolated SQL.
                const rows = asc
                    ? await sql`
                        SELECT event_id, received_at,
                               properties->>'build'   AS build,
                               properties->>'session' AS session,
                               jsonb_array_length(COALESCE(properties->'lines', '[]'::jsonb)) AS line_count,
                               properties->'lines'    AS lines
                        FROM analytics_events
                        WHERE event_name = 'web_trace'
                          AND properties->>'session' = ${String(q.session)}
                        ORDER BY received_at ASC
                        OFFSET ${offset} LIMIT ${limit}`
                    : await sql`
                        SELECT event_id, received_at,
                               properties->>'build'   AS build,
                               properties->>'session' AS session,
                               jsonb_array_length(COALESCE(properties->'lines', '[]'::jsonb)) AS line_count,
                               properties->'lines'    AS lines
                        FROM analytics_events
                        WHERE event_name = 'web_trace'
                          AND properties->>'session' = ${String(q.session)}
                        ORDER BY received_at DESC
                        OFFSET ${offset} LIMIT ${limit}`;
                // Total batches for the session, so a caller knows how far it can page
                // instead of guessing where the session ends.
                const totalRow = await sql`
                    SELECT COUNT(*)::bigint AS batches
                    FROM analytics_events
                    WHERE event_name = 'web_trace'
                      AND properties->>'session' = ${String(q.session)}`;
                const total = totalRow && totalRow[0] ? Number(totalRow[0].batches) : null;
                return res.status(200).json({
                    view: 'traces', session: String(q.session),
                    order: asc ? 'asc' : 'desc', offset: offset, limit: limit,
                    total_batches: total,
                    returned: rows.length,
                    has_more: total != null ? (offset + rows.length) < total : null,
                    rows: rows,
                });
            }
            // No session given → latest sessions summary so the owner can pick one.
            const rows = await sql`
                SELECT properties->>'session' AS session,
                       MAX(properties->>'build') AS build,
                       COUNT(*)::bigint AS batches,
                       SUM(jsonb_array_length(COALESCE(properties->'lines', '[]'::jsonb)))::bigint AS total_lines,
                       MAX(received_at) AS latest
                FROM analytics_events
                WHERE event_name = 'web_trace'
                  AND received_at > NOW() - INTERVAL '7 days'
                GROUP BY 1
                ORDER BY 5 DESC
                LIMIT ${limit}`;
            return res.status(200).json({ view: 'traces', sessions: rows, limit: limit });
        }

        return res.status(400).json({ error: 'Unknown view. Use: overview | players | metrics | traces' });
    } catch (err) {
        console.error('[admin/db] error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
