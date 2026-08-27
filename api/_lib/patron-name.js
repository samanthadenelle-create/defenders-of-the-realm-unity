// =============================================================================
// api/_lib/patron-name.js -- the PUBLIC name a $500 Founder appears under on the
// Benefactors of the Realm wall (WO-1073, owner ruling 2026-08-27).
// -----------------------------------------------------------------------------
// The wall is GLOBAL and every kingdom reads it, so this is the second free-text
// field in the product after the username (_lib/username-policy.js), and it is
// the more dangerous one: a username sits on a leaderboard the player can leave,
// a patron name sits on a permanent honour roll that lifetime totals can never
// remove them from (WO-1073 section 3.4 -- an SPL transfer cannot reverse, so
// lifetime spend only ever grows).
//
// THREE RULES THE OWNER RULING FIXES, and they are all here rather than in the
// endpoint, so no future endpoint can accidentally skip one:
//
//   1. NEVER an account identity. The name is stored BESIDE the entitlement and
//      is never the wallet, never an email, never a real name. The charset bans
//      '@' outright (so no address shape can be typed at all) and
//      PATRON_NAME_RESEMBLES_WALLET refuses a name that is a run of the caller's
//      own base58 address -- the obvious "just put my wallet up there" move.
//   2. A LENGTH CAP. PATRON_NAME_MAX_LEN is 24, wider than a username's 16
//      because a house name ("Wardens of the Ashen Vale") is the point of the
//      rung, and narrow enough that one wall row is one line at any font size.
//   3. A PROFANITY / IMPERSONATION FILTER, reusing the username denylist
//      (identical normalisation: leetspeak folded, repeats squashed) plus the
//      RESERVED_TOKENS below, because "Official Elarion Staff" on a paid honour
//      roll reads as endorsement in a way it does not on a leaderboard.
//
// -- THE EDIT PATH, DECIDED ON PURPOSE (the ruling demands an explicit answer) -
// The name is EDITABLE, a bounded number of times: MAX_PATRON_NAME_EDITS (3)
// self-serve changes after the first set, each one re-running this entire gate.
// Neither extreme is right:
//   * NO edit path -- wall entry is permanent, so one typo or one regretted
//     handle is permanent public harm with no remedy, on a list the player
//     reached by PAYING. That is the worst outcome available.
//   * UNLIMITED edits -- the wall becomes a broadcast channel: a name can be
//     rotated faster than a human can moderate it, and a filter-evading name can
//     be swapped back the moment attention moves on.
// A small pinned allowance gives a real remedy while keeping the wall a stable
// honour roll. Exhausting it is deliberately not a dead end: it returns
// PATRON_NAME_EDITS_EXHAUSTED, a support decision, not a silent failure.
//
// validatePatronName(raw, { wallet }) -> { ok:true, patronName } | { ok:false, error }
// =============================================================================

const { normalizeForMatch, PROFANITY_DENYLIST } = require('./username-policy');

const PATRON_NAME_MIN_LEN = 3;
const PATRON_NAME_MAX_LEN = 24;

// Pinned, never derived from anything that moves.
const MAX_PATRON_NAME_EDITS = 3;

// A short fragment coincidentally shared with a base58 address means nothing;
// six or more characters of it is somebody publishing their address.
const WALLET_RESEMBLANCE_MIN_LEN = 6;

// ASCII only, by construction: letters, digits, space, apostrophe, hyphen,
// underscore. No '@' (email shapes), no combining marks, no zero-width joiners,
// no right-to-left override -- the homoglyph impersonation tricks are simply not
// expressible. Must start and end on an alphanumeric so a name cannot be padded
// to sort to the top of the wall.
const PATRON_NAME_ALLOWED_RE = /^[A-Za-z0-9][A-Za-z0-9 '_-]*[A-Za-z0-9]$/;

// Runs of punctuation/space are a layout attack on a fixed-width wall row.
const PATRON_NAME_RUN_RE = /[ '_-]{2,}/;

// Impersonation of the project, its staff, or its systems. The profanity and
// generic-authority words ('admin', 'moderator', 'official', 'system',
// 'support') already live in the username denylist and are reused, not copied.
const RESERVED_TOKENS = Object.freeze([
    'staff', 'gamemaster', 'developer', 'administrator', 'customersupport',
    'devteam', 'thedevs', 'realmstaff',
]);

const PatronNameError = Object.freeze({
    TOO_SHORT: 'PATRON_NAME_TOO_SHORT',
    TOO_LONG: 'PATRON_NAME_TOO_LONG',
    INVALID_CHARS: 'PATRON_NAME_INVALID_CHARS',
    REJECTED: 'PATRON_NAME_REJECTED',
    RESEMBLES_WALLET: 'PATRON_NAME_RESEMBLES_WALLET',
    TAKEN: 'PATRON_NAME_TAKEN',
    EDITS_EXHAUSTED: 'PATRON_NAME_EDITS_EXHAUSTED',
});

function squash(word) {
    return word.replace(/(.)\1+/g, '$1');
}

/** Profanity + impersonation, over the same normalised form as usernames. */
function isCleanPatronName(raw) {
    const norm = normalizeForMatch(raw);
    if (!norm) return true;
    for (const bad of PROFANITY_DENYLIST) {
        if (norm.includes(squash(bad))) return false;
    }
    for (const reserved of RESERVED_TOKENS) {
        if (norm.includes(squash(reserved))) return false;
    }
    return true;
}

/** True when the candidate publishes a recognisable run of the caller's address. */
function resemblesWallet(candidate, wallet) {
    if (typeof wallet !== 'string' || wallet.trim() === '') return false;
    const flat = String(candidate).toLowerCase().replace(/[^a-z0-9]/g, '');
    if (flat.length < WALLET_RESEMBLANCE_MIN_LEN) return false;
    return wallet.toLowerCase().includes(flat);
}

/**
 * The whole gate. Endpoints call THIS and nothing else -- uniqueness is the
 * database's job (a unique index -> 23505 -> PATRON_NAME_TAKEN) and eligibility
 * is the server aggregate's job (_lib/benefactors.js).
 */
function validatePatronName(raw, options) {
    if (raw == null) return { ok: false, error: PatronNameError.TOO_SHORT };
    const patronName = String(raw).trim();

    if (patronName.length < PATRON_NAME_MIN_LEN) return { ok: false, error: PatronNameError.TOO_SHORT };
    if (patronName.length > PATRON_NAME_MAX_LEN) return { ok: false, error: PatronNameError.TOO_LONG };
    if (!PATRON_NAME_ALLOWED_RE.test(patronName)) return { ok: false, error: PatronNameError.INVALID_CHARS };
    if (PATRON_NAME_RUN_RE.test(patronName)) return { ok: false, error: PatronNameError.INVALID_CHARS };
    if (!isCleanPatronName(patronName)) return { ok: false, error: PatronNameError.REJECTED };

    const wallet = options && options.wallet;
    if (resemblesWallet(patronName, wallet)) return { ok: false, error: PatronNameError.RESEMBLES_WALLET };

    return { ok: true, patronName: patronName };
}

module.exports = {
    MAX_PATRON_NAME_EDITS,
    PATRON_NAME_MAX_LEN,
    PATRON_NAME_MIN_LEN,
    PatronNameError,
    RESERVED_TOKENS,
    WALLET_RESEMBLANCE_MIN_LEN,
    validatePatronName,
};
