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
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const saveSrc = fs.readFileSync(
    path.join(__dirname, '..', 'api', 'game', 'save.js'), 'utf8');
const save = require('../api/game/save.js');

// ── 1. The judgement ─────────────────────────────────────────────────────────

test('an ABSENT schema_version is refused, not silently treated as v10', () => {
    assert.equal(typeof save.judgeSchemaVersion, 'function',
        'save.js exports no judgeSchemaVersion — the version is still being defaulted inline');

    for (const absent of [undefined, null, '', 'v38', NaN, {}]) {
        const r = save.judgeSchemaVersion(absent, 38);
        assert.equal(r.ok, false, `a missing/malformed version (${JSON.stringify(absent)}) was accepted`);
        assert.equal(r.code, 'SCHEMA_VERSION_MISSING');
    }
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

test('the stored version is read by the EXISTING prior-state SELECT, not a second query', () => {
    // A second round trip per save on the hottest endpoint in the game, to read a
    // column the query already had to visit, is the wrong trade.
    assert.match(saveSrc,
        /SELECT\s+game_state,\s*updated_at,\s*schema_version\s+FROM\s+player_data/,
        'schema_version is not being read by the prior-state SELECT');
});
