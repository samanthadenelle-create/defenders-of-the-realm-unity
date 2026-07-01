// =============================================================================
// api/events/track.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Receives a batch of analytics events from the Unity client (EventTracker.cs)
// and inserts one row per event into the analytics_events table.
//
// Client : Assets/_Modules/Core/Analytics/EventTracker.cs
//   POST  application/json
//   Body  : { "events": [ { "playerId", "eventName", "properties", "clientTs" }, … ] }
//           - playerId   string  (BoundWallet | "anonymous")
//           - eventName  string  (snake_case)
//           - properties string  (JSON STRING — parsed to JSONB on insert)
//           - clientTs   long    (unix epoch MILLISECONDS)
//   Reply : { "success": true }   (client is fire-and-forget; only checks 2xx)
//
// Driver: @neondatabase/serverless   (same as game/save.js, game/load.js)
// Status codes: 200 | 400 | 500   (project constraint — no others)
// =============================================================================

const { neon } = require('@neondatabase/serverless');

module.exports = async (req, res) => {
    // CORS: the published app runs under <app>.pinet.com and POSTs events cross-origin.
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    if (req.method === 'OPTIONS') { return res.status(204).end(); }

    if (req.method !== 'POST') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    // ── Parse body (Vercel auto-parses application/json into req.body) ─────────
    let body = req.body;
    try {
        if (typeof body === 'string') body = JSON.parse(body);
    } catch (err) {
        console.error('[events/track] Body parse error:', err);
        return res.status(400).json({ error: 'Invalid payload' });
    }

    const events = body && Array.isArray(body.events) ? body.events : null;
    if (!events) {
        return res.status(400).json({ error: 'Missing events array' });
    }
    if (events.length === 0) {
        // Nothing to write, but a valid request — keep the client happy.
        return res.status(200).json({ success: true, inserted: 0 });
    }

    try {
        const sql = neon(process.env.DATABASE_URL);

        let inserted = 0;
        for (const ev of events) {
            if (!ev) continue;

            const playerId  = ev.playerId  != null ? String(ev.playerId)  : 'anonymous';
            const eventName = ev.eventName != null ? String(ev.eventName) : null;
            if (!eventName) continue; // skip malformed events rather than fail the batch

            // properties arrives as a JSON STRING from the client; parse so it
            // lands as proper JSONB. Fall back to {} if it isn't valid JSON.
            let propsObj = {};
            if (ev.properties != null) {
                if (typeof ev.properties === 'string') {
                    try { propsObj = JSON.parse(ev.properties); }
                    catch { propsObj = { _raw: ev.properties }; }
                } else if (typeof ev.properties === 'object') {
                    propsObj = ev.properties;
                }
            }

            const clientTs = ev.clientTs != null ? Number(ev.clientTs) : null;

            // The ::jsonb cast is required because the Neon HTTP driver sends
            // parameters as strings (same pattern as game/save.js).
            await sql`
                INSERT INTO analytics_events (player_id, event_name, properties, client_ts)
                VALUES (
                    ${playerId},
                    ${eventName},
                    ${JSON.stringify(propsObj)}::jsonb,
                    ${Number.isFinite(clientTs) ? clientTs : null}
                )
            `;
            inserted++;
        }

        return res.status(200).json({ success: true, inserted });
    } catch (err) {
        console.error('[events/track] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
