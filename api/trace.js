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

// ── INGEST CAPS (2026-08-19, store-readiness audit) ──────────────────────────
// This endpoint is unauthenticated and Access-Control-Allow-Origin: '*', which it
// MUST be — the published web app POSTs cross-origin from <app>.pinet.com and the
// shipped clients carry no key. Authenticating it would silently kill web tracing
// for every build already in the wild, which is the one thing that makes a web bug
// diagnosable at all (docs: the /triage-web-issue read path).
//
// So the exposure is bounded by SIZE, not by identity. Without these caps a single
// scripted caller can write unbounded rows into the production Neon DB at our cost —
// its sibling api/events/track.js has capped at 100 per batch since it was written
// (events/track.js:25) and this file never got the same treatment.
//
// TRUNCATION IS RECORDED, NEVER SILENT (CLAUDE.md §12: no silent failures). When a
// batch is trimmed the stored row carries truncated/droppedLines/droppedChars, so a
// short trace in the DB can always be told apart from a short trace on the client.
const MAX_LINES       = 500;      // lines per request (a cold-boot batch is ~100-200)
const MAX_LINE_CHARS  = 2000;     // one pathological line cannot carry a payload
const MAX_TOTAL_CHARS = 256000;   // ~256 KB of text per row, hard ceiling

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
    let lines = Array.isArray(body)
        ? body.map(toStr)
        : (body && Array.isArray(body.entries) ? body.entries.map(toStr)
        : (body && Array.isArray(body.lines)   ? body.lines.map(toStr)
        : (body ? [JSON.stringify(body)] : [])));

    if (!lines || lines.length === 0) {
        return res.status(200).json({ success: true, inserted: 0 });
    }

    // Apply the caps. Keep the HEAD of the batch: the boot/first-failure lines are the
    // diagnostic ones, and a flood arrives at the tail.
    const rawCount = lines.length;
    let dropped = rawCount > MAX_LINES ? rawCount - MAX_LINES : 0;
    let lineCuts = 0;
    let capped = lines.slice(0, MAX_LINES).map(l => {
        const s = String(l);
        if (s.length <= MAX_LINE_CHARS) return s;
        lineCuts++;                       // a per-line cut counts as truncation too
        return s.slice(0, MAX_LINE_CHARS) + '...[cut]';
    });
    let droppedChars = 0;
    let total = 0;
    const bounded = [];
    for (const l of capped) {
        if (total + l.length > MAX_TOTAL_CHARS) { droppedChars += l.length; dropped++; continue; }
        total += l.length;
        bounded.push(l);
    }
    const truncated = dropped > 0 || droppedChars > 0 || lineCuts > 0;
    if (truncated) {
        // Loud to us, invisible to the player (owner law: never a giant json failure screen).
        console.warn(`[web_trace] TRUNCATED sess=${String(session).slice(0, 14)} build=${build} ` +
                     `raw=${rawCount} kept=${bounded.length} droppedLines=${dropped} droppedChars=${droppedChars} lineCuts=${lineCuts} ` +
                     `(caps lines=${MAX_LINES} lineChars=${MAX_LINE_CHARS} totalChars=${MAX_TOTAL_CHARS})`);
    }
    lines = bounded;
    if (lines.length === 0) {
        return res.status(200).json({ success: true, inserted: 0, truncated: true });
    }

    try {
        // Echo CLIENT traces to Vercel runtime logs so they're readable via get_runtime_logs WITHOUT
        // the (sensitive, unpullable) DATABASE_URL — closes the web-debug read gap. One-line summary
        // always + each SIGNAL line as its OWN log entry (Pi flow + errors; the AudioMixer 'warning'
        // boot noise is intentionally excluded to keep volume/cost sane).
        try {
            const isSignal = l => /\[Flow:Pi\]|PiInit|PiAuth|Signing in|timed out|Exception|threw|softlock|NullReference|Fail|\berror\b|SeekerBootstrap|tier=|device=|\[Flow:Perf\]|\bfps=|\bF8\b|BreakCapture|flagged|\[Flow:Build\]|placement|TowerPlacement/i.test(l);
            const signal = lines.filter(isSignal);
            console.log(`[web_trace] sess=${String(session).slice(0, 14)} build=${build} lines=${lines.length} signal=${signal.length}`);
            for (const s of signal) console.log('  [sig] ' + s);
        } catch (e) { /* logging must never break the sink */ }

        const sql = neon(process.env.DATABASE_URL);
        // truncated/droppedLines ride INTO the row when caps fired, so a short trace in the DB
        // is always distinguishable from a short trace on the client (never a silent trim).
        const props = truncated
            ? { build: String(build), session: String(session), lines: lines,
                truncated: true, rawLines: rawCount, droppedLines: dropped, droppedChars: droppedChars, lineCuts: lineCuts }
            : { build: String(build), session: String(session), lines: lines };
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
