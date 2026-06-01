// =============================================================================
// api/profile/get.js — Vercel Serverless Function (WO-129 §2.2)
// -----------------------------------------------------------------------------
// Reads a player's public profile: username, avatar/hero, headline stats, and
// any socials the player chose to make public. PUBLIC + read-only (a profile is
// the public identity on the leaderboard). Private fields (non-public socials)
// are never returned here.
//
// Headline stats are derived live from leaderboard_scores (the all-time bests),
// so they always match the boards — no duplicate stat store to drift.
//
// Client : ProfileService (Core) — WO-129 §4 (NEW).
//   GET   /api/profile?wallet=<addr>
//   Reply (player has a profile row):
//     { success:true, wallet, username, avatarId,
//       socials: { x:{handle}, ... },              // PUBLIC socials only
//       stats: { highestWave, longestHold, totalResources },  // all-time bests
//       createdAt, renamedAt }
//   Reply (no profile row yet): 404 { success:false, error:'PROFILE_NOT_FOUND' }
//     (the client shows a derived "Defender#<short>" default until the player
//      sets a username — that default is NOT stored server-side, WO-129 §2.2).
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 404 | 500
// =============================================================================

const { neon } = require('@neondatabase/serverless');

// All-time headline boards surfaced on the profile (WO-129 §2.2 "headline stats").
const STAT_METRICS = {
    highestWave: 'highest_wave',
    longestHold: 'longest_hold',
    totalResources: 'total_resources',
};

module.exports = async (req, res) => {
    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    const wallet = req.query && req.query.wallet != null ? String(req.query.wallet).trim() : '';
    if (!wallet) {
        return res.status(400).json({ error: 'Missing wallet' });
    }

    try {
        const sql = neon(process.env.DATABASE_URL);

        const profRows = await sql`
            SELECT wallet, username, avatar_id, social_links, created_at, renamed_at
            FROM player_profiles
            WHERE wallet = ${wallet}
            LIMIT 1
        `;
        if (profRows.length === 0) {
            return res.status(404).json({ success: false, error: 'PROFILE_NOT_FOUND' });
        }
        const p = profRows[0];

        // Headline stats = the player's all-time best on each headline board.
        const statRows = await sql`
            SELECT metric, score
            FROM leaderboard_scores
            WHERE wallet = ${wallet} AND period_id = 'alltime'
              AND metric IN ('highest_wave', 'longest_hold', 'total_resources')
        `;
        const byMetric = {};
        for (const r of statRows) byMetric[r.metric] = Number(r.score);

        const stats = {};
        for (const [outKey, metric] of Object.entries(STAT_METRICS)) {
            stats[outKey] = byMetric[metric] ?? 0;
        }

        // Surface only socials the player opted to show publicly.
        const socials = {};
        const links = p.social_links && typeof p.social_links === 'object' ? p.social_links : {};
        for (const [provider, link] of Object.entries(links)) {
            if (link && typeof link === 'object' && link.public === true && link.handle) {
                socials[provider] = { handle: link.handle };
            }
        }

        return res.status(200).json({
            success: true,
            wallet: p.wallet,
            username: p.username ?? null,
            avatarId: p.avatar_id ?? null,
            socials,
            stats,
            createdAt: p.created_at,
            renamedAt: p.renamed_at ?? null,
        });
    } catch (err) {
        console.error('[profile/get] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
