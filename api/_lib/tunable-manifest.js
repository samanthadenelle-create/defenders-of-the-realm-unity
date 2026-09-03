// =============================================================================
// api/_lib/tunable-manifest.js - WO-1328. The JSON that DRIVES the Command
// Center balance editor.
// -----------------------------------------------------------------------------
// Owner ruling 2026-09-02, verbatim:
//   "and think about it, should be in command center so you dont need to be a
//    rocket scientist. a area for skills, and tiers of skills or spells or
//    almost anything (misc) and they can have a simple UI that rives a json"
// said in the same breath as: "i have been screaming this for months."
//
// Read the second sentence as the requirement. The point is not "add a page".
// The point is that changing one balance number must stop costing a specialist:
// today it is either a thirty-minute rebuild or a PowerShell command with an
// exact key name, and the only person who can judge feel is on a phone.
//
// -----------------------------------------------------------------------------
// ⛔ THIS FILE IS NOT A COPY OF THE KNOB LIST. IT IS A JOIN.
// -----------------------------------------------------------------------------
// Three facts about a knob, three owners, and none of them written twice:
//
//   key + kind + default   ->  DERIVED from DeNelle.Core.Ops.RemoteTunables.Registry
//                              by tools/gen-tunable-manifest.mjs into
//                              ./tunable-manifest.generated.json. The build is the
//                              only place a default may live (api/_lib/tunables.js
//                              says so in capitals, and means it).
//
//   "may this key be written at all"
//                          ->  the TUNABLE_KEYS ALLOWLIST in ./tunables.js, which
//                              is a spell-check at write time and never a source
//                              of truth.
//
//   area + label + plain English + safe range
//                          ->  PRESENTATION below. Hand-authored, and genuinely
//                              NEW information: nowhere else in this repo says
//                              which of the owner's four areas a knob belongs to
//                              or what it reads like to a human on a phone.
//
// build() joins the three and REFUSES to invent. A key present in one source and
// absent from another is reported by mismatches() as a defect that NAMES THE TWO
// SOURCES - it is never papered over with a default, because a manifest that
// quietly drops a lever is a lever the owner cannot find and will not know to
// look for. test/tunables-manifest.test.js drives that oracle and is the thing
// that goes red.
//
// -----------------------------------------------------------------------------
// ⛔ SERVER-AUTHORITATIVE VALUES ARE PERMANENTLY OUT OF SCOPE.
// -----------------------------------------------------------------------------
// Prices, entitlements, grants and purchase amounts (api/_lib/purchase-catalog.js)
// must NEVER appear in this manifest or on the page it drives. The game takes real
// money on mainnet; a client-side override of a price is an exploit, not a feature.
// This is stated here, in the manifest itself, AND printed on the page, so that a
// future seat adding "just one more knob" has to walk past the rule twice.
//
// The rail this rides is described end to end in docs/PROD022_TUNABLE_FLAGS.md.
//
// CommonJS, no dependencies. Files under api/_lib/ are NOT routed by Vercel
// (leading underscore), so this is a library, never an endpoint. ASCII only:
// every string here is rendered into the served console page, which
// test/command-center.test.js pins as 7-bit ASCII end to end.
// =============================================================================

const generated = require('./tunable-manifest.generated.json');
const { TUNABLE_KEYS } = require('./tunables');

/**
 * The owner's four areas, in the order she named them, and what each one is FOR.
 * An area with no knobs still renders - it is how the page says "this lever does
 * not exist yet" instead of silently having no opinion.
 */
const AREAS = [
    {
        id: 'skills',
        title: 'Skills',
        blurb: 'How strong a hero ability is, and how fast it comes back.',
    },
    {
        id: 'tiers',
        title: 'Tiers',
        blurb: 'How much a skill or a structure gains per level or rank.',
    },
    {
        id: 'spells',
        title: 'Spells',
        blurb: 'What a cast does and how it looks: damage, healing, drain, and spell visuals.',
    },
    {
        id: 'misc',
        title: 'Misc',
        blurb: 'Everything else that ships as a number: loading policy, retries, ' +
               'trace volume. Change these only when chasing a bug.',
    },
];

/**
 * HAND-AUTHORED PRESENTATION, keyed by knob key.
 *
 *   area     one of AREAS[].id
 *   label    what the owner reads. Never the key.
 *   what     plain English. What moving it actually does, and what the shipped
 *            value means, in a sentence a person can act on.
 *   min/max  the SAFE range this surface will submit. Not a security boundary -
 *            api/_lib/tunables.js validates independently and the client clamps
 *            again - it is a guard rail against a fat thumb on a phone.
 *   risk     one short sentence shown under the control when the knob is not an
 *            ordinary balance dial. Optional.
 *
 * ⭐ ADDING A LEVER LATER IS A DATA EDIT, NOT A UI EDIT: add the knob to
 * RemoteTunables.Registry and to TUNABLE_KEYS, re-run
 * node tools/gen-tunable-manifest.mjs, and add one entry here. The page grows a
 * card on its own. That is what "a simple UI that rives a json" buys.
 */
const PRESENTATION = {
    'combat.drainReturnPct': {
        area: 'spells',
        label: 'Drain healing return',
        what: 'How much of the damage a drain spell deals comes back to the caster as ' +
              'healing, as a percent. The game ships at 60, which you chose so that drain ' +
              'helps stave off rather than run the show - a player using it well survives a ' +
              'fight they would have lost, but cannot stand still and win by attrition. 100 ' +
              'means the caster heals for exactly the damage dealt, which is how it used to ' +
              'behave. Lower it further to make the mage squishier. This covers EVERY drain: ' +
              'Syphon Essence, the mage Drain, and the ranger Healing Shot.',
        min: 0,
        max: 1000,
    },

    // ---- WO-1330: the three levers of the ONE over-time engine. Every
    // damage-over-time and every heal-over-time effect in the game reads these, so
    // the copy talks about "all of them" rather than naming one spell. The tick
    // knob is described as a BEAT you can count, never as a look - rhythm is
    // carrying signal here that colour cannot.
    'combat.overTimeTickMs': {
        area: 'spells',
        label: 'Over-time pulse beat',
        what: 'How often an effect that works over time lands one pulse, in ' +
              'milliseconds. The game ships at 1000 - one pulse a second, a row of ' +
              'separate thuds you can count. 250 turns the same effect into a fast ' +
              'continuous drain; 2000 into slow heavy hits. It does NOT change how much ' +
              'is dealt or healed in total: a faster beat always means smaller pulses. ' +
              'This is purely how the effect READS. Covers every burn, every poison and ' +
              'every regen at once.',
        min: 50,
        max: 60000,
    },

    'combat.overTimeMagnitudePct': {
        area: 'spells',
        label: 'Over-time strength',
        what: 'Scales how hard each over-time pulse hits or heals, as a percent of the ' +
              'numbers written in the ability files. The game ships at 100, meaning ' +
              'exactly what is authored. 50 halves every damage-over-time AND every regen ' +
              'at the same time; 0 makes them do nothing without deleting anything. Use it ' +
              'to ask whether over-time effects as a whole are pulling their weight ' +
              'against straight burst damage.',
        min: 0,
        max: 1000,
    },

    'combat.overTimeDurationPct': {
        area: 'spells',
        label: 'Over-time duration',
        what: 'Scales how long an over-time effect lasts, as a percent of the seconds ' +
              'written in the ability files. The game ships at 100. Raising it adds ' +
              'pulses, so unlike the strength dial it changes the TOTAL amount dealt or ' +
              'healed. Kept separate from strength on purpose: "each pulse hurts more" ' +
              'and "it lasts longer" feel completely different even when the totals match, ' +
              'and one dial could not tell you which you preferred.',
        min: 0,
        max: 1000,
    },

    // WO-1327 landed these two while WO-1328 was being built, and the oracle in
    // test/tunables-manifest.test.js went red naming them within the minute -
    // which is the entire argument for deriving the spine instead of typing it.
    'vfx.particleBouncePct': {
        area: 'spells',
        label: 'Spell particles: bounce off the world',
        what: 'How bouncy a spell particle is when it hits the ground or a wall, as a ' +
              'percent. The game ships at 0, meaning a particle stops where it lands ' +
              'instead of ricocheting back at the caster. 100 leaves the art pack exactly ' +
              'as its artist authored it. Anything between tightens it part way; it can ' +
              'never make an effect bouncier than the artist made it.',
        min: 0,
        max: 100,
    },
    'vfx.maxParticleLights': {
        area: 'spells',
        label: 'Spell particles: real lights per effect',
        what: 'How many real-time lights one spell effect may light the scene with at ' +
              'once. The game ships at 4. The big fire spell was authored with 25, which ' +
              'is what a phone struggles with. 0 turns particle lights off completely.',
        min: 0,
        max: 25,
        risk: 'This is a phone performance dial as much as a look. Raising it makes ' +
              'spells prettier and the frame rate worse.',
    },

    // ---- the PROD-022 loading knobs. Not balance. They live under Misc because
    // they are numbers that ship, and the owner asked for "almost anything (misc)",
    // but every one of them says out loud that it is a bug-hunting lever.
    'pi.eagerStructureWarm': {
        area: 'misc',
        label: 'Pi Browser: load all building art up front',
        what: 'ON makes the Pi Browser build load and keep every building model at ' +
              'startup instead of fetching each one when it is first needed. Slower ' +
              'start, fewer fetches later. Ships OFF.',
        min: 0,
        max: 1,
        risk: 'Bug-hunting lever for the Pi Browser crash loop. Takes effect on the ' +
              'next launch of the app, not immediately.',
    },
    'pi.awaitInitBeforeFirstLoad': {
        area: 'misc',
        label: 'Pi Browser: wait for the asset system before the first request',
        what: 'ON makes the Pi Browser build finish setting up its asset system before ' +
              'it asks for the first model, queueing anything asked for meanwhile. ' +
              'Ships OFF.',
        min: 0,
        max: 1,
        risk: 'Bug-hunting lever for the Pi Browser crash loop. Takes effect on the ' +
              'next launch of the app, not immediately.',
    },
    'pi.disableRemoteStructureArt': {
        area: 'misc',
        label: 'Pi Browser: stop downloading building art entirely',
        what: 'ON makes the Pi Browser build never request building art. Buildings show ' +
              'their placeholder instead. The town still works. Ships OFF.',
        min: 0,
        max: 1,
        risk: 'Deliberately trades how the game LOOKS for a clean answer about whether ' +
              'art downloads are what is crashing Pi Browser. Next launch, not immediate.',
    },
    'assets.maxConcurrentRequests': {
        area: 'misc',
        label: 'Art downloads allowed at once',
        what: 'How many art downloads may run at the same time. 0 means what ships ' +
              'today: no explicit cap. 1 or more installs a hard ceiling everywhere.',
        min: 0,
        max: 8,
        risk: 'Bug-hunting lever. 0 is not "none allowed" - it means "no cap", which is ' +
              'the shipped behaviour.',
    },
    'pi.requestTimeoutSeconds': {
        area: 'misc',
        label: 'Pi Browser: seconds before an art download gives up',
        what: 'How long one art download may take in Pi Browser before it is abandoned. ' +
              'Ships at 20 seconds.',
        min: 5,
        max: 120,
        risk: 'Bug-hunting lever. Setting this to 0 would not mean "no timeout" - it is ' +
              'why Reset exists.',
    },
    'assets.maxRequestAttempts': {
        area: 'misc',
        label: 'Retries per piece of art',
        what: 'How many times one piece of art is re-requested before it is given up on ' +
              'for the rest of the session. Ships at 3.',
        min: 1,
        max: 10,
        risk: 'Bug-hunting lever. Too high and the retries themselves become the load.',
    },
    'visuals.missLogCap': {
        area: 'misc',
        label: 'Full log lines per missing model',
        what: 'How many full "could not find this model" lines are written before the ' +
              'log drops to a short repeated line. It never goes silent. Ships at 3.',
        min: 0,
        max: 50,
        risk: 'Diagnostics volume only. Failures are always logged, at every setting.',
    },
    'trace.assetVerbosity': {
        area: 'misc',
        label: 'Asset log detail',
        what: 'How chatty the asset system is in the logs. 2 is what ships (everything). ' +
              '1 is milestones only. 0 is no routine narration.',
        min: 0,
        max: 2,
        risk: 'Only the SUCCESS narration dims. Warnings and failures are always logged ' +
              'and cannot be turned off.',
    },
};

/** The verbatim boundary sentence. Printed on the page; asserted by the oracle. */
const OUT_OF_SCOPE_NOTICE =
    'Prices, purchase amounts, entitlements and grants are NEVER editable here and never ' +
    'will be. Those are decided by the server (api/_lib/purchase-catalog.js) because the ' +
    'game takes real money; a value the phone could override would be an exploit, not a ' +
    'feature. This page edits gameplay numbers only.';

/** Clear is not zero, in one sentence. Printed on the page; asserted by the oracle. */
const CLEAR_IS_NOT_ZERO_NOTICE =
    'Reset REMOVES the override so the knob answers whatever the installed game says, ' +
    'which is not the same as setting it to 0. Resetting the Pi Browser art timeout ' +
    'returns it to 20 seconds; setting it to 0 would mean zero seconds.';

/** Map from key to its allowlist entry, so the join can prove the key is writable. */
function allowlistMap() {
    const m = new Map();
    for (const spec of TUNABLE_KEYS) m.set(spec.key, spec);
    return m;
}

/**
 * Every way the three sources can disagree, as a list of plain-English defects.
 * EMPTY MEANS AGREEMENT. Each string NAMES THE TWO SOURCES, because "the manifest
 * is wrong" is not an actionable sentence at 2am and "the build registry has
 * combat.foo, the manifest does not" is.
 *
 * @returns {string[]}
 */
function mismatches() {
    const out = [];
    const allow = allowlistMap();
    const spine = Array.isArray(generated.knobs) ? generated.knobs : null;

    if (!spine) {
        out.push('tunable-manifest.generated.json has no knobs array - regenerate it with ' +
                 'node tools/gen-tunable-manifest.mjs');
        return out;
    }

    const areaIds = new Set(AREAS.map((a) => a.id));
    const seen = new Set();

    for (const knob of spine) {
        const key = knob && knob.key;
        if (!key) { out.push('tunable-manifest.generated.json holds a knob with no key'); continue; }
        seen.add(key);

        const spec = allow.get(key);
        if (!spec) {
            out.push('BUILD REGISTRY vs SERVER ALLOWLIST: RemoteTunables.Registry has "' + key +
                     '" but TUNABLE_KEYS in api/_lib/tunables.js does not - the server would ' +
                     'REFUSE every write to it.');
        } else if (spec.kind !== knob.kind) {
            out.push('BUILD REGISTRY vs SERVER ALLOWLIST: "' + key + '" is ' + knob.kind +
                     ' in RemoteTunables.Registry and ' + spec.kind +
                     ' in TUNABLE_KEYS (api/_lib/tunables.js).');
        }

        const pres = PRESENTATION[key];
        if (!pres) {
            out.push('BUILD REGISTRY vs CONSOLE MANIFEST: RemoteTunables.Registry has "' + key +
                     '" but PRESENTATION in api/_lib/tunable-manifest.js does not - the knob ' +
                     'would be INVISIBLE in the Command Center.');
            continue;
        }
        if (!areaIds.has(pres.area)) {
            out.push('CONSOLE MANIFEST: "' + key + '" claims area "' + pres.area +
                     '", which is not one of ' + AREAS.map((a) => a.id).join(' / ') + '.');
        }
        if (!(typeof pres.min === 'number') || !(typeof pres.max === 'number') || pres.min > pres.max) {
            out.push('CONSOLE MANIFEST: "' + key + '" has no usable safe range.');
        } else if (knob.default < pres.min || knob.default > pres.max) {
            out.push('BUILD REGISTRY vs CONSOLE MANIFEST: "' + key + '" ships at ' + knob.default +
                     ' but the manifest safe range is ' + pres.min + '..' + pres.max +
                     ' - the page could not offer the value the game actually ships with.');
        }
        if (knob.kind === 'bool' && (pres.min !== 0 || pres.max !== 1)) {
            out.push('CONSOLE MANIFEST: "' + key + '" is a bool but its safe range is not 0..1.');
        }
        if (!pres.label || !pres.what) {
            out.push('CONSOLE MANIFEST: "' + key + '" has no label or no plain-English description.');
        }
    }

    for (const spec of TUNABLE_KEYS) {
        if (!seen.has(spec.key)) {
            out.push('SERVER ALLOWLIST vs BUILD REGISTRY: TUNABLE_KEYS in api/_lib/tunables.js ' +
                     'has "' + spec.key + '" but RemoteTunables.Registry does not - no build ' +
                     'reads it, so writing it would do nothing.');
        }
    }

    for (const key of Object.keys(PRESENTATION)) {
        if (!seen.has(key)) {
            out.push('CONSOLE MANIFEST vs BUILD REGISTRY: PRESENTATION in ' +
                     'api/_lib/tunable-manifest.js has "' + key + '" but ' +
                     'RemoteTunables.Registry does not - the page would show a lever that ' +
                     'moves nothing.');
        }
    }

    return out;
}

/**
 * The manifest the page is driven by: areas in the owner's order, each carrying
 * its knobs in registry order.
 *
 * ⚠ IT REFUSES TO SHIP A DISAGREEMENT SILENTLY. Any knob this cannot fully join
 * is DROPPED from the areas and NAMED in `defects`, and the page prints those
 * words. A balance editor that quietly hides a lever is worse than one that says
 * it is broken, because the owner would go on believing the number cannot move.
 *
 * @returns {{version:number, areas:Array, defects:string[], notices:object}}
 */
function build() {
    const defects = mismatches();
    const allow = allowlistMap();
    const spine = Array.isArray(generated.knobs) ? generated.knobs : [];

    const byArea = new Map(AREAS.map((a) => [a.id, []]));
    for (const knob of spine) {
        const pres = knob && PRESENTATION[knob.key];
        if (!pres) continue;
        if (!byArea.has(pres.area)) continue;
        if (!allow.has(knob.key)) continue;          // not writable => not offered
        byArea.get(pres.area).push({
            key: knob.key,
            kind: knob.kind,
            def: knob.default,
            label: pres.label,
            what: pres.what,
            min: pres.min,
            max: pres.max,
            risk: pres.risk || null,
        });
    }

    return {
        version: 1,
        source: generated.source,
        areas: AREAS.map((a) => ({
            id: a.id,
            title: a.title,
            blurb: a.blurb,
            knobs: byArea.get(a.id) || [],
        })),
        defects: defects,
        notices: {
            outOfScope: OUT_OF_SCOPE_NOTICE,
            clearIsNotZero: CLEAR_IS_NOT_ZERO_NOTICE,
        },
    };
}

module.exports = {
    AREAS,
    PRESENTATION,
    OUT_OF_SCOPE_NOTICE,
    CLEAR_IS_NOT_ZERO_NOTICE,
    mismatches,
    build,
};
