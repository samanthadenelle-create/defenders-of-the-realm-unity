// =============================================================================
// test/migrations.runner.test.js - WO-1446 + "one migration runner, not nine".
// -----------------------------------------------------------------------------
// Two things are proven here, and the SECOND one is the durable half of the ticket.
//
//   1. tools/run-migrations.mjs behaves: filename ordering, the additive-only
//      audit, the ledger skip, and the ONE-transaction shape of an apply. All of
//      it against a recording mock - zero network, zero database.
//
//   2. ⛔ NO COLUMN IS INSERTED BY api/_lib/wallet-auth.js THAT NOTHING CREATES.
//      This is the oracle for the failure that has now cost two weeks of live
//      wallet logins:
//        * auth_sessions.identity_kind - INSERTed from 2026-08-30, absent from the
//          production database, 500ing EVERY wallet session mint with 42703 until
//          2026-09-06 (WO-1440 RESULT §7c).
//        * auth_sessions.signed_at     - about to repeat it verbatim (WO-1446).
//      Both were *described* in api/schema.sql as an ALTER, and an ALTER inside
//      schema.sql is a DESCRIPTION, not a thing that runs. Only api/migrations/
//      gets applied. So the source set below is deliberately asymmetric:
//
//          migrations/*.sql : CREATE TABLE bodies  AND  ALTER ... ADD COLUMN
//          schema.sql       : CREATE TABLE bodies  ONLY   <- the ALTERs are excluded
//
//      That asymmetry IS the test. Widen it to accept schema.sql's ALTERs and this
//      file goes permanently green while production keeps 500ing, which is exactly
//      the state the repo was in this morning.
//
//     node --test test/migrations.runner.test.js
//
// Zero network, zero database, zero Unity. Node built-ins only.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const REPO = path.resolve(__dirname, '..');
const MIGRATIONS_DIR = path.join(REPO, 'api', 'migrations');
const SCHEMA_SQL = path.join(REPO, 'api', 'schema.sql');
const WALLET_AUTH = path.join(REPO, 'api', '_lib', 'wallet-auth.js');
const NEW_MIGRATION = '20260906_0020_auth_sessions_signed_at.sql';

// The runner is ESM; this suite is CommonJS like every other file in test/.
// A dynamic import is the seam, and it is cached after the first await.
let R;
async function runner() {
    if (!R) R = await import('node:url').then(u =>
        import(u.pathToFileURL(path.join(REPO, 'tools', 'run-migrations.mjs')).href));
    return R;
}

// ── A recording client. `.query` is the entire contract applyOne needs. ──────
function mockClient(opts = {}) {
    const calls = [];
    return {
        calls,
        async query(text, values) {
            calls.push({ text: String(text).trim(), values: values || null });
            if (opts.throwOn && opts.throwOn.test(String(text))) throw new Error('boom: ' + opts.throwOn);
            return { rows: opts.rows || [] };
        },
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// 1. THE FILE LIST IS DERIVED, AND IN FILENAME ORDER
// ═══════════════════════════════════════════════════════════════════════════

test('listMigrations returns every .sql on disk, in filename order', async () => {
    const { listMigrations } = await runner();
    const files = listMigrations(MIGRATIONS_DIR);
    const onDisk = fs.readdirSync(MIGRATIONS_DIR).filter(f => f.toLowerCase().endsWith('.sql'));

    assert.equal(files.length, onDisk.length,
        'the runner must see EVERY migration on disk - a hand-kept array is what this replaces');
    assert.deepEqual(files, [...files].sort(),
        'filename order is the apply order; the NNNN prefix is the only thing sequencing these');
    assert.ok(files.length >= 20, `expected at least 20 migrations, saw ${files.length}`);
});

test('the WO-1446 migration exists, is last, and carries EXACTLY the schema.sql ALTER', async () => {
    const { listMigrations } = await runner();
    const files = listMigrations(MIGRATIONS_DIR);
    assert.ok(files.includes(NEW_MIGRATION), `${NEW_MIGRATION} must exist - it is the applyable copy`);
    assert.equal(files[files.length - 1], NEW_MIGRATION, 'it is the newest and must sort last');

    const EXPECTED = 'ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS signed_at TIMESTAMPTZ NOT NULL DEFAULT NOW();';
    const body = fs.readFileSync(path.join(MIGRATIONS_DIR, NEW_MIGRATION), 'utf8');
    const schema = fs.readFileSync(SCHEMA_SQL, 'utf8');
    assert.ok(body.includes(EXPECTED), 'the migration must carry the ALTER verbatim');
    assert.ok(schema.includes(EXPECTED),
        'api/schema.sql must still describe the same column - the two may never disagree');

    // Exactly one executable statement. A migration that quietly grew a second
    // statement is a different migration than the one that was reviewed.
    const { splitStatements, maskNonCode } = R;
    const stmts = splitStatements(body).filter(s => maskNonCode(s).trim());
    assert.equal(stmts.length, 1, `expected 1 statement, got ${stmts.length}: ${stmts.map(s => s.trim().slice(0, 60))}`);
});

// ═══════════════════════════════════════════════════════════════════════════
// 2. THE ADDITIVE-ONLY AUDIT
// ═══════════════════════════════════════════════════════════════════════════

test('every migration ON DISK passes the additive audit', async () => {
    const { listMigrations, auditAdditive } = await runner();
    const dirty = [];
    for (const f of listMigrations(MIGRATIONS_DIR)) {
        const offenders = auditAdditive(fs.readFileSync(path.join(MIGRATIONS_DIR, f), 'utf8'));
        if (offenders.length) dirty.push(`${f}: ${offenders.join(' | ')}`);
    }
    assert.deepEqual(dirty, [], 'a destructive statement in api/migrations/ stops the runner before anything applies');
});

test('the audit catches DROP / DELETE / TRUNCATE, and does NOT false-positive', async () => {
    const { auditAdditive } = await runner();

    assert.equal(auditAdditive('DROP TABLE auth_sessions;').length, 1, 'DROP TABLE must be caught');
    assert.equal(auditAdditive('DELETE FROM auth_nonces WHERE used = TRUE;').length, 1, 'DELETE must be caught');
    assert.equal(auditAdditive('TRUNCATE player_data;').length, 1, 'TRUNCATE must be caught');
    assert.equal(auditAdditive('ALTER TABLE t DROP COLUMN c;').length, 1,
        'a DROP COLUMN hidden mid-statement is still destructive');
    assert.equal(auditAdditive('DO $$ BEGIN DROP TABLE t; END $$;').length, 1,
        'a DROP inside a DO block is still a DROP - the audit must see into dollar-quoted bodies');

    // The two real false-positive traps in this corpus.
    assert.deepEqual(auditAdditive(
        'ALTER TABLE db_promo_pack_items ADD CONSTRAINT fk FOREIGN KEY (pack_id) REFERENCES p(id) ON DELETE CASCADE;'),
        [], 'ON DELETE CASCADE is a clause, not a statement (20260828_0006 would fail the run otherwise)');
    assert.deepEqual(auditAdditive(
        '-- a cron may run: DELETE FROM auth_nonces WHERE expires_at < NOW();\nSELECT 1;'),
        [], 'prose describing a DELETE is prose (api/schema.sql documents exactly this)');
    assert.deepEqual(auditAdditive("INSERT INTO t (note) VALUES ('DROP TABLE x');"), [],
        'a destructive-looking string LITERAL is data, not a statement');
});

test('the ONE exemption is paired drop-and-recreate, and only that', async () => {
    const { auditAdditive } = await runner();

    // Postgres has no ADD CONSTRAINT IF NOT EXISTS, so this is the only idempotent
    // way to author a CHECK. Four migrations in this repo already do it (0011, 0017).
    assert.deepEqual(auditAdditive(
        'ALTER TABLE q DROP CONSTRAINT IF EXISTS q_rail_check;\n' +
        "ALTER TABLE q ADD  CONSTRAINT q_rail_check CHECK (rail IN ('solana','pi'));"),
        [], 'a CHECK dropped and recreated under the SAME NAME in the SAME FILE destroys nothing');

    assert.deepEqual(auditAdditive(
        'DROP TRIGGER IF EXISTS votes_immutable ON votes;\n' +
        'CREATE TRIGGER votes_immutable BEFORE UPDATE ON votes FOR EACH ROW EXECUTE FUNCTION f();'),
        [], 'same for an immutability trigger (0010, 0012)');

    // The exemption is PAIRING, not the keyword. Unpaired stays refused.
    assert.equal(auditAdditive('ALTER TABLE q DROP CONSTRAINT IF EXISTS q_rail_check;').length, 1,
        'a constraint dropped and NOT recreated is a real loosening of a guard');
    assert.equal(auditAdditive('DROP TRIGGER IF EXISTS votes_immutable ON votes;').length, 1,
        'an immutability trigger dropped and not recreated is how an append-only table stops being one');

    // And no pairing exempts DATA destruction.
    assert.equal(auditAdditive('DROP TABLE t;\nCREATE TABLE t (id TEXT);').length, 1,
        'drop-and-recreate a TABLE is not a repair, it is deleting every row in it');
    assert.equal(auditAdditive('ALTER TABLE t DROP COLUMN c;\nALTER TABLE t ADD COLUMN c INT;').length, 1,
        'same for a column');

    // Statement-level DROP that is not a drop of anything.
    assert.deepEqual(auditAdditive('ALTER TABLE t ALTER COLUMN c DROP NOT NULL;'), [],
        'DROP NOT NULL / DROP DEFAULT relax a constraint on a column; they remove no object and no row');
});

// ═══════════════════════════════════════════════════════════════════════════
// 3. TRANSACTION HANDLING - the file's own BEGIN/COMMIT must not end ours
// ═══════════════════════════════════════════════════════════════════════════

test('stripOuterTransaction removes the file BEGIN/COMMIT and keeps plpgsql BEGIN', async () => {
    const { stripOuterTransaction } = await runner();

    const wrapped = 'BEGIN;\nALTER TABLE t ADD COLUMN c INT;\nCOMMIT;\n';
    const stripped = stripOuterTransaction(wrapped);
    assert.ok(/ALTER TABLE t ADD COLUMN c INT/.test(stripped), 'the real statement survives');
    assert.ok(!/^\s*BEGIN\s*;/mi.test(stripped), 'the file BEGIN is gone - our transaction owns the boundary');
    assert.ok(!/^\s*COMMIT\s*;/mi.test(stripped),
        'the file COMMIT is gone - it would END our transaction and drop the ledger INSERT into autocommit');

    const doBlock = 'BEGIN;\nDO $$ BEGIN IF TRUE THEN NULL; END IF; END $$;\nCOMMIT;\n';
    const keptBlock = stripOuterTransaction(doBlock);
    assert.ok(/DO \$\$ BEGIN IF TRUE THEN NULL; END IF; END \$\$/.test(keptBlock),
        'the plpgsql BEGIN lives inside a dollar-quoted body and must be untouched, whole');
});

test('a real wrapped migration keeps its statements after stripping', async () => {
    const { stripOuterTransaction } = await runner();
    const f = path.join(MIGRATIONS_DIR, '20260828_0005_db_promo_packs.sql');
    const before = fs.readFileSync(f, 'utf8');
    assert.ok(/^BEGIN;/m.test(before), 'fixture assumption: this file opens with its own BEGIN;');
    const after = stripOuterTransaction(before);
    assert.ok(!/^\s*(BEGIN|COMMIT)\s*;/mi.test(after), 'both are stripped');
    assert.ok(/CREATE TABLE/i.test(after), 'the body is intact');
});

test('EVERY self-wrapped migration survives the strip, DO-blocks whole', async () => {
    const { listMigrations, splitStatements, maskNonCode, stripOuterTransaction } = await runner();
    let wrapped = 0;
    for (const f of listMigrations(MIGRATIONS_DIR)) {
        const before = fs.readFileSync(path.join(MIGRATIONS_DIR, f), 'utf8');
        const beforeStmts = splitStatements(before);
        const txCount = beforeStmts.filter(s =>
            /^(BEGIN|COMMIT|ROLLBACK|START\s+TRANSACTION)$/i.test(maskNonCode(s).trim())).length;
        if (!txCount) continue;
        wrapped++;

        const after = stripOuterTransaction(before);
        const afterStmts = splitStatements(after);
        assert.equal(afterStmts.length, beforeStmts.length - txCount,
            `${f}: stripping must remove exactly the ${txCount} transaction statement(s) and nothing else`);
        assert.equal(afterStmts.filter(s =>
            /^(BEGIN|COMMIT|ROLLBACK)$/i.test(maskNonCode(s).trim())).length, 0,
            `${f}: no top-level transaction statement may survive`);

        // 0001:96 is the precise trap: a plpgsql BEGIN inside a file-level BEGIN;…COMMIT;.
        const doBlocks = (before.match(/DO\s+\$/gi) || []).length;
        assert.equal((after.match(/DO\s+\$/gi) || []).length, doBlocks,
            `${f}: every DO block must survive whole - its inner BEGIN is not a transaction`);
    }
    assert.ok(wrapped >= 11, `expected at least 11 self-wrapped migrations, saw ${wrapped}`);
});

// ═══════════════════════════════════════════════════════════════════════════
// 4. THE LEDGER - skip what is recorded, apply what is not
// ═══════════════════════════════════════════════════════════════════════════

test('plan() skips ledger-recorded ids and preserves order for the rest', async () => {
    const { plan } = await runner();
    const files = ['a.sql', 'b.sql', 'c.sql', 'd.sql'];

    const first = plan(files, new Set());
    assert.deepEqual(first.toApply, files, 'an EMPTY ledger means every file applies - the first prod run');
    assert.deepEqual(first.skipped, []);

    const partial = plan(files, new Set(['a.sql', 'c.sql']));
    assert.deepEqual(partial.toApply, ['b.sql', 'd.sql'], 'gaps apply in order, not just the tail');
    assert.deepEqual(partial.skipped, ['a.sql', 'c.sql']);

    const second = plan(files, new Set(files));
    assert.deepEqual(second.toApply, [], 'a second run applies NOTHING - the runner is idempotent by ledger');
    assert.equal(second.skipped.length, 4);

    assert.deepEqual(plan(files, ['a.sql']).toApply, ['b.sql', 'c.sql', 'd.sql'],
        'an array of ids is accepted as well as a Set');
});

test('applyOne wraps the body AND the ledger row in one transaction', async () => {
    const { applyOne } = await runner();
    const c = mockClient();
    await applyOne(c, '20260906_0020_auth_sessions_signed_at.sql', 'BEGIN;\nALTER TABLE t ADD COLUMN c INT;\nCOMMIT;\n');

    const shape = c.calls.map(x => x.text.split(/\s+/)[0].toUpperCase());
    assert.deepEqual(shape, ['BEGIN', 'ALTER', 'INSERT', 'COMMIT'],
        'exactly: our BEGIN, the stripped body, the ledger row, our COMMIT');

    const ledger = c.calls[2];
    assert.match(ledger.text, /INSERT INTO schema_migrations \(id, applied_at\)/,
        'the ledger row is written INSIDE the same transaction as the migration');
    assert.deepEqual(ledger.values, ['20260906_0020_auth_sessions_signed_at.sql'],
        'the id is the FILENAME, and it is parameterised, never interpolated');
    assert.match(ledger.text, /ON CONFLICT \(id\) DO NOTHING/, 're-applying must never key-collide');
});

test('a failing migration ROLLS BACK and writes no ledger row', async () => {
    const { applyOne } = await runner();
    const c = mockClient({ throwOn: /ALTER/ });
    await assert.rejects(() => applyOne(c, 'x.sql', 'ALTER TABLE t ADD COLUMN c INT;'), /boom/);

    const shape = c.calls.map(x => x.text.split(/\s+/)[0].toUpperCase());
    assert.deepEqual(shape, ['BEGIN', 'ALTER', 'ROLLBACK'],
        'no COMMIT, and crucially NO INSERT - a half-applied migration must never be recorded as done');
    assert.ok(!c.calls.some(x => /INSERT INTO schema_migrations/.test(x.text)),
        'recording a migration that threw is the one failure mode a ledger must not have');
});

// ═══════════════════════════════════════════════════════════════════════════
// 4b. BASELINE MODE - because two migrations on disk CANNOT be re-applied
// ═══════════════════════════════════════════════════════════════════════════

// Statements that raise on a second run. Comments stripped first; a constraint
// counts as guarded when the same file DROPs it IF EXISTS, or tests pg_constraint
// for it inside a DO block (0001/0002/0003/0004 all use one of those two idioms).
function nonIdempotentStatements(sql) {
    const t = stripComments(sql);
    const found = [];
    const dropped = new Set([...t.matchAll(/DROP\s+CONSTRAINT\s+IF\s+EXISTS\s+(\w+)/gi)].map(m => m[1].toLowerCase()));
    const guarded = /pg_constraint/i.test(t);
    for (const m of t.matchAll(/ADD\s+CONSTRAINT\s+(\w+)/gi)) {
        if (!dropped.has(m[1].toLowerCase()) && !guarded) found.push('ADD CONSTRAINT ' + m[1]);
    }
    for (const m of t.matchAll(/INSERT\s+INTO\s+(\w+)[\s\S]{0,4000}?;/gi)) {
        if (!/ON\s+CONFLICT/i.test(m[0])) found.push('INSERT INTO ' + m[1] + ' (no ON CONFLICT)');
    }
    for (const m of t.matchAll(/CREATE\s+(?:UNIQUE\s+)?(TABLE|INDEX)\s+(?!IF\s+NOT\s+EXISTS)(\w+)/gi)) {
        found.push('CREATE ' + m[1] + ' ' + m[2] + ' (no IF NOT EXISTS)');
    }
    return found;
}

test('⛔ exactly TWO migrations cannot be re-applied - which is why --baseline exists', () => {
    // MEASURED, not assumed. If this list ever shrinks to [], --baseline stops being
    // necessary and this file should say so. If it GROWS, a new migration was authored
    // that a re-run would break, and the author needs to know before it reaches prod.
    const dirty = {};
    for (const f of fs.readdirSync(MIGRATIONS_DIR).filter(x => x.endsWith('.sql')).sort()) {
        const hits = nonIdempotentStatements(fs.readFileSync(path.join(MIGRATIONS_DIR, f), 'utf8'));
        if (hits.length) dirty[f] = hits.length;
    }
    assert.deepEqual(Object.keys(dirty).sort(), [
        '20260828_0004_promo_reward_tiers.sql',
        '20260829_0011_public_town_snapshot_profile.sql',
    ], 'the set of non-re-runnable migrations changed - see this test\'s comment before touching the runner');

    assert.equal(dirty['20260828_0004_promo_reward_tiers.sql'], 1,
        "0004 INSERTs the live FIRSTWATCH promo_codes row with no ON CONFLICT: a re-run is 23505 " +
        'against an ALREADY-REDEEMED campaign');
    assert.equal(dirty['20260829_0011_public_town_snapshot_profile.sql'], 10,
        '0011 adds ten CHECK constraints with no DROP IF EXISTS and no pg_constraint guard: a re-run is 42710');

    // AND THIS IS WHY IT MATTERS: 0020 sorts LAST. A plain first run would reach 0004
    // long before it reached signed_at, die there, and leave the live database in the
    // exact state WO-1446 was raised about.
    const files = fs.readdirSync(MIGRATIONS_DIR).filter(x => x.endsWith('.sql')).sort();
    assert.ok(files.indexOf('20260828_0004_promo_reward_tiers.sql') < files.indexOf(NEW_MIGRATION),
        'the un-re-runnable file sorts BEFORE the fix, so a plain first run never reaches the fix');
});

test('--baseline covers everything up to and including the named file, and nothing after', async () => {
    const { parseArgs, baselineSet, listMigrations } = await runner();

    assert.deepEqual(parseArgs([]), { baselineThrough: null }, 'no flags is the ordinary apply run');
    assert.equal(parseArgs(['--baseline', 'x.sql']).baselineThrough, 'x.sql');
    assert.throws(() => parseArgs(['--baseline']), /needs the migration filename/);
    assert.throws(() => parseArgs(['--dry-run']), /unrecognised argument/,
        'an unknown flag must REFUSE, never silently fall through to applying');

    const files = listMigrations(MIGRATIONS_DIR);
    const through = '20260906_0019_promo_guest_redeem_ip_budget.sql';
    const covered = baselineSet(files, through);
    assert.equal(covered[covered.length - 1], through, 'inclusive of the named file');
    assert.ok(!covered.includes(NEW_MIGRATION),
        'the WO-1446 migration must NOT be baselined - it has never been applied and must really run');
    assert.equal(covered.length, files.length - 1, 'exactly one file is left for the ordinary run');

    assert.throws(() => baselineSet(files, 'not_a_file.sql'), /not in api\/migrations/,
        'baselining a name that does not exist would silently baseline nothing');
});

test('the ledger DDL creates exactly the table the ticket specifies', async () => {
    const { LEDGER_DDL, LEDGER_TABLE } = await runner();
    assert.equal(LEDGER_TABLE, 'schema_migrations');
    assert.match(LEDGER_DDL, /CREATE TABLE IF NOT EXISTS schema_migrations/);
    assert.match(LEDGER_DDL, /id\s+TEXT\s+PRIMARY KEY/i);
    assert.match(LEDGER_DDL, /applied_at\s+TIMESTAMPTZ/i);
});

test('importing the runner does not run it', async () => {
    // The whole file is importable without DATABASE_URL. If main() ever escapes its
    // guard, this suite would exit(16) instead of failing - so reaching this line at
    // all is the assertion.
    const mod = await runner();
    assert.equal(typeof mod.listMigrations, 'function');
    assert.equal(process.env.DATABASE_URL || '', process.env.DATABASE_URL || '',
        'no DATABASE_URL was required to import');
});

// ═══════════════════════════════════════════════════════════════════════════
// 5. THE DRIFT ORACLE - the durable half of WO-1446
// ═══════════════════════════════════════════════════════════════════════════

// Strip comments so prose about a column is never mistaken for a column.
function stripComments(sql) {
    return sql.replace(/\/\*[\s\S]*?\*\//g, ' ')
        .split('\n').map(l => l.replace(/--.*$/, '')).join('\n');
}

// Columns declared inside CREATE TABLE bodies. A paren-depth walk, because CHECK(),
// DEFAULT() and numeric(10,2) all nest parens and a naive /\(([^)]*)\)/ stops early.
function createTableColumns(sql) {
    const out = new Set();
    const re = /CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z0-9_."]+)\s*\(/gi;
    let m;
    while ((m = re.exec(sql)) !== null) {
        const table = m[1].replace(/"/g, '').split('.').pop().toLowerCase();
        let i = re.lastIndex, depth = 1, start = i;
        const entries = [];
        while (i < sql.length && depth > 0) {
            const ch = sql[i];
            if (ch === '(') depth++;
            else if (ch === ')') { depth--; if (depth === 0) { entries.push(sql.slice(start, i)); break; } }
            else if (ch === ',' && depth === 1) { entries.push(sql.slice(start, i)); start = i + 1; }
            i++;
        }
        for (const e of entries) {
            const tok = e.trim().split(/\s+/)[0];
            if (!tok) continue;
            const name = tok.replace(/"/g, '').toLowerCase();
            if (/^(constraint|primary|unique|check|foreign|exclude|like)$/.test(name)) continue;
            if (!/^[a-z_][a-z0-9_]*$/.test(name)) continue;
            out.add(`${table}.${name}`);
        }
    }
    return out;
}

// Columns added by ALTER TABLE ... ADD COLUMN. Same regex the live sweep uses
// (tools/wo1440-alter-column-sweep.mjs), deliberately.
function alterAddedColumns(sql) {
    const out = new Set();
    const re = /ALTER\s+TABLE\s+(?:IF\s+EXISTS\s+)?([A-Za-z0-9_]+)\s+ADD\s+COLUMN\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z0-9_]+)/gi;
    let m;
    while ((m = re.exec(sql)) !== null) out.add(`${m[1].toLowerCase()}.${m[2].toLowerCase()}`);
    return out;
}

// Columns api/_lib/wallet-auth.js actually WRITES. Keys are table.column: `wallet`
// and `expires_at` exist on several tables, so a flat set of bare column names
// would pass vacuously.
function insertedColumns(js) {
    const out = new Set();
    const re = /INSERT\s+INTO\s+([A-Za-z0-9_]+)\s*\(([^)]*)\)/gi;
    let m;
    while ((m = re.exec(js)) !== null) {
        const table = m[1].toLowerCase();
        for (const raw of m[2].split(',')) {
            const c = raw.trim().replace(/"/g, '').toLowerCase();
            if (/^[a-z_][a-z0-9_]*$/.test(c)) out.add(`${table}.${c}`);
        }
    }
    return out;
}

// The source set a DEPLOY actually gets: migrations in full, schema.sql's CREATEs only.
function knownColumns({ excludeMigrations = [] } = {}) {
    const known = new Set();
    for (const f of fs.readdirSync(MIGRATIONS_DIR).filter(f => f.endsWith('.sql')).sort()) {
        if (excludeMigrations.includes(f)) continue;
        const body = stripComments(fs.readFileSync(path.join(MIGRATIONS_DIR, f), 'utf8'));
        for (const k of createTableColumns(body)) known.add(k);
        for (const k of alterAddedColumns(body)) known.add(k);
    }
    const schema = stripComments(fs.readFileSync(SCHEMA_SQL, 'utf8'));
    for (const k of createTableColumns(schema)) known.add(k);   // CREATE bodies ONLY - see the header
    return known;
}

test('the oracle can see the columns it is meant to police (sanity)', () => {
    const inserted = insertedColumns(fs.readFileSync(WALLET_AUTH, 'utf8'));
    assert.ok(inserted.has('auth_sessions.signed_at'),
        'wallet-auth.js must still INSERT signed_at - if this fails the renewal cap was reverted, ' +
        'and this whole oracle would go green for the wrong reason');
    assert.ok(inserted.has('auth_sessions.identity_kind'), 'and identity_kind, the 2026-08-30 instance');
    assert.ok(inserted.size >= 8, `expected several INSERTed columns, parsed ${inserted.size}`);

    const known = knownColumns();
    assert.ok(known.has('auth_sessions.token'), 'CREATE TABLE bodies must parse (auth_sessions.token)');
    assert.ok(known.has('auth_sessions.identity_kind'), 'ALTER ADD COLUMN in migrations must parse');
    assert.ok(known.size > 100, `expected a large column corpus, parsed ${known.size}`);
});

test('⛔ every column wallet-auth.js INSERTs is CREATED by a file a deploy applies', () => {
    const inserted = insertedColumns(fs.readFileSync(WALLET_AUTH, 'utf8'));
    const known = knownColumns();
    const orphans = [...inserted].filter(c => !known.has(c)).sort();

    assert.deepEqual(orphans, [],
        'These columns are INSERTed by api/_lib/wallet-auth.js and created by NOTHING under\n' +
        'api/migrations/ or a schema.sql CREATE TABLE:\n  ' + orphans.join('\n  ') + '\n' +
        'An ALTER that lives only in api/schema.sql is a DESCRIPTION - nothing applies it to prod.\n' +
        'Add api/migrations/<date>_<nnnn>_<name>.sql, or this INSERT 500s with 42703 for every player.');
});

test('⛔ …and it PROVES it: remove 0020 and signed_at is reported', () => {
    // The same oracle, over the same real tree, with today's migration taken out -
    // i.e. the repo exactly as it stood this morning. If this does not go red, the
    // test above cannot have caught signed_at and is worthless.
    const inserted = insertedColumns(fs.readFileSync(WALLET_AUTH, 'utf8'));
    const withoutFix = knownColumns({ excludeMigrations: [NEW_MIGRATION] });
    const orphans = [...inserted].filter(c => !withoutFix.has(c));

    assert.deepEqual(orphans, ['auth_sessions.signed_at'],
        'Without 20260906_0020, auth_sessions.signed_at must be the one and only orphan - that is the\n' +
        'exact defect WO-1446 was raised for, and this line is the proof the oracle would have caught it.');
});
