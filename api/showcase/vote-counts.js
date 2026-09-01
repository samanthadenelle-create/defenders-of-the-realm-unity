'use strict';
const { neon } = require('@neondatabase/serverless');
const contest = require('../_lib/showcase-contest');

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const env = deps.env || process.env;
    return async (req, res) => {
        if (!contest.enabled(env)) return res.status(404).json({ success: false, error: 'NOT_FOUND' });
        if (req.method !== 'GET') return res.status(400).json({ error: 'METHOD_NOT_ALLOWED' });
        const id = req.query && String(req.query.contestId || '').trim();
        const categoryId = req.query && String(req.query.categoryId || '').trim();
        if (!contest.CONTEST_ID.test(id) || !contest.CATEGORY_ID.test(categoryId))
            return res.status(404).json({ success: false, error: 'NOT_FOUND' });
        try {
            const sql = getSql();
            const rows = await sql`
                SELECT cc.showcase_id, COUNT(v.voter_wallet)::bigint AS votes
                FROM showcase_contest_category_candidates cc
                JOIN showcase_contests c ON c.contest_id = cc.contest_id
                JOIN showcase_contest_categories cat
                  ON cat.contest_id = cc.contest_id AND cat.category_id = cc.category_id
                JOIN public_town_showcases sh ON sh.showcase_id = cc.showcase_id AND sh.published = TRUE
                LEFT JOIN showcase_contest_category_votes v
                  ON v.contest_id = cc.contest_id AND v.category_id = cc.category_id
                 AND v.showcase_id = cc.showcase_id
                WHERE cc.contest_id = ${id} AND cc.category_id = ${categoryId}
                  AND cat.active = TRUE AND cc.eligible = TRUE AND NOW() >= c.voting_ends_at
                GROUP BY cc.showcase_id ORDER BY votes DESC, cc.showcase_id ASC LIMIT 500
            `;
            return res.status(200).json({ success: true, contestId: id, categoryId,
                candidates: (rows || []).map(r => ({ showcaseId: r.showcase_id, votes: Number(r.votes) })) });
        } catch (_) { return res.status(500).json({ error: 'Internal server error' }); }
    };
}
module.exports = makeHandler();
module.exports._test = { makeHandler };
