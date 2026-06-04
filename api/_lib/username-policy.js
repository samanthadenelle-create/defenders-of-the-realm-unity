// =============================================================================
// api/_lib/username-policy.js — server-side username safety gate (WO-129 §2.2)
// -----------------------------------------------------------------------------
// Usernames are the ONE free-text field in the product (chat stays templated —
// WO-129 §5), so they get the server-side gate: format validation + a profanity
// denylist with basic normalization (leetspeak / whitespace / repeats) so the
// public board stays clean (US-9). Kept server-side so the list can be updated
// without shipping the client.
//
// validateUsername(raw) → { ok:true, username } | { ok:false, error }
//   error ∈ 'USERNAME_TOO_SHORT' | 'USERNAME_TOO_LONG'
//          | 'USERNAME_INVALID_CHARS' | 'USERNAME_REJECTED'
// Uniqueness (USERNAME_TAKEN) is enforced by the DB unique index, NOT here.
// =============================================================================

const MIN_LEN = 3;
const MAX_LEN = 16;
// Letters, digits, underscore. No leading/trailing/length-padding whitespace, no
// PII-friendly punctuation. (The display layer trims; we store the trimmed form.)
const ALLOWED_RE = /^[A-Za-z0-9_]+$/;

// Profanity / abuse denylist. Server-side + extensible. Matched against a
// NORMALIZED form (see normalizeForMatch) so 'b!t<h', 'b1tch', 'bbiiitch' all
// collapse to the base word. Keep this conservative; expand via deploy as needed.
const DENYLIST = [
    'fuck', 'shit', 'bitch', 'cunt', 'asshole', 'nigger', 'nigga', 'faggot',
    'retard', 'rape', 'nazi', 'whore', 'slut', 'dick', 'pussy', 'cock',
    'admin', 'moderator', 'official', 'system', 'support',
];

// Leetspeak → letter folding for denylist matching.
const LEET_MAP = {
    '0': 'o', '1': 'i', '3': 'e', '4': 'a', '5': 's', '7': 't', '8': 'b',
    '@': 'a', '$': 's', '!': 'i', '|': 'i',
};

// Collapse a candidate to a comparison form: lowercase, de-leet, strip non-letters,
// then squash runs of the same letter (so 'b i i i tch' → 'bitch').
function normalizeForMatch(s) {
    const lowered = s.toLowerCase();
    let out = '';
    for (const ch of lowered) {
        const folded = LEET_MAP[ch] != null ? LEET_MAP[ch] : ch;
        if (folded >= 'a' && folded <= 'z') out += folded;
    }
    // squash repeated letters
    return out.replace(/(.)\1+/g, '$1');
}

function isClean(raw) {
    const norm = normalizeForMatch(raw);
    if (!norm) return true;
    for (const bad of DENYLIST) {
        // squash the denylist word the same way so 'niiigger' base-matches.
        const badNorm = bad.replace(/(.)\1+/g, '$1');
        if (norm.includes(badNorm)) return false;
    }
    return true;
}

function validateUsername(raw) {
    if (raw == null) return { ok: false, error: 'USERNAME_TOO_SHORT' };
    const username = String(raw).trim();

    if (username.length < MIN_LEN) return { ok: false, error: 'USERNAME_TOO_SHORT' };
    if (username.length > MAX_LEN) return { ok: false, error: 'USERNAME_TOO_LONG' };
    if (!ALLOWED_RE.test(username)) return { ok: false, error: 'USERNAME_INVALID_CHARS' };
    if (!isClean(username)) return { ok: false, error: 'USERNAME_REJECTED' };

    return { ok: true, username };
}

module.exports = {
    MIN_LEN,
    MAX_LEN,
    validateUsername,
    normalizeForMatch, // exported for tests
};
