// =============================================================================
// api/bug-report.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Receives an in-game bug report from the Help menu and inserts it into the
// bug_reports table. Straight append-only insert.
//
// Client : Assets/_Modules/HUD/HelpMenu.cs  (PostBugReport coroutine)
//   POST  application/json
//   Body  : { "description": <text>,
//             "context": { "route": <sceneName>, "appVersion": <Application.version> } }
//           - description capped at 4000 chars client-side; no playerId is sent.
//   Reply : { "success": true }   (client only checks for 2xx; logs the body)
//
// HOST NOTE: HelpMenu posts to the OLD host
//   https://defenders-of-the-realm.vercel.app/api/bug-report   (NO "-v2")
// whereas every other service uses the "-v2" host. So unless the client is
// repointed (a .cs change — out of scope here), this function must be deployed
// on the OLD project to receive the call. See api/DB_SETUP.md → "Host mismatch".
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');

const MAX_DESCRIPTION = 4000;

module.exports = async (req, res) => {
    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    let body = req.body;
    try {
        if (typeof body === 'string') body = JSON.parse(body);
    } catch (err) {
        console.error('[bug-report] Body parse error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }

    if (!body || typeof body !== 'object') {
        return res.status(400).json({ error: 'Invalid payload' });
    }

    let description = body.description != null ? String(body.description) : '';
    if (!description.trim()) {
        return res.status(400).json({ error: 'Missing description' });
    }
    // Mirror the client-side cap defensively.
    if (description.length > MAX_DESCRIPTION) {
        description = description.slice(0, MAX_DESCRIPTION);
    }

    const context    = (body.context && typeof body.context === 'object') ? body.context : {};
    const route      = context.route      != null ? String(context.route)      : null;
    const appVersion = context.appVersion != null ? String(context.appVersion) : null;
    // HelpMenu sends no playerId today, but accept one if a future caller adds it.
    const playerId   = body.playerId      != null ? String(body.playerId)      : null;

    try {
        const sql = neon(process.env.DATABASE_URL);

        await sql`
            INSERT INTO bug_reports (description, route, app_version, player_id, context)
            VALUES (
                ${description},
                ${route},
                ${appVersion},
                ${playerId},
                ${JSON.stringify(context)}::jsonb
            )
        `;

        return res.status(200).json({ success: true });
    } catch (err) {
        console.error('[bug-report] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
