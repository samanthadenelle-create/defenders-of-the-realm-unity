// URGENT cross-lane probe: can issueSession's INSERT actually run on production?
// Inserts a throwaway, already-expired session row in each shape and deletes it.
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));

const W = 'PROBEwalletPROBEwalletPROBEwallet1';
async function attempt(label, fn) {
    try { await fn(); console.log(label, '-> OK'); }
    catch (e) { console.log(label, '-> FAILS:', (e.code || '') + ' ' + String(e.message).slice(0, 160)); }
}

await attempt('shape A  (token, wallet, expires_at)                 [pre-0013 schema]', () =>
    sql`INSERT INTO auth_sessions (token, wallet, expires_at)
        VALUES ('probeA', ${W}, NOW() - INTERVAL '1 hour')`);

await attempt('shape B  (token, wallet, identity_kind, expires_at)  [HEAD issueSession]', () =>
    sql`INSERT INTO auth_sessions (token, wallet, identity_kind, expires_at)
        VALUES ('probeB', ${W}, 'wallet', NOW() - INTERVAL '1 hour')`);

await attempt('shape C  (+ signed_at)                               [WO-1441 in-flight]', () =>
    sql`INSERT INTO auth_sessions (token, wallet, identity_kind, expires_at, signed_at)
        VALUES ('probeC', ${W}, 'wallet', NOW() - INTERVAL '1 hour', NOW())`);

await sql`DELETE FROM auth_sessions WHERE wallet = ${W}`;
console.log('probe rows cleaned:', (await sql`SELECT COUNT(*)::int AS n FROM auth_sessions WHERE wallet = ${W}`)[0].n);
console.log('live session rows total:', (await sql`SELECT COUNT(*)::int AS n FROM auth_sessions`)[0].n);
