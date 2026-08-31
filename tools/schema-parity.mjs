// =============================================================================
// schema-parity.mjs — does the DEPLOYED database match api/schema.sql?
// -----------------------------------------------------------------------------
// ⛔ WHY THIS EXISTS (WO-1173). On 2026-08-24 the deployed database drifted from
// api/schema.sql FOUR times, each found only when something tripped over it:
//    1. dungeon_status          MISSING  -> a 500 in the log
//    2. auth_sessions           MISSING  -> the signed handshake 500d; EVERY wallet
//                                           save had never been written
//    3. purchase_quotes         MISSING  -> the quote rail could never have run
//    4. purchase_entitlements   OLD      -> ⛔ a real 391 SKR payment settled and
//                                           could not be recorded
//
// Drift 4 is the one that matters: the table EXISTED, so every "does it exist"
// check passed. It was missing four columns AND its network CHECK predated
// mainnet ('devnet','mainnet' vs the declared 'devnet','mainnet','mainnet-beta').
//
// ⚠ AND IT FAILS AT THE WORST MOMENT BY CONSTRUCTION: /api/purchases/verify runs
// AFTER the transfer settles, so every schema fault on that path is discovered
// with the money already gone and no refund route on an SPL transfer. The chain
// settles first, always — so the schema must be right BEFORE the first
// transaction. That means a gate, not vigilance.
//
// ⭐ EVERY OTHER GATE WAS GREEN ALL DAY. COMPILE_GATE_OK, REGRESSION_OK,
// R2_PARITY_OK, CATALOG_FALLBACK_GEN_OK all validate the ARTIFACT. None looks at
// the DATABASE the artifact talks to. This is the §16 bundle-parity shape one
// layer down: there a build runs perfectly with capsule enemies because bundles
// were never pushed; here a build runs perfectly and TAKES MONEY because a column
// was never added.
//
// Judge by the MARKER on a fresh log, never the exit code (CLAUDE.md §8).
//
//   node tools/schema-parity.mjs                 # needs DATABASE_URL in env
//   node tools/schema-parity.mjs --expected-only # parse schema.sql, no DB needed
//
// Emits SCHEMA_PARITY_OK, or SCHEMA_PARITY_FAIL with the exact diff.
// =============================================================================

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const SCHEMA_PATH = join(HERE, '..', 'api', 'schema.sql');

// ── Parse api/schema.sql — the DECLARATION ──────────────────────────────────
// Deliberately a narrow parser over our own file, not a general SQL parser: it
// only has to understand the subset we actually write, and a wrong answer here
// is worse than no answer, so anything it cannot parse it reports rather than
// guesses at.
function parseSchema(sql) {
    const tables = new Map();

    const tableRe = /CREATE TABLE IF NOT EXISTS\s+([a-z_]+)\s*\(([\s\S]*?)\n\);/g;
    let m;
    while ((m = tableRe.exec(sql)) !== null) {
        const name = m[1];
        const body = m[2];
        const columns = new Map();
        const checks = new Map();   // column -> Set(allowed values), for IN (...) checks only

        for (let raw of body.split('\n')) {
            const line = raw.replace(/--.*$/, '').trim();
            if (!line) continue;
            // A column definition starts with an identifier followed by a type.
            const col = /^([a-z_]+)\s+([A-Za-z][A-Za-z0-9 (),]*)/.exec(line);
            if (col && !/^(CONSTRAINT|PRIMARY|UNIQUE|FOREIGN|CHECK)\b/i.test(line)) {
                columns.set(col[1], col[2].trim());
            }
            // CHECK (col IN ('a','b')) — the constraint class that silently
            // rejected a valid row today.
            const chk = /CHECK\s*\(\s*([a-z_]+)\s+IN\s*\(([^)]*)\)\s*\)/i.exec(line);
            if (chk) {
                const vals = chk[2].split(',')
                    .map(v => v.trim().replace(/^'|'$/g, ''))
                    .filter(Boolean);
                checks.set(chk[1], new Set(vals));
            }
        }
        tables.set(name, { columns, checks });
    }
    return tables;
}

// ── Compare against the LIVE database ───────────────────────────────────────
async function readDeployed(sql, tableNames) {
    const live = new Map();

    const cols = await sql`
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'`;
    for (const r of cols) {
        if (!live.has(r.table_name)) live.set(r.table_name, { columns: new Set(), checks: new Map() });
        live.get(r.table_name).columns.add(r.column_name);
    }

    // ⚠ COMPARE VALUE SETS, NOT CONSTRAINT TEXT. Postgres rewrites
    // `IN ('a','b')` as `= ANY (ARRAY['a'::text,'b'::text])`, so a string compare
    // would false-alarm on every constraint and the gate would start being ignored
    // — which is worse than not having it.
    const cons = await sql`
        SELECT rel.relname AS table_name, pg_get_constraintdef(c.oid) AS def
        FROM pg_constraint c
        JOIN pg_class rel ON rel.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = rel.relnamespace
        WHERE c.contype = 'c' AND n.nspname = 'public'`;
    for (const r of cons) {
        if (!live.has(r.table_name)) continue;
        // TWO renderings, and missing the second one made this gate lie.
        //
        //   CHECK (col IN ('a','b'))  -> pg renders  col = ANY (ARRAY['a'::text, ...])
        //   CHECK (col IN ('a'))      -> pg SIMPLIFIES a one-element IN to  col = 'a'::text
        //
        // Only the first was matched, so every SINGLE-VALUE check read as
        // "CHECK MISSING" no matter how correctly it was defined. That hid
        // purchase_quotes.currency IN ('SKR') and purchase_entitlements.rail IN
        // ('solana') - BOTH on the money path, and both reported as drift while
        // the database was right. A gate that cannot see a correct constraint
        // sends people to "repair" a thing that is not broken.
        let col = null;
        let vals = null;

        // Match only a WHOLE enum constraint. A compound relational check such as
        // `(scope='account' AND cardinality(categories)=0) OR ...` also contains
        // enum-looking fragments; accepting its first fragment overwrote the real
        // scope constraint and falsely reported the live DB as narrower.
        const many = /^CHECK\s*\(\(+([a-z_]+)\s*=\s*ANY\s*\(ARRAY\[([^\]]*)\]\)\)+\)$/i.exec(r.def);
        if (many) {
            col = many[1];
            vals = many[2].split(',')
                .map(v => v.trim().replace(/::text/g, '').replace(/^'|'$/g, ''))
                .filter(Boolean);
        } else {
            const one = /^CHECK\s*\(\(+([a-z_]+)\s*=\s*'([^']*)'(?:::[a-z ]+)?\)+\)$/i.exec(r.def);
            if (one) { col = one[1]; vals = [one[2]]; }
        }

        if (!col || !vals || !vals.length) continue;
        live.get(r.table_name).checks.set(col, new Set(vals));
    }
    return live;
}

function compare(expected, live) {
    const problems = [];
    for (const [table, want] of expected) {
        const have = live.get(table);
        if (!have) { problems.push(`TABLE MISSING: ${table}`); continue; }

        for (const col of want.columns.keys()) {
            if (!have.columns.has(col)) problems.push(`COLUMN MISSING: ${table}.${col}`);
        }

        for (const [col, wantVals] of want.checks) {
            const haveVals = have.checks.get(col);
            if (!haveVals) { problems.push(`CHECK MISSING: ${table}.${col}`); continue; }
            // ⛔ NARROWER is the dangerous direction: it silently REJECTS valid rows,
            // which is precisely how a settled 391 SKR payment went unrecorded.
            const missing = [...wantVals].filter(v => !haveVals.has(v));
            if (missing.length) {
                problems.push(
                    `CHECK NARROWER: ${table}.${col} rejects ${JSON.stringify(missing)} ` +
                    `(deployed allows ${JSON.stringify([...haveVals])})`);
            }
            const extra = [...haveVals].filter(v => !wantVals.has(v));
            if (extra.length) {
                problems.push(`CHECK WIDER (report only): ${table}.${col} also allows ${JSON.stringify(extra)}`);
            }
        }
    }
    return problems;
}

// ── Run ─────────────────────────────────────────────────────────────────────
const expected = parseSchema(readFileSync(SCHEMA_PATH, 'utf8'));

if (process.argv.includes('--expected-only')) {
    console.log(`parsed ${expected.size} table(s) from api/schema.sql`);
    for (const [t, v] of expected) {
        const checks = [...v.checks.entries()].map(([c, s]) => `${c}{${[...s].join('|')}}`);
        console.log(`  ${t}: ${v.columns.size} column(s)` + (checks.length ? `  checks: ${checks.join(' ')}` : ''));
    }
    console.log('SCHEMA_PARSE_OK');
    process.exit(0);
}

// DATABASE_URL comes from the environment when there is one, and otherwise from
// .env.local, which is where this repo actually keeps it.
//
// This fallback is NOT a softening of the gate — it is what makes the gate
// RUNNABLE AT ALL. .githooks/pre-push invokes this tool with a bare environment,
// so before the fallback existed EVERY api/schema.sql change was blocked with
// "no DATABASE_URL in env", and the only way through was for a human to export a
// secret by hand at the exact moment they were being told no. CLAUDE.md §16 names
// that shape: a gate whose remedy is "a human remembers a second command" is not
// a gate, it is a speed bump people learn to route around — and the routing
// around is what eventually ships the unverified schema.
//
// The check itself is UNCHANGED. Parity is still proven against the LIVE database
// and must still print SCHEMA_PARITY_OK. The value is never printed, logged, or
// echoed into an error message.
function resolveDatabaseUrl() {
    if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
    try {
        const text = readFileSync(join(HERE, '..', '.env.local'), 'utf8');
        for (const line of text.split(/\r?\n/)) {
            const m = line.match(/^\s*DATABASE_URL\s*=\s*(.*)$/);
            if (!m) continue;
            let v = m[1].trim();
            if ((v.startsWith('"') && v.endsWith('"')) ||
                (v.startsWith("'") && v.endsWith("'"))) {
                v = v.slice(1, -1);
            }
            if (v) return v;
        }
    } catch { /* absent or unreadable — fall through to the honest failure below */ }
    return null;
}

const url = resolveDatabaseUrl();
if (!url) {
    console.error('SCHEMA_PARITY_FAIL no DATABASE_URL in env and none in .env.local. ' +
                  'Run with --expected-only to check the parser without a database.');
    process.exit(1);
}

const { neon } = await import('@neondatabase/serverless');
const sql = neon(url);
const live = await readDeployed(sql, [...expected.keys()]);
const problems = compare(expected, live);

const blocking = problems.filter(p => !p.startsWith('CHECK WIDER'));
for (const p of problems) console.log((blocking.includes(p) ? '  FAIL  ' : '  note  ') + p);

if (blocking.length) {
    console.error(`SCHEMA_PARITY_FAIL ${blocking.length} problem(s) — the deployed database does ` +
                  `not match api/schema.sql. ⛔ Do NOT ship: /verify runs AFTER settlement, so a ` +
                  `schema fault on the money path is found with the money already gone.`);
    process.exit(1);
}
console.log(`SCHEMA_PARITY_OK ${expected.size} table(s) verified against api/schema.sql`);
