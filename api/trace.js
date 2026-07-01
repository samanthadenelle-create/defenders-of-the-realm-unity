// =============================================================================
// api/trace.js — Vercel Serverless Function (WO-443 remote web-debug sink)
// -----------------------------------------------------------------------------
// Receives WebTrace batches (FlowTrace steps + Unity errors/exceptions) from the
// WebGL client so real web-client errors become queryable in the DB.
//
// Client : Assets/_Modules/Core/Diagnostics/WebTrace.cs + WebTraceSink.cs
//   POST  application/json
//   Headers: X-Trace-Session, X-Trace-Build
//   Body   : the trace batch — an array of lines, OR { lines: [...] }
//   Reply  : { success: true }   (client is fire-and-forget; only checks 2xx)
//
// Storage: reuses the proven analytics_events table (event_name = 'web_trace',
// properties = { build, session, lines }) — NO new table/migration required.
// Driver: @neondatabase/serverless (same as events/track.js, game/save.js).
// Status codes: 200 | 400 | 500 (project constraint — no others).
// =============================================================================

const { neon } = require('@neondatabase/serverless');

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try { if (typeof body === 'string') body = JSON.parse(body); }
    catch { body = { _raw: String(req.body) }; }

    const session = req.headers['x-trace-session'] || (body && body.session) || 'anonymous';
    const build   = req.headers['x-trace-build']   || (body && body.build)   || 'unknown';

    // Batch may be a raw array of lines, or { lines: [...] }, or arbitrary JSON — store as-is.
    const lines = Array.isArray(body)
        ? body
        : (body && Array.isArray(body.lines) ? body.lines : (body ? [JSON.stringify(body)] : []));

    if (!lines || lines.length === 0) {
        return res.status(200).json({ success: true, inserted: 0 });
    }

    try {
        // Echo to Vercel runtime logs so the CLIENT traces are readable via get_runtime_logs
        // WITHOUT the (sensitive, unpullable) DATABASE_URL — this closes the web-debug read gap
        // that hid the Pi-hang trace on 2026-07-01. Log a one-line summary always + only the
        // SIGNAL lines (Fail/Warn/error/Pi/softlock) to keep runtime-log volume/cost sane.
        try {
            const flat = lines.map(l => typeof l === 'string' ? l : JSON.stringify(l));
            const signal = flat.filter(l => /Fail|Warn|error|exception|\bPi\b|threw|softlock|NullReference/i.test(l));
            console.log(`[web_trace] sess=${String(session).slice(0, 12)} build=${build} lines=${flat.length} signal=${signal.length}`);
            if (signal.length) console.log('[web_trace:signal]\n' + signal.join('\n'));
        } catch (e) { /* logging must never break the sink */ }

        const sql = neon(process.env.DATABASE_URL);
        const props = { build: String(build), session: String(session), lines: lines };
        // ::jsonb cast — the Neon HTTP driver sends params as strings (same as events/track.js).
        await sql`
            INSERT INTO analytics_events (player_id, event_name, properties, client_ts)
            VALUES (
                ${String(session)},
                ${'web_trace'},
                ${JSON.stringify(props)}::jsonb,
                ${Date.now()}
            )
        `;
        return res.status(200).json({ success: true, inserted: lines.length });
    } catch (err) {
        console.error('[trace] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
