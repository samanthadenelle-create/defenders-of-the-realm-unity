'use strict';

// =============================================================================
// WO-1223 — the SERVER half of the dungeon coverage contract.
// -----------------------------------------------------------------------------
// The Unity oracle (Assets/Editor/Regression/DungeonStatusRegression.cs, case
// [door-coverage]) proves that api/_lib/dungeon-manifest.json lists exactly the
// dungeons a player can reach. It cannot prove the other half: that each of
// those doors actually HAS a row to flip. Unity has no database, and a batchmode
// gate that opens a socket is a gate nobody trusts.
//
// So the split is: the manifest is the contract, Unity owns the client side of
// it, and this file owns the server side.
//
// ⚠ WHAT THIS ASSERTS AGAINST, AND WHAT THAT DOES NOT COVER — read before
//   trusting a green run. The authority here is api/schema.sql, the TRACKED
//   provisioning artifact, not the live Neon table. That is a deliberate choice
//   with a real limit:
//     * It catches the failure that has actually happened: a dungeon shipped
//       with no row anywhere, because nobody wrote one. ⚠ Since the owner's
//       fail-closed ruling (2026-08-26, WO-1223) that failure is no longer a
//       missed GATING opportunity - a gated dungeon with no row is SHUT to every
//       player. This file went from "nice to have" to an availability gate.
//     * It does NOT catch a row deleted straight out of Neon after provisioning
//       (the seed uses ON CONFLICT DO NOTHING, so re-running schema.sql will not
//       restore it either). Catching that needs a live query, which needs
//       DATABASE_URL and a network — neither of which belongs in a unit test.
//   A live check is a separate, network-gated job if it is ever wanted. Pretending
//   this file covers it would be worse than the gap.
//
// ⛔ This file does NOT red on the manifest's 'unaccounted' entries. Those are a
//   FINDING pending the owner's ruling on which list they belong in, and the
//   Unity case already fails on each of them by name. Two suites shouting the
//   same unresolved question would just get one of them switched off.
//
// Run: node --test test/
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const manifestPath = path.join(__dirname, '..', 'api', '_lib', 'dungeon-manifest.json');
const schemaPath = path.join(__dirname, '..', 'api', 'schema.sql');

const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));

/** The status vocabulary the CHECK constraint pins. Mirrors DungeonDoorState. */
const STATUSES = ['open', 'sealed', 'collapsed', 'rescue', 'flooded'];

/** Accounting values the manifest is allowed to carry. */
const ACCOUNTING = ['portal-gated', 'not-gated', 'unaccounted'];

/**
 * Every dungeon_id seeded into dungeon_status by api/schema.sql.
 * Parsed from the INSERT block rather than the whole file, so an id that happens
 * to appear in a comment elsewhere cannot fake a row into existence.
 */
function seededDungeonIds(sql) {
    const start = sql.indexOf('INSERT INTO dungeon_status');
    assert.notEqual(start, -1, 'api/schema.sql has no INSERT INTO dungeon_status block');
    // Do not stop at the first semicolon: authored SQL comments inside the VALUES
    // block may contain prose punctuation. The conflict clause is the structural
    // terminator for this seed statement and cannot be faked by comment wording.
    const end = sql.indexOf('ON CONFLICT (dungeon_id)', start);
    assert.notEqual(end, -1, 'the INSERT INTO dungeon_status block has no ON CONFLICT terminator');
    const block = sql.slice(start, end);

    const ids = [];
    const re = /\(\s*'([^']+)'\s*,\s*'([^']+)'\s*\)/g;
    let m;
    while ((m = re.exec(block)) !== null) {
        assert.ok(STATUSES.includes(m[2]),
            `schema.sql seeds dungeon '${m[1]}' with status '${m[2]}', which the CHECK constraint rejects`);
        ids.push(m[1]);
    }
    return ids;
}

test('manifest is well formed and carries no duplicate ids', () => {
    assert.equal(manifest.version, 1);
    assert.ok(Array.isArray(manifest.dungeons), 'manifest.dungeons must be an array');
    assert.ok(manifest.dungeons.length > 0, 'an empty manifest asserts nothing - it is a hollow pass');

    const seen = new Set();
    for (const row of manifest.dungeons) {
        assert.equal(typeof row.id, 'string');
        assert.match(row.id, /^dg_[a-z0-9_]+$/, `'${row.id}' is not a dungeon id`);
        assert.ok(!seen.has(row.id), `duplicate manifest entry for '${row.id}'`);
        seen.add(row.id);

        assert.ok(ACCOUNTING.includes(row.accounting),
            `'${row.id}' carries accounting '${row.accounting}', which is not one of ${ACCOUNTING.join('|')}`);
        assert.ok(typeof row.reason === 'string' && row.reason.trim().length >= 20,
            `'${row.id}' has no stated reason - an entry without one is exactly the softening this gate forbids`);
    }
});

test('every portal-gated dungeon has a dungeon_status row to flip', () => {
    const sql = fs.readFileSync(schemaPath, 'utf8');
    const seeded = new Set(seededDungeonIds(sql));

    const gated = manifest.dungeons.filter(d => d.accounting === 'portal-gated').map(d => d.id);
    assert.ok(gated.length > 0, 'no portal-gated dungeons in the manifest - the case would assert nothing');

    for (const id of gated) {
        assert.ok(seeded.has(id),
            `'${id}' is a gated dungeon door with NO row in dungeon_status. Since the owner's ` +
            `fail-closed ruling (2026-08-26, WO-1223: "not acesable if not in table") that means it ` +
            `is SHUT to every player, not merely un-closeable. Add a row to api/schema.sql AND to ` +
            `Neon - the seed uses ON CONFLICT DO NOTHING, so re-running schema.sql will not ` +
            `back-fill an already-provisioned database.`);
    }
});

test('schema.sql seeds no dungeon the client does not ship', () => {
    const sql = fs.readFileSync(schemaPath, 'utf8');
    const known = new Set(manifest.dungeons.map(d => d.id));

    for (const id of seededDungeonIds(sql)) {
        assert.ok(known.has(id),
            `api/schema.sql seeds a dungeon_status row for '${id}', which is in no client build. ` +
            `Either the client dropped it or the id is a typo - a row nothing queries is dead weight ` +
            `that reads as coverage in review.`);
    }
});

test('a not-gated dungeon is never also seeded a row', () => {
    const sql = fs.readFileSync(schemaPath, 'utf8');
    const seeded = new Set(seededDungeonIds(sql));

    for (const row of manifest.dungeons.filter(d => d.accounting === 'not-gated')) {
        assert.ok(!seeded.has(row.id),
            `'${row.id}' is recorded as outside this system ("${row.reason}") yet schema.sql seeds it a ` +
            `dungeon_status row. One of the two is wrong.`);
    }
});
