// =============================================================================
// tools/gen-tunable-manifest.mjs - WO-1328. DERIVE the balance-editor manifest
// spine from the ONE place knob defaults are allowed to live.
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-02, verbatim:
//   "should be in command center so you dont need to be a rocket scientist. a
//    area for skills, and tiers of skills or spells or almost anything (misc)
//    and they can have a simple UI that rives a json"
// preceded by "i have been screaming this for months."
//
// -----------------------------------------------------------------------------
// WHY THIS FILE EXISTS AT ALL, AND WHY IT IS A GENERATOR RATHER THAN A LIST.
// -----------------------------------------------------------------------------
// The Command Center balance editor has to PRINT a knob's shipping default -
// "OVERRIDDEN (default 100)" is the whole point of the surface, because the
// owner is red/green colourblind and the state has to be a WORD next to a
// NUMBER, not a dot.
//
// But api/_lib/tunables.js says, in capitals and on purpose:
//     THIS FILE HOLDS NO DEFAULTS, AND THAT IS THE DESIGN.
// Defaults live in the BUILD, in DeNelle.Core.Ops.RemoteTunables.Registry, and
// nowhere else, because a default written twice is a default that rots - the
// duplicated-state disease CLAUDE.md sections 2, 5, 15 and 16 each record a
// separate scar from.
//
// Hand-typing the defaults into a manifest would have made a FIFTH copy. So the
// spine of the manifest is GENERATED from RemoteTunables.cs instead, checked in
// as api/_lib/tunable-manifest.generated.json, and re-derived and compared by
// test/tunables-manifest.test.js on every run. If the .cs moves and the JSON
// does not, the oracle goes RED and NAMES THE TWO SOURCES THAT DISAGREE.
//
// What is generated: key, kind, default.        <- machine facts, one owner.
// What is hand-authored: area, label, plain      <- owner-facing prose, which is
//   English, safe min/max (api/_lib/           genuinely NEW information and is
//   tunable-manifest.js).                        not a copy of anything.
//
// -----------------------------------------------------------------------------
// USAGE
//     node tools/gen-tunable-manifest.mjs            # rewrite the JSON
//     node tools/gen-tunable-manifest.mjs --check    # exit non-zero on drift
//
// Judge it by the MARKER on the output, never the exit code (CLAUDE.md section 8,
// runbook section 0): TUNABLE_MANIFEST_GEN_OK / TUNABLE_MANIFEST_DRIFT /
// TUNABLE_MANIFEST_GEN_FAIL.
//
// No dependencies. ASCII only. Reads two files, writes one.
// =============================================================================

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '..');

export const REGISTRY_CS = 'Assets/_Modules/Core/Ops/RemoteTunables.cs';
export const GENERATED_JSON = 'api/_lib/tunable-manifest.generated.json';

/**
 * Parse DeNelle.Core.Ops.RemoteTunables and return the Registry in declaration
 * order as [{ key, kind, default }].
 *
 * The parse is deliberately STRICT and deliberately LOUD. A registry entry this
 * cannot resolve throws with the entry's text in the message, because silently
 * dropping a knob would produce a manifest that is quietly short one lever - a
 * failure the owner would only discover by not finding the knob she came for.
 *
 * @param {string} src contents of RemoteTunables.cs
 */
export function parseRegistry(src) {
    if (typeof src !== 'string' || !src.length) {
        throw new Error('RemoteTunables.cs was empty or unreadable');
    }

    // const string KeyFoo = "foo.bar";
    const strConsts = new Map();
    const strRe = /const\s+string\s+([A-Za-z_]\w*)\s*=\s*"([^"]*)"\s*;/g;
    for (let m = strRe.exec(src); m; m = strRe.exec(src)) strConsts.set(m[1], m[2]);

    // const int VerbosityVerbose = 2;
    const intConsts = new Map();
    const intRe = /const\s+int\s+([A-Za-z_]\w*)\s*=\s*(-?\d+)\s*;/g;
    for (let m = intRe.exec(src); m; m = intRe.exec(src)) intConsts.set(m[1], parseInt(m[2], 10));

    const start = src.indexOf('Registry =');
    if (start < 0) throw new Error('no "Registry =" in ' + REGISTRY_CS);
    const body = src.slice(start);

    const out = [];
    // new TunableSpec(<key>, TunableKind.<Kind>, <default>,
    const entryRe = /new\s+TunableSpec\s*\(\s*([A-Za-z_]\w*|"[^"]*")\s*,\s*TunableKind\.(Bool|Int)\s*,\s*(-?\d+|[A-Za-z_]\w*)\s*,/g;
    for (let m = entryRe.exec(body); m; m = entryRe.exec(body)) {
        const rawKey = m[1];
        const key = rawKey.startsWith('"')
            ? rawKey.slice(1, -1)
            : strConsts.get(rawKey);
        if (!key) throw new Error('registry entry names an unresolvable key const: ' + rawKey);

        const kind = m[2].toLowerCase();

        const rawDef = m[3];
        let def;
        if (/^-?\d+$/.test(rawDef)) def = parseInt(rawDef, 10);
        else if (intConsts.has(rawDef)) def = intConsts.get(rawDef);
        else throw new Error('registry entry for ' + key + ' names an unresolvable default const: ' + rawDef);

        out.push({ key, kind, default: def });
    }

    if (!out.length) throw new Error('parsed zero knobs out of ' + REGISTRY_CS + ' - the parse, not the registry, is what changed');
    return out;
}

/** Re-derive the spine from disk. Used by the generator AND by the oracle. */
export function deriveFromDisk(repoRoot) {
    const root = repoRoot || REPO;
    const src = fs.readFileSync(path.join(root, REGISTRY_CS), 'utf8');
    return parseRegistry(src);
}

/** The exact bytes the generated file should hold for a given spine. */
export function renderJson(spine) {
    return JSON.stringify({
        _generated: 'DO NOT HAND-EDIT. Produced by tools/gen-tunable-manifest.mjs from ' +
            REGISTRY_CS + ' - the ONE place a knob default may live. Hand-authored ' +
            'presentation (area, label, plain English, safe range) lives in ' +
            'api/_lib/tunable-manifest.js and is joined onto this spine at require time. ' +
            'test/tunables-manifest.test.js re-derives this and goes RED naming which two ' +
            'sources disagree.',
        source: REGISTRY_CS,
        knobs: spine,
    }, null, 4) + '\n';
}

function main() {
    const check = process.argv.includes('--check');
    let spine;
    try {
        spine = deriveFromDisk(REPO);
    } catch (err) {
        console.log('TUNABLE_MANIFEST_GEN_FAIL ' + (err && err.message));
        process.exitCode = 1;
        return;
    }

    const target = path.join(REPO, GENERATED_JSON);
    const next = renderJson(spine);
    const prev = fs.existsSync(target) ? fs.readFileSync(target, 'utf8') : null;

    if (check) {
        if (prev === next) {
            console.log('TUNABLE_MANIFEST_GEN_OK knobs=' + spine.length + ' (checked, no drift)');
        } else {
            console.log('TUNABLE_MANIFEST_DRIFT ' + GENERATED_JSON + ' does not match ' + REGISTRY_CS +
                ' - run: node tools/gen-tunable-manifest.mjs');
            process.exitCode = 1;
        }
        return;
    }

    fs.writeFileSync(target, next, 'utf8');
    console.log('TUNABLE_MANIFEST_GEN_OK knobs=' + spine.length + ' -> ' + GENERATED_JSON +
        (prev === next ? ' (unchanged)' : ' (rewritten)'));
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
    main();
}
