// =============================================================================
// vault-watch.mjs - poll the treasury's SKR balance during an owner purchase test.
//
// WHY THE CHAIN AND NOT THE DB: Vercel returns DATABASE_URL REDACTED on `vercel env pull`
// (marked sensitive), so the Neon purchase tables are unreachable from here - and
// api/admin/db.js does not expose purchase_quotes / purchase_entitlements either (WO-1169).
// The chain needs no credential and is the authority anyway: if the SKR moved, it moved.
//
// Read-only RPC. Usage: node tools/watch/vault-watch.mjs [--once]
// =============================================================================
import { readFileSync } from 'node:fs';

const wallets = JSON.parse(readFileSync('Assets/Resources/Data/Canonical/wallets.json', 'utf8'));
const ATA  = wallets.mainnetPurchaseRecipient.skrAta;
const VAULT= wallets.mainnetPurchaseRecipient.address;
const RPC  = 'https://api.mainnet-beta.solana.com';

async function rpc(method, params) {
    const r = await fetch(RPC, {
        method: 'POST', headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }),
    });
    const j = await r.json();
    if (j.error) throw new Error(j.error.message);
    return j.result;
}

async function balance() {
    const r = await rpc('getTokenAccountBalance', [ATA, { commitment: 'confirmed' }]);
    return { ui: r.value.uiAmountString, raw: r.value.amount, dec: r.value.decimals };
}

async function recentSigs(limit = 5) {
    return await rpc('getSignaturesForAddress', [ATA, { limit }]);
}

const once = process.argv.includes('--once');
let last = null;
console.log(`vault ${VAULT}\n  SKR ATA ${ATA}`);

for (;;) {
    try {
        const b = await balance();
        if (last === null) {
            const sigs = await recentSigs(3);
            console.log(`BASELINE ${b.ui} SKR (raw ${b.raw}, decimals ${b.dec})`);
            for (const s of sigs) console.log(`  recent ${s.signature.slice(0, 24)}... slot=${s.slot} err=${s.err ? 'YES' : 'no'}`);
            console.log('watching for a CHANGE...');
        } else if (b.raw !== last.raw) {
            const delta = (Number(b.raw) - Number(last.raw)) / 10 ** b.dec;
            console.log(`\n*** BALANCE CHANGED  ${last.ui} -> ${b.ui} SKR   (delta ${delta > 0 ? '+' : ''}${delta})`);
            const sigs = await recentSigs(3);
            for (const s of sigs) console.log(`  sig ${s.signature} slot=${s.slot} err=${s.err ? JSON.stringify(s.err) : 'none'}`);
        }
        last = b;
    } catch (e) { console.log('poll error:', e.message); }
    if (once) break;
    await new Promise(r => setTimeout(r, 8000));
}
