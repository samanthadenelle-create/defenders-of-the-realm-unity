// =============================================================================
// api/patronage/benefactors.js -- GET the Benefactors of the Realm wall.
// WO-1073, owner ruling 2026-08-27.
// -----------------------------------------------------------------------------
// PUBLIC and UNAUTHENTICATED, on purpose and by requirement: "every kingdom can
// see it". Auth here would make the wall per-player, which is the defect the
// ruling exists to fix. There is nothing private to protect because there is
// nothing private in the response.
//
//   GET /api/patronage/benefactors?limit=<n>
//     limit  OPTIONAL, clamped 1..WALL_MAX_ROWS (default WALL_DEFAULT_ROWS).
//   Reply : {
//     success: true,
//     tier: 'founder_benefactor',
//     count: <n>,
//     benefactors: [ { ordinal, patronName, foundedOn,
//                      monumentAssetId, monumentIsBespoke }, ... ]
//   }
//
// monumentAssetId is PER PATRON (owner ruling 2026-08-27: each Founder's
// monument is a custom FBX the owner creates WITH them, one-on-one). A patron
// with no bespoke asset yet resolves to the shared stand-in, so the client
// always has something to place and the tier can be switched on before any
// collaboration finishes. Founder A can be standing beside their real monument
// while Founder B is still on the stand-in -- it is per patron, never a global
// phase, so do NOT collapse this to one flag on the client.
//
// WHAT IS DELIBERATELY ABSENT FROM THAT REPLY, and must stay absent:
//   * wallet / any account identity  -- the name is a chosen alias, stored
//     BESIDE the entitlement; the address never leaves the database.
//   * any dollar figure              -- WO-1073 section 4: show the TIER, never
//     the amount. The wall says WHO, never HOW MUCH.
//   * $50 Patron and $150 High Patron rows -- ruled out ("Do NOT list $50 or
//     $150"). The library filters on tier_id and the table's CHECK constraint
//     accepts only the founder id, so this is enforced twice.
//
// A database that is unreachable returns an EMPTY wall with success:true rather
// than a 500. The wall is an ornament on someone else's kingdom; a founder's
// honour roll failing loudly into a player's face is worse than it being briefly
// absent, and the client must render "no benefactors yet" correctly regardless
// (it is the true state on day one).
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400   (public read -- never 401/404)
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { applyCors } = require('../_lib/http');
const { FOUNDER_TIER_ID, readBenefactorWall } = require('../_lib/benefactors');

const EMPTY_WALL = { success: true, tier: FOUNDER_TIER_ID, count: 0, benefactors: [] };

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;
    if (req.method !== 'GET') {
        return res.status(400).json({ success: false, error: 'Method not allowed' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[patronage/benefactors] DB init error:', err && err.message);
        return res.status(200).json(EMPTY_WALL);
    }

    try {
        const limit = req.query ? req.query.limit : undefined;
        const wall = await readBenefactorWall(sql, limit);
        return res.status(200).json({
            success: true,
            tier: wall.tierId,
            count: wall.count,
            benefactors: wall.benefactors,
        });
    } catch (err) {
        // Never echo the driver's message: it can carry the connection string.
        console.error('[patronage/benefactors] read error:', err && err.code);
        return res.status(200).json(EMPTY_WALL);
    }
};

module.exports._test = { EMPTY_WALL };
