// =============================================================================
// channel-pin.mjs - pin the Discord tools to the channel they were set up for.
// -----------------------------------------------------------------------------
// WO-1175 Phase 2 creates a COMMUNITY Discord. tools/status-post.mjs and
// tools/discord-inbox.mjs are bound to the owner's PRIVATE development channel,
// and both files carry a hand-written warning not to repoint them at a public
// one. A comment is not a gate - CLAUDE.md section 16 is an entire section about
// exactly that failure ("a gate whose remedy is a human remembers a second
// command is not a gate"). This file turns the two warnings into a machine
// check, so the hazard the community server creates is closed before it exists.
//
// WHAT A REPOINT WOULD ACTUALLY COST, in each direction:
//   * status-post repointed at a community channel PUBLISHES gate markers,
//     commit hashes and suite counts to players. The game is LIVE; words to
//     players carry the weight of code.
//   * discord-inbox repointed at a community channel INGESTS anything any
//     player types into an inbox an agent reads. Every message is already
//     untrusted input, but a private channel bounds who can write it. A public
//     one does not.
//   * And the inbox carries a SECOND, quieter bug: state.json's watermark
//     belongs to the channel it was taken from. Point it somewhere else and the
//     baseline step is skipped, so the new channel's backlog lands in the queue
//     as if it were new traffic.
//
// TOFU (trust on first use), on purpose. Requiring the owner to pin by hand
// would leave the hazard open until she remembered - the same shape as the bug
// above. First good use records the fingerprint; every later run must match.
// This mirrors discord-inbox's own "first run BASELINES" convention rather than
// inventing a second one.
//
// ONE FILE, TWO CALLERS. Do not copy this logic into either tool. Section 16
// records what happened the last time a check was inlined into two chains: they
// drifted and one of them silently stopped checking.
//
// SECRETS: the value is NEVER stored, printed or returned. What is written and
// shown is a 12-hex prefix of its SHA-256 - irreversible, and not enumerable
// against a webhook token's entropy. Length and shape only, as with
// DATABASE_URL.
// =============================================================================

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

export const PIN_DIR = 'logs/ops-channel';

/** 12-hex prefix of SHA-256. Empty/absent input -> ''. Never reversible. */
export function fingerprint(value) {
  if (value == null) return '';
  const s = String(value).trim();
  if (!s) return '';
  return crypto.createHash('sha256').update(s, 'utf8').digest('hex').slice(0, 12);
}

export function pinPath(name, dir = PIN_DIR) {
  return path.resolve(dir, `${name}.pin`);
}

export function readPin(name, dir = PIN_DIR) {
  try {
    const raw = fs.readFileSync(pinPath(name, dir), 'utf8').trim();
    return /^[0-9a-f]{12}$/.test(raw) ? raw : null;
  } catch {
    return null;
  }
}

export function writePin(name, fp, dir = PIN_DIR) {
  if (!/^[0-9a-f]{12}$/.test(String(fp))) throw new Error('refusing to write a malformed pin');
  fs.mkdirSync(path.resolve(dir), { recursive: true });
  fs.writeFileSync(pinPath(name, dir), String(fp) + '\n', 'utf8');
}

/**
 * Decide whether `value` may be used under pin `name`.
 *
 * Returns { state, fp, pinned, path } where state is one of:
 *   'empty'    - nothing configured; the caller's own no-op path applies
 *   'unpinned' - no pin on disk yet; caller MAY proceed and should pin on success
 *   'match'    - pinned and identical; proceed
 *   'mismatch' - pinned and DIFFERENT; the caller must refuse. Fail closed.
 *
 * This function never writes. Pinning is a separate, explicit call so a caller
 * can wait for proof the target actually worked before recording it.
 */
export function checkPin(name, value, dir = PIN_DIR) {
  const fp = fingerprint(value);
  const pinned = readPin(name, dir);
  const p = pinPath(name, dir);
  if (!fp) return { state: 'empty', fp, pinned, path: p };
  if (!pinned) return { state: 'unpinned', fp, pinned, path: p };
  return { state: fp === pinned ? 'match' : 'mismatch', fp, pinned, path: p };
}

/** The one refusal sentence, so both tools word it identically. */
export function refusalLine(prefix, check) {
  return (
    `${prefix} pinned to a different channel (pinned ${check.pinned}, current ${check.fp}). ` +
    `Refusing to send or read. If the change is intended, delete ${check.path} and re-run.`
  );
}
