// Sweep: every `ALTER TABLE <t> ADD COLUMN [IF NOT EXISTS] <c>` in api/migrations/ and
// api/schema.sql, checked against the LIVE database.
//
// WHY THIS EXISTS: tools/schema-parity.mjs reads CREATE TABLE bodies only, deliberately —
// so a column added by ALTER is invisible to it. That blind spot is exactly how
// auth_sessions.identity_kind sat unapplied for a week while the deployed issueSession
// INSERTed it, failing 42703 for every wallet with no gate saying so.
import { readFileSync, readdirSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const root = new URL('../api/', import.meta.url);
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));

const files = [new URL('schema.sql', root),
    ...readdirSync(new URL('migrations/', root)).filter(f => f.endsWith('.sql'))
        .map(f => new URL('migrations/' + f, root))];

const wanted = new Map(); // "table.column" -> source file
const re = /ALTER\s+TABLE\s+(?:IF\s+EXISTS\s+)?([A-Za-z0-9_]+)\s+ADD\s+COLUMN\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z0-9_]+)/gi;
for (const f of files) {
    const text = readFileSync(f, 'utf8')
        .split('\n').filter(l => !l.trimStart().startsWith('--')).join('\n');
    let m;
    while ((m = re.exec(text)) !== null) {
        wanted.set(`${m[1].toLowerCase()}.${m[2].toLowerCase()}`, f.pathname.split('/').pop());
    }
}

const live = await sql`
    SELECT table_name, column_name FROM information_schema.columns WHERE table_schema='public'`;
const have = new Set(live.map(r => `${r.table_name}.${r.column_name}`));

const missing = [...wanted.entries()].filter(([k]) => !have.has(k));
console.log(`checked ${wanted.size} ALTER-added column(s) across ${files.length} file(s)`);
for (const [k, f] of missing) console.log('  MISSING ON LIVE DB:', k, '<-', f);
console.log(missing.length === 0 ? 'ALTER_COLUMN_SWEEP_OK' : `ALTER_COLUMN_SWEEP_MISSING ${missing.length}`);
