// WO-1440 — apply api/migrations/20260906_0019_promo_guest_redeem_ip_budget.sql
// to the live Neon database, then VERIFY by shape query (never by exit code).
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const url = env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, '');
const sql = neon(url);

const file = new URL('../api/migrations/20260906_0019_promo_guest_redeem_ip_budget.sql', import.meta.url);
const text = readFileSync(file, 'utf8');

// Strip comments, then split on statement terminators. This migration contains no
// function bodies or dollar-quoted strings, so a naive split is safe here.
const stripped = text.split('\n').filter(l => !l.trimStart().startsWith('--')).join('\n');
const statements = stripped.split(';').map(s => s.trim()).filter(Boolean);

for (const s of statements) {
    console.log('RUN:', s.split('\n')[0].slice(0, 90));
    // neon 0.10.x exposes only the tagged-template form; a one-element strings
    // array with no interpolations is exactly that, for parameterless DDL.
    await sql([s]);
}

const cols = await sql`
    SELECT column_name FROM information_schema.columns
     WHERE table_schema='public' AND table_name='promo_redemptions' AND column_name='ip_hash'`;
const tbl = await sql`
    SELECT column_name, data_type FROM information_schema.columns
     WHERE table_schema='public' AND table_name='promo_ip_budget' ORDER BY ordinal_position`;

console.log('promo_redemptions.ip_hash present:', cols.length === 1);
console.log('promo_ip_budget columns:', tbl.map(c => `${c.column_name}:${c.data_type}`).join(', '));
if (cols.length === 1 && tbl.length === 6) console.log('MIGRATION_0019_OK');
else console.log('MIGRATION_0019_FAILED');
