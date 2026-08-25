# WO-1180 - The board parser accepts a malformed `**Status:**` and hides the rows it rescues

**Status:** READY. **Silo:** Tooling/board.
**Origin:** review of the 2026-08-24 board reflection - *"WO-932 exposed a parser-level weakness, not
merely a one-file typo."* Correct, and the fix needs to be narrower than it first looks.

## The finding

`tools/board_build.py:300` matches the status line with:

```python
re.search(r"^\*\*Status:?\*?\*?:?\s*(.+)$", text, re.MULTILINE)
```

⚠ **Both the colon and the closing asterisks are OPTIONAL**, so `**Status: PHASES 1-4 ...**` -
markers *inside* the bold - parses happily. WO-932 carried exactly that and nobody could see it.

## ⛔ AND THE PROPOSED FIX WAS ALREADY TRIED, THE OTHER WAY ROUND

The review suggested *"classify from the leading canonical status token only."* **`bucket_of`
already does that** - the leading-word test was promoted to **every** keyword on **2026-08-23**,
after a sweep found **FOURTEEN** tickets mis-bucketed by substring: *"the PRE-ACK hole closed"* read
as **Closed**, *"design complete, can be implemented"* read as **Done**, *"UNBLOCKED"* read as
**Blocked**.

⚠ **That error only ever ran ONE WAY: toward "finished."** Live work rendered as done, so nobody
looked at it again - and a board that hides open work is worse than no board.

⭐ **The substring pass is kept DELIBERATELY as a fallback**, and the comment at
`tools/board_build.py` says why: many legacy statuses lead with a non-canonical word
(`PARTIAL 2026-08-22 - ...`), and a leading-token-ONLY rule would dump every one of them into
Unlabeled - **trading a silent mis-bucket for a loud false defect.** Implementing the review's
suggestion literally would reopen the problem that change solved.

## The narrower fix - keep both properties

1. **Tighten the regex to require the exact marker `**Status:**`.** A malformed marker becomes a
   **named, reported defect**, not a silent success. ⚠ Report it; do not drop the row - a row that
   vanishes is the failure mode this whole ticket is about.
2. ⭐ **Keep the substring fallback, but COUNT AND LIST every row it rescues.** That is the real
   answer. A row classified by fallback rather than by its leading token is precisely the fragile
   case - **WO-932 was one edit from vanishing because it lived there** - and today nothing
   distinguishes it from a healthy row. Printing `FALLBACK_BUCKETED n: <files>` converts an invisible
   class into a visible worklist that can be drained ticket by ticket, without a false-defect wave.
3. Once that list is empty, and **only then**, the fallback can be removed.


## Scope amendment 2026-08-24 (Ready-queue audit, `READY_FOR_REVIEW.md`)

Two further defects are **proven** and belong to this ticket. Both are additive to the narrower fix
above — neither changes items 1–3.

4. **Malformed `**Status:` markers were real and widespread.** Confirmed on **WO-1008, WO-827 and
   WO-822** (all three used `**Status:` with no closing `**`). ⚠ All three have since been repaired
   by hand, so they are **history, not live fixtures** — prove this case by **inducing** a malformed
   marker on a scratch file, per acceptance item 4. The rule stands: such a row must stay **VISIBLE**
   and be **REPORTED** as malformed.

5. ⛔ **`WO-?` is not a valid assignable key.** Two unrelated tickets shared it —
   `WorkOrders/WORK_ORDER_ad_generator.md` and `WorkOrders/WORK_ORDER_economy_store_packs.md` — so a
   single board key addressed two different pieces of work. **The board must never let unrelated
   tickets share an assignable key.** Detect and report any duplicate or unresolvable id (`WO-?`,
   repeated numbers) by name; do not silently merge them into one row. *(Both of those two are now
   non-assignable — economy_store_packs is CLOSED/superseded and ad_generator is SPEC pending RCA —
   so, as with item 4, prove by induction rather than against those files.)*

⛔ **NOT in scope: `.RESULT.md` exclusion.** An earlier draft of this audit claimed frozen
`.RESULT.md` files were contaminating source rows (cited on WO-1001/935/932/557). **That claim does
not survive checking and is retracted.** `tools/board_build.py:319` already `continue`s on any
`base.endswith(".RESULT.md")` before anything else runs, `grep -c "RESULT\.md" BOARD.html` returns
**0**, and WO-1001 renders exactly **one** row. What exists is the `<span class="res">RESULT</span>`
**badge** drawn on the source ticket's own row (`:315-316` builds the `results` set, `:408` renders it) — a
badge that was misread as a second row. ⭐ **Do not "fix" this.** Writing a repair for correct
behaviour is how a working capability gets broken.

## Acceptance

- [ ] A malformed `**Status: ...**` marker is reported by name, and the row is still rendered
- [ ] The build prints how many rows were bucketed by fallback, and which
- [ ] `Unlabeled: 0` still holds, and no row silently changes bucket - **diff the counts before and
      after** and account for every delta
- [ ] Prove each case by **inducing** it and watching the build report it
- [ ] A malformed marker is reported **by name** and its row still renders (induced, item 4)
- [ ] A duplicate / unresolvable id (`WO-?`, a repeated number) is reported **by name** and is never
      collapsed into one row (induced, item 5)
- [ ] ⭐ **The strengthened green bar:** a build is only clean when it reports `Unlabeled 0` **AND**
      zero malformed markers **AND** zero duplicate ids **AND** zero closed-status contradictions.
      Any one of those non-zero is a **FAILING** board, not a warning.
