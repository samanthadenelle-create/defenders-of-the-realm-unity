# WORK ORDER 1011 — Board workflow: acclimate the CLI to BOARD.html as the live board (source-of-truth discipline)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-08 (UI seat) — number from `CLI_LANES_WO_NUMBERS.md` banner (bumped 1011 → 1012 in the same edit)
**Lane:** Process/tooling. Touches docs + one script + (optionally) the boot skill. **No game code.**
**Provenance:** owner ruling 2026-08-08 — Notion is RETIRED as the board. Root cause: the Notion mirror
was reachable by NO seat (the CLI has no MCP auth and headless runs cannot OAuth; the UI connector was
OAuth'd to a different personal workspace, so the DB 404'd). Items were being lost to an unreachable,
hand-mirrored board. The replacement is a DERIVED board: `BOARD.html`, generated from the repo by
`tools/board_build.py`. CLAUDE.md §2 and the `⚠ SUPERSEDED` banner on `NOTION_SOURCE_OF_TRUTH.md`
already record the ruling; THIS WO makes the CLI actually live on it.

---

## 1. The model (one paragraph the CLI must internalize)

**The repo IS the board.** `WorkOrders/*.md` status lines + `.RESULT.md` markers + the
`CLI_LANES_WO_NUMBERS.md` banner ARE the data; `BOARD.html` is a 2-second derived VIEW of them
(`python tools/board_build.py`). There is nothing to sync, mirror, or update in a second system —
which means the board is exactly as truthful as the status lines in the WO files. Status hygiene is
therefore not paperwork; it IS the board.

---

## 2. What the CLI adopts (the working protocol)

1. **Regenerate at session boot and before any board read:** `python tools/board_build.py`. Never read
   a stale `BOARD.html`; never hand-edit it (it is generated output).
2. **Status lines are updated IN THE SAME COMMIT as the work** — the §15 canon rule extended to
   statuses. Finishing an implementation = flip the WO's `**Status:**` line + write the `.RESULT.md`
   in that commit. A completed WO whose file still says READY is a lie the board will faithfully render.
3. **Never mirror to Notion.** No Notion writes, no Notion reads, regardless of what older docs say —
   `NOTION_SOURCE_OF_TRUTH.md` is superseded; any doc still pointing at Notion gets a `STALE:` flag when
   touched.
4. **The parser's status vocabulary is canon** (first `**Status:**` line in the file; keyword priority):
   - `SUPERSEDED` / `CLOSED` / `CANCELLED` → **Closed**
   - `DONE` / `IMPLEMENTED` / `COMPLETE` (or a `.RESULT.md` exists) → **Done**
   - `BLOCKED` → **Blocked**
   - `READY` (any phrasing containing it) → **Ready**
   - `DRAFT` / `SPEC` / `NOT STARTED` / `PROPOSAL` → **Spec**
   - anything else → **Unlabeled** (treat as a defect in the WO file — fix the line)
   Compound truth reads left-to-right by that priority (e.g. "DELIVERED — defect pass open" contains
   neither DONE nor READY → Unlabeled: word it "IN PROGRESS — defect pass open, DELIVERED core" is
   still Unlabeled — prefer including one canonical keyword, e.g. "READY TO IMPLEMENT (defect pass)"
   or "DONE (pending felt-verify)", so the row lands in the right bucket).

## 3. Deliverables

- **`docs/BOARD.md`** — a one-page usage doc stating §1's model, the §2 protocol, and the §2.4
  vocabulary table verbatim, plus "how to add a new status keyword" (edit `bucket_of` in
  `tools/board_build.py` AND the table in the same commit).
- **Boot wiring:** add the regen command to the session boot path the CLI actually follows —
  `SESSION_CANON_LOADER.md` + `docs/HANDOVER.md` (and the `run-defenders` skill's boot notes if it has
  them). One line each: "Regenerate the board: `python tools/board_build.py` (2 s)."
- **Index updates:** `PROJECT_INDEX.md` gains rows for `BOARD.html` (generated — do not edit) and
  `tools/board_build.py`; `docs/README.md` gains `docs/BOARD.md`.
- **Sweep legacy pointers:** grep the load-bearing set for "Notion" and banner/`STALE:`-flag any doc
  still calling it the board (do NOT rewrite frozen dated ledgers — banner only, §15).
- **(Optional, if trivial)** a `--check` flag on `board_build.py` that exits nonzero when any WO is
  **Unlabeled**, so the check-in gate can enforce vocabulary. Do not build more CI than this.

## 4. Phase 2 — the status-debt sweep (separate commit wave, same WO)

First generation surfaced **~516 WOs claiming READY TO IMPLEMENT** — years of drift now visible.
Reconcile them the §12 way (from evidence, never assumption):
- Evidence per WO: a matching `.RESULT.md`, HEAD commits referencing the number, or the feature
  verifiably in the tree → flip to `DONE (reconciled <date> from the tree, NOT felt-verified)`.
- Superseded by a later WO / removed system → `CLOSED — SUPERSEDED` with the pointer.
- Genuinely still pending → leave READY.
- Frozen/dated WOs stay body-frozen — status line + banner only (§15).
- Batch in slices (e.g. 50–100 per commit) with the board regenerated after each slice; the shrinking
  Ready count is the progress meter. Target: Ready reflects the REAL actionable queue.

## 5. Acceptance criteria

- [ ] `docs/BOARD.md` exists with the model + protocol + vocabulary (§2 verbatim).
- [ ] Boot docs (`SESSION_CANON_LOADER.md`, `docs/HANDOVER.md`) instruct the regen; `PROJECT_INDEX.md`
      and `docs/README.md` index the new files.
- [ ] No load-bearing doc still names Notion as the live board without a supersession banner.
- [ ] A fresh session following only the boot docs produces a current `BOARD.html` without further
      instruction.
- [ ] Phase 2: Ready-bucket count reduced to the real actionable queue; every flip evidence-cited in
      its status line; zero Unlabeled rows at the end of the sweep.
- [ ] `COMPILE_GATE_OK` not required (no game code) — but run `python tools/board_build.py` clean as
      the gate for this WO, plus `REGRESSION_OK` untouched (prove no game files changed).

## 6. What NOT to touch

- `BOARD.html` by hand (generated), the WO numbering banner beyond normal minting, game code/scenes.
- Do not resurrect any Notion tooling. Do not build a web service/CI pipeline around the board — it is
  a 2-second local script by design.
