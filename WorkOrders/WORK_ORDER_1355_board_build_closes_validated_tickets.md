# WO-1355 — The board build closes what she validated

**Status:** FIXED 2026-09-03 - the close pass is inside `python tools/board_build.py`
(`tools/board_close_pass.py`); marker `BOARD_CLOSE_OK`; pinned by
`tools/board_validation_roundtrip_test.py` stages 5-9 (4 mutations proven RED); check-in gate 1b/1c
green under PS 5.1. Not yet exercised against a real sign-off because the record is still empty -
awaiting her first export.

**Silo:** tooling / board (no Unity, no `Assets/`)
**Seat:** CLI
**Created:** 2026-09-03

---

## 1. The ruling

> "i test and sign off in the owner validation section when you do board next you flip all passed
> and validated to closed"

and, defining the state she signs off FROM:

> "once you move to device for testing gets moved to fixed"

The ticket lifecycle, now explicit:

```
new issue -> ticket -> assign an SME -> check in when complete
          -> ON HER DEVICE = FIXED          (not "code complete", not "committed")
          -> she signs off in Owner Validation (Passed + Validated)
          -> the NEXT board build flips those to CLOSED
```

## 2. Why it is *inside* the board build

The close already existed as `tools/board_close_validated.py` — a **second command**, and the CLI
kept dropping it, so she had to ask twice. CLAUDE.md §16 settles that shape:

> a gate whose remedy is "a human remembers a second command" is not a gate

So `python tools/board_build.py` now performs the close itself, **before** it parses the work
orders, so the page that run writes already shows the tickets it just closed. Running it afterwards
would print a close and draw a board still saying Fixed.

There is exactly **one** implementation. `board_close_validated.py` no longer contains close logic
or its own `set_status`/`first_status`; it imports `board_close_pass` and keeps only its own job —
the **bounce** (Fail / Needs Work back to READY with her note) plus the legacy Chrome-LevelDB
salvage. A drifted second copy of a status rewriter rewrites live tickets.

## 3. The eligibility rule (exact)

A ticket is closed **only** when all of:

| # | Condition | Otherwise |
|---|---|---|
| 1 | `verdict == "Pass"` **AND** `validated == true` | counted **held**, nothing written |
| 2 | the WO file exists in `WorkOrders/` | counted **missing**, reported by name, `BOARD_CLOSE_FAIL` |
| 3 | its current bucket is **Fixed** (via `board_build.classify_status`) | counted **held** with the bucket named |
| 4 | its current bucket is not already **Closed** | counted **already-closed**, file untouched |

Handling of the non-Pass verdicts, stated plainly: **Fail**, **Needs Work**, a **blank** verdict, a
**Pass that was never validated**, and a **validated entry carrying no verdict** all close nothing.
An **unrecognised** verdict string cannot even reach the pass — `owner_validations.normalize()`
coerces anything outside `("", "Pass", "Fail", "Needs Work")` to `""`, so garbage arrives as blank
and is held, never guessed at. Rule 3 is the load-bearing one: FIXED now means *it reached her
device*, which is the only state a felt-test sign-off can validly follow, so a stale mark can never
close a READY / SPEC / BLOCKED / DONE ticket.

## 4. What is written

The close stamp is **prepended**; the existing status body is carried verbatim after
`PRIOR STATUS:` — already this repo's convention for historical status prose, and the exact marker
`board_build.status_contradiction` splits on, so preserving the body cannot manufacture a false
"contradiction" defect.

```
**Status:** CLOSED 2026-09-03 - owner felt-test PASS (validated 2026-09-03T08:00:00,
build 2026.09.03.353742). PRIOR STATUS: FIXED 2026-09-01 - wolf routing shipped; HELD
awaiting her retag; finding 3 deliberately not fixed.
```

Those FIXED lines carry the real engineering record — what shipped, what is HELD awaiting a retag,
findings deliberately not fixed. Erasing them would destroy what the board exists to keep. The
`at` + `build` in the stamp make it auditable: which sign-off, on which build, closed this ticket.

## 5. Abort behaviour

A malformed or unreadable `proof/owner-validations.json` prints `VALIDATIONS_PARSE_FAIL` and
`BOARD_CLOSE_FAIL` and **writes nothing** — never a partial close, never a silent zero reported as
success. This matches the board build's existing abort on the same condition.

## 6. Marker

```
BOARD_CLOSE_OK   closed <n>, held <n>, already-closed <n>, missing <n>
BOARD_CLOSE_FAIL <same counts, plus no-status-line>
BOARD_CLOSE_SKIPPED   (only under --no-close / EOA_BOARD_CLOSE=0)
```

Judge it by marker presence on a fresh log, never by the exit code (CLAUDE.md §8/§16). The opt-out
is deliberately an opt-**OUT**; an opt-IN flag would rebuild the forgotten-second-command hole this
ticket closes. It exists so the self-check can point the real entry point at the live `WorkOrders/`
without ever rewriting a real ticket.

## 7. Files

| File | Change |
|---|---|
| `tools/board_close_pass.py` | **NEW** — the one close implementation, rules + reasoning in its header |
| `tools/board_build.py` | calls it before `parse_wos()`; `--no-close`; `WO_DIR` now honours `EOA_WO_DIR` |
| `tools/board_close_validated.py` | close half + `first_status`/`set_status` deleted; bounce keeps `PRIOR STATUS:` too |
| `tools/board_validation_roundtrip_test.py` | stages 5-9; stage 3 pinned with `EOA_BOARD_CLOSE=0` |
| `tools/owner_validations.py` | `_readme` line repointed at the board build |
| `tools/regression/checkin_gate.ps1` | stage 1c comment widened (no logic change) |
| `docs/BOARD.md` | new §6e |

## 8. Acceptance — all met

- [x] The close runs from `python tools/board_build.py` with no second command.
- [x] `BOARD_CLOSE_OK closed 1, held 5, already-closed 1, missing 0` on a 7-fixture tree.
- [x] Idempotent: runs 2 and 3 produce byte-identical work-order files.
- [x] A CLOSED ticket and a non-FIXED (READY / DONE) ticket are byte-identical after the pass.
- [x] The FIXED status body survives verbatim, and so does the rest of the file.
- [x] A corrupt record aborts with `VALIDATIONS_PARSE_FAIL` + `BOARD_CLOSE_FAIL`, nothing written.
- [x] A validation naming a missing WO file is named on the log.
- [x] `VALIDATION_ROUNDTRIP_OK`, with four source mutations each proven RED.
- [x] Check-in gate 1b (`BOARD_CHECK_OK`, exit 0) and 1c (`STAGE_1C_PASS`) green under
      PowerShell 5.1.26100.9278; `checkin_gate.ps1` tokenises with `parse_errors=0`.
- [x] `tools/board_close_validated.py` was never run against the live `WorkOrders/`; no
      `**Status:**` line in `WorkOrders/` was hand-edited. `git status WorkOrders` unchanged
      throughout.

## 9. The oracle's teeth

`tools/board_validation_roundtrip_test.py` stage 9 mutates a **copy** of
`board_close_pass.py`'s source (the file on disk is never touched) and requires the stage-5
contract to go red for each:

| Mutation | Rule deleted | Caught by |
|---|---|---|
| `verdict != "Pass" or not validated` -> `verdict != "Pass"` | 1 | `9005_fixed_unvalidated` was rewritten |
| `bucket != "Fixed"` -> allow Ready/Done | 2 | `9002_ready_pass` was rewritten |
| kill the already-Closed early return **and** allow Closed | 3 | `9003_closed_pass` was rewritten |
| `return f"{head}. PRIOR STATUS: {prior}"` -> `return head` | 4 | the FIXED body did not survive |

Then the **unmutated** pass is re-run and must be green — the success path is proven, not only the
refusals (memory `prove-the-success-path-not-just-the-refusal`).

Every stage runs against a throwaway `WorkOrders/` (`EOA_WO_DIR`), a throwaway record
(`EOA_VALIDATIONS_PATH`) and a throwaway page (`EOA_BOARD_OUT`). A test that rewrites status lines
must never be able to reach the tickets it is protecting.

## 10. Owner Validation

Nothing to felt-test on a device. To see it work: sign off a Fixed ticket on `BOARD.html`, tap
**Export for the CLI**, hand the text over, then

```
python tools/board_build.py --ingest -
python tools/board_build.py
```

and that second command should print `BOARD_CLOSE_OK closed 1 ...` with the ticket moved to the
Closed group, its old FIXED line still readable after `PRIOR STATUS:`.
