// =============================================================================
// tools/maintenance-toggle.mjs - the OPERATOR SURFACE for the kill switches.
// -----------------------------------------------------------------------------
// WO-1243. Six toggles the owner flips when she decides an area must stop:
//     farming | raiding | arena | dungeons | store | server
//
// Owner ruling 2026-08-27, verbatim: "mine allows if we see someone finds a
// hack, we seal that area and patch". This is the lever. It writes the table
// that api/_lib/maintenance.js enforces server-side; it does NOT ship a build,
// does NOT deploy, and takes effect within about five seconds.
//
// Driven by tools/command-centre.ps1 -Maintenance, which is the surface the
// owner actually uses ("a toggle in command center"). Runnable by hand too.
//
//   node tools/maintenance-toggle.mjs list
//   node tools/maintenance-toggle.mjs seal  raiding "Raids are closed while we fix an exploit."
//   node tools/maintenance-toggle.mjs open  raiding
//
// Needs DATABASE_URL in the environment. ⛔ Never pass a connection string as an
// argument - it lands in shell history and in the command-centre run log.
//
// Judge by the MARKER on a fresh log (CLAUDE.md section 8), never the exit code:
//     MAINTENANCE_TOGGLE_OK   / MAINTENANCE_LIST_OK
//     MAINTENANCE_TOGGLE_FAIL
//
// AUDIT TRAIL: every write stamps updated_by and updated_at, so "when did we
// seal it, and who flipped it" is answerable after an incident. updated_by is an
// operator label (--by, default the machine name), NEVER a player identity.
//
// ⚠ UPSERT, NOT INSERT ... ON CONFLICT DO NOTHING. api/schema.sql seeds the six
// rows with DO NOTHING, which does NOT back-fill a database provisioned before
// today - the trap that shut two dungeons in production this week (WO-1223).
// This tool therefore CREATES the row if it is missing rather than silently
// doing nothing. Under the fail-open ruling a missing row is harmless (nothing
// is sealed), but a SEAL that silently did not write would be a disaster, so the
// write path can never be the no-op branch.
// =============================================================================

import { hostname } from 'node:os';

const AREAS = ['farming', 'raiding', 'arena', 'dungeons', 'store', 'server'];

function fail(msg) {
    console.error('MAINTENANCE_TOGGLE_FAIL ' + msg);
    process.exit(16);
}

const argv = process.argv.slice(2);
const flags = {};
const positional = [];
for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--by' && i + 1 < argv.length) { flags.by = argv[++i]; continue; }
    positional.push(argv[i]);
}

const command = (positional[0] || '').toLowerCase();
const operator = String(flags.by || hostname() || 'operator').slice(0, 64);

if (command !== 'list' && command !== 'seal' && command !== 'open') {
    fail('USAGE list | seal <area> "<message>" | open <area>');
}

const url = process.env.DATABASE_URL;
if (!url) fail('DATABASE_URL_MISSING');

const { neon } = await import('@neondatabase/serverless');
const sql = neon(url);

// -----------------------------------------------------------------------------
// list
// -----------------------------------------------------------------------------
if (command === 'list') {
    let rows;
    try {
        rows = await sql`SELECT area_id, closed, message, updated_by, updated_at
                         FROM maintenance_toggles ORDER BY area_id`;
    } catch (err) {
        // ⛔ A read failure is NOT "everything is open". It means we cannot see the
        // table, and the operator must be told that in those words - the fail-open
        // ruling governs the GAME's behaviour, never this tool's reporting.
        fail('TABLE_UNREADABLE ' + (err && err.message ? err.message : 'unknown') +
             ' (the game itself FAILS OPEN in this state - nothing is sealed)');
    }

    const seen = new Set();
    for (const r of rows) {
        seen.add(r.area_id);
        const state = r.closed === true ? 'SEALED' : 'open  ';
        console.log('  ' + state + '  ' + String(r.area_id).padEnd(9) +
                    '  by=' + (r.updated_by || '-') +
                    '  at=' + (r.updated_at ? new Date(r.updated_at).toISOString() : '-') +
                    '  msg=' + (r.message ? JSON.stringify(String(r.message)) : '(none)'));
    }
    for (const a of AREAS) {
        if (!seen.has(a)) {
            console.log('  open    ' + a.padEnd(9) + '  by=-  at=-  msg=(no row; fail-open means OPEN)');
        }
    }
    const sealed = rows.filter((r) => r.closed === true).map((r) => r.area_id);
    console.log('MAINTENANCE_LIST_OK rows=' + rows.length + ' sealed=' +
                (sealed.length ? sealed.join(',') : 'none'));
    process.exit(0);
}

// -----------------------------------------------------------------------------
// seal / open
// -----------------------------------------------------------------------------
const area = (positional[1] || '').toLowerCase();
if (!AREAS.includes(area)) fail('UNKNOWN_AREA "' + area + '" (expected one of ' + AREAS.join(', ') + ')');

const closing = command === 'seal';
let message = positional[2] != null ? String(positional[2]) : '';

if (closing) {
    if (!message.trim()) {
        // A seal with no message puts an unexplained wall in front of a paying
        // player. The banner must read as maintenance from its WORDS - the owner
        // is red/green colourblind and no meaning may live in colour alone.
        fail('MESSAGE_REQUIRED_TO_SEAL (the banner has nothing to say without it)');
    }
    // eslint-disable-next-line no-control-regex
    if (/[^\x20-\x7E]/.test(message)) {
        fail('MESSAGE_NOT_ASCII (the in-game banner font is ASCII-only)');
    }
    if (message.length > 200) fail('MESSAGE_TOO_LONG (200 chars max; it scrolls past)');
} else {
    message = null;   // opening clears the banner text with the seal
}

let rows;
try {
    rows = await sql`
        INSERT INTO maintenance_toggles (area_id, closed, message, updated_by, updated_at)
        VALUES (${area}, ${closing}, ${message}, ${operator}, NOW())
        ON CONFLICT (area_id) DO UPDATE
        SET closed = EXCLUDED.closed,
            message = EXCLUDED.message,
            updated_by = EXCLUDED.updated_by,
            updated_at = NOW()
        RETURNING area_id, closed, updated_by, updated_at`;
} catch (err) {
    fail('WRITE_FAILED ' + (err && err.message ? err.message : 'unknown'));
}

if (!rows || !rows.length) fail('WRITE_RETURNED_NO_ROW');

const r = rows[0];
// Read the row back rather than trusting the statement: this is the proof that
// the seal actually landed, which is the one thing that must never be assumed.
console.log((r.closed ? 'SEALED  ' : 'OPENED  ') + r.area_id +
            '  by=' + r.updated_by +
            '  at=' + new Date(r.updated_at).toISOString());
if (closing) {
    console.log('  NOTE: server-side enforcement takes effect within ~5s (lambda memo).');
    console.log('  NOTE: the player banner catches up within ~40s (10s edge + 30s client poll).');
}
console.log('MAINTENANCE_TOGGLE_OK area=' + r.area_id + ' closed=' + r.closed + ' by=' + r.updated_by);
process.exit(0);
