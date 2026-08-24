# WO-1181 - A status can lead with FIXED and say "not done" four words later

**Status:** READY. **Silo:** Tooling/board.
**Origin:** review of the Fixed bucket, 2026-08-24. **SEVEN rows** were green while their own status
text admitted the work was unfinished.

## The finding

The parser takes the **leading canonical token** as the verdict and treats the rest as commentary.
That is deliberate and it fixed a real problem. ⚠ **But it means a line can say `FIXED` and then
contradict itself**, and the board believes the first word:

| WO | The line led with | The same line also said |
|---|---|---|
| WO-1157 | `FIXED` | *"PARTIAL AGAINST ITS OWN WORDING"* - the first purchase still shows **two** wallet prompts, which is the ticket's entire promise |
| WO-1171 | `FIXED` | *"§4 player-facing placement is READY, not yet done"* |
| WO-1134 | `FIXED` | *"the clear-count ladder is **NOT built** (owner numbers open)"* - the repeatable-endgame core |
| WO-1129 | `FIXED` | *"**STILL OPEN** - and it is the bulk of the ticket"* |
| WO-1128 | `FIXED` | *"the owner's open question is **STILL OPEN**"* |
| WO-1060 | `FIXED` | *"**43 panels red**, two owner rulings open"* - its own acceptance unmet |
| PROD-007 | `FIXED` | *"§6 OPEN ... deliberately NOT fixed"* |

## ⛔ THE THROUGH-LINE: EVERY BOARD PARSING BUG SO FAR HAS ERRED TOWARD "FINISHED"

Three distinct mechanisms, one direction:

1. **Substring-anywhere** (fixed 2026-08-23) - *"the hole closed"* read as **Closed**;
   *"can be implemented"* read as **Done**. **Fourteen** tickets.
2. **Leading-token-only** (this ticket) - the caveat after the verdict is discarded. **Seven** rows.
3. **Malformed marker + fallback rescue** (WO-1180) - WO-932 was one edit from vanishing.

⭐ **This is not coincidence: authors write the good news first.** A status line is composed
verdict-then-caveat, so ANY rule that privileges the front of the line will systematically
under-report unfinished work. ⚠ **A board that hides open work is worse than no board** - nobody
looks at a green row again.

## The lint

Reject a `FIXED` / `DONE` / `CLOSED` status whose text also asserts **work remaining**.

⛔ **AND THE DISTINCTION THAT MAKES THIS SHIP-ABLE: "work remaining" is NOT "verification
remaining."** CLAUDE.md §13 reserves closing for the PO, so **every correctly-handled Fixed row says
"awaiting owner felt-verify"** - banning that phrase would flag the entire healthy Fixed bucket and
the lint would be switched off within a day.

| ⛔ Contradiction - flag it | ✅ Legitimate on a Fixed row |
|---|---|
| `PARTIAL`, `NOT DONE`, `NOT YET DONE`, `NOT BUILT` | `AWAITING OWNER FELT-TEST` / `FELT-VERIFY` |
| `STILL OPEN`, `IS OPEN`, `REMAINS OPEN` | `AWAITING OWNER CLOSE` |
| `OWNER RULING OPEN`, `AWAITING OWNER RULING`, `NUMBERS OPEN` | `PO CLOSES` |
| `OWED`, `BULK OF THE TICKET` | |

⚠ **`READY` alone is too blunt to ban** - *"FIXED, ready for felt-test"* is legitimate. WO-1171's real
tell was **"not yet done"**, which the list above already catches. Ban the phrase, not the word.

⚠ **`PARTIAL` is not a canonical leading token**, so a status rewritten to lead with it falls into
WO-1180's substring-fallback path. Downgrades must lead with `READY` or `BLOCKED` and put PARTIAL in
the commentary. Today's seven were written that way.

## Acceptance

- [ ] A self-contradicting status is reported by name, with the offending phrase quoted
- [ ] The healthy Fixed bucket produces **zero** findings - run it against HEAD and confirm, or the
      lint is unusable on day one
- [ ] Prove it by **inducing** a contradiction and watching it fire before trusting it
- [ ] Wire into `board_build.py --check` alongside the Unlabeled check
