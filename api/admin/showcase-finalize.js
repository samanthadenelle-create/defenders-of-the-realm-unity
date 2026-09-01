'use strict';
const { neon } = require('@neondatabase/serverless');
const { readRawBody } = require('../_lib/http');
const { keyOk, normalizeOperator } = require('../_lib/ops');
const contest = require('../_lib/showcase-contest');

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const env = deps.env || process.env;
    return async (req, res) => {
        res.setHeader('Cache-Control', 'no-store');
        if (!contest.enabled(env)) return res.status(404).json({ ok: false, code: 'NOT_FOUND' });
        if (req.method !== 'POST') return res.status(400).json({ ok: false, code: 'METHOD_NOT_ALLOWED' });
        if (!env.ADMIN_DASH_KEY || !env.ADMIN_OPS_KEY)
            return res.status(400).json({ ok: false, code: 'OPS_WRITE_NOT_CONFIGURED' });
        if (!keyOk(req.headers['x-admin-key'], env.ADMIN_DASH_KEY) ||
            !keyOk(req.headers['x-admin-ops-key'], env.ADMIN_OPS_KEY))
            return res.status(400).json({ ok: false, code: 'UNAUTHORIZED' });
        let body;
        try { body = req.body && typeof req.body === 'object' ? req.body :
            JSON.parse((await readRawBody(req, 4096)).toString('utf8')); }
        catch (_) { return res.status(400).json({ ok: false, code: 'BAD_BODY' }); }
        if (!body || Object.keys(body).some(k => !['contestId', 'by'].includes(k)) ||
            !contest.CONTEST_ID.test(String(body.contestId || '')))
            return res.status(400).json({ ok: false, code: 'BAD_BODY' });
        let operator;
        try { operator = normalizeOperator(body.by); }
        catch (_) { return res.status(400).json({ ok: false, code: 'BAD_OPERATOR' }); }
        try {
            const sql = getSql();
            const rows = await sql`
                WITH locked AS (
                    SELECT contest_id FROM showcase_contests
                    WHERE contest_id = ${body.contestId} AND voting_ends_at <= NOW()
                    FOR UPDATE
                ), ranked AS (
                    SELECT cc.contest_id, cc.category_id, sh.owner_wallet, cc.showcase_id,
                           cat.rules_version, cat.vote_weight,
                           COUNT(v.voter_wallet)::bigint AS vote_count,
                           ROW_NUMBER() OVER (PARTITION BY cc.category_id
                             ORDER BY COUNT(v.voter_wallet) DESC, cc.showcase_id ASC) AS place
                    FROM showcase_contest_category_candidates cc
                    JOIN locked l ON l.contest_id = cc.contest_id
                    JOIN showcase_contest_categories cat
                      ON cat.contest_id = cc.contest_id AND cat.category_id = cc.category_id
                    JOIN public_town_showcases sh ON sh.showcase_id = cc.showcase_id
                    LEFT JOIN showcase_contest_category_votes v
                      ON v.contest_id = cc.contest_id AND v.category_id = cc.category_id
                     AND v.showcase_id = cc.showcase_id
                    WHERE cc.eligible = TRUE AND cat.active = TRUE AND sh.published = TRUE
                    GROUP BY cc.contest_id, cc.category_id, sh.owner_wallet, cc.showcase_id,
                             cat.rules_version, cat.vote_weight
                ), runs AS (
                    INSERT INTO showcase_contest_result_runs
                        (contest_id, category_id, rules_version, finalized_by)
                    SELECT DISTINCT contest_id, category_id, rules_version, ${operator} FROM ranked
                    ON CONFLICT (contest_id, category_id) DO NOTHING
                    RETURNING result_id, contest_id, category_id
                ), result_rows AS (
                    INSERT INTO showcase_contest_result_rows
                        (result_id, showcase_id, placement, vote_count, weighted_score)
                    SELECT run.result_id, r.showcase_id, r.place, r.vote_count,
                           r.vote_count * r.vote_weight
                    FROM ranked r JOIN runs run USING (contest_id, category_id)
                    ON CONFLICT (result_id, showcase_id) DO NOTHING
                    RETURNING result_id
                ), grants AS (
                    INSERT INTO sku_entitlements
                        (wallet, sku, grant_id, source_kind, source_ref, expires_at, metadata)
                    SELECT r.owner_wallet, t.cosmetic_sku,
                           'community:' || ${body.contestId} || ':' || r.category_id || ':' || r.showcase_id || ':' || t.tier_id,
                           'community', ${body.contestId},
                           CASE WHEN t.duration_days IS NULL THEN NULL
                                ELSE NOW() + (t.duration_days * INTERVAL '1 day') END,
                           jsonb_build_object('contestId', ${body.contestId}, 'categoryId', r.category_id,
                                              'showcaseId', r.showcase_id, 'placement', r.place, 'tierId', t.tier_id,
                                              'expiryBehavior', ci.expiry_behavior,
                                              'fallbackSku', ci.fallback_sku)
                    FROM ranked r JOIN showcase_contest_category_reward_tiers t
                      ON t.contest_id = ${body.contestId} AND t.category_id = r.category_id
                     AND r.place BETWEEN t.placement_from AND t.placement_to
                    JOIN catalog_items ci ON ci.sku = t.cosmetic_sku AND ci.item_kind = 'cosmetic' AND ci.active = TRUE
                    WHERE t.duration_days IS NULL OR ci.expiry_behavior <> 'fallback' OR ci.fallback_sku IS NOT NULL
                    ON CONFLICT (grant_id) DO NOTHING RETURNING entitlement_id
                ), finished AS (
                    UPDATE showcase_contests SET finalized_at = COALESCE(finalized_at, NOW()),
                        finalized_by = COALESCE(finalized_by, ${operator})
                    WHERE contest_id IN (SELECT contest_id FROM locked)
                      AND EXISTS (SELECT 1 FROM showcase_contest_result_runs rr
                                  WHERE rr.contest_id = showcase_contests.contest_id)
                    RETURNING contest_id
                )
                SELECT (SELECT COUNT(*) FROM grants)::int AS grants,
                       EXISTS(SELECT 1 FROM finished) AS finalized
            `;
            if (!rows || !rows[0] || !rows[0].finalized)
                return res.status(400).json({ ok: false, code: 'NOT_READY' });
            return res.status(200).json({ ok: true, contestId: body.contestId,
                grantsCreated: Number(rows[0].grants), state: 'finalized' });
        } catch (_) { return res.status(500).json({ ok: false, code: 'SERVER_ERROR' }); }
    };
}
module.exports = makeHandler();
module.exports._test = { makeHandler };
