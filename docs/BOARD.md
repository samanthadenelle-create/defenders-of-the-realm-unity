# BOARD.md — how the work-order board works (WO-1011)

**The board is `BOARD.html` at the repo root. It is GENERATED. Never hand-edit it.**

```
python tools/board_build.py          # ~2 s — regenerate the view
python tools/board_build.py --check  # same, but exits 1 if any WO is Unlabeled
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

## 7. What this board is NOT

- Not a service, not CI, not a database. It is one Python file and one HTML output.
- Not a place to record work: the WO markdown is. The board only *shows* what the markdown says.
- Not a Notion replacement to be re-mirrored anywhere. The retired mirror's failure mode — a second
  copy nobody could reach — is exactly what "derive it in 2 seconds" prevents.

## 8. Related

- `BOARD_NOW.md` — prioritized pull list + recency supersession table
- `CLI_LANES_WO_NUMBERS.md` — the **sole** numbering authority (bump the banner in the same edit as a mint)
- `WorkOrders/` — the data
- `tools/board_build.py` — the generator
- `docs/HANDOVER.md`, `SESSION_CANON_LOADER.md` — session boot, which instructs the regen
