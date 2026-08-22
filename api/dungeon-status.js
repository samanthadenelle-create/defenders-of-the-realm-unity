// =============================================================================
// api/dungeon-status.js — Vercel Serverless Function (WO-1114 §5, §7b)
// -----------------------------------------------------------------------------
// Returns the door state of every dungeon, so content can be opened and closed
// WITHOUT a client build. That matters doubly on the Solana dApp Store, where a
// client change otherwise costs a review cycle.
//
// READ-ONLY and PUBLIC, no auth — deliberately, and the reasoning is load-bearing:
//   1. The status must resolve BEFORE sign-in (it is read at the title screen,
//      before any wallet or Firebase identity exists).
//   2. Nothing is disclosed. The reply is four dungeon ids the client already
//      ships in DungeonWorldPortalSpawner, plus prose written to be read by
//      players. No player datum, no key, no economy value.
//   3. Auth would INVERT the safety property: an auth-gated status call fails for
//      offline and guest players, and a fail-closed reading of that failure locks
//      content — the exact outcome WO-1114 §6 forbids. Public read means the only
//      failure mode is "the client falls back to open", which is the safe way to
//      fail. (Precedent: api/leaderboard/get.js, public read, same reasoning.)
// WRITES are admin-only and go through the existing api/admin/db.js path
//   (X-Admin-Key). No new auth surface is minted here.
//
// ⛔ THE COPY RULE TRAVELS WITH THE DATA: a closed dungeon must read as WORLD,
//   never as build status. headline/body are AUTHORED PROSE — never "under
//   construction", "coming soon", "WIP" or any dev vocabulary. The client-side
//   oracle (Assets/Editor/Regression/DungeonStatusRegression.cs, case
//   [door-copy]) cannot see rows written here, which is why api/schema.sql also
//   pins the status enum with a CHECK constraint and says the rule out loud.
//
// Client : DeNelle.Core.World.DungeonStatusService (public GET, no headers).
//   GET   /api/dungeon-status
//   Reply : { success: true, version: 1, dungeons: { "<id>": { status, headline?, body?, sigil? } } }
//
// An EMPTY table is a correct, healthy answer: absence means open. The client
// treats a missing id, an unknown status string and an unreachable server all as
// OPEN — nothing this endpoint can do or fail to do may lock a player out.
//
// ⚠ vercel.json sets "git": { "deploymentEnabled": false } — PUSHING DOES NOT
//   DEPLOY. This endpoint stays dead until someone runs `vercel --prod` by hand.
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { applyCors } = require('./_lib/http');

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;   // 204 preflight already answered

    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    // Short edge cache: a flip propagates in about a minute with no client change,
    // which is acceptance criterion 3. stale-while-revalidate is the important half —
    // if Neon hiccups the edge keeps serving the last good payload instead of a 500,
    // one more layer between a backend wobble and a door that will not open.
    res.setHeader('Cache-Control', 'public, max-age=60, s-maxage=60, stale-while-revalidate=300');

    try {
        const sql = neon(process.env.DATABASE_URL);
        const rows = await sql`
            SELECT dungeon_id, status, headline, body, sigil
            FROM dungeon_status`;

        const dungeons = {};
        for (const r of rows) {
            const entry = { status: r.status };
            if (r.headline) entry.headline = r.headline;
            if (r.body) entry.body = r.body;
            if (r.sigil) entry.sigil = r.sigil;
            dungeons[r.dungeon_id] = entry;
        }

        return res.status(200).json({ success: true, version: 1, dungeons: dungeons });
    } catch (err) {
        console.error('[dungeon-status] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
