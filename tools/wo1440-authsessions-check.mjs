// Cross-lane safety check: does auth_sessions.signed_at exist on production?
// (Another lane's WO-1441 server work appeared in the shared working tree; if it was
// swept into a deploy without its migration, session minting would fail outright.)
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));
const cols = await sql`
    SELECT column_name, is_nullable, column_default FROM information_schema.columns
     WHERE table_schema='public' AND table_name='auth_sessions' ORDER BY ordinal_position`;
console.log('auth_sessions columns:', JSON.stringify(cols, null, 1));
console.log('signed_at present:', cols.some(c => c.column_name === 'signed_at'));
