'use strict';
const { neon } = require('@neondatabase/serverless');
const { verifySession, WALLET_MAX_BODY_BYTES } = require('../_lib/wallet-auth');
const { applyCors, readBodyExact } = require('../_lib/http');
const contest = require('../_lib/showcase-contest');

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const auth = deps.verifySession || verifySession;
    const env = deps.env || process.env;
    return async (req, res) => {
        if (applyCors(req, res, 'POST, OPTIONS')) return;
        if (!contest.enabled(env)) return res.status(404).json({ success: false, error: 'NOT_FOUND' });
        if (req.method !== 'POST') return res.status(400).json({ success: false, error: 'METHOD_NOT_ALLOWED' });
        let body;
        try { body = JSON.parse(((await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer).toString('utf8')); }
        catch (_) { return res.status(400).json({ success: false, error: 'BAD_PAYLOAD' }); }
        const value = contest.validateVote(body);
        if (!value) return res.status(400).json({ success: false, error: 'BAD_PAYLOAD' });
        const sql = getSql();
        const proven = await auth(sql, String(req.headers['x-session'] || ''), value.playerId);
        if (!proven.ok) return res.status(401).json({ success: false, error: 'AUTH_REQUIRED' });
        try {
            const rows = await sql`
                WITH eligible AS (
                    SELECT c.contest_id, cat.category_id, cc.showcase_id
                    FROM showcase_contests c
                    JOIN showcase_contest_categories cat ON cat.contest_id = c.contest_id
                    JOIN showcase_contest_category_candidates cc
                      ON cc.contest_id = cat.contest_id AND cc.category_id = cat.category_id
                    JOIN public_town_showcases sh ON sh.showcase_id = cc.showcase_id
                    WHERE c.contest_id = ${value.contestId}
                      AND NOW() >= c.starts_at AND NOW() < c.voting_ends_at
                      AND c.finalized_at IS NULL AND cat.active = TRUE
                      AND cc.eligible = TRUE AND sh.published = TRUE
                      AND cat.category_id = ${value.categoryId}
                      AND cc.showcase_id = ${value.showcaseId}
                      AND sh.owner_wallet <> ${value.playerId}
                ), inserted AS (
                    INSERT INTO showcase_contest_category_votes
                        (contest_id, category_id, voter_wallet, showcase_id)
                    SELECT contest_id, category_id, ${value.playerId}, showcase_id FROM eligible
                    ON CONFLICT (contest_id, category_id, voter_wallet) DO NOTHING
                    RETURNING showcase_id
                )
                SELECT 'cast' AS state, showcase_id FROM inserted
                UNION ALL
                SELECT CASE WHEN v.showcase_id = ${value.showcaseId} THEN 'already_cast' ELSE 'choice_locked' END,
                       v.showcase_id
                FROM showcase_contest_category_votes v
                WHERE v.contest_id = ${value.contestId} AND v.category_id = ${value.categoryId}
                  AND v.voter_wallet = ${value.playerId}
                  AND NOT EXISTS (SELECT 1 FROM inserted)
                LIMIT 1
            `;
            if (!rows || rows.length === 0) return res.status(400).json({ success: false, error: 'INELIGIBLE' });
            if (rows[0].state === 'choice_locked')
                return res.status(400).json({ success: false, error: 'VOTE_IMMUTABLE' });
            return res.status(200).json({ success: true, state: rows[0].state,
                categoryId: value.categoryId, showcaseId: value.showcaseId });
        } catch (_) { return res.status(500).json({ error: 'Internal server error' }); }
    };
}
module.exports = makeHandler();
module.exports.config = { api: { bodyParser: false } };
module.exports._test = { makeHandler };
