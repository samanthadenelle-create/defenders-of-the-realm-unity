'use strict';

const { neon } = require('@neondatabase/serverless');

const ALLOWED_METRICS = new Set(['highest_wave', 'longest_hold', 'total_resources', 'clan', 'arena']);
const PERIOD_RE = /^(alltime|\d{4}-W\d{2})$/;

function makeHandler(getSql = () => neon(process.env.DATABASE_URL)) {
    return async function handler(req, res) {
        if (req.method !== 'GET') return res.status(400).json({ error: 'Method not allowed' });
        const q = req.query || {};
        const metric = q.metric == null ? 'highest_wave' : String(q.metric).trim();
        const period = q.period == null ? 'alltime' : String(q.period).trim();
        if (!ALLOWED_METRICS.has(metric) || !PERIOD_RE.test(period))
            return res.status(400).json({ error: 'Invalid board' });
        try {
            const sql = getSql();
            const rows = await sql`
                WITH ranked AS (
                    SELECT ROW_NUMBER() OVER (
                               ORDER BY s.score DESC, s.updated_at ASC, s.wallet ASC
                           ) AS rank,
                           s.wallet, p.username, s.score
                    FROM leaderboard_scores s
                    LEFT JOIN player_profiles p ON p.wallet = s.wallet
                    WHERE s.metric = ${metric} AND s.period_id = ${period}
                ), top_ten AS (
                    SELECT * FROM ranked WHERE rank <= 10
                )
                SELECT t.rank, t.username, t.score,
                       CASE WHEN sh.published THEN sh.showcase_id ELSE NULL END AS showcase_id
                FROM top_ten t
                LEFT JOIN public_town_showcases sh ON sh.owner_wallet = t.wallet
                ORDER BY t.rank ASC
            `;
            const top = (rows || []).map(row => ({
                rank: Number(row.rank), username: row.username || null,
                score: Number(row.score), showcaseId: row.showcase_id || null,
            }));
            return res.status(200).json({ success: true, metric, period, top });
        } catch (_) { return res.status(500).json({ error: 'Internal server error' }); }
    };
}

module.exports = makeHandler();
module.exports._test = { makeHandler, ALLOWED_METRICS, PERIOD_RE };
