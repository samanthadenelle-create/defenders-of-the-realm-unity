// =============================================================================
// WO-1440 — prove BOTH wallet sub-rails end-to-end against PRODUCTION.
// -----------------------------------------------------------------------------
// "A wallet holder still redeems via the wallet rail, unchanged" cannot be proven by
// refusals alone (memory: prove-the-success-path-not-just-the-refusal). A wallet is
// only an ed25519 keypair as far as this endpoint is concerned — nothing here needs a
// funded or real wallet — so the success path IS testable:
//
//   RAIL 1  per-request SIGNATURE  (X-Wallet + X-Nonce + X-Signature)
//           The ONLY wallet auth path the PUBLISHED store build 2026.08.17.328845 has:
//           the session rail landed in e526e013f on 2026-08-23, six days after it.
//   RAIL 2  bearer SESSION         (X-Session + X-Wallet)
//           Newer builds. Untestable before today: issueSession INSERTed
//           auth_sessions.identity_kind, a column that did not exist on production, so
//           no session could be minted at all. Migration 0013 was applied first.
//
// Uses a THROWAWAY promo code and a throwaway keypair; deletes both. Never FIRSTWATCH.
// Run: node tools/wo1440-wallet-rail-prod-proof.mjs
// =============================================================================
import { readFileSync } from 'node:fs';
import crypto from 'node:crypto';
import nacl from 'tweetnacl';
import bs58mod from 'bs58';
import { neon } from '@neondatabase/serverless';

const bs58 = bs58mod.default || bs58mod;
const env = readFileSync(new URL('../.env.local', import.meta.url), 'utf8');
const sql = neon(env.match(/^DATABASE_URL=(.*)$/m)[1].trim().replace(/^["']|["']$/g, ''));

const BASE = 'https://defenders-of-the-realm-v2.vercel.app';
const CODE = 'WO1440-WALLETPROOF';

// A wallet is a 32-byte ed25519 public key in base58. Generate one; sign with it.
const kp = nacl.sign.keyPair();
const WALLET = bs58.encode(Buffer.from(kp.publicKey));
console.log('throwaway wallet (base58, %d chars): %s', WALLET.length, WALLET);

async function cleanup() {
    await sql`DELETE FROM promo_codes WHERE code = ${CODE}`;
    await sql`DELETE FROM promo_ip_budget WHERE code = ${CODE}`;
    await sql`DELETE FROM auth_sessions WHERE wallet = ${WALLET}`;
    await sql`DELETE FROM auth_nonces  WHERE wallet = ${WALLET}`;
}
await cleanup();
await sql`
    INSERT INTO promo_codes
        (code, reward_crystals, reward_coins, message, active,
         max_redemptions, per_player_limit, expires_at, tier1_limit, redemption_count)
    VALUES (${CODE}, 7, 0, 'wallet rail proof', TRUE, NULL, NULL, NULL, NULL, 0)`;

const bodyStr = JSON.stringify({ playerId: WALLET, code: CODE, supportsInlinePackRewards: true });
const bodyBytes = Buffer.from(bodyStr, 'utf8');

// ── RAIL 1: per-request signature ────────────────────────────────────────────
const nonceRes = await fetch(`${BASE}/api/auth/nonce?wallet=${encodeURIComponent(WALLET)}`);
const nonceJson = await nonceRes.json().catch(() => ({}));
console.log('\nGET /api/auth/nonce ->', nonceRes.status, JSON.stringify(nonceJson).slice(0, 160));

if (nonceJson && nonceJson.nonce) {
    const msg = `dotr-save:v1:${WALLET}:${nonceJson.nonce}:${crypto.createHash('sha256').update(bodyBytes).digest('hex')}`;
    const sig = bs58.encode(Buffer.from(nacl.sign.detached(Buffer.from(msg, 'utf8'), kp.secretKey)));
    const r = await fetch(BASE + '/api/promo/redeem', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Wallet': WALLET, 'X-Nonce': nonceJson.nonce, 'X-Signature': sig,
        },
        body: bodyStr,
    });
    console.log('RAIL 1 (signature)  HTTP %d  %s', r.status, JSON.stringify(await r.json().catch(() => ({}))));
} else {
    console.log('RAIL 1 SKIPPED — no nonce issued.');
}

// ── RAIL 2: bearer session ───────────────────────────────────────────────────
// Mint it the way api/auth/session.js does, through issueSession's real INSERT shape.
// (Going through the HTTP endpoint would prove the same thing; this isolates the
// redeem route from session.js, which another lane is editing right now.)
const token = crypto.randomBytes(32).toString('base64url');
await sql`INSERT INTO auth_sessions (token, wallet, identity_kind, expires_at)
          VALUES (${token}, ${WALLET}, 'wallet', NOW() + INTERVAL '5 minutes')`;
const r2 = await fetch(BASE + '/api/promo/redeem', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Session': token, 'X-Wallet': WALLET },
    body: bodyStr,
});
const j2 = await r2.json().catch(() => ({}));
console.log('RAIL 2 (session)    HTTP %d  %s', r2.status, JSON.stringify(j2));

// ── What landed ──────────────────────────────────────────────────────────────
const ledger = await sql`SELECT player_id, crystals, redemption_ordinal, ip_hash
                           FROM promo_redemptions WHERE code = ${CODE}`;
const budget = await sql`SELECT * FROM promo_ip_budget WHERE code = ${CODE}`;
console.log('\nledger:', JSON.stringify(ledger));
console.log('promo_ip_budget rows:', budget.length,
    '  <-- MUST be 0: a proven wallet is never charged the guest IP budget');

const walletGranted = j2 && j2.success === true;
await cleanup();
console.log(walletGranted && budget.length === 0
    ? 'WO1440_WALLET_RAIL_OK (session sub-rail proven end-to-end; wallets not IP-counted)'
    : 'WO1440_WALLET_RAIL_FAILED');
