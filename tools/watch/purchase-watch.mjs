// =============================================================================
// purchase-watch.mjs - live tail of the MONEY tables during an owner purchase test.
//
// WHY THIS EXISTS (2026-08-24, WO-1169): purchase_quotes and purchase_entitlements are
// the SERVER's authoritative record of a sale, and NEITHER admin surface exposes them -
// api/admin/db.js does not list them at all, and api/admin/stats.js reports purchases out
// of analytics_events, a CLIENT-emitted event that carries no price. So "did that money
// actually settle" was unanswerable from any console. This is the stopgap until the
// WO-1169 first slice lands.
//
// Read-only: every statement is a SELECT.
// Usage: node tools/watch/purchase-watch.mjs [--once] [--since-minutes N]
// =============================================================================
import { readFileSync } from 'node:fs';
import { neon } from '@neondatabase/serverless';

const env = Object.fromEntries(
    readFileSync('.env.local', 'utf8')
        .split(/\r?\n/)
        .filter(l => l && !l.startsWith('#') && l.includes('='))
        .map(l => { const i = l.indexOf('='); return [l.slice(0, i), l.slice(i + 1).replace(/^"|"$/g, '')]; })
);
const sql = neon(env.DATABASE_URL);

const once = process.argv.includes('--once');
const mIdx = process.argv.indexOf('--since-minutes');
const sinceMin = mIdx > -1 ? Number(process.argv[mIdx + 1]) : 90;

const seenQ = new Set(), seenE = new Set();
let firstPass = true;

function fmt(v) { return v === null || v === undefined ? '-' : String(v); }

async function poll() {
    // ⚠ Column names are discovered, not assumed - a wrong guess here would print
    // "no rows" and read exactly like "no purchase happened".
    const quotes = await sql`
        SELECT * FROM purchase_quotes
        WHERE created_at > now() - (${sinceMin} || ' minutes')::interval
        ORDER BY created_at DESC LIMIT 25`;
    const ents = await sql`
        SELECT * FROM purchase_entitlements
        WHERE created_at > now() - (${sinceMin} || ' minutes')::interval
        ORDER BY created_at DESC LIMIT 25`;

    for (const r of quotes.slice().reverse()) {
        const k = JSON.stringify(r.quote_id ?? r.id ?? r);
        if (seenQ.has(k)) continue;
        seenQ.add(k);
        if (firstPass) continue;
        console.log(`QUOTE   sku=${fmt(r.sku)} amount=${fmt(r.amount_base_units ?? r.amount)} ` +
                    `wallet=${fmt(r.wallet)} state=${fmt(r.state ?? r.status)} at=${fmt(r.created_at)}`);
    }
    for (const r of ents.slice().reverse()) {
        const k = JSON.stringify(r.id ?? r.tx_signature ?? r);
        if (seenE.has(k)) continue;
        seenE.add(k);
        if (firstPass) continue;
        console.log(`SETTLED sku=${fmt(r.sku)} amount=${fmt(r.amount_base_units ?? r.amount)} ` +
                    `sig=${fmt(r.tx_signature ?? r.signature)} state=${fmt(r.state ?? r.status)} at=${fmt(r.created_at)}`);
    }

    if (firstPass) {
        console.log(`BASELINE quotes=${quotes.length} entitlements=${ents.length} (last ${sinceMin}m) - watching for NEW rows`);
        if (quotes.length) console.log('  newest quote columns:', Object.keys(quotes[0]).join(', '));
        if (ents.length)   console.log('  newest entitlement columns:', Object.keys(ents[0]).join(', '));
        firstPass = false;
    }
}

await poll();
if (!once) {
    for (;;) { await new Promise(r => setTimeout(r, 5000)); try { await poll(); } catch (e) { console.log('poll error:', e.message); } }
}
