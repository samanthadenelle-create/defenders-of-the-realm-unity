# WORK ORDER 1197 - the board has no bucket that means "partially landed"

**Status:** FIXED 2026-08-25 (`5f3985928`) - shape (a), the `"PARTIAL"` sub-badge, chosen by the dev lane over (b) the required `RESIDUAL:` field; awaiting owner felt-verify. `tools/board_build.py` renders a `"PARTIAL"` badge on any Ready row whose status line says a partially-landed piece is already on disk, and `docs/BOARD.md` section 3b moved in the SAME change as the parser, per that doc's section 4. Verified against the three REAL rows rather than a synthetic fixture: WO-1170, PROD-014 and WO-1073 all render the badge and all three stay in the Ready bucket, so the open halves remain takeable - the constraint the ticket cared about most. RESIDUAL: (b) was the more durable answer and the dev lane did not take it, so a partially-landed claim can be PERMITTED but never CHECKED - a row may assert one without naming what is left. BOARD-KEEPER NOTE 2026-08-25: the badge-name mentions above are QUOTED deliberately. Unquoted, the word tripped `status_contradiction` on this very row and the build printed `BOARD_CHECK_FAIL 1 status contradiction(s)` - the ticket that shipped the detector was the first row its own detector falsely accused. Quoting uses the parser's OWN sanctioned "reported, not asserted" exemption, so the record is unchanged in meaning; the blind spot underneath it (the lint cannot tell a row NAMING the badge from a row CLAIMING one) is reported as a finding, not patched.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1197 -> 1198 in the same edit)
**Silo:** Tooling / board
**Origin:** found by the board keeper during the 2026-08-25 reconcile, reported rather than
silently worked around. It did not mint this itself - numbering is not its job.

---

## The finding

`docs/BOARD.md` section 3b **can** express "slice landed, ticket open" in the STATUS LINE without
lying. The parser confirms it: `board_build.py:105-112` tests the LEADING word first and returns
immediately, so a later `DONE` or `BLOCKED` inside the prose cannot yank the row into the wrong
bucket. All three of today's partials bucket correctly.

**The gap is one level up: the BUCKET is the only thing a board reader sees at a glance, and there is
no bucket that means "partially landed."**

After today's reconcile, the Ready bucket advertises three tickets whose best-understood slices are
**already built**:

| WO | What is already landed | What is still open |
|---|---|---|
| **WO-1170** | sites 1, 2, 3 | sites 4 and 5; site 6 WITHDRAWN as mis-specified |
| **PROD-014** | slice (b), the acknowledge/exit | its headed capture; slices (c) and (d) blocked |
| **WO-1073** | the architecture slice | entitlement flip + migration, endpoints, client surfaces, cosmetic rendering |

STOP **A puller who trusts the bucket and not the sentence re-does landed work.** The caveat in the
status line can only mitigate that, never prevent it - because the caveat is not what gets read first.

## Why this is worth a ticket rather than a habit

**Three instances appeared in a SINGLE reconcile pass.** That is a pattern, not a coincidence, and it
is the same failure that got Batch 8 refused: the dev lane was handed WO-1137 and WO-1138 as fresh
work when both were already finished. The board was the proximate cause then, and the mechanism that
caused it is still here - only the specific rows changed.

* And the direction matters: a stale `READY` on FINISHED work wastes a seat. A `DONE` on
half-finished work **hides** the remainder, which is worse - it stops anyone looking. Any fix must
avoid trading one for the other.

## Two candidate shapes - pick one, do not build both

**(a) A `PARTIAL` sub-badge rendered on the card.** The bucket stays `Ready` so nothing about
assignability changes; the badge carries the truth to the reader's eye. Smallest change, purely
presentational.

**(b) A required `RESIDUAL:` field the generator LIFTS out of the status line onto the row.** Stronger,
because it forces the residual to be stated rather than merely permitted, and it becomes checkable -
`--check` could fail a row that claims partial completion without naming what is left.

* **(b) is the more durable answer and (a) is the cheaper one.** ⚠ Whoever takes this should say which
they chose and why, in the handback.

## Constraints

- ⛔ **Do NOT add a new BUCKET.** A fourth destination changes what "Ready" means for every existing
  query and every seat's mental model. The bucket vocabulary in `docs/BOARD.md` section 3b is canon.
- ⛔ **Do NOT hand-edit `BOARD.html`** - it is generated output.
- Per `docs/BOARD.md` section 4, a change to the status vocabulary edits **both** `bucket_of()` in
  `tools/board_build.py` **and** the table in section 3b, **in the same commit**. A keyword the parser
  knows and the doc does not is invisible to every human; the reverse silently produces `Unlabeled`.
- ⛔ `--check` must keep failing on GENUINE defects only. It currently reports 0 unlabeled and 0
  contradictions, and that number is honest enough to gate on. Do not make it noisy.
- ⚠ Leave the 244 `NEAR_MISS_STATUS_MARKER` rows alone - cosmetic since WO-1180, standing instruction.

## Acceptance

1. A reader scanning the board can tell, WITHOUT opening the ticket, that WO-1170, PROD-014 and
   WO-1073 have landed work in them.
2. Those three still bucket as assignable - ⛔ the fix must not move them out of `Ready`, because the
   open halves genuinely are takeable.
3. `board_build.py` and `docs/BOARD.md` section 3b move in the SAME commit.
4. `BOARD_CHECK_OK` still reports 0 unlabeled and 0 contradictions.
5. Prove it against those three real rows, not a synthetic fixture.
