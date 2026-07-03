// =============================================================================
// api/bug-report.js — Vercel Serverless Function (WO-596 player bug report)
// -----------------------------------------------------------------------------
// Receives a player bug report from the in-game form (BugReportView/VM) and
// inserts it into the bug_reports table. Sits BESIDE api/trace.js on the LIVE
// "-v2" project and matches its patterns (CORS for pinet, Neon HTTP driver,
// runtime-log echo readable without DATABASE_URL).
//
// Client : Assets/_Modules/HUD/BugReportVM.cs (Submit coroutine)
//   POST  application/json
//   Body  : { note:         <text, may be empty — the capture is the value>,
//             sceneName:    <active scene>,
//             sessionId:    <anonymous per-session id, "br-...">,
//             version:      <Application.version>,
//             platform:     <Application.platform>,
//             piUid?:       <SALTED SHA-256 HASH of the Pi uid — never raw>,
//             traceTail:    [ recent "[Flow:*]"/error lines, oldest first ],
//             screenshotB64?: <JPEG re-encoded ≤ ~300KB, base64> }
//   Reply : { success: true }  (client checks 2xx; Failed → local fallback save)
//
// Legacy shape { description, context:{route,appVersion} } still accepted so a
// stale client can't 400 (the old stub's dead-domain POST is retired client-side).
//
// Size caps (defensive, mirror the client): note 4000 chars, traceTail 120 lines
// x 500 chars, screenshot base64 ~420K chars (≈300KB binary + b64 overhead) —
// an over-cap image is DROPPED (report still lands, context notes the drop).
//
// Storage: bug_reports (description, route, app_version, player_id, context)
//   note→description, sceneName→route, version→app_version, piUid hash→player_id,
//   everything else (platform, sessionId, traceTail, screenshotB64) → context
//   jsonb. NO migration needed — the existing table carries the new payload.
// Driver: @neondatabase/serverless (same as trace.js / events/track.js).
// Status codes: 200 | 400 | 500 (project constraint — no others).
// =============================================================================

const { neon } = require('@neondatabase/serverless');

const MAX_NOTE       = 4000;
const MAX_TAIL_LINES = 120;
const MAX_TAIL_CHARS = 500;
const MAX_SHOT_B64   = 420000; // ≈300KB binary as base64

module.exports = async (req, res) => {
    // CORS: the published app runs under <app>.pinet.com and POSTs cross-origin
    // (same as api/trace.js — the OLD version of this function had no CORS and
    // would have failed every browser call).
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    if (req.method === 'OPTIONS') { return res.status(204).end(); }

    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try { if (typeof body === 'string') body = JSON.parse(body); }
    catch (err) {
        console.error('[bug-report] Body parse error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }
    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    // ── Normalize (new WO-596 shape first, legacy {description,context} second) ──
    const legacyCtx = (body.context && typeof body.context === 'object') ? body.context : {};

    let note = body.note != null ? String(body.note)
             : body.description != null ? String(body.description) : '';
    if (note.length > MAX_NOTE) note = note.slice(0, MAX_NOTE);

    const sceneName  = body.sceneName  != null ? String(body.sceneName)
                     : legacyCtx.route != null ? String(legacyCtx.route) : null;
    const version    = body.version    != null ? String(body.version)
                     : legacyCtx.appVersion != null ? String(legacyCtx.appVersion) : null;
    const platform   = body.platform   != null ? String(body.platform)   : null;
    const sessionId  = body.sessionId  != null ? String(body.sessionId)  : null;
    // piUid arrives as a client-side SALTED HASH (never a raw uid) — stored as
    // player_id so repeat reporters correlate without carrying identity.
    const piUidHash  = body.piUid      != null ? String(body.piUid)
                     : body.playerId   != null ? String(body.playerId)   : null;

    let traceTail = Array.isArray(body.traceTail) ? body.traceTail.map(l => String(l)) : [];
    if (traceTail.length > MAX_TAIL_LINES) traceTail = traceTail.slice(-MAX_TAIL_LINES);
    traceTail = traceTail.map(l => l.length > MAX_TAIL_CHARS ? l.slice(0, MAX_TAIL_CHARS) : l);

    let screenshotB64 = typeof body.screenshotB64 === 'string' ? body.screenshotB64 : null;
    let screenshotDropped = false;
    if (screenshotB64 && screenshotB64.length > MAX_SHOT_B64) {
        screenshotB64 = null;                 // over the ~300KB cap — drop, keep the report
        screenshotDropped = true;
    }

    // A report with no note AND no capture at all is noise.
    if (!note.trim() && traceTail.length === 0 && !screenshotB64) {
        return res.status(400).json({ error: 'Empty report' });
    }

    const context = {
        platform,
        sessionId,
        traceTail,
        screenshotB64,
        screenshotDropped: screenshotDropped || undefined,
    };

    try {
        // Echo a signal line to Vercel runtime logs so reports are readable via
        // get_runtime_logs WITHOUT the (sensitive, unpullable) DATABASE_URL —
        // same pattern as api/trace.js. Note + tail lines each as their OWN log
        // entry (one blob gets truncated by the log viewer).
        try {
            console.log(`[bug_report] sess=${String(sessionId || 'anon').slice(0, 14)} scene=${sceneName} ` +
                        `ver=${version} plat=${platform} note=${note.length}ch tail=${traceTail.length} ` +
                        `shot=${screenshotB64 ? screenshotB64.length + 'b64' : (screenshotDropped ? 'DROPPED(cap)' : 'none')} ` +
                        `pi=${piUidHash ? 'hash' : 'no'}`);
            if (note.trim()) console.log('  [note] ' + note.slice(0, 300).replace(/\n/g, ' | '));
            for (const l of traceTail.slice(-15)) console.log('  [tail] ' + l);
        } catch (e) { /* logging must never break the sink */ }

        const sql = neon(process.env.DATABASE_URL);
        // ::jsonb cast — the Neon HTTP driver sends params as strings (same as trace.js).
        await sql`
            INSERT INTO bug_reports (description, route, app_version, player_id, context)
            VALUES (
                ${note},
                ${sceneName},
                ${version},
                ${piUidHash},
                ${JSON.stringify(context)}::jsonb
            )
        `;

        return res.status(200).json({ success: true });
    } catch (err) {
        console.error('[bug-report] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
