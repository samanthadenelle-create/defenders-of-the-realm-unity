'use strict';

// =============================================================================
// test/admin.sku.dropdown.test.js - WO-1599.
//   the oracle for the Command Center's SKU field: a DROPDOWN built from the
//   catalog the page already fetches, never a list typed into the page.
//
// Owner ask 2026-09-07, verbatim:
//   "Would it be possible to add in the command center a drop-down for the SKUs
//    that allowed me to just select the SKU from a drop-down list instead of
//    having to manually type it in?"
//
// WHAT IS ACTUALLY BEING PROVEN, and why each case is here.
// -----------------------------------------------------------------------------
//  1 THE LIST HAS ONE SOURCE. The options are built from state.skus - the
//    WO-1532 catalog view - and NO catalog sku appears as a literal anywhere in
//    the page. This case was RED against HEAD before the change: the old typed
//    field carried placeholder="hearth-spark", a real catalog sku, hardcoded into
//    the page. That is the exact shape of the duplicated state CLAUDE.md s2/s5/s16
//    keeps paying for, and the assertion is derived from the catalog rather than
//    from a list retyped here, so a NEW pack is covered the day it is authored.
//
//  2 THE TYPED FALLBACK SURVIVES. A pack authored straight into the database is
//    not in the catalog yet. A console that can only offer what the catalog knows
//    is a console that blocks a legitimate mint, so the free-text input is still
//    in the page behind a toggle.
//
//  3 A CATALOG OUTAGE DEGRADES IN WORDS, NOT IN COLOUR. state.skusErr renders the
//    select DISABLED, says why in a sentence, and leaves the typed field OPEN.
//    An empty-but-enabled dropdown would read as "there are no packs", which is
//    the same confident lie the SKUs tab already refuses to tell.
//
//  4 THE SUBMIT PATH READS THE FIELD, NOT THE OLD INPUT. $('ppack').value is gone;
//    one reader answers whichever half is live.
//
//  5 THE PAGE IS STILL 7-BIT ASCII (WO-1244 rule 6). The new markup is inside the
//    served template, so the house marks used in this repo's comments would ship
//    if they were written there.
//
// SERVER-SIDE VALIDATION IS UNTOUCHED BY DESIGN - api/_lib/ops.js still refuses an
// unknown sku, and the last case pins that this ticket did not soften it. The
// dropdown is presentation.
//
//   node --test test/admin.sku.dropdown.test.js
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const REPO = path.join(__dirname, '..');
const readSrc = (rel) => fs.readFileSync(path.join(REPO, rel), 'utf8');
const PAGE = require('../api/admin/console.js').PAGE;
const catalog = require('../api/_lib/sku-catalog');

// Same lint as test/command-center.test.js / test/admin.skus.view.test.js:
// comments stripped so prose is not mistaken for code, string literals left
// intact so a real hardcoded sku cannot hide inside one.
function stripComments(src) {
    return src.replace(/\/\*[\s\S]*?\*\//g, ' ').replace(/(^|[^:])\/\/[^\n]*/g, '$1 ');
}

// -- the render harness ------------------------------------------------------
// Shape borrowed verbatim from test/admin.skus.view.test.js so the page is
// exercised ONE way, not two. It re-opens the page's IIFE just far enough to
// reach renderPromos, changes not one byte of what is served, and throws if the
// page's shape moves rather than silently testing nothing.

function renderPromosHtml(mutateState) {
    const i = PAGE.indexOf('<script>');
    const j = PAGE.lastIndexOf('</script>');
    let js = PAGE.slice(i + 8, j).trim();
    const tail = '})();';
    assert.ok(js.endsWith(tail), 'the page script is no longer a plain IIFE - fix this harness');
    js = js.slice(0, -tail.length) +
        '\n  globalThis.__probe = { state: state, render: renderPromos };\n})();';

    const el = () => ({
        textContent: '', value: '', innerHTML: '', hidden: false,
        addEventListener() {}, setAttribute() {}, getAttribute() { return null; },
        querySelectorAll() { return []; }, querySelector() { return null; },
    });
    const ctx = {
        document: { getElementById: el, addEventListener() {} },
        window: { prompt() { return null; }, confirm() { return false; } },
        fetch: () => new Promise(() => {}),
        Date, Math, JSON, Number, String, Array, Object, isFinite, parseInt, Promise, console,
    };
    ctx.globalThis = ctx;
    vm.createContext(ctx);
    vm.runInContext(js, ctx);
    // The promos card needs an ops read to render at all; the code list itself is
    // not what this ticket touched, so it stays empty.
    const s = ctx.__probe.state;
    s.ops = { promos: { note: '', rows: [] } };
    mutateState(s);
    return ctx.__probe.render();
}

// The page's own escaper, mirrored, because a pack NAME legitimately carries an
// apostrophe ("Keeper's Satchel") and the option text is escaped on the way out.
// Asserting the raw name would fail on the page doing the right thing.
function esc(v) {
    return String(v).replace(/[&<>"']/g, (c) => (
        c === '&' ? '&amp;' : c === '<' ? '&lt;' : c === '>' ? '&gt;'
        : c === '"' ? '&quot;' : '&#39;'));
}

// A catalog body shaped exactly like GET /api/admin/stats?view=skus, built from
// the real library so a change to the row shape reaches this test.
function catalogBody() {
    return catalog.build();
}

// =============================================================================
// 1. ONE SOURCE FOR THE LIST
// =============================================================================

test('the sku field is a SELECT built from state.skus, not from a list in the page', () => {
    const src = stripComments(readSrc('api/admin/console.js'));
    assert.ok(src.indexOf('function skuFieldHtml') > 0,
        'there must be ONE builder, so a second sku field later is a call and not a copy');
    const builder = src.slice(src.indexOf('function skuFieldHtml'),
                              src.indexOf('function skuFieldValue'));
    assert.ok(builder.indexOf('state.skus') > 0, 'the options come from the fetched catalog');
    assert.ok(builder.indexOf('state.skusErr') > 0, 'and the failed read is handled in the builder');
    assert.ok(builder.indexOf('<select') > 0 && builder.indexOf('<option') > 0,
        'it must actually emit a select with options');
});

test('NO catalog sku is hardcoded anywhere in the page', () => {
    // Derived from the catalog, never retyped: a pack authored tomorrow is covered
    // the day it lands. RED against HEAD - the old typed field shipped
    // placeholder="hearth-spark", which is a real catalog sku.
    const src = stripComments(readSrc('api/admin/console.js'));
    const skus = catalog.packs().map((p) => String(p.sku)).filter(Boolean);
    assert.ok(skus.length > 0, 'the catalog must have packs, or this test proves nothing');
    for (const sku of skus) {
        assert.ok(src.indexOf("'" + sku + "'") < 0 && src.indexOf('"' + sku + '"') < 0,
            'the page hardcodes the sku ' + sku + '. The dropdown has exactly one source and it ' +
            'is the fetched catalog; a literal here is the copy that goes stale.');
    }
});

test('the old typed-only input is gone from the mint form', () => {
    assert.ok(PAGE.indexOf('<input id="ppack"') < 0,
        'the primary control is the dropdown now');
    assert.ok(PAGE.indexOf("skuFieldHtml('ppack'") > 0,
        'and the mint form builds it through the one builder');
});

// =============================================================================
// 2. THE TYPED FALLBACK
// =============================================================================

test('a free-text fallback still exists, so an unknown sku can never block a mint', () => {
    const src = stripComments(readSrc('api/admin/console.js'));
    assert.ok(src.indexOf('sku-typeit') > 0, 'there is a toggle');
    assert.ok(src.indexOf("id=\"' + id + '-text\"") > 0 || src.indexOf("' + id + '-text'") > 0,
        'and a text input the toggle reveals');
});

test('RENDERED: the dropdown lists every catalog pack as "name (sku)", with a none option', () => {
    const body = catalogBody();
    const html = renderPromosHtml((s) => { s.skus = body; s.skusErr = null; });

    assert.ok(html.indexOf('<select id="ppack"') >= 0, 'the field is a select');
    assert.ok(html.indexOf('<option value="">- none -</option>') >= 0,
        'the optional field opens on an explicit none, not on the first pack by accident');
    for (const p of body.packs) {
        assert.ok(html.indexOf('<option value="' + p.sku + '">') >= 0,
            p.sku + ' is missing from the dropdown');
        assert.ok(html.indexOf(esc((p.name || p.sku) + ' (' + p.sku + ')')) >= 0,
            p.sku + ' must read as its NAME first - the owner picks by what a pack IS');
    }
    // The fallback is present but not the live input while the catalog read.
    assert.ok(html.indexOf('id="ppack-text"') >= 0, 'the typed field is in the page');
    assert.ok(/id="ppack-text"[^>]*hidden/.test(html),
        'and it starts hidden, so exactly one input is live');
    assert.ok(html.indexOf('disabled') < 0 || html.indexOf('<select id="ppack" disabled') < 0,
        'a healthy catalog leaves the select enabled');
});

// =============================================================================
// 3. THE OUTAGE, IN WORDS
// =============================================================================

test('RENDERED: a failed catalog read disables the select, says why, and opens the typed field', () => {
    const html = renderPromosHtml((s) => { s.skus = null; s.skusErr = 'HTTP 500'; });

    assert.ok(html.indexOf('<select id="ppack" disabled>') >= 0,
        'an empty ENABLED dropdown would read as "there are no packs"');
    assert.ok(html.indexOf('COULD NOT READ') >= 0 && html.indexOf('HTTP 500') >= 0,
        'the reason is stated in WORDS - the owner is red/green colourblind');
    assert.ok(html.indexOf('unreadable') >= 0,
        'and the disabled control says so itself, not only the note beside it');
    assert.ok(!/id="ppack-text"[^>]*hidden/.test(html),
        'the typed field must be OPEN, or an outage blocks every mint');
    assert.ok(html.indexOf('- none -') < 0,
        'a broken read must never render as a legitimately empty list');
    assert.ok(html.indexOf('sku-typeit') < 0,
        'and no toggle back to a disabled select - that would leave two dead inputs');
});

// =============================================================================
// 4. THE SUBMIT PATH
// =============================================================================

test('the mint submits whichever half is live, through one reader', () => {
    const src = stripComments(readSrc('api/admin/console.js'));
    assert.ok(src.indexOf("$('ppack').value") < 0,
        'reading the select directly would ignore a typed sku');
    assert.ok(src.indexOf("rewardPackSku: skuFieldValue('ppack')") > 0,
        'the draft is built from the one reader');
    const reader = src.slice(src.indexOf('function skuFieldValue'));
    assert.ok(reader.indexOf('.hidden') > 0,
        'and the reader decides by which input is showing, so two values can never both count');
});

// =============================================================================
// 5. THE PAGE CONTRACT
// =============================================================================

test('the served page is still 7-bit ASCII from end to end', () => {
    for (let i = 0; i < PAGE.length; i++) {
        if (PAGE.charCodeAt(i) > 126) {
            assert.fail('non-ASCII at ' + i + ': ' + JSON.stringify(PAGE.slice(i - 40, i + 40)));
        }
    }
});

test('server-side sku validation is untouched - the dropdown is presentation only', () => {
    const ops = stripComments(readSrc('api/_lib/ops.js'));
    assert.ok(ops.indexOf('PACK_SKU_NOT_ASCII') > 0, 'ops.js still refuses a non-ASCII sku');
    assert.ok(ops.indexOf('PACK_SKU_TOO_LONG') > 0, 'and one that is too long');
    assert.ok(ops.indexOf('REWARD_EMPTY') > 0, 'and a code that would grant nothing');
});
