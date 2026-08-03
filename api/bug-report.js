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
        const stored = await insertReport(sql, { note, sceneName, version, piUidHash, context });
        try {
            console.log(`[bug_report] STORED report_id=${stored.reportId} via=${stored.shape} ` +
                        `identity=${piUidHash ? String(piUidHash).slice(0, 16) : 'none'}`);
        } catch (e) { /* logging must never break the sink */ }

        return res.status(200).json({ success: true, reportId: stored.reportId, shape: stored.shape });
    } catch (err) {
        console.error('[bug-report] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};

// =============================================================================
//  DRIFT-TOLERANT INSERT (2026-08-02 — this endpoint was 500-ing on EVERY report)
// -----------------------------------------------------------------------------
//  CAPTURED PROOF, live production, request 02:25:43 UTC 2026-08-03:
//      [bug-report] DB error: NeonDbError:
//      column "player_id" of relation "bug_reports" does not exist   (SQLSTATE 42703)
//  The live bug_reports table predates api/schema.sql's definition and is missing
//  columns the INSERT names — the SAME schema drift already documented for
//  player_data (schema.sql §1). Result: bug_reports has 0 rows, and every bug a
//  tester has ever submitted from Settings died as a 500. The player saw a
//  failure toast and their capture went to a local file nobody reads.
//
//  Two fixes, both needed:
//    • api/schema.sql now carries idempotent ALTER ... ADD COLUMN IF NOT EXISTS
//      statements for every column this file writes. That is the REAL fix and it
//      requires the owner to run schema.sql against Neon.
//    • This cascade is the BELT: it retries with progressively fewer columns,
//      folding whatever it had to drop into the payload it can still write, so a
//      report lands even on a drifted table. A bug report is the one thing we
//      cannot afford to lose to a missing column — it is the tester's only voice.
//
//  Each attempt reports WHICH shape succeeded (`via=` in the log, `shape` in the
//  response), so drift stays visible instead of being silently papered over.
// =============================================================================
async function insertReport(sql, r) {
    const ctxFull = JSON.stringify(r.context);
    const attempts = [
        // 1. The intended shape.
        {
            shape: 'full',
            run: () => sql`
                INSERT INTO bug_reports (description, route, app_version, player_id, context)
                VALUES (${r.note}, ${r.sceneName}, ${r.version}, ${r.piUidHash}, ${ctxFull}::jsonb)
                RETURNING report_id`,
        },
        // 2. No player_id column — fold the identity into context so it is not lost.
        {
            shape: 'no_player_id',
            run: () => sql`
                INSERT INTO bug_reports (description, route, app_version, context)
                VALUES (${r.note}, ${r.sceneName}, ${r.version},
                        ${JSON.stringify(Object.assign({}, r.context, { playerId: r.piUidHash }))}::jsonb)
                RETURNING report_id`,
        },
        // 3. Only description + context survive — fold route/version/identity in.
        {
            shape: 'description_context',
            run: () => sql`
                INSERT INTO bug_reports (description, context)
                VALUES (${r.note},
                        ${JSON.stringify(Object.assign({}, r.context, {
                            playerId: r.piUidHash, route: r.sceneName, appVersion: r.version,
                        }))}::jsonb)
                RETURNING report_id`,
        },
        // 4. Last resort — a single text column. Everything becomes the description.
        //    Ugly and greppable beats lost.
        {
            shape: 'description_only',
            run: () => sql`
                INSERT INTO bug_reports (description)
                VALUES (${r.note + '\n\n[context] ' + JSON.stringify(Object.assign({}, r.context, {
                    playerId: r.piUidHash, route: r.sceneName, appVersion: r.version,
                }))})
                RETURNING report_id`,
        },
        // 5. Even `report_id` may not be the PK's name on a drifted table, and a
        //    bad RETURNING raises the same 42703 as a bad column. Drop RETURNING
        //    entirely: we lose the id (reportId comes back null) and still keep
        //    the report, which is the trade that matters.
        {
            shape: 'description_only_no_returning',
            run: () => sql`
                INSERT INTO bug_reports (description)
                VALUES (${r.note + '\n\n[context] ' + JSON.stringify(Object.assign({}, r.context, {
                    playerId: r.piUidHash, route: r.sceneName, appVersion: r.version,
                }))})`,
        },
    ];

    let lastErr = null;
    for (const attempt of attempts) {
        try {
            const rows = await attempt.run();
            const reportId = rows && rows[0] ? Number(rows[0].report_id) : null;
            if (attempt.shape !== 'full') {
                console.warn(`[bug-report] SCHEMA DRIFT: fell back to shape "${attempt.shape}" — ` +
                             `run api/schema.sql against Neon to restore the full column set. ` +
                             `Last error: ${lastErr ? lastErr.message : 'n/a'}`);
            }
            return { reportId: reportId, shape: attempt.shape };
        } catch (err) {
            lastErr = err;
            // Only a missing-column / undefined-table error is worth retrying with a
            // narrower shape. Anything else (connection, permission) will fail the
            // same way every time — rethrow immediately rather than hammering.
            const code = err && err.code;
            if (code !== '42703' && code !== '42P01' && code !== '42804') throw err;
        }
    }
    throw lastErr || new Error('bug_reports insert failed with every shape');
}
