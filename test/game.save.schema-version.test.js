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
    // there is no Postgres in this runner. What IS provable, and what actually
    // matters, is that the application never chooses a version — the version-less
    // statement omits the column from the INSERT list and from the SET clause
    // entirely, so an existing row keeps whatever it had and a new row takes whatever
    // the DATABASE does with an unnamed column.
    //
    // ⚠ AND WHAT THE DATABASE DOES CHANGED TODAY (2026-09-07, migration
    // api/migrations/20260907_0022_game_saves_schema_version_default.sql). This
    // comment used to say the column was INTEGER NOT NULL DEFAULT 10 so "a literal
    // NULL can never be written", and that a new row "takes the DATABASE's default".
    // That default WAS the WO-1457 corruption: a brand-new player on a version-less
    // client landed at 10 with current-shaped state, load.js returned the 10, and
    // ApplyBackendState ran the whole v10→current chain over state that was never
    // v10. 0022 drops the DEFAULT and the NOT NULL, so the unnamed column now lands
    // NULL = "never declared, do not migrate". The assertions below are unchanged and
    // still the right ones — only the consequence of omitting the column moved.
    // ⚠ THE COLUMN LIST IS NO LONGER FROZEN (widened 2026-09-07, WO-1598). It read
    // `(player_id, game_state, trust, updated_at)` verbatim, which pinned the incidental
    // membership of the list rather than the property under test — and broke the moment
    // reset_epoch legitimately joined BOTH arms. The version-less arm is now identified by
    // what makes it the version-less arm: it is the upsert that does NOT name
    // schema_version. That is the assertion, so it cannot pass for a shape reason again.
    const upserts = saveSrc.match(
        /INSERT INTO player_data \([^)]*\)[\s\S]*?updated_at\s*=\s*NOW\(\)/g) || [];
    assert.equal(upserts.length, 2,
        `expected exactly two upsert arms (version-less + versioned), saw ${upserts.length}`);
    const versionless = upserts.filter(u => !/schema_version/.test(u));
    assert.equal(versionless.length, 1,
        'there is no version-less upsert — an absent version is still writing a schema_version');
    assert.doesNotMatch(versionless[0], /schema_version/,
        'the version-less upsert still names schema_version; it must not touch the column at all');

    // And no arm of the executable source may hand the DB a version literal.
    const executable = saveSrc.replace(/^\s*\/\/.*$/gm, '')
                              .replace(/^\s*--.*$/gm, '')
                              .replace(/\/\*[\s\S]*?\*\//g, '');
    assert.doesNotMatch(executable, /schema_version\s*(=|,)\s*10\b/,
        'a literal v10 is being written to schema_version again');
});

test('⛔ a NULL stored version is UNKNOWN, never version ZERO', () => {
    // Migration 20260907_0022 made player_data.schema_version nullable, so the
    // prior-state SELECT can now hand this judgement a NULL for the first time. The
    // trap is that `Number(null)` is 0, NOT NaN — a bare Number()+isFinite records a
    // fabricated "version 0" as the stored prior, and that number is what an incident
    // would read in save_schema_version_refused's `stored` field.
    //
    // The path that matters end to end: a version-less client saves (row lands NULL),
    // the player upgrades to a version-declaring client, and saves again. That second
    // save MUST be accepted at its declared version.
    const upgraded = save.judgeSchemaVersion(38, null);
    assert.equal(upgraded.ok, true,
        'a client that starts declaring a version after a NULL-stored row must still save');
    assert.equal(upgraded.version, 38, 'and it carries its own declared version forward');

    // Belt: even read as 0, a downgrade check of `v < s` cannot refuse — nothing is
    // below zero. Pinned so a future stricter judgement cannot start refusing here
    // without this line going red first.
    assert.equal(save.judgeSchemaVersion(1, 0).ok, true,
        'a stored 0 must never become a reason to refuse a save');

    // And the READ side: the null must be filtered before it is coerced.
    const executable = saveSrc.replace(/^\s*\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
    assert.doesNotMatch(executable, /Number\(priorRows\[0\]\.schema_version\)/,
        'schema_version is being coerced with a bare Number() again — Number(null) is 0, ' +
        'so a NULL row would be recorded as a real stored version 0');
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
    // ⚠ MATCHED AS A MEMBER OF THE ONE SELECT, not as a fixed column list (widened
    // 2026-09-07, WO-1598, which added reset_epoch to the same statement for the same
    // reason). A frozen list pins the incidental ORDER of the columns and fails the next
    // time anything legitimately joins the query — which says nothing about whether a
    // second round trip was introduced, the property this case actually exists to hold.
    const priorSelect = saveSrc.match(/SELECT\s+game_state[^`]*?FROM\s+player_data/);
    assert.ok(priorSelect, 'the prior-state SELECT is gone');
    assert.match(priorSelect[0], /\bschema_version\b/,
        'schema_version is not being read by the prior-state SELECT');
    assert.equal((saveSrc.match(/FROM player_data/g) || []).length, 1,
        'save.js reads player_data more than once — the version must ride the existing SELECT');
});
