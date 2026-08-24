// =============================================================================
// api/admin/schema-shape.js — WHAT THE DEPLOYED DATABASE ACTUALLY LOOKS LIKE.
// -----------------------------------------------------------------------------
// Returns the live tables, columns and CHECK constraints. It does NOT judge them:
// the comparison against api/schema.sql happens in tools/schema-parity.mjs, which
// has the repo. This endpoint has the database. Neither needs a copy of the other,
// which is the whole point — an embedded expected-shape would be one more fact
// written twice (WO-1170), and this gate exists precisely because duplicated facts
// drift.
//
// ⛔ WHY IT EXISTS AT ALL (WO-1173). On 2026-08-24 the deployed database drifted
// from api/schema.sql FIVE times, each found only when something tripped over it:
// dungeon_status (missing), auth_sessions (missing), purchase_quotes (missing),
// purchase_entitlements (OLD VERSION — a real 391 SKR payment settled and could
// not be recorded), and bug_reports (old version). Every other gate was GREEN
// throughout: COMPILE_GATE_OK, REGRESSION_OK, R2_PARITY_OK all validate the
// ARTIFACT and none of them looks at the database the artifact talks to.
//
// ⚠ AND THE MONEY PATH FAILS AT THE WORST MOMENT BY CONSTRUCTION:
// /api/purchases/verify runs AFTER the transfer settles, so a schema fault there
// is discovered with the money already gone and no refund route on an SPL
// transfer. The chain settles first, always. There is no ordering fix — the
// schema has to be right BEFORE the first transaction, which means a gate.
//
// READ-ONLY BY CONSTRUCTION, same contract as db.js/stats.js: every statement is
// a SELECT against information_schema / pg_catalog. Key-gated by ADMIN_DASH_KEY.
//
//   GET /api/admin/schema-shape        (header: x-admin-key)
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { applyCors, newRef, quietFail } = require('../_lib/http');
const { AuthCode } = require('../_lib/wallet-auth');

function adminKeyOk(given, expected) {
    if (!expected || !given) return false;
    const a = Buffer.from(String(given));
    const b = Buffer.from(String(expected));
    if (a.length !== b.length) return false;
    try { return require('crypto').timingSafeEqual(a, b); } catch (_) { return false; }
}

module.exports = async function handler(req, res) {
    if (applyCors(req, res, 'GET, OPTIONS')) return;
    const ref = newRef();

    if (req.method !== 'GET') return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);

    const key = (req.headers && (req.headers['x-admin-key'] || req.headers['X-Admin-Key'])) || '';
    if (!adminKeyOk(key, process.env.ADMIN_DASH_KEY)) {
        return quietFail(res, 401, AuthCode.UNAUTHORIZED || 'AUTH_UNAUTHORIZED', ref);
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error(`[admin/schema-shape] ref=${ref} step=db-connect FAILED:`,
            err && err.message ? err.message : err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    try {
        // Columns, per public table.
        const cols = await sql`
            SELECT table_name, column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
            ORDER BY table_name, ordinal_position
            LIMIT 5000`;

        // CHECK constraints, as Postgres rewrites them.
        // ⚠ The COMPARISON must parse value SETS out of these, never compare the
        // text: Postgres rewrites `IN ('a','b')` as `= ANY (ARRAY['a'::text,...])`,
        // so a string compare would false-alarm on every constraint — and a gate
        // that cries wolf is one people start ignoring, which is worse than none.
        const checks = await sql`
            SELECT rel.relname AS table_name,
                   con.conname  AS constraint_name,
                   pg_get_constraintdef(con.oid) AS definition
            FROM pg_constraint con
            JOIN pg_class rel      ON rel.oid = con.conrelid
            JOIN pg_namespace nsp  ON nsp.oid = rel.relnamespace
            WHERE con.contype = 'c' AND nsp.nspname = 'public'
            ORDER BY rel.relname, con.conname
            LIMIT 2000`;

        const tables = {};
        for (const r of cols) {
            if (!tables[r.table_name]) tables[r.table_name] = { columns: {}, checks: {} };
            tables[r.table_name].columns[r.column_name] = {
                type: r.data_type, nullable: r.is_nullable === 'YES',
            };
        }
        for (const r of checks) {
            if (!tables[r.table_name]) continue;
            tables[r.table_name].checks[r.constraint_name] = r.definition;
        }

        return res.status(200).json({
            ok: true,
            generated_at: new Date().toISOString(),
            table_count: Object.keys(tables).length,
            tables,
            note: 'Deployed shape only. Compare against api/schema.sql with ' +
                  'tools/schema-parity.mjs --shape <file>. This endpoint judges nothing.',
        });
    } catch (err) {
        console.error(`[admin/schema-shape] ref=${ref} step=read FAILED:`,
            err && err.message ? err.message : err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
};
