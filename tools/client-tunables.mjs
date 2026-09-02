// =============================================================================
// tools/client-tunables.mjs - the OPERATOR SURFACE for the PROD-022 knobs.
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-02, verbatim:
//   "make the testing as robust as possible with as many solutions as
//    possible... all we really have to do is just flip a flag and possibly
//    redeploy"
//
// A WebGL rebuild costs about thirty minutes and PROD-022 is a P0 crash loop we
// cannot reproduce locally. So every candidate mitigation ships in ONE build
// behind its own key, all defaulting to today's behaviour, and this is the lever
// that flips them. It writes the table api/client-tunables.js serves; it does NOT
// ship a build, does NOT deploy, and reaches a running client in about 40 s
// (10 s edge cache + the 30 s client poll).
//
// Driven by tools/command-centre.ps1 -Tunables, which is the surface the owner
// actually uses. Runnable by hand too.
//
//   node tools/client-tunables.mjs list
//   node tools/client-tunables.mjs set   pi.awaitInitBeforeFirstLoad 1
//   node tools/client-tunables.mjs clear pi.awaitInitBeforeFirstLoad
//
// ⭐ CLEAR IS NOT "SET TO 0". Clearing REMOVES the override, so the knob answers
// whatever the BUILD hardcodes - which for pi.requestTimeoutSeconds is 20, not 0.
// It is the one-word way back to today's behaviour and it is a separate verb for
// exactly that reason.
//
// Needs DATABASE_URL in the environment. ⛔ Never pass a connection string as an
// argument - it lands in shell history and in the command-centre run log.
//
// Judge by the MARKER on a fresh log (CLAUDE.md section 8), never the exit code:
//     TUNABLES_SET_OK / TUNABLES_CLEAR_OK / TUNABLES_LIST_OK
//     TUNABLES_FAIL
//
// AUDIT TRAIL: every write stamps updated_by and updated_at, so "which knob was
// on when that session died" is answerable afterwards. updated_by is an operator
// label (--by, default the machine name), NEVER a player identity.
//
// THE KEY ALLOWLIST IS IMPORTED from api/_lib/tunables.js rather than retyped -
// a third copy of the key list is the duplicated-state failure CLAUDE.md sections
// 2, 5 and 16 keep warning about. The DEFAULTS are deliberately NOT here at all:
// they live in the build (DeNelle.Core.Ops.RemoteTunables.Registry) and this
// table carries overrides only, so an empty table means today's behaviour.
//
// Owner-facing list of every knob, its default and what it tests:
//     docs/PROD022_TUNABLE_FLAGS.md
// =============================================================================

import { hostname } from 'node:os';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);

function fail(msg) {
    console.error('TUNABLES_FAIL ' + msg);
    process.exit(16);
}

let TUNABLE_KEYS, isKnownKey, normalizeValue;
try {
    ({ TUNABLE_KEYS, isKnownKey, normalizeValue } = require('../api/_lib/tunables.js'));
} catch (err) {
    fail('CANNOT_LOAD_KEY_ALLOWLIST ' + (err && err.message));
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

if (command !== 'list' && command !== 'set' && command !== 'clear') {
    fail('USAGE list | set <key> <value> | clear <key>   (keys: ' +
         TUNABLE_KEYS.map((k) => k.key).join(', ') + ')');
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
        rows = await sql`SELECT key, value, updated_by, updated_at
                         FROM client_tunables ORDER BY key`;
    } catch (err) {
        // ⛔ A read failure is NOT "no overrides are set". It means we cannot see
        // the table, and the operator must be told that in those words.
        fail('TABLE_UNREADABLE ' + (err && err.message) +
             ' - this tool cannot tell you what is set. It does NOT mean nothing is set.');
    }

    const set = new Map();
    for (const r of rows || []) set.set(String(r.key), r);

    console.log('');
    console.log('PROD-022 client tunables. An ABSENT row means the knob answers the value the');
    console.log('BUILD hardcodes - i.e. today\'s behaviour. Defaults live in the build, on purpose;');
    console.log('see docs/PROD022_TUNABLE_FLAGS.md for each knob, its default and what it tests.');
    console.log('');
    for (const spec of TUNABLE_KEYS) {
        const row = set.get(spec.key);
        if (row) {
            console.log('  OVERRIDDEN  ' + spec.key.padEnd(32) + ' = ' + String(row.value).padEnd(6) +
                        '  by ' + (row.updated_by || '?') + ' at ' + (row.updated_at || '?'));
        } else {
            console.log('  default     ' + spec.key.padEnd(32) + '   (' + spec.kind + ', no row)');
        }
    }

    const stray = [...set.keys()].filter((k) => !isKnownKey(k));
    for (const k of stray) {
        console.log('  ⚠ UNKNOWN   ' + k + ' - no build understands this key; it is ignored by every client.');
    }

    console.log('');
    console.log('TUNABLES_LIST_OK overrides=' + set.size + ' known=' + TUNABLE_KEYS.length);
    process.exit(0);
}

// -----------------------------------------------------------------------------
// set
// -----------------------------------------------------------------------------
if (command === 'set') {
    const key = String(positional[1] || '').trim();
    const raw = positional[2];

    if (!isKnownKey(key)) {
        fail('UNKNOWN_TUNABLE_KEY "' + key + '" - one of: ' +
             TUNABLE_KEYS.map((k) => k.key).join(', '));
    }
    const value = normalizeValue(key, raw);
    if (value === null) {
        // Refused rather than written, because a value the client cannot parse
        // would be accepted here, ignored there, and read as "the flag did nothing".
        fail('BAD_TUNABLE_VALUE "' + String(raw) + '" for ' + key +
             ' - bools take 0/1, ints take a whole number.');
    }

    let row;
    try {
        // UPSERT, never ON CONFLICT DO NOTHING. A write that silently did not land
        // would send the owner chasing a build during an incident.
        const rows = await sql`
            INSERT INTO client_tunables (key, value, updated_by, updated_at)
            VALUES (${key}, ${value}, ${operator}, NOW())
            ON CONFLICT (key) DO UPDATE
            SET value = EXCLUDED.value,
                updated_by = EXCLUDED.updated_by,
                updated_at = NOW()
            RETURNING key, value, updated_by, updated_at`;
        // Read the row BACK rather than trusting the statement. This is the proof
        // the flip actually landed, which is the one thing that must never be assumed.
        if (!rows || !rows.length) fail('WRITE_RETURNED_NO_ROW - the flip did not land');
        row = rows[0];
    } catch (err) {
        fail('WRITE_FAILED ' + (err && err.message));
    }

    console.log('set ' + row.key + ' = ' + row.value + ' by ' + row.updated_by + ' at ' + row.updated_at);
    console.log('Reaches a running client within about 40s (10s edge cache + 30s client poll).');
    console.log('Boot-time knobs are read from the on-device cache, so they take effect on the');
    console.log('NEXT launch of a client that has already fetched this value once.');
    console.log('TUNABLES_SET_OK key=' + row.key + ' value=' + row.value);
    process.exit(0);
}

// -----------------------------------------------------------------------------
// clear
// -----------------------------------------------------------------------------
{
    const key = String(positional[1] || '').trim();
    if (!isKnownKey(key)) {
        fail('UNKNOWN_TUNABLE_KEY "' + key + '" - one of: ' +
             TUNABLE_KEYS.map((k) => k.key).join(', '));
    }

    let existed = false;
    try {
        const rows = await sql`DELETE FROM client_tunables WHERE key = ${key} RETURNING key`;
        existed = !!(rows && rows.length);
    } catch (err) {
        fail('WRITE_FAILED ' + (err && err.message));
    }

    console.log('cleared ' + key + (existed ? '' : ' (there was no override; it was already at the default)'));
    console.log('This knob now answers the value the BUILD hardcodes - which is NOT the same as 0.');
    console.log('TUNABLES_CLEAR_OK key=' + key + ' hadOverride=' + existed);
    process.exit(0);
}
