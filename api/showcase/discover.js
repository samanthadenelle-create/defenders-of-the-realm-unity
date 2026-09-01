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
        res.setHeader('Cache-Control', 'private, no-store');
        if (!contest.enabled(env)) return res.status(404).json({ success:false, error:'NOT_FOUND' });
        if (req.method !== 'POST') return res.status(400).json({ success:false, error:'METHOD_NOT_ALLOWED' });
        let body;
        try { body = JSON.parse(((await readBodyExact(req, WALLET_MAX_BODY_BYTES)).buffer).toString('utf8')); }
        catch (_) { return res.status(400).json({ success:false, error:'BAD_PAYLOAD' }); }
        const value = contest.validateDiscovery(body);
        if (!value) return res.status(400).json({ success:false, error:'BAD_PAYLOAD' });
        const sql = getSql();
        const proven = await auth(sql, String(req.headers['x-session'] || ''), value.playerId);
        if (!proven.ok) return res.status(401).json({ success:false, error:'AUTH_REQUIRED' });
        try {
            const rows = await sql`
                SELECT cc.showcase_id
                FROM showcase_contest_category_candidates cc
                JOIN showcase_contests c ON c.contest_id = cc.contest_id
                JOIN showcase_contest_categories cat
                  ON cat.contest_id = cc.contest_id AND cat.category_id = cc.category_id
                JOIN public_town_showcases sh ON sh.showcase_id = cc.showcase_id
                WHERE cc.contest_id = ${value.contestId} AND cc.category_id = ${value.categoryId}
                  AND cat.active = TRUE AND cc.eligible = TRUE AND sh.published = TRUE
                  AND sh.owner_wallet <> ${value.playerId}
                  AND NOW() >= c.starts_at AND NOW() < c.voting_ends_at
                  AND c.finalized_at IS NULL
                ORDER BY md5(cc.showcase_id || ':' || cat.discovery_salt || ':' || ${value.playerId})
                LIMIT 100`;
            return res.status(200).json({ success:true, contestId:value.contestId,
                categoryId:value.categoryId, candidates:(rows || []).map(r=>({showcaseId:r.showcase_id})) });
        } catch (_) { return res.status(500).json({ error:'Internal server error' }); }
    };
}
module.exports = makeHandler();
module.exports.config = { api:{ bodyParser:false } };
module.exports._test = { makeHandler };
