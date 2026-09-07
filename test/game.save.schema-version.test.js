'use strict';

// =============================================================================
// WO-1457 — /api/game/save must never let schema_version go BACKWARDS.
// -----------------------------------------------------------------------------
// The upsert wrote `schema_version = EXCLUDED.schema_version` and defaulted an
// ABSENT version to 10. The live client schema is v38 (SaveSchema.CurrentVersion),
// so an old build, a replayed request, or a payload that simply omits the field
// stamped the row back to 10 *while writing current-shaped state*. The next load
// then runs the wrong migration chain over data that is not shaped for it.
//
// Two independent defences, and this file proves BOTH, because either alone is a
// near-miss:
//   1. A BEHAVIOURAL judgement (`judgeSchemaVersion`) that refuses the request —
//      a stale client is visible instead of quietly corrupting.
//   2. GREATEST() on the upsert itself — the row cannot regress even if a future
//      caller reaches the SQL by some path the judgement did not cover.
//
// The judgement is exported as a pure function precisely so it can be proven
// without a database; the SQL half is a source-shape assertion (there is no
// Postgres in this test runner, and GREATEST()'s semantics are Postgres's job to
// keep, not ours to re-test).
//
// ⛔ THE ABSENT CASE FLIPPED 2026-09-07 — a P0 field outage, and this file asserted
// the broken behaviour. WO-1457 ruled an absent version "a malformed payload" and
// this test pinned the 400. But the SHIPPED Android client never sends the field
// (the fix is in the working tree and reaches players only with the NEXT build),
// so the moment the refusal deployed, every save from every device in the field
// was refused: saves landing at 09-07 07:49 local, and from 07:53 every POST
// answering 400 {"ok":false,"code":"SCHEMA_VERSION_MISSING"}. Cloud save was down
// for everyone.
//
// The corrected ruling, and what this file now proves:
//   absent/unparseable = UNKNOWN, "do not touch the version" — ACCEPT the write,
//   leave schema_version exactly as it is, and keep the absence VISIBLE (a
//   `schemaVersionNote` in the 200 body + the same audit row the refusal wrote).
//   Never "malformed, drop the save"; and still never the original "default to 10".
// The DOWNGRADE refusal and the GREATEST() clamp are UNCHANGED and still pinned —
// a client that NAMES an older version has made a claim, and that claim is refused.
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const saveSrc = fs.readFileSync(
    path.join(__dirname, '..', 'api', 'game', 'save.js'), 'utf8');
const save = require('../api/game/save.js');

// ── 1. The judgement ─────────────────────────────────────────────────────────

test('an ABSENT schema_version is ACCEPTED, leaves the version untouched, and says so', () => {
    assert.equal(typeof save.judgeSchemaVersion, 'function',
        'save.js exports no judgeSchemaVersion — the version is still being defaulted inline');

    for (const absent of [undefined, null, '', 'v38', NaN, {}]) {
        const label = JSON.stringify(absent);
        const r = save.judgeSchemaVersion(absent, 38);
        // 1. ACCEPTED — this is the assertion that keeps the field alive. Every
        //    client shipped before today omits the field; refusing them was the outage.
        assert.equal(r.ok, true,
            `a version-less payload (${label}) was refused — that is the 2026-09-07 outage, ` +
            'every shipped client omits the field');
        // 2. UNTOUCHED — null means "write no version", never 10 and never the stored
        //    value echoed back as if the client had claimed it.
        assert.equal(r.version, null,
            `an absent version (${label}) produced a version to write (${r.version}) — ` +
            'the server must not invent one');
        assert.notEqual(r.version, 10, 'the original defaulting bug is back');
        // 3. VISIBLE — an accepted absence that leaves no trace is the silent failure
        //    CLAUDE.md §12 forbids.
        assert.equal(r.note, 'SCHEMA_VERSION_ABSENT',
            `the absence of a version (${label}) is not reported to the caller`);
        assert.equal(r.code, undefined, 'an accepted save must not carry a refusal code');
    }
});

test('a FIRST save with NO version and NO stored row still writes — and invents no version', () => {
    // The brand-new-player case: nothing to compare against AND nothing declared.
    for (const stored of [null, undefined, NaN]) {
        const r = save.judgeSchemaVersion(undefined, stored);
        assert.equal(r.ok, true, 'a brand-new player with an old client could not save at all');
        assert.equal(r.version, null,
            'a first save with no declared version must write NO version, not 10');
        assert.equal(r.note, 'SCHEMA_VERSION_ABSENT');
    }
});

test('the version-less write names schema_version NOWHERE — not a value the server picked', () => {
    // ⚠ WHY THIS IS A SOURCE-SHAPE ASSERTION AND NOT "the stored value is NULL":
    // there is no Postgres in this runner, and the column is INTEGER NOT NULL
    // DEFAULT 10 (api/schema.sql:61 + the ALTER at :72), so a literal NULL can never
    // be written — passing one would raise a not-null violation on the INSERT arm and
    // lose the save of every new player. What IS provable, and what actually matters,
    // is that the application never chooses a version: the version-less statement
    // omits the column from the INSERT list and from the SET clause entirely, so an
    // existing row keeps whatever it had and a new row takes the DATABASE's default.
    const versionless = saveSrc.match(
        /INSERT INTO player_data \(player_id, game_state, trust, updated_at\)[\s\S]*?updated_at = NOW\(\)/);
    assert.ok(versionless,
        'there is no version-less upsert — an absent version is still writing a schema_version');
    assert.doesNotMatch(versionless[0], /schema_version/,
        'the version-less upsert still names schema_version; it must touch the column at all');

    // And no arm of the executable source may hand the DB a version literal.
    const executable = saveSrc.replace(/^\s*\/\/.*$/gm, '')
                              .replace(/^\s*--.*$/gm, '')
                              .replace(/\/\*[\s\S]*?\*\//g, '');
    assert.doesNotMatch(executable, /schema_version\s*(=|,)\s*10\b/,
        'a literal v10 is being written to schema_version again');
});

test('a DOWNGRADE is refused with its own named code', () => {
    const r = save.judgeSchemaVersion(10, 38);
    assert.equal(r.ok, false, 'a v10 client was allowed to stamp a v38 row back to 10');
    assert.equal(r.code, 'SCHEMA_VERSION_DOWNGRADE',
        'a downgrade must be distinguishable from a malformed payload, or a stale build stays invisible');
});

test('an EQUAL version is accepted — the ordinary case must still save', () => {
    // memory: prove-the-success-path-not-just-the-refusal. A guard that refuses
    // every save also satisfies "downgrade refused".
    const r = save.judgeSchemaVersion(38, 38);
    assert.equal(r.ok, true, 'the ordinary same-version save was refused');
    assert.equal(r.version, 38);
});

test('an UPGRADE is accepted and carries the NEW version forward', () => {
    const r = save.judgeSchemaVersion(39, 38);
    assert.equal(r.ok, true);
    assert.equal(r.version, 39);
});

test('a FIRST save (no stored row) is accepted at whatever version it declares', () => {
    for (const stored of [null, undefined, NaN]) {
        const r = save.judgeSchemaVersion(38, stored);
        assert.equal(r.ok, true, 'a brand-new player could not save');
        assert.equal(r.version, 38);
    }
});

// ── 2. The SQL half ──────────────────────────────────────────────────────────

test('the upsert clamps with GREATEST and no longer defaults to 10', () => {
    assert.match(saveSrc,
        /schema_version\s*=\s*GREATEST\(\s*player_data\.schema_version\s*,\s*EXCLUDED\.schema_version\s*\)/,
        'the upsert still overwrites schema_version unconditionally');
    const executable = saveSrc.replace(/^\s*\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
    assert.doesNotMatch(executable, /schemaVersion\s*\?\?\s*10/,
        'an absent version is still being written as v10');
});

test('an accepted absence is VISIBLE — a note in the 200 body and its own audit row', () => {
    // A silent accept would be worse than the refusal it replaces: the field would
    // heal and nobody would ever learn how many clients still omit the version.
    assert.match(saveSrc, /schemaVersionNote:\s*schemaVersionNote\s*\|\|\s*undefined/,
        'the 200 response does not carry schemaVersionNote');
    assert.match(saveSrc, /logApiEvent\(sql,\s*playerId,\s*'save_schema_version_absent'/,
        'an absent version writes no audit row — the absence is invisible server-side');
    // The downgrade refusal keeps its own, distinct row. Reusing the "refused" event
    // name for a save we accepted would be a lie in the audit log.
    assert.match(saveSrc, /logApiEvent\(sql,\s*playerId,\s*'save_schema_version_refused'/,
        'the downgrade refusal lost its audit row');
});

test('the stored version is read by the EXISTING prior-state SELECT, not a second query', () => {
    // A second round trip per save on the hottest endpoint in the game, to read a
    // column the query already had to visit, is the wrong trade.
    assert.match(saveSrc,
        /SELECT\s+game_state,\s*updated_at,\s*schema_version\s+FROM\s+player_data/,
        'schema_version is not being read by the prior-state SELECT');
});
