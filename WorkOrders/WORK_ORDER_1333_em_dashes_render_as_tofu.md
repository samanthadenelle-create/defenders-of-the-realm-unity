# WORK ORDER 1333 - Em dashes in DISPLAYED titles render as tofu boxes

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Player-facing strings / canon
**Type:** EXISTING defect (shipped copy)
**Minted:** 2026-09-02 (CLI) as an adjacent finding from WO-1332's sweep. Predates that ticket.
**Severity:** P3 - cosmetic, player-visible, and trivially fixable.

## The finding

WO-1332's spelling sweep found **U+2014 EM DASH in strings that are DISPLAYED**, not in comments:

- `Assets/Resources/Data/Canonical/lore-fragments.json:27`
- `Assets/Resources/Data/Canonical/healers-cottage.json:216`
- `Assets/Editor/DungeonSceneBuilder.cs:995-1028`

## Why this is a defect and not a style preference

CLAUDE.md and `START_HERE.md` are explicit: **player-facing TMP strings are ASCII-ONLY, because a
non-ASCII glyph the font does not carry renders as a TOFU BOX (a hollow rectangle) on the device.**
The rule exists from experience, and a tofu oracle already FAILS the regression on this class -
CJK brackets cost a full gate run on 2026-08-27.

These occurrences predate the current oracle's coverage, which is the same shape of gap WO-1332 just
closed for names: **the rule existed, and the specific files were unguarded.**

## The work

Replace the em dashes in DISPLAYED strings with ASCII equivalents (` - ` reads correctly and is what
the rest of the corpus uses). Sweep for the whole class while you are there, not just these three
sites - check also for the ellipsis character, smart quotes, and any other non-ASCII in a string that
reaches TMP.

⛔ **DISPLAYED strings only.** A non-ASCII character in a COMMENT is harmless and must be left alone -
rewriting comments inflates the diff and buries the real change. Classify before editing.

⚠ Canonical JSONs have TWINS (`Assets/Resources/` wins at load; `Assets/StreamingAssets/` is the
editable source). Fix BOTH or the change is invisible or half-applied. Verify byte-identical after.

## Acceptance

- [ ] Every non-ASCII character in a player-facing string is gone; the count is stated before/after.
- [ ] Comments are untouched, and the RESULT says so.
- [ ] Both canonical twins byte-identical.
- [ ] The existing tofu oracle is WIDENED to cover these files, so the class cannot return. Prove it
      RED first (reintroduce one em dash, watch it fail, restore) and report the mutation.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] PO closes.

## What NOT to touch

- Do not rewrite the surrounding copy. Swap the character, nothing else - the words are the owner's.
- Do not touch ids, keys or addresses.
- Do not widen into WO-1332 (names - CLOSED, no action) or WO-1326 (wolf art).
