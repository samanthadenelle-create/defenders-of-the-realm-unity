# docs/_archive — Frozen History (moved 2026-07-12)

This folder holds **frozen, point-in-time documents** moved here to declutter the repo
root and `docs/`. **Nothing was deleted.** Every file was relocated with `git mv`, so full
history is intact and any file can be recovered:

```
git log --follow -- docs/_archive/<sub>/<file>.md   # see history
git mv docs/_archive/<sub>/<file>.md <original-path> # restore
```

## What's in here
Dated snapshots and completed one-offs that are no longer live canon:
- Overnight / morning / shift-change reports, session handovers & resumes
- Dated backlog reconciliations, queue-health, board-status, PM boards
- Completed dated RCA / audit / triage / census / ledger docs
- Superseded `CANON_GROUND_TRUTH_*` (the **newest** stays at repo root)
- Playtest cards & checklists from past sessions
- The retired `SESSION_START_HERE.md` (self-flagged RETIRED)

## Layout
- `root/` — files that lived at the repo root
- `docs/` — files that lived under `docs/`

## What is NOT here
The load-bearing canon set (read every session) stayed exactly where it was:
`START_HERE.md`, `KEY_FACTS.md`, `SESSION_CANON_LOADER.md`, `CLAUDE.md`, `PREFLIGHT_GATE.md`,
`SAMANTHA.md`, `PROJECT_INDEX.md`, the newest `CANON_GROUND_TRUTH_2026-07-12.md`,
`PIPELINE_STATE.md`, `CLI_LANES_WO_NUMBERS.md`, `MASTER_PIPELINES_BACKLOG_2026-06-06.md`
(WO-numbering authority), and the `docs/` architecture / catalog / SME hubs.

See `CLEANUP_MANIFEST_2026-07-12.md` at the repo root for the full moved-file ledger and
the UNSURE list left in place for the owner to rule on.
