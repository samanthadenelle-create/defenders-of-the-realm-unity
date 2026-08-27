// =============================================================================
// api/maintenance.js - Vercel Serverless Function (WO-1243)
// -----------------------------------------------------------------------------
// The COURTESY half of the operator kill switches: what the game client reads so
// it can put a rolling banner on the screen and refuse to open a sealed area.
//
// ⛔ THIS ENDPOINT IS NOT THE CONTROL. Read api/_lib/maintenance.js before
// changing anything here. Owner ruling 2026-08-27: "mine allows if we see
// someone finds a hack, we seal that area and patch" - a person exploiting the
// game runs whatever client they like, so a seal that only this endpoint
// announces stops nobody. The enforcement lives inside the endpoints where the
// exploited action lands (api/purchases/quote.js, api/game/save.js,
// api/leaderboard/submit.js). This one exists so honest players are TOLD.
//
// READ-ONLY and PUBLIC, no auth. Same three reasons as api/dungeon-status.js:
//   1. It must resolve before sign-in - a full `server` maintenance window has
//      to be announceable at the title screen, before any identity exists.
//   2. Nothing is disclosed: six area ids the client already ships, plus prose
//      the operator wrote to be read by players.
//   3. Auth would make every sign-in failure look like a maintenance window.
//
// WRITES go through tools/maintenance-toggle.mjs (DATABASE_URL, operator
// machine) which tools/command-centre.ps1 drives. No write surface is minted
// here - an endpoint that can seal the game is an endpoint worth attacking.
//
// UPDATE 2026-08-27 (WO-1244): a write surface now EXISTS, and it is deliberately
// NOT here. api/admin/ops.js seals and opens these toggles from the owner's phone,
// behind a SECOND secret (ADMIN_OPS_KEY, separate from ADMIN_DASH_KEY), POST-only,
// no CORS, fail-closed when that key is unset. The sentence above still holds for
// THIS file and must stay true of it - the point was never "no write may exist",
// it was "the public read endpoint must not also be the write endpoint". Pointed
// at from here so nobody reads this header, concludes no write path exists, and
// mints a second one.
//
// ⭐ CACHE-CONTROL IS AN EXPOSURE WINDOW, NOT A PERFORMANCE KNOB.
// Every second the edge serves a stale payload is a second an honest player is
// still being shown an area the owner has already sealed. s-maxage is therefore
// 10 s, not the 60 s api/dungeon-status.js uses - that system flips on a human
// timescale, this one flips while an exploit is running. stale-while-revalidate
// is kept SHORT for the same reason; it earns its place only because a Neon
// blip serving the last good payload is strictly better than a 500 (which,
// under the fail-open ruling, opens everything).
//
// FAIL-OPEN: on any error this returns 200 with `readOk: false` and NO closed
// areas, rather than a 500. The client cannot tell the difference from "nothing
// is sealed", which is exactly the ruling - see the header of _lib/maintenance.js
// for why, in the owner's own words.
//
// ⚠ vercel.json sets "git": { "deploymentEnabled": false } - PUSHING DOES NOT
//   DEPLOY. This endpoint stays dead until a deploy runs.
//
//   GET /api/maintenance
//   Reply: { ok, version, readOk, areas: { "<area>": { closed, message } } }
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { applyCors } = require('./_lib/http');
const { AREAS, readToggles } = require('./_lib/maintenance');

/** Payload schema version. The client parses forward-compatibly (see MaintenanceCatalog). */
const PAYLOAD_VERSION = 1;

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;

    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    res.setHeader('Cache-Control', 'public, max-age=0, s-maxage=10, stale-while-revalidate=30');

    let sql = null;
    try { sql = neon(process.env.DATABASE_URL); }
    catch (_) { sql = null; }

    // readToggles never throws and never rejects; ok=false is the fail-open branch.
    const state = await readToggles(sql);

    const areas = {};
    if (state.ok) {
        const serverRow = state.rows['server'];
        const serverClosed = !!(serverRow && serverRow.closed);
        for (const id of AREAS) {
            const row = state.rows[id];
            const ownClosed = !!(row && row.closed);
            // `server` closes everything. The client is told the effective state
            // AND which toggle did it, so the banner can name the right thing.
            const closed = ownClosed || (serverClosed && id !== 'server');
            areas[id] = {
                closed: closed,
                closedBy: closed ? (ownClosed ? id : 'server') : null,
                message: closed ? ((ownClosed ? (row && row.message) : (serverRow && serverRow.message)) || null) : null,
            };
        }
    }

    return res.status(200).json({
        ok: true,
        version: PAYLOAD_VERSION,
        readOk: state.ok === true,
        reason: state.reason,
        areas: areas,
    });
};
