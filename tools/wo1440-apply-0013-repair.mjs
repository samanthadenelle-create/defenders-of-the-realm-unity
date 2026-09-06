// PRODUCTION REPAIR (found while proving WO-1440's wallet-rail acceptance):
// api/_lib/wallet-auth.issueSession INSERTs `identity_kind`, but migration
// 20260830_0013_auth_sessions_identity_kind.sql had NEVER been applied to the live
// database — so POST /api/auth/session failed 42703 for EVERY wallet, and no wallet
// could obtain a session at all. Additive, idempotent, one column with a default.
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));

await sql`ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS identity_kind TEXT NOT NULL DEFAULT 'wallet'`;

const col = await sql`
    SELECT column_name, data_type, is_nullable, column_default
      FROM information_schema.columns
     WHERE table_name='auth_sessions' AND column_name='identity_kind'`;
console.log('identity_kind:', JSON.stringify(col));

// Prove the SUCCESS path, not just the absence of an error: run issueSession's exact
// INSERT shape, then remove the probe row.
const W = 'PROBEwalletPROBEwalletPROBEwallet1';
let ok = false;
try {
    await sql`INSERT INTO auth_sessions (token, wallet, identity_kind, expires_at)
              VALUES ('probe0013', ${W}, 'wallet', NOW() - INTERVAL '1 hour')`;
    ok = true;
} catch (e) { console.log('INSERT still fails:', e.code, e.message); }
await sql`DELETE FROM auth_sessions WHERE wallet = ${W}`;
console.log(ok && col.length === 1 ? 'MIGRATION_0013_REPAIR_OK' : 'MIGRATION_0013_REPAIR_FAILED');
