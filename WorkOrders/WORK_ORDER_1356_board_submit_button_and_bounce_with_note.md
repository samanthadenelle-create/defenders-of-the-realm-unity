# WORK ORDER 1356 — Board: a Submit button, and Fail / Needs Work bounce back to READY with her note

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T13:25:21, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - Submit button + `--submit` ingest + the bounce pass are implemented and proven (VALIDATION_ROUNDTRIP_OK, 49/49 assertions, 8 mutations caught; the `file://` download verified in real Chrome over CDP). Awaiting owner felt-verify to CLOSE.

**Owner ruling (verbatim, 2026-09-03):**
> "add a submit button so you run a script to close the ones passed. move the needs work and failed back to ready with a note"

**Lane:** tooling / board. **Files:** `tools/board_build.py`, `tools/board_close_pass.py`,
`tools/board_close_validated.py`, `tools/owner_validations.py`,
`tools/board_validation_roundtrip_test.py`, `docs/BOARD.md`, `BOARD.html` (derived).
**Not touched:** `Assets/`, any `.unity`, `tools/*ship*.ps1`, `publishing/`, `KEY_FACTS.md`,
`CLI_LANES_WO_NUMBERS.md`, any `**Status:**` line in `WorkOrders/` (no live close or bounce pass
was ever run — every test ran through the `EOA_WO_DIR` / `EOA_VALIDATIONS_PATH` / `EOA_BOARD_OUT` /
`EOA_SUBMIT_DIR` temp harness).

---

## 1. The defect, and the evidence it was already live

Her sign-offs lived in browser storage and reached disk only through
**Export → Copy → hand the text to the CLI → `--ingest -`**. That hand-over is friction, and a
mechanism with friction is one that stops being used. The proof it had already stopped: her board
read **43/78 verified with seven Pass+Validated rows**, while `proof/owner-validations.json` held
**ZERO**, so the close pass reported `BOARD_CLOSE_OK closed 0`.

And the *second half of her sign-off did not exist at all*: a Fail or a Needs Work changed nothing,
and the note she typed saying **why** — the single most valuable artefact in the loop — was thrown
away.

---

## 2. HALF 1 — the Submit button

### The constraint that shaped it

⛔ **She opens the board over `file://` (`D:/EoA/BOARD.html`). A `file://` page cannot write into
the repo and there is no server to POST to.** So the design question was not "which endpoint" but
"what can this page genuinely do to put bytes on disk with the fewest owner actions". The answer:
**hand the browser a file to save.** One tap, no dialog, no paste.

### What the button does

`Submit marks to the CLI` builds the same export payload the Copy path builds, then downloads it as
**`eoa-validations-<UTC stamp>.json`** (blob URL, falling back to a `data:` URI). The stamp is in
the *name*, not left to mtime alone, so "the newest submission" is a fact rather than a guess and a
re-submit never collides into Chrome's `" (1)"` renaming.

**It says what it did**, in words — the owner is red/green colourblind, so success and failure are
never carried by hue:

- `SUBMITTED 2 marks as eoa-validations-20260903T220802Z.json - check your Downloads folder. Next:
  the CLI runs python tools/board_build.py --submit … IF NO FILE APPEARED IN DOWNLOADS, this did
  NOT work - open "Export for the CLI" below instead.`
- `NOT SUBMITTED - the browser refused to save the file (<error name>). Nothing left this page.`
- `NOTHING TO SUBMIT - no ticket carries a mark yet.`

A browser gives no completion callback for a download, so the message deliberately never claims
more than it knows: it names the count, the exact filename to look for, the next command, and the
fallback. The button is `min-height:44px` unconditionally (phone-first), not only under the phone
media query.

### ⭐ Verified from `file://`, not assumed

Driven through **real Chrome** (`C:\Program Files\Google\Chrome\Application\chrome.exe`) over the
DevTools protocol against a real `file:///D:/eoa/...` page, clicking the real `#vsubmit`:

```
== HTTP control: http://127.0.0.1:8391/
EVENT[browser] completed 16fb793f
files (HTTP control): ["eoa-validations-20260903T220757Z.json"]

== FILE:// the real case: file:///D:/eoa/tmp/wo1356/BOARD_PROBE.html
EVENT[browser] completed 074865fa
files (FILE:// the real case): [... ,"eoa-validations-20260903T220802Z.json"]
  eoa-validations-20260903T220802Z.json 397 bytes ::
  { "validations": { "WORK_ORDER_1344_ftue_pointer...": { "verdict": "Needs Work", ... "note": "right now its a red d" }, …
```

`file://` behaved **identically to the `http://` control** — nothing about the scheme refuses the
save. (Two earlier probe rounds reported `state: canceled`; that was the harness sending
`Browser.setDownloadBehavior` to the wrong CDP endpoint, not the page. Worth recording: the first
result said the feature did not work, and it was the *instrument* that was wrong.)

### The ingest path it feeds

```
python tools/board_build.py --submit
```

Takes the **newest** `eoa-validations-*.json` from, in order: `EOA_SUBMIT_DIR` (exclusive when set,
so a test never depends on the operator's real Downloads), else `<repo>/inbox/`, `~/Downloads`,
`~/OneDrive/Downloads`. It prints `VALIDATIONS_SUBMIT_FILE <path> (saved N min ago)`, folds the
payload in through the *same* `_ingest_path()` that `--ingest` uses, then **falls through into the
ordinary board build** — so one command ingests, closes, bounces and redraws. That fall-through is
the point: CLAUDE.md §16 — a gate whose remedy is "a human remembers a second command" is not a
gate. No file found → `VALIDATIONS_SUBMIT_FAIL`, and it stops *before* the passes rather than
running them on stale data.

⚠ **The Export/Copy escape hatch is kept, working and unchanged.** Its summary now reads "the
manual fallback, if Submit did not produce a file".

---

## 3. HALF 2 — the bounce, with her note

Implemented in **`tools/board_close_pass.py`** (`sanitize_note` / `bounce_stamp` / `bounce_pass` /
`run_bounce`), beside the close, because both are the same dangerous act: rewriting a `**Status:**`
line from a data file. **There is exactly one bouncer** —
`board_close_validated.apply()` is now a thin adapter that maps its legacy `(verdict, note)` tuples
onto it, and `board_build.py` calls `run_bounce` immediately after the close, off the same read of
the record.

### The format, with the worked WO-1184 example

```
BEFORE
**Status:** FIXED 2026-08-27 — implemented; awaiting owner felt-verify to CLOSE.

AFTER  (mark: Validated + "Needs Work", note "right now its a red d")
**Status:** READY TO IMPLEMENT - owner felt-test 2026-09-03 Needs Work
 (marked 2026-09-03T22:08:02, build 2026.09.03.354093) - "right now its a red d".
 Bounced from Fixed. PRIOR STATUS: FIXED 2026-08-27 — implemented; awaiting owner felt-verify to CLOSE.
```

### The rules

| | Rule | Why |
|---|---|---|
| B1 | **Verdict alone bounces** — `validated` is *not* required, deliberately unlike the close | A close is terminal so it demands two signals. A bounce is the recoverable direction and is fully reversible from the `PRIOR STATUS:` text it preserves. Requiring the extra tap would leave a ticket she marked Fail sitting silently in Fixed forever — the exact failure this loop exists to end |
| B2 | **Only a FIXED ticket bounces**, judged by `board_build.classify_status` | The same function the Fixed bucket uses, so the two can never disagree. A DONE/CLOSED/SPEC row is held and reported |
| B3 | **Idempotent** | A bounce writes a status leading with READY, which the next run classifies Ready and skips (`already-ready <n>`). No stacked notes, no nested `PRIOR STATUS:` chains |
| B4 | **The existing status body survives verbatim** after `PRIOR STATUS:` | Those FIXED lines carry what shipped and what is still held. Same convention the close uses, and `status_contradiction` already splits on that marker |
| B5 | **An empty note is legitimate** | She may mark Needs Work having typed nothing. It bounces anyway and the stamp carries no quote. A reason is never invented |
| B6 | **Her words are not reworded** | See below |
| B7 | Corrupt record **aborts**; a mark naming a missing WO file is **reported** | Identical to close rules 6 and 7 |

**B3, stated plainly:** editing a note *after* a bounce does not re-stamp the ticket. That is the
correct trade — the alternative stacks a second note and nests a `PRIOR STATUS:` chain on every
rebuild — and the ticket is already back in the queue, where a person edits it.

### B6 — exactly what `sanitize_note()` transforms, and nothing else

A note may contain quotes, newlines or markdown that would break a single-line ASCII status field.
Every transformation is **named on the log** (`note adjusted for the status line: …`):

1. smart quotes / dashes / ellipsis / nbsp folded to their ASCII equivalents (so `don’t` → `don't`,
   never `dont`);
2. any *remaining* non-ASCII or control character replaced with one space;
3. line breaks and runs of whitespace flattened to single spaces — a raw newline would split the
   status line and orphan the tail;
4. a literal `PRIOR STATUS:` inside her text written `PRIOR STATUS -`, so the marker carrying the
   preserved old status cannot be forged from a note;
5. truncation at 160 characters with a trailing `...`.

No rewording, re-ordering, capitalising, spell-correcting or summarising.

**Marker:** `BOARD_BOUNCE_OK bounced <n>, already-ready <n>, held <n>, missing <n>` /
`BOARD_BOUNCE_FAIL <why>`. Judge it on a fresh log, never the exit code. `EOA_BOARD_CLOSE=0` /
`--no-close` skips it too (`BOARD_BOUNCE_SKIPPED`).

---

## 4. The page wording that was wrong

The blurb claimed sign-offs "are kept in `proof/owner-validations.json` (committed)" — true only
*after* an export, so it read as though her marks were already durable when they were one manual
step from being lost. Replaced with a statement of what is and is not saved:

> **A mark you make here is NOT saved yet.** It lives only in this browser until you tap **Submit** —
> that is the step that writes a file the CLI can read. Once submitted and taken in, it is stored in
> **proof/owner-validations.json**, which is committed, survives every commit and every rebuild, and
> is **NOT** tied to a build.
> This page shows **N** mark(s) already in that record (rendered from disk, no JavaScript needed)
> plus anything you have marked on this device since.
> What your verdict does on the next board build: **Pass + Validated** flips the ticket from Fixed to
> **CLOSED**; **Fail** or **Needs Work** sends it back to **READY** carrying your note into the ticket.

---

## 5. ⭐ Acceptance — run against copies of the eight real tickets on her live board

`EOA_WO_DIR` pointed at a temp copy; the eight real files were never written.

```
VALIDATIONS_SUBMIT_FILE ...\drop\eoa-validations-20260903T220802Z.json  (saved 0 min ago)
VALIDATIONS_SUBMIT_OK ingested; continuing into the board build...
    CLOSED  WORK_ORDER_1006_manage_launcher_category_browser_panels.md
    CLOSED  WORK_ORDER_1278_post_wave_victory_modal.md
    CLOSED  WORK_ORDER_1283_apk_build_marker_assertion.md
    CLOSED  WORK_ORDER_1304_animator_builders_missing_dead_guard.md
    CLOSED  WORK_ORDER_1305_spell_fire_9_marquee_and_synty_duplicate_addresses.md
    CLOSED  WORK_ORDER_1326_wolf_colour_differs_by_build_target.md
    CLOSED  WORK_ORDER_1331_canonical_json_remote_source_seam.md
BOARD_CLOSE_OK closed 7, held 1, already-closed 0, missing 0
    BOUNCED WORK_ORDER_1184_lookout_horde_warnings.md   Needs Work -> READY   "right now its a red d"
BOARD_BOUNCE_OK bounced 1, already-ready 0, held 0, missing 0

closed: 7 / 7      1184 -> READY: True      1184 not closed: True
1184 note verbatim: True                    1184 body preserved: True
idempotent over 4 runs: True                PRIOR STATUS: markers on 1184 after 4 runs: 1
```

---

## 6. The oracle — `tools/board_validation_roundtrip_test.py`

`VALIDATION_ROUNDTRIP_OK`, **49/49 assertions**. New/extended stages:

- **5b** — the same `board_build` run emits `BOARD_BOUNCE_OK bounced 4, already-ready 0, held 1,
  missing 0`; the acceptance-shaped fixture (Validated **+** Needs Work **+** note) bounces to READY
  with the note verbatim and is **not** closed.
- **6** — idempotency across four runs, byte-identical, and the second run reports
  `already-ready 4` (B3 proven positively, not just by absence of change).
- **7** — the bounce **names** a validation pointing at a missing WO file and returns not-ok.
- **8** — a corrupt record prints `VALIDATIONS_PARSE_FAIL` + `BOARD_BOUNCE_FAIL` and touches no
  status line.
- **9b** — RED proof, four bounce mutations (below).
- **10 / 10b** — `--submit` end to end: newest drop file wins over an older one, `VALIDATIONS_SUBMIT_FILE`
  names it, the marks land in the record, and the same command closes 1 and bounces 4. With no drop
  file: `VALIDATIONS_SUBMIT_FAIL`, no pass runs, nothing touched.

**Mutation report (proved RED first — every one was caught):**

| Mutation | Caught by |
|---|---|
| drop her note from the bounce stamp | `acceptance case: her note did not land in the ticket verbatim` |
| B4 — throw away the existing status body | `B4: the FIXED status body did not survive the bounce` |
| B2 — let a DONE ticket be bounced | `B2: a DONE ticket was bounced by a felt-test verdict` |
| B5 — skip a ticket whose note is empty | `B1/B5: a Fail with no note and no Validated tap did not bounce` |

Then: *"and the UNmutated bounce pass is green again (the success path, proven)"* — the success
path is asserted, not only the refusals (memory `prove-the-success-path-not-just-the-refusal`).

⚠ **Two pre-existing close-pass mutation anchors had to be made exact** (they now carry their
trailing newline). `board_close_pass.py` holds the bounce as well, which tests the same predicate
text `if bucket != "Fixed":`, so a bare substring anchor matched twice and the mutation was
**skipped** — and a skipped mutation is a RED proof that proves nothing while reporting only a
warning. Fixed, with the reason written above the mutant table in the test.

## 7. Other gates

- **`node --check`** on the JS extracted from the shipped `BOARD.html`: **passes**.
- **No-JS**: every server-rendered part is unchanged — the `[X] VALIDATED` badge, the button label,
  the row sinking, the per-group counts and the new blurb all render from disk. `#vsubmitstat`'s
  server-rendered default reads "Not submitted yet on this device.", which is true with JS off.
- **PowerShell 5.1**: **no `.ps1` was touched by this work order** (`git status` shows none), so
  there is nothing new to parse-prove. `tools/regression/checkin_gate.ps1` and
  `.claude/hooks/lane-check.ps1` call `board_build.py` and are unmodified; the new markers are
  additive and neither script parses them.
- `BOARD.html` was regenerated with `EOA_BOARD_CLOSE=0`, which printed `BOARD_CLOSE_SKIPPED` +
  `BOARD_BOUNCE_SKIPPED` — so the page ships with the Submit button while **no live `**Status:**`
  line was touched**.

## 8. Follow-ups (not done here, deliberately)

- The `file://` download proof is desktop Chrome. If she ever validates from the **phone**, the
  board is not reachable over `file://` there at all — that is a separate hosting question, and the
  Copy fallback remains the phone path today.
- `<repo>/inbox/` is in the search order but is not created. Making it and pointing the browser's
  download dir at it would remove the "check Downloads" sentence entirely — one owner decision, not
  a code change.

---

*Provenance: implemented 2026-09-03 by the CLI seat under WO-1356 (number minted from the
`CLI_LANES_WO_NUMBERS.md` banner).*
---

# FOLLOW-UP (2026-09-03) - "count only what is saved", and the ingest joins the ordinary build

*Appended to WO-1356 rather than minted as a new number: same owner loop, same files, same session.*

## F1. The defect - two sources of truth for one number

`BOARD.html`'s Owner Validation header read **"43 / 78 verified"** out of the browser's
`localStorage`, while the close and bounce passes read `proof/owner-validations.json` **on disk** -
and that file held **zero**. So the page showed a confident verified count with green
`[X] VALIDATED` badges while the pass reported `BOARD_CLOSE_OK closed 0`.

> Her words: *"just still shows as 79 fixed. I would expect you to move those to closed"*

She was right, and the board was the thing lying. Same duplicated-state failure this repo keeps
paying for (CLAUDE.md 2's stale WO block, 5's retired dependency table, 16's copy-pasted push).

### The ruling she chose

> **"Count only what is saved."** The `N / M verified` headline counts ONLY marks that have reached
> the durable record. Unsubmitted marks still show their per-row badge, but they must never inflate
> the headline number.

### What now renders

```
Owner Validation
  0 / 36 verified                                   <- the RECORD, server-rendered
  43 marks on this device are waiting to be submitted. Nothing is lost - they are safe in
  this browser. Tap "Submit marks to the CLI" above and they will be saved and counted
  in the number above.
```

and when nothing is outstanding:

```
  Nothing waiting - every mark on this device is already in the saved record.
```

Never a bare empty line, never a red-flavoured word: the pending state reads as *waiting* and
*safe*, and it names the action. The owner is red/green colourblind, so the state is carried by the
WORDS - no hue anywhere in it.

### Where the number comes from now

| Half | Source | File:line |
|---|---|---|
| Headline, no-JS load | `disk_done`, computed from `owner_validations.entries()` | `tools/board_build.py:730` (accumulate) -> `:888` (`<span id="vprogress">{disk_done} / ... verified</span>`) |
| Headline, with JS | `vDurableDone(vtickets, disk)` - **the `disk` map only**, which is built from each row's server-rendered `data-disk` | `tools/board_build.py:1027` (assignment), `:1013-1014` (the function) |
| Pending line | `vPending(vtickets, disk, validation)` - a ticket counts when its local mark DIFFERS from disk on `validated` / `verdict` / `note` | `tools/board_build.py:1028-1033`, function at `:1015-1018` |
| Per-row `[X] VALIDATED` badge | unchanged - `eff()` (record overlaid with local) | `tools/board_build.py:737, :1021-1024` |

The counting functions are fenced with `/* [ORACLE:counts] ... [/ORACLE:counts] */` and are **pure,
argument-only**, precisely so the oracle can extract that exact block out of the shipped HTML and
run it under node. What is tested is what she reads, not a Python re-implementation of it.

**The no-JS path is intact and is the *better* half here:** the durable count is the one thing
`board_build.py` already knows at render time, so the headline is correct on a cold load with
JavaScript off entirely. Only the pending count genuinely needs JS (only the browser knows what is
in that browser), and its server-rendered default says something true without JS:
*"Any marks you make on this device are not counted in the number above until you tap Submit. They
stay safe in this browser until then."*

### The `Needs Felt-Test` filter - checked, and deliberately LEFT on local state

It filters on `eff()` - the record **overlaid with the local marks** (`board_build.py:1023`,
`item.style.display=(needsOnly&&state.validated)?'none':''`). That is the right read and it is not
the same question as the headline: the filter answers *"what have I still not tested?"*, and a
ticket she marked ten seconds ago on this device is one she HAS tested, so it should sink out of
her way immediately - even before the file reaches disk. The headline answers *"what is safely
recorded?"*, which is a claim about durability. **They read different state on purpose, and the
page now labels both.** No silent pick was made: the two are named here so a later seat does not
"unify" them and re-break one of them.

## F2. SCOPE ADDITION - the ingest is part of the ORDINARY build

> Her words: *"i would expect you to do this everytime you build the board. CAn you add it to the
> rebuild script"*

`--submit` was opt-in. **A flag the CLI has to remember is the same failure as a second command it
has to remember** - the exact reasoning that put the close pass inside the build (CLAUDE.md 16). So
the default path is now: **auto-ingest the newest drop file -> close -> bounce -> render**, and
`--submit` remains only as the explicit form (still `VALIDATIONS_SUBMIT_FAIL` + exit when there is
no file, because a seat that typed it asked a question and deserves an answer).

Implemented in `tools/board_build.py`: `auto_submit()` plus `submission_candidates()` /
`_submit_rank()` / `consumed_path()` / `_sha256()` / `_consumed_load()` / `_consumed_has()` /
`_consumed_remember()`. Called from `main()` on every run that is not `--submit`, **before** the
record is read, so the page written by THIS run already renders the marks it just took in.

### The five risks, each answered by a rule (not by hand-waving)

| | Rule | How it is honoured |
|---|---|---|
| S1 | **A stale drop must never resurrect old marks** | Candidates are ranked by the **UTC stamp in the FILENAME** first; mtime only breaks a tie, then the filename. The stamp is what her browser wrote at the moment she tapped Submit; mtime is whatever a copy or a sync did afterwards. **Only the single newest candidate is ever considered** - if it is already consumed the pass STOPS rather than falling back to an older file. Two files with the SAME stamp (Chrome's `" (1)"` rename) fall to mtime, then to the greater filename - deterministic, and identical bytes are a no-op anyway |
| S2 | **Never re-ingest the same file** | Recorded by **sha256 of the bytes** in `<record>.consumed.json` (name/mtime/at kept only as provenance; bounded to 100). Hash-first because the same payload can arrive under a second name. `VALIDATIONS_SUBMIT_ALREADY` on a repeat. This is *proven*, not assumed - stage 12b asserts the record is byte-identical after the second build |
| S3 | **A malformed drop file must not break the build** | **Chosen behaviour: report loudly and CONTINUE rendering.** A half-written download must not freeze the board. It is **not** marked consumed, so the complaint repeats on every single build until it is dealt with - it can never be silently skipped into "I thought my marks landed" |
| S4 | **Say what it did, every time** | `VALIDATIONS_SUBMIT_FILE <path> (saved N min ago)` (plus `[newest of K; the K-1 older one(s) are ignored]`), `VALIDATIONS_SUBMIT_ALREADY`, `VALIDATIONS_SUBMIT_NONE`, `VALIDATIONS_SUBMIT_SKIPPED`, `VALIDATIONS_SUBMIT_UNREADABLE`. Silence on a routine path is how "did my Submit work?" becomes unanswerable |
| S5 | **Escape hatch, opt-OUT only** | `EOA_BOARD_SUBMIT=0` and `--no-submit`, matching the existing `EOA_BOARD_CLOSE=0` shape. An opt-IN would restore the hole |
| S6 | **It must not fire in the gate** | `tools/regression/checkin_gate.ps1` stage 1b runs `board_build.py --check`. **`--check` now IMPLIES the opt-out inside `board_build.py` itself** (`main()`), which is the structural pin - any CI path using `--check` is safe by construction. The gate ALSO sets `$env:EOA_BOARD_SUBMIT='0'` around the call as a second lock. A check-in must never read a developer's `~/Downloads` or write the shared record as a side effect |

The consumed ledger sits beside the record and follows `EOA_VALIDATIONS_PATH`, so the temp-dir
harness gets its own and can never consume - or be confused by - the real one. A corrupt ledger is
reported (`VALIDATIONS_CONSUMED_UNREADABLE`) and treated as empty: that is the safe direction,
because a repeat ingest of an identical payload is a proven no-op while a blocked board is not.

## F3. Oracle - `tools/board_validation_roundtrip_test.py`, now **85/85** assertions

`VALIDATION_ROUNDTRIP_OK`. New stages:

- **11** - the headline. The `[ORACLE:counts]` block is extracted from the built page and run under
  **node** with fixture inputs: record 2 + browser 3 -> **2** (never 3, never 5); **record EMPTY +
  browser marks -> 0** (the case that matters right now) with 3 reported pending; a local mark that
  already matches the record is **not** pending. Plus the no-JS half asserted straight off the HTML:
  the server-rendered `id="vprogress">N /` equals the record's count, and `id="vpending"` renders.
- **11b** - RED proof for the headline.
- **12 / 12b / 12c / 12d / 12e** - the ordinary build: it ingests the drop file with **no flag**
  and then closes 1 / bounces 4 in the same command; the **stamp beats a deliberately newer mtime**
  on the stale file; the same file is **never taken twice** and the record is byte-identical after
  the second build; **no drop file is a loud no-op** (`VALIDATIONS_SUBMIT_NONE`, board still built);
  a **malformed** file reports, does not block, and is **not consumed** so it reports again; the
  **opt-out** holds for `EOA_BOARD_SUBMIT=0`, `--no-submit` and `--check`.
- **12f** - RED proof for the auto-ingest, in-process against mutated copies of `board_build.py`
  (the real file is never touched), with the **unmutated** pass asserted green FIRST
  (memory `prove-the-success-path-not-just-the-refusal`).
- The whole suite now pins `EOA_BOARD_SUBMIT=0` at start-up: a test whose input depends on whatever
  is sitting in the operator's `~/Downloads` proves nothing. Stage 12 opts back in explicitly,
  always against a throwaway `EOA_SUBMIT_DIR`.

### RED first, against the CODE AS SHIPPED - not only against mutants

Built the page from **`git show HEAD:tools/board_build.py`** and ran stage 11's contract against it:

```
RED vs HEAD - failures found: 4
  RED  HEAD carries no fenced [ORACLE:counts] block - nothing to test under node
  RED  HEAD does not assign the headline from the disk map
  RED  HEAD assigns the headline from `done` - the EFFECTIVE (browser-overlaid) count.
       THIS IS THE DEFECT: 43 / 78 verified over an empty record.
  RED  HEAD renders no pending line at all
RED_PROOF_OK
```

### Mutation report (every one caught)

| Mutation | Caught by |
|---|---|
| headline counts the browser overlay again (`\|\|(LOCAL[t]\|\|{}).validated`) | `mutation caught: the empty-record case now reads 1, not 0` / `record 2 + browser 3 now reads 3, not 2` |
| S2 - forget that a file was already ingested | `S2: the same file was ingested a SECOND time` |
| S1 - let an OLDER drop file win (`cands[0]` -> `cands[-1]`) | `S1: the newest drop file was not the one taken` |
| S3 - treat an unreadable drop file as ingested | `S3: a malformed drop file was reported as ingested` |
| S4 - ignore the opt-out | `S4: EOA_BOARD_SUBMIT=0 did not stop the auto-ingest` |

## F4. Other gates

- **`node --check`** on the JS extracted from the shipped `BOARD.html`: **passes** (`NODE_CHECK_OK`).
- **No-JS**: the headline, the `[X] VALIDATED` badges, the row sinking, the per-group counts and the
  pending line's default all render from disk. Live page now reads `0 / 36 verified` - the record's
  own count, over an empty `validations: {}`.
- **ASCII-only** in every added line (0 non-ASCII characters in the diff), 44px tap targets
  unchanged, no hue-carried state.
- `BOARD.html` regenerated with `EOA_BOARD_CLOSE=0 EOA_BOARD_SUBMIT=0`, printing
  `VALIDATIONS_SUBMIT_SKIPPED` + `BOARD_CLOSE_SKIPPED` + `BOARD_BOUNCE_SKIPPED` - so the page ships
  with the fix while **no live `**Status:**` line was touched and no Downloads folder was read**.
  Every test ran through the `EOA_WO_DIR` / `EOA_VALIDATIONS_PATH` / `EOA_BOARD_OUT` /
  `EOA_SUBMIT_DIR` temp harness.
- **PowerShell 5.1**: `tools/regression/checkin_gate.ps1` gained two lines (an env assignment and a
  `Remove-Item Env:\EOA_BOARD_SUBMIT`), no new syntax, and neither touches `$LASTEXITCODE` between
  the board call and its check.
- Canon updated in the same breath (CLAUDE.md 15): `docs/BOARD.md` - the ordinary-build ingest with
  its guard table, the "count only what is saved" rule, and a correction of the now-false bullet
  *"a rebuild has no write path to it at all"*.

## F5. Not done, deliberately

- The per-group `<area> N / M` counts still show **effective** state (record + local), matching the
  per-row badges rather than the headline. Left alone on purpose - the ruling and the brief both
  scope the change to the HEADLINE, and those counts sit inline with the badges they summarise.
  Flagged here so it is a decision on the record, not an oversight.
- `<repo>/inbox/` is still in the search order and still not created (unchanged from the original
  WO); pointing the browser's download directory at it would remove the "check Downloads" sentence.

*Provenance: follow-up implemented 2026-09-03 by the CLI seat under WO-1356. No new WO number minted
(same loop, same files). Not committed by this agent - the lead commits.*
