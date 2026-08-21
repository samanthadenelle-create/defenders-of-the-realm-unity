**Status:** PARKED — owner ruling 2026-08-21: needs a real solution, revisit once the board is clear.

# WORK ORDER 1131 — armor.json / weapons.json dual-copy drift: stop patching, fix the mechanism

**Type:** DEFECT + MECHANISM. **Date:** 2026-08-21. **Lane:** Data/Catalog.
**Minted by:** CLI (banner bumped 1131 -> 1132 in the same edit).

## The observation

`Assets/Resources/Data/Canonical/` and `Assets/StreamingAssets/Data/Canonical/` are supposed to be
byte-identical dual copies — `DataWebRegression` fails the gate on drift, which is how the
`abilities.json` drift I caused on 2026-08-21 was caught within minutes.

**But `armor.json` and `weapons.json` are ALREADY drifting in HEAD**, and the suite was green with
them at 15:44 that day. So the dual-copy law is enforced for some files and not these two. Either
they are deliberately exempt (and the exemption is not visible where a reader would look), or the
oracle reports only the first drift it finds and these are hiding behind whichever file fails next.
**That ambiguity is the actual defect** — a gate you cannot tell is covering a file is not a gate.

## Owner ruling 2026-08-21 (verbal, this session)

> "the armor weapons will be something we do once the board is clear"
> "we need a real solution so its parked for a few"

So: **do NOT paper over it by copying one file onto the other.** That is what I deliberately did
NOT do when I found it. A blind copy picks a winner between two files that have diverged for an
unknown reason and an unknown length of time, and it would destroy whichever side holds the edit
somebody actually wanted.

## What "a real solution" has to answer

1. **Which copy is authoritative, and why?** `DataWebRegression` states Resources WINS at runtime.
   If that is always true, StreamingAssets is a derived artifact and should be GENERATED, never
   hand-edited — at which point drift becomes structurally impossible instead of merely detected.
2. **Why do these two drift when the others do not?** Diff them first. If the delta is meaningful
   content (a real authoring edit that only landed on one side) that content must be preserved, not
   overwritten.
3. **Is the oracle actually checking every pair, or short-circuiting on the first failure?** Read
   `DataWebRegression` and prove which. If it short-circuits, other drifted pairs may be hiding.
4. **Should StreamingAssets exist at all** for canonical data, or is it a legacy of the WebGL path?

## Acceptance

- The authority is named in ONE place and enforced (generated copy, or a check that names EVERY
  drifted pair rather than the first).
- `armor.json` / `weapons.json` reconciled with the divergent content reviewed, not discarded.
- A regression that fails when any pair drifts, listing all of them.

## Do NOT

- Do not `cp` one over the other to make the gate green.
- Do not add these two files to an exemption list to silence the check.
