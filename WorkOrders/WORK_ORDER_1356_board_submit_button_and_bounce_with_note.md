# WORK ORDER 1356 — Board: a Submit button, and Fail / Needs Work bounce back to READY with her note

**Status:** FIXED 2026-09-03 - Submit button + `--submit` ingest + the bounce pass are implemented and proven (VALIDATION_ROUNDTRIP_OK, 49/49 assertions, 8 mutations caught; the `file://` download verified in real Chrome over CDP). Awaiting owner felt-verify to CLOSE.

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
