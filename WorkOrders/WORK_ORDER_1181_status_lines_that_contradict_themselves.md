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

## ⭐ LEAD FEEDBACK 2026-08-24 - two refinements the HEAD run surfaced, both real

Codex implemented this and **correctly refused to call it ready**, because its own acceptance demands
a zero-finding run against HEAD and five statuses fired. ⭐ **That was the right call** - a lint that
cries wolf on day one gets switched off in a week. Four of the five were genuine and are now
reconciled (WO-1100, WO-1152, WO-970, WO-978). ⚠ **One of them was mine**: I left WO-1152 `FIXED` by
hand this morning with "R2 push owed" in the same line. Pushing is **work remaining**, not
verification. The lint was right and I was wrong.

The other two findings change the lint itself:

### 1. ⛔ EXEMPT `*.RESULT.md` FILES - the acceptance is UNSATISFIABLE without this

Two of the five flagged rows were **`.RESULT.md` files** (WO-1100, WO-999). ⛔ **CLAUDE.md §15 freezes
those: "dated point-in-time ledgers ... RESULT files ... never rewrite."**

So the lint currently demands an edit that canon forbids, and **no amount of reconciliation can ever
make the HEAD run clean.** Exempt them. If a RESULT file reads as current, §15's remedy is a
`⚠ SUPERSEDED <date>` banner, never a status rewrite.

### 2. ⚠ `"still open"` IS TOO BLUNT - the same mistake `READY` would have been

**WO-999 was a FALSE POSITIVE.** Its line read *"owner felt-close still open"* - and an owner close
outstanding is **verification remaining**, which §13 makes the **normal state of every correctly
handled row**. Flagging it would flag the healthy bucket, which is exactly the failure this ticket's
own spec warns about.

⭐ **The rule is: ban the PHRASE, not the WORD.** This ticket already says `READY` alone is too blunt
because *"FIXED, ready for felt-test"* is legitimate. **`still open` has the identical problem.**

| ⛔ Fire on | ✅ Never fire on |
|---|---|
| `§N ... still open` / `§N is open` | `owner felt-close still open` |
| `ruling still open` / `question still open` | `owner close still open` |
| `NOT added`, `NOT built`, `push owed` | `awaiting owner felt-verify` / `felt-test` |

⚠ **WO-999 still needed a fix - just not the one the lint named.** It led with `DONE` while awaiting a
close, and §13 reserves DONE for closed. Corrected to `FIXED`.

### The debt the run surfaced - that is WO-1180's worklist, and it is the point

**31 malformed status markers** and **43 rows depending on the substring fallback.** ⭐ Both numbers
are exactly what WO-1180 was written to make visible: an invisible class became a countable one.
⛔ **Do not bulk-rewrite them** - drain by hand, in batches, checking bucket counts before and after
so no row silently changes meaning.
