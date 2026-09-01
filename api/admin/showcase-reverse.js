'use strict';
const { neon } = require('@neondatabase/serverless');
const { readRawBody } = require('../_lib/http');
const { keyOk, normalizeOperator } = require('../_lib/ops');
const contest = require('../_lib/showcase-contest');

function makeHandler(deps = {}) {
    const getSql = deps.getSql || (() => neon(process.env.DATABASE_URL));
    const env = deps.env || process.env;
    return async (req, res) => {
        res.setHeader('Cache-Control','no-store');
        if (!contest.enabled(env)) return res.status(404).json({ok:false,code:'NOT_FOUND'});
        if (req.method !== 'POST') return res.status(400).json({ok:false,code:'METHOD_NOT_ALLOWED'});
        if (!env.ADMIN_DASH_KEY || !env.ADMIN_OPS_KEY) return res.status(400).json({ok:false,code:'OPS_WRITE_NOT_CONFIGURED'});
        if (!keyOk(req.headers['x-admin-key'],env.ADMIN_DASH_KEY) ||
            !keyOk(req.headers['x-admin-ops-key'],env.ADMIN_OPS_KEY))
            return res.status(400).json({ok:false,code:'UNAUTHORIZED'});
        let body;
        try { body=req.body&&typeof req.body==='object'?req.body:JSON.parse((await readRawBody(req,4096)).toString('utf8')); }
        catch (_) { return res.status(400).json({ok:false,code:'BAD_BODY'}); }
        if (!body || Object.keys(body).some(k=>!['contestId','categoryId','by','reason'].includes(k)) ||
            !contest.CONTEST_ID.test(String(body.contestId||'')) ||
            !contest.CATEGORY_ID.test(String(body.categoryId||'')) ||
            typeof body.reason !== 'string' || body.reason.trim().length < 3 || body.reason.trim().length > 500)
            return res.status(400).json({ok:false,code:'BAD_BODY'});
        let operator; try { operator=normalizeOperator(body.by); }
        catch (_) { return res.status(400).json({ok:false,code:'BAD_OPERATOR'}); }
        try {
            const sql=getSql();
            const rows=await sql`
                WITH target AS (
                    SELECT result_id FROM showcase_contest_result_runs
                    WHERE contest_id=${body.contestId} AND category_id=${body.categoryId}
                    FOR UPDATE
                ), reversal AS (
                    INSERT INTO showcase_contest_result_reversals (result_id,reversed_by,reason)
                    SELECT result_id,${operator},${body.reason.trim()} FROM target
                    ON CONFLICT (result_id) DO NOTHING RETURNING result_id
                ), revoked AS (
                    UPDATE sku_entitlements SET state='revoked', revoked_at=NOW(),
                        revoke_reason=${'community result reversed: '+body.reason.trim()}, updated_at=NOW()
                    WHERE source_kind='community' AND source_ref=${body.contestId}
                      AND metadata->>'categoryId'=${body.categoryId}
                      AND state='active' AND EXISTS (SELECT 1 FROM reversal)
                    RETURNING entitlement_id
                )
                SELECT EXISTS(SELECT 1 FROM target) AS found,
                       EXISTS(SELECT 1 FROM reversal) AS reversed,
                       (SELECT COUNT(*) FROM revoked)::int AS revoked`;
            if (!rows||!rows[0]||!rows[0].found) return res.status(400).json({ok:false,code:'NOT_FOUND'});
            return res.status(200).json({ok:true,contestId:body.contestId,categoryId:body.categoryId,
                state:rows[0].reversed?'reversed':'already_reversed',entitlementsRevoked:Number(rows[0].revoked||0)});
        } catch (_) { return res.status(500).json({ok:false,code:'SERVER_ERROR'}); }
    };
}
module.exports=makeHandler();
module.exports._test={makeHandler};
