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

## Acceptance

- [ ] A malformed `**Status: ...**` marker is reported by name, and the row is still rendered
- [ ] The build prints how many rows were bucketed by fallback, and which
- [ ] `Unlabeled: 0` still holds, and no row silently changes bucket - **diff the counts before and
      after** and account for every delta
- [ ] Prove each case by **inducing** it and watching the build report it
