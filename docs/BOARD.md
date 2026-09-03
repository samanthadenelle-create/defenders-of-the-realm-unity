# BOARD.md — how the work-order board works (WO-1011)

**The board is `BOARD.html` at the repo root. It is GENERATED. Never hand-edit it.**

```
python tools/board_build.py          # ~2 s — regenerate the view
python tools/board_build.py --check  # same, but exits 1 if any WO is Unlabeled
python tools/board_build.py --ingest -   # fold the owner's pasted validations into the record (§6d)
python tools/board_validation_roundtrip_test.py   # prove a rebuild cannot lose a sign-off
```

---

## 1. The model — the repo IS the board

`WorkOrders/*.md` **`**Status:**` lines**, `*.RESULT.md` markers, and the
`CLI_LANES_WO_NUMBERS.md` banner **are the data**. `BOARD.html` is a two-second derived **view** of
them.

There is nothing to sync, mirror, or update in a second system. The consequence is the whole point:

> **The board is exactly as truthful as the status lines in the WO files.**
> A finished work order whose file still says `READY` is a lie the board will faithfully render.

So status hygiene is not paperwork — **it IS the board**. This is also why the board cannot drift the
way the retired Notion mirror did: there is no second copy to fall behind.

---

## 2. The protocol

1. **Regenerate at session boot, and before any board read.** Never read a stale `BOARD.html`;
   never hand-edit it (it is generated output — your edit is destroyed on the next run).
2. **Flip the status line IN THE SAME COMMIT as the work.** This is the §15 canon rule extended to
   statuses: finishing an implementation means flipping `**Status:**` *and* writing the `.RESULT.md`
   in that same commit. A status flip deferred to "later" is a board that lies until later.
3. **Never mirror to Notion — no writes, and no reads**, regardless of what older docs say.
   `NOTION_SOURCE_OF_TRUTH.md` is superseded. Any doc still pointing at Notion as the board gets a
   `STALE:` flag when touched (banner only on frozen dated ledgers, §15).
4. **The status vocabulary below is canon.** It is read from the **first** `**Status:**` line in the
   file, by keyword priority.

---

## 3. Status vocabulary (keyword priority, first match wins)

### 3a. Scope — which files owe a status at all (WO-937)

`WorkOrders/` holds **two kinds of file**, and only one of them is in the status workflow. The
parser decides by **filename prefix — the document's KIND, not whether it has a number:**

| File | Kind | Bucket |
|---|---|---|
| `WORK_ORDER_*.md` — **numbered `WORK_ORDER_<n>_*.md` or legacy unnumbered `WORK_ORDER_<slug>.md`** | a work order | bucketed by the status table in §3b |
| anything else — `README.md`, `AUDIT_*`, `BRIEF_*`, `HANDOFF_*`, `NOTES_*`, `QA_CHECKLIST_*`, `DESIGN_*`, `RESEARCH_BRIEF_*`, `REVIEW_*`, `WO541_MODEL_API.md`, … | a **companion doc** | **Doc** |

**`Doc` is a NON-DEFECT bucket and `--check` ignores it entirely.** These files are references, not
units of work — demanding a `**Status:**` line from a `README.md` or an audit brief would be absurd.
They are still **rendered, linked, and searchable by filename**, because this board is the only index
of `WorkOrders/` anyone actually opens; dropping them would trade a miscount for a discoverability
hole. Filter them out with the `Doc` chip when you want a pure work view.

> ⚠ **Unnumbered `WORK_ORDER_<slug>.md` files are REAL work orders and still owe a status.** They
> render as `WO-?`. Scoping on "has a number" instead of "is a work order" would silently launder
> their missing statuses into the non-defect bucket — do not do that.

### 3b. Status buckets (work orders only)

| Keyword in the `**Status:**` line | Bucket |
|---|---|
| `SUPERSEDED` / `CLOSED` / `CANCELLED` | **Closed** |
| `DONE` / `IMPLEMENTED` / `COMPLETE` — **or a `.RESULT.md` exists** | **Done** |
| `BLOCKED` | **Blocked** |
| `READY` (any phrasing containing it) | **Ready** |
| `DRAFT` / `SPEC` / `NOT STARTED` / `PROPOSAL` | **Spec** |
| anything else | **Unlabeled** |

An assignable Ready row whose status says `PARTIAL`, or says that a named `SLICE ... LANDED`,
stays in **Ready** and also renders a visible **PARTIAL** sub-badge. The sub-badge is presentation,
not a fourth bucket: it warns that work already landed while preserving the open residual as
assignable. Keep the landed and residual scope explicit in the status line; the badge does not infer
either scope from source files.

An assignable Ready row whose implementation exists only in another development lane uses
`OFFTREE RETURNED lane=<branch>` or `OFFTREE AWAITING-REVIEW lane=<branch>` in its status line. It
stays in **Ready** and renders a visually distinct **OFF-TREE · state · branch** sub-badge. `RETURNED`
means the lane owns requested revisions; `AWAITING-REVIEW` means the lead owes review. The required
lane value makes the work findable, and the explicit grammar prevents prose that merely names the
PARTIAL or OFFTREE sub-badge from asserting either state. This is presentation, not a new bucket.

**`Unlabeled` is a DEFECT in the WO file, not a category.** It means the status line carries no
canonical keyword, so the row cannot be bucketed and silently drops out of every real query.
Since the §3a scope fix it is a *pure* defect count — a companion doc can never land there.

Compound statuses are read left-to-right by that priority, which trips people up:

- ❌ `DELIVERED — defect pass open` → contains neither `DONE` nor `READY` → **Unlabeled**
- ❌ `IN PROGRESS — defect pass open, DELIVERED core` → still **Unlabeled**
- ✅ `READY TO IMPLEMENT (defect pass)` → **Ready**
- ✅ `DONE (pending felt-verify)` → **Done**

Write the truth *and* include one canonical keyword. The nuance belongs after it, not instead of it.

> ⚠ **A `.RESULT.md` forces the Done bucket** regardless of the status line. If you file a RESULT for
> partially-complete work, say so loudly in the status line **and** the RESULT body — the board will
> call it Done either way, so the file has to carry the caveat the bucket cannot.

---

## 4. Adding a new status keyword

Edit **both** in the **same commit**, or the doc and the parser start disagreeing:

1. `bucket_of()` in `tools/board_build.py`
2. the table in §3b above

Same rule for a change of **scope** (what counts as a work order): `is_work_order()` in
`tools/board_build.py` and the table in §3a move together.

A keyword the parser knows but this doc does not is invisible to every human; a keyword this doc
promises but the parser does not know silently produces `Unlabeled` rows.

---

## 5. `--check` and the check-in gate

`python tools/board_build.py --check` regenerates as normal, then **exits 1** if any WO is
`Unlabeled`, printing the offending numbers and files (capped at 40, with a remainder count) so the
output is a to-do list rather than a bare number.

It fails on **genuine defects only** — real work orders with no canonical keyword. `Doc` rows (§3a)
are out of scope and never counted, which is what makes the number honest enough to gate on.

A plain run is **report-only** and always exits 0 — adding the flag to a gate is a deliberate act, and
no one's build should start failing because a WO file is sloppy.

**Wired into the check-in gate (WO-937 C, 2026-08-16):** `tools/regression/checkin_gate.ps1` runs
`board_build.py --check` as stage 1b (right after the static gate, ~1 s, no Unity). Unlabeled hit 0
first, so the gate enforces the vocabulary without failing anyone retroactively. A board-check FAIL
fails the gate summary but does not short-circuit the code stages — it is a docs defect, not a
compile one.

Both runs also print a report-only `DUPLICATE_WO_NUMBERS` block — WO numbers claimed by more than one
file (56 known legacy collisions). Duplicates are **flagged, never silently renumbered** (a collision
is its own finding; resolve first-on-disk-and-referenced-wins). They do not affect the exit code.

### Created date + "opened within" (WO-940)

Every row's date column is the ticket's **CREATED date, never last-modified** — an edit must not
reset a ticket's apparent age (`SUNDAY_HOUSEKEEPING.md` §4 makes age the primary validity evidence).
Resolution order: `**Minted:** YYYY-MM-DD` from the WO body → git first-add date (one repo-wide git
call) → mtime as a last resort, visibly marked `~` as an estimate. The cell renders
`YYYY-MM-DD · <age>d`; rows older than 7 days carry a literal `7d+` badge (word + colour, never
colour alone — the owner is red/green colourblind). "opened within" buttons (7d / 30d / 90d / all)
compose with the bucket chips and the search box. Age is **derived at generation time, never typed
into a WO file**.

---

## 6. Priority when tickets conflict (owner 2026-08-15)

Two rules keep a 900+ Ready column from thrashing the team:

### 6a. Recency window — last 50–100 WOs win on contradiction

The **last ~50–100 work orders by WO number** (both mint blocks: main line *and* UI seat)
are weighted as **valid** when they **contradict** older tickets.

- **Floor (refresh when minting):** as of 2026-08-15, last 100 ≈ **WO-915+**, last 50 ≈ **WO-965+**
  (max on disk was **1020**). Recompute as `max(WO#) − 99` / `max − 49` when the banner moves.
- **On conflict** (same system, opposite acceptance criteria, or a Done RESULT that kills an older
  Ready goal): **implement / believe the newer WO.** Close or partial-scope the older one
  (`CLOSED — SUPERSEDED by WO-NNN` or `READY — PARTIAL: <remainder that does not fight NNN>`).
- **Not a free close-all:** older tickets that are **orthogonal** (no contradiction) still need the
  age/validity check (§6b). Recency only breaks ties.

Live priority stack + known supersession pairs: **`BOARD_NOW.md`** (repo root).

### 6b. Age — >14 days verify first (outside the recency window)

Any open ticket whose file has been idle **>14 days** and sits **below** the last-100 floor is
**VERIFY FIRST** before implementation: still broken at HEAD, partial, done+RESULT, or closed.
Do not treat a multi-month Ready status as a work order.

### 6c. Working stack

`BOARD_NOW.md` is the human-ordered pull list (P0–P3). `BOARD.html` is the full inventory.
When they disagree on *what to do next*, **`BOARD_NOW.md` wins** until regenerated after a
priority session; when they disagree on *status*, the WO file’s `**Status:**` line wins
(regenerate the HTML).

---

## 6d. Owner validations — the DURABLE record (2026-09-03)

The board's **Owner Validation** section is where the PO felt-tests a Fixed ticket and signs it
off. Her sign-off is the only thing that closes a ticket (CLAUDE.md §13), so it is DATA, not
view state.

**The record: `proof/owner-validations.json`** — committed, human-readable, one ticket per line,
keys sorted (so two seats validating different tickets merge without a human, and a same-ticket
conflict is a real disagreement that should stop and be read). Keyed by work-order **filename**,
because this repo has duplicate WO numbers and the friendly label would make two unrelated files
share one sign-off. Full reasoning lives in the header of `tools/owner_validations.py`.

**⛔ IT IS NOT BUILD-SCOPED, and never becomes so again.** The old code kept sign-offs in browser
`localStorage` under `eoa-owner-validation:<apk build>:<commit sha>`, so **every commit minted a
new key and orphaned every mark she had made** — with the CLI committing hourly, the one person
whose sign-off closes a ticket was losing her work hourly, and the CLI could not see any of it
(hence `tools/board_close_validated.py`, which scraped Chrome's LevelDB out of her user profile).
A sign-off is a judgement about a **fix** ("the wolf routes correctly now"), not about a binary;
it does not stop being true because a doc got committed. Provenance is kept *inside* each entry
(`at` + `build`), so "was this signed off before the current APK?" stays answerable per ticket
instead of being force-answered "all of it is stale" once an hour.

**How a mark gets from her phone into the record** (a browser cannot write to the repo):

```
BOARD.html > Owner Validation > "Export for the CLI" > Copy   (or "Save as file")
python tools/board_build.py --ingest -        # paste it; or --ingest <file>
python tools/board_build.py                   # rebuild; the marks now render from disk
```

Tradeoff, stated plainly: **one manual hand-off**. It needs no server, no auth and no network, it
works on a phone over `file://` or any static host, and it cannot lose a mark — which a write
endpoint reachable from a phone browser could not match without new infrastructure to secure.
Marks she has not exported yet live only in that browser, and the export block says so.

- `--ingest` is the **only** writer of the record. A rebuild has **no write path** to it at all.
- Newest `at` wins on merge, so a stale paste from a second device cannot overwrite a newer mark.
- An **unreadable** record ABORTS the rebuild (`VALIDATIONS_PARSE_FAIL`) rather than rendering
  "0 verified" over corrupt bytes — which would look normal and invite her to redo signed-off work.
- Marker: every run prints `VALIDATIONS_OK <n> recorded, <m> validated, preserved across rebuild`.
  Judge it by marker presence on a fresh log, never the exit code (CLAUDE.md §8/§16).
- Self-check: `python tools/board_validation_roundtrip_test.py` → `VALIDATION_ROUNDTRIP_OK`. It
  proves a rebuild preserves a sign-off **and** proves its own assertions have teeth by stubbing
  the read path back to the old always-empty behaviour and requiring the failure to be caught.
- A validated row is distinguishable **without colour** (the owner is red/green colourblind): the
  word `[X] VALIDATED`, the button label flipping to `Validated`, and the row sinking to the
  bottom of its group. All three are server-rendered, so they show with JavaScript off.
- One-time in-page migration sweeps any leftover `eoa-owner-validation:*` key into the durable
  overlay and reports what it recovered. Best effort by nature — it can only reach keys in the
  same browser and origin, and it never deletes the old keys.
- Marking in the browser still changes **no** `**Status:**` line. The board build applies it — see
  §6e.

## 6e. The CLOSE pass — Pass + Validated becomes CLOSED, on the next board build (WO-1355)

Owner ruling 2026-09-03: *"i test and sign off in the owner validation section when you do board
next you flip all passed and validated to closed"* — and, defining the state she signs off FROM:
*"once you move to device for testing gets moved to fixed"*.

```
new issue -> ticket -> assign an SME -> check in when complete
          -> ON HER DEVICE = FIXED            (not "code complete", not "committed")
          -> she signs off in Owner Validation (Passed + Validated)
          -> the NEXT board build flips those to CLOSED
```

**It runs inside `python tools/board_build.py` itself** (`tools/board_close_pass.py`, called before
the work orders are parsed, so the page that run writes already shows the closes). It is not a
second command, because it kept being forgotten and she had to ask twice — CLAUDE.md §16 settles
that shape: *a gate whose remedy is "a human remembers a second command" is not a gate*.

The pass REWRITES `**Status:**` lines from a data file, so the rules are the design:

1. **Both signals or nothing** — `verdict == "Pass"` **and** `validated == true`. Fail, Needs Work,
   a blank verdict, a Pass never validated, and a validated entry with no verdict all close
   nothing and are counted as *held*. An **unrecognised** verdict cannot even reach the pass:
   `owner_validations.normalize()` coerces anything it does not know to `""`.
2. **Only a FIXED ticket is eligible** — Fixed means "it reached her device", the only state a
   felt-test sign-off can validly follow. READY / SPEC / BLOCKED / DONE are never closed by a
   stale mark. Eligibility is read through `board_build.classify_status`, the board's own status
   vocabulary, so the closer and the Fixed bucket can never disagree.
3. **Never resurrect, never re-stamp** — an already-CLOSED ticket is left alone. Ten runs produce
   the same bytes as one.
4. **The existing status text survives verbatim** — the stamp is prepended and the old line is
   carried after `PRIOR STATUS:` (already this repo's convention for historical status prose, and
   the marker `status_contradiction` splits on, so preserving the body cannot manufacture a false
   defect). Those FIXED lines hold the real engineering record; erasing them would destroy exactly
   what the board exists to keep.
5. **Auditable** — the stamp carries the entry's `at` and `build`, so which sign-off on which build
   closed a ticket is readable off the status line.
6. **A malformed record ABORTS the pass** — `VALIDATIONS_PARSE_FAIL` + `BOARD_CLOSE_FAIL`, nothing
   written. Never a partial close, never a silent zero reported as success.
7. **A validation naming a missing WO file is reported**, never dropped.

- Marker: `BOARD_CLOSE_OK closed <n>, held <n>, already-closed <n>, missing <n>` (or
  `BOARD_CLOSE_FAIL ...`). Judge it on a fresh log, never by the exit code.
- Opt-out for tests and emergencies only: `--no-close`, or `EOA_BOARD_CLOSE=0`, which prints
  `BOARD_CLOSE_SKIPPED`. It is deliberately an opt-OUT; an opt-IN would restore the forgotten-
  second-command hole.
- `tools/board_close_validated.py` is now only the **bounce** (Fail / Needs Work back to READY,
  with her note) plus the legacy Chrome-LevelDB salvage. It calls the same one close module rather
  than keeping a second copy of the rules — a drifted copy of a status rewriter rewrites live
  tickets.
- Covered by `tools/board_validation_roundtrip_test.py` stages 5-9 against a throwaway
  `WorkOrders/` (`EOA_WO_DIR`): both-signals, idempotency across three runs, a CLOSED ticket
  untouched, a non-FIXED ticket never closed, the body preserved, abort on a corrupt record — and
  four source mutations that each must be caught.

## 7. What this board is NOT

- Not a service, not CI, not a database. It is one Python file and one HTML output.
- Not a place to record work: the WO markdown is. The board only *shows* what the markdown says.
- Not a Notion replacement to be re-mirrored anywhere. The retired mirror's failure mode — a second
  copy nobody could reach — is exactly what "derive it in 2 seconds" prevents.

## 8. Related

- `BOARD_NOW.md` — prioritized pull list + recency supersession table
- `CLI_LANES_WO_NUMBERS.md` — the **sole** numbering authority (bump the banner in the same edit as a mint)
- `WorkOrders/` — the data
- `tools/board_build.py` — the generator (and `--ingest`, the record's only writer)
- `proof/owner-validations.json` — the owner's durable felt-test sign-offs (§6d)
- `tools/board_close_pass.py` — the Pass+Validated -> CLOSED pass the board build runs (§6e)
- `tools/owner_validations.py` — the record's shape, merge rule and reasoning
- `tools/board_validation_roundtrip_test.py` — proves a rebuild cannot lose a sign-off
- `docs/HANDOVER.md`, `SESSION_CANON_LOADER.md` — session boot, which instructs the regen
