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
    // CORS: the published app runs under <app>.pinet.com and POSTs traces cross-origin.
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Trace-Session, X-Trace-Build');
    if (req.method === 'OPTIONS') { return res.status(204).end(); }

    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try { if (typeof body === 'string') body = JSON.parse(body); }
    catch { body = { _raw: String(req.body) }; }

    const session = req.headers['x-trace-session'] || (body && (body.session || body.sessionId)) || 'anonymous';
    const build   = req.headers['x-trace-build']   || (body && (body.build   || body.buildId))   || 'unknown';

    // Normalize every caller shape to an array of readable PER-LINE strings:
    //   WebTrace client -> { sessionId, buildId, entries:[{utcMs,kind,tag,message,scene}] }
    //   others          -> a raw array, or { lines:[...] }
    // Per-entry strings (not one JSON blob) so the runtime-log echo below is greppable and each
    // line is its own log entry (a single blob gets truncated by the log viewer — that hid the
    // Pi flow lines on the first pass 2026-07-01).
    const toStr = e => (e && typeof e === 'object')
        ? `[${e.scene || '?'}] ${(e.kind || 'log')}: ${e.message != null ? e.message : JSON.stringify(e)}`.trim()
        : String(e);
    const lines = Array.isArray(body)
        ? body.map(toStr)
        : (body && Array.isArray(body.entries) ? body.entries.map(toStr)
        : (body && Array.isArray(body.lines)   ? body.lines.map(toStr)
        : (body ? [JSON.stringify(body)] : [])));

    if (!lines || lines.length === 0) {
        return res.status(200).json({ success: true, inserted: 0 });
    }

    try {
        // Echo CLIENT traces to Vercel runtime logs so they're readable via get_runtime_logs WITHOUT
        // the (sensitive, unpullable) DATABASE_URL — closes the web-debug read gap. One-line summary
        // always + each SIGNAL line as its OWN log entry (Pi flow + errors; the AudioMixer 'warning'
        // boot noise is intentionally excluded to keep volume/cost sane).
        try {
            const isSignal = l => /\[Flow:Pi\]|PiInit|PiAuth|Signing in|timed out|Exception|threw|softlock|NullReference|Fail|\berror\b/i.test(l);
            const signal = lines.filter(isSignal);
            console.log(`[web_trace] sess=${String(session).slice(0, 14)} build=${build} lines=${lines.length} signal=${signal.length}`);
            for (const s of signal) console.log('  [sig] ' + s);
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
